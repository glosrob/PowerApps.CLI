using Microsoft.Xrm.Sdk.Metadata;
using PowerApps.CLI.Infrastructure;
using PowerApps.CLI.Models;
using PowerApps.CLI.Services;
using Xunit;

namespace PowerApps.CLI.Tests.Services;

public class SchemaExtractorTests
{
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

    [Fact]
    public async Task ExtractSchemaAsync_SortsTablesColumnsRelationshipsAndTargetsAsync()
    {
        // Arrange - everything supplied out of order, as Dataverse may return it
        var extractor = CreateExtractor(
            ("rob_zeta", new[] { "rob_zcol", "rob_acol" }, new[] { "rob_zeta_z_rel", "rob_zeta_a_rel" }),
            ("rob_alpha", new[] { "rob_lookup" }, new[] { "rob_alpha_m_rel" }));

        // Act
        var schema = await extractor.ExtractSchemaAsync("RobSolution");

        // Assert
        Assert.Equal(new[] { "rob_alpha", "rob_zeta" }, schema.Entities.Select(e => e.LogicalName));
        Assert.Equal(new[] { "rob_acol", "rob_zcol" }, schema.Entities[1].Attributes.Select(a => a.LogicalName));
        Assert.Equal(new[] { "rob_alpha_m_rel", "rob_zeta_a_rel", "rob_zeta_z_rel" }, schema.Relationships.Select(r => r.SchemaName));
        Assert.Equal(new[] { "rob_atarget", "rob_ztarget" }, schema.Entities[0].Attributes[0].Targets);
    }

    [Fact]
    public async Task ExtractSchemaAsync_LeavesChoiceOptionsInMetadataOrderAsync()
    {
        // Arrange - option order is the maker-defined display order, so it must not be sorted
        var extractor = CreateExtractor(("rob_alpha", new[] { "rob_choice" }, Array.Empty<string>()));

        // Act
        var schema = await extractor.ExtractSchemaAsync("RobSolution");

        // Assert
        var options = schema.Entities[0].Attributes[0].OptionSet!.Options;
        Assert.Equal(new[] { 3, 1, 2 }, options.Select(o => o.Value));
    }

    private static SchemaExtractor CreateExtractor(params (string Table, string[] Columns, string[] Relationships)[] tables)
    {
        var mockClient = new Moq.Mock<IDataverseClient>();
        mockClient.Setup(c => c.GetEnvironmentUrl()).Returns("https://rob.crm.dynamics.com");
        mockClient.Setup(c => c.GetOrganizationName()).Returns("Rob");
        mockClient.Setup(c => c.GetEntitiesFromSolutionAsync("RobSolution"))
            .ReturnsAsync(tables.ToDictionary(t => t.Table, _ => new List<string> { "RobSolution" }));

        foreach (var (table, columns, relationships) in tables)
        {
            var metadata = new EntityMetadata { LogicalName = table };
            SetNonPublic(metadata, "Attributes",
                columns.Select(c => (AttributeMetadata)new StringAttributeMetadata { LogicalName = c }).ToArray());
            SetNonPublic(metadata, "OneToManyRelationships",
                relationships.Select(r => new OneToManyRelationshipMetadata { SchemaName = r }).ToArray());
            SetNonPublic(metadata, "ManyToManyRelationships", Array.Empty<ManyToManyRelationshipMetadata>());

            mockClient.Setup(c => c.GetEntityMetadataAsync(table, Moq.It.IsAny<EntityFilters>())).ReturnsAsync(metadata);
        }

        var mockMapper = new Moq.Mock<IMetadataMapper>();
        mockMapper.Setup(m => m.MapEntity(Moq.It.IsAny<EntityMetadata>()))
            .Returns((EntityMetadata m) => new EntitySchema { LogicalName = m.LogicalName });
        mockMapper.Setup(m => m.MapAttribute(Moq.It.IsAny<AttributeMetadata>()))
            .Returns((AttributeMetadata a) => new AttributeSchema
            {
                LogicalName = a.LogicalName,
                Targets = a.LogicalName == "rob_lookup" ? new[] { "rob_ztarget", "rob_atarget" } : null,
                OptionSet = a.LogicalName == "rob_choice"
                    ? new OptionSetSchema
                    {
                        Options = new List<OptionSchema>
                        {
                            new() { Value = 3, Label = "Third" },
                            new() { Value = 1, Label = "First" },
                            new() { Value = 2, Label = "Second" }
                        }
                    }
                    : null
            });
        mockMapper.Setup(m => m.MapOneToManyRelationship(Moq.It.IsAny<OneToManyRelationshipMetadata>()))
            .Returns((OneToManyRelationshipMetadata r) => new RelationshipSchema { SchemaName = r.SchemaName });

        return new SchemaExtractor(mockMapper.Object, mockClient.Object);
    }

    private static void SetNonPublic(EntityMetadata metadata, string propertyName, object value)
    {
        typeof(EntityMetadata).GetProperty(propertyName)!.SetValue(metadata, value);
    }
}
