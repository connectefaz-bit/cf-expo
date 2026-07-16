namespace CfMvc.Data;

public class ContestEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Phase { get; set; } = "";
    public long DurationSeconds { get; set; }
    public long? StartTimeSeconds { get; set; }
    /// <summary>True once contest.standings has been used to get the authoritative problem list.</summary>
    public bool IsEnriched { get; set; }
    public DateTime SyncedAt { get; set; }

    public ICollection<ProblemEntity> Problems { get; set; } = new List<ProblemEntity>();
}
