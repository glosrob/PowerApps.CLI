using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Discovery;
using Microsoft.Xrm.Sdk.Query;

namespace PowerApps.CLI.Infrastructure;

/// <summary>
/// Handles authentication and connection to Dataverse environments.
/// </summary>
public class DataverseClient : IDataverseClient
{
    private const string DefaultAppId = "51f81489-12ee-4a9e-aaae-a2591f45987d"; // Microsoft-provided app ID for OAuth
    private const string DefaultRedirectUri = "http://localhost";

    private ServiceClient? _serviceClient;

    /// <summary>
    /// Gets the connected ServiceClient, throwing if not connected.
    /// </summary>
    private ServiceClient ServiceClient =>
        _serviceClient ?? throw new InvalidOperationException("Not connected. Call ConnectAsync first.");

    /// <summary>
    /// Gets the current ServiceClient instance for advanced operations.
    /// </summary>
    /// <returns>The connected ServiceClient.</returns>
    /// <exception cref="InvalidOperationException">Thrown if not connected.</exception>
    public ServiceClient GetServiceClient() => ServiceClient;

    public async Task<ServiceClient> ConnectAsync(string url, string? clientId = null, string? clientSecret = null, string? connectionString = null)
    {
        if (string.IsNullOrWhiteSpace(url) && string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Either URL or connection string must be provided.");
        }

        // Check for environment variables if options not provided
        clientId ??= Environment.GetEnvironmentVariable("DATAVERSE_CLIENT_ID");
        clientSecret ??= Environment.GetEnvironmentVariable("DATAVERSE_CLIENT_SECRET");

        ServiceClient serviceClient;

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            // Use provided connection string
            serviceClient = new ServiceClient(connectionString);
        }
        else if (!string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(clientSecret))
        {
            // Use client credentials (service principal)
            var connString = $"AuthType=ClientSecret;Url={url};ClientId={clientId};ClientSecret={clientSecret}";
            serviceClient = new ServiceClient(connString);
        }
        else
        {
            // Use interactive authentication (OAuth with browser)
            var connString = $"AuthType=OAuth;Url={url};AppId={DefaultAppId};RedirectUri={DefaultRedirectUri};LoginPrompt=Auto";
            serviceClient = new ServiceClient(connString);
        }

        // Validate connection
        if (!serviceClient.IsReady)
        {
            var errorMessage = $"Failed to connect to Dataverse environment.";
            if (!string.IsNullOrEmpty(serviceClient.LastError))
            {
                errorMessage += $" Error: {serviceClient.LastError}";
            }
            if (serviceClient.LastException != null)
            {
                errorMessage += $" Exception: {serviceClient.LastException.Message}";
            }
            throw new InvalidOperationException(errorMessage, serviceClient.LastException);
        }

        _serviceClient = serviceClient;
        return await Task.FromResult(serviceClient);
    }

    public string GetOrganizationName()
    {
        return ServiceClient.ConnectedOrgFriendlyName ?? string.Empty;
    }

    public string GetEnvironmentUrl()
    {
        if (ServiceClient.ConnectedOrgPublishedEndpoints.ContainsKey(EndpointType.OrganizationService))
        {
            return ServiceClient.ConnectedOrgPublishedEndpoints[EndpointType.OrganizationService];
        }

        return ServiceClient.ConnectedOrgUriActual?.ToString() ?? string.Empty;
    }

    public bool IsConnected()
    {
        return _serviceClient?.IsReady ?? false;
    }

    public EntityCollection RetrieveRecords(string entityName, string? fetchXml = null)
    {
        if (string.IsNullOrWhiteSpace(entityName))
        {
            throw new ArgumentException("Entity name must be provided.", nameof(entityName));
        }

        if (!string.IsNullOrWhiteSpace(fetchXml))
        {
            // Use provided FetchXML
            return ServiceClient.RetrieveMultiple(new FetchExpression(fetchXml));
        }
        else
        {
            // Retrieve all records with QueryExpression
            var query = new QueryExpression(entityName)
            {
                ColumnSet = new ColumnSet(true), // Get all columns
                PageInfo = new PagingInfo
                {
                    Count = 5000,
                    PageNumber = 1
                }
            };

            var results = new EntityCollection();
            EntityCollection pageResults;

            do
            {
                pageResults = ServiceClient.RetrieveMultiple(query);
                results.Entities.AddRange(pageResults.Entities);

                if (pageResults.MoreRecords)
                {
                    query.PageInfo.PageNumber++;
                    query.PageInfo.PagingCookie = pageResults.PagingCookie;
                }
            } while (pageResults.MoreRecords);

            return results;
        }
    }
}
