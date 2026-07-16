namespace CfMvc.Models;

/// <summary>Mirrors the "problems" objects returned by the public Codeforces API.</summary>
public class CfProblem
{
    public int? ContestId { get; set; }
    public string? ProblemsetName { get; set; }
    public string Index { get; set; } = "";
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public double? Points { get; set; }
    public int? Rating { get; set; }
    public List<string> Tags { get; set; } = new();
    /// <summary>
    /// 1-based position of this problem within its contest (slot 1 = first on the standings page).
    /// 0 means unset — TableBuilder will assign positions via alphabetical Index sort.
    /// </summary>
    public int Position { get; set; }
}

/// <summary>Mirrors a single entry from contest.list.</summary>
public class CfContest
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Phase { get; set; } = "";
    public bool Frozen { get; set; }
    public long DurationSeconds { get; set; }
    public long? StartTimeSeconds { get; set; }
    public long? RelativeTimeSeconds { get; set; }
}

/// <summary>Mirrors a single entry from user.status.</summary>
public class CfSubmission
{
    public long Id { get; set; }
    public int? ContestId { get; set; }
    public long CreationTimeSeconds { get; set; }
    public CfSubmissionProblem Problem { get; set; } = new();
    public string? Verdict { get; set; }
}

public class CfSubmissionProblem
{
    public int? ContestId { get; set; }
    public string Index { get; set; } = "";
    public string Name { get; set; } = "";
    public int? Rating { get; set; }
    public List<string> Tags { get; set; } = new();
}

/// <summary>Generic envelope every Codeforces API endpoint responds with.</summary>
public class CfApiResponse<T>
{
    public string Status { get; set; } = "";
    public T? Result { get; set; }
    public string? Comment { get; set; }
}

/// <summary>contest.standings cares about the "problems" array for our purposes.</summary>
public class CfStandingsResult
{
    public List<CfProblem> Problems { get; set; } = new();
}

public enum CellStatus
{
    Solved,
    Unsolved,
    Missing,
    /// <summary>Problem exists but no handle was provided — displayed in neutral colour.</summary>
    Available,
}

public class TableCell
{
    public CellStatus Status { get; set; } = CellStatus.Missing;
    public CfProblem? Problem { get; set; }
    /// <summary>Actual CF index label to display in the cell (A, B, C1, C2, D1, …).</summary>
    public string? Index { get; set; }
}

public class TableRow
{
    public CfContest Contest { get; set; } = new();
    public List<string> Types { get; set; } = new();
    /// <summary>Cells keyed by 1-based position within the contest.</summary>
    public Dictionary<int, TableCell> Cells { get; set; } = new();
}
