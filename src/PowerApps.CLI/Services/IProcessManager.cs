using PowerApps.CLI.Infrastructure;
using PowerApps.CLI.Models;

namespace PowerApps.CLI.Services;

/// <summary>
/// Service for managing Dataverse process states.
/// </summary>
public interface IProcessManager
{
    /// <summary>
    /// Retrieves all processes from specified solutions.
    /// </summary>
    List<ProcessInfo> RetrieveProcesses(IDataverseClient client, List<string> solutions);

    /// <summary>
    /// Determines expected state for processes based on inactive patterns.
    /// </summary>
    void DetermineExpectedStates(List<ProcessInfo> processes, List<string> inactivePatterns);

    /// <summary>
    /// Manages process states to match expected states.
    /// </summary>
    ProcessManageSummary ManageProcessStates(
        IDataverseClient client,
        List<ProcessInfo> processes,
        bool isDryRun,
        int maxRetries);
}
