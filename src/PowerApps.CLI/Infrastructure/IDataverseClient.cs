using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;

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
    /// Gets the current ServiceClient instance for advanced operations.
    /// </summary>
    /// <returns>The connected ServiceClient.</returns>
    /// <exception cref="InvalidOperationException">Thrown if not connected.</exception>
    ServiceClient GetServiceClient();

    /// <summary>
    /// Gets the organization name from the connected environment.
    /// </summary>
    /// <returns>The organization friendly name.</returns>
    string GetOrganizationName();

    /// <summary>
    /// Gets the environment URL from the connected environment.
    /// </summary>
    /// <returns>The environment URL.</returns>
    string GetEnvironmentUrl();

    /// <summary>
    /// Validates that the client is ready and connected.
    /// </summary>
    /// <returns>True if connected and ready, false otherwise.</returns>
    bool IsConnected();

    /// <summary>
    /// Retrieves records from a table with optional FetchXML filter.
    /// </summary>
    /// <param name="entityName">The logical name of the table.</param>
    /// <param name="fetchXml">Optional FetchXML query. If null, retrieves all records.</param>
    /// <returns>Collection of entities matching the query.</returns>
    EntityCollection RetrieveRecords(string entityName, string? fetchXml = null);
}
