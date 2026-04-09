using ClosedXML.Excel;
using PowerApps.CLI.Infrastructure;
using PowerApps.CLI.Models;
using PowerApps.CLI.Services;

namespace PowerApps.CLI.Tests.Services;

public class DataPatchReporterTests
{
    private readonly Mock<IFileWriter> _mockFileWriter;
    private readonly DataPatchReporter _reporter;
    private byte[]? _capturedBytes;

    public DataPatchReporterTests()
    {
        _mockFileWriter = new Mock<IFileWriter>();
        _reporter = new DataPatchReporter(_mockFileWriter.Object);

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

    // --- Header and metadata ---

    [Fact]
    public async Task GenerateReportAsync_WritesEnvironmentUrlToMetadata()
    {
        var summary = new DataPatchSummary { EnvironmentUrl = "https://test.crm.dynamics.com" };

        await _reporter.GenerateReportAsync(summary, "report.xlsx");

        var ws = OpenWorkbook().Worksheet("Data Patch");
        Assert.Equal("https://test.crm.dynamics.com", ws.Cell(3, 2).GetString());
    }

    [Fact]
    public async Task GenerateReportAsync_WritesBoldTitleInCellA1()
    {
        var summary = new DataPatchSummary();

        await _reporter.GenerateReportAsync(summary, "report.xlsx");

        var cell = OpenWorkbook().Worksheet("Data Patch").Cell(1, 1);
        Assert.Equal("Data Patch Report", cell.GetString());
        Assert.True(cell.Style.Font.Bold);
    }

    // --- Summary counts ---

    [Fact]
    public async Task GenerateReportAsync_WithNoResults_WritesSummaryZeroCounts()
    {
        var summary = new DataPatchSummary();

        await _reporter.GenerateReportAsync(summary, "report.xlsx");

        var ws = OpenWorkbook().Worksheet("Data Patch");
        // Summary rows start at row 6 (row 3=env, 4=date, 5=blank, 6=Summary label, 7=Updated, 8=Unchanged, 9=Failed)
        Assert.Contains("0", ws.Cell(7, 1).GetString()); // Updated
        Assert.Contains("0", ws.Cell(8, 1).GetString()); // Unchanged
        Assert.Contains("0", ws.Cell(9, 1).GetString()); // Failed
    }

    [Fact]
    public async Task GenerateReportAsync_WithMixedResults_WritesSummaryCountsCorrectly()
    {
        var summary = new DataPatchSummary
        {
            Results =
            [
                new PatchResult { Status = PatchStatus.Updated },
                new PatchResult { Status = PatchStatus.Updated },
                new PatchResult { Status = PatchStatus.Unchanged },
                new PatchResult { Status = PatchStatus.NotFound }
            ]
        };

        await _reporter.GenerateReportAsync(summary, "report.xlsx");

        var ws = OpenWorkbook().Worksheet("Data Patch");
        Assert.Contains("2", ws.Cell(7, 1).GetString()); // Updated: 2
        Assert.Contains("1", ws.Cell(8, 1).GetString()); // Unchanged: 1
        Assert.Contains("1", ws.Cell(9, 1).GetString()); // Failed: 1
    }

    // --- Result rows ---

    [Fact]
    public async Task GenerateReportAsync_WithUpdatedResult_WritesRowWithCorrectValues()
    {
        var summary = new DataPatchSummary
        {
            Results =
            [
                new PatchResult
                {
                    Entity    = "account",
                    Key       = "Contoso",
                    Field     = "telephone1",
                    OldValue  = "01234",
                    NewValue  = "05678",
                    Status    = PatchStatus.Updated
                }
            ]
        };

        await _reporter.GenerateReportAsync(summary, "report.xlsx");

        var ws = OpenWorkbook().Worksheet("Data Patch");
        // Table header is at row 11, data starts at row 12
        var dataRow = ws.RangeUsed()!.Rows()
            .First(r => r.Cell(1).GetString() == "account");

        Assert.Equal("Contoso", dataRow.Cell(2).GetString());
        Assert.Equal("telephone1", dataRow.Cell(3).GetString());
        Assert.Equal("01234", dataRow.Cell(4).GetString());
        Assert.Equal("05678", dataRow.Cell(5).GetString());
        Assert.Equal("Updated", dataRow.Cell(6).GetString());
    }

    [Fact]
    public async Task GenerateReportAsync_WithErrorResult_WritesErrorMessageInStatusCell()
    {
        var summary = new DataPatchSummary
        {
            Results =
            [
                new PatchResult
                {
                    Entity       = "contact",
                    Key          = "Jane",
                    Field        = "emailaddress1",
                    Status       = PatchStatus.Error,
                    ErrorMessage = "Connection timeout"
                }
            ]
        };

        await _reporter.GenerateReportAsync(summary, "report.xlsx");

        var ws = OpenWorkbook().Worksheet("Data Patch");
        var dataRow = ws.RangeUsed()!.Rows()
            .First(r => r.Cell(1).GetString() == "contact");

        Assert.Contains("Connection timeout", dataRow.Cell(6).GetString());
    }

    [Fact]
    public async Task GenerateReportAsync_WithNotFoundResult_WritesNotFoundStatus()
    {
        var summary = new DataPatchSummary
        {
            Results =
            [
                new PatchResult { Entity = "account", Status = PatchStatus.NotFound }
            ]
        };

        await _reporter.GenerateReportAsync(summary, "report.xlsx");

        var ws = OpenWorkbook().Worksheet("Data Patch");
        var dataRow = ws.RangeUsed()!.Rows()
            .First(r => r.Cell(1).GetString() == "account");

        Assert.Equal("Not Found", dataRow.Cell(6).GetString());
    }

    [Fact]
    public async Task GenerateReportAsync_WithAmbiguousMatchResult_WritesAmbiguousMatchStatus()
    {
        var summary = new DataPatchSummary
        {
            Results =
            [
                new PatchResult { Entity = "account", Status = PatchStatus.AmbiguousMatch }
            ]
        };

        await _reporter.GenerateReportAsync(summary, "report.xlsx");

        var ws = OpenWorkbook().Worksheet("Data Patch");
        var dataRow = ws.RangeUsed()!.Rows()
            .First(r => r.Cell(1).GetString() == "account");

        Assert.Equal("Ambiguous Match", dataRow.Cell(6).GetString());
    }

    // --- File output ---

    [Fact]
    public async Task GenerateReportAsync_WritesToSpecifiedOutputPath()
    {
        var summary = new DataPatchSummary();

        await _reporter.GenerateReportAsync(summary, "my-report.xlsx");

        _mockFileWriter.Verify(x => x.WriteBytesAsync("my-report.xlsx", It.IsAny<byte[]>()), Times.Once);
    }

    [Fact]
    public async Task GenerateReportAsync_WritesValidExcelWorkbook()
    {
        var summary = new DataPatchSummary();

        await _reporter.GenerateReportAsync(summary, "report.xlsx");

        // Should not throw — i.e. the bytes represent a valid workbook
        var workbook = OpenWorkbook();
        Assert.NotNull(workbook.Worksheet("Data Patch"));
    }
}
