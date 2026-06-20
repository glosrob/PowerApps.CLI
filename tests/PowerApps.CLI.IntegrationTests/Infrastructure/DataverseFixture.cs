using PowerApps.CLI.Infrastructure;
using Xunit;

namespace PowerApps.CLI.IntegrationTests.Infrastructure;

public class DataverseFixture : IDisposable
{
    private readonly DataverseClient? _client;

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

    public void Dispose() => _client?.Dispose();
}

[CollectionDefinition("Dataverse")]
public class DataverseCollection : ICollectionFixture<DataverseFixture> { }
