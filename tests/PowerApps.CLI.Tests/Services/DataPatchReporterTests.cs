using ClosedXML.Excel;
using Moq;
using PowerApps.CLI.Infrastructure;
using PowerApps.CLI.Models;
using PowerApps.CLI.Services;
using Xunit;

namespace PowerApps.CLI.Tests.Services;

public class DataPatchReporterTests : IDisposable
{
    private readonly Mock<IFileWriter> _mockFileWriter;
    private readonly DataPatchReporter _reporter;
    private readonly string _tempDirectory;

    public DataPatchReporterTests()
    {
        _mockFileWriter = new Mock<IFileWriter>();
        _reporter = new DataPatchReporter(_mockFileWriter.Object);
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"PowerAppsCLI_DataPatch_{Guid.NewGuid()}");
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
        Assert.Throws<ArgumentNullException>(() => new DataPatchReporter(null!));
    }

    #endregion

    #region File Output Tests

    [Fact]
    public async Task GenerateReportAsync_WritesFileToDisk()
    {
        var outputPath = Path.Combine(_tempDirectory, "report.xlsx");

        await _reporter.GenerateReportAsync(new DataPatchSummary(), outputPath);

        Assert.True(File.Exists(outputPath));
    }

    [Fact]
    public async Task GenerateReportAsync_CallsFileWriterWithCorrectPath()
    {
        var outputPath = Path.Combine(_tempDirectory, "report.xlsx");

        await _reporter.GenerateReportAsync(new DataPatchSummary(), outputPath);

        _mockFileWriter.Verify(x => x.WriteBytesAsync(outputPath, It.IsAny<byte[]>()), Times.Once);
    }

    #endregion

    #region Metadata Tests

    [Fact]
    public async Task GenerateReportAsync_WritesEnvironmentUrl()
    {
        var outputPath = Path.Combine(_tempDirectory, "report.xlsx");
        var summary = new DataPatchSummary { EnvironmentUrl = "https://test.crm.dynamics.com" };

        await _reporter.GenerateReportAsync(summary, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheets.First();
        Assert.Equal("https://test.crm.dynamics.com", ws.Cell(3, 2).Value.ToString());
    }

    [Fact]
    public async Task GenerateReportAsync_WritesFormattedExecutionDate()
    {
        var outputPath = Path.Combine(_tempDirectory, "report.xlsx");
        var summary = new DataPatchSummary
        {
            ExecutionDate = new DateTime(2026, 6, 13, 10, 30, 0, DateTimeKind.Utc)
        };

        await _reporter.GenerateReportAsync(summary, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheets.First();
        Assert.Equal("2026-06-13 10:30:00 UTC", ws.Cell(4, 2).Value.ToString());
    }

    #endregion

    #region Summary Counts Tests

    [Fact]
    public async Task GenerateReportAsync_WritesSummaryCounts()
    {
        var outputPath = Path.Combine(_tempDirectory, "report.xlsx");
        var summary = new DataPatchSummary
        {
            Results =
            {
                new PatchResult { Status = PatchStatus.Updated },
                new PatchResult { Status = PatchStatus.Unchanged },
                new PatchResult { Status = PatchStatus.NotFound }
            }
        };

        await _reporter.GenerateReportAsync(summary, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheets.First();
        Assert.Equal("Updated:   1", ws.Cell(7, 1).Value.ToString());
        Assert.Equal("Unchanged: 1", ws.Cell(8, 1).Value.ToString());
        Assert.Equal("Failed:    1", ws.Cell(9, 1).Value.ToString());
    }

    [Fact]
    public async Task GenerateReportAsync_HasFailures_FailedSummaryCellIsRed()
    {
        var outputPath = Path.Combine(_tempDirectory, "report-failures.xlsx");
        var summary = new DataPatchSummary
        {
            Results = { new PatchResult { Status = PatchStatus.NotFound } }
        };

        await _reporter.GenerateReportAsync(summary, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheets.First();
        Assert.Equal(XLColor.Red, ws.Cell(9, 1).Style.Font.FontColor);
    }

    [Fact]
    public async Task GenerateReportAsync_NoFailures_FailedSummaryCellIsNotRed()
    {
        var outputPath = Path.Combine(_tempDirectory, "report-no-failures.xlsx");
        var summary = new DataPatchSummary
        {
            Results = { new PatchResult { Status = PatchStatus.Updated } }
        };

        await _reporter.GenerateReportAsync(summary, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheets.First();
        Assert.NotEqual(XLColor.Red, ws.Cell(9, 1).Style.Font.FontColor);
    }

    #endregion

    #region Result Row Data Tests

    [Fact]
    public async Task GenerateReportAsync_WithResult_WritesRowData()
    {
        var outputPath = Path.Combine(_tempDirectory, "report.xlsx");
        var summary = new DataPatchSummary
        {
            Results =
            {
                new PatchResult
                {
                    Entity = "account",
                    Key = "ACC001",
                    Field = "name",
                    OldValue = "Old Name",
                    NewValue = "New Name",
                    Status = PatchStatus.Updated
                }
            }
        };

        await _reporter.GenerateReportAsync(summary, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheets.First();
        Assert.Equal("account", ws.Cell(12, 1).Value.ToString());
        Assert.Equal("ACC001", ws.Cell(12, 2).Value.ToString());
        Assert.Equal("name", ws.Cell(12, 3).Value.ToString());
        Assert.Equal("Old Name", ws.Cell(12, 4).Value.ToString());
        Assert.Equal("New Name", ws.Cell(12, 5).Value.ToString());
    }

    [Fact]
    public async Task GenerateReportAsync_NullOldAndNewValues_WriteEmptyStrings()
    {
        var outputPath = Path.Combine(_tempDirectory, "report.xlsx");
        var summary = new DataPatchSummary
        {
            Results = { new PatchResult { OldValue = null, NewValue = null, Status = PatchStatus.Updated } }
        };

        await _reporter.GenerateReportAsync(summary, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheets.First();
        Assert.Equal(string.Empty, ws.Cell(12, 4).Value.ToString());
        Assert.Equal(string.Empty, ws.Cell(12, 5).Value.ToString());
    }

    #endregion

    #region Status Text Tests

    [Theory]
    [InlineData(PatchStatus.Updated, "Updated")]
    [InlineData(PatchStatus.Unchanged, "Unchanged")]
    [InlineData(PatchStatus.NotFound, "Not Found")]
    [InlineData(PatchStatus.AmbiguousMatch, "Ambiguous Match")]
    public async Task GenerateReportAsync_StatusText_IsCorrect(PatchStatus status, string expectedText)
    {
        var outputPath = Path.Combine(_tempDirectory, $"report-{status}.xlsx");
        var summary = new DataPatchSummary
        {
            Results = { new PatchResult { Status = status } }
        };

        await _reporter.GenerateReportAsync(summary, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheets.First();
        Assert.Equal(expectedText, ws.Cell(12, 6).Value.ToString());
    }

    [Fact]
    public async Task GenerateReportAsync_ErrorStatus_IncludesErrorMessage()
    {
        var outputPath = Path.Combine(_tempDirectory, "report-error.xlsx");
        var summary = new DataPatchSummary
        {
            Results = { new PatchResult { Status = PatchStatus.Error, ErrorMessage = "Record locked" } }
        };

        await _reporter.GenerateReportAsync(summary, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheets.First();
        Assert.Equal("Error: Record locked", ws.Cell(12, 6).Value.ToString());
    }

    #endregion

    #region Status Colour Tests

    [Theory]
    [InlineData(PatchStatus.NotFound)]
    [InlineData(PatchStatus.AmbiguousMatch)]
    [InlineData(PatchStatus.Error)]
    public async Task GenerateReportAsync_FailureStatus_StatusCellIsRed(PatchStatus status)
    {
        var outputPath = Path.Combine(_tempDirectory, $"report-color-{status}.xlsx");
        var summary = new DataPatchSummary
        {
            Results = { new PatchResult { Status = status } }
        };

        await _reporter.GenerateReportAsync(summary, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheets.First();
        Assert.Equal(XLColor.Red, ws.Cell(12, 6).Style.Font.FontColor);
    }

    [Fact]
    public async Task GenerateReportAsync_UpdatedStatus_StatusCellIsDarkGreen()
    {
        var outputPath = Path.Combine(_tempDirectory, "report-updated.xlsx");
        var summary = new DataPatchSummary
        {
            Results = { new PatchResult { Status = PatchStatus.Updated } }
        };

        await _reporter.GenerateReportAsync(summary, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheets.First();
        Assert.Equal(XLColor.DarkGreen, ws.Cell(12, 6).Style.Font.FontColor);
    }

    [Fact]
    public async Task GenerateReportAsync_UnchangedStatus_StatusCellIsGray()
    {
        var outputPath = Path.Combine(_tempDirectory, "report-unchanged.xlsx");
        var summary = new DataPatchSummary
        {
            Results = { new PatchResult { Status = PatchStatus.Unchanged } }
        };

        await _reporter.GenerateReportAsync(summary, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheets.First();
        Assert.Equal(XLColor.Gray, ws.Cell(12, 6).Style.Font.FontColor);
    }

    #endregion
}
