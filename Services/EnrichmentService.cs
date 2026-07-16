using System.Collections.Concurrent;
using CfMvc.Models;

namespace CfMvc.Services;

/// <summary>
/// Resolves the "twin round" problem-list overrides described in TableBuilder. Results are
/// cached in a process-wide dictionary for the app's lifetime (finished contests never change),
/// so only the first request that touches a given flagged contest pays the network cost.
/// </summary>
public class EnrichmentService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private static readonly ConcurrentDictionary<int, List<CfProblem>> Overrides = new();
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public EnrichmentService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    /// <summary>
    /// Ensures every flagged contest is resolved, fetching any not-yet-cached ones from
    /// contest.standings (throttled to be polite to Codeforces' API).
    /// </summary>
    public async Task<Dictionary<int, List<CfProblem>>> ResolveAsync(List<int> flaggedContestIds)
    {
        var missing = flaggedContestIds.Where(id => !Overrides.ContainsKey(id)).Distinct().ToList();

        if (missing.Count > 0)
        {
            // Only one resolution runs app-wide at a time so concurrent page loads
            // don't hammer the CF API in parallel.
            await Gate.WaitAsync();
            try
            {
                // Re-check inside the lock — another thread may have resolved while we waited.
                missing = missing.Where(id => !Overrides.ContainsKey(id)).ToList();
                if (missing.Count > 0)
                {
                    using var scope = _scopeFactory.CreateScope();
                    var api = scope.ServiceProvider.GetRequiredService<CodeforcesApiService>();

                    foreach (var contestId in missing)
                    {
                        try
                        {
                            var problems = await api.FetchContestProblemsAsync(contestId);
                            Overrides[contestId] = problems;
                        }
                        catch
                        {
                            // Skip contests that fail — they simply keep showing whatever
                            // problemset.problems already gave them.
                        }
                        await Task.Delay(300);
                    }
                }
            }
            finally
            {
                Gate.Release();
            }
        }

        return flaggedContestIds
            .Where(Overrides.ContainsKey)
            .ToDictionary(id => id, id => Overrides[id]);
    }

    /// <summary>Snapshot of whatever overrides are already cached, without triggering any fetch.</summary>
    public Dictionary<int, List<CfProblem>> GetAvailable(List<int> flaggedContestIds) =>
        flaggedContestIds
            .Where(Overrides.ContainsKey)
            .ToDictionary(id => id, id => Overrides[id]);
}
