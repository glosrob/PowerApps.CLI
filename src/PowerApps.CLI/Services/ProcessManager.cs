using Microsoft.Xrm.Sdk;
using PowerApps.CLI.Infrastructure;
using PowerApps.CLI.Models;

namespace PowerApps.CLI.Services;

/// <summary>
/// Service for managing Dataverse process states.
/// </summary>
public class ProcessManager : IProcessManager
{
    private readonly IConsoleLogger _logger;
    private readonly IDataverseClient _client;

    public ProcessManager(IConsoleLogger logger, IDataverseClient client)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public List<ProcessInfo> RetrieveProcesses(List<string> solutions)
    {
        var processes = new Dictionary<Guid, ProcessInfo>();

        // Retrieve workflows/flows/business rules
        var workflowResults = _client.RetrieveProcesses(solutions);
        foreach (var entity in workflowResults.Entities)
        {
            if (!processes.ContainsKey(entity.Id))
            {
                processes[entity.Id] = new ProcessInfo
                {
                    Id = entity.Id,
                    Name = entity.GetAttributeValue<string>("name") ?? "Unknown",
                    Type = (ProcessType)entity.GetAttributeValue<OptionSetValue>("category").Value,
                    CurrentState = entity.GetAttributeValue<OptionSetValue>("statecode").Value == 1
                        ? ProcessState.Active
                        : ProcessState.Inactive
                };
            }
        }

        // Retrieve duplicate detection rules
        var duplicateRuleResults = _client.RetrieveDuplicateRules(solutions);
        foreach (var entity in duplicateRuleResults.Entities)
        {
            if (!processes.ContainsKey(entity.Id))
            {
                processes[entity.Id] = new ProcessInfo
                {
                    Id = entity.Id,
                    Name = entity.GetAttributeValue<string>("name") ?? "Unknown",
                    Type = ProcessType.DuplicateDetectionRule,
                    CurrentState = entity.GetAttributeValue<OptionSetValue>("statecode").Value == 1
                        ? ProcessState.Active
                        : ProcessState.Inactive
                };
            }
        }

        return processes.Values.ToList();
    }

    public void DetermineExpectedStates(List<ProcessInfo> processes, List<string> inactivePatterns)
    {
        foreach (var process in processes)
        {
            // Check if process matches any inactive pattern
            var shouldBeInactive = inactivePatterns.Any(pattern =>
                MatchesPattern(process.Name, pattern));

            process.ExpectedState = shouldBeInactive ? ProcessState.Inactive : ProcessState.Active;
        }
    }

    public ProcessManageSummary ManageProcessStates(
        List<ProcessInfo> processes,
        bool isDryRun,
        int maxRetries)
    {
        var summary = new ProcessManageSummary
        {
            ExecutionDate = DateTime.UtcNow,
            IsDryRun = isDryRun
        };

        var processesToManage = processes.Where(p => p.CurrentState != p.ExpectedState).ToList();
        var retryList = new List<ProcessInfo>();

        // Initial pass
        foreach (var process in processesToManage)
        {
            var result = ManageProcess(process, isDryRun);
            summary.Results.Add(result);

            if (!result.Success && !isDryRun)
            {
                retryList.Add(process);
            }
        }

        // Retry logic for failed processes
        var retryAttempt = 0;
        while (retryList.Any() && retryAttempt < maxRetries)
        {
            retryAttempt++;
            _logger.LogInfo($"Retry attempt {retryAttempt} of {maxRetries} for {retryList.Count} process(es)...");

            var currentRetryList = new List<ProcessInfo>(retryList);
            retryList.Clear();

            foreach (var process in currentRetryList)
            {
                var result = ManageProcess(process, isDryRun);

                // Update the existing result
                var existingResult = summary.Results.First(r => r.Process.Id == process.Id);
                summary.Results.Remove(existingResult);
                summary.Results.Add(result);

                if (!result.Success)
                {
                    retryList.Add(process);
                }
            }
        }

        // Add unchanged processes
        var unchangedProcesses = processes.Where(p => p.CurrentState == p.ExpectedState);
        foreach (var process in unchangedProcesses)
        {
            summary.Results.Add(new ProcessManageResult
            {
                Process = process,
                Action = ProcessAction.NoChangeNeeded,
                Success = true
            });
        }

        return summary;
    }

    private ProcessManageResult ManageProcess(ProcessInfo process, bool isDryRun)
    {
        var result = new ProcessManageResult { Process = process };

        try
        {
            if (isDryRun)
            {
                result.Action = process.ExpectedState == ProcessState.Active
                    ? ProcessAction.Activated
                    : ProcessAction.Deactivated;
                result.Success = true;

                _logger.LogInfoIfVerbose(
                    $"[DRY RUN] Would {(process.ExpectedState == ProcessState.Active ? "activate" : "deactivate")}: {process.Name}");
            }
            else
            {
                if (process.ExpectedState == ProcessState.Active)
                {
                    if (process.Type == ProcessType.DuplicateDetectionRule)
                        _client.ActivateDuplicateRule(process.Id);
                    else
                        _client.ActivateProcess(process.Id);

                    result.Action = ProcessAction.Activated;
                    _logger.LogInfo($"✓ Activated: {process.Name}");
                }
                else
                {
                    if (process.Type == ProcessType.DuplicateDetectionRule)
                        _client.DeactivateDuplicateRule(process.Id);
                    else
                        _client.DeactivateProcess(process.Id);

                    result.Action = ProcessAction.Deactivated;
                    _logger.LogInfo($"✓ Deactivated: {process.Name}");
                }

                result.Success = true;
            }
        }
        catch (Exception ex)
        {
            result.Action = ProcessAction.Failed;
            result.Success = false;
            result.ErrorMessage = ex.Message;

            _logger.LogError($"✗ Failed to manage {process.Name}: {ex.Message}");
        }

        return result;
    }

    private bool MatchesPattern(string text, string pattern)
    {
        // Simple wildcard matching (* means any characters)
        if (!pattern.Contains('*'))
        {
            return text.Equals(pattern, StringComparison.OrdinalIgnoreCase);
        }

        var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace("\\*", ".*") + "$";

        return System.Text.RegularExpressions.Regex.IsMatch(
            text,
            regexPattern,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }
}
