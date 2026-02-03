using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using PowerApps.CLI.Infrastructure;
using PowerApps.CLI.Models;

namespace PowerApps.CLI.Services;

public class SchemaExtractor : ISchemaExtractor
{
    private readonly IMetadataMapper _metadataMapper;
    private readonly IDataverseClient _dataverseClient;

    public SchemaExtractor(IMetadataMapper metadataMapper, IDataverseClient dataverseClient)
    {
        _metadataMapper = metadataMapper;
        _dataverseClient = dataverseClient;
    }

    public async Task<PowerAppsSchema> ExtractSchemaAsync(string? solutionNames = null)
    {
        var serviceClient = _dataverseClient.GetServiceClient();
        
        var schema = new PowerAppsSchema
        {
            ExtractedDate = DateTime.UtcNow,
            EnvironmentUrl = _dataverseClient.GetEnvironmentUrl(),
            OrganisationName = _dataverseClient.GetOrganizationName()
        };

        // Parse solution names
        var solutions = ParseSolutionNames(solutionNames);
        schema.SolutionNames = solutions.Count > 0 ? solutions : null;

        // Get entities from solutions or all entities
        var entityLogicalNames = solutions.Count > 0
            ? await GetEntitiesFromSolutionsAsync(serviceClient, solutions)
            : await GetAllEntityNamesAsync(serviceClient);

        // Retrieve entity metadata
        var entities = new Dictionary<string, EntitySchema>();
        foreach (var entityName in entityLogicalNames.Keys)
        {
            var entityMetadata = await RetrieveEntityMetadataAsync(serviceClient, entityName);
            if (entityMetadata != null)
            {
                var entity = _metadataMapper.MapEntity(entityMetadata);
                
                // Track which solutions this entity appears in
                entity.FoundInSolutions = entityLogicalNames[entityName];

                // Map attributes
                if (entityMetadata.Attributes != null)
                {
                    foreach (var attribute in entityMetadata.Attributes)
                    {
                        entity.Attributes.Add(_metadataMapper.MapAttribute(attribute));
                    }
                }

                entities[entityName] = entity;
            }
        }

        schema.Entities = entities.Values.ToList();

        // Retrieve relationships
        var relationships = await RetrieveRelationshipsAsync(serviceClient, entityLogicalNames.Keys.ToList());
        schema.Relationships = relationships;

        return schema;
    }

    private List<string> ParseSolutionNames(string? solutionNames)
    {
        if (string.IsNullOrWhiteSpace(solutionNames))
        {
            return new List<string>();
        }

        return solutionNames
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();
    }

    private async Task<Dictionary<string, List<string>>> GetEntitiesFromSolutionsAsync(
        ServiceClient serviceClient, 
        List<string> solutionNames)
    {
        var entitySolutions = new Dictionary<string, List<string>>();

        foreach (var solutionName in solutionNames)
        {
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

            var results = await Task.Run(() => serviceClient.RetrieveMultiple(query));

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
                        (RetrieveEntityResponse)serviceClient.Execute(metadataRequest));
                    
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
        }

        return entitySolutions;
    }

    private async Task<Dictionary<string, List<string>>> GetAllEntityNamesAsync(ServiceClient serviceClient)
    {
        var request = new RetrieveAllEntitiesRequest
        {
            EntityFilters = EntityFilters.Entity,
            RetrieveAsIfPublished = false
        };

        var response = await Task.Run(() => (RetrieveAllEntitiesResponse)serviceClient.Execute(request));

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

    private async Task<EntityMetadata?> RetrieveEntityMetadataAsync(
        ServiceClient serviceClient, 
        string entityLogicalName)
    {
        try
        {
            var request = new RetrieveEntityRequest
            {
                LogicalName = entityLogicalName,
                EntityFilters = EntityFilters.Entity | EntityFilters.Attributes,
                RetrieveAsIfPublished = false
            };

            var response = await Task.Run(() => (RetrieveEntityResponse)serviceClient.Execute(request));
            return response.EntityMetadata;
        }
        catch
        {
            return null;
        }
    }

    private async Task<List<RelationshipSchema>> RetrieveRelationshipsAsync(
        ServiceClient serviceClient, 
        List<string> entityLogicalNames)
    {
        var relationships = new List<RelationshipSchema>();

        foreach (var entityName in entityLogicalNames)
        {
            try
            {
                var request = new RetrieveEntityRequest
                {
                    LogicalName = entityName,
                    EntityFilters = EntityFilters.Relationships,
                    RetrieveAsIfPublished = false
                };

                var response = await Task.Run(() => (RetrieveEntityResponse)serviceClient.Execute(request));

                // OneToMany relationships
                if (response.EntityMetadata.OneToManyRelationships != null)
                {
                    foreach (var relationship in response.EntityMetadata.OneToManyRelationships)
                    {
                        var rel = _metadataMapper.MapOneToManyRelationship(relationship);
                        // Deduplicate by SchemaName
                        if (!relationships.Any(r => r.SchemaName == rel.SchemaName))
                        {
                            relationships.Add(rel);
                        }
                    }
                }

                // ManyToMany relationships
                if (response.EntityMetadata.ManyToManyRelationships != null)
                {
                    foreach (var relationship in response.EntityMetadata.ManyToManyRelationships)
                    {
                        var rel = _metadataMapper.MapManyToManyRelationship(relationship);
                        // Deduplicate by SchemaName
                        if (!relationships.Any(r => r.SchemaName == rel.SchemaName))
                        {
                            relationships.Add(rel);
                        }
                    }
                }
            }
            catch
            {
                // Skip if entity relationships cannot be retrieved
                continue;
            }
        }

        return relationships;
    }
}
