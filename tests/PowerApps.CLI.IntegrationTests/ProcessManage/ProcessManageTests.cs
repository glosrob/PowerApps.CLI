using Microsoft.Xrm.Sdk;
using PowerApps.CLI.Infrastructure;
using PowerApps.CLI.IntegrationTests.Infrastructure;
using PowerApps.CLI.Models;
using PowerApps.CLI.Services;
using Xunit;

namespace PowerApps.CLI.IntegrationTests.ProcessManage;

[Collection("Dataverse")]
[Trait("Category", "Integration")]
public class ProcessManageTests : IAsyncLifetime
{
    private readonly DataverseFixture _fixture;

    // Captured once — stable for the lifetime of the environment; re-fetching before every
    // test would waste round-trips for data that changes only via DisposeAsync restores.
    private static List<ProcessInfo>? _initialStates;

    private const string IntegrationSolution = "XRTSoftIntegrationTests";

    private const string ExampleAction = "Example Action";
    private const string ExampleBusinessRule = "Example Business Rule";
    private const string ExampleWorkflow = "Example Triggered Workflow";
    private const string ExampleWorkflowAsync = "Example Triggered Workflow Async";
    private const string ExampleWorkflowChild = "Example Triggered Workflow Child";
    private const string PluginStepCreate = "XRT.IntegrationTestPluginLib.ExamplePlugin: Create of xrt_integrationtest";
    private const string PluginStepUpdate = "XRT.IntegrationTestPluginLib.ExamplePlugin: Update of xrt_integrationtest";

    public ProcessManageTests(DataverseFixture fixture) => _fixture = fixture;

    public Task InitializeAsync()
    {
        if (_fixture.ConfigurationError is not null)
        {
            return Task.CompletedTask;
        }

        var manager = CreateManager();
        var processes = manager.RetrieveProcesses([IntegrationSolution]);

        if (_initialStates is null)
        {
            var names = processes.Select(p => p.Name).ToHashSet();
            var missing = new[] { ExampleAction, ExampleBusinessRule, ExampleWorkflow, ExampleWorkflowAsync, ExampleWorkflowChild, PluginStepCreate, PluginStepUpdate }
                .Where(n => !names.Contains(n)).ToList();

            if (missing.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Missing processes in '{IntegrationSolution}': {string.Join(", ", missing)}. " +
                    "Ensure all test processes are deployed before running integration tests.");
            }

            _initialStates = processes.Select(p => new ProcessInfo
            {
                Id = p.Id,
                Name = p.Name,
                Type = p.Type,
                CurrentState = p.CurrentState
            }).ToList();
        }

        // Drive all processes to inactive baseline before every test
        foreach (var p in processes)
        {
            p.ExpectedState = ProcessState.Inactive;
        }

        manager.ManageProcessStates(processes, isDryRun: false, maxRetries: 0);

        var baseline = manager.RetrieveProcesses([IntegrationSolution]);
        var stillActive = baseline.Where(p => p.CurrentState != ProcessState.Inactive).Select(p => p.Name).ToList();
        if (stillActive.Count > 0)
        {
            throw new InvalidOperationException(
                $"Failed to establish inactive baseline — still active: {string.Join(", ", stillActive)}");
        }

        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        if (_fixture.ConfigurationError is not null || _initialStates is null)
        {
            return Task.CompletedTask;
        }

        var manager = CreateManager();
        var current = manager.RetrieveProcesses([IntegrationSolution]);

        var initialById = _initialStates.ToDictionary(p => p.Id);
        foreach (var process in current)
        {
            if (initialById.TryGetValue(process.Id, out var initial))
            {
                process.ExpectedState = initial.CurrentState;
            }
        }

        manager.ManageProcessStates(current, isDryRun: false, maxRetries: 0);

        return Task.CompletedTask;
    }

    // -------------------------------------------------------------------------
    // Retrieve processes
    // -------------------------------------------------------------------------

    [SkippableFact]
    public void RetrieveProcesses_ReturnsAllExpectedProcesses()
    {
        Skip.If(_fixture.ConfigurationError is not null, _fixture.ConfigurationError);

        var processes = CreateManager().RetrieveProcesses([IntegrationSolution]);

        Assert.Contains(processes, p => p.Name == ExampleAction);
        Assert.Contains(processes, p => p.Name == ExampleBusinessRule);
        Assert.Contains(processes, p => p.Name == ExampleWorkflow);
        Assert.Contains(processes, p => p.Name == ExampleWorkflowAsync);
        Assert.Contains(processes, p => p.Name == ExampleWorkflowChild);
        Assert.Contains(processes, p => p.Name == PluginStepCreate);
        Assert.Contains(processes, p => p.Name == PluginStepUpdate);
    }

    // -------------------------------------------------------------------------
    // Dry run — no state changes made
    // -------------------------------------------------------------------------

    [SkippableFact]
    public void ManageProcessStates_DryRun_MakesNoStateChanges()
    {
        Skip.If(_fixture.ConfigurationError is not null, _fixture.ConfigurationError);

        var manager = CreateManager();
        var processes = manager.RetrieveProcesses([IntegrationSolution]);
        manager.DetermineExpectedStates(processes, inactivePatterns: []);
        var summary = manager.ManageProcessStates(processes, isDryRun: true, maxRetries: 0);

        Assert.False(summary.HasFailures);
        // ExampleWorkflow is Inactive with ExpectedState=Active — a real run would activate it.
        // Still inactive here proves the dry run did not fire any state-change API calls.
        Assert.Equal(0, QueryWorkflowStatecode(FindProcess(ExampleWorkflow).Id));
    }

    // -------------------------------------------------------------------------
    // Activate business rule
    // -------------------------------------------------------------------------

    [SkippableFact]
    public void ManageProcessStates_ActivateBusinessRule_ActivatesInDataverse()
    {
        Skip.If(_fixture.ConfigurationError is not null, _fixture.ConfigurationError);

        var manager = CreateManager();
        var processes = manager.RetrieveProcesses([IntegrationSolution]);
        manager.DetermineExpectedStates(processes, inactivePatterns: []);
        manager.ManageProcessStates(processes, isDryRun: false, maxRetries: 0);

        Assert.Equal(1, QueryWorkflowStatecode(FindProcess(ExampleBusinessRule).Id));
    }

    // -------------------------------------------------------------------------
    // Deactivate business rule
    // -------------------------------------------------------------------------

    [SkippableFact]
    public void ManageProcessStates_DeactivateBusinessRule_DeactivatesInDataverse()
    {
        Skip.If(_fixture.ConfigurationError is not null, _fixture.ConfigurationError);

        // Pre-activate so the command has a real state change to make
        _fixture.Client.ActivateProcess(FindProcess(ExampleBusinessRule).Id);

        var manager = CreateManager();
        var processes = manager.RetrieveProcesses([IntegrationSolution]);
        manager.DetermineExpectedStates(processes, inactivePatterns: [ExampleBusinessRule]);
        manager.ManageProcessStates(processes, isDryRun: false, maxRetries: 0);

        Assert.Equal(0, QueryWorkflowStatecode(FindProcess(ExampleBusinessRule).Id));
    }

    // -------------------------------------------------------------------------
    // Activate workflow
    // -------------------------------------------------------------------------

    [SkippableFact]
    public void ManageProcessStates_ActivateWorkflow_ActivatesInDataverse()
    {
        Skip.If(_fixture.ConfigurationError is not null, _fixture.ConfigurationError);

        var manager = CreateManager();
        var processes = manager.RetrieveProcesses([IntegrationSolution]);
        manager.DetermineExpectedStates(processes, inactivePatterns: []);
        manager.ManageProcessStates(processes, isDryRun: false, maxRetries: 0);

        Assert.Equal(1, QueryWorkflowStatecode(FindProcess(ExampleWorkflow).Id));
    }

    // -------------------------------------------------------------------------
    // Deactivate workflow
    // -------------------------------------------------------------------------

    [SkippableFact]
    public void ManageProcessStates_DeactivateWorkflow_DeactivatesInDataverse()
    {
        Skip.If(_fixture.ConfigurationError is not null, _fixture.ConfigurationError);

        // Pre-activate so the command has a real state change to make
        _fixture.Client.ActivateProcess(FindProcess(ExampleWorkflow).Id);

        var manager = CreateManager();
        var processes = manager.RetrieveProcesses([IntegrationSolution]);
        manager.DetermineExpectedStates(processes, inactivePatterns: [ExampleWorkflow]);
        manager.ManageProcessStates(processes, isDryRun: false, maxRetries: 0);

        Assert.Equal(0, QueryWorkflowStatecode(FindProcess(ExampleWorkflow).Id));
    }

    // -------------------------------------------------------------------------
    // Bulk activation — multiple workflows in a single run
    // -------------------------------------------------------------------------

    [SkippableFact]
    public void ManageProcessStates_BulkActivation_ActivatesAllWorkflowsInOneRun()
    {
        Skip.If(_fixture.ConfigurationError is not null, _fixture.ConfigurationError);

        var manager = CreateManager();
        var processes = manager.RetrieveProcesses([IntegrationSolution]);
        manager.DetermineExpectedStates(processes, inactivePatterns: []);
        manager.ManageProcessStates(processes, isDryRun: false, maxRetries: 0);

        Assert.Equal(1, QueryWorkflowStatecode(FindProcess(ExampleWorkflow).Id));
        Assert.Equal(1, QueryWorkflowStatecode(FindProcess(ExampleWorkflowAsync).Id));
        Assert.Equal(1, QueryWorkflowStatecode(FindProcess(ExampleWorkflowChild).Id));
    }

    // -------------------------------------------------------------------------
    // Plugin step — activate
    // -------------------------------------------------------------------------

    [SkippableFact]
    public void ManageProcessStates_ActivatePluginStep_EnablesInDataverse()
    {
        Skip.If(_fixture.ConfigurationError is not null, _fixture.ConfigurationError);

        var manager = CreateManager();
        var processes = manager.RetrieveProcesses([IntegrationSolution]);
        manager.DetermineExpectedStates(processes, inactivePatterns: []);
        manager.ManageProcessStates(processes, isDryRun: false, maxRetries: 0);

        // Plugin steps use inverted statecode: 0 = Enabled (Active), 1 = Disabled (Inactive)
        Assert.Equal(0, QueryPluginStepStatecode(FindProcess(PluginStepCreate).Id));
    }

    // -------------------------------------------------------------------------
    // Plugin step — deactivate
    // -------------------------------------------------------------------------

    [SkippableFact]
    public void ManageProcessStates_DeactivatePluginStep_DisablesInDataverse()
    {
        Skip.If(_fixture.ConfigurationError is not null, _fixture.ConfigurationError);

        // Pre-enable so the command has a real state change to make
        _fixture.Client.EnablePluginStep(FindProcess(PluginStepCreate).Id);

        var manager = CreateManager();
        var processes = manager.RetrieveProcesses([IntegrationSolution]);
        manager.DetermineExpectedStates(processes, inactivePatterns: [PluginStepCreate]);
        manager.ManageProcessStates(processes, isDryRun: false, maxRetries: 0);

        // statecode 1 = Disabled (Inactive) for plugin steps
        Assert.Equal(1, QueryPluginStepStatecode(FindProcess(PluginStepCreate).Id));
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private ProcessManager CreateManager() => new(new ConsoleLogger(), _fixture.Client);

    private ProcessInfo FindProcess(string name) =>
        _initialStates!.Single(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

    private int QueryWorkflowStatecode(Guid id)
    {
        var result = _fixture.Client.RetrieveRecordsByFetchXml($@"<fetch count='1'>
  <entity name='workflow'>
    <attribute name='statecode' />
    <filter>
      <condition attribute='workflowid' operator='eq' value='{id}' />
    </filter>
  </entity>
</fetch>");
        return result.Entities[0].GetAttributeValue<OptionSetValue>("statecode").Value;
    }

    private int QueryPluginStepStatecode(Guid id)
    {
        var result = _fixture.Client.RetrieveRecordsByFetchXml($@"<fetch count='1'>
  <entity name='sdkmessageprocessingstep'>
    <attribute name='statecode' />
    <filter>
      <condition attribute='sdkmessageprocessingstepid' operator='eq' value='{id}' />
    </filter>
  </entity>
</fetch>");
        return result.Entities[0].GetAttributeValue<OptionSetValue>("statecode").Value;
    }
}
