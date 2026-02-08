using Microsoft.Crm.Sdk.Messages;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Discovery;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;

namespace PowerApps.CLI.Infrastructure;

/// <summary>
/// Handles authentication and connection to Dataverse environments.
/// </summary>
public class DataverseClient : IDataverseClient
{
    private const string DefaultAppId = "51f81489-12ee-4a9e-aaae-a2591f45987d"; // Microsoft-provided app ID for OAuth
    private const string DefaultRedirectUri = "http://localhost";

    private string _url { get; set; } = string.Empty;
    private string _clientId {get;set; } = string.Empty;
    private string _clientSecret {get;set;} = string.Empty;
    private string _connectionString {get;set;} = string.Empty;
    private readonly ServiceClient _serviceClient;

    public DataverseClient(string url, string? clientId = null, string? clientSecret = null, string? connectionString = null)
    {
        _url = url;
        _clientId = clientId ?? string.Empty;
        _clientSecret = clientSecret ?? string.Empty;
        _connectionString = connectionString ?? string.Empty;
        _serviceClient = Connect(_url, _clientId, _clientSecret, _connectionString);
    }

    public string GetOrganizationName()
    {
        return _serviceClient.ConnectedOrgFriendlyName ?? string.Empty;
    }

    public string GetEnvironmentUrl()
    {
        if (_serviceClient.ConnectedOrgPublishedEndpoints.ContainsKey(EndpointType.OrganizationService))
        {
            return _serviceClient.ConnectedOrgPublishedEndpoints[EndpointType.OrganizationService];
        }
        return _serviceClient.ConnectedOrgUriActual?.ToString() ?? string.Empty;
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
            return _serviceClient.RetrieveMultiple(new FetchExpression(fetchXml));
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
                pageResults = _serviceClient.RetrieveMultiple(query);
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

    public EntityCollection RetrieveMultiple(QueryExpression query)
    {
        if (query == null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        return _serviceClient.RetrieveMultiple(query);
    }

    public OrganizationResponse Execute(OrganizationRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        return _serviceClient.Execute(request);
    }

    public async Task<Dictionary<string, List<string>>> GetAllEntityMetadataAsync()
    {
        var request = new RetrieveAllEntitiesRequest
        {
            EntityFilters = EntityFilters.Entity,
            RetrieveAsIfPublished = false
        };

        var response = await Task.Run(() => (RetrieveAllEntitiesResponse)_serviceClient.Execute(request));

        var entities = new Dictionary<string, List<string>>();
        foreach (var entity in response.EntityMetadata)
        {
            if (!string.IsNullOrEmpty(entity.LogicalName))
            {
                entities[entity.LogicalName] = new List<string>();
            }
        }

        return entities;
    }

    public async Task<Dictionary<string, List<string>>> GetEntitiesFromSolutionAsync(string solutionName)
    {
        var entitySolutions = new Dictionary<string, List<string>>();

        // Query solution components for entities
        var query = new QueryExpression("solutioncomponent")
        {
            ColumnSet = new ColumnSet("objectid", "componenttype"),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("componenttype", ConditionOperator.Equal, 1) // 1 = Entity
                }
            },
            LinkEntities =
            {
                new LinkEntity
                {
                    LinkFromEntityName = "solutioncomponent",
                    LinkFromAttributeName = "solutionid",
                    LinkToEntityName = "solution",
                    LinkToAttributeName = "solutionid",
                    LinkCriteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("uniquename", ConditionOperator.Equal, solutionName)
                        }
                    }
                }
            }
        };

        var results = await Task.Run(() => _serviceClient.RetrieveMultiple(query));

        foreach (var component in results.Entities)
        {
            var objectId = component.GetAttributeValue<Guid>("objectid");
            
            // Get entity metadata to find logical name
            var metadataRequest = new RetrieveEntityRequest
            {
                MetadataId = objectId,
                EntityFilters = EntityFilters.Entity
            };

            try
            {
                var response = await Task.Run(() => 
                    (RetrieveEntityResponse)_serviceClient.Execute(metadataRequest));
                
                var logicalName = response.EntityMetadata.LogicalName;

                if (!entitySolutions.ContainsKey(logicalName))
                {
                    entitySolutions[logicalName] = new List<string>();
                }

                if (!entitySolutions[logicalName].Contains(solutionName))
                {
                    entitySolutions[logicalName].Add(solutionName);
                }
            }
            catch
            {
                // Skip if entity cannot be retrieved
                continue;
            }
        }

        return entitySolutions;
    }

    public async Task<EntityMetadata?> GetEntityMetadataAsync(string entityLogicalName, EntityFilters filters)
    {
        try
        {
            var request = new RetrieveEntityRequest
            {
                LogicalName = entityLogicalName,
                EntityFilters = filters,
                RetrieveAsIfPublished = false
            };

            var response = await Task.Run(() => (RetrieveEntityResponse)_serviceClient.Execute(request));
            return response.EntityMetadata;
        }
        catch
        {
            return null;
        }
    }

    public EntityCollection RetrieveProcesses(List<string> solutions)
    {
        var query = new QueryExpression("workflow")
        {
            ColumnSet = new ColumnSet("workflowid", "name", "category", "statecode", "statuscode"),
            Criteria = new FilterExpression(LogicalOperator.And)
        };

        // Filter by category: Workflow(0), BusinessRule(2), Action(3), BusinessProcessFlow(4), CloudFlow(5)
        query.Criteria.AddCondition("category", ConditionOperator.In, 0, 2, 3, 4, 5);

        // Filter by solutions if specified
        if (solutions.Any())
        {
            foreach (var solution in solutions)
            {
                var componentLink = query.AddLink("solutioncomponent", "workflowid", "objectid");
                var solutionLink = componentLink.AddLink("solution", "solutionid", "solutionid");
                solutionLink.LinkCriteria.AddCondition("uniquename", ConditionOperator.Equal, solution);
            }
        }

        return _serviceClient.RetrieveMultiple(query);
    }

    public void ActivateProcess(Guid processId)
    {
        var request = new SetStateRequest
        {
            EntityMoniker = new EntityReference("workflow", processId),
            State = new OptionSetValue(1), // Active
            Status = new OptionSetValue(2)  // Activated
        };
        _serviceClient.Execute(request);
    }

    public void DeactivateProcess(Guid processId)
    {
        var request = new SetStateRequest
        {
            EntityMoniker = new EntityReference("workflow", processId),
            State = new OptionSetValue(0), // Inactive
            Status = new OptionSetValue(1)  // Draft
        };
        _serviceClient.Execute(request);
    }

    public EntityCollection RetrieveRecordsByFetchXml(string fetchXml)
    {
        if (string.IsNullOrWhiteSpace(fetchXml))
        {
            throw new ArgumentException("FetchXML query must be provided.", nameof(fetchXml));
        }

        return _serviceClient.RetrieveMultiple(new FetchExpression(fetchXml));
    }

    private static ServiceClient Connect(string url, string? clientId = null, string? clientSecret = null, string? connectionString = null)
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

        return serviceClient;
    }

}
