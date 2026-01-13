using System.Text.Json;
using ClosedXML.Excel;
using Moq;
using PowerApps.CLI.Infrastructure;
using PowerApps.CLI.Models;
using PowerApps.CLI.Services;
using Xunit;

namespace PowerApps.CLI.Tests.Services;

public class SchemaExporterTests : IDisposable
{
    private readonly Mock<IFileWriter> _mockFileWriter;
    private readonly SchemaExporter _exporter;
    private readonly string _tempDirectory;

    public SchemaExporterTests()
    {
        _mockFileWriter = new Mock<IFileWriter>();
        _exporter = new SchemaExporter(_mockFileWriter.Object);
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"PowerAppsCLI_Tests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDirectory);
        
        // Setup file writer to actually write files for verification
        _mockFileWriter
            .Setup(x => x.WriteTextAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string path, string content) => File.WriteAllTextAsync(path, content));
        
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

    #region Null Validation Tests

    [Fact]
    public async Task ExportAsync_WithNullSchema_ShouldThrowArgumentNullExceptionAsync()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _exporter.ExportAsync(null!, "output.json", "json"));
    }

    [Fact]
    public async Task ExportAsync_WithNullOutputPath_ShouldThrowArgumentExceptionAsync()
    {
        // Arrange
        var schema = new PowerAppsSchema();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _exporter.ExportAsync(schema, null!, "json"));
    }

    [Fact]
    public async Task ExportAsync_WithInvalidFormat_ShouldThrowArgumentExceptionAsync()
    {
        // Arrange
        var schema = new PowerAppsSchema();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _exporter.ExportAsync(schema, "output.txt", "invalid"));
    }

    #endregion

    #region JSON Export Tests

    [Fact]
    public async Task ExportAsync_ToJson_ShouldCreateValidJsonFileAsync()
    {
        // Arrange
        var schema = CreateTestSchema();
        var outputPath = Path.Combine(_tempDirectory, "test-schema.json");

        // Act
        await _exporter.ExportAsync(schema, outputPath, "json");

        // Assert
        Assert.True(File.Exists(outputPath));
        var json = await File.ReadAllTextAsync(outputPath);
        var deserializedSchema = JsonSerializer.Deserialize<PowerAppsSchema>(json);
        Assert.NotNull(deserializedSchema);
        Assert.Equal(schema.SolutionNames?.Count, deserializedSchema.SolutionNames?.Count);
        Assert.Equal(schema.Entities?.Count, deserializedSchema.Entities?.Count);
    }

    [Fact]
    public async Task ExportAsync_ToJson_ShouldBeIndentedAsync()
    {
        // Arrange
        var schema = CreateTestSchema();
        var outputPath = Path.Combine(_tempDirectory, "test-schema-indented.json");

        // Act
        await _exporter.ExportAsync(schema, outputPath, "json");

        // Assert
        var json = await File.ReadAllTextAsync(outputPath);
        Assert.Contains(Environment.NewLine, json); // Should have line breaks (indented)
        Assert.Contains("  ", json); // Should have indentation
    }

    #endregion

    #region XLSX Export Tests

    [Fact]
    public async Task ExportAsync_ToXlsx_ShouldCreateValidExcelFileAsync()
    {
        // Arrange
        var schema = CreateTestSchema();
        var outputPath = Path.Combine(_tempDirectory, "test-schema.xlsx");

        // Act
        await _exporter.ExportAsync(schema, outputPath, "xlsx");

        // Assert
        Assert.True(File.Exists(outputPath));
        
        using var workbook = new XLWorkbook(outputPath);
        Assert.True(workbook.Worksheets.Contains("Summary"));
        Assert.True(workbook.Worksheets.Contains("Attributes"));
        Assert.True(workbook.Worksheets.Contains("Relationships"));
    }

    [Fact]
    public async Task ExportAsync_ToXlsx_SummarySheet_ShouldContainSolutionColumnAsync()
    {
        // Arrange
        var schema = CreateTestSchema();
        var outputPath = Path.Combine(_tempDirectory, "test-schema-solutions.xlsx");

        // Act
        await _exporter.ExportAsync(schema, outputPath, "xlsx");

        // Assert
        using var workbook = new XLWorkbook(outputPath);
        var summarySheet = workbook.Worksheet("Summary");
        
        Assert.NotNull(summarySheet);
        
        // Find the header row (after the metadata section)
        var headerRow = 0;
        for (int row = 1; row <= 20; row++)
        {
            if (summarySheet.Cell(row, 1).GetString() == "Logical Name")
            {
                headerRow = row;
                break;
            }
        }
        
        Assert.True(headerRow > 0, "Could not find header row in Summary sheet");
        
        // Check for Solutions column header (column 7)
        Assert.Equal("Solutions", summarySheet.Cell(headerRow, 7).GetString());
        
        // Check that solution data is populated (first data row after header)
        var entity1Solutions = summarySheet.Cell(headerRow + 1, 7).GetString();
        Assert.Contains("TestSolution1", entity1Solutions);
    }

    [Fact]
    public async Task ExportAsync_ToXlsx_AttributesSheet_ShouldContainAllAttributesAsync()
    {
        // Arrange
        var schema = CreateTestSchema();
        var outputPath = Path.Combine(_tempDirectory, "test-schema-attributes.xlsx");

        // Act
        await _exporter.ExportAsync(schema, outputPath, "xlsx");

        // Assert
        using var workbook = new XLWorkbook(outputPath);
        var attributesSheet = workbook.Worksheet("Attributes");
        
        Assert.NotNull(attributesSheet);
        
        // Should have header row plus at least 2 data rows (2 attributes from test entity)
        var lastRow = attributesSheet.LastRowUsed();
        Assert.NotNull(lastRow);
        Assert.True(lastRow.RowNumber() >= 3);
    }

    #endregion

    #region Helper Methods

    private static PowerAppsSchema CreateTestSchema()
    {
        return new PowerAppsSchema
        {
            SolutionNames = new List<string> { "TestSolution1", "TestSolution2" },
            Entities = new List<EntitySchema>
            {
                new()
                {
                    LogicalName = "account",
                    DisplayName = "Account",
                    SchemaName = "Account",
                    Description = "Test account entity",
                    PrimaryIdAttribute = "accountid",
                    PrimaryNameAttribute = "name",
                    IsCustomEntity = false,
                    FoundInSolutions = new List<string> { "TestSolution1", "TestSolution2" },
                    Attributes = new List<AttributeSchema>
                    {
                        new()
                        {
                            LogicalName = "accountid",
                            DisplayName = "Account",
                            SchemaName = "AccountId",
                            AttributeType = "Uniqueidentifier",
                            IsPrimaryId = true,
                            IsCustomAttribute = false
                        },
                        new()
                        {
                            LogicalName = "name",
                            DisplayName = "Account Name",
                            SchemaName = "Name",
                            AttributeType = "String",
                            MaxLength = 100,
                            IsPrimaryName = true,
                            IsCustomAttribute = false
                        }
                    }
                },
                new()
                {
                    LogicalName = "contact",
                    DisplayName = "Contact",
                    SchemaName = "Contact",
                    PrimaryIdAttribute = "contactid",
                    PrimaryNameAttribute = "fullname",
                    IsCustomEntity = false,
                    FoundInSolutions = new List<string> { "TestSolution1" },
                    Attributes = new List<AttributeSchema>
                    {
                        new()
                        {
                            LogicalName = "contactid",
                            AttributeType = "Uniqueidentifier",
                            IsPrimaryId = true
                        }
                    }
                }
            },
            Relationships = new List<RelationshipSchema>
            {
                new()
                {
                    SchemaName = "account_contact",
                    ReferencedEntity = "account",
                    ReferencedAttribute = "accountid",
                    ReferencingEntity = "contact",
                    ReferencingAttribute = "parentcustomerid",
                    RelationshipType = "OneToManyRelationship",
                    IsCustomRelationship = false
                }
            }
        };
    }

    #endregion
}
