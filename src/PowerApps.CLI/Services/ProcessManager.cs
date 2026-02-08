using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using PowerApps.CLI.Infrastructure;
using PowerApps.CLI.Models;

namespace PowerApps.CLI.Services;

/// <summary>
/// Service for managing Dataverse process states.
/// </summary>
public class ProcessManager : IProcessManager
{
    private readonly IConsoleLogger _logger;

    public ProcessManager(IConsoleLogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public List<ProcessInfo> RetrieveProcesses(IDataverseClient client, List<string> solutions)
    {
        var query = new QueryExpression("workflow")
        {
            ColumnSet = new ColumnSet("workflowid", "name", "category", "statecode", "statuscode"),
            Criteria = new FilterExpression(LogicalOperator.And)
        };

        // Filter by category: Workflow(0), BusinessRule(2), Action(3), BusinessProcessFlow(4), CloudFlow(5)
        query.Criteria.AddCondition("category", ConditionOperator.In, 0, 2, 3, 4, 5);

        // Filter by solutions if specified
        if (solutions.Any())
        {
            var solutionFilter = new FilterExpression(LogicalOperator.Or);
            foreach (var solution in solutions)
            {
                var componentLink = query.AddLink("solutioncomponent", "workflowid", "objectid");
                var solutionLink = componentLink.AddLink("solution", "solutionid", "solutionid");
                solutionLink.LinkCriteria.AddCondition("uniquename", ConditionOperator.Equal, solution);
            }
        }

        var results = client.RetrieveMultiple(query);

        // Deduplicate by process ID (joins can produce duplicate rows)
        var processes = new Dictionary<Guid, ProcessInfo>();
        foreach (var entity in results.Entities)
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
        IDataverseClient client,
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
            var result = ManageProcess(client, process, isDryRun);
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
                var result = ManageProcess(client, process, isDryRun);
                
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

    private ProcessManageResult ManageProcess(IDataverseClient client, ProcessInfo process, bool isDryRun)
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
                    ActivateProcess(client, process.Id);
                    result.Action = ProcessAction.Activated;
                    _logger.LogInfo($"✓ Activated: {process.Name}");
                }
                else
                {
                    DeactivateProcess(client, process.Id);
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

    private void ActivateProcess(IDataverseClient client, Guid processId)
    {
        var request = new SetStateRequest
        {
            EntityMoniker = new EntityReference("workflow", processId),
            State = new OptionSetValue(1), // Active
            Status = new OptionSetValue(2)  // Activated
        };
        client.Execute(request);
    }

    private void DeactivateProcess(IDataverseClient client, Guid processId)
    {
        var request = new SetStateRequest
        {
            EntityMoniker = new EntityReference("workflow", processId),
            State = new OptionSetValue(0), // Inactive
            Status = new OptionSetValue(1)  // Draft
        };
        client.Execute(request);
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
