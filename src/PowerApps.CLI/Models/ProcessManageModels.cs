namespace PowerApps.CLI.Models;

/// <summary>
/// Represents a Dataverse process (workflow, business rule, flow, action).
/// </summary>
public class ProcessInfo
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ProcessType Type { get; set; }
    public ProcessState CurrentState { get; set; }
    public ProcessState ExpectedState { get; set; }
    public string? SolutionName { get; set; }
}

/// <summary>
/// Type of Dataverse process.
/// </summary>
public enum ProcessType
{
    Workflow = 0,
    BusinessRule = 2,
    Action = 3,
    BusinessProcessFlow = 4,
    CloudFlow = 5,
    DuplicateDetectionRule = 100
}

/// <summary>
/// State of a process.
/// </summary>
public enum ProcessState
{
    Inactive = 0,
    Active = 1
}

/// <summary>
/// Result of managing a single process.
/// </summary>
public class ProcessManageResult
{
    public ProcessInfo Process { get; set; } = new();
    public ProcessAction Action { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Action taken on a process.
/// </summary>
public enum ProcessAction
{
    NoChangeNeeded,
    Activated,
    Deactivated,
    Failed
}

/// <summary>
/// Overall result of process management operation.
/// </summary>
public class ProcessManageSummary
{
    public string EnvironmentUrl { get; set; } = string.Empty;
    public DateTime ExecutionDate { get; set; }
    public bool IsDryRun { get; set; }
    public List<ProcessManageResult> Results { get; set; } = new();

    public int TotalProcesses => Results.Count;
    public int ActivatedCount => Results.Count(r => r.Action == ProcessAction.Activated);
    public int DeactivatedCount => Results.Count(r => r.Action == ProcessAction.Deactivated);
    public int UnchangedCount => Results.Count(r => r.Action == ProcessAction.NoChangeNeeded);
    public int FailedCount => Results.Count(r => r.Action == ProcessAction.Failed);
    public bool HasFailures => FailedCount > 0;
}
