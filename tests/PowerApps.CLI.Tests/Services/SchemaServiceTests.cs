using Moq;
using PowerApps.CLI.Infrastructure;
using PowerApps.CLI.Models;
using PowerApps.CLI.Services;
using Xunit;

namespace PowerApps.CLI.Tests.Services;

/// <summary>
/// Tests for SchemaService guard clauses and parameter validation.
/// Note: Connection and logging tests are skipped as they require integration testing with a real ServiceClient.
/// </summary>
public class SchemaServiceTests
{
    private readonly Mock<IConsoleLogger> _mockLogger;
    private readonly Mock<ISchemaExporter> _mockSchemaExporter;
    private readonly Mock<IDataverseClient> _mockDataverseClient;
    private readonly Mock<ISchemaExtractor> _mockSchemaExtractor;
    private readonly SchemaService _service;

    public SchemaServiceTests()
    {
        _mockLogger = new Mock<IConsoleLogger>();
        _mockSchemaExporter = new Mock<ISchemaExporter>();
        _mockDataverseClient = new Mock<IDataverseClient>();
        _mockSchemaExtractor = new Mock<ISchemaExtractor>();
        
        _service = new SchemaService(
            _mockLogger.Object,
            _mockSchemaExporter.Object,
            _mockDataverseClient.Object,
            _mockSchemaExtractor.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Arrange
        var mockSchemaExporter = new Mock<ISchemaExporter>();
        var mockDataverseClient = new Mock<IDataverseClient>();
        var mockSchemaExtractor = new Mock<ISchemaExtractor>();
        
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => 
            new SchemaService(null!, mockSchemaExporter.Object, mockDataverseClient.Object, mockSchemaExtractor.Object));
        
        Assert.Equal("logger", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullSchemaExporter_ShouldThrowArgumentNullException()
    {
        // Arrange
        var mockLogger = new Mock<IConsoleLogger>();
        var mockDataverseClient = new Mock<IDataverseClient>();
        var mockSchemaExtractor = new Mock<ISchemaExtractor>();
        
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => 
            new SchemaService(mockLogger.Object, null!, mockDataverseClient.Object, mockSchemaExtractor.Object));
        
        Assert.Equal("schemaExporter", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithValidDependencies_ShouldSucceed()
    {
        // Arrange
        var mockLogger = new Mock<IConsoleLogger>();
        var mockSchemaExporter = new Mock<ISchemaExporter>();
        var mockDataverseClient = new Mock<IDataverseClient>();
        var mockSchemaExtractor = new Mock<ISchemaExtractor>();
        
        // Act
        var service = new SchemaService(
            mockLogger.Object,
            mockSchemaExporter.Object,
            mockDataverseClient.Object,
            mockSchemaExtractor.Object);

        // Assert
        Assert.NotNull(service);
    }

    #endregion

    #region OutputPath Guard Clause Tests

    [Fact]
    public async Task ExportSchemaAsync_WithNullOutputPath_ShouldThrowArgumentExceptionAsync()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ExportSchemaAsync(null!, "json"));

        Assert.Contains("Output path cannot be null or whitespace", exception.Message);
        Assert.Equal("outputPath", exception.ParamName);
    }

    [Fact]
    public async Task ExportSchemaAsync_WithEmptyOutputPath_ShouldThrowArgumentExceptionAsync()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ExportSchemaAsync("", "json"));

        Assert.Contains("Output path cannot be null or whitespace", exception.Message);
        Assert.Equal("outputPath", exception.ParamName);
    }

    [Fact]
    public async Task ExportSchemaAsync_WithWhitespaceOutputPath_ShouldThrowArgumentExceptionAsync()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ExportSchemaAsync("   ", "json"));

        Assert.Contains("Output path cannot be null or whitespace", exception.Message);
        Assert.Equal("outputPath", exception.ParamName);
    }

    #endregion

    #region Format Validation Tests

    [Theory]
    [InlineData("invalid")]
    [InlineData("txt")]
    [InlineData("xml")]
    [InlineData("JSON1")]
    [InlineData("pdf")]
    public async Task ExportSchemaAsync_WithInvalidFormat_ShouldThrowArgumentExceptionAsync(string invalidFormat)
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ExportSchemaAsync("output.file", invalidFormat));

        Assert.Contains("Invalid format", exception.Message);
        Assert.Contains("Supported formats", exception.Message);
        Assert.Equal("format", exception.ParamName);
    }

    [Theory]
    [InlineData("json")]
    [InlineData("JSON")]
    [InlineData("Json")]
    [InlineData("xlsx")]
    [InlineData("XLSX")]
    public async Task ExportSchemaAsync_WithValidFormat_ShouldNotThrowFormatExceptionAsync(string format)
    {
        // Note: This test verifies that format validation passes for valid formats
        // The method may still fail due to connection issues (ServiceClient being null in mocks)
        // but it should NOT throw an ArgumentException about invalid format
        
        // Act
        try
        {
            await _service.ExportSchemaAsync("output.file", format);
            // If we reach here, format was accepted (success)
            Assert.True(true);
        }
        catch (ArgumentException ex) when (ex.ParamName == "format")
        {
            // Format validation failed - this is what we're testing against
            Assert.Fail($"Format '{format}' should be valid but was rejected: {ex.Message}");
        }
        catch
        {
            // Other exceptions (connection errors, etc.) are fine - we only care about format validation
            Assert.True(true);
        }
    }

    #endregion

    #region Attribute Filter Tests

    [Fact]
    public async Task ExportSchemaAsync_WithAttributePrefix_FiltersToPrefixedAttributesAsync()
    {
        // Arrange
        var exported = SetupExtractAndCaptureExport(
            "xrt_name", "xrt_custom", "createdon", "ownerid");

        // Act
        await _service.ExportSchemaAsync("output.json", "json", attributePrefix: "xrt_");

        // Assert
        var names = exported().Entities[0].Attributes.Select(a => a.LogicalName);
        Assert.Equal(new[] { "xrt_name", "xrt_custom" }, names);
    }

    [Fact]
    public async Task ExportSchemaAsync_WithAttributePrefix_IsCaseInsensitiveAsync()
    {
        // Arrange
        var exported = SetupExtractAndCaptureExport("XRT_Name", "createdon");

        // Act
        await _service.ExportSchemaAsync("output.json", "json", attributePrefix: "xrt_");

        // Assert
        var names = exported().Entities[0].Attributes.Select(a => a.LogicalName);
        Assert.Equal(new[] { "XRT_Name" }, names);
    }

    [Fact]
    public async Task ExportSchemaAsync_WithExcludeAttributes_RemovesExcludedAttributesAsync()
    {
        // Arrange
        var exported = SetupExtractAndCaptureExport(
            "xrt_name", "createdon", "modifiedon", "ownerid");

        // Act
        await _service.ExportSchemaAsync("output.json", "json", excludeAttributes: "createdon, MODIFIEDON");

        // Assert — case-insensitive, whitespace-trimmed
        var names = exported().Entities[0].Attributes.Select(a => a.LogicalName);
        Assert.Equal(new[] { "xrt_name", "ownerid" }, names);
    }

    [Fact]
    public async Task ExportSchemaAsync_WithBothFilters_AppliesExcludeAndPrefixAsync()
    {
        // Arrange
        var exported = SetupExtractAndCaptureExport(
            "xrt_name", "xrt_secret", "createdon");

        // Act — prefix keeps xrt_*, exclude drops xrt_secret
        await _service.ExportSchemaAsync(
            "output.json", "json", attributePrefix: "xrt_", excludeAttributes: "xrt_secret");

        // Assert
        var names = exported().Entities[0].Attributes.Select(a => a.LogicalName);
        Assert.Equal(new[] { "xrt_name" }, names);
    }

    [Fact]
    public async Task ExportSchemaAsync_WithNoFilters_KeepsAllAttributesAsync()
    {
        // Arrange
        var exported = SetupExtractAndCaptureExport("xrt_name", "createdon", "ownerid");

        // Act
        await _service.ExportSchemaAsync("output.json", "json");

        // Assert
        var names = exported().Entities[0].Attributes.Select(a => a.LogicalName);
        Assert.Equal(new[] { "xrt_name", "createdon", "ownerid" }, names);
    }

    /// <summary>
    /// Wires the extractor to return a single-entity schema with the given attribute logical names
    /// and captures the schema handed to the exporter. Returns an accessor for that captured schema.
    /// </summary>
    private Func<PowerAppsSchema> SetupExtractAndCaptureExport(params string[] attributeLogicalNames)
    {
        var schema = new PowerAppsSchema
        {
            Entities = new List<EntitySchema>
            {
                new()
                {
                    LogicalName = "xrt_integrationtest",
                    Attributes = attributeLogicalNames
                        .Select(n => new AttributeSchema { LogicalName = n })
                        .ToList()
                }
            }
        };

        _mockSchemaExtractor
            .Setup(e => e.ExtractSchemaAsync(It.IsAny<string?>()))
            .ReturnsAsync(schema);

        PowerAppsSchema? captured = null;
        _mockSchemaExporter
            .Setup(e => e.ExportAsync(It.IsAny<PowerAppsSchema>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<PowerAppsSchema, string, string>((s, _, _) => captured = s)
            .Returns(Task.CompletedTask);

        return () => captured ?? throw new InvalidOperationException("Export was not invoked.");
    }

    #endregion
}
