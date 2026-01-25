using Microsoft.Xrm.Sdk;
using PowerApps.CLI.Infrastructure;
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
        HashSet<string> excludeFields,
        string? primaryNameField = null,
        string? primaryIdField = null)
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
                RecordName = sourceEntity.GetRecordName(primaryNameField),
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
                RecordName = targetEntity.GetRecordName(primaryNameField),
                DifferenceType = DifferenceType.Deleted
            });
        }

        // Find MODIFIED records (in both, compare fields)
        foreach (var commonId in sourceDict.Keys.Intersect(targetDict.Keys))
        {
            var sourceEntity = sourceDict[commonId];
            var targetEntity = targetDict[commonId];

            var fieldDifferences = CompareFields(sourceEntity, targetEntity, excludeFields, primaryIdField);

            if (fieldDifferences.Any())
            {
                result.Differences.Add(new RecordDifference
                {
                    RecordId = commonId,
                    RecordName = sourceEntity.GetRecordName(primaryNameField),
                    DifferenceType = DifferenceType.Modified,
                    FieldDifferences = fieldDifferences
                });
            }
        }

        return result;
    }

    private List<FieldDifference> CompareFields(Entity sourceEntity, Entity targetEntity, HashSet<string> excludeFields, string? primaryIdField = null)
    {
        var differences = new List<FieldDifference>();

        // Combine all attribute names from both entities
        var allAttributes = sourceEntity.Attributes.Keys
            .Union(targetEntity.Attributes.Keys)
            .Where(attr => !ShouldExcludeField(attr, excludeFields, primaryIdField))
            .ToList();

        foreach (var attributeName in allAttributes)
        {
            var sourceValue = sourceEntity.GetFormattedValue(attributeName);
            var targetValue = targetEntity.GetFormattedValue(attributeName);

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

    private bool ShouldExcludeField(string fieldName, HashSet<string> excludeFields, string? primaryIdField = null)
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

        // Exclude the primary ID attribute if specified
        if (!string.IsNullOrWhiteSpace(primaryIdField) && 
            string.Equals(fieldName, primaryIdField, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Try to auto-detect primary ID if not specified
        if (string.IsNullOrWhiteSpace(primaryIdField) &&
            fieldName.EndsWith("id", StringComparison.OrdinalIgnoreCase) && 
            fieldName.Length > 2 && 
            !fieldName.Contains("_"))
        {
            // This is likely the primary key (e.g., "accountid")
            return true;
        }

        return false;
    }
}
