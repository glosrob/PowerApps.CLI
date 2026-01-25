using Microsoft.Xrm.Sdk;

namespace PowerApps.CLI.Infrastructure;

/// <summary>
/// Extension methods for Microsoft.Xrm.Sdk.Entity.
/// </summary>
public static class EntityExtensions
{
    /// <summary>
    /// Gets the formatted value for an attribute, falling back to raw value if formatted value is not available.
    /// </summary>
    /// <param name="entity">The entity containing the attribute.</param>
    /// <param name="attributeName">The logical name of the attribute.</param>
    /// <returns>The formatted value as a string, or null if the attribute does not exist or has no value.</returns>
    public static string? GetFormattedValue(this Entity entity, string attributeName)
    {
        if (entity == null)
        {
            throw new ArgumentNullException(nameof(entity));
        }

        // Try to get formatted value first (for lookups, optionsets, dates)
        if (entity.FormattedValues.ContainsKey(attributeName))
        {
            return entity.FormattedValues[attributeName];
        }

        // Return null if attribute doesn't exist
        if (!entity.Attributes.ContainsKey(attributeName))
        {
            return null;
        }

        var value = entity.Attributes[attributeName];
        
        // Return null for null values
        if (value == null)
        {
            return null;
        }

        // Handle specific types with pattern matching
        return value switch
        {
            EntityReference entityRef => entityRef.Name ?? entityRef.Id.ToString(),
            OptionSetValue optionSet => optionSet.Value.ToString(),
            Money money => money.Value.ToString("F2"),
            _ => value.ToString()
        };
    }

    /// <summary>
    /// Gets a human-readable name for a record.
    /// </summary>
    /// <param name="entity">The entity to get the name from.</param>
    /// <param name="primaryNameField">Optional specific field to use as the primary name.</param>
    /// <returns>The record name or ID if no name found.</returns>
    public static string GetRecordName(this Entity entity, string? primaryNameField = null)
    {
        if (entity == null)
        {
            throw new ArgumentNullException(nameof(entity));
        }

        // Try specific field if provided
        if (!string.IsNullOrWhiteSpace(primaryNameField))
        {
            var nameFromPrimary = TryGetAttributeValue(entity, primaryNameField);
            if (nameFromPrimary != null)
            {
                return nameFromPrimary;
            }
        }

        // Try common name attributes
        var nameAttributes = new[] { "name", entity.LogicalName + "name", "fullname", "subject", "title" };
        
        foreach (var attr in nameAttributes)
        {
            var nameValue = TryGetAttributeValue(entity, attr);
            if (nameValue != null)
            {
                return nameValue;
            }
        }

        // Fall back to ID
        return entity.Id.ToString();
    }

    private static string? TryGetAttributeValue(Entity entity, string attributeName)
    {
        if (!entity.Attributes.ContainsKey(attributeName))
        {
            return null;
        }

        var value = entity.Attributes[attributeName];
        return value?.ToString();
    }
}
