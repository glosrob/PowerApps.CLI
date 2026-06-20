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

        var extractor = new SchemaExtractor(new MetadataMapper(), _fixture.Client);

        var schema = await extractor.ExtractSchemaAsync();

        Assert.NotEmpty(schema.Entities);
    }

    [SkippableFact]
    public async Task ExtractSchema_IntegrationTestSolution_ContainsPrimaryTableAsync()
    {
        Skip.If(_fixture.ConfigurationError is not null, _fixture.ConfigurationError);

        var extractor = new SchemaExtractor(new MetadataMapper(), _fixture.Client);

        var schema = await extractor.ExtractSchemaAsync("XRTSoftIntegrationTests");

        Assert.Contains(schema.Entities, e => e.LogicalName == "xrt_integrationtest");
    }

    [Fact(Skip = "Attribute prefix filtering is not implemented in SchemaExtractor — tracked as a gap in SchemaService.ExportSchemaAsync")]
    public Task ExtractSchema_AttributePrefix_ReturnsOnlyPrefixedAttributesAsync()
    {
        throw new NotImplementedException(
            "Attribute prefix filtering not yet implemented — see SchemaService.ExportSchemaAsync.");
    }

    [SkippableFact]
    public async Task ExtractSchema_NonExistentSolution_ReturnsEmptySchemaAsync()
    {
        Skip.If(_fixture.ConfigurationError is not null, _fixture.ConfigurationError);

        var extractor = new SchemaExtractor(new MetadataMapper(), _fixture.Client);

        var schema = await extractor.ExtractSchemaAsync("NonExistentSolution_DoesNotExist");

        Assert.Empty(schema.Entities);
    }
}
