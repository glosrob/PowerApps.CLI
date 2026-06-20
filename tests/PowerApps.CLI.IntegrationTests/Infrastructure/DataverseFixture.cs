using PowerApps.CLI.Infrastructure;
using PowerApps.CLI.Models;
using PowerApps.CLI.Services;
using Xunit;

namespace PowerApps.CLI.IntegrationTests.Infrastructure;

public class DataverseFixture : IDisposable
{
    private readonly DataverseClient? _client;
    private readonly Dictionary<string, PowerAppsSchema> _schemaCache = new();

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

    public void Dispose() => _client?.Dispose();
}

[CollectionDefinition("Dataverse")]
public class DataverseCollection : ICollectionFixture<DataverseFixture> { }
