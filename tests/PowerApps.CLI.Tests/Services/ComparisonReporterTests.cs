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

    private static ComparisonResult CreateResultWithTableDifference() =>
        new()
        {
            TableResults =
            {
                new TableComparisonResult
                {
                    TableName = "account",
                    SourceRecordCount = 5,
                    TargetRecordCount = 4,
                    Differences =
                    {
                        new RecordDifference { RecordId = Guid.NewGuid(), RecordName = "Test", DifferenceType = DifferenceType.New }
                    }
                }
            }
        };

    private static RelationshipComparisonResult CreateRelationshipResult(string name, bool hasDifferences)
    {
        var rel = new RelationshipComparisonResult
        {
            RelationshipName = name,
            IntersectEntity = "intersectentity",
            SourceAssociationCount = 3,
            TargetAssociationCount = 2
        };
        if (hasDifferences)
        {
            rel.Differences.Add(new AssociationDifference
            {
                Entity1Id = Guid.NewGuid(),
                Entity1Name = "Entity 1",
                Entity2Id = Guid.NewGuid(),
                Entity2Name = "Entity 2",
                DifferenceType = DifferenceType.New
            });
        }
        return rel;
    }

    #endregion

    #region No Differences Message Tests

    [Fact]
    public async Task GenerateReportAsync_NoDifferences_ShowsCleanMessage()
    {
        var outputPath = Path.Combine(_tempDirectory, "clean-message.xlsx");

        await _reporter.GenerateReportAsync(new ComparisonResult(), outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheet("Summary");
        Assert.Equal(
            "No differences found - all tables and relationships are in sync.",
            ws.Cell(7, 1).Value.ToString());
    }

    [Fact]
    public async Task GenerateReportAsync_NoDifferences_CleanMessageIsGreen()
    {
        var outputPath = Path.Combine(_tempDirectory, "clean-colour.xlsx");

        await _reporter.GenerateReportAsync(new ComparisonResult(), outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheet("Summary");
        Assert.Equal(XLColor.Green, ws.Cell(7, 1).Style.Font.FontColor);
    }

    #endregion

    #region Summary Sheet - Table Section Tests

    [Fact]
    public async Task GenerateReportAsync_WithTableDifferences_WritesSummaryTableCounts()
    {
        var outputPath = Path.Combine(_tempDirectory, "table-counts.xlsx");
        var result = new ComparisonResult
        {
            TableResults =
            {
                new TableComparisonResult
                {
                    TableName = "account",
                    SourceRecordCount = 10,
                    TargetRecordCount = 9,
                    Differences =
                    {
                        new RecordDifference { RecordId = Guid.NewGuid(), DifferenceType = DifferenceType.New },
                        new RecordDifference { RecordId = Guid.NewGuid(), DifferenceType = DifferenceType.Deleted },
                        new RecordDifference
                        {
                            RecordId = Guid.NewGuid(),
                            DifferenceType = DifferenceType.Modified,
                            FieldDifferences = { new FieldDifference { FieldName = "name", SourceValue = "A", TargetValue = "B" } }
                        }
                    }
                }
            }
        };

        await _reporter.GenerateReportAsync(result, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheet("Summary");
        // Row 7: "Table Comparisons", row 8: headers, row 9: first table data
        Assert.Equal("account", ws.Cell(9, 1).Value.ToString());
        Assert.Equal("10",      ws.Cell(9, 2).Value.ToString());
        Assert.Equal("9",       ws.Cell(9, 3).Value.ToString());
        Assert.Equal("1",       ws.Cell(9, 4).Value.ToString()); // NewCount
        Assert.Equal("1",       ws.Cell(9, 5).Value.ToString()); // ModifiedCount
        Assert.Equal("1",       ws.Cell(9, 6).Value.ToString()); // DeletedCount
    }

    [Fact]
    public async Task GenerateReportAsync_TableWithDifferences_StatusCellIsRed()
    {
        var outputPath = Path.Combine(_tempDirectory, "table-status-red.xlsx");

        await _reporter.GenerateReportAsync(CreateResultWithTableDifference(), outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheet("Summary");
        Assert.Equal("Differences Found", ws.Cell(9, 7).Value.ToString());
        Assert.Equal(XLColor.Red, ws.Cell(9, 7).Style.Font.FontColor);
    }

    [Fact]
    public async Task GenerateReportAsync_TableInSync_StatusCellIsGreen()
    {
        var outputPath = Path.Combine(_tempDirectory, "table-status-green.xlsx");
        var result = new ComparisonResult
        {
            TableResults =
            {
                // Sorted alphabetically: account (in sync) comes before contact (differences)
                new TableComparisonResult { TableName = "account", SourceRecordCount = 5, TargetRecordCount = 5 },
                new TableComparisonResult
                {
                    TableName = "contact",
                    Differences = { new RecordDifference { RecordId = Guid.NewGuid(), DifferenceType = DifferenceType.New } }
                }
            }
        };

        await _reporter.GenerateReportAsync(result, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheet("Summary");
        Assert.Equal("In Sync", ws.Cell(9, 7).Value.ToString());
        Assert.Equal(XLColor.Green, ws.Cell(9, 7).Style.Font.FontColor);
    }

    [Fact]
    public async Task GenerateReportAsync_MultipleTablesInSummary_SortedAlphabetically()
    {
        var outputPath = Path.Combine(_tempDirectory, "table-sort.xlsx");
        var result = new ComparisonResult
        {
            TableResults =
            {
                new TableComparisonResult
                {
                    TableName = "Zebra",
                    Differences = { new RecordDifference { RecordId = Guid.NewGuid(), DifferenceType = DifferenceType.New } }
                },
                new TableComparisonResult
                {
                    TableName = "Alpha",
                    Differences = { new RecordDifference { RecordId = Guid.NewGuid(), DifferenceType = DifferenceType.New } }
                }
            }
        };

        await _reporter.GenerateReportAsync(result, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheet("Summary");
        Assert.Equal("Alpha", ws.Cell(9,  1).Value.ToString());
        Assert.Equal("Zebra", ws.Cell(10, 1).Value.ToString());
    }

    [Fact]
    public async Task GenerateReportAsync_TableWithDifferences_NameCellIsHyperlinked()
    {
        var outputPath = Path.Combine(_tempDirectory, "table-hyperlink.xlsx");

        await _reporter.GenerateReportAsync(CreateResultWithTableDifference(), outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheet("Summary");
        var nameCell = ws.Cell(9, 1);
        Assert.Equal(XLColor.Blue, nameCell.Style.Font.FontColor);
        Assert.Equal(XLFontUnderlineValues.Single, nameCell.Style.Font.Underline);
    }

    #endregion

    #region Summary Sheet - Relationship Section Tests

    [Fact]
    public async Task GenerateReportAsync_WithRelationshipResults_CreatesRelationshipSection()
    {
        var outputPath = Path.Combine(_tempDirectory, "rel-section.xlsx");
        var result = new ComparisonResult
        {
            RelationshipResults = { CreateRelationshipResult("account_contact", hasDifferences: true) }
        };

        await _reporter.GenerateReportAsync(result, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheet("Summary");
        // No table results → row 7 → row++ = 8 for "Relationship Comparisons"
        Assert.Equal("Relationship Comparisons", ws.Cell(8, 1).Value.ToString());
    }

    [Fact]
    public async Task GenerateReportAsync_WithRelationshipResults_WritesRelationshipCounts()
    {
        var outputPath = Path.Combine(_tempDirectory, "rel-counts.xlsx");
        var result = new ComparisonResult
        {
            RelationshipResults =
            {
                new RelationshipComparisonResult
                {
                    RelationshipName = "account_contact",
                    SourceAssociationCount = 5,
                    TargetAssociationCount = 3,
                    Differences =
                    {
                        new AssociationDifference { Entity1Id = Guid.NewGuid(), Entity2Id = Guid.NewGuid(), DifferenceType = DifferenceType.New },
                        new AssociationDifference { Entity1Id = Guid.NewGuid(), Entity2Id = Guid.NewGuid(), DifferenceType = DifferenceType.Deleted }
                    }
                }
            }
        };

        await _reporter.GenerateReportAsync(result, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheet("Summary");
        // Row 8: heading, row 9: headers, row 10: first rel data
        Assert.Equal("account_contact", ws.Cell(10, 1).Value.ToString());
        Assert.Equal("5",               ws.Cell(10, 2).Value.ToString()); // SourceAssociationCount
        Assert.Equal("3",               ws.Cell(10, 3).Value.ToString()); // TargetAssociationCount
        Assert.Equal("1",               ws.Cell(10, 4).Value.ToString()); // NewCount
        Assert.Equal("1",               ws.Cell(10, 5).Value.ToString()); // DeletedCount
    }

    [Fact]
    public async Task GenerateReportAsync_RelationshipWithDifferences_StatusIsRed()
    {
        var outputPath = Path.Combine(_tempDirectory, "rel-status-red.xlsx");
        var result = new ComparisonResult
        {
            RelationshipResults = { CreateRelationshipResult("account_contact", hasDifferences: true) }
        };

        await _reporter.GenerateReportAsync(result, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheet("Summary");
        Assert.Equal("Differences Found", ws.Cell(10, 6).Value.ToString());
        Assert.Equal(XLColor.Red, ws.Cell(10, 6).Style.Font.FontColor);
    }

    [Fact]
    public async Task GenerateReportAsync_RelationshipInSync_StatusIsGreen()
    {
        var outputPath = Path.Combine(_tempDirectory, "rel-status-green.xlsx");
        var result = new ComparisonResult
        {
            // Two relationships sorted alphabetically: aaa_bbb (in sync), zzz_yyy (differences)
            RelationshipResults =
            {
                new RelationshipComparisonResult { RelationshipName = "aaa_bbb" },
                CreateRelationshipResult("zzz_yyy", hasDifferences: true)
            }
        };

        await _reporter.GenerateReportAsync(result, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheet("Summary");
        // aaa_bbb at row 10 (in sync), zzz_yyy at row 11 (differences)
        Assert.Equal("In Sync", ws.Cell(10, 6).Value.ToString());
        Assert.Equal(XLColor.Green, ws.Cell(10, 6).Style.Font.FontColor);
    }

    [Fact]
    public async Task GenerateReportAsync_WithBothTablesAndRelationships_CreatesBothSections()
    {
        var outputPath = Path.Combine(_tempDirectory, "both-sections.xlsx");
        var result = new ComparisonResult
        {
            TableResults =
            {
                new TableComparisonResult
                {
                    TableName = "account",
                    Differences = { new RecordDifference { RecordId = Guid.NewGuid(), DifferenceType = DifferenceType.New } }
                }
            },
            RelationshipResults = { CreateRelationshipResult("account_contact", hasDifferences: true) }
        };

        await _reporter.GenerateReportAsync(result, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheet("Summary");
        // Table section at row 7; relationship section at row 11 (1 table row + blank)
        Assert.Equal("Table Comparisons",        ws.Cell(7,  1).Value.ToString());
        Assert.Equal("Relationship Comparisons", ws.Cell(11, 1).Value.ToString());
    }

    #endregion

    #region Table Detail Sheet Tests

    [Fact]
    public async Task GenerateReportAsync_DetailSheet_HasCorrectTitle()
    {
        var outputPath = Path.Combine(_tempDirectory, "detail-title.xlsx");

        await _reporter.GenerateReportAsync(CreateComparisonResultWithDifference(DifferenceType.New), outputPath);

        using var workbook = new XLWorkbook(outputPath);
        Assert.Equal("Test Table - Differences", workbook.Worksheet("Test Table").Cell(1, 1).Value.ToString());
    }

    [Fact]
    public async Task GenerateReportAsync_DetailSheet_HasSummaryCounts()
    {
        var outputPath = Path.Combine(_tempDirectory, "detail-counts.xlsx");
        var result = new ComparisonResult
        {
            TableResults =
            {
                new TableComparisonResult
                {
                    TableName = "account",
                    SourceRecordCount = 10,
                    TargetRecordCount = 8,
                    Differences =
                    {
                        new RecordDifference { RecordId = Guid.NewGuid(), DifferenceType = DifferenceType.New },
                        new RecordDifference { RecordId = Guid.NewGuid(), DifferenceType = DifferenceType.Deleted }
                    }
                }
            }
        };

        await _reporter.GenerateReportAsync(result, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheet("account");
        Assert.Equal("Total Source: 10",             ws.Cell(3, 1).Value.ToString());
        Assert.Equal("Total Target: 8",              ws.Cell(4, 1).Value.ToString());
        Assert.Equal("New: 1, Modified: 0, Deleted: 1", ws.Cell(5, 1).Value.ToString());
    }

    [Fact]
    public async Task GenerateReportAsync_ModifiedRecord_WritesFieldColumns()
    {
        var outputPath = Path.Combine(_tempDirectory, "modified-fields.xlsx");
        var recordId = Guid.NewGuid();
        var result = new ComparisonResult
        {
            TableResults =
            {
                new TableComparisonResult
                {
                    TableName = "account",
                    Differences =
                    {
                        new RecordDifference
                        {
                            RecordId = recordId,
                            RecordName = "Test Account",
                            DifferenceType = DifferenceType.Modified,
                            FieldDifferences =
                            {
                                new FieldDifference { FieldName = "name", SourceValue = "Old Name", TargetValue = "New Name" }
                            }
                        }
                    }
                }
            }
        };

        await _reporter.GenerateReportAsync(result, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheet("account");
        Assert.Equal(recordId.ToString(), ws.Cell(8, 1).Value.ToString());
        Assert.Equal("Test Account",      ws.Cell(8, 2).Value.ToString());
        Assert.Equal("Modified",          ws.Cell(8, 3).Value.ToString());
        Assert.Equal("name",              ws.Cell(8, 4).Value.ToString());
        Assert.Equal("Old Name",          ws.Cell(8, 5).Value.ToString());
        Assert.Equal("New Name",          ws.Cell(8, 6).Value.ToString());
    }

    [Fact]
    public async Task GenerateReportAsync_NullFieldValues_WrittenAsNullText()
    {
        var outputPath = Path.Combine(_tempDirectory, "null-values.xlsx");
        var result = new ComparisonResult
        {
            TableResults =
            {
                new TableComparisonResult
                {
                    TableName = "account",
                    Differences =
                    {
                        new RecordDifference
                        {
                            RecordId = Guid.NewGuid(),
                            DifferenceType = DifferenceType.Modified,
                            FieldDifferences =
                            {
                                new FieldDifference { FieldName = "description", SourceValue = null, TargetValue = null }
                            }
                        }
                    }
                }
            }
        };

        await _reporter.GenerateReportAsync(result, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheet("account");
        Assert.Equal("(null)", ws.Cell(8, 5).Value.ToString());
        Assert.Equal("(null)", ws.Cell(8, 6).Value.ToString());
    }

    [Fact]
    public async Task GenerateReportAsync_ModifiedRecordWithMultipleFields_OneRowPerField()
    {
        var outputPath = Path.Combine(_tempDirectory, "multi-field.xlsx");
        var result = new ComparisonResult
        {
            TableResults =
            {
                new TableComparisonResult
                {
                    TableName = "account",
                    Differences =
                    {
                        new RecordDifference
                        {
                            RecordId = Guid.NewGuid(),
                            DifferenceType = DifferenceType.Modified,
                            FieldDifferences =
                            {
                                new FieldDifference { FieldName = "city", SourceValue = "London", TargetValue = "Paris" },
                                new FieldDifference { FieldName = "name", SourceValue = "Old",    TargetValue = "New"   }
                            }
                        }
                    }
                }
            }
        };

        await _reporter.GenerateReportAsync(result, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheet("account");
        // Field diffs sorted by FieldName: city first, then name
        Assert.Equal("city", ws.Cell(8, 4).Value.ToString());
        Assert.Equal("name", ws.Cell(9, 4).Value.ToString());
    }

    #endregion

    #region Relationship Detail Sheet Tests

    [Fact]
    public async Task GenerateReportAsync_RelationshipWithDifferences_CreatesDetailSheet()
    {
        var outputPath = Path.Combine(_tempDirectory, "rel-detail-sheet.xlsx");
        var result = new ComparisonResult
        {
            RelationshipResults = { CreateRelationshipResult("account_contact", hasDifferences: true) }
        };

        await _reporter.GenerateReportAsync(result, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        Assert.True(workbook.Worksheets.Contains("account_contact"));
    }

    [Fact]
    public async Task GenerateReportAsync_RelationshipWithNoDifferences_NoDetailSheet()
    {
        var outputPath = Path.Combine(_tempDirectory, "rel-no-detail.xlsx");
        var result = new ComparisonResult
        {
            TableResults =
            {
                new TableComparisonResult
                {
                    TableName = "account",
                    Differences = { new RecordDifference { RecordId = Guid.NewGuid(), DifferenceType = DifferenceType.New } }
                }
            },
            RelationshipResults =
            {
                new RelationshipComparisonResult { RelationshipName = "account_contact" } // no differences
            }
        };

        await _reporter.GenerateReportAsync(result, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        Assert.False(workbook.Worksheets.Contains("account_contact"));
    }

    [Fact]
    public async Task GenerateReportAsync_RelationshipDetailSheet_HasCorrectTitle()
    {
        var outputPath = Path.Combine(_tempDirectory, "rel-title.xlsx");
        var result = new ComparisonResult
        {
            RelationshipResults = { CreateRelationshipResult("account_contact", hasDifferences: true) }
        };

        await _reporter.GenerateReportAsync(result, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        Assert.Equal(
            "account_contact - Differences",
            workbook.Worksheet("account_contact").Cell(1, 1).Value.ToString());
    }

    [Fact]
    public async Task GenerateReportAsync_RelationshipDetailSheet_HasMetadata()
    {
        var outputPath = Path.Combine(_tempDirectory, "rel-metadata.xlsx");
        var result = new ComparisonResult
        {
            RelationshipResults =
            {
                new RelationshipComparisonResult
                {
                    RelationshipName = "account_contact",
                    IntersectEntity = "accountcontact",
                    SourceAssociationCount = 8,
                    TargetAssociationCount = 6,
                    Differences =
                    {
                        new AssociationDifference { Entity1Id = Guid.NewGuid(), Entity2Id = Guid.NewGuid(), DifferenceType = DifferenceType.New },
                        new AssociationDifference { Entity1Id = Guid.NewGuid(), Entity2Id = Guid.NewGuid(), DifferenceType = DifferenceType.Deleted }
                    }
                }
            }
        };

        await _reporter.GenerateReportAsync(result, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheet("account_contact");
        Assert.Equal("Intersect Entity: accountcontact", ws.Cell(3, 1).Value.ToString());
        Assert.Equal("Total Source: 8",                  ws.Cell(4, 1).Value.ToString());
        Assert.Equal("Total Target: 6",                  ws.Cell(5, 1).Value.ToString());
        Assert.Equal("New: 1, Deleted: 1",               ws.Cell(6, 1).Value.ToString());
    }

    [Fact]
    public async Task GenerateReportAsync_RelationshipDetailSheet_WritesRowData()
    {
        var outputPath = Path.Combine(_tempDirectory, "rel-row-data.xlsx");
        var entity1Id = Guid.NewGuid();
        var entity2Id = Guid.NewGuid();
        var result = new ComparisonResult
        {
            RelationshipResults =
            {
                new RelationshipComparisonResult
                {
                    RelationshipName = "account_contact",
                    Differences =
                    {
                        new AssociationDifference
                        {
                            Entity1Id   = entity1Id,
                            Entity1Name = "Contoso Ltd",
                            Entity2Id   = entity2Id,
                            Entity2Name = "John Smith",
                            DifferenceType = DifferenceType.New
                        }
                    }
                }
            }
        };

        await _reporter.GenerateReportAsync(result, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheet("account_contact");
        // Row 8: headers, row 9: first data row
        Assert.Equal("Contoso Ltd",        ws.Cell(9, 1).Value.ToString());
        Assert.Equal(entity1Id.ToString(), ws.Cell(9, 2).Value.ToString());
        Assert.Equal("John Smith",         ws.Cell(9, 3).Value.ToString());
        Assert.Equal(entity2Id.ToString(), ws.Cell(9, 4).Value.ToString());
        Assert.Equal("New",                ws.Cell(9, 5).Value.ToString());
    }

    [Fact]
    public async Task GenerateReportAsync_RelationshipDetailSheet_NullEntityNames_FallBackToId()
    {
        var outputPath = Path.Combine(_tempDirectory, "rel-null-names.xlsx");
        var entity1Id = Guid.NewGuid();
        var entity2Id = Guid.NewGuid();
        var result = new ComparisonResult
        {
            RelationshipResults =
            {
                new RelationshipComparisonResult
                {
                    RelationshipName = "account_contact",
                    Differences =
                    {
                        new AssociationDifference
                        {
                            Entity1Id   = entity1Id,
                            Entity1Name = null,
                            Entity2Id   = entity2Id,
                            Entity2Name = null,
                            DifferenceType = DifferenceType.New
                        }
                    }
                }
            }
        };

        await _reporter.GenerateReportAsync(result, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var ws = workbook.Worksheet("account_contact");
        Assert.Equal(entity1Id.ToString(), ws.Cell(9, 1).Value.ToString());
        Assert.Equal(entity2Id.ToString(), ws.Cell(9, 3).Value.ToString());
    }

    #endregion

    #region SanitizeSheetName Tests

    [Fact]
    public async Task GenerateReportAsync_TableNameOver31Chars_SheetNameTruncated()
    {
        var longName = "A_Very_Long_Table_Name_That_Exceeds_Excel_Sheet_Limit";
        var outputPath = Path.Combine(_tempDirectory, "long-name.xlsx");
        var result = new ComparisonResult
        {
            TableResults =
            {
                new TableComparisonResult
                {
                    TableName = longName,
                    Differences = { new RecordDifference { RecordId = Guid.NewGuid(), DifferenceType = DifferenceType.New } }
                }
            }
        };

        await _reporter.GenerateReportAsync(result, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        Assert.True(workbook.Worksheets.Contains(longName.Substring(0, 31)));
    }

    [Fact]
    public async Task GenerateReportAsync_TableNameWithSpecialChars_SheetNameSanitized()
    {
        var outputPath = Path.Combine(_tempDirectory, "special-chars.xlsx");
        var result = new ComparisonResult
        {
            TableResults =
            {
                new TableComparisonResult
                {
                    TableName = "Table[With]Special/Chars",
                    Differences = { new RecordDifference { RecordId = Guid.NewGuid(), DifferenceType = DifferenceType.New } }
                }
            }
        };

        await _reporter.GenerateReportAsync(result, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        Assert.True(workbook.Worksheets.Contains("Table_With_Special_Chars"));
    }

    #endregion

    #region File Writer Tests

    [Fact]
    public async Task GenerateReportAsync_CallsFileWriterWithCorrectPath()
    {
        var outputPath = Path.Combine(_tempDirectory, "path-check.xlsx");

        await _reporter.GenerateReportAsync(new ComparisonResult(), outputPath);

        _mockFileWriter.Verify(x => x.WriteBytesAsync(outputPath, It.IsAny<byte[]>()), Times.Once);
    }

    #endregion
}
