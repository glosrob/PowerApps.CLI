using PowerApps.CLI.Models;

namespace PowerApps.CLI.Services;

/// <summary>
/// Filters entities and attributes for constants generation based on configuration.
/// </summary>
public interface IConstantsFilter
{
    /// <summary>
    /// Filters entities based on exclusion list.
    /// </summary>
    List<EntitySchema> FilterEntities(List<EntitySchema> entities, ConstantsConfig config);

    /// <summary>
    /// Filters attributes within an entity based on configuration.
    /// </summary>
    EntitySchema FilterAttributes(EntitySchema entity, ConstantsConfig config);

    /// <summary>
    /// Extracts global option sets from entities.
    /// </summary>
    List<OptionSetSchema> ExtractGlobalOptionSets(List<EntitySchema> entities);
}
