using CfMvc.Models;

namespace CfMvc.Services;

public static class TableBuilder
{
    // ── Position grouping ────────────────────────────────────────────────────

    /// <summary>
    /// Given the problems for a single contest, returns a dict keyed by 1-based position.
    /// <para>
    /// When problems carry explicit <see cref="CfProblem.Position"/> values (loaded from DB or
    /// contest.standings) those are used directly. Otherwise positions are assigned by
    /// alphabetical sort of Index — ordinal comparison gives the correct CF contest order
    /// for all naming schemes: A &lt; B &lt; C &lt; C1 &lt; C2 &lt; D &lt; D1 &lt; D2 &lt; E …
    /// </para>
    /// </summary>
    private static Dictionary<int, CfProblem> GroupByPosition(IEnumerable<CfProblem> problems)
    {
        var list = problems.ToList();
        var result = new Dictionary<int, CfProblem>();

        bool hasExplicit = list.Any(p => p.Position > 0);

        if (hasExplicit)
        {
            foreach (var p in list.Where(p => p.Position > 0))
                result.TryAdd(p.Position, p);
        }
        else
        {
            var sorted = list.OrderBy(p => p.Index, StringComparer.Ordinal).ToList();
            for (int i = 0; i < sorted.Count; i++)
                result[i + 1] = sorted[i];
        }

        return result;
    }

    /// <summary>Groups every known problem by contestId, then by 1-based position within contest.</summary>
    public static Dictionary<int, Dictionary<int, CfProblem>> GroupProblemsByContest(List<CfProblem> problems)
    {
        return problems
            .Where(p => p.ContestId.HasValue)
            .GroupBy(p => p.ContestId!.Value)
            .ToDictionary(g => g.Key, g => GroupByPosition(g));
    }

    public static int CountContestProblems(Dictionary<int, Dictionary<int, CfProblem>> byContest, int contestId) =>
        byContest.TryGetValue(contestId, out var pos) ? pos.Count : 0;

    /// <summary>Set of "contestId/index" keys the handle has an "OK" verdict on.</summary>
    public static HashSet<string> BuildSolvedSet(List<CfSubmission> submissions)
    {
        var solved = new HashSet<string>();
        foreach (var s in submissions)
        {
            if (s.Verdict != "OK") continue;
            if (s.Problem.ContestId is not int contestId) continue;
            solved.Add($"{contestId}/{s.Problem.Index}");
        }
        return solved;
    }

    /// <summary>
    /// Finds contests that likely lost shared problems to a simultaneous split-round sibling
    /// (e.g. "Round N (Div. 1)" / "Round N (Div. 2)" sharing several problems, where
    /// problemset.problems only attributes each shared problem to one of the two contest IDs).
    /// </summary>
    public static List<int> FindContestsNeedingEnrichment(List<CfContest> contests, List<CfProblem> problems)
    {
        var byContest = GroupProblemsByContest(problems);
        var byStart = new Dictionary<long, List<CfContest>>();
        foreach (var contest in contests)
        {
            if (contest.StartTimeSeconds is not long start) continue;
            if (!byStart.TryGetValue(start, out var list))
            {
                list = new List<CfContest>();
                byStart[start] = list;
            }
            list.Add(contest);
        }

        var flagged = new List<int>();
        foreach (var group in byStart.Values)
        {
            if (group.Count < 2) continue;
            var counts = group.Select(c => CountContestProblems(byContest, c.Id)).ToList();
            var max = counts.Max();
            if (max == 0) continue;
            for (var i = 0; i < group.Count; i++)
            {
                if (counts[i] > 0 && counts[i] < max) flagged.Add(group[i].Id);
            }
        }
        return flagged;
    }

    /// <summary>
    /// Builds one row per finished contest with position-keyed cells, newest contest first.
    /// <para>
    /// Column headers show ordinal position (1st, 2nd, 3rd…); each cell displays the actual
    /// Codeforces problem index (A, B, C1, C2, D1, D2, …) so split-round contests render correctly.
    /// </para>
    /// <para>
    /// When <paramref name="noHandle"/> is true all present problems are marked
    /// <see cref="CellStatus.Available"/> (neutral — no solved/unsolved colour).
    /// </para>
    /// </summary>
    public static List<TableRow> BuildTable(
        List<CfProblem> problems,
        List<CfContest> contests,
        HashSet<string> solved,
        bool noHandle = false,
        Dictionary<int, List<CfProblem>>? overrides = null)
    {
        var byContest = GroupProblemsByContest(problems);
        var rows = new List<TableRow>();

        foreach (var contest in contests)
        {
            Dictionary<int, CfProblem>? positions;

            if (overrides is not null && overrides.TryGetValue(contest.Id, out var overrideList))
                positions = GroupByPosition(overrideList);
            else
                byContest.TryGetValue(contest.Id, out positions);

            if (positions is null || positions.Count == 0) continue;

            int maxPos = positions.Keys.Max();
            var cells = new Dictionary<int, TableCell>();

            for (int pos = 1; pos <= maxPos; pos++)
            {
                if (!positions.TryGetValue(pos, out var problem))
                {
                    cells[pos] = new TableCell { Status = CellStatus.Missing };
                    continue;
                }

                var key = $"{contest.Id}/{problem.Index}";
                cells[pos] = new TableCell
                {
                    Status = noHandle ? CellStatus.Available
                           : solved.Contains(key) ? CellStatus.Solved
                           : CellStatus.Unsolved,
                    Problem = problem,
                    Index = problem.Index,
                };
            }

            rows.Add(new TableRow
            {
                Contest = contest,
                Types = ContestTypeHelper.GetContestTypes(contest.Name),
                Cells = cells,
            });
        }

        rows.Sort((a, b) => (b.Contest.StartTimeSeconds ?? 0).CompareTo(a.Contest.StartTimeSeconds ?? 0));
        return rows;
    }
}
