using PowerApps.CLI.IntegrationTests.Infrastructure;
using PowerApps.CLI.Models;
using Xunit;

namespace PowerApps.CLI.IntegrationTests.ConstantsGenerate;

[Collection("Dataverse")]
[Trait("Category", "Integration")]
public class ConstantsGenerateTests(DataverseFixture fixture)
{
    private const string IntegrationSolution = "XRTSoftIntegrationTests";

    private readonly DataverseFixture _fixture = fixture;

    // -------------------------------------------------------------------------
    // Smoke test — full pipeline, single-file mode
    // -------------------------------------------------------------------------

    [SkippableFact]
    public async Task GenerateConstants_SolutionFilter_ProducesTablesAndChoicesFilesAsync()
    {
        Skip.If(_fixture.ConfigurationError is not null, _fixture.ConfigurationError);

        var outputDir = await _fixture.GetOrGenerateConstantsAsync("default", IntegrationSolution, DefaultConfig());

        Assert.True(File.Exists(Path.Combine(outputDir, "Tables.cs")), "Tables.cs was not generated");
        Assert.True(new FileInfo(Path.Combine(outputDir, "Tables.cs")).Length > 0, "Tables.cs is empty");
        Assert.True(File.Exists(Path.Combine(outputDir, "Choices.cs")), "Choices.cs was not generated");
        Assert.True(new FileInfo(Path.Combine(outputDir, "Choices.cs")).Length > 0, "Choices.cs is empty");
    }

    // -------------------------------------------------------------------------
    // Multi-file mode baseline
    // -------------------------------------------------------------------------

    [SkippableFact]
    public async Task GenerateConstants_MultiFileMode_ProducesPerEntityFilesAsync()
    {
        Skip.If(_fixture.ConfigurationError is not null, _fixture.ConfigurationError);

        var outputDir = await _fixture.GetOrGenerateConstantsAsync("multi-file", IntegrationSolution, MultiFileConfig());

        // xrt_integrationtest has DisplayName "Integration Test" → IntegrationTest.cs
        var tablesFile = Path.Combine(outputDir, "Tables", "IntegrationTest.cs");
        Assert.True(File.Exists(tablesFile), $"Expected per-entity file at {tablesFile}");
        var content = await File.ReadAllTextAsync(tablesFile);
        Assert.Contains("\"xrt_integrationtest\"", content);
    }

    // -------------------------------------------------------------------------
    // Single-file mode — solution scoping covers both tables
    // -------------------------------------------------------------------------

    [SkippableFact]
    public async Task GenerateConstants_SingleFileMode_ContainsBothTablesAsync()
    {
        Skip.If(_fixture.ConfigurationError is not null, _fixture.ConfigurationError);

        var outputDir = await _fixture.GetOrGenerateConstantsAsync("default", IntegrationSolution, DefaultConfig());
        var tables = await File.ReadAllTextAsync(Path.Combine(outputDir, "Tables.cs"));

        Assert.Contains("\"xrt_integrationtest\"", tables);
        Assert.Contains("\"xrt_integrationothertest\"", tables);
    }

    // -------------------------------------------------------------------------
    // Entity metadata in Tables.cs
    // -------------------------------------------------------------------------

    [SkippableFact]
    public async Task GenerateConstants_PrimaryTable_HasCorrectEntityMetadataAsync()
    {
        Skip.If(_fixture.ConfigurationError is not null, _fixture.ConfigurationError);

        var outputDir = await _fixture.GetOrGenerateConstantsAsync("default", IntegrationSolution, DefaultConfig());
        var tables = await File.ReadAllTextAsync(Path.Combine(outputDir, "Tables.cs"));

        // EntityLogicalName, PrimaryIdAttribute, PrimaryNameAttribute come from real
        // MapEntity output — unit tests provide these via mocked properties
        Assert.Contains("\"xrt_integrationtest\"", tables);
        Assert.Contains("\"xrt_integrationtestid\"", tables);
        Assert.Contains("\"xrt_name\"", tables);
    }

    // -------------------------------------------------------------------------
    // Global choices in Choices.cs
    // -------------------------------------------------------------------------

    [SkippableFact]
    public async Task GenerateConstants_GlobalChoice_GeneratesCorrectOptionValuesAsync()
    {
        Skip.If(_fixture.ConfigurationError is not null, _fixture.ConfigurationError);

        var outputDir = await _fixture.GetOrGenerateConstantsAsync("default", IntegrationSolution, DefaultConfig());
        var choices = await File.ReadAllTextAsync(Path.Combine(outputDir, "Choices.cs"));

        // ExtractGlobalOptionSets deduplication and GenerateGlobalOptionSetClass with real Dataverse data
        Assert.Contains("= 971940000;", choices);
        Assert.Contains("= 971940001;", choices);
    }

    // -------------------------------------------------------------------------
    // Local choice nested class in Tables.cs
    // -------------------------------------------------------------------------

    [SkippableFact]
    public async Task GenerateConstants_LocalChoice_GeneratesNestedClassAsync()
    {
        Skip.If(_fixture.ConfigurationError is not null, _fixture.ConfigurationError);

        var outputDir = await _fixture.GetOrGenerateConstantsAsync("default", IntegrationSolution, DefaultConfig());
        var tables = await File.ReadAllTextAsync(Path.Combine(outputDir, "Tables.cs"));

        // AppendLocalOptionSets is the most complex template path — this is the only test
        // that runs it against real Dataverse data rather than mocked EntitySchema objects.
        Assert.Contains("\"xrt_localchoice\"", tables);
        Assert.Contains("= 971940000;", tables);
        Assert.Contains("= 971940001;", tables);
    }

    // -------------------------------------------------------------------------
    // SkipVirtualFields filter
    // -------------------------------------------------------------------------

    [SkippableFact]
    public async Task GenerateConstants_SkipVirtualFields_ExcludesCurrencyCompanionAsync()
    {
        Skip.If(_fixture.ConfigurationError is not null, _fixture.ConfigurationError);

        var config = DefaultConfig();
        config.SkipVirtualFields = true;
        var outputDir = await _fixture.GetOrGenerateConstantsAsync("skip-virtual", IntegrationSolution, config);
        var tables = await File.ReadAllTextAsync(Path.Combine(outputDir, "Tables.cs"));

        // xrt_currencyfield_base is a read-only (IsValidForCreate=false, IsValidForUpdate=false) Money
        // field — the base-currency companion. AttributeOf is unreliable for these in the SDK response,
        // so the filter detects them by type + writability. Unit tests cannot verify real SDK values.
        Assert.DoesNotContain("xrt_currencyfield_base", tables);
        Assert.Contains("\"xrt_currencyfield\"", tables);
    }

    // -------------------------------------------------------------------------
    // --pascal-case false
    // -------------------------------------------------------------------------

    [SkippableFact]
    public async Task GenerateConstants_PascalCaseFalse_ProducesValidOutputAsync()
    {
        Skip.If(_fixture.ConfigurationError is not null, _fixture.ConfigurationError);

        var config = DefaultConfig();
        config.PascalCaseConversion = false;
        var outputDir = await _fixture.GetOrGenerateConstantsAsync("no-pascal", IntegrationSolution, config);
        var tables = await File.ReadAllTextAsync(Path.Combine(outputDir, "Tables.cs"));

        // String constant values are invariant with respect to PascalCase — the flag affects
        // C# identifier names, not the Dataverse logical name values being stored.
        Assert.Contains("\"xrt_integrationtest\"", tables);
        Assert.Contains("\"xrt_name\"", tables);
    }

    // -------------------------------------------------------------------------
    // --include-entities false
    // -------------------------------------------------------------------------

    [SkippableFact]
    public async Task GenerateConstants_IncludeEntitiesFalse_OmitsTablesFileAsync()
    {
        Skip.If(_fixture.ConfigurationError is not null, _fixture.ConfigurationError);

        var config = DefaultConfig();
        config.IncludeEntities = false;
        var outputDir = await _fixture.GetOrGenerateConstantsAsync("no-entities", IntegrationSolution, config);

        Assert.False(File.Exists(Path.Combine(outputDir, "Tables.cs")),
            "Tables.cs should not exist when IncludeEntities=false");
        Assert.True(File.Exists(Path.Combine(outputDir, "Choices.cs")),
            "Choices.cs should still be generated");
        var choices = await File.ReadAllTextAsync(Path.Combine(outputDir, "Choices.cs"));
        Assert.Contains("= 971940000;", choices);
    }

    // -------------------------------------------------------------------------
    // --include-option-sets false
    // -------------------------------------------------------------------------

    [SkippableFact]
    public async Task GenerateConstants_IncludeOptionSetsFalse_OmitsChoicesFileAsync()
    {
        Skip.If(_fixture.ConfigurationError is not null, _fixture.ConfigurationError);

        var config = DefaultConfig();
        config.IncludeGlobalOptionSets = false;
        var outputDir = await _fixture.GetOrGenerateConstantsAsync("no-choices", IntegrationSolution, config);

        Assert.True(File.Exists(Path.Combine(outputDir, "Tables.cs")),
            "Tables.cs should still be generated");
        Assert.False(File.Exists(Path.Combine(outputDir, "Choices.cs")),
            "Choices.cs should not exist when IncludeGlobalOptionSets=false");
    }

    // -------------------------------------------------------------------------
    // --exclude-entities
    // -------------------------------------------------------------------------

    [SkippableFact]
    public async Task GenerateConstants_ExcludeEntities_OmitsExcludedEntityFileAsync()
    {
        Skip.If(_fixture.ConfigurationError is not null, _fixture.ConfigurationError);

        var config = MultiFileConfig();
        config.ExcludeEntities = ["xrt_integrationtest"];
        var outputDir = await _fixture.GetOrGenerateConstantsAsync("exclude-primary", IntegrationSolution, config);
        var tablesDir = Path.Combine(outputDir, "Tables");

        Assert.False(File.Exists(Path.Combine(tablesDir, "IntegrationTest.cs")),
            "IntegrationTest.cs should be absent after excluding xrt_integrationtest");

        // The other entity's file should be present — read all remaining files rather than
        // hardcoding the filename, which depends on xrt_integrationothertest's display name.
        var remainingFiles = Directory.GetFiles(tablesDir, "*.cs");
        Assert.True(remainingFiles.Length > 0, "At least one entity file should remain after exclusion");
        var allContent = string.Concat(await Task.WhenAll(remainingFiles.Select(f => File.ReadAllTextAsync(f))));
        Assert.Contains("\"xrt_integrationothertest\"", allContent);
    }

    // -------------------------------------------------------------------------
    // --attribute-prefix
    // -------------------------------------------------------------------------

    [SkippableFact]
    public async Task GenerateConstants_AttributePrefix_IncludesOnlyPrefixedAttributesAsync()
    {
        Skip.If(_fixture.ConfigurationError is not null, _fixture.ConfigurationError);

        var config = DefaultConfig();
        config.AttributePrefix = "xrt_";
        var outputDir = await _fixture.GetOrGenerateConstantsAsync("attribute-prefix-xrt", IntegrationSolution, config);
        var tables = await File.ReadAllTextAsync(Path.Combine(outputDir, "Tables.cs"));

        Assert.Contains("\"xrt_name\"", tables);
        Assert.DoesNotContain("\"createdon\"", tables);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static ConstantsConfig DefaultConfig() => new()
    {
        SingleFile = true,
        IncludeEntities = true,
        IncludeGlobalOptionSets = true,
        PascalCaseConversion = true
    };

    private static ConstantsConfig MultiFileConfig() => new()
    {
        SingleFile = false,
        IncludeEntities = true,
        IncludeGlobalOptionSets = true,
        PascalCaseConversion = true
    };
}
