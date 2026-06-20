using PowerApps.CLI.IntegrationTests.Infrastructure;
using PowerApps.CLI.Services;
using Xunit;

namespace PowerApps.CLI.IntegrationTests.SchemaExport;

[Collection("Dataverse")]
[Trait("Category", "Integration")]
public class SchemaExportTests(DataverseFixture fixture)
{
    private readonly DataverseFixture _fixture = fixture;

    [SkippableFact]
    public async Task ExtractSchema_NoFilter_ReturnsNonEmptySchemaAsync()
    {
        Skip.If(_fixture.ConfigurationError is not null, _fixture.ConfigurationError);

        // Arrange
        var extractor = new SchemaExtractor(new MetadataMapper(), _fixture.Client);

        // Act
        var schema = await extractor.ExtractSchemaAsync();

        // Assert
        Assert.NotEmpty(schema.Entities);
    }

    [SkippableFact]
    public async Task ExtractSchema_IntegrationTestSolution_ContainsPrimaryTableAsync()
    {
        Skip.If(_fixture.ConfigurationError is not null, _fixture.ConfigurationError);

        // Arrange
        var extractor = new SchemaExtractor(new MetadataMapper(), _fixture.Client);

        // Act
        var schema = await extractor.ExtractSchemaAsync("XRTSoftIntegrationTests");

        // Assert
        Assert.Contains(schema.Entities, e => e.LogicalName == "xrt_integrationtest");
    }

    [SkippableFact]
    public Task ExtractSchema_AttributePrefix_ReturnsOnlyPrefixedAttributesAsync()
    {
        Skip.If(true, "Attribute prefix filtering is not implemented — see SchemaService.ExportSchemaAsync.");
        return Task.CompletedTask;
    }

    [SkippableFact]
    public async Task ExtractSchema_NonExistentSolution_ReturnsEmptySchemaAsync()
    {
        Skip.If(_fixture.ConfigurationError is not null, _fixture.ConfigurationError);

        // Arrange
        var extractor = new SchemaExtractor(new MetadataMapper(), _fixture.Client);

        // Act
        var schema = await extractor.ExtractSchemaAsync("NonExistentSolution_DoesNotExist");

        // Assert
        Assert.Empty(schema.Entities);
    }
}
