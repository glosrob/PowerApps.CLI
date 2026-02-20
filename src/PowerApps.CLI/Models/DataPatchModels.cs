namespace PowerApps.CLI.Models;

public enum PatchStatus
{
    Updated,
    Unchanged,
    NotFound,
    AmbiguousMatch,
    Error
}

public class PatchResult
{
    public string Entity { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Field { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public PatchStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
}

public class DataPatchSummary
{
    public string EnvironmentUrl { get; set; } = string.Empty;
    public DateTime ExecutionDate { get; set; } = DateTime.UtcNow;
    public List<PatchResult> Results { get; set; } = new();

    public int UpdatedCount => Results.Count(r => r.Status == PatchStatus.Updated);
    public int UnchangedCount => Results.Count(r => r.Status == PatchStatus.Unchanged);
    public int FailedCount => Results.Count(r => r.Status is PatchStatus.NotFound or PatchStatus.AmbiguousMatch or PatchStatus.Error);
    public bool HasFailures => FailedCount > 0;
}
