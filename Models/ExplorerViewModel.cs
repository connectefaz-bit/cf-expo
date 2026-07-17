namespace CfMvc.Models;

public class ExplorerViewModel
{
    public string? Handle { get; set; }
    public string Search { get; set; } = "";
    public string TypeFilter { get; set; } = "All";
    public string? Error { get; set; }
    public int SolvedCount { get; set; }
    public List<TableRow> Rows { get; set; } = new();
    /// <summary>Maximum problem position across all displayed rows — drives the number of columns rendered.</summary>
    public int MaxColumns { get; set; }
    /// <summary>True when the database has no contest data yet (first run before the initial sync completes).</summary>
        public bool IsDbEmpty { get; set; }
    /// <summary>True when at least one row has problems beyond the 12-column cap — drives the overflow "+N" column.</summary>
    public bool HasOverflow { get; set; }

    // ── Pagination ──────────────────────────────────────────────────────────
    public static readonly int[] PageSizeOptions = [30, 50, 100];
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public int TotalRows { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalRows / PageSize) : 1;
}
