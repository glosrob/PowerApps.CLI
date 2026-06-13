using ClosedXML.Excel;
using Moq;
using PowerApps.CLI.Infrastructure;
using PowerApps.CLI.Models;
using PowerApps.CLI.Services;
using Xunit;

namespace PowerApps.CLI.Tests.Services;

public class ProcessReporterTests : IDisposable
{
    private readonly Mock<IFileWriter> _mockFileWriter;
    private readonly ProcessReporter _reporter;
    private readonly string _tempDirectory;

    public ProcessReporterTests()
    {
        _mockFileWriter = new Mock<IFileWriter>();
        _reporter = new ProcessReporter(_mockFileWriter.Object);
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"PowerAppsCLI_ProcessReporter_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDirectory);

        _mockFileWriter
            .Setup(x => x.WriteBytesAsync(It.IsAny<string>(), It.IsAny<byte[]>()))
            .Returns((string path, byte[] content) => File.WriteAllBytesAsync(path, content));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
            Directory.Delete(_tempDirectory, true);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullFileWriter_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ProcessReporter(null!));
    }

    #endregion

    #region File Output Tests

    [Fact]
    public async Task GenerateReportAsync_WritesFileToDisk()
    {
        var outputPath = Path.Combine(_tempDirectory, "report.xlsx");

        await _reporter.GenerateReportAsync(new ProcessManageSummary(), outputPath);

        Assert.True(File.Exists(outputPath));
    }

    [Fact]
    public async Task GenerateReportAsync_CallsFileWriterWithCorrectPath()
    {
        var outputPath = Path.Combine(_tempDirectory, "report.xlsx");

        await _reporter.GenerateReportAsync(new ProcessManageSummary(), outputPath);

        _mockFileWriter.Verify(x => x.WriteBytesAsync(outputPath, It.IsAny<byte[]>()), Times.Once);
    }

    #endregion

    #region Metadata Tests

    [Fact]
    public async Task GenerateReportAsync_WritesEnvironmentUrl()
    {
        var outputPath = Path.Combine(_tempDirectory, "report.xlsx");
        var summary = new ProcessManageSummary { EnvironmentUrl = "https://test.crm.dynamics.com" };

        await _reporter.GenerateReportAsync(summary, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheets.First();
        Assert.Equal("https://test.crm.dynamics.com", ws.Cell(3, 2).Value.ToString());
    }

    [Fact]
    public async Task GenerateReportAsync_WritesFormattedExecutionDate()
    {
        var outputPath = Path.Combine(_tempDirectory, "report.xlsx");
        var summary = new ProcessManageSummary
        {
            ExecutionDate = new DateTime(2026, 6, 13, 9, 0, 0, DateTimeKind.Utc)
        };

        await _reporter.GenerateReportAsync(summary, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheets.First();
        Assert.Equal("2026-06-13 09:00:00 UTC", ws.Cell(4, 2).Value.ToString());
    }

    [Fact]
    public async Task GenerateReportAsync_IsDryRun_WritesDryRunMode()
    {
        var outputPath = Path.Combine(_tempDirectory, "report.xlsx");
        var summary = new ProcessManageSummary { IsDryRun = true };

        await _reporter.GenerateReportAsync(summary, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheets.First();
        Assert.Equal("Dry Run (Preview)", ws.Cell(5, 2).Value.ToString());
    }

    [Fact]
    public async Task GenerateReportAsync_IsNotDryRun_WritesExecutedMode()
    {
        var outputPath = Path.Combine(_tempDirectory, "report.xlsx");
        var summary = new ProcessManageSummary { IsDryRun = false };

        await _reporter.GenerateReportAsync(summary, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheets.First();
        Assert.Equal("Executed", ws.Cell(5, 2).Value.ToString());
    }

    #endregion

    #region Summary Counts Tests

    [Fact]
    public async Task GenerateReportAsync_WritesSummaryCounts()
    {
        var outputPath = Path.Combine(_tempDirectory, "report.xlsx");
        var summary = new ProcessManageSummary
        {
            Results =
            {
                MakeResult("A", ProcessAction.Activated),
                MakeResult("B", ProcessAction.Deactivated),
                MakeResult("C", ProcessAction.NoChangeNeeded),
                MakeResult("D", ProcessAction.Failed)
            }
        };

        await _reporter.GenerateReportAsync(summary, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheets.First();
        Assert.Equal("Total Processes: 4", ws.Cell(8, 1).Value.ToString());
        Assert.Equal("Activated: 1",       ws.Cell(9, 1).Value.ToString());
        Assert.Equal("Deactivated: 1",     ws.Cell(10, 1).Value.ToString());
        Assert.Equal("Unchanged: 1",       ws.Cell(11, 1).Value.ToString());
        Assert.Equal("Failed: 1",          ws.Cell(12, 1).Value.ToString());
    }

    [Fact]
    public async Task GenerateReportAsync_HasFailures_FailedSummaryCellIsRed()
    {
        var outputPath = Path.Combine(_tempDirectory, "report-failures.xlsx");
        var summary = new ProcessManageSummary
        {
            Results = { MakeResult("Flow1", ProcessAction.Failed) }
        };

        await _reporter.GenerateReportAsync(summary, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheets.First();
        Assert.Equal(XLColor.Red, ws.Cell(12, 1).Style.Font.FontColor);
    }

    [Fact]
    public async Task GenerateReportAsync_NoFailures_FailedSummaryCellIsNotRed()
    {
        var outputPath = Path.Combine(_tempDirectory, "report-no-failures.xlsx");
        var summary = new ProcessManageSummary
        {
            Results = { MakeResult("Flow1", ProcessAction.Activated) }
        };

        await _reporter.GenerateReportAsync(summary, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheets.First();
        Assert.NotEqual(XLColor.Red, ws.Cell(12, 1).Style.Font.FontColor);
    }

    #endregion

    #region Result Row Tests

    [Fact]
    public async Task GenerateReportAsync_WithResults_SortsByProcessName()
    {
        var outputPath = Path.Combine(_tempDirectory, "report.xlsx");
        var summary = new ProcessManageSummary
        {
            Results =
            {
                MakeResult("Zebra Flow",  ProcessAction.Activated),
                MakeResult("Alpha Flow",  ProcessAction.Activated),
                MakeResult("Middle Flow", ProcessAction.Activated)
            }
        };

        await _reporter.GenerateReportAsync(summary, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheets.First();
        Assert.Equal("Alpha Flow",  ws.Cell(15, 1).Value.ToString());
        Assert.Equal("Middle Flow", ws.Cell(16, 1).Value.ToString());
        Assert.Equal("Zebra Flow",  ws.Cell(17, 1).Value.ToString());
    }

    [Fact]
    public async Task GenerateReportAsync_WithResult_WritesProcessStateColumns()
    {
        var outputPath = Path.Combine(_tempDirectory, "report.xlsx");
        var summary = new ProcessManageSummary
        {
            Results =
            {
                new ProcessManageResult
                {
                    Process = new ProcessInfo
                    {
                        Name = "My Flow",
                        Type = ProcessType.CloudFlow,
                        ExpectedState = ProcessState.Active,
                        CurrentState = ProcessState.Inactive
                    },
                    Action = ProcessAction.Activated
                }
            }
        };

        await _reporter.GenerateReportAsync(summary, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheets.First();
        Assert.Equal("My Flow",   ws.Cell(15, 1).Value.ToString());
        Assert.Equal("Cloud Flow", ws.Cell(15, 2).Value.ToString());
        Assert.Equal("Active",    ws.Cell(15, 3).Value.ToString());
        Assert.Equal("Inactive",  ws.Cell(15, 4).Value.ToString());
        Assert.Equal("Activated", ws.Cell(15, 5).Value.ToString());
    }

    [Fact]
    public async Task GenerateReportAsync_WithErrorMessage_WritesErrorMessageColumn()
    {
        var outputPath = Path.Combine(_tempDirectory, "report.xlsx");
        var summary = new ProcessManageSummary
        {
            Results =
            {
                new ProcessManageResult
                {
                    Process = new ProcessInfo { Name = "Bad Flow" },
                    Action = ProcessAction.Failed,
                    ErrorMessage = "Access denied"
                }
            }
        };

        await _reporter.GenerateReportAsync(summary, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheets.First();
        Assert.Equal("Access denied", ws.Cell(15, 6).Value.ToString());
    }

    #endregion

    #region Process Type Name Tests

    [Theory]
    [InlineData(ProcessType.Workflow, "Workflow")]
    [InlineData(ProcessType.BusinessRule, "Business Rule")]
    [InlineData(ProcessType.Action, "Action")]
    [InlineData(ProcessType.BusinessProcessFlow, "Business Process Flow")]
    [InlineData(ProcessType.CloudFlow, "Cloud Flow")]
    [InlineData(ProcessType.DuplicateDetectionRule, "Duplicate Detection Rule")]
    public async Task GenerateReportAsync_ProcessTypeName_IsCorrect(ProcessType type, string expectedName)
    {
        var outputPath = Path.Combine(_tempDirectory, $"report-{type}.xlsx");
        var summary = new ProcessManageSummary
        {
            Results = { new ProcessManageResult { Process = new ProcessInfo { Name = "P", Type = type }, Action = ProcessAction.NoChangeNeeded } }
        };

        await _reporter.GenerateReportAsync(summary, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheets.First();
        Assert.Equal(expectedName, ws.Cell(15, 2).Value.ToString());
    }

    #endregion

    #region Action Text Tests

    [Theory]
    [InlineData(ProcessAction.NoChangeNeeded, "Unchanged")]
    [InlineData(ProcessAction.Activated, "Activated")]
    [InlineData(ProcessAction.Deactivated, "Deactivated")]
    [InlineData(ProcessAction.Failed, "Failed")]
    public async Task GenerateReportAsync_ActionText_IsCorrect(ProcessAction action, string expectedText)
    {
        var outputPath = Path.Combine(_tempDirectory, $"report-{action}.xlsx");
        var summary = new ProcessManageSummary
        {
            Results = { MakeResult("P", action) }
        };

        await _reporter.GenerateReportAsync(summary, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheets.First();
        Assert.Equal(expectedText, ws.Cell(15, 5).Value.ToString());
    }

    #endregion

    #region Action Colour Tests

    [Theory]
    [InlineData(ProcessAction.Activated)]
    [InlineData(ProcessAction.Deactivated)]
    public async Task GenerateReportAsync_ActivatedOrDeactivated_ActionCellIsBlue(ProcessAction action)
    {
        var outputPath = Path.Combine(_tempDirectory, $"report-{action}.xlsx");
        var summary = new ProcessManageSummary
        {
            Results = { MakeResult("P", action) }
        };

        await _reporter.GenerateReportAsync(summary, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheets.First();
        Assert.Equal(XLColor.Blue, ws.Cell(15, 5).Style.Font.FontColor);
    }

    [Fact]
    public async Task GenerateReportAsync_FailedAction_ActionCellIsRed()
    {
        var outputPath = Path.Combine(_tempDirectory, "report-failed.xlsx");
        var summary = new ProcessManageSummary
        {
            Results = { MakeResult("P", ProcessAction.Failed) }
        };

        await _reporter.GenerateReportAsync(summary, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheets.First();
        Assert.Equal(XLColor.Red, ws.Cell(15, 5).Style.Font.FontColor);
    }

    #endregion

    #region Helper Methods

    private static ProcessManageResult MakeResult(string name, ProcessAction action) =>
        new()
        {
            Process = new ProcessInfo { Name = name },
            Action = action,
            Success = action != ProcessAction.Failed
        };

    #endregion
}
