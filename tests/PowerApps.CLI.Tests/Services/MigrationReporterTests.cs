using ClosedXML.Excel;
using PowerApps.CLI.Infrastructure;
using PowerApps.CLI.Models;
using PowerApps.CLI.Services;

namespace PowerApps.CLI.Tests.Services;

public class MigrationReporterTests
{
    private readonly Mock<IFileWriter> _mockFileWriter;
    private readonly MigrationReporter _reporter;
    private byte[]? _capturedBytes;

    public MigrationReporterTests()
    {
        _mockFileWriter = new Mock<IFileWriter>();
        _reporter = new MigrationReporter(_mockFileWriter.Object);

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

    // --- Workbook structure ---

    [Fact]
    public async Task GenerateReportAsync_AlwaysCreatesMigrationSummarySheet()
    {
        await _reporter.GenerateReportAsync(new MigrationSummary(), "report.xlsx");

        Assert.NotNull(OpenWorkbook().Worksheet("Migration Summary"));
    }

    [Fact]
    public async Task GenerateReportAsync_WithNoErrors_DoesNotCreateErrorsSheet()
    {
        await _reporter.GenerateReportAsync(new MigrationSummary(), "report.xlsx");

        var workbook = OpenWorkbook();
        Assert.Null(workbook.Worksheets.FirstOrDefault(ws => ws.Name == "Errors"));
    }

    [Fact]
    public async Task GenerateReportAsync_WithErrors_CreatesErrorsSheet()
    {
        var summary = new MigrationSummary
        {
            TableResults =
            [
                new TableMigrationResult
                {
                    TableName = "account",
                    Errors = [ new RecordError { TableName = "account", RecordId = Guid.NewGuid(), Phase = "Upsert", ErrorMessage = "Timeout" } ]
                }
            ]
        };

        await _reporter.GenerateReportAsync(summary, "report.xlsx");

        Assert.NotNull(OpenWorkbook().Worksheet("Errors"));
    }

    // --- Summary sheet metadata ---

    [Fact]
    public async Task GenerateReportAsync_WritesSourceAndTargetEnvironments()
    {
        var summary = new MigrationSummary
        {
            SourceEnvironment = "https://source.crm.dynamics.com",
            TargetEnvironment = "https://target.crm.dynamics.com"
        };

        await _reporter.GenerateReportAsync(summary, "report.xlsx");

        var ws = OpenWorkbook().Worksheet("Migration Summary");
        Assert.Equal("https://source.crm.dynamics.com", ws.Cell(3, 2).GetString());
        Assert.Equal("https://target.crm.dynamics.com", ws.Cell(4, 2).GetString());
    }

    [Fact]
    public async Task GenerateReportAsync_WhenDryRun_WritesDryRunMode()
    {
        var summary = new MigrationSummary { IsDryRun = true };

        await _reporter.GenerateReportAsync(summary, "report.xlsx");

        var ws = OpenWorkbook().Worksheet("Migration Summary");
        Assert.Equal("Dry Run (Preview)", ws.Cell(6, 2).GetString());
    }

    [Fact]
    public async Task GenerateReportAsync_WhenExecuted_WritesExecutedMode()
    {
        var summary = new MigrationSummary { IsDryRun = false };

        await _reporter.GenerateReportAsync(summary, "report.xlsx");

        var ws = OpenWorkbook().Worksheet("Migration Summary");
        Assert.Equal("Executed", ws.Cell(6, 2).GetString());
    }

    // --- Table results ---

    [Fact]
    public async Task GenerateReportAsync_WithTableResult_WritesTableRowWithCounts()
    {
        var summary = new MigrationSummary
        {
            TableResults =
            [
                new TableMigrationResult
                {
                    TableName          = "account",
                    RecordCount        = 10,
                    UpsertedCount      = 8,
                    LookupsPatchedCount = 3,
                    StateChangesCount  = 1,
                    SkippedCount       = 2
                }
            ]
        };

        await _reporter.GenerateReportAsync(summary, "report.xlsx");

        var ws = OpenWorkbook().Worksheet("Migration Summary");
        var dataRow = ws.RangeUsed()!.Rows()
            .First(r => r.Cell(1).GetString() == "account");

        Assert.Equal(10, dataRow.Cell(2).GetValue<int>());
        Assert.Equal(8,  dataRow.Cell(3).GetValue<int>());
        Assert.Equal(3,  dataRow.Cell(4).GetValue<int>());
        Assert.Equal(1,  dataRow.Cell(5).GetValue<int>());
        Assert.Equal(2,  dataRow.Cell(6).GetValue<int>());
    }

    // --- N:N results ---

    [Fact]
    public async Task GenerateReportAsync_WithManyToManyResults_WritesRelationshipSection()
    {
        var summary = new MigrationSummary
        {
            ManyToManyResults =
            [
                new ManyToManyMigrationResult
                {
                    RelationshipName  = "account_contact",
                    Entity1Name       = "account",
                    Entity2Name       = "contact",
                    SourceCount       = 5,
                    AssociatedCount   = 3,
                    DisassociatedCount = 1
                }
            ]
        };

        await _reporter.GenerateReportAsync(summary, "report.xlsx");

        var ws = OpenWorkbook().Worksheet("Migration Summary");
        var dataRow = ws.RangeUsed()!.Rows()
            .First(r => r.Cell(1).GetString() == "account_contact");

        Assert.Equal("account", dataRow.Cell(2).GetString());
        Assert.Equal("contact", dataRow.Cell(3).GetString());
        Assert.Equal(5, dataRow.Cell(4).GetValue<int>());
        Assert.Equal(3, dataRow.Cell(5).GetValue<int>());
        Assert.Equal(1, dataRow.Cell(6).GetValue<int>());
    }

    [Fact]
    public async Task GenerateReportAsync_WithNoManyToManyResults_DoesNotWriteRelationshipSection()
    {
        var summary = new MigrationSummary
        {
            TableResults = [ new TableMigrationResult { TableName = "account" } ]
        };

        await _reporter.GenerateReportAsync(summary, "report.xlsx");

        var ws = OpenWorkbook().Worksheet("Migration Summary");
        Assert.DoesNotContain(ws.RangeUsed()!.Rows(), r => r.Cell(1).GetString() == "N:N Relationships");
    }

    // --- Errors sheet ---

    [Fact]
    public async Task GenerateReportAsync_WithErrors_WritesErrorDetailsToErrorsSheet()
    {
        var recordId = Guid.NewGuid();
        var summary = new MigrationSummary
        {
            TableResults =
            [
                new TableMigrationResult
                {
                    TableName = "contact",
                    Errors =
                    [
                        new RecordError
                        {
                            TableName    = "contact",
                            RecordId     = recordId,
                            Phase        = "Upsert",
                            ErrorMessage = "Duplicate key violation"
                        }
                    ]
                }
            ]
        };

        await _reporter.GenerateReportAsync(summary, "report.xlsx");

        var ws = OpenWorkbook().Worksheet("Errors");
        var dataRow = ws.RangeUsed()!.Rows()
            .First(r => r.Cell(1).GetString() == "contact");

        Assert.Equal(recordId.ToString(), dataRow.Cell(2).GetString());
        Assert.Equal("Upsert",                 dataRow.Cell(3).GetString());
        Assert.Equal("Duplicate key violation", dataRow.Cell(4).GetString());
    }

    // --- File output ---

    [Fact]
    public async Task GenerateReportAsync_WritesToSpecifiedOutputPath()
    {
        await _reporter.GenerateReportAsync(new MigrationSummary(), "my-migration-report.xlsx");

        _mockFileWriter.Verify(x => x.WriteBytesAsync("my-migration-report.xlsx", It.IsAny<byte[]>()), Times.Once);
    }
}
