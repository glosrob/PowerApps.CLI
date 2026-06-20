using PowerApps.CLI.Infrastructure;
using Xunit;

namespace PowerApps.CLI.IntegrationTests.Infrastructure;

public class DataverseFixture : IDisposable
{
    public DataverseClient Client { get; }

    public DataverseFixture()
    {
        var config = IntegrationTestConfig.Load();
        Client = new DataverseClient(config.Url, config.ClientId, config.ClientSecret);
    }

    public void Dispose() => Client.Dispose();
}

[CollectionDefinition("Dataverse")]
public class DataverseCollection : ICollectionFixture<DataverseFixture> { }
