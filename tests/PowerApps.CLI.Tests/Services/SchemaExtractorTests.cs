using Microsoft.Xrm.Sdk.Metadata;
using PowerApps.CLI.Infrastructure;
using PowerApps.CLI.Models;
using PowerApps.CLI.Services;
using Xunit;

namespace PowerApps.CLI.Tests.Services;

public class SchemaExtractorTests
{
    private readonly Moq.Mock<IMetadataMapper> _mockMapper;
    private readonly Moq.Mock<IDataverseClient> _mockClient;
    private readonly SchemaExtractor _extractor;

    public SchemaExtractorTests()
    {
        _mockMapper = new Moq.Mock<IMetadataMapper>();
        _mockClient = new Moq.Mock<IDataverseClient>();
        _extractor  = new SchemaExtractor(_mockMapper.Object, _mockClient.Object);

        _mockClient.Setup(c => c.GetEnvironmentUrl()).Returns("https://test.crm.dynamics.com");
        _mockClient.Setup(c => c.GetOrganizationName()).Returns("TestOrg");
    }

    #region ExtractSchemaAsync — no solution filter

    [Fact]
    public async Task ExtractSchemaAsync_WithNoSolution_CallsGetAllEntityMetadata()
    {
        _mockClient.Setup(c => c.GetAllEntityMetadataAsync())
            .ReturnsAsync(new Dictionary<string, List<string>>());

        await _extractor.ExtractSchemaAsync();

        _mockClient.Verify(c => c.GetAllEntityMetadataAsync(), Moq.Times.Once);
    }

    [Fact]
    public async Task ExtractSchemaAsync_WithNoSolution_PopulatesEnvironmentUrl()
    {
        _mockClient.Setup(c => c.GetAllEntityMetadataAsync())
            .ReturnsAsync(new Dictionary<string, List<string>>());

        var schema = await _extractor.ExtractSchemaAsync();

        Assert.Equal("https://test.crm.dynamics.com", schema.EnvironmentUrl);
    }

    [Fact]
    public async Task ExtractSchemaAsync_MapsEachEntityReturnedByClient()
    {
        var entityMetadata = new EntityMetadata { LogicalName = "account" };
        _mockClient.Setup(c => c.GetAllEntityMetadataAsync())
            .ReturnsAsync(new Dictionary<string, List<string>> { { "account", new List<string>() } });
        _mockClient.Setup(c => c.GetEntityMetadataAsync("account", EntityFilters.Entity | EntityFilters.Attributes))
            .ReturnsAsync(entityMetadata);
        _mockMapper.Setup(m => m.MapEntity(entityMetadata))
            .Returns(new EntitySchema { LogicalName = "account" });

        var schema = await _extractor.ExtractSchemaAsync();

        Assert.Single(schema.Entities);
        Assert.Equal("account", schema.Entities[0].LogicalName);
    }

    [Fact]
    public async Task ExtractSchemaAsync_SkipsEntityWhenMetadataReturnsNull()
    {
        _mockClient.Setup(c => c.GetAllEntityMetadataAsync())
            .ReturnsAsync(new Dictionary<string, List<string>> { { "account", new List<string>() } });
        _mockClient.Setup(c => c.GetEntityMetadataAsync("account", Moq.It.IsAny<EntityFilters>()))
            .ReturnsAsync((EntityMetadata?)null);

        var schema = await _extractor.ExtractSchemaAsync();

        Assert.Empty(schema.Entities);
    }

    #endregion

    #region ExtractSchemaAsync — with solution filter

    [Fact]
    public async Task ExtractSchemaAsync_WithSolution_CallsGetEntitiesFromSolution()
    {
        _mockClient.Setup(c => c.GetEntitiesFromSolutionAsync("MySolution"))
            .ReturnsAsync(new Dictionary<string, List<string>>());

        await _extractor.ExtractSchemaAsync("MySolution");

        _mockClient.Verify(c => c.GetEntitiesFromSolutionAsync("MySolution"), Moq.Times.Once);
        _mockClient.Verify(c => c.GetAllEntityMetadataAsync(), Moq.Times.Never);
    }

    [Fact]
    public async Task ExtractSchemaAsync_WithMultipleSolutions_MergesEntitiesAcrossSolutions()
    {
        _mockClient.Setup(c => c.GetEntitiesFromSolutionAsync("Sol1"))
            .ReturnsAsync(new Dictionary<string, List<string>> { { "account", new List<string> { "Sol1" } } });
        _mockClient.Setup(c => c.GetEntitiesFromSolutionAsync("Sol2"))
            .ReturnsAsync(new Dictionary<string, List<string>> { { "contact", new List<string> { "Sol2" } } });

        var accountMetadata = new EntityMetadata { LogicalName = "account" };
        var contactMetadata = new EntityMetadata { LogicalName = "contact" };

        _mockClient.Setup(c => c.GetEntityMetadataAsync("account", EntityFilters.Entity | EntityFilters.Attributes))
            .ReturnsAsync(accountMetadata);
        _mockClient.Setup(c => c.GetEntityMetadataAsync("contact", EntityFilters.Entity | EntityFilters.Attributes))
            .ReturnsAsync(contactMetadata);
        _mockClient.Setup(c => c.GetEntityMetadataAsync(Moq.It.IsAny<string>(), EntityFilters.Relationships))
            .ReturnsAsync((EntityMetadata?)null);

        _mockMapper.Setup(m => m.MapEntity(accountMetadata)).Returns(new EntitySchema { LogicalName = "account" });
        _mockMapper.Setup(m => m.MapEntity(contactMetadata)).Returns(new EntitySchema { LogicalName = "contact" });

        var schema = await _extractor.ExtractSchemaAsync("Sol1,Sol2");

        Assert.Equal(2, schema.Entities.Count);
    }

    #endregion

    #region RetrieveRelationshipsAsync

    [Fact]
    public async Task ExtractSchemaAsync_MapsOneToManyRelationships()
    {
        var rel = new OneToManyRelationshipMetadata { SchemaName = "account_contact" };
        var entityMetadata = new EntityMetadata { LogicalName = "account" };
        SetPrivateProperty(entityMetadata, "OneToManyRelationships", new[] { rel });

        _mockClient.Setup(c => c.GetAllEntityMetadataAsync())
            .ReturnsAsync(new Dictionary<string, List<string>> { { "account", new List<string>() } });
        _mockClient.Setup(c => c.GetEntityMetadataAsync("account", EntityFilters.Entity | EntityFilters.Attributes))
            .ReturnsAsync(new EntityMetadata { LogicalName = "account" });
        _mockClient.Setup(c => c.GetEntityMetadataAsync("account", EntityFilters.Relationships))
            .ReturnsAsync(entityMetadata);
        _mockMapper.Setup(m => m.MapEntity(Moq.It.IsAny<EntityMetadata>()))
            .Returns(new EntitySchema { LogicalName = "account" });
        _mockMapper.Setup(m => m.MapOneToManyRelationship(rel))
            .Returns(new RelationshipSchema { SchemaName = "account_contact" });

        var schema = await _extractor.ExtractSchemaAsync();

        Assert.Single(schema.Relationships);
        Assert.Equal("account_contact", schema.Relationships[0].SchemaName);
    }

    [Fact]
    public async Task ExtractSchemaAsync_MapsManToManyRelationships()
    {
        var rel = new ManyToManyRelationshipMetadata { SchemaName = "account_category" };
        var entityMetadata = new EntityMetadata { LogicalName = "account" };
        SetPrivateProperty(entityMetadata, "ManyToManyRelationships", new[] { rel });

        _mockClient.Setup(c => c.GetAllEntityMetadataAsync())
            .ReturnsAsync(new Dictionary<string, List<string>> { { "account", new List<string>() } });
        _mockClient.Setup(c => c.GetEntityMetadataAsync("account", EntityFilters.Entity | EntityFilters.Attributes))
            .ReturnsAsync(new EntityMetadata { LogicalName = "account" });
        _mockClient.Setup(c => c.GetEntityMetadataAsync("account", EntityFilters.Relationships))
            .ReturnsAsync(entityMetadata);
        _mockMapper.Setup(m => m.MapEntity(Moq.It.IsAny<EntityMetadata>()))
            .Returns(new EntitySchema { LogicalName = "account" });
        _mockMapper.Setup(m => m.MapManyToManyRelationship(rel))
            .Returns(new RelationshipSchema { SchemaName = "account_category" });

        var schema = await _extractor.ExtractSchemaAsync();

        Assert.Single(schema.Relationships);
        Assert.Equal("account_category", schema.Relationships[0].SchemaName);
    }

    [Fact]
    public async Task ExtractSchemaAsync_DeduplicatesRelationshipsBySchemaName()
    {
        var rel = new OneToManyRelationshipMetadata { SchemaName = "account_contact" };

        // Two entities both return the same relationship
        var accountMeta = new EntityMetadata { LogicalName = "account" };
        var contactMeta = new EntityMetadata { LogicalName = "contact" };
        SetPrivateProperty(accountMeta, "OneToManyRelationships", new[] { rel });
        SetPrivateProperty(contactMeta, "OneToManyRelationships", new[] { rel });

        _mockClient.Setup(c => c.GetAllEntityMetadataAsync())
            .ReturnsAsync(new Dictionary<string, List<string>>
            {
                { "account", new List<string>() },
                { "contact", new List<string>() }
            });
        _mockClient.Setup(c => c.GetEntityMetadataAsync("account", EntityFilters.Entity | EntityFilters.Attributes))
            .ReturnsAsync(new EntityMetadata { LogicalName = "account" });
        _mockClient.Setup(c => c.GetEntityMetadataAsync("contact", EntityFilters.Entity | EntityFilters.Attributes))
            .ReturnsAsync(new EntityMetadata { LogicalName = "contact" });
        _mockClient.Setup(c => c.GetEntityMetadataAsync("account", EntityFilters.Relationships))
            .ReturnsAsync(accountMeta);
        _mockClient.Setup(c => c.GetEntityMetadataAsync("contact", EntityFilters.Relationships))
            .ReturnsAsync(contactMeta);
        _mockMapper.Setup(m => m.MapEntity(Moq.It.IsAny<EntityMetadata>()))
            .Returns<EntityMetadata>(e => new EntitySchema { LogicalName = e.LogicalName ?? string.Empty });
        _mockMapper.Setup(m => m.MapOneToManyRelationship(rel))
            .Returns(new RelationshipSchema { SchemaName = "account_contact" });

        var schema = await _extractor.ExtractSchemaAsync();

        Assert.Single(schema.Relationships); // Deduplicated — not 2
    }

    #endregion

    // EntityMetadata uses internal setters — set via reflection
    private static void SetPrivateProperty<T>(EntityMetadata metadata, string propertyName, T value)
    {
        var prop = typeof(EntityMetadata).GetProperty(propertyName,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        prop?.SetValue(metadata, value);
    }

    [Theory]
    [InlineData(null, 0)]
    [InlineData("", 0)]
    [InlineData("  ", 0)]
    [InlineData("RobSolution1", 1)]
    [InlineData("RobSolution1,RobSolution2", 2)]
    [InlineData("RobSolution1, RobSolution2, RobSolution3", 3)]
    [InlineData("  RobSolution1  ,  RobSolution2  ", 2)]
    [InlineData("Sol1,,Sol2", 2)] // Empty entries should be filtered
    public void ParseSolutionNames_ShouldHandleVariousInputs(string? input, int expectedCount)
    {
        // Arrange
        var extractorType = typeof(SchemaExtractor);
        var parseMethod = extractorType.GetMethod("ParseSolutionNames", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var mockMapper = new Moq.Mock<IMetadataMapper>();
        var mockDataverseClient = new Moq.Mock<IDataverseClient>();
        var extractor = new SchemaExtractor(mockMapper.Object, mockDataverseClient.Object);

        // Act
        var result = (List<string>?)parseMethod?.Invoke(extractor, new object?[] { input });

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedCount, result.Count);
    }

    [Fact]
    public void ParseSolutionNames_ShouldTrimWhitespace()
    {
        // Arrange
        var extractorType = typeof(SchemaExtractor);
        var parseMethod = extractorType.GetMethod("ParseSolutionNames", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var mockMapper = new Moq.Mock<IMetadataMapper>();
        var mockDataverseClient = new Moq.Mock<IDataverseClient>();
        var extractor = new SchemaExtractor(mockMapper.Object, mockDataverseClient.Object);

        // Act
        var result = (List<string>?)parseMethod?.Invoke(extractor, new object?[] { "  Sol1  ,  Sol2  " });

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Sol1", result);
        Assert.Contains("Sol2", result);
        Assert.DoesNotContain("  Sol1  ", result);
    }

    [Fact]
    public void Constructor_ShouldAcceptDependencies()
    {
        // Arrange
        var mockMapper = new Moq.Mock<IMetadataMapper>();
        var mockDataverseClient = new Moq.Mock<IDataverseClient>();

        // Act
        var extractor = new SchemaExtractor(mockMapper.Object, mockDataverseClient.Object);

        // Assert
        Assert.NotNull(extractor);
    }
}
