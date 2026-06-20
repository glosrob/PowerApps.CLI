using PowerApps.CLI.IntegrationTests.Infrastructure;
using PowerApps.CLI.Models;
using PowerApps.CLI.Services;
using Xunit;

namespace PowerApps.CLI.IntegrationTests.SchemaExport;

[Collection("Dataverse")]
[Trait("Category", "Integration")]
public class SchemaExportTests(DataverseFixture fixture)
{
    private const string IntegrationSolution = "XRTSoftIntegrationTests";
    private const string PrimaryTable = "xrt_integrationtest";

    private readonly DataverseFixture _fixture = fixture;

    [SkippableFact]
    public async Task ExtractSchema_NoFilter_ReturnsNonEmptySchemaAsync()
    {
        Skip.If(_fixture.ConfigurationError is not null, _fixture.ConfigurationError);

        // Arrange — no solution filter retrieves all-table metadata, so this test
        // extracts directly rather than via the solution-scoped cache.
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

        // Act
        var schema = await _fixture.GetSolutionSchemaAsync(IntegrationSolution);

        // Assert
        Assert.Contains(schema.Entities, e => e.LogicalName == PrimaryTable);
    }

    [SkippableFact]
    public async Task ExtractSchema_PrimaryTable_MapsAttributeTypesAsync()
    {
        Skip.If(_fixture.ConfigurationError is not null, _fixture.ConfigurationError);

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
        var table = await GetPrimaryTableAsync();

        // Assert
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
    public async Task ExtractSchema_GlobalChoiceColumn_PopulatesOptionSetAsync()
    {
        Skip.If(_fixture.ConfigurationError is not null, _fixture.ConfigurationError);

        // Act
        var table = await GetPrimaryTableAsync();
        var choice = GetAttribute(table, "xrt_globalchoice");

        // Assert
        Assert.NotNull(choice.OptionSet);
        Assert.True(choice.OptionSet!.IsGlobal);
        Assert.Equal("xrt_globalchoice", choice.OptionSet.Name);
        Assert.Contains(choice.OptionSet.Options, o => o.Value == 971940000 && o.Label == "Choice 1");
        Assert.Contains(choice.OptionSet.Options, o => o.Value == 971940001 && o.Label == "Choice 2");
    }

    [SkippableFact]
    public async Task ExtractSchema_MultiSelectChoiceColumn_ResolvesSameGlobalOptionSetAsync()
    {
        Skip.If(_fixture.ConfigurationError is not null, _fixture.ConfigurationError);

        // Act — fetch both the picklist and multi-select columns from the same schema so we can
        // assert they genuinely resolve to the same global option set, not just coincidentally
        // matching values.
        var table = await GetPrimaryTableAsync();
        var globalChoice = GetAttribute(table, "xrt_globalchoice");
        var multiSelect = GetAttribute(table, "xrt_multiselectglobalchoicefield");

        // Assert
        Assert.NotNull(globalChoice.OptionSet);
        Assert.NotNull(multiSelect.OptionSet);
        Assert.True(multiSelect.OptionSet!.IsGlobal);
        Assert.Equal(globalChoice.OptionSet!.Name, multiSelect.OptionSet.Name);
        Assert.Contains(multiSelect.OptionSet.Options, o => o.Value == 971940000 && o.Label == "Choice 1");
        Assert.Contains(multiSelect.OptionSet.Options, o => o.Value == 971940001 && o.Label == "Choice 2");
    }

    [SkippableFact]
    public async Task ExtractSchema_LocalChoiceColumn_PopulatesNonGlobalOptionSetAsync()
    {
        Skip.If(_fixture.ConfigurationError is not null, _fixture.ConfigurationError);

        // Act
        var table = await GetPrimaryTableAsync();
        var localChoice = GetAttribute(table, "xrt_localchoice");

        // Assert — local choice has an inline option set that is not global
        // (canonical values per tests/fixtures/integration-test-schema.json)
        Assert.NotNull(localChoice.OptionSet);
        Assert.False(localChoice.OptionSet!.IsGlobal);
        Assert.Equal("xrt_integrationtest_xrt_localchoice", localChoice.OptionSet.Name);
        Assert.Contains(localChoice.OptionSet.Options, o => o.Value == 971940000 && o.Label == "Local 1");
        Assert.Contains(localChoice.OptionSet.Options, o => o.Value == 971940001 && o.Label == "Local 2");
    }

    [SkippableFact]
    public async Task ExtractSchema_LookupColumn_ProducesOneToManyRelationshipAsync()
    {
        Skip.If(_fixture.ConfigurationError is not null, _fixture.ConfigurationError);

        // Act — the xrt_lookupfield column produces a custom N:1 relationship
        // (canonical values per tests/fixtures/integration-test-schema.json)
        var schema = await _fixture.GetSolutionSchemaAsync(IntegrationSolution);
        var relationship = schema.Relationships
            .SingleOrDefault(r => r.SchemaName == "xrt_integrationtest_LookupField_xrt_integrationothertest");

        // Assert
        Assert.NotNull(relationship);
        Assert.Equal("OneToMany", relationship!.RelationshipType);
        Assert.Equal(PrimaryTable, relationship.ReferencingEntity);
        Assert.Equal("xrt_lookupfield", relationship.ReferencingAttribute);
        Assert.Equal("xrt_integrationothertest", relationship.ReferencedEntity);
        Assert.Equal("xrt_integrationothertestid", relationship.ReferencedAttribute);
        Assert.True(relationship.IsCustomRelationship);
    }

    [SkippableFact]
    public async Task ExtractSchema_ManyToManyRelationship_IsExtractedAsync()
    {
        Skip.If(_fixture.ConfigurationError is not null, _fixture.ConfigurationError);

        // Act — the custom N:N between the two test tables
        // (canonical values per tests/fixtures/integration-test-schema.json)
        var schema = await _fixture.GetSolutionSchemaAsync(IntegrationSolution);
        var relationship = schema.Relationships
            .SingleOrDefault(r => r.SchemaName == "xrt_IntegrationTest_xrt_IntegrationOtherTest_xrt_IntegrationOtherTest");

        // Assert
        Assert.NotNull(relationship);
        Assert.Equal("ManyToMany", relationship!.RelationshipType);
        Assert.Equal("xrt_integrationothertest", relationship.Entity1LogicalName);
        Assert.Equal(PrimaryTable, relationship.Entity2LogicalName);
        Assert.Equal("xrt_integrationtest_xrt_integrationothe", relationship.IntersectEntityName);
        Assert.True(relationship.IsCustomRelationship);
    }

    [SkippableFact]
    public async Task ExtractSchema_Relationships_AreDeduplicatedBySchemaNameAsync()
    {
        Skip.If(_fixture.ConfigurationError is not null, _fixture.ConfigurationError);

        // The N:N relationship is reachable from both endpoint tables, so without deduplication
        // it would appear twice when both tables are in scope. Assert each SchemaName is unique.
        var schema = await _fixture.GetSolutionSchemaAsync(IntegrationSolution);

        var duplicates = schema.Relationships
            .GroupBy(r => r.SchemaName)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key} (x{g.Count()})")
            .ToList();

        Assert.True(duplicates.Count == 0, "Duplicate relationships:\n" + string.Join("\n", duplicates));
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

        // Act
        var schema = await _fixture.GetSolutionSchemaAsync("NonExistentSolution_DoesNotExist");

        // Assert
        Assert.Empty(schema.Entities);
    }

    private async Task<EntitySchema> GetPrimaryTableAsync()
    {
        var schema = await _fixture.GetSolutionSchemaAsync(IntegrationSolution);
        var table = schema.Entities.SingleOrDefault(e => e.LogicalName == PrimaryTable);
        Assert.NotNull(table);
        return table!;
    }

    private static AttributeSchema GetAttribute(EntitySchema table, string logicalName)
    {
        var attribute = table.Attributes.SingleOrDefault(a => a.LogicalName == logicalName);
        Assert.NotNull(attribute);
        return attribute!;
    }
}
