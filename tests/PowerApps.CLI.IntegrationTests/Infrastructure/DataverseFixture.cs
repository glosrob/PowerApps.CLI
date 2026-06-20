using PowerApps.CLI.Infrastructure;
using Xunit;

namespace PowerApps.CLI.IntegrationTests.Infrastructure;

public class DataverseFixture : IDisposable
{
    private readonly DataverseClient _client;

    public IDataverseClient Client => _client;

    public DataverseFixture()
    {
        var config = IntegrationTestConfig.Load();
        _client = new DataverseClient(config.Url, config.ClientId, config.ClientSecret);
    }

    public void Dispose() => _client.Dispose();
}

[CollectionDefinition("Dataverse")]
public class DataverseCollection : ICollectionFixture<DataverseFixture> { }
