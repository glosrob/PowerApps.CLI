using Microsoft.Xrm.Sdk;
using Moq;
using PowerApps.CLI.Infrastructure;
using PowerApps.CLI.Models;
using PowerApps.CLI.Services;
using Xunit;

namespace PowerApps.CLI.Tests.Services;

public class ProcessManagerTests
{
    private readonly Mock<IConsoleLogger> _mockLogger;
    private readonly Mock<IDataverseClient> _mockClient;
    private readonly ProcessManager _manager;

    public ProcessManagerTests()
    {
        _mockLogger = new Mock<IConsoleLogger>();
        _mockClient = new Mock<IDataverseClient>();
        // Default: return empty collections so tests that don't care about duplicate rules still work
        _mockClient.Setup(c => c.RetrieveDuplicateRules(It.IsAny<List<string>>())).Returns(new EntityCollection());
        _manager = new ProcessManager(_mockLogger.Object, _mockClient.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new ProcessManager(null!, _mockClient.Object));
        Assert.Equal("logger", ex.ParamName);
    }

    [Fact]
    public void Constructor_WithNullClient_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new ProcessManager(_mockLogger.Object, null!));
        Assert.Equal("client", ex.ParamName);
    }

    #endregion

    #region RetrieveProcesses Tests

    [Fact]
    public void RetrieveProcesses_DeduplicatesByProcessId()
    {
        // Arrange - two entities with the same ID
        var processId = Guid.NewGuid();
        var entity1 = CreateProcessEntity(processId, "Test Flow", ProcessType.CloudFlow, ProcessState.Active);
        var entity2 = CreateProcessEntity(processId, "Test Flow", ProcessType.CloudFlow, ProcessState.Active);

        var collection = new EntityCollection(new List<Entity> { entity1, entity2 });
        _mockClient.Setup(c => c.RetrieveProcesses(It.IsAny<List<string>>())).Returns(collection);

        // Act
        var result = _manager.RetrieveProcesses(new List<string> { "MySolution" });

        // Assert
        Assert.Single(result);
        Assert.Equal(processId, result[0].Id);
    }

    [Fact]
    public void RetrieveProcesses_MapsEntityFieldsCorrectly()
    {
        // Arrange
        var processId = Guid.NewGuid();
        var entity = CreateProcessEntity(processId, "My Workflow", ProcessType.Workflow, ProcessState.Active);

        var collection = new EntityCollection(new List<Entity> { entity });
        _mockClient.Setup(c => c.RetrieveProcesses(It.IsAny<List<string>>())).Returns(collection);

        // Act
        var result = _manager.RetrieveProcesses(new List<string>());

        // Assert
        Assert.Single(result);
        Assert.Equal(processId, result[0].Id);
        Assert.Equal("My Workflow", result[0].Name);
        Assert.Equal(ProcessType.Workflow, result[0].Type);
        Assert.Equal(ProcessState.Active, result[0].CurrentState);
    }

    [Fact]
    public void RetrieveProcesses_MapsInactiveStateCorrectly()
    {
        // Arrange
        var entity = CreateProcessEntity(Guid.NewGuid(), "Inactive Flow", ProcessType.CloudFlow, ProcessState.Inactive);

        var collection = new EntityCollection(new List<Entity> { entity });
        _mockClient.Setup(c => c.RetrieveProcesses(It.IsAny<List<string>>())).Returns(collection);

        // Act
        var result = _manager.RetrieveProcesses(new List<string>());

        // Assert
        Assert.Equal(ProcessState.Inactive, result[0].CurrentState);
    }

    [Fact]
    public void RetrieveProcesses_PassesSolutionsToClient()
    {
        // Arrange
        var solutions = new List<string> { "Solution1", "Solution2" };
        _mockClient.Setup(c => c.RetrieveProcesses(solutions)).Returns(new EntityCollection());

        // Act
        _manager.RetrieveProcesses(solutions);

        // Assert
        _mockClient.Verify(c => c.RetrieveProcesses(solutions), Times.Once);
    }

    #endregion

    #region DetermineExpectedStates Tests

    [Fact]
    public void DetermineExpectedStates_ExactMatch_SetsInactive()
    {
        // Arrange
        var processes = new List<ProcessInfo>
        {
            new() { Name = "My Workflow", CurrentState = ProcessState.Active }
        };

        // Act
        _manager.DetermineExpectedStates(processes, new List<string> { "My Workflow" });

        // Assert
        Assert.Equal(ProcessState.Inactive, processes[0].ExpectedState);
    }

    [Fact]
    public void DetermineExpectedStates_ExactMatch_IsCaseInsensitive()
    {
        // Arrange
        var processes = new List<ProcessInfo>
        {
            new() { Name = "My Workflow", CurrentState = ProcessState.Active }
        };

        // Act
        _manager.DetermineExpectedStates(processes, new List<string> { "my workflow" });

        // Assert
        Assert.Equal(ProcessState.Inactive, processes[0].ExpectedState);
    }

    [Fact]
    public void DetermineExpectedStates_WildcardPattern_MatchesCorrectly()
    {
        // Arrange
        var processes = new List<ProcessInfo>
        {
            new() { Name = "Test: My Cloud Flow", CurrentState = ProcessState.Active },
            new() { Name = "Production Flow", CurrentState = ProcessState.Active }
        };

        // Act
        _manager.DetermineExpectedStates(processes, new List<string> { "Test:*" });

        // Assert
        Assert.Equal(ProcessState.Inactive, processes[0].ExpectedState);
        Assert.Equal(ProcessState.Active, processes[1].ExpectedState);
    }

    [Fact]
    public void DetermineExpectedStates_NoMatch_SetsActive()
    {
        // Arrange
        var processes = new List<ProcessInfo>
        {
            new() { Name = "Production Flow", CurrentState = ProcessState.Inactive }
        };

        // Act
        _manager.DetermineExpectedStates(processes, new List<string> { "Test*" });

        // Assert
        Assert.Equal(ProcessState.Active, processes[0].ExpectedState);
    }

    [Fact]
    public void DetermineExpectedStates_MultiplePatterns_MatchesAny()
    {
        // Arrange
        var processes = new List<ProcessInfo>
        {
            new() { Name = "Dev Flow", CurrentState = ProcessState.Active },
            new() { Name = "Test Flow", CurrentState = ProcessState.Active },
            new() { Name = "Prod Flow", CurrentState = ProcessState.Active }
        };

        // Act
        _manager.DetermineExpectedStates(processes, new List<string> { "Dev*", "Test*" });

        // Assert
        Assert.Equal(ProcessState.Inactive, processes[0].ExpectedState);
        Assert.Equal(ProcessState.Inactive, processes[1].ExpectedState);
        Assert.Equal(ProcessState.Active, processes[2].ExpectedState);
    }

    #endregion

    #region ManageProcessStates Tests

    [Fact]
    public void ManageProcessStates_OnlyManagesProcessesNeedingChange()
    {
        // Arrange
        var processes = new List<ProcessInfo>
        {
            new() { Id = Guid.NewGuid(), Name = "NeedsActivation", CurrentState = ProcessState.Inactive, ExpectedState = ProcessState.Active },
            new() { Id = Guid.NewGuid(), Name = "AlreadyCorrect", CurrentState = ProcessState.Active, ExpectedState = ProcessState.Active }
        };

        // Act
        var summary = _manager.ManageProcessStates(processes, false, 0);

        // Assert
        _mockClient.Verify(c => c.ActivateProcess(processes[0].Id), Times.Once);
        _mockClient.Verify(c => c.ActivateProcess(processes[1].Id), Times.Never);
        _mockClient.Verify(c => c.DeactivateProcess(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public void ManageProcessStates_DryRun_DoesNotCallClient()
    {
        // Arrange
        var processes = new List<ProcessInfo>
        {
            new() { Id = Guid.NewGuid(), Name = "ToActivate", CurrentState = ProcessState.Inactive, ExpectedState = ProcessState.Active },
            new() { Id = Guid.NewGuid(), Name = "ToDeactivate", CurrentState = ProcessState.Active, ExpectedState = ProcessState.Inactive }
        };

        // Act
        var summary = _manager.ManageProcessStates(processes, true, 0);

        // Assert
        _mockClient.Verify(c => c.ActivateProcess(It.IsAny<Guid>()), Times.Never);
        _mockClient.Verify(c => c.DeactivateProcess(It.IsAny<Guid>()), Times.Never);
        Assert.Equal(2, summary.Results.Count(r => r.Success));
    }

    [Fact]
    public void ManageProcessStates_DryRun_ReportsCorrectActions()
    {
        // Arrange
        var processes = new List<ProcessInfo>
        {
            new() { Id = Guid.NewGuid(), Name = "ToActivate", CurrentState = ProcessState.Inactive, ExpectedState = ProcessState.Active },
            new() { Id = Guid.NewGuid(), Name = "ToDeactivate", CurrentState = ProcessState.Active, ExpectedState = ProcessState.Inactive }
        };

        // Act
        var summary = _manager.ManageProcessStates(processes, true, 0);

        // Assert
        var activateResult = summary.Results.First(r => r.Process.Name == "ToActivate");
        var deactivateResult = summary.Results.First(r => r.Process.Name == "ToDeactivate");
        Assert.Equal(ProcessAction.Activated, activateResult.Action);
        Assert.Equal(ProcessAction.Deactivated, deactivateResult.Action);
    }

    [Fact]
    public void ManageProcessStates_IncludesUnchangedProcesses()
    {
        // Arrange
        var processes = new List<ProcessInfo>
        {
            new() { Id = Guid.NewGuid(), Name = "AlreadyActive", CurrentState = ProcessState.Active, ExpectedState = ProcessState.Active }
        };

        // Act
        var summary = _manager.ManageProcessStates(processes, false, 0);

        // Assert
        Assert.Single(summary.Results);
        Assert.Equal(ProcessAction.NoChangeNeeded, summary.Results[0].Action);
        Assert.True(summary.Results[0].Success);
    }

    [Fact]
    public void ManageProcessStates_RetriesFailedProcesses()
    {
        // Arrange
        var processId = Guid.NewGuid();
        var processes = new List<ProcessInfo>
        {
            new() { Id = processId, Name = "FlakeyProcess", CurrentState = ProcessState.Inactive, ExpectedState = ProcessState.Active }
        };

        // First call fails, second succeeds
        var callCount = 0;
        _mockClient.Setup(c => c.ActivateProcess(processId))
            .Callback(() =>
            {
                callCount++;
                if (callCount == 1)
                    throw new Exception("Temporary failure");
            });

        // Act
        var summary = _manager.ManageProcessStates(processes, false, 2);

        // Assert
        Assert.Equal(2, callCount);
        var result = summary.Results.First(r => r.Process.Id == processId);
        Assert.True(result.Success);
        Assert.Equal(ProcessAction.Activated, result.Action);
    }

    [Fact]
    public void ManageProcessStates_RespectsMaxRetries()
    {
        // Arrange
        var processId = Guid.NewGuid();
        var processes = new List<ProcessInfo>
        {
            new() { Id = processId, Name = "AlwaysFails", CurrentState = ProcessState.Inactive, ExpectedState = ProcessState.Active }
        };

        _mockClient.Setup(c => c.ActivateProcess(processId))
            .Throws(new Exception("Permanent failure"));

        // Act
        var summary = _manager.ManageProcessStates(processes, false, 2);

        // Assert: 1 initial + 2 retries = 3 total calls
        _mockClient.Verify(c => c.ActivateProcess(processId), Times.Exactly(3));
        var result = summary.Results.First(r => r.Process.Id == processId);
        Assert.False(result.Success);
        Assert.Equal(ProcessAction.Failed, result.Action);
    }

    [Fact]
    public void ManageProcessStates_FailedProcess_CapturesErrorMessage()
    {
        // Arrange
        var processId = Guid.NewGuid();
        var processes = new List<ProcessInfo>
        {
            new() { Id = processId, Name = "FailingProcess", CurrentState = ProcessState.Inactive, ExpectedState = ProcessState.Active }
        };

        _mockClient.Setup(c => c.ActivateProcess(processId))
            .Throws(new Exception("Access denied"));

        // Act
        var summary = _manager.ManageProcessStates(processes, false, 0);

        // Assert
        var result = summary.Results.First(r => r.Process.Id == processId);
        Assert.Equal("Access denied", result.ErrorMessage);
    }

    [Fact]
    public void ManageProcessStates_CallsDeactivateForInactiveExpectedState()
    {
        // Arrange
        var processId = Guid.NewGuid();
        var processes = new List<ProcessInfo>
        {
            new() { Id = processId, Name = "ToDeactivate", CurrentState = ProcessState.Active, ExpectedState = ProcessState.Inactive }
        };

        // Act
        _manager.ManageProcessStates(processes, false, 0);

        // Assert
        _mockClient.Verify(c => c.DeactivateProcess(processId), Times.Once);
    }

    #endregion

    #region DuplicateDetectionRule Tests

    [Fact]
    public void RetrieveProcesses_IncludesDuplicateDetectionRules()
    {
        // Arrange
        var workflowId = Guid.NewGuid();
        var ruleId = Guid.NewGuid();

        var workflowEntity = CreateProcessEntity(workflowId, "My Workflow", ProcessType.Workflow, ProcessState.Active);
        _mockClient.Setup(c => c.RetrieveProcesses(It.IsAny<List<string>>()))
            .Returns(new EntityCollection(new List<Entity> { workflowEntity }));

        var ruleEntity = CreateDuplicateRuleEntity(ruleId, "Duplicate Accounts", ProcessState.Active);
        _mockClient.Setup(c => c.RetrieveDuplicateRules(It.IsAny<List<string>>()))
            .Returns(new EntityCollection(new List<Entity> { ruleEntity }));

        // Act
        var result = _manager.RetrieveProcesses(new List<string> { "MySolution" });

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, p => p.Name == "My Workflow" && p.Type == ProcessType.Workflow);
        Assert.Contains(result, p => p.Name == "Duplicate Accounts" && p.Type == ProcessType.DuplicateDetectionRule);
    }

    [Fact]
    public void RetrieveProcesses_DeduplicatesDuplicateDetectionRules()
    {
        // Arrange
        var ruleId = Guid.NewGuid();
        var entity1 = CreateDuplicateRuleEntity(ruleId, "Duplicate Accounts", ProcessState.Active);
        var entity2 = CreateDuplicateRuleEntity(ruleId, "Duplicate Accounts", ProcessState.Active);

        _mockClient.Setup(c => c.RetrieveProcesses(It.IsAny<List<string>>())).Returns(new EntityCollection());
        _mockClient.Setup(c => c.RetrieveDuplicateRules(It.IsAny<List<string>>()))
            .Returns(new EntityCollection(new List<Entity> { entity1, entity2 }));

        // Act
        var result = _manager.RetrieveProcesses(new List<string>());

        // Assert
        Assert.Single(result);
        Assert.Equal(ruleId, result[0].Id);
    }

    [Fact]
    public void RetrieveProcesses_MapsDuplicateRuleInactiveStateCorrectly()
    {
        // Arrange
        var ruleEntity = CreateDuplicateRuleEntity(Guid.NewGuid(), "Inactive Rule", ProcessState.Inactive);
        _mockClient.Setup(c => c.RetrieveProcesses(It.IsAny<List<string>>())).Returns(new EntityCollection());
        _mockClient.Setup(c => c.RetrieveDuplicateRules(It.IsAny<List<string>>()))
            .Returns(new EntityCollection(new List<Entity> { ruleEntity }));

        // Act
        var result = _manager.RetrieveProcesses(new List<string>());

        // Assert
        Assert.Equal(ProcessState.Inactive, result[0].CurrentState);
        Assert.Equal(ProcessType.DuplicateDetectionRule, result[0].Type);
    }

    [Fact]
    public void ManageProcessStates_ActivatesDuplicateRuleViaCorrectClientMethod()
    {
        // Arrange
        var ruleId = Guid.NewGuid();
        var processes = new List<ProcessInfo>
        {
            new() { Id = ruleId, Name = "Duplicate Rule", Type = ProcessType.DuplicateDetectionRule,
                     CurrentState = ProcessState.Inactive, ExpectedState = ProcessState.Active }
        };

        // Act
        _manager.ManageProcessStates(processes, false, 0);

        // Assert
        _mockClient.Verify(c => c.ActivateDuplicateRule(ruleId), Times.Once);
        _mockClient.Verify(c => c.ActivateProcess(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public void ManageProcessStates_DeactivatesDuplicateRuleViaCorrectClientMethod()
    {
        // Arrange
        var ruleId = Guid.NewGuid();
        var processes = new List<ProcessInfo>
        {
            new() { Id = ruleId, Name = "Duplicate Rule", Type = ProcessType.DuplicateDetectionRule,
                     CurrentState = ProcessState.Active, ExpectedState = ProcessState.Inactive }
        };

        // Act
        _manager.ManageProcessStates(processes, false, 0);

        // Assert
        _mockClient.Verify(c => c.DeactivateDuplicateRule(ruleId), Times.Once);
        _mockClient.Verify(c => c.DeactivateProcess(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public void RetrieveProcesses_PassesSolutionsToDuplicateRuleRetrieval()
    {
        // Arrange
        var solutions = new List<string> { "Solution1", "Solution2" };
        _mockClient.Setup(c => c.RetrieveProcesses(solutions)).Returns(new EntityCollection());
        _mockClient.Setup(c => c.RetrieveDuplicateRules(solutions)).Returns(new EntityCollection());

        // Act
        _manager.RetrieveProcesses(solutions);

        // Assert
        _mockClient.Verify(c => c.RetrieveDuplicateRules(solutions), Times.Once);
    }

    #endregion

    #region Helpers

    private static Entity CreateProcessEntity(Guid id, string name, ProcessType type, ProcessState state)
    {
        var entity = new Entity("workflow", id);
        entity["name"] = name;
        entity["category"] = new OptionSetValue((int)type);
        entity["statecode"] = new OptionSetValue(state == ProcessState.Active ? 1 : 0);
        return entity;
    }

    private static Entity CreateDuplicateRuleEntity(Guid id, string name, ProcessState state)
    {
        var entity = new Entity("duplicaterule", id);
        entity["name"] = name;
        entity["statecode"] = new OptionSetValue(state == ProcessState.Active ? 1 : 0);
        return entity;
    }

    #endregion
}
