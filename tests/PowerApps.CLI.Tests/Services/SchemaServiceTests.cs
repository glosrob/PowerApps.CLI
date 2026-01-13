using Moq;
using PowerApps.CLI.Infrastructure;
using PowerApps.CLI.Services;
using Xunit;

namespace PowerApps.CLI.Tests.Services;

/// <summary>
/// Tests for SchemaService guard clauses and parameter validation.
/// Note: Connection and logging tests are skipped as they require integration testing with a real ServiceClient.
/// </summary>
public class SchemaServiceTests
{
    private readonly Mock<IDataverseClient> _mockDataverseClient;
    private readonly Mock<IConsoleLogger> _mockLogger;
    private readonly Mock<ISchemaExtractor> _mockSchemaExtractor;
    private readonly Mock<ISchemaExporter> _mockSchemaExporter;
    private readonly SchemaService _service;

    public SchemaServiceTests()
    {
        _mockDataverseClient = new Mock<IDataverseClient>();
        _mockLogger = new Mock<IConsoleLogger>();
        _mockSchemaExtractor = new Mock<ISchemaExtractor>();
        _mockSchemaExporter = new Mock<ISchemaExporter>();
        _service = new SchemaService(
            _mockDataverseClient.Object,
            _mockLogger.Object,
            _mockSchemaExtractor.Object,
            _mockSchemaExporter.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullDataverseClient_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => 
            new SchemaService(null!, _mockLogger.Object, _mockSchemaExtractor.Object, _mockSchemaExporter.Object));
        
        Assert.Equal("dataverseClient", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => 
            new SchemaService(_mockDataverseClient.Object, null!, _mockSchemaExtractor.Object, _mockSchemaExporter.Object));
        
        Assert.Equal("logger", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullSchemaExtractor_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => 
            new SchemaService(_mockDataverseClient.Object, _mockLogger.Object, null!, _mockSchemaExporter.Object));
        
        Assert.Equal("schemaExtractor", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullSchemaExporter_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => 
            new SchemaService(_mockDataverseClient.Object, _mockLogger.Object, _mockSchemaExtractor.Object, null!));
        
        Assert.Equal("schemaExporter", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithValidDependencies_ShouldSucceed()
    {
        // Act
        var service = new SchemaService(
            _mockDataverseClient.Object,
            _mockLogger.Object,
            _mockSchemaExtractor.Object,
            _mockSchemaExporter.Object);

        // Assert
        Assert.NotNull(service);
    }

    #endregion

    #region URL/ConnectionString Guard Clause Tests

    [Fact]
    public async Task ExportSchemaAsync_WithNoUrlAndNoConnectionString_ShouldThrowArgumentExceptionAsync()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ExportSchemaAsync(null, "output.json", "json"));

        Assert.Contains("Either URL or connection string must be provided", exception.Message);
    }

    [Fact]
    public async Task ExportSchemaAsync_WithEmptyUrlAndEmptyConnectionString_ShouldThrowArgumentExceptionAsync()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ExportSchemaAsync("", "output.json", "json", connectionString: ""));

        Assert.Contains("Either URL or connection string must be provided", exception.Message);
    }

    [Fact]
    public async Task ExportSchemaAsync_WithWhitespaceUrlAndWhitespaceConnectionString_ShouldThrowArgumentExceptionAsync()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ExportSchemaAsync("   ", "output.json", "json", connectionString: "   "));

        Assert.Contains("Either URL or connection string must be provided", exception.Message);
    }

    #endregion

    #region OutputPath Guard Clause Tests

    [Fact]
    public async Task ExportSchemaAsync_WithNullOutputPath_ShouldThrowArgumentExceptionAsync()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ExportSchemaAsync("https://test.crm.dynamics.com", null!, "json"));

        Assert.Contains("Output path cannot be null or whitespace", exception.Message);
        Assert.Equal("outputPath", exception.ParamName);
    }

    [Fact]
    public async Task ExportSchemaAsync_WithEmptyOutputPath_ShouldThrowArgumentExceptionAsync()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ExportSchemaAsync("https://test.crm.dynamics.com", "", "json"));

        Assert.Contains("Output path cannot be null or whitespace", exception.Message);
        Assert.Equal("outputPath", exception.ParamName);
    }

    [Fact]
    public async Task ExportSchemaAsync_WithWhitespaceOutputPath_ShouldThrowArgumentExceptionAsync()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ExportSchemaAsync("https://test.crm.dynamics.com", "   ", "json"));

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
            _service.ExportSchemaAsync("https://test.crm.dynamics.com", "output.file", invalidFormat));

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
            await _service.ExportSchemaAsync("https://test.crm.dynamics.com", "output.file", format);
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
}
