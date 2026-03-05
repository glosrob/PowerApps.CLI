using PowerApps.CLI.Commands;
using PowerApps.CLI.Infrastructure;
using PowerApps.CLI.Models;
using PowerApps.CLI.Services;

namespace PowerApps.CLI.Tests.Commands;

public class SolutionLayersCommandTests
{
    private readonly Mock<IConsoleLogger> _mockLogger;
    private readonly Mock<IDataverseClient> _mockClient;
    private readonly Mock<ISolutionLayerService> _mockService;
    private readonly Mock<ISolutionLayerReporter> _mockReporter;
    private readonly SolutionLayersCommand _command;

    public SolutionLayersCommandTests()
    {
        _mockLogger = new Mock<IConsoleLogger>();
        _mockClient = new Mock<IDataverseClient>();
        _mockClient.Setup(c => c.GetEnvironmentUrl()).Returns("https://test.crm.dynamics.com");

        _mockService = new Mock<ISolutionLayerService>();
        _mockReporter = new Mock<ISolutionLayerReporter>();

        _command = new SolutionLayersCommand(
            _mockLogger.Object,
            _mockClient.Object,
            _mockService.Object,
            _mockReporter.Object);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoUnmanagedLayers_Returns0()
    {
        var result = new SolutionLayerResult
        {
            SolutionName = "MySolution",
            EnvironmentUrl = "https://test.crm.dynamics.com",
            ReportDate = DateTime.UtcNow
        };

        _mockService.Setup(s => s.GetUnmanagedLayersAsync("MySolution", It.IsAny<Action<int, int, int>?>(), It.IsAny<Action<string>?>()))
            .ReturnsAsync(result);
        _mockReporter.Setup(r => r.GenerateReportAsync(result, "solution-layers.xlsx"))
            .Returns(Task.CompletedTask);

        var exitCode = await _command.ExecuteAsync("MySolution", "solution-layers.xlsx");

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUnmanagedLayersExist_Returns0AndLogsWarnings()
    {
        var result = new SolutionLayerResult
        {
            SolutionName = "MySolution",
            EnvironmentUrl = "https://test.crm.dynamics.com",
            ReportDate = DateTime.UtcNow,
            LayeredComponents = new List<LayeredComponent>
            {
                new() { ComponentName = "rob_entity", ComponentType = "Entity", UnmanagedLayerOwner = "Active (Unmanaged Customisations)" },
                new() { ComponentName = "rob_form", ComponentType = "Form", UnmanagedLayerOwner = "Active (Unmanaged Customisations)" }
            }
        };

        _mockService.Setup(s => s.GetUnmanagedLayersAsync("MySolution", It.IsAny<Action<int, int, int>?>(), It.IsAny<Action<string>?>()))
            .ReturnsAsync(result);
        _mockReporter.Setup(r => r.GenerateReportAsync(result, "solution-layers.xlsx"))
            .Returns(Task.CompletedTask);

        var exitCode = await _command.ExecuteAsync("MySolution", "solution-layers.xlsx");

        Assert.Equal(0, exitCode);
        _mockLogger.Verify(l => l.LogWarning(It.Is<string>(s => s.Contains("2"))), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_AlwaysGeneratesReport()
    {
        var result = new SolutionLayerResult { SolutionName = "MySolution" };

        _mockService.Setup(s => s.GetUnmanagedLayersAsync("MySolution", It.IsAny<Action<int, int, int>?>(), It.IsAny<Action<string>?>()))
            .ReturnsAsync(result);
        _mockReporter.Setup(r => r.GenerateReportAsync(result, "output.xlsx"))
            .Returns(Task.CompletedTask);

        await _command.ExecuteAsync("MySolution", "output.xlsx");

        _mockReporter.Verify(r => r.GenerateReportAsync(result, "output.xlsx"), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenServiceThrows_Returns1()
    {
        _mockService.Setup(s => s.GetUnmanagedLayersAsync("MySolution", It.IsAny<Action<int, int, int>?>(), It.IsAny<Action<string>?>()))
            .ThrowsAsync(new InvalidOperationException("Connection failed"));

        var exitCode = await _command.ExecuteAsync("MySolution", "output.xlsx");

        Assert.Equal(1, exitCode);
        _mockLogger.Verify(l => l.LogError(It.Is<string>(s => s.Contains("Connection failed"))), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenReporterThrows_Returns1()
    {
        var result = new SolutionLayerResult { SolutionName = "MySolution" };

        _mockService.Setup(s => s.GetUnmanagedLayersAsync("MySolution", It.IsAny<Action<int, int, int>?>(), It.IsAny<Action<string>?>()))
            .ReturnsAsync(result);
        _mockReporter.Setup(r => r.GenerateReportAsync(It.IsAny<SolutionLayerResult>(), It.IsAny<string>()))
            .ThrowsAsync(new IOException("Disk full"));

        var exitCode = await _command.ExecuteAsync("MySolution", "output.xlsx");

        Assert.Equal(1, exitCode);
        _mockLogger.Verify(l => l.LogError(It.Is<string>(s => s.Contains("Disk full"))), Times.Once);
    }

    [Fact]
    public void CreateCliCommand_ReturnsValidCommand()
    {
        var command = SolutionLayersCommand.CreateCliCommand();

        Assert.NotNull(command);
        Assert.Equal("solution-layers", command.Name);
    }

    [Fact]
    public void CreateCliCommand_HasRequiredSolutionOption()
    {
        var command = SolutionLayersCommand.CreateCliCommand();

        var solutionOption = command.Options.FirstOrDefault(o => o.Name == "solution");
        Assert.NotNull(solutionOption);
        Assert.True(solutionOption.IsRequired);
    }
}
