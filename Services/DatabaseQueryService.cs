using System.Text.Json;
using CfMvc.Data;
using CfMvc.Models;
using Microsoft.EntityFrameworkCore;

namespace CfMvc.Services;

/// <summary>
/// Loads contests and problems from the local database and maps them to the
/// shared <see cref="CfContest"/> / <see cref="CfProblem"/> model types used
/// throughout the rest of the application.
/// </summary>
public class DatabaseQueryService
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);
    private readonly AppDbContext _db;

    public DatabaseQueryService(AppDbContext db) => _db = db;

    /// <summary>
    /// Returns all finished contests and their problems from the database.
    /// Problems include their stored 1-based <see cref="CfProblem.Position"/> values
    /// so TableBuilder can build position-keyed cells without needing to sort by Index.
    /// </summary>
    public async Task<(List<CfContest> Contests, List<CfProblem> Problems)> GetAllAsync()
    {
        var contestEntities = await _db.Contests
            .AsNoTracking()
            .OrderByDescending(c => c.StartTimeSeconds)
            .ToListAsync();

        var problemEntities = await _db.Problems
            .AsNoTracking()
            .ToListAsync();

        var contests = contestEntities.Select(c => new CfContest
        {
            Id = c.Id,
            Name = c.Name,
            Type = c.Type,
            Phase = c.Phase,
            DurationSeconds = c.DurationSeconds,
            StartTimeSeconds = c.StartTimeSeconds,
        }).ToList();

        var problems = problemEntities.Select(p =>
        {
            List<string> tags;
            try { tags = JsonSerializer.Deserialize<List<string>>(p.TagsJson, JsonOpts) ?? new(); }
            catch { tags = new(); }

            return new CfProblem
            {
                ContestId = p.ContestId,
                Index = p.Index,
                Position = p.Position,
                Name = p.Name,
                Rating = p.Rating,
                Tags = tags,
            };
        }).ToList();

        return (contests, problems);
    }

    /// <summary>True when the database contains at least one contest record.</summary>
    public Task<bool> HasDataAsync() => _db.Contests.AnyAsync();
}
