using ClosedXML.Excel;
using Moq;
using PowerApps.CLI.Infrastructure;
using PowerApps.CLI.Models;
using PowerApps.CLI.Services;
using Xunit;

namespace PowerApps.CLI.Tests.Services;

public class ComparisonReporterTests : IDisposable
{
    private readonly Mock<IFileWriter> _mockFileWriter;
    private readonly ComparisonReporter _reporter;
    private readonly string _tempDirectory;

    public ComparisonReporterTests()
    {
        _mockFileWriter = new Mock<IFileWriter>();
        _reporter = new ComparisonReporter(_mockFileWriter.Object);
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"PowerAppsCLI_Tests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDirectory);

        // Setup file writer to actually write files for verification
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

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullFileWriter_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ComparisonReporter(null!));
    }

    #endregion

    #region GenerateReportAsync - No Differences Tests

    [Fact]
    public async Task GenerateReportAsync_WithNoDifferences_CreatesSummarySheetOnlyAsync()
    {
        // Arrange
        var outputPath = Path.Combine(_tempDirectory, "no-differences.xlsx");
        var comparisonResult = new ComparisonResult
        {
            SourceEnvironment = "https://dev.crm.dynamics.com",
            TargetEnvironment = "https://test.crm.dynamics.com",
            ComparisonDate = DateTime.UtcNow,
            TableResults =
            {
                new TableComparisonResult
                {
                    TableName = "Account",
                    SourceRecordCount = 5,
                    TargetRecordCount = 5
                }
            }
        };

        // Act
        await _reporter.GenerateReportAsync(comparisonResult, outputPath);

        // Assert
        Assert.True(File.Exists(outputPath));

        using var workbook = new XLWorkbook(outputPath);
        Assert.Single(workbook.Worksheets); // Only summary sheet
        Assert.True(workbook.Worksheets.Contains("Summary"));
    }

    #endregion

    #region GenerateReportAsync - With Differences Tests

    [Fact]
    public async Task GenerateReportAsync_WithDifferences_CreatesDetailSheetsAsync()
    {
        // Arrange
        var outputPath = Path.Combine(_tempDirectory, "with-differences.xlsx");
        var comparisonResult = new ComparisonResult
        {
            SourceEnvironment = "https://dev.crm.dynamics.com",
            TargetEnvironment = "https://test.crm.dynamics.com",
            ComparisonDate = DateTime.UtcNow,
            TableResults =
            {
                new TableComparisonResult
                {
                    TableName = "Category",
                    SourceRecordCount = 7,
                    TargetRecordCount = 8,
                    Differences =
                    {
                        new RecordDifference
                        {
                            RecordId = Guid.NewGuid(),
                            RecordName = "Female2",
                            DifferenceType = DifferenceType.Modified,
                            FieldDifferences =
                            {
                                new FieldDifference
                                {
                                    FieldName = "rob_name",
                                    SourceValue = "Female2",
                                    TargetValue = "Female"
                                }
                            }
                        },
                        new RecordDifference
                        {
                            RecordId = Guid.NewGuid(),
                            RecordName = "Rob Test",
                            DifferenceType = DifferenceType.Deleted
                        }
                    }
                }
            }
        };

        // Act
        await _reporter.GenerateReportAsync(comparisonResult, outputPath);

        // Assert
        Assert.True(File.Exists(outputPath));

        using var workbook = new XLWorkbook(outputPath);
        Assert.Equal(2, workbook.Worksheets.Count); // Summary + Category detail sheet
        Assert.True(workbook.Worksheets.Contains("Summary"));
        Assert.True(workbook.Worksheets.Contains("Category"));
    }

    [Fact]
    public async Task GenerateReportAsync_DetailSheet_ContainsCorrectDifferenceDataAsync()
    {
        // Arrange
        var outputPath = Path.Combine(_tempDirectory, "detail-data.xlsx");
        var recordId = Guid.NewGuid();
        var comparisonResult = new ComparisonResult
        {
            SourceEnvironment = "https://dev.crm.dynamics.com",
            TargetEnvironment = "https://test.crm.dynamics.com",
            ComparisonDate = DateTime.UtcNow,
            TableResults =
            {
                new TableComparisonResult
                {
                    TableName = "Test Table",
                    SourceRecordCount = 1,
                    TargetRecordCount = 1,
                    Differences =
                    {
                        new RecordDifference
                        {
                            RecordId = recordId,
                            RecordName = "Test Record",
                            DifferenceType = DifferenceType.Modified,
                            FieldDifferences =
                            {
                                new FieldDifference
                                {
                                    FieldName = "field1",
                                    SourceValue = "Value A",
                                    TargetValue = "Value B"
                                }
                            }
                        }
                    }
                }
            }
        };

        // Act
        await _reporter.GenerateReportAsync(comparisonResult, outputPath);

        // Assert
        using var workbook = new XLWorkbook(outputPath);
        var detailSheet = workbook.Worksheet("Test Table");
        Assert.NotNull(detailSheet);
        
        // Check that status is sentence case
        var statusCell = detailSheet.Cell(8, 3); // Row 8 is first data row
        Assert.Equal("Modified", statusCell.Value.ToString());
    }

    [Fact]
    public async Task GenerateReportAsync_MultipleTablesWithDifferences_CreatesAllDetailSheetsAsync()
    {
        // Arrange
        var outputPath = Path.Combine(_tempDirectory, "multiple-tables.xlsx");
        var comparisonResult = new ComparisonResult
        {
            SourceEnvironment = "https://dev.crm.dynamics.com",
            TargetEnvironment = "https://test.crm.dynamics.com",
            ComparisonDate = DateTime.UtcNow,
            TableResults =
            {
                new TableComparisonResult
                {
                    TableName = "Table1",
                    SourceRecordCount = 1,
                    TargetRecordCount = 2,
                    Differences =
                    {
                        new RecordDifference
                        {
                            RecordId = Guid.NewGuid(),
                            RecordName = "New Record",
                            DifferenceType = DifferenceType.New
                        }
                    }
                },
                new TableComparisonResult
                {
                    TableName = "Table2",
                    SourceRecordCount = 2,
                    TargetRecordCount = 1,
                    Differences =
                    {
                        new RecordDifference
                        {
                            RecordId = Guid.NewGuid(),
                            RecordName = "Deleted Record",
                            DifferenceType = DifferenceType.Deleted
                        }
                    }
                }
            }
        };

        // Act
        await _reporter.GenerateReportAsync(comparisonResult, outputPath);

        // Assert
        using var workbook = new XLWorkbook(outputPath);
        Assert.Equal(3, workbook.Worksheets.Count); // Summary + 2 detail sheets
        Assert.True(workbook.Worksheets.Contains("Table1"));
        Assert.True(workbook.Worksheets.Contains("Table2"));
    }

    #endregion

    #region Summary Sheet Tests

    [Fact]
    public async Task GenerateReportAsync_SummarySheet_ContainsEnvironmentInfoAsync()
    {
        // Arrange
        var outputPath = Path.Combine(_tempDirectory, "summary-env.xlsx");
        var comparisonResult = new ComparisonResult
        {
            SourceEnvironment = "https://dev.crm.dynamics.com",
            TargetEnvironment = "https://test.crm.dynamics.com",
            ComparisonDate = new DateTime(2026, 1, 25, 12, 0, 0, DateTimeKind.Utc)
        };

        // Act
        await _reporter.GenerateReportAsync(comparisonResult, outputPath);

        // Assert
        using var workbook = new XLWorkbook(outputPath);
        var summary = workbook.Worksheet("Summary");
        
        // Check environment URLs are present (row 3 = source, row 4 = target)
        var sourceEnvCell = summary.Cell(3, 2);
        Assert.Equal("https://dev.crm.dynamics.com", sourceEnvCell.Value.ToString());
        
        var targetEnvCell = summary.Cell(4, 2);
        Assert.Equal("https://test.crm.dynamics.com", targetEnvCell.Value.ToString());
    }

    #endregion

    #region Difference Type Tests

    [Fact]
    public async Task GenerateReportAsync_WithNewRecord_ShowsCorrectStatusAsync()
    {
        // Arrange
        var outputPath = Path.Combine(_tempDirectory, "new-record.xlsx");
        var comparisonResult = CreateComparisonResultWithDifference(DifferenceType.New);

        // Act
        await _reporter.GenerateReportAsync(comparisonResult, outputPath);

        // Assert
        using var workbook = new XLWorkbook(outputPath);
        var detailSheet = workbook.Worksheet("Test Table");
        var statusCell = detailSheet.Cell(8, 3); // First data row
        Assert.Equal("New", statusCell.Value.ToString());
    }

    [Fact]
    public async Task GenerateReportAsync_WithDeletedRecord_ShowsCorrectStatusAsync()
    {
        // Arrange
        var outputPath = Path.Combine(_tempDirectory, "deleted-record.xlsx");
        var comparisonResult = CreateComparisonResultWithDifference(DifferenceType.Deleted);

        // Act
        await _reporter.GenerateReportAsync(comparisonResult, outputPath);

        // Assert
        using var workbook = new XLWorkbook(outputPath);
        var detailSheet = workbook.Worksheet("Test Table");
        var statusCell = detailSheet.Cell(8, 3);
        Assert.Equal("Deleted", statusCell.Value.ToString());
    }

    #endregion

    #region Sheet Name Collision Tests

    [Fact]
    public async Task GenerateReportAsync_TablesSharing31CharacterPrefix_CreatesSeparateDetailSheetsAsync()
    {
        // Arrange - truncating both names to Excel's 31-character limit used to produce a
        // duplicate worksheet name and throw.
        var outputPath = Path.Combine(_tempDirectory, "table-name-collision.xlsx");
        var comparisonResult = new ComparisonResult
        {
            SourceEnvironment = "https://dev.crm.dynamics.com",
            TargetEnvironment = "https://test.crm.dynamics.com",
            ComparisonDate = DateTime.UtcNow,
            TableResults =
            {
                CreateTableResultWithDifference("rob_informationrequesttype_rob_first"),
                CreateTableResultWithDifference("rob_informationrequesttype_rob_second")
            }
        };

        // Act
        await _reporter.GenerateReportAsync(comparisonResult, outputPath);

        // Assert
        using var workbook = new XLWorkbook(outputPath);
        Assert.True(workbook.Worksheets.Contains("rob_informationrequesttype_rob_"));
        Assert.True(workbook.Worksheets.Contains("rob_informationrequesttype_ro~2"));
    }

    [Fact]
    public async Task GenerateReportAsync_TableAndRelationshipWithSameName_CreatesSeparateDetailSheetsAsync()
    {
        // Arrange - tables and relationships share the workbook's sheet namespace.
        var outputPath = Path.Combine(_tempDirectory, "cross-collision.xlsx");
        var comparisonResult = new ComparisonResult
        {
            SourceEnvironment = "https://dev.crm.dynamics.com",
            TargetEnvironment = "https://test.crm.dynamics.com",
            ComparisonDate = DateTime.UtcNow,
            TableResults = { CreateTableResultWithDifference("rob_sharedname") },
            RelationshipResults = { CreateRelationshipResultWithDifference("rob_sharedname") }
        };

        // Act
        await _reporter.GenerateReportAsync(comparisonResult, outputPath);

        // Assert
        using var workbook = new XLWorkbook(outputPath);
        Assert.True(workbook.Worksheets.Contains("rob_sharedname"));
        Assert.True(workbook.Worksheets.Contains("rob_sharedname~2"));
    }

    [Fact]
    public async Task GenerateReportAsync_TableNamedSummary_DoesNotCollideWithSummarySheetAsync()
    {
        // Arrange
        var outputPath = Path.Combine(_tempDirectory, "summary-collision.xlsx");
        var comparisonResult = new ComparisonResult
        {
            SourceEnvironment = "https://dev.crm.dynamics.com",
            TargetEnvironment = "https://test.crm.dynamics.com",
            ComparisonDate = DateTime.UtcNow,
            TableResults = { CreateTableResultWithDifference("Summary") }
        };

        // Act
        await _reporter.GenerateReportAsync(comparisonResult, outputPath);

        // Assert
        using var workbook = new XLWorkbook(outputPath);
        Assert.True(workbook.Worksheets.Contains("Summary~2"));
    }

    [Fact]
    public async Task GenerateReportAsync_CollidingTableNames_LinksSummaryToBothDetailSheetsAsync()
    {
        // Arrange
        var outputPath = Path.Combine(_tempDirectory, "collision-links.xlsx");
        var comparisonResult = new ComparisonResult
        {
            SourceEnvironment = "https://dev.crm.dynamics.com",
            TargetEnvironment = "https://test.crm.dynamics.com",
            ComparisonDate = DateTime.UtcNow,
            TableResults =
            {
                CreateTableResultWithDifference("rob_informationrequesttype_rob_first"),
                CreateTableResultWithDifference("rob_informationrequesttype_rob_second")
            }
        };

        // Act
        await _reporter.GenerateReportAsync(comparisonResult, outputPath);

        // Assert - every summary hyperlink must point at a sheet that actually exists
        using var workbook = new XLWorkbook(outputPath);
        var summarySheet = workbook.Worksheet("Summary");

        var targets = summarySheet.Hyperlinks
            .Where(h => !h.IsExternal)
            .Select(h => h.InternalAddress.Split('!')[0].Trim('\''))
            .Distinct()
            .ToList();

        Assert.Equal(2, targets.Count);
        Assert.All(targets, target => Assert.True(
            workbook.Worksheets.Contains(target),
            $"Summary links to '{target}', which is not a worksheet in the workbook"));
    }

    #endregion

    #region Helper Methods

    private static ComparisonResult CreateComparisonResultWithDifference(DifferenceType differenceType)
    {
        return new ComparisonResult
        {
            SourceEnvironment = "https://dev.crm.dynamics.com",
            TargetEnvironment = "https://test.crm.dynamics.com",
            ComparisonDate = DateTime.UtcNow,
            TableResults =
            {
                new TableComparisonResult
                {
                    TableName = "Test Table",
                    SourceRecordCount = 1,
                    TargetRecordCount = 1,
                    Differences =
                    {
                        new RecordDifference
                        {
                            RecordId = Guid.NewGuid(),
                            RecordName = "Test Record",
                            DifferenceType = differenceType
                        }
                    }
                }
            }
        };
    }

    private static TableComparisonResult CreateTableResultWithDifference(string tableName)
    {
        return new TableComparisonResult
        {
            TableName = tableName,
            SourceRecordCount = 1,
            TargetRecordCount = 0,
            Differences =
            {
                new RecordDifference
                {
                    RecordId = Guid.NewGuid(),
                    RecordName = "Test Record",
                    DifferenceType = DifferenceType.New
                }
            }
        };
    }

    private static RelationshipComparisonResult CreateRelationshipResultWithDifference(string relationshipName)
    {
        return new RelationshipComparisonResult
        {
            RelationshipName = relationshipName,
            IntersectEntity = relationshipName,
            SourceAssociationCount = 1,
            TargetAssociationCount = 0,
            Differences =
            {
                new AssociationDifference
                {
                    Entity1Id = Guid.NewGuid(),
                    Entity1Name = "Record 1",
                    Entity2Id = Guid.NewGuid(),
                    Entity2Name = "Record 2",
                    DifferenceType = DifferenceType.New
                }
            }
        };
    }

    #endregion
}
