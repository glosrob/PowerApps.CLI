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
    public async Task ExtractSchema_PrimaryTable_MapsAttributeTypesAsync()
    {
        Skip.If(_fixture.ConfigurationError is not null, _fixture.ConfigurationError);

        // Arrange
        var extractor = new SchemaExtractor(new MetadataMapper(), _fixture.Client);

        // Expected AttributeType for each column on xrt_integrationtest. Values are the SDK
        // AttributeTypeCode names, except MultiSelectPicklist/File/Image which MetadataMapper
        // overrides (the SDK reports those as "Virtual").
        //
        // NOTE: xrt_imagefield currently FAILS — the mapper reports "Virtual" instead of "Image".
        // This is an intentional failing assertion documenting the bug tracked in #69; it turns
        // green once the ImageAttributeMetadata override is added.
        var expectedTypes = new Dictionary<string, string>
        {
            ["xrt_name"] = "String",
            ["xrt_multilinetext"] = "Memo",
            ["xrt_wholenumber"] = "Integer",
            ["xrt_decimalnumber"] = "Decimal",
            ["xrt_floatnumber"] = "Double",
            ["xrt_currencyfield"] = "Money",
            ["xrt_currencyfield_base"] = "Money",
            ["xrt_boolfield"] = "Boolean",
            ["xrt_localchoice"] = "Picklist",
            ["xrt_globalchoice"] = "Picklist",
            ["xrt_multiselectglobalchoicefield"] = "MultiSelectPicklist",
            ["xrt_dateonlyfield"] = "DateTime",
            ["xrt_datetimefield"] = "DateTime",
            ["xrt_lookupfield"] = "Lookup",
            ["xrt_customerfield"] = "Customer",
            ["xrt_filefield"] = "File",
            ["xrt_imagefield"] = "Image",
            ["xrt_formulafield"] = "String",
        };

        // Act
        var schema = await extractor.ExtractSchemaAsync("XRTSoftIntegrationTests");
        var table = schema.Entities.SingleOrDefault(e => e.LogicalName == "xrt_integrationtest");

        // Assert
        Assert.NotNull(table);

        var mismatches = new List<string>();
        foreach (var (logicalName, expectedType) in expectedTypes)
        {
            var attribute = table.Attributes.SingleOrDefault(a => a.LogicalName == logicalName);
            if (attribute is null)
            {
                mismatches.Add($"{logicalName}: column not found");
            }
            else if (attribute.AttributeType != expectedType)
            {
                mismatches.Add($"{logicalName}: expected '{expectedType}' but was '{attribute.AttributeType}'");
            }
        }

        Assert.True(mismatches.Count == 0, "Attribute type mismatches:\n" + string.Join("\n", mismatches));
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
