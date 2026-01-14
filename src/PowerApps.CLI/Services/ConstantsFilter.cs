using PowerApps.CLI.Models;

namespace PowerApps.CLI.Services;

/// <summary>
/// Filters entities and attributes for constants generation based on configuration.
/// </summary>
public class ConstantsFilter : IConstantsFilter
{
    /// <summary>
    /// Filters entities based on exclusion list.
    /// </summary>
    public List<EntitySchema> FilterEntities(List<EntitySchema> entities, ConstantsConfig config)
    {
        if (config.ExcludeEntities.Count == 0)
            return entities;

        var excludeSet = new HashSet<string>(config.ExcludeEntities, StringComparer.OrdinalIgnoreCase);
        
        return entities
            .Where(e => !excludeSet.Contains(e.LogicalName))
            .ToList();
    }

    /// <summary>
    /// Filters attributes within an entity based on configuration.
    /// </summary>
    public EntitySchema FilterAttributes(EntitySchema entity, ConstantsConfig config)
    {
        var filteredEntity = new EntitySchema
        {
            LogicalName = entity.LogicalName,
            SchemaName = entity.SchemaName,
            DisplayName = entity.DisplayName,
            Description = entity.Description,
            PrimaryIdAttribute = entity.PrimaryIdAttribute,
            PrimaryNameAttribute = entity.PrimaryNameAttribute,
            EntitySetName = entity.EntitySetName,
            IsCustomEntity = entity.IsCustomEntity,
            IsActivity = entity.IsActivity,
            IsAuditEnabled = entity.IsAuditEnabled,
            OwnershipType = entity.OwnershipType,
            FoundInSolutions = entity.FoundInSolutions,
            Attributes = FilterAttributeList(entity.Attributes, config)
        };

        return filteredEntity;
    }

    /// <summary>
    /// Extracts global option sets from entities.
    /// </summary>
    public List<OptionSetSchema> ExtractGlobalOptionSets(List<EntitySchema> entities)
    {
        var globalOptionSets = new Dictionary<string, OptionSetSchema>(StringComparer.OrdinalIgnoreCase);

        foreach (var entity in entities)
        {
            foreach (var attribute in entity.Attributes)
            {
                if (attribute.OptionSet != null && 
                    attribute.OptionSet.IsGlobal && 
                    !string.IsNullOrEmpty(attribute.OptionSet.Name))
                {
                    // Use dictionary to deduplicate by name
                    if (!globalOptionSets.ContainsKey(attribute.OptionSet.Name))
                    {
                        globalOptionSets[attribute.OptionSet.Name] = attribute.OptionSet;
                    }
                }
            }
        }

        return globalOptionSets.Values.OrderBy(o => o.Name).ToList();
    }

    private List<AttributeSchema> FilterAttributeList(List<AttributeSchema> attributes, ConstantsConfig config)
    {
        var filteredAttributes = attributes.AsEnumerable();

        // Apply exclusion list
        if (config.ExcludeAttributes.Count > 0)
        {
            var excludeSet = new HashSet<string>(config.ExcludeAttributes, StringComparer.OrdinalIgnoreCase);
            filteredAttributes = filteredAttributes.Where(a => !excludeSet.Contains(a.LogicalName));
        }

        // Apply attribute prefix filter
        if (!string.IsNullOrWhiteSpace(config.AttributePrefix))
        {
            filteredAttributes = filteredAttributes.Where(a => 
                a.LogicalName.StartsWith(config.AttributePrefix, StringComparison.OrdinalIgnoreCase));
        }

        return filteredAttributes.ToList();
    }
}
