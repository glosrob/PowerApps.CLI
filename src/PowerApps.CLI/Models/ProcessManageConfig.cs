namespace PowerApps.CLI.Models;

/// <summary>
/// Configuration for process management.
/// </summary>
public class ProcessManageConfig
{
    /// <summary>
    /// Solutions to include processes from. If empty, all solutions are included.
    /// </summary>
    public List<string> Solutions { get; set; } = new();

    /// <summary>
    /// Name patterns for processes that should be inactive. Supports wildcards (*).
    /// Processes matching these patterns will be deactivated.
    /// All other processes will be activated.
    /// </summary>
    public List<string> InactivePatterns { get; set; } = new();

    /// <summary>
    /// Maximum number of retry attempts for processes that fail to activate/deactivate.
    /// Useful for handling parent-child dependencies.
    /// </summary>
    public int MaxRetries { get; set; } = 3;
}
