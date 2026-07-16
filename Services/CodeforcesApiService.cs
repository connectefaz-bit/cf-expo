using System.Text.Json;
using CfMvc.Models;
using Microsoft.Extensions.Caching.Memory;

namespace CfMvc.Services;

/// <summary>
/// Thin client for the official Codeforces API (https://codeforces.com/apiHelp),
/// called server-side from ASP.NET Core instead of the browser.
/// </summary>
public class CodeforcesApiService
{
    private const string CfApiBase = "https://codeforces.com/api";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;

    public CodeforcesApiService(HttpClient http, IMemoryCache cache)
    {
        _http = http;
        _cache = cache;
    }

    private async Task<T> FetchAsync<T>(string path)
    {
        using var response = await _http.GetAsync($"{CfApiBase}{path}");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        var envelope = JsonSerializer.Deserialize<CfApiResponse<T>>(body, JsonOptions)
            ?? throw new InvalidOperationException("Codeforces API returned an empty response.");
        if (envelope.Status != "OK" || envelope.Result is null)
            throw new InvalidOperationException(envelope.Comment ?? "Codeforces API returned an error.");
        return envelope.Result;
    }

    /// <summary>Every problem ever published on Codeforces. Cached for 1 hour since it rarely changes.</summary>
    public Task<List<CfProblem>> FetchAllProblemsAsync() =>
        _cache.GetOrCreateAsync("cf:problems", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
            var result = await FetchAsync<CfStandingsResult>("/problemset.problems");
            return result.Problems;
        })!;

    /// <summary>Every non-gym contest. Cached for 15 minutes so upcoming/running contests stay fresh.</summary>
    public Task<List<CfContest>> FetchAllContestsAsync() =>
        _cache.GetOrCreateAsync("cf:contests", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15);
            return await FetchAsync<List<CfContest>>("/contest.list?gym=false");
        })!;

    /// <summary>
    /// All submissions ever made by a handle. The controller caches this result with a 30-minute TTL.
    /// Only user.status is called for handle lookups — all other data comes from the database.
    /// </summary>
    public async Task<List<CfSubmission>> FetchUserSubmissionsAsync(string handle)
    {
        var trimmed = handle.Trim();
        if (trimmed.Length == 0) return new List<CfSubmission>();
        return await FetchAsync<List<CfSubmission>>($"/user.status?handle={Uri.EscapeDataString(trimmed)}");
    }

    /// <summary>
    /// The authoritative problem list for a single contest from contest.standings.
    /// Problems are returned with <see cref="CfProblem.Position"/> set to their 1-based
    /// array index in the standings (which is the true contest order).
    /// Used to patch split-round contests that problemset.problems under-reports.
    /// </summary>
    public async Task<List<CfProblem>> FetchContestProblemsAsync(int contestId)
    {
        var cacheKey = $"cf:standings:{contestId}";
        if (_cache.TryGetValue(cacheKey, out List<CfProblem>? cached) && cached is not null)
            return cached;

        var result = await FetchAsync<CfStandingsResult>($"/contest.standings?contestId={contestId}");

        // Assign contestId and 1-based position from standings array order.
        for (int i = 0; i < result.Problems.Count; i++)
        {
            result.Problems[i].ContestId = contestId;
            result.Problems[i].Position = i + 1;
        }

        // Finished contests never change; cache indefinitely for this process lifetime.
        _cache.Set(cacheKey, result.Problems);
        return result.Problems;
    }

    public static string ProblemUrl(int contestId, string index) =>
        $"https://codeforces.com/contest/{contestId}/problem/{index}";
}
