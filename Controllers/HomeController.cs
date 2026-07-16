using CfMvc.Models;
using CfMvc.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Caching.Memory;

namespace CfMvc.Controllers;

public class HomeController : Controller
{
    private readonly CodeforcesApiService _api;
    private readonly EnrichmentService _enrichment;
    private readonly DatabaseQueryService _dbQuery;
    private readonly IMemoryCache _cache;

    public HomeController(
        CodeforcesApiService api,
        EnrichmentService enrichment,
        DatabaseQueryService dbQuery,
        IMemoryCache cache)
    {
        _api = api;
        _enrichment = enrichment;
        _dbQuery = dbQuery;
        _cache = cache;
    }

    /// <summary>
    /// Main explorer page.
    ///
    /// No handle:   Loads all contests and problems directly from the database (no CF API calls).
    ///              Cells are shown in a neutral "Available" colour — no solved/unsolved distinction.
    ///
    /// With handle: Loads contests and problems from the database, then calls user.status ONLY
    ///              to determine solved problems. The user.status result is cached for 30 minutes
    ///              to avoid hammering the CF API on repeat visits.
    ///
    /// Response is output-cached per unique (handle, search, type) combination for 5 minutes,
    /// reducing DB load and render time on repeated page loads.
    /// </summary>
    [OutputCache(Duration = 300, VaryByQueryKeys = ["handle", "search", "type"])]
    [HttpGet("/")]
    public async Task<IActionResult> Index(string? handle, string search = "", string type = "All")
    {
        var model = new ExplorerViewModel
        {
            Handle = handle,
            Search = search,
            TypeFilter = type,
        };

        try
        {
            // Always load base data from the database.
            var (contests, problems) = await _dbQuery.GetAllAsync();
            // IsDbEmpty when there are no problems yet — either fresh install or mid-sync.
            model.IsDbEmpty = problems.Count == 0;

            var solved = new HashSet<string>();
            bool noHandle = string.IsNullOrWhiteSpace(handle);

            if (!noHandle)
            {
                var trimmed = handle!.Trim();
                model.Handle = trimmed;

                // Cache user.status results for 30 minutes to reduce API calls.
                var cacheKey = $"solved:{trimmed.ToLowerInvariant()}";
                if (!_cache.TryGetValue(cacheKey, out List<CfSubmission>? submissions) || submissions is null)
                {
                    submissions = await _api.FetchUserSubmissionsAsync(trimmed);
                    _cache.Set(cacheKey, submissions, TimeSpan.FromMinutes(30));
                }

                solved = TableBuilder.BuildSolvedSet(submissions);
                model.SolvedCount = solved.Count;
            }

            // Apply in-memory enrichment overrides for split-round contests.
            // These are fetched from contest.standings on-demand and cached process-wide.
            // Cap the wait at 3 s so slow first-time enrichment doesn't stall the page.
            var flagged = TableBuilder.FindContestsNeedingEnrichment(contests, problems);
            var resolveTask = _enrichment.ResolveAsync(flagged);
            var completed = await Task.WhenAny(resolveTask, Task.Delay(TimeSpan.FromSeconds(3)));
            var overrides = completed == resolveTask ? await resolveTask : _enrichment.GetAvailable(flagged);

            var rows = TableBuilder.BuildTable(problems, contests, solved, noHandle, overrides);

            model.Rows = rows
                .Where(r => ContestTypeHelper.Matches(r.Contest.Name, type))
                .Where(r => string.IsNullOrEmpty(search)
                         || r.Contest.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
                .ToList();

            const int ColCap = 12;
            var rawMax = model.Rows.Count > 0
                ? model.Rows.Max(r => r.Cells.Count > 0 ? r.Cells.Keys.Max() : 0)
                : 0;
            model.MaxColumns  = Math.Min(rawMax, ColCap);
            model.HasOverflow = rawMax > ColCap;
        }
        catch (Exception ex)
        {
            model.Error = ex.Message;
        }

        return View(model);
    }

    [HttpGet("/Home/Error")]
    public IActionResult Error() => View(new ErrorViewModel { RequestId = HttpContext.TraceIdentifier });
}
