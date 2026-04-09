using ClosedXML.Excel;
using PowerApps.CLI.Infrastructure;
using PowerApps.CLI.Models;
using PowerApps.CLI.Services;

namespace PowerApps.CLI.Tests.Services;

public class ProcessReporterTests
{
    private readonly Mock<IFileWriter> _mockFileWriter;
    private readonly ProcessReporter _reporter;
    private byte[]? _capturedBytes;

    public ProcessReporterTests()
    {
        _mockFileWriter = new Mock<IFileWriter>();
        _reporter = new ProcessReporter(_mockFileWriter.Object);

        _mockFileWriter
            .Setup(x => x.WriteBytesAsync(It.IsAny<string>(), It.IsAny<byte[]>()))
            .Callback<string, byte[]>((_, bytes) => _capturedBytes = bytes)
            .Returns(Task.CompletedTask);
    }

    private XLWorkbook OpenWorkbook()
    {
        Assert.NotNull(_capturedBytes);
        return new XLWorkbook(new MemoryStream(_capturedBytes));
    }

    private static ProcessManageResult MakeResult(string name, ProcessType type, ProcessAction action, string? error = null) =>
        new()
        {
            Process = new ProcessInfo
            {
                Name           = name,
                Type           = type,
                CurrentState   = ProcessState.Inactive,
                ExpectedState  = ProcessState.Active
            },
            Action       = action,
            Success      = action != ProcessAction.Failed,
            ErrorMessage = error
        };

    // --- Workbook structure ---

    [Fact]
    public async Task GenerateReportAsync_CreatesProcessManagementSheet()
    {
        await _reporter.GenerateReportAsync(new ProcessManageSummary(), "report.xlsx");

        Assert.NotNull(OpenWorkbook().Worksheet("Process Management"));
    }

    [Fact]
    public async Task GenerateReportAsync_WritesBoldTitle()
    {
        await _reporter.GenerateReportAsync(new ProcessManageSummary(), "report.xlsx");

        var cell = OpenWorkbook().Worksheet("Process Management").Cell(1, 1);
        Assert.Equal("Process Management Report", cell.GetString());
        Assert.True(cell.Style.Font.Bold);
    }

    // --- Metadata ---

    [Fact]
    public async Task GenerateReportAsync_WritesEnvironmentUrl()
    {
        var summary = new ProcessManageSummary { EnvironmentUrl = "https://test.crm.dynamics.com" };

        await _reporter.GenerateReportAsync(summary, "report.xlsx");

        var ws = OpenWorkbook().Worksheet("Process Management");
        Assert.Equal("https://test.crm.dynamics.com", ws.Cell(3, 2).GetString());
    }

    [Fact]
    public async Task GenerateReportAsync_WhenDryRun_WritesDryRunMode()
    {
        var summary = new ProcessManageSummary { IsDryRun = true };

        await _reporter.GenerateReportAsync(summary, "report.xlsx");

        var ws = OpenWorkbook().Worksheet("Process Management");
        Assert.Equal("Dry Run (Preview)", ws.Cell(5, 2).GetString());
    }

    [Fact]
    public async Task GenerateReportAsync_WhenExecuted_WritesExecutedMode()
    {
        var summary = new ProcessManageSummary { IsDryRun = false };

        await _reporter.GenerateReportAsync(summary, "report.xlsx");

        var ws = OpenWorkbook().Worksheet("Process Management");
        Assert.Equal("Executed", ws.Cell(5, 2).GetString());
    }

    // --- Process rows ---

    [Fact]
    public async Task GenerateReportAsync_WithActivatedProcess_WritesActivatedAction()
    {
        var summary = new ProcessManageSummary
        {
            Results = [ MakeResult("My Workflow", ProcessType.Workflow, ProcessAction.Activated) ]
        };

        await _reporter.GenerateReportAsync(summary, "report.xlsx");

        var ws = OpenWorkbook().Worksheet("Process Management");
        var row = ws.RangeUsed()!.Rows().First(r => r.Cell(1).GetString() == "My Workflow");

        Assert.Equal("Activated", row.Cell(5).GetString());
    }

    [Fact]
    public async Task GenerateReportAsync_WithDeactivatedProcess_WritesDeactivatedAction()
    {
        var summary = new ProcessManageSummary
        {
            Results = [ MakeResult("My Rule", ProcessType.BusinessRule, ProcessAction.Deactivated) ]
        };

        await _reporter.GenerateReportAsync(summary, "report.xlsx");

        var ws = OpenWorkbook().Worksheet("Process Management");
        var row = ws.RangeUsed()!.Rows().First(r => r.Cell(1).GetString() == "My Rule");

        Assert.Equal("Deactivated", row.Cell(5).GetString());
    }

    [Fact]
    public async Task GenerateReportAsync_WithFailedProcess_WritesErrorMessage()
    {
        var summary = new ProcessManageSummary
        {
            Results = [ MakeResult("My Flow", ProcessType.CloudFlow, ProcessAction.Failed, "Access denied") ]
        };

        await _reporter.GenerateReportAsync(summary, "report.xlsx");

        var ws = OpenWorkbook().Worksheet("Process Management");
        var row = ws.RangeUsed()!.Rows().First(r => r.Cell(1).GetString() == "My Flow");

        Assert.Equal("Failed",        row.Cell(5).GetString());
        Assert.Equal("Access denied", row.Cell(6).GetString());
    }

    // --- GetProcessTypeName coverage ---

    [Theory]
    [InlineData(ProcessType.Workflow,              "Workflow")]
    [InlineData(ProcessType.BusinessRule,          "Business Rule")]
    [InlineData(ProcessType.Action,                "Action")]
    [InlineData(ProcessType.BusinessProcessFlow,   "Business Process Flow")]
    [InlineData(ProcessType.CloudFlow,             "Cloud Flow")]
    [InlineData(ProcessType.DuplicateDetectionRule,"Duplicate Detection Rule")]
    public async Task GenerateReportAsync_WritesCorrectProcessTypeName(ProcessType type, string expectedLabel)
    {
        var summary = new ProcessManageSummary
        {
            Results = [ MakeResult("Test Process", type, ProcessAction.NoChangeNeeded) ]
        };

        await _reporter.GenerateReportAsync(summary, "report.xlsx");

        var ws = OpenWorkbook().Worksheet("Process Management");
        var row = ws.RangeUsed()!.Rows().First(r => r.Cell(1).GetString() == "Test Process");

        Assert.Equal(expectedLabel, row.Cell(2).GetString());
    }

    // --- Sorting ---

    [Fact]
    public async Task GenerateReportAsync_WritesProcessesSortedByName()
    {
        var summary = new ProcessManageSummary
        {
            Results =
            [
                MakeResult("Zebra Process", ProcessType.Workflow,  ProcessAction.Activated),
                MakeResult("Alpha Process", ProcessType.CloudFlow, ProcessAction.Activated)
            ]
        };

        await _reporter.GenerateReportAsync(summary, "report.xlsx");

        var ws = OpenWorkbook().Worksheet("Process Management");
        var dataRows = ws.RangeUsed()!.Rows()
            .Where(r => r.Cell(1).GetString().EndsWith("Process"))
            .ToList();

        Assert.Equal("Alpha Process", dataRows[0].Cell(1).GetString());
        Assert.Equal("Zebra Process", dataRows[1].Cell(1).GetString());
    }

    // --- File output ---

    [Fact]
    public async Task GenerateReportAsync_WritesToSpecifiedOutputPath()
    {
        await _reporter.GenerateReportAsync(new ProcessManageSummary(), "process-report.xlsx");

        _mockFileWriter.Verify(x => x.WriteBytesAsync("process-report.xlsx", It.IsAny<byte[]>()), Times.Once);
    }
}
