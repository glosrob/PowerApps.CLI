using Microsoft.Xrm.Sdk.Metadata;
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
            ? await GetEntitiesFromSolutionsAsync(solutions)
            : await _dataverseClient.GetAllEntityMetadataAsync();

        // Retrieve entity metadata
        var entities = new Dictionary<string, EntitySchema>();
        foreach (var entityName in entityLogicalNames.Keys)
        {
            var entityMetadata = await _dataverseClient.GetEntityMetadataAsync(entityName, EntityFilters.Entity | EntityFilters.Attributes);
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
        var relationships = await RetrieveRelationshipsAsync(entityLogicalNames.Keys.ToList());
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
        List<string> solutionNames)
    {
        var entitySolutions = new Dictionary<string, List<string>>();

        foreach (var solutionName in solutionNames)
        {
            var solutionEntities = await _dataverseClient.GetEntitiesFromSolutionAsync(solutionName);
            
            foreach (var kvp in solutionEntities)
            {
                if (!entitySolutions.ContainsKey(kvp.Key))
                {
                    entitySolutions[kvp.Key] = new List<string>();
                }
                
                entitySolutions[kvp.Key].Add(solutionName);
            }
        }

        return entitySolutions;
    }

    private async Task<List<RelationshipSchema>> RetrieveRelationshipsAsync(
        List<string> entityLogicalNames)
    {
        var relationships = new List<RelationshipSchema>();

        foreach (var entityName in entityLogicalNames)
        {
            var entityMetadata = await _dataverseClient.GetEntityMetadataAsync(entityName, EntityFilters.Relationships);
            if (entityMetadata == null) continue;

            // OneToMany relationships
            if (entityMetadata.OneToManyRelationships != null)
            {
                foreach (var relationship in entityMetadata.OneToManyRelationships)
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
            if (entityMetadata.ManyToManyRelationships != null)
            {
                foreach (var relationship in entityMetadata.ManyToManyRelationships)
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

        return relationships;
    }
}
