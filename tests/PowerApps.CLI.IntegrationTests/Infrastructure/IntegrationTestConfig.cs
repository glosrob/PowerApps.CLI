using System.Text.Json;

namespace PowerApps.CLI.IntegrationTests.Infrastructure;

public class IntegrationTestConfig
{
    public string Url { get; init; } = string.Empty;
    public string ClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;

    public static IntegrationTestConfig Load()
    {
        var connectionsPath = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "scripts", "connections.json"));

        if (File.Exists(connectionsPath))
        {
            var json = File.ReadAllText(connectionsPath);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("IntegrationTests", out var section))
            {
                return new IntegrationTestConfig
                {
                    Url = section.GetProperty("Url").GetString() ?? string.Empty,
                    ClientId = section.GetProperty("ClientId").GetString() ?? string.Empty,
                    ClientSecret = section.GetProperty("ClientSecret").GetString() ?? string.Empty,
                };
            }
        }

        var url = Environment.GetEnvironmentVariable("DATAVERSE_URL");
        var clientId = Environment.GetEnvironmentVariable("DATAVERSE_CLIENT_ID");
        var clientSecret = Environment.GetEnvironmentVariable("DATAVERSE_CLIENT_SECRET");

        if (!string.IsNullOrEmpty(url) && !string.IsNullOrEmpty(clientId) && !string.IsNullOrEmpty(clientSecret))
            return new IntegrationTestConfig { Url = url, ClientId = clientId, ClientSecret = clientSecret };

        throw new IntegrationTestConfigurationException(
            $"Integration test connection details not found. " +
            $"Create tests/scripts/connections.json with an 'IntegrationTests' key, " +
            $"or set DATAVERSE_URL, DATAVERSE_CLIENT_ID, and DATAVERSE_CLIENT_SECRET environment variables. " +
            $"Looked for connections.json at: {connectionsPath}");
    }
}

public class IntegrationTestConfigurationException(string message) : Exception(message);
