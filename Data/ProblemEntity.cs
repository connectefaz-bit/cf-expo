namespace CfMvc.Data;

public class ProblemEntity
{
    /// <summary>Codeforces contest ID.</summary>
    public int ContestId { get; set; }
    /// <summary>Problem index as shown on CF: A, B, C1, C2, D, D1, D2, …</summary>
    public string Index { get; set; } = "";
    /// <summary>1-based position of this problem within the contest's problem list.</summary>
    public int Position { get; set; }
    public string Name { get; set; } = "";
    public int? Rating { get; set; }
    /// <summary>JSON array of tag strings, e.g. ["dp","greedy"].</summary>
    public string TagsJson { get; set; } = "[]";

    public ContestEntity Contest { get; set; } = null!;
}
