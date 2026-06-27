using PowerApps.CLI.Infrastructure;
using PowerApps.CLI.Models;
using PowerApps.CLI.Services;
using Xunit;

namespace PowerApps.CLI.IntegrationTests.Infrastructure;

public class DataverseFixture : IDisposable
{
    private readonly DataverseClient? _client;
    private readonly Dictionary<string, PowerAppsSchema> _schemaCache = new();
    private readonly Dictionary<string, string> _constantsOutputCache = new();

    /// <summary>
    /// The shared Dataverse client. Only valid when <see cref="ConfigurationError"/> is null —
    /// tests should guard access with <c>Skip.If(fixture.ConfigurationError is not null, ...)</c>.
    /// </summary>
    public IDataverseClient Client => _client
        ?? throw new InvalidOperationException("Dataverse client is not configured — check ConfigurationError.");

    /// <summary>
    /// Populated when connection details could not be loaded. Tests use this to skip gracefully
    /// rather than failing when the environment is not configured.
    /// </summary>
    public string? ConfigurationError { get; }

    public DataverseFixture()
    {
        try
        {
            var config = IntegrationTestConfig.Load();
            _client = new DataverseClient(config.Url, config.ClientId, config.ClientSecret);
        }
        catch (IntegrationTestConfigurationException ex)
        {
            ConfigurationError = ex.Message;
        }
    }

    /// <summary>
    /// Extracts and caches the schema for a solution. Extraction is the expensive part of an
    /// integration run, so the result is reused across tests in the collection. Tests within a
    /// collection run serially, so no synchronisation is required. Returned schema is read-only
    /// from the tests' perspective — do not mutate it.
    /// </summary>
    public async Task<PowerAppsSchema> GetSolutionSchemaAsync(string solutionName)
    {
        if (!_schemaCache.TryGetValue(solutionName, out var schema))
        {
            var extractor = new SchemaExtractor(new MetadataMapper(), Client);
            schema = await extractor.ExtractSchemaAsync(solutionName);
            _schemaCache[solutionName] = schema;
        }

        return schema;
    }

    /// <summary>
    /// Generates constants for a solution and caches the output directory by <paramref name="cacheKey"/>.
    /// Multiple tests with the same key share a single generation run; different keys (e.g. different
    /// filter configs) each get their own run. The temp directories are deleted on fixture disposal.
    /// </summary>
    public async Task<string> GetOrGenerateConstantsAsync(string cacheKey, string solution, ConstantsConfig config)
    {
        if (_constantsOutputCache.TryGetValue(cacheKey, out var cachedDir))
            return cachedDir;

        var outputDir = Path.Combine(Path.GetTempPath(), $"constants-it-{Guid.NewGuid():N}");
        var schema = await GetSolutionSchemaAsync(solution);

        var filter = new ConstantsFilter();
        var entities = schema.Entities.ToList();

        if (config.ExcludeEntities.Count > 0)
            entities = filter.FilterEntities(entities, config);

        for (int i = 0; i < entities.Count; i++)
            entities[i] = filter.FilterAttributes(entities[i], config);

        var outputConfig = new ConstantsOutputConfig
        {
            OutputPath = outputDir,
            Namespace = "XRTSoft.Constants",
            SingleFile = config.SingleFile,
            IncludeEntities = config.IncludeEntities,
            IncludeGlobalOptionSets = config.IncludeGlobalOptionSets,
            PascalCaseConversion = config.PascalCaseConversion,
            ExcludeAttributes = config.ExcludeAttributes
        };

        var generator = new ConstantsGenerator(
            new CodeTemplateGenerator(true, true, new IdentifierFormatter(config.PascalCaseConversion)),
            filter,
            new FileWriter());

        await generator.GenerateAsync(entities, outputConfig, new ConsoleLogger());

        _constantsOutputCache[cacheKey] = outputDir;
        return outputDir;
    }

    public void Dispose()
    {
        _client?.Dispose();

        foreach (var dir in _constantsOutputCache.Values)
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }
}

[CollectionDefinition("Dataverse")]
public class DataverseCollection : ICollectionFixture<DataverseFixture> { }
