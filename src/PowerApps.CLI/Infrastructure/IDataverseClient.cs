using Microsoft.PowerPlatform.Dataverse.Client;

namespace PowerApps.CLI.Infrastructure;

/// <summary>
/// Provides authentication and connection to Dataverse environments.
/// </summary>
public interface IDataverseClient
{
    /// <summary>
    /// Connects to a Dataverse environment using the specified authentication method.
    /// </summary>
    /// <param name="url">The Dataverse environment URL (e.g., https://org.crm.dynamics.com).</param>
    /// <param name="clientId">Optional Azure AD Application (Client) ID for service principal authentication.</param>
    /// <param name="clientSecret">Optional Azure AD Application Client Secret for service principal authentication.</param>
    /// <param name="connectionString">Optional connection string for advanced scenarios.</param>
    /// <returns>An authenticated ServiceClient instance.</returns>
    Task<ServiceClient> ConnectAsync(string url, string? clientId = null, string? clientSecret = null, string? connectionString = null);

    /// <summary>
    /// Gets the organization name from the connected service client.
    /// </summary>
    /// <param name="serviceClient">The connected service client.</param>
    /// <returns>The organization friendly name.</returns>
    string GetOrganizationName(ServiceClient serviceClient);

    /// <summary>
    /// Gets the environment URL from the connected service client.
    /// </summary>
    /// <param name="serviceClient">The connected service client.</param>
    /// <returns>The environment URL.</returns>
    string GetEnvironmentUrl(ServiceClient serviceClient);

    /// <summary>
    /// Validates that the service client is ready and connected.
    /// </summary>
    /// <param name="serviceClient">The service client to validate.</param>
    /// <returns>True if connected and ready, false otherwise.</returns>
    bool IsConnected(ServiceClient serviceClient);
}
