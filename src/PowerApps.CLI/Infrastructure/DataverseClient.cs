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
public class DataverseClient : IDataverseClient, IDisposable
{
    // Properties

    private const string DefaultAppId = "51f81489-12ee-4a9e-aaae-a2591f45987d"; // Microsoft-provided app ID for OAuth
    private const string DefaultRedirectUri = "http://localhost";

    // Maps solutioncomponent.componenttype (int) to the msdyn_solutioncomponentname string
    // required by the msdyn_componentlayer virtual entity. Both filters must be provided.
    // Values match the ComponentType enum names used by the Dataverse SDK (PascalCase).
    // Modern platform types (connection references, cloud flows, etc.) are supplemented at
    // runtime from solutioncomponentdefinition — see GetModernComponentTypeNamesAsync.
    private static readonly Dictionary<int, string> _componentLayerTypeNames = new()
    {
        [1]   = "Entity",
        [2]   = "Attribute",
        [3]   = "Relationship",
        [4]   = "AttributePicklistValue",
        [5]   = "AttributeLookupValue",
        [6]   = "ViewAttribute",
        [7]   = "LocalizedLabel",
        [8]   = "RelationshipExtraCondition",
        [9]   = "OptionSet",
        [10]  = "EntityRelationship",
        [11]  = "EntityRelationshipRole",
        [12]  = "EntityRelationshipRelationships",
        [13]  = "ManagedProperty",
        [14]  = "EntityKey",
        [16]  = "Privilege",
        [17]  = "PrivilegeObjectTypeCode",
        [18]  = "Index",
        [20]  = "Role",
        [21]  = "RolePrivilege",
        [22]  = "DisplayString",
        [23]  = "DisplayStringMap",
        [24]  = "Form",
        [25]  = "Organization",
        [26]  = "SavedQuery",
        [29]  = "Workflow",
        [31]  = "Report",
        [32]  = "ReportEntity",
        [33]  = "ReportCategory",
        [34]  = "ReportVisibility",
        [35]  = "Attachment",
        [36]  = "EmailTemplate",
        [37]  = "ContractTemplate",
        [38]  = "KBArticleTemplate",
        [39]  = "MailMergeTemplate",
        [44]  = "DuplicateRule",
        [45]  = "DuplicateRuleCondition",
        [46]  = "EntityMap",
        [47]  = "AttributeMap",
        [48]  = "RibbonCommand",
        [49]  = "RibbonContextGroup",
        [50]  = "RibbonCustomization",
        [52]  = "RibbonRule",
        [53]  = "RibbonTabToCommandMap",
        [55]  = "RibbonDiff",
        [59]  = "SavedQueryVisualization",
        [60]  = "SystemForm",
        [61]  = "WebResource",
        [62]  = "SiteMap",
        [63]  = "ConnectionRole",
        [65]  = "HierarchyRule",
        [66]  = "CustomControl",
        [68]  = "CustomControlDefaultConfig",
        [70]  = "FieldSecurityProfile",
        [71]  = "FieldPermission",
        [80]  = "AppModule",
        [90]  = "PluginType",
        [91]  = "PluginAssembly",
        [92]  = "SdkMessageProcessingStep",
        [93]  = "SdkMessageProcessingStepImage",
        [95]  = "ServiceEndpoint",
        [150] = "RoutingRule",
        [151] = "RoutingRuleItem",
        [152] = "SLA",
        [153] = "SLAItem",
        [154] = "ConvertRule",
        [155] = "ConvertRuleItem",
        [161] = "MobileOfflineProfile",
        [162] = "MobileOfflineProfileItem",
        [165] = "SimilarityRule",
        [166] = "DataSourceMapping",
        [300] = "CanvasApp",
        [380] = "EnvironmentVariableDefinition",
        [381] = "EnvironmentVariableValue",
        [418] = "msdyn_dataflow",               // Special case: routing key differs from enum name
    };

    // Component type codes for solutioncomponent.componenttype — a subset of the values in
    // _componentLayerTypeNames that are also used directly in query conditions and component list building.
    private const int ComponentTypeEntity = 1;
    private const int ComponentTypeAttribute = 2;
    private const int ComponentTypeSavedQuery = 26;
    private const int ComponentTypeSavedQueryVisualization = 59;
    private const int ComponentTypeSystemForm = 60;

    // Workflow category codes (workflow.category option set).
    private const int WorkflowCategoryWorkflow = 0;
    private const int WorkflowCategoryBusinessRule = 2;
    private const int WorkflowCategoryAction = 3;
    private const int WorkflowCategoryBusinessProcessFlow = 4;
    private const int WorkflowCategoryCloudFlow = 5;

    // Workflow (process) state and status codes.
    private const int WorkflowStateActive = 1;
    private const int WorkflowStatusActivated = 2;
    private const int WorkflowStateInactive = 0;
    private const int WorkflowStatusDraft = 1;

    // Duplicate rule state and status codes.
    private const int DuplicateRuleStateInactive = 0;
    private const int DuplicateRuleStatusUnpublished = 0;

    // Plugin step state and status codes (inverted relative to most entities: 0 = Enabled, 1 = Disabled).
    private const int PluginStepStateEnabled = 0;
    private const int PluginStepStatusEnabled = 1;
    private const int PluginStepStateDisabled = 1;
    private const int PluginStepStatusDisabled = 2;

    private string _url { get; set; } = string.Empty;
    private string _clientId {get;set; } = string.Empty;
    private string _clientSecret {get;set;} = string.Empty;
    private string _connectionString {get;set;} = string.Empty;
    private readonly IOrganizationService _orgService;
    private readonly ServiceClient? _serviceClient; // Narrow reference for org-info members not on IOrganizationService
    private bool _disposed;

    // Constructors

    public DataverseClient(string url, string? clientId = null, string? clientSecret = null, string? connectionString = null)
    {
        _url = url;
        _clientId = clientId ?? string.Empty;
        _clientSecret = clientSecret ?? string.Empty;
        _connectionString = connectionString ?? string.Empty;
        var sc = Connect(_url, _clientId, _clientSecret, _connectionString);
        _serviceClient = sc;
        _orgService = sc;
    }

    // For unit testing only — ServiceClient-specific members (GetOrganizationName, GetEnvironmentUrl) are unavailable.
    internal DataverseClient(IOrganizationService orgService)
    {
        _orgService = orgService;
    }

    // Methods

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _serviceClient?.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    public string GetOrganizationName()
    {
        if (_serviceClient is null)
        {
            throw new NotSupportedException("GetOrganizationName is not available in test context.");
        }
        return _serviceClient.ConnectedOrgFriendlyName ?? string.Empty;
    }

    public string GetEnvironmentUrl()
    {
        if (_serviceClient is null)
        {
            throw new NotSupportedException("GetEnvironmentUrl is not available in test context.");
        }
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
            return _orgService.RetrieveMultiple(new FetchExpression(fetchXml));
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
                pageResults = _orgService.RetrieveMultiple(query);
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
        ArgumentNullException.ThrowIfNull(query);

        return _orgService.RetrieveMultiple(query);
    }

    public OrganizationResponse Execute(OrganizationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _orgService.Execute(request);
    }

    public async Task<Dictionary<string, List<string>>> GetAllEntityMetadataAsync()
    {
        var request = new RetrieveAllEntitiesRequest
        {
            EntityFilters = EntityFilters.Entity,
            RetrieveAsIfPublished = false
        };

        var response = await Task.Run(() => (RetrieveAllEntitiesResponse)_orgService.Execute(request));

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
                    new ConditionExpression("componenttype", ConditionOperator.Equal, ComponentTypeEntity)
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

        var results = await Task.Run(() => _orgService.RetrieveMultiple(query));

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
                    (RetrieveEntityResponse)_orgService.Execute(metadataRequest));

                var logicalName = response.EntityMetadata.LogicalName;

                if (!entitySolutions.TryGetValue(logicalName, out List<string>? value))
                {
                    value = new List<string>();
                    entitySolutions[logicalName] = value;
                }

                if (!value.Contains(solutionName))
                {
                    value.Add(solutionName);
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

            var response = await Task.Run(() => (RetrieveEntityResponse)_orgService.Execute(request));
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

        query.Criteria.AddCondition("category", ConditionOperator.In,
            WorkflowCategoryWorkflow, WorkflowCategoryBusinessRule, WorkflowCategoryAction,
            WorkflowCategoryBusinessProcessFlow, WorkflowCategoryCloudFlow);

        // Filter by solutions if specified
        if (solutions.Count != 0)
        {
            foreach (var solution in solutions)
            {
                var componentLink = query.AddLink("solutioncomponent", "workflowid", "objectid");
                var solutionLink = componentLink.AddLink("solution", "solutionid", "solutionid");
                solutionLink.LinkCriteria.AddCondition("uniquename", ConditionOperator.Equal, solution);
            }
        }

        return _orgService.RetrieveMultiple(query);
    }

    public void ActivateProcess(Guid processId)
    {
        var request = new SetStateRequest
        {
            EntityMoniker = new EntityReference("workflow", processId),
            State = new OptionSetValue(WorkflowStateActive),
            Status = new OptionSetValue(WorkflowStatusActivated)
        };
        _orgService.Execute(request);
    }

    public void DeactivateProcess(Guid processId)
    {
        var request = new SetStateRequest
        {
            EntityMoniker = new EntityReference("workflow", processId),
            State = new OptionSetValue(WorkflowStateInactive),
            Status = new OptionSetValue(WorkflowStatusDraft)
        };
        _orgService.Execute(request);
    }

    public EntityCollection RetrieveDuplicateRules(List<string> solutions)
    {
        var query = new QueryExpression("duplicaterule")
        {
            ColumnSet = new ColumnSet("duplicateruleid", "name", "statecode", "statuscode"),
            Criteria = new FilterExpression(LogicalOperator.And)
        };

        // Filter by solutions if specified
        if (solutions.Count != 0)
        {
            foreach (var solution in solutions)
            {
                var componentLink = query.AddLink("solutioncomponent", "duplicateruleid", "objectid");
                var solutionLink = componentLink.AddLink("solution", "solutionid", "solutionid");
                solutionLink.LinkCriteria.AddCondition("uniquename", ConditionOperator.Equal, solution);
            }
        }

        return _orgService.RetrieveMultiple(query);
    }

    public void ActivateDuplicateRule(Guid ruleId)
    {
        var request = new PublishDuplicateRuleRequest
        {
            DuplicateRuleId = ruleId
        };
        _orgService.Execute(request);
    }

    public void DeactivateDuplicateRule(Guid ruleId)
    {
        var request = new SetStateRequest
        {
            EntityMoniker = new EntityReference("duplicaterule", ruleId),
            State = new OptionSetValue(DuplicateRuleStateInactive),
            Status = new OptionSetValue(DuplicateRuleStatusUnpublished)
        };
        _orgService.Execute(request);
    }

    public EntityCollection RetrievePluginSteps(List<string> solutions)
    {
        var query = new QueryExpression("sdkmessageprocessingstep")
        {
            ColumnSet = new ColumnSet("sdkmessageprocessingstepid", "name", "statecode"),
            Criteria = new FilterExpression(LogicalOperator.And)
        };

        if (solutions.Count != 0)
        {
            foreach (var solution in solutions)
            {
                var componentLink = query.AddLink("solutioncomponent", "sdkmessageprocessingstepid", "objectid");
                var solutionLink = componentLink.AddLink("solution", "solutionid", "solutionid");
                solutionLink.LinkCriteria.AddCondition("uniquename", ConditionOperator.Equal, solution);
            }
        }

        return _orgService.RetrieveMultiple(query);
    }

    public void EnablePluginStep(Guid stepId)
    {
        var request = new SetStateRequest
        {
            EntityMoniker = new EntityReference("sdkmessageprocessingstep", stepId),
            State = new OptionSetValue(PluginStepStateEnabled),
            Status = new OptionSetValue(PluginStepStatusEnabled)
        };
        _orgService.Execute(request);
    }

    public void DisablePluginStep(Guid stepId)
    {
        var request = new SetStateRequest
        {
            EntityMoniker = new EntityReference("sdkmessageprocessingstep", stepId),
            State = new OptionSetValue(PluginStepStateDisabled),
            Status = new OptionSetValue(PluginStepStatusDisabled)
        };
        _orgService.Execute(request);
    }

    public EntityCollection RetrieveRecordsByFetchXml(string fetchXml)
    {
        if (string.IsNullOrWhiteSpace(fetchXml))
        {
            throw new ArgumentException("FetchXML query must be provided.", nameof(fetchXml));
        }

        return _orgService.RetrieveMultiple(new FetchExpression(fetchXml));
    }

    public ExecuteMultipleResponse ExecuteMultiple(OrganizationRequestCollection requests, bool continueOnError)
    {
        ArgumentNullException.ThrowIfNull(requests);

        var batch = new ExecuteMultipleRequest
        {
            Requests = requests,
            Settings = new ExecuteMultipleSettings
            {
                ContinueOnError = continueOnError,
                ReturnResponses = true
            }
        };
        return (ExecuteMultipleResponse)_orgService.Execute(batch);
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
        var response = (RetrieveEntityResponse)_orgService.Execute(request);
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
        var response = (RetrieveRelationshipResponse)_orgService.Execute(request);
        return (ManyToManyRelationshipMetadata)response.RelationshipMetadata;
    }

    public async Task<EntityCollection> GetSolutionComponentLayersAsync(string solutionName, Action<int, int, int>? batchProgress = null, Action<string>? phaseLog = null)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Phase 1: get component object IDs and type codes from solutioncomponent.
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

        var components = await Task.Run(() => _orgService.RetrieveMultiple(componentQuery));
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
        {
            return new EntityCollection();
        }

        phaseLog?.Invoke($"Phase 1 ({sw.ElapsedMilliseconds}ms): {componentList.Count} solution component(s) from solutioncomponent.");
        sw.Restart();

        // Merge the static classic-type map with modern types from solutioncomponentdefinition.
        // The static dictionary covers types defined in the SDK ComponentType enum; the
        // definition table adds platform-specific types (connection references, cloud flows, etc.)
        // whose integer codes only exist in the org's own option set metadata.
        var typeNames = new Dictionary<int, string>(_componentLayerTypeNames);
        foreach (var kvp in await GetModernComponentTypeNamesAsync(phaseLog))
        {
            if (!typeNames.ContainsKey(kvp.Key))
            {
                typeNames[kvp.Key] = kvp.Value;
            }
        }

        // Phase 1b: Expand attribute (column) components and capture entity info for Phase 1c.
        // Entity rows in solutioncomponent (type 1) implicitly include all their attributes —
        // we enumerate attribute metadata to get individual MetadataIds for the layer scan.
        var entityIds = componentList.Where(c => c.TypeCode == ComponentTypeEntity).Select(c => c.Id).ToList();
        var seenIds = new HashSet<Guid>(componentList.Select(c => c.Id));
        var entityInfoById = new Dictionary<Guid, (string LogicalName, string DisplayName)>();

        foreach (var entityId in entityIds)
        {
            try
            {
                var entityResponse = await Task.Run(() => (RetrieveEntityResponse)_orgService.Execute(
                    new RetrieveEntityRequest
                    {
                        MetadataId = entityId,
                        EntityFilters = EntityFilters.Attributes,
                        RetrieveAsIfPublished = false
                    }));

                var entityLogicalName = entityResponse.EntityMetadata.LogicalName;
                var entityDisplayName = entityResponse.EntityMetadata.DisplayName?.UserLocalizedLabel?.Label ?? entityLogicalName;
                entityInfoById[entityId] = (entityLogicalName, entityDisplayName);

                foreach (var attr in entityResponse.EntityMetadata.Attributes)
                {
                    if (attr.MetadataId.HasValue && seenIds.Add(attr.MetadataId.Value))
                    {
                        componentList.Add((attr.MetadataId.Value, ComponentTypeAttribute, entityLogicalName, entityDisplayName));
                    }
                }
            }
            catch
            {
                // Skip if entity metadata cannot be retrieved
            }
        }

        phaseLog?.Invoke($"Phase 1b ({sw.ElapsedMilliseconds}ms): expanded to {componentList.Count} component(s) after attribute enumeration.");
        sw.Restart();

        // Phase 1c: Expand forms (type 60), views (type 26), and charts (type 59) for each entity.
        // These are implicit solution components when an entity belongs to a solution with
        // rootcomponentbehavior = 0 — they have no explicit solutioncomponent rows of their own.
        foreach (var (logicalName, displayName) in entityInfoById.Values)
        {
            var formResults = await Task.Run(() => _orgService.RetrieveMultiple(
                new QueryExpression("systemform")
                {
                    ColumnSet = new ColumnSet("formid"),
                    NoLock = true,
                    Criteria = new FilterExpression
                    {
                        Conditions = { new ConditionExpression("objecttypecode", ConditionOperator.Equal, logicalName) }
                    }
                }));
            foreach (var form in formResults.Entities)
            {
                if (seenIds.Add(form.Id))
                {
                    componentList.Add((form.Id, ComponentTypeSystemForm, logicalName, displayName));
                }
            }

            var viewResults = await Task.Run(() => _orgService.RetrieveMultiple(
                new QueryExpression("savedquery")
                {
                    ColumnSet = new ColumnSet("savedqueryid"),
                    NoLock = true,
                    Criteria = new FilterExpression
                    {
                        Conditions = { new ConditionExpression("returnedtypecode", ConditionOperator.Equal, logicalName) }
                    }
                }));
            foreach (var view in viewResults.Entities)
            {
                if (seenIds.Add(view.Id))
                {
                    componentList.Add((view.Id, ComponentTypeSavedQuery, logicalName, displayName));
                }
            }

            var chartResults = await Task.Run(() => _orgService.RetrieveMultiple(
                new QueryExpression("savedqueryvisualization")
                {
                    ColumnSet = new ColumnSet("savedqueryvisualizationid"),
                    NoLock = true,
                    Criteria = new FilterExpression
                    {
                        Conditions = { new ConditionExpression("primaryentitytypecode", ConditionOperator.Equal, logicalName) }
                    }
                }));
            foreach (var chart in chartResults.Entities)
            {
                if (seenIds.Add(chart.Id))
                {
                    componentList.Add((chart.Id, ComponentTypeSavedQueryVisualization, logicalName, displayName));
                }
            }
        }

        phaseLog?.Invoke($"Phase 1c ({sw.ElapsedMilliseconds}ms): expanded to {componentList.Count} component(s) after form/view/chart enumeration.");
        sw.Restart();

        // Phase 2: batch individual msdyn_componentlayer queries into ExecuteMultiple calls.
        // msdyn_componentlayer requires exactly one msdyn_componentid per query (IN clauses
        // are silently ignored by the virtual entity provider), so we pack batchSize individual
        // RetrieveMultipleRequests into each ExecuteMultipleRequest. This cuts HTTP round-trips
        // while preserving the per-component query semantics.
        const int batchSize = 200;
        const int maxConcurrency = 10;
        var layerBag = new System.Collections.Concurrent.ConcurrentBag<Entity>();
        var semaphore = new SemaphoreSlim(maxConcurrency);
        var completed = 0;
        var total = componentList.Count;

        var unmappedTypes = componentList
            .Where(c => !typeNames.ContainsKey(c.TypeCode))
            .Select(c => c.TypeCode)
            .Distinct()
            .OrderBy(t => t)
            .ToList();
        if (unmappedTypes.Count > 0)
        {
            phaseLog?.Invoke($"Warning: component type code(s) [{string.Join(", ", unmappedTypes)}] have no routing key and will be skipped.");
        }
        var unmappedCount = componentList.Count(c => !typeNames.ContainsKey(c.TypeCode));
        Interlocked.Add(ref completed, unmappedCount);

        // Build one RetrieveMultipleRequest per known component, then chunk into batches.
        var componentRequests = componentList
            .Where(c => typeNames.ContainsKey(c.TypeCode))
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
                                new ConditionExpression("msdyn_solutioncomponentname", ConditionOperator.Equal, typeNames[c.TypeCode]),
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
                {
                    requests.Add(item.Request);
                }

                var multipleResponse = (ExecuteMultipleResponse)await Task.Run(() =>
                    _orgService.Execute(new ExecuteMultipleRequest
                    {
                        Requests = requests,
                        Settings = new ExecuteMultipleSettings { ContinueOnError = true, ReturnResponses = true }
                    }));

                foreach (var responseItem in multipleResponse.Responses)
                {
                    if (responseItem.Fault != null)
                    {
                        continue;
                    }
                    var component = batch[responseItem.RequestIndex].Component;
                    var entityCollection = ((RetrieveMultipleResponse)responseItem.Response).EntityCollection;

                    foreach (var entity in entityCollection.Entities)
                    {
                        // Stamp the parent entity info for attribute/form/view/chart components
                        // so the service can surface it in the report without additional API calls.
                        if (component.EntityLogicalName != null)
                        {
                            entity["_entityname"] = component.EntityLogicalName;
                        }
                        if (component.EntityDisplayName != null)
                        {
                            entity["_entitydisplayname"] = component.EntityDisplayName;
                        }
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

    // Queries solutioncomponentdefinition to get the msdyn_solutioncomponentname routing key
    // for modern platform types (connection references, cloud flows, etc.) whose integer codes
    // are absent from the static ComponentLayerTypeNames dictionary.
    private async Task<Dictionary<int, string>> GetModernComponentTypeNamesAsync(Action<string>? phaseLog = null)
    {
        try
        {
            var results = await Task.Run(() => _orgService.RetrieveMultiple(
                new QueryExpression("solutioncomponentdefinition")
                {
                    ColumnSet = new ColumnSet("solutioncomponenttype", "name"),
                    NoLock = true
                }));

            var dict = new Dictionary<int, string>();
            foreach (var entity in results.Entities)
            {
                var typeCode = entity.GetAttributeValue<int>("solutioncomponenttype");
                var name = entity.GetAttributeValue<string>("name");
                if (typeCode > 0 && !string.IsNullOrEmpty(name))
                {
                    dict[typeCode] = name;
                }
            }
            return dict;
        }
        catch (Exception ex)
        {
            phaseLog?.Invoke($"Warning: solutioncomponentdefinition query failed ({ex.Message}); modern component types will fall back to static map only.");
            return new Dictionary<int, string>();
        }
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
