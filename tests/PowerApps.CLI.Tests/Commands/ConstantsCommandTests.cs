using Moq;
using PowerApps.CLI.Commands;
using PowerApps.CLI.Infrastructure;
using PowerApps.CLI.Models;
using PowerApps.CLI.Services;
using Xunit;

namespace PowerApps.CLI.Tests.Commands;

public class ConstantsCommandTests
{
    private readonly Mock<IConsoleLogger> _mockLogger;
    private readonly Mock<ISchemaExtractor> _mockSchemaExtractor;
    private readonly Mock<IConstantsFilter> _mockFilter;
    private readonly Mock<IConstantsGenerator> _mockGenerator;
    private readonly ConstantsCommand _command;

    public ConstantsCommandTests()
    {
        _mockLogger = new Mock<IConsoleLogger>();
        _mockSchemaExtractor = new Mock<ISchemaExtractor>();
        _mockFilter = new Mock<IConstantsFilter>();
        _mockGenerator = new Mock<IConstantsGenerator>();

        _command = new ConstantsCommand(
            _mockLogger.Object,
            _mockSchemaExtractor.Object,
            _mockFilter.Object,
            _mockGenerator.Object);
    }

    [Fact]
    public async Task ExecuteAsync_WithNoUrlOrConnectionString_Returns1()
    {
        var result = await _command.ExecuteAsync(
            null, null, null, "./output", "MyNamespace", false,
            null, true, true, null, null, null, true);

        Assert.Equal(1, result);
        _mockLogger.Verify(l => l.LogError(It.Is<string>(s => s.Contains("--url or --connection-string"))), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_CallsExtractSchemaWithSolution()
    {
        // Arrange
        var schema = new PowerAppsSchema { Entities = new List<EntitySchema>() };
        _mockSchemaExtractor.Setup(e => e.ExtractSchemaAsync("MySolution"))
            .ReturnsAsync(schema);

        // Act
        var result = await _command.ExecuteAsync(
            null, "https://test.crm.dynamics.com", "MySolution", "./output", "MyNamespace", false,
            null, true, true, null, null, null, true);

        // Assert
        Assert.Equal(0, result);
        _mockSchemaExtractor.Verify(e => e.ExtractSchemaAsync("MySolution"), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_CallsGenerateAsyncWithCorrectOutputConfig()
    {
        // Arrange
        var entities = new List<EntitySchema> { new() { LogicalName = "account" } };
        var schema = new PowerAppsSchema { Entities = entities };
        _mockSchemaExtractor.Setup(e => e.ExtractSchemaAsync(It.IsAny<string?>()))
            .ReturnsAsync(schema);

        // Act
        await _command.ExecuteAsync(
            null, "https://test.crm.dynamics.com", null, "./Generated", "My.Namespace", false,
            null, true, true, null, null, null, true);

        // Assert
        _mockGenerator.Verify(g => g.GenerateAsync(
            entities,
            It.Is<ConstantsOutputConfig>(c =>
                c.OutputPath == "./Generated" &&
                c.Namespace == "My.Namespace" &&
                c.PascalCaseConversion == true),
            _mockLogger.Object), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithConfigFile_UsesConfigForFiltering()
    {
        // Arrange
        var config = new ConstantsConfig
        {
            ExcludeEntities = new List<string> { "systemuser" },
            ExcludeAttributes = new List<string>(),
            PascalCaseConversion = true,
            SingleFile = false,
            IncludeEntities = true,
            IncludeGlobalOptionSets = true
        };

        var entities = new List<EntitySchema>
        {
            new() { LogicalName = "account" },
            new() { LogicalName = "systemuser" }
        };
        var schema = new PowerAppsSchema { Entities = entities };
        _mockSchemaExtractor.Setup(e => e.ExtractSchemaAsync(It.IsAny<string?>()))
            .ReturnsAsync(schema);
        _mockFilter.Setup(f => f.FilterEntities(entities, config))
            .Returns(new List<EntitySchema> { entities[0] });

        // Act
        await _command.ExecuteAsync(
            config, "https://test.crm.dynamics.com", null, "./output", "NS", false,
            null, true, true, null, null, null, true);

        // Assert
        _mockFilter.Verify(f => f.FilterEntities(entities, config), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithExcludeAttributes_FiltersAttributes()
    {
        // Arrange
        var entity = new EntitySchema { LogicalName = "account" };
        var schema = new PowerAppsSchema { Entities = new List<EntitySchema> { entity } };
        _mockSchemaExtractor.Setup(e => e.ExtractSchemaAsync(It.IsAny<string?>()))
            .ReturnsAsync(schema);
        _mockFilter.Setup(f => f.FilterAttributes(entity, It.IsAny<ConstantsConfig>()))
            .Returns(entity);

        // Act - pass attribute prefix to trigger attribute filtering
        await _command.ExecuteAsync(
            null, "https://test.crm.dynamics.com", null, "./output", "NS", false,
            null, true, true, null, null, "rob_", true);

        // Assert
        _mockFilter.Verify(f => f.FilterAttributes(entity, It.IsAny<ConstantsConfig>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithCliExcludeOptions_BuildsConfigCorrectly()
    {
        // Arrange
        var entity = new EntitySchema { LogicalName = "account" };
        var schema = new PowerAppsSchema { Entities = new List<EntitySchema> { entity } };
        _mockSchemaExtractor.Setup(e => e.ExtractSchemaAsync(It.IsAny<string?>()))
            .ReturnsAsync(schema);
        _mockFilter.Setup(f => f.FilterEntities(It.IsAny<List<EntitySchema>>(), It.IsAny<ConstantsConfig>()))
            .Returns(new List<EntitySchema> { entity });

        // Act - pass comma-separated exclude entities
        await _command.ExecuteAsync(
            null, "https://test.crm.dynamics.com", null, "./output", "NS", false,
            null, true, true, "systemuser,team", "modifiedby,createdon", null, true);

        // Assert - filter should be called with the parsed exclusion list
        _mockFilter.Verify(f => f.FilterEntities(
            It.IsAny<List<EntitySchema>>(),
            It.Is<ConstantsConfig>(c =>
                c.ExcludeEntities.Contains("systemuser") &&
                c.ExcludeEntities.Contains("team"))), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithUrl_LogsUrl()
    {
        // Arrange
        var schema = new PowerAppsSchema { Entities = new List<EntitySchema>() };
        _mockSchemaExtractor.Setup(e => e.ExtractSchemaAsync(It.IsAny<string?>()))
            .ReturnsAsync(schema);

        // Act
        await _command.ExecuteAsync(
            null, "https://myorg.crm.dynamics.com", null, "./output", "NS", false,
            null, true, true, null, null, null, true);

        // Assert
        _mockLogger.Verify(l => l.LogVerbose(It.Is<string>(s => s.Contains("https://myorg.crm.dynamics.com"))), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenServiceThrows_Returns1()
    {
        // Arrange
        _mockSchemaExtractor.Setup(e => e.ExtractSchemaAsync(It.IsAny<string?>()))
            .ThrowsAsync(new Exception("Connection timeout"));

        // Act
        var result = await _command.ExecuteAsync(
            null, "https://test.crm.dynamics.com", null, "./output", "NS", false,
            null, true, true, null, null, null, true);

        // Assert
        Assert.Equal(1, result);
    }

    [Fact]
    public void CreateCliCommand_ReturnsValidCommand()
    {
        var command = ConstantsCommand.CreateCliCommand();

        Assert.NotNull(command);
        Assert.Equal("constants-generate", command.Name);
    }
}
