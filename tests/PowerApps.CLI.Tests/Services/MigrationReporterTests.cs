using ClosedXML.Excel;
using Moq;
using PowerApps.CLI.Infrastructure;
using PowerApps.CLI.Models;
using PowerApps.CLI.Services;
using Xunit;

namespace PowerApps.CLI.Tests.Services;

public class MigrationReporterTests : IDisposable
{
    private readonly Mock<IFileWriter> _mockFileWriter;
    private readonly MigrationReporter _reporter;
    private readonly string _tempDirectory;

    public MigrationReporterTests()
    {
        _mockFileWriter = new Mock<IFileWriter>();
        _reporter = new MigrationReporter(_mockFileWriter.Object);
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"PowerAppsCLI_Migration_{Guid.NewGuid()}");
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
        Assert.Throws<ArgumentNullException>(() => new MigrationReporter(null!));
    }

    #endregion

    #region File Output Tests

    [Fact]
    public async Task GenerateReportAsync_WritesFileToDisk()
    {
        var outputPath = Path.Combine(_tempDirectory, "report.xlsx");

        await _reporter.GenerateReportAsync(new MigrationSummary(), outputPath);

        Assert.True(File.Exists(outputPath));
    }

    [Fact]
    public async Task GenerateReportAsync_CallsFileWriterWithCorrectPath()
    {
        var outputPath = Path.Combine(_tempDirectory, "report.xlsx");

        await _reporter.GenerateReportAsync(new MigrationSummary(), outputPath);

        _mockFileWriter.Verify(x => x.WriteBytesAsync(outputPath, It.IsAny<byte[]>()), Times.Once);
    }

    #endregion

    #region Sheet Structure Tests

    [Fact]
    public async Task GenerateReportAsync_NoErrors_OnlyCreatesSummarySheet()
    {
        var outputPath = Path.Combine(_tempDirectory, "report.xlsx");
        var summary = new MigrationSummary(); // HasErrors = false

        await _reporter.GenerateReportAsync(summary, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        Assert.Single(workbook.Worksheets);
        Assert.True(workbook.Worksheets.Contains("Migration Summary"));
    }

    [Fact]
    public async Task GenerateReportAsync_WithErrors_CreatesErrorsSheet()
    {
        var outputPath = Path.Combine(_tempDirectory, "report.xlsx");
        var summary = new MigrationSummary
        {
            TableResults =
            {
                new TableMigrationResult
                {
                    TableName = "account",
                    Errors = { new RecordError { TableName = "account", RecordId = Guid.NewGuid(), Phase = "Upsert", ErrorMessage = "Failed" } }
                }
            }
        };

        await _reporter.GenerateReportAsync(summary, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        Assert.Equal(2, workbook.Worksheets.Count);
        Assert.True(workbook.Worksheets.Contains("Migration Summary"));
        Assert.True(workbook.Worksheets.Contains("Errors"));
    }

    #endregion

    #region Metadata Tests

    [Fact]
    public async Task GenerateReportAsync_WritesSourceAndTargetEnvironments()
    {
        var outputPath = Path.Combine(_tempDirectory, "report.xlsx");
        var summary = new MigrationSummary
        {
            SourceEnvironment = "https://source.crm.dynamics.com",
            TargetEnvironment = "https://target.crm.dynamics.com"
        };

        await _reporter.GenerateReportAsync(summary, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheet("Migration Summary");
        Assert.Equal("https://source.crm.dynamics.com", ws.Cell(3, 2).Value.ToString());
        Assert.Equal("https://target.crm.dynamics.com", ws.Cell(4, 2).Value.ToString());
    }

    [Fact]
    public async Task GenerateReportAsync_WritesFormattedExecutionDate()
    {
        var outputPath = Path.Combine(_tempDirectory, "report.xlsx");
        var summary = new MigrationSummary
        {
            ExecutionDate = new DateTime(2026, 6, 13, 14, 0, 0, DateTimeKind.Utc)
        };

        await _reporter.GenerateReportAsync(summary, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheet("Migration Summary");
        Assert.Equal("2026-06-13 14:00:00 UTC", ws.Cell(5, 2).Value.ToString());
    }

    [Fact]
    public async Task GenerateReportAsync_IsDryRun_WritesDryRunMode()
    {
        var outputPath = Path.Combine(_tempDirectory, "report.xlsx");
        var summary = new MigrationSummary { IsDryRun = true };

        await _reporter.GenerateReportAsync(summary, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheet("Migration Summary");
        Assert.Equal("Dry Run (Preview)", ws.Cell(6, 2).Value.ToString());
    }

    [Fact]
    public async Task GenerateReportAsync_IsNotDryRun_WritesExecutedMode()
    {
        var outputPath = Path.Combine(_tempDirectory, "report.xlsx");
        var summary = new MigrationSummary { IsDryRun = false };

        await _reporter.GenerateReportAsync(summary, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheet("Migration Summary");
        Assert.Equal("Executed", ws.Cell(6, 2).Value.ToString());
    }

    [Fact]
    public async Task GenerateReportAsync_WritesFormattedDuration()
    {
        var outputPath = Path.Combine(_tempDirectory, "report.xlsx");
        var summary = new MigrationSummary { Duration = new TimeSpan(0, 0, 2, 35, 750) };

        await _reporter.GenerateReportAsync(summary, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheet("Migration Summary");
        Assert.Equal("02:35.750", ws.Cell(7, 2).Value.ToString());
    }

    #endregion

    #region Totals Tests

    [Fact]
    public async Task GenerateReportAsync_WritesTotalsAggregatedFromTableResults()
    {
        var outputPath = Path.Combine(_tempDirectory, "report.xlsx");
        var summary = new MigrationSummary
        {
            TableResults =
            {
                new TableMigrationResult { RecordCount = 10, UpsertedCount = 8, LookupsPatchedCount = 3, StateChangesCount = 2, SkippedCount = 2 },
                new TableMigrationResult { RecordCount = 5,  UpsertedCount = 5, LookupsPatchedCount = 1, StateChangesCount = 0, SkippedCount = 0 }
            }
        };

        await _reporter.GenerateReportAsync(summary, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheet("Migration Summary");
        Assert.Equal("15", ws.Cell(10, 2).Value.ToString()); // TotalRecords
        Assert.Equal("13", ws.Cell(11, 2).Value.ToString()); // TotalUpserted
        Assert.Equal("4",  ws.Cell(12, 2).Value.ToString()); // TotalLookupsPatched
        Assert.Equal("2",  ws.Cell(13, 2).Value.ToString()); // TotalStateChanges
        Assert.Equal("2",  ws.Cell(14, 2).Value.ToString()); // TotalSkipped
    }

    [Fact]
    public async Task GenerateReportAsync_HasErrors_ErrorsTotalCellIsRed()
    {
        var outputPath = Path.Combine(_tempDirectory, "report.xlsx");
        var summary = new MigrationSummary
        {
            TableResults =
            {
                new TableMigrationResult
                {
                    Errors = { new RecordError { TableName = "account", RecordId = Guid.NewGuid(), Phase = "Upsert", ErrorMessage = "Fail" } }
                }
            }
        };

        await _reporter.GenerateReportAsync(summary, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheet("Migration Summary");
        Assert.Equal(XLColor.Red, ws.Cell(17, 2).Style.Font.FontColor);
    }

    [Fact]
    public async Task GenerateReportAsync_NoErrors_ErrorsTotalCellIsNotRed()
    {
        var outputPath = Path.Combine(_tempDirectory, "report.xlsx");

        await _reporter.GenerateReportAsync(new MigrationSummary(), outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheet("Migration Summary");
        Assert.NotEqual(XLColor.Red, ws.Cell(17, 2).Style.Font.FontColor);
    }

    #endregion

    #region Table Results Tests

    [Fact]
    public async Task GenerateReportAsync_WithTableResults_WritesTableRowData()
    {
        var outputPath = Path.Combine(_tempDirectory, "report.xlsx");
        var summary = new MigrationSummary
        {
            TableResults =
            {
                new TableMigrationResult
                {
                    TableName = "contact",
                    RecordCount = 20,
                    UpsertedCount = 18,
                    LookupsPatchedCount = 5,
                    StateChangesCount = 3,
                    SkippedCount = 2
                }
            }
        };

        await _reporter.GenerateReportAsync(summary, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheet("Migration Summary");
        Assert.Equal("contact", ws.Cell(20, 1).Value.ToString());
        Assert.Equal("20", ws.Cell(20, 2).Value.ToString());
        Assert.Equal("18", ws.Cell(20, 3).Value.ToString());
        Assert.Equal("5",  ws.Cell(20, 4).Value.ToString());
        Assert.Equal("3",  ws.Cell(20, 5).Value.ToString());
        Assert.Equal("2",  ws.Cell(20, 6).Value.ToString());
    }

    #endregion

    #region ManyToMany Results Tests

    [Fact]
    public async Task GenerateReportAsync_NoManyToManyResults_NoManyToManySection()
    {
        var outputPath = Path.Combine(_tempDirectory, "report.xlsx");
        var summary = new MigrationSummary
        {
            TableResults = { new TableMigrationResult { TableName = "account", RecordCount = 1 } }
        };

        await _reporter.GenerateReportAsync(summary, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheet("Migration Summary");
        // Row 21 onwards should be empty (no N:N header)
        Assert.Equal(string.Empty, ws.Cell(23, 1).Value.ToString());
    }

    [Fact]
    public async Task GenerateReportAsync_WithManyToManyResults_WritesManyToManyData()
    {
        var outputPath = Path.Combine(_tempDirectory, "report.xlsx");
        var summary = new MigrationSummary
        {
            TableResults = { new TableMigrationResult { TableName = "account", RecordCount = 2 } },
            ManyToManyResults =
            {
                new ManyToManyMigrationResult
                {
                    RelationshipName = "account_contact",
                    Entity1Name = "account",
                    Entity2Name = "contact",
                    SourceCount = 5,
                    AssociatedCount = 3,
                    DisassociatedCount = 1
                }
            }
        };

        await _reporter.GenerateReportAsync(summary, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheet("Migration Summary");
        // N:N section starts after table data + blank + "N:N Relationships" heading + header row
        // Row 20: account data, row 21: blank, row 22: "N:N Relationships", row 23: header, row 24: data
        Assert.Equal("account_contact", ws.Cell(24, 1).Value.ToString());
        Assert.Equal("account", ws.Cell(24, 2).Value.ToString());
        Assert.Equal("contact", ws.Cell(24, 3).Value.ToString());
        Assert.Equal("5", ws.Cell(24, 4).Value.ToString());
        Assert.Equal("3", ws.Cell(24, 5).Value.ToString());
        Assert.Equal("1", ws.Cell(24, 6).Value.ToString());
    }

    #endregion

    #region Errors Sheet Tests

    [Fact]
    public async Task GenerateReportAsync_ErrorsSheet_ContainsTableAndManyToManyErrors()
    {
        var outputPath = Path.Combine(_tempDirectory, "report.xlsx");
        var tableRecordId = Guid.NewGuid();
        var m2mRecordId = Guid.NewGuid();
        var summary = new MigrationSummary
        {
            TableResults =
            {
                new TableMigrationResult
                {
                    TableName = "account",
                    Errors =
                    {
                        new RecordError { TableName = "account", RecordId = tableRecordId, Phase = "Upsert", ErrorMessage = "Table error" }
                    }
                }
            },
            ManyToManyResults =
            {
                new ManyToManyMigrationResult
                {
                    RelationshipName = "account_contact",
                    Errors =
                    {
                        new RecordError { TableName = "account_contact", RecordId = m2mRecordId, Phase = "Associate", ErrorMessage = "M2M error" }
                    }
                }
            }
        };

        await _reporter.GenerateReportAsync(summary, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var errorsSheet = workbook.Worksheet("Errors");
        Assert.NotNull(errorsSheet);
        // Row 1 is the header; data starts at row 2
        Assert.Equal("account", errorsSheet.Cell(2, 1).Value.ToString());
        Assert.Equal(tableRecordId.ToString(), errorsSheet.Cell(2, 2).Value.ToString());
        Assert.Equal("Upsert", errorsSheet.Cell(2, 3).Value.ToString());
        Assert.Equal("Table error", errorsSheet.Cell(2, 4).Value.ToString());

        Assert.Equal("account_contact", errorsSheet.Cell(3, 1).Value.ToString());
        Assert.Equal(m2mRecordId.ToString(), errorsSheet.Cell(3, 2).Value.ToString());
        Assert.Equal("Associate", errorsSheet.Cell(3, 3).Value.ToString());
        Assert.Equal("M2M error", errorsSheet.Cell(3, 4).Value.ToString());
    }

    #endregion
}
