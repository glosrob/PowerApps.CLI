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
    // Smoke test — full pipeline
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
    // Entity metadata in Tables.cs
    // -------------------------------------------------------------------------

    [SkippableFact]
    public async Task GenerateConstants_PrimaryTable_HasCorrectEntityMetadataAsync()
    {
        Skip.If(_fixture.ConfigurationError is not null, _fixture.ConfigurationError);

        var outputDir = await _fixture.GetOrGenerateConstantsAsync("default", IntegrationSolution, DefaultConfig());
        var tables = await File.ReadAllTextAsync(Path.Combine(outputDir, "Tables.cs"));

        // EntityLogicalName, PrimaryIdAttribute, PrimaryNameAttribute constants come from real
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
        // xrt_localchoice must appear as a string constant and its option values as int constants.
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

        // xrt_currencyfield_base has AttributeOf = "xrt_currencyfield" in the real SDK response,
        // so it must be excluded. Unit tests mock AttributeOf to an empty string and cannot verify this.
        Assert.DoesNotContain("xrt_currencyfield_base", tables);
        Assert.Contains("\"xrt_currencyfield\"", tables);
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
}
