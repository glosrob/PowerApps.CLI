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

    // Maps solutioncomponent.componenttype (int) to the msdyn_solutioncomponentname string
    // required by the msdyn_componentlayer virtual entity. Both filters must be provided.
    // Values match the ComponentType enum names used by the Dataverse SDK (PascalCase).
    private static readonly Dictionary<int, string> ComponentLayerTypeNames = new()
    {
        [1]   = "Entity",
        [2]   = "Attribute",
        [3]   = "Relationship",
        [9]   = "OptionSet",
        [10]  = "OptionSetValue",
        [11]  = "PluginAssembly",
        [12]  = "PluginType",
        [13]  = "SdkMessage",
        [14]  = "SdkMessageFilter",
        [16]  = "ServiceEndpoint",
        [17]  = "MessageProcessingStep",
        [18]  = "MessageProcessingStepImage",
        [24]  = "RibbonCustomization",
        [25]  = "RibbonCommand",
        [26]  = "RibbonContextGroup",
        [29]  = "Workflow",
        [33]  = "SystemForm",
        [36]  = "AttributeMap",
        [47]  = "RibbonTabToCommandMap",
        [48]  = "RibbonDiff",
        [59]  = "SavedQueryVisualization",
        [60]  = "SystemForm",
        [61]  = "WebResource",
        [62]  = "SiteMap",
        [63]  = "ConnectionRole",
        [65]  = "HierarchyRule",
        [66]  = "CustomControl",
        [70]  = "FieldSecurityProfile",
        [71]  = "FieldPermission",
        [90]  = "PluginAssembly",
        [91]  = "PluginType",
        [92]  = "SDKMessageProcessingStep",
        [93]  = "SDKMessageProcessingStepImage",
        [95]  = "ServiceEndpoint",
        [418] = "msdyn_dataflow",
    };

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

    public EntityCollection RetrieveDuplicateRules(List<string> solutions)
    {
        var query = new QueryExpression("duplicaterule")
        {
            ColumnSet = new ColumnSet("duplicateruleid", "name", "statecode", "statuscode"),
            Criteria = new FilterExpression(LogicalOperator.And)
        };

        // Filter by solutions if specified
        if (solutions.Any())
        {
            foreach (var solution in solutions)
            {
                var componentLink = query.AddLink("solutioncomponent", "duplicateruleid", "objectid");
                var solutionLink = componentLink.AddLink("solution", "solutionid", "solutionid");
                solutionLink.LinkCriteria.AddCondition("uniquename", ConditionOperator.Equal, solution);
            }
        }

        return _serviceClient.RetrieveMultiple(query);
    }

    public void ActivateDuplicateRule(Guid ruleId)
    {
        var request = new PublishDuplicateRuleRequest
        {
            DuplicateRuleId = ruleId
        };
        _serviceClient.Execute(request);
    }

    public void DeactivateDuplicateRule(Guid ruleId)
    {
        var request = new SetStateRequest
        {
            EntityMoniker = new EntityReference("duplicaterule", ruleId),
            State = new OptionSetValue(0), // Inactive
            Status = new OptionSetValue(0)  // Unpublished
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

    public ExecuteMultipleResponse ExecuteMultiple(OrganizationRequestCollection requests, bool continueOnError)
    {
        if (requests == null)
        {
            throw new ArgumentNullException(nameof(requests));
        }

        var batch = new ExecuteMultipleRequest
        {
            Requests = requests,
            Settings = new ExecuteMultipleSettings
            {
                ContinueOnError = continueOnError,
                ReturnResponses = true
            }
        };
        return (ExecuteMultipleResponse)_serviceClient.Execute(batch);
    }

    public EntityMetadata GetEntityMetadata(string entityLogicalName)
    {
        if (string.IsNullOrWhiteSpace(entityLogicalName))
        {
            throw new ArgumentException("Entity logical name must be provided.", nameof(entityLogicalName));
        }

        var request = new RetrieveEntityRequest
        {
            LogicalName = entityLogicalName,
            EntityFilters = EntityFilters.Attributes,
            RetrieveAsIfPublished = false
        };
        var response = (RetrieveEntityResponse)_serviceClient.Execute(request);
        return response.EntityMetadata;
    }

    public ManyToManyRelationshipMetadata GetManyToManyRelationshipMetadata(string relationshipName)
    {
        if (string.IsNullOrWhiteSpace(relationshipName))
        {
            throw new ArgumentException("Relationship name must be provided.", nameof(relationshipName));
        }

        var request = new RetrieveRelationshipRequest
        {
            Name = relationshipName
        };
        var response = (RetrieveRelationshipResponse)_serviceClient.Execute(request);
        return (ManyToManyRelationshipMetadata)response.RelationshipMetadata;
    }

    public async Task<EntityCollection> GetSolutionComponentLayersAsync(string solutionName, Action<int, int, int>? batchProgress = null, Action<string>? phaseLog = null)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Phase 1: get component object IDs and type names from solutioncomponent.
        // msdyn_componentlayer requires BOTH msdyn_componentid AND msdyn_solutioncomponentname
        // to return results — the type name acts as a routing key for this virtual entity.
        var componentQuery = new QueryExpression("solutioncomponent")
        {
            ColumnSet = new ColumnSet("objectid", "componenttype"),
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

        var components = await Task.Run(() => _serviceClient.RetrieveMultiple(componentQuery));
        var componentList = components.Entities
            .Select(e => (
                Id: e.GetAttributeValue<Guid>("objectid"),
                TypeCode: e.GetAttributeValue<OptionSetValue>("componenttype")?.Value ?? 0,
                EntityLogicalName: (string?)null,
                EntityDisplayName: (string?)null
            ))
            .Where(c => c.Id != Guid.Empty && c.TypeCode != 0)
            .GroupBy(c => c.Id)
            .Select(g => g.First())
            .ToList();

        if (componentList.Count == 0)
            return new EntityCollection();

        phaseLog?.Invoke($"Phase 1 ({sw.ElapsedMilliseconds}ms): {componentList.Count} solution component(s) from solutioncomponent.");
        sw.Restart();

        // Phase 1b: Expand attribute (column) components.
        // Managed solutions don't store individual Attribute records in solutioncomponent —
        // only the entity itself (componenttype=1) is listed. We enumerate each entity's
        // attributes via metadata to get their MetadataIds and include them in the layer scan.
        var entityIds = componentList.Where(c => c.TypeCode == 1).Select(c => c.Id).ToList();
        var seenIds = new HashSet<Guid>(componentList.Select(c => c.Id));

        foreach (var entityId in entityIds)
        {
            try
            {
                var entityResponse = await Task.Run(() => (RetrieveEntityResponse)_serviceClient.Execute(
                    new RetrieveEntityRequest
                    {
                        MetadataId = entityId,
                        EntityFilters = EntityFilters.Attributes,
                        RetrieveAsIfPublished = false
                    }));

                var entityLogicalName = entityResponse.EntityMetadata.LogicalName;
                var entityDisplayName = entityResponse.EntityMetadata.DisplayName?.UserLocalizedLabel?.Label ?? entityLogicalName;
                foreach (var attr in entityResponse.EntityMetadata.Attributes)
                {
                    // Only expand custom attributes. Non-custom (standard/Microsoft) attributes
                    // are not solution components and may have layers from unrelated managed
                    // solutions, producing false positives. Any non-custom attribute that IS
                    // explicitly in the solution will already be captured by Phase 1.
                    if (attr.IsCustomAttribute != true) continue;

                    if (attr.MetadataId.HasValue && seenIds.Add(attr.MetadataId.Value))
                        componentList.Add((attr.MetadataId.Value, 2, entityLogicalName, entityDisplayName)); // 2 = Attribute
                }
            }
            catch
            {
                // Skip if entity metadata cannot be retrieved
            }
        }

        phaseLog?.Invoke($"Phase 1b ({sw.ElapsedMilliseconds}ms): expanded to {componentList.Count} component(s) after attribute enumeration.");
        sw.Restart();

        // Phase 2: batch individual msdyn_componentlayer queries into ExecuteMultiple calls.
        // msdyn_componentlayer requires exactly one msdyn_componentid per query (IN clauses
        // are silently ignored by the virtual entity provider), so we pack batchSize individual
        // RetrieveMultipleRequests into each ExecuteMultipleRequest. This cuts HTTP round-trips
        // from ~6,867 to ~35 while preserving the per-component query semantics.
        const int batchSize = 200;
        const int maxConcurrency = 10;
        var layerBag = new System.Collections.Concurrent.ConcurrentBag<Entity>();
        var semaphore = new SemaphoreSlim(maxConcurrency);
        var completed = 0;
        var total = componentList.Count;

        // Pre-count unmapped components (no type-name mapping) so progress reaches 100%.
        var unmappedCount = componentList.Count(c => !ComponentLayerTypeNames.ContainsKey(c.TypeCode));
        Interlocked.Add(ref completed, unmappedCount);

        // Build one RetrieveMultipleRequest per known component, then chunk into batches.
        var componentRequests = componentList
            .Where(c => ComponentLayerTypeNames.ContainsKey(c.TypeCode))
            .Select(c => (
                Component: c,
                Request: (OrganizationRequest)new RetrieveMultipleRequest
                {
                    Query = new QueryExpression("msdyn_componentlayer")
                    {
                        NoLock = true,
                        ColumnSet = new ColumnSet(true),
                        Criteria = new FilterExpression
                        {
                            Conditions =
                            {
                                new ConditionExpression("msdyn_solutioncomponentname", ConditionOperator.Equal, ComponentLayerTypeNames[c.TypeCode]),
                                new ConditionExpression("msdyn_componentid", ConditionOperator.Equal, c.Id),
                            }
                        }
                    }
                }
            ))
            .ToList();

        var batches = componentRequests.Chunk(batchSize).ToList();

        var tasks = batches.Select(async batch =>
        {
            await semaphore.WaitAsync();
            try
            {
                var requests = new OrganizationRequestCollection();
                foreach (var item in batch)
                    requests.Add(item.Request);

                var multipleResponse = (ExecuteMultipleResponse)await Task.Run(() =>
                    _serviceClient.Execute(new ExecuteMultipleRequest
                    {
                        Requests = requests,
                        Settings = new ExecuteMultipleSettings { ContinueOnError = true, ReturnResponses = true }
                    }));

                foreach (var responseItem in multipleResponse.Responses)
                {
                    if (responseItem.Fault != null) continue;
                    var component = batch[responseItem.RequestIndex].Component;
                    var entityCollection = ((RetrieveMultipleResponse)responseItem.Response).EntityCollection;

                    foreach (var entity in entityCollection.Entities)
                    {
                        // Stamp the parent entity info for attribute components so the service
                        // can surface it in the report without additional API calls.
                        if (component.EntityLogicalName != null)
                            entity["_entityname"] = component.EntityLogicalName;
                        if (component.EntityDisplayName != null)
                            entity["_entitydisplayname"] = component.EntityDisplayName;
                        layerBag.Add(entity);
                    }
                }

                var newCompleted = Interlocked.Add(ref completed, batch.Length);
                batchProgress?.Invoke(total, newCompleted, total);
            }
            finally
            {
                semaphore.Release();
            }
        }).ToList();

        await Task.WhenAll(tasks);

        phaseLog?.Invoke($"Phase 2 ({sw.ElapsedMilliseconds}ms): {layerBag.Count} layer record(s) from {batches.Count} ExecuteMultiple batch(es) across {total} component(s).");

        var allLayers = new EntityCollection();
        allLayers.Entities.AddRange(layerBag);
        return allLayers;
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
