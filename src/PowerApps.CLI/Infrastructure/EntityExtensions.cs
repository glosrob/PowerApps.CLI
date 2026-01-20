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

        // Fall back to raw value
        if (entity.Attributes.ContainsKey(attributeName))
        {
            var value = entity.Attributes[attributeName];
            
            if (value == null)
            {
                return null;
            }

            // Handle specific types
            if (value is EntityReference entityRef)
            {
                return entityRef.Name ?? entityRef.Id.ToString();
            }

            if (value is OptionSetValue optionSet)
            {
                return optionSet.Value.ToString();
            }

            if (value is Money money)
            {
                return money.Value.ToString("F2");
            }

            return value.ToString();
        }

        return null;
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

        // If specific field provided, try that first
        if (!string.IsNullOrWhiteSpace(primaryNameField) && entity.Attributes.ContainsKey(primaryNameField))
        {
            var value = entity.Attributes[primaryNameField];
            if (value != null)
            {
                return value.ToString() ?? entity.Id.ToString();
            }
        }

        // Try common name attributes
        var nameAttributes = new[] { "name", entity.LogicalName + "name", "fullname", "subject", "title" };
        
        foreach (var attr in nameAttributes)
        {
            if (entity.Attributes.ContainsKey(attr))
            {
                var value = entity.Attributes[attr];
                if (value != null)
                {
                    return value.ToString() ?? entity.Id.ToString();
                }
            }
        }

        // Fall back to ID
        return entity.Id.ToString();
    }
}
