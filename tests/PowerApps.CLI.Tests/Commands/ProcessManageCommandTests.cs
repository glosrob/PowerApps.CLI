using Moq;
using PowerApps.CLI.Commands;
using PowerApps.CLI.Infrastructure;
using PowerApps.CLI.Models;
using PowerApps.CLI.Services;
using Xunit;

namespace PowerApps.CLI.Tests.Commands;

public class ProcessManageCommandTests
{
    private readonly Mock<IConsoleLogger> _mockLogger;
    private readonly Mock<IDataverseClient> _mockClient;
    private readonly Mock<IProcessManager> _mockProcessManager;
    private readonly Mock<IProcessReporter> _mockReporter;
    private readonly Mock<IFileWriter> _mockFileWriter;
    private readonly ProcessManageCommand _command;

    public ProcessManageCommandTests()
    {
        _mockLogger = new Mock<IConsoleLogger>();
        _mockClient = new Mock<IDataverseClient>();
        _mockProcessManager = new Mock<IProcessManager>();
        _mockReporter = new Mock<IProcessReporter>();
        _mockFileWriter = new Mock<IFileWriter>();

        _mockClient.Setup(c => c.GetEnvironmentUrl()).Returns("https://test.crm.dynamics.com");

        _command = new ProcessManageCommand(
            _mockLogger.Object,
            _mockClient.Object,
            _mockProcessManager.Object,
            _mockReporter.Object,
            _mockFileWriter.Object);
    }

    private void SetupConfigFile(string configPath, ProcessManageConfig config)
    {
        _mockFileWriter.Setup(f => f.FileExists(configPath)).Returns(true);
        var json = System.Text.Json.JsonSerializer.Serialize(config);
        _mockFileWriter.Setup(f => f.ReadTextAsync(configPath)).ReturnsAsync(json);
    }

    [Fact]
    public async Task ExecuteAsync_CallsRetrieveProcessesWithConfiguredSolutions()
    {
        // Arrange
        var config = new ProcessManageConfig
        {
            Solutions = new List<string> { "Solution1", "Solution2" },
            InactivePatterns = new List<string> { "Test*" }
        };
        SetupConfigFile("config.json", config);

        _mockProcessManager.Setup(m => m.RetrieveProcesses(config.Solutions))
            .Returns(new List<ProcessInfo>());
        _mockProcessManager.Setup(m => m.ManageProcessStates(It.IsAny<List<ProcessInfo>>(), false, 3))
            .Returns(new ProcessManageSummary());

        // Act
        await _command.ExecuteAsync("config.json", "report.xlsx", false);

        // Assert
        _mockProcessManager.Verify(m => m.RetrieveProcesses(
            It.Is<List<string>>(s => s.Contains("Solution1") && s.Contains("Solution2"))), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_CallsDetermineExpectedStatesWithInactivePatterns()
    {
        // Arrange
        var config = new ProcessManageConfig
        {
            Solutions = new List<string> { "Sol1" },
            InactivePatterns = new List<string> { "Test*", "Dev*" }
        };
        SetupConfigFile("config.json", config);

        var processes = new List<ProcessInfo>
        {
            new() { Id = Guid.NewGuid(), Name = "Test Flow" }
        };
        _mockProcessManager.Setup(m => m.RetrieveProcesses(It.IsAny<List<string>>()))
            .Returns(processes);
        _mockProcessManager.Setup(m => m.ManageProcessStates(It.IsAny<List<ProcessInfo>>(), false, 3))
            .Returns(new ProcessManageSummary());

        // Act
        await _command.ExecuteAsync("config.json", "report.xlsx", false);

        // Assert
        _mockProcessManager.Verify(m => m.DetermineExpectedStates(
            processes,
            It.Is<List<string>>(p => p.Contains("Test*") && p.Contains("Dev*"))), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_PassesDryRunFlagToManageProcessStates()
    {
        // Arrange
        var config = new ProcessManageConfig { Solutions = new List<string> { "Sol1" } };
        SetupConfigFile("config.json", config);

        _mockProcessManager.Setup(m => m.RetrieveProcesses(It.IsAny<List<string>>()))
            .Returns(new List<ProcessInfo>());
        _mockProcessManager.Setup(m => m.ManageProcessStates(It.IsAny<List<ProcessInfo>>(), true, 3))
            .Returns(new ProcessManageSummary());

        // Act
        await _command.ExecuteAsync("config.json", "report.xlsx", true);

        // Assert
        _mockProcessManager.Verify(m => m.ManageProcessStates(
            It.IsAny<List<ProcessInfo>>(), true, 3), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_GeneratesReport()
    {
        // Arrange
        var config = new ProcessManageConfig { Solutions = new List<string> { "Sol1" } };
        SetupConfigFile("config.json", config);

        _mockProcessManager.Setup(m => m.RetrieveProcesses(It.IsAny<List<string>>()))
            .Returns(new List<ProcessInfo>());
        _mockProcessManager.Setup(m => m.ManageProcessStates(It.IsAny<List<ProcessInfo>>(), false, 3))
            .Returns(new ProcessManageSummary());

        // Act
        await _command.ExecuteAsync("config.json", "report.xlsx", false);

        // Assert
        _mockReporter.Verify(r => r.GenerateReportAsync(
            It.IsAny<ProcessManageSummary>(), "report.xlsx"), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenHasFailures_Returns1()
    {
        // Arrange
        var config = new ProcessManageConfig { Solutions = new List<string> { "Sol1" } };
        SetupConfigFile("config.json", config);

        _mockProcessManager.Setup(m => m.RetrieveProcesses(It.IsAny<List<string>>()))
            .Returns(new List<ProcessInfo>());

        var summary = new ProcessManageSummary();
        summary.Results.Add(new ProcessManageResult
        {
            Process = new ProcessInfo { Name = "Failed" },
            Action = ProcessAction.Failed,
            Success = false
        });
        _mockProcessManager.Setup(m => m.ManageProcessStates(It.IsAny<List<ProcessInfo>>(), false, 3))
            .Returns(summary);

        // Act
        var result = await _command.ExecuteAsync("config.json", "report.xlsx", false);

        // Assert
        Assert.Equal(1, result);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoFailures_Returns0()
    {
        // Arrange
        var config = new ProcessManageConfig { Solutions = new List<string> { "Sol1" } };
        SetupConfigFile("config.json", config);

        _mockProcessManager.Setup(m => m.RetrieveProcesses(It.IsAny<List<string>>()))
            .Returns(new List<ProcessInfo>());
        _mockProcessManager.Setup(m => m.ManageProcessStates(It.IsAny<List<ProcessInfo>>(), false, 3))
            .Returns(new ProcessManageSummary());

        // Act
        var result = await _command.ExecuteAsync("config.json", "report.xlsx", false);

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task ExecuteAsync_WhenConfigNotFound_Returns1()
    {
        // Arrange
        _mockFileWriter.Setup(f => f.FileExists("missing.json")).Returns(false);

        // Act
        var result = await _command.ExecuteAsync("missing.json", "report.xlsx", false);

        // Assert
        Assert.Equal(1, result);
    }

    [Fact]
    public void CreateCliCommand_ReturnsValidCommand()
    {
        var command = ProcessManageCommand.CreateCliCommand();

        Assert.NotNull(command);
        Assert.Equal("process-manage", command.Name);
    }
}
