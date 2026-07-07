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
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"PowerAppsCLI_Tests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDirectory);

        _mockFileWriter
            .Setup(x => x.WriteBytesAsync(It.IsAny<string>(), It.IsAny<byte[]>()))
            .Returns((string path, byte[] content) => File.WriteAllBytesAsync(path, content));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }

    [Fact]
    public void Constructor_WithNullFileWriter_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new MigrationReporter(null!));
    }

    [Fact]
    public async Task GenerateReportAsync_WithManyToManyResult_IncludesTargetExistingCountAsync()
    {
        // Arrange
        var outputPath = Path.Combine(_tempDirectory, "m2m-report.xlsx");
        var summary = new MigrationSummary
        {
            SourceEnvironment = "https://dev.crm.dynamics.com",
            TargetEnvironment = "https://test.crm.dynamics.com",
            ManyToManyResults =
            {
                new ManyToManyMigrationResult
                {
                    RelationshipName = "contact_leads",
                    Entity1Name = "contact",
                    Entity2Name = "lead",
                    SourceCount = 10,
                    TargetExistingCount = 4,
                    AssociatedCount = 6,
                    DisassociatedCount = 1
                }
            }
        };

        // Act
        await _reporter.GenerateReportAsync(summary, outputPath);

        // Assert
        using var workbook = new XLWorkbook(outputPath);
        var summarySheet = workbook.Worksheet("Migration Summary");

        var headerRow = summarySheet.Rows()
            .First(r => r.Cell(1).GetString() == "Relationship");
        Assert.Equal("Target Existing", headerRow.Cell(5).GetString());
        Assert.Equal("Associated", headerRow.Cell(6).GetString());
        Assert.Equal("Disassociated", headerRow.Cell(7).GetString());
        Assert.Equal("Errors", headerRow.Cell(8).GetString());

        var dataRow = summarySheet.Row(headerRow.RowNumber() + 1);
        Assert.Equal("contact_leads", dataRow.Cell(1).GetString());
        Assert.Equal(10, dataRow.Cell(4).GetValue<int>());
        Assert.Equal(4, dataRow.Cell(5).GetValue<int>());
        Assert.Equal(6, dataRow.Cell(6).GetValue<int>());
        Assert.Equal(1, dataRow.Cell(7).GetValue<int>());
    }
}
