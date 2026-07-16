using System.Text.Json;
using CfMvc.Data;
using CfMvc.Models;
using Microsoft.EntityFrameworkCore;

namespace CfMvc.Services;

/// <summary>
/// Background service that keeps the database in sync with Codeforces.
/// Runs once 15 s after startup, then daily at midnight UTC.
///
/// Algorithm:
///   1. Fetch contest.list from CF.
///   2. Detect contests not yet stored in the DB.
///   3. If any new contests exist, fetch problemset.problems.
///   4. Upsert new contests and their problems (assigns 1-based positions via
///      alphabetical Index sort — identical to CF contest order for all naming schemes).
///   5. Enrich split-round contests among the new batch via contest.standings.
/// </summary>
public class SyncService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SyncService> _logger;

    public SyncService(IServiceScopeFactory scopeFactory, ILogger<SyncService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Give the app time to finish startup before hitting the CF API.
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        await RunSyncAsync(stoppingToken);

        // Daily at midnight UTC.
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            var nextMidnight = now.Date.AddDays(1);
            var delay = nextMidnight - now;
            _logger.LogInformation("Next CF sync scheduled at {Time} UTC", nextMidnight);
            await Task.Delay(delay, stoppingToken);
            await RunSyncAsync(stoppingToken);
        }
    }

    private async Task RunSyncAsync(CancellationToken ct)
    {
        _logger.LogInformation("CF sync started.");
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var api = scope.ServiceProvider.GetRequiredService<CodeforcesApiService>();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // 1. Fetch contest list and determine which are new.
            var allContests = await api.FetchAllContestsAsync();
            var finished = allContests.Where(c => c.Phase == "FINISHED").ToList();

            var existingIds = (await db.Contests.Select(c => c.Id).ToListAsync(ct)).ToHashSet();
            var newContests = finished.Where(c => !existingIds.Contains(c.Id)).ToList();

            _logger.LogInformation("CF sync: {Total} finished contests, {New} new.", finished.Count, newContests.Count);

            // 2. Upsert contests (name/phase can change for recently-finished ones).
            foreach (var contest in finished)
            {
                var existing = await db.Contests.FindAsync(new object[] { contest.Id }, ct);
                if (existing is null)
                {
                    db.Contests.Add(new ContestEntity
                    {
                        Id = contest.Id,
                        Name = contest.Name,
                        Type = contest.Type,
                        Phase = contest.Phase,
                        DurationSeconds = contest.DurationSeconds,
                        StartTimeSeconds = contest.StartTimeSeconds,
                        SyncedAt = DateTime.UtcNow,
                    });
                }
                else
                {
                    existing.Name = contest.Name;
                    existing.Phase = contest.Phase;
                    existing.SyncedAt = DateTime.UtcNow;
                }
            }
            await db.SaveChangesAsync(ct);

            // 3. If there are new contests, fetch all problems and store those for new contests.
            if (newContests.Count > 0)
            {
                var allProblems = await api.FetchAllProblemsAsync();

                // Assign positions via TableBuilder (alphabetical sort → CF contest order).
                var byContest = TableBuilder.GroupProblemsByContest(allProblems);
                var newIds = newContests.Select(c => c.Id).ToHashSet();

                foreach (var contestId in newIds)
                {
                    if (!byContest.TryGetValue(contestId, out var positionMap)) continue;

                    foreach (var (position, problem) in positionMap)
                    {
                        var exists = await db.Problems
                            .FindAsync(new object[] { contestId, problem.Index }, ct);
                        if (exists is null)
                        {
                            db.Problems.Add(new ProblemEntity
                            {
                                ContestId = contestId,
                                Index = problem.Index,
                                Position = position,
                                Name = problem.Name,
                                Rating = problem.Rating,
                                TagsJson = JsonSerializer.Serialize(problem.Tags),
                            });
                        }
                    }
                }
                await db.SaveChangesAsync(ct);
                _logger.LogInformation("CF sync: problems stored for {Count} new contests.", newContests.Count);

                // 4. Enrich split-round contests in the new batch via contest.standings.
                var newCfContests = finished.Where(c => newIds.Contains(c.Id)).ToList();
                var flagged = TableBuilder.FindContestsNeedingEnrichment(newCfContests, allProblems);
                if (flagged.Count > 0)
                {
                    _logger.LogInformation("CF sync: enriching {Count} split-round contests.", flagged.Count);
                    await EnrichAndPersistAsync(db, api, flagged, ct);
                }
            }

            _logger.LogInformation("CF sync completed successfully.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("CF sync cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CF sync failed.");
        }
    }

    /// <summary>
    /// Fetches contest.standings for each flagged split-round contest, replaces its
    /// problems in the DB with the authoritative ordered list, and marks it as enriched.
    /// </summary>
    private static async Task EnrichAndPersistAsync(
        AppDbContext db,
        CodeforcesApiService api,
        List<int> contestIds,
        CancellationToken ct)
    {
        foreach (var contestId in contestIds)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                // FetchContestProblemsAsync already sets Position = array index + 1.
                var problems = await api.FetchContestProblemsAsync(contestId);

                // Replace the basic problems with the authoritative standings list.
                var existing = db.Problems.Where(p => p.ContestId == contestId);
                db.Problems.RemoveRange(existing);

                for (int i = 0; i < problems.Count; i++)
                {
                    var p = problems[i];
                    db.Problems.Add(new ProblemEntity
                    {
                        ContestId = contestId,
                        Index = p.Index,
                        Position = i + 1,
                        Name = p.Name,
                        Rating = p.Rating,
                        TagsJson = JsonSerializer.Serialize(p.Tags),
                    });
                }

                var contestEntity = await db.Contests.FindAsync(new object[] { contestId }, ct);
                if (contestEntity is not null) contestEntity.IsEnriched = true;

                await db.SaveChangesAsync(ct);

                // Be polite to Codeforces' API rate limits.
                await Task.Delay(500, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Non-fatal: the basic problem data already in DB is shown instead.
                Console.Error.WriteLine($"[SyncService] Failed to enrich contest {contestId}: {ex.Message}");
            }
        }
    }
}
