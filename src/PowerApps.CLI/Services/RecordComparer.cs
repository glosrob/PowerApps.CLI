using Microsoft.Xrm.Sdk;
using PowerApps.CLI.Models;

namespace PowerApps.CLI.Services;

/// <summary>
/// Compares records between source and target environments.
/// </summary>
public class RecordComparer : IRecordComparer
{
    private static readonly HashSet<string> DefaultSystemFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "createdby", "createdon", "createdonbehalfby",
        "modifiedby", "modifiedon", "modifiedonbehalfby",
        "ownerid", "owninguser", "owningteam", "owningbusinessunit",
        "versionnumber", "importsequencenumber", "overriddencreatedon",
        "timezoneruleversionnumber", "utcconversiontimezonecode"
    };

    public TableComparisonResult CompareRecords(
        string tableName,
        EntityCollection sourceRecords,
        EntityCollection targetRecords,
        HashSet<string> excludeFields)
    {
        var result = new TableComparisonResult
        {
            TableName = tableName,
            SourceRecordCount = sourceRecords.Entities.Count,
            TargetRecordCount = targetRecords.Entities.Count
        };

        // Create dictionaries for fast lookup by primary key
        var sourceDict = sourceRecords.Entities.ToDictionary(e => e.Id);
        var targetDict = targetRecords.Entities.ToDictionary(e => e.Id);

        // Find NEW records (in source only)
        foreach (var sourceId in sourceDict.Keys.Except(targetDict.Keys))
        {
            var sourceEntity = sourceDict[sourceId];
            result.Differences.Add(new RecordDifference
            {
                RecordId = sourceId,
                RecordName = GetRecordName(sourceEntity),
                DifferenceType = DifferenceType.New
            });
        }

        // Find DELETED records (in target only)
        foreach (var targetId in targetDict.Keys.Except(sourceDict.Keys))
        {
            var targetEntity = targetDict[targetId];
            result.Differences.Add(new RecordDifference
            {
                RecordId = targetId,
                RecordName = GetRecordName(targetEntity),
                DifferenceType = DifferenceType.Deleted
            });
        }

        // Find MODIFIED records (in both, compare fields)
        foreach (var commonId in sourceDict.Keys.Intersect(targetDict.Keys))
        {
            var sourceEntity = sourceDict[commonId];
            var targetEntity = targetDict[commonId];

            var fieldDifferences = CompareFields(sourceEntity, targetEntity, excludeFields);

            if (fieldDifferences.Any())
            {
                result.Differences.Add(new RecordDifference
                {
                    RecordId = commonId,
                    RecordName = GetRecordName(sourceEntity),
                    DifferenceType = DifferenceType.Modified,
                    FieldDifferences = fieldDifferences
                });
            }
        }

        return result;
    }

    private List<FieldDifference> CompareFields(Entity sourceEntity, Entity targetEntity, HashSet<string> excludeFields)
    {
        var differences = new List<FieldDifference>();

        // Combine all attribute names from both entities
        var allAttributes = sourceEntity.Attributes.Keys
            .Union(targetEntity.Attributes.Keys)
            .Where(attr => !ShouldExcludeField(attr, excludeFields))
            .ToList();

        foreach (var attributeName in allAttributes)
        {
            var sourceValue = GetFormattedValue(sourceEntity, attributeName);
            var targetValue = GetFormattedValue(targetEntity, attributeName);

            // Compare values (case-sensitive for precision)
            if (!string.Equals(sourceValue, targetValue, StringComparison.Ordinal))
            {
                differences.Add(new FieldDifference
                {
                    FieldName = attributeName,
                    SourceValue = sourceValue,
                    TargetValue = targetValue
                });
            }
        }

        return differences;
    }

    private bool ShouldExcludeField(string fieldName, HashSet<string> excludeFields)
    {
        // Exclude if in custom exclude list
        if (excludeFields.Contains(fieldName))
        {
            return true;
        }

        // Exclude if it's a default system field
        if (DefaultSystemFields.Contains(fieldName))
        {
            return true;
        }

        // Always exclude the primary ID attribute (handled separately)
        if (fieldName.EndsWith("id", StringComparison.OrdinalIgnoreCase) && 
            fieldName.Length > 2 && 
            !fieldName.Contains("_"))
        {
            // This is likely the primary key (e.g., "accountid")
            return true;
        }

        return false;
    }

    private string? GetFormattedValue(Entity entity, string attributeName)
    {
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

    private string GetRecordName(Entity entity)
    {
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
