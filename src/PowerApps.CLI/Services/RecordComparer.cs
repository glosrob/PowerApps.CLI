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
        ISet<string>? includeFields = null,
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

            var fieldDifferences = CompareFields(sourceEntity, targetEntity, excludeFields, includeFields, primaryIdField);

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

    private List<FieldDifference> CompareFields(Entity sourceEntity, Entity targetEntity, HashSet<string> excludeFields, ISet<string>? includeFields, string? primaryIdField = null)
    {
        var differences = new List<FieldDifference>();

        // Combine all attribute names from both entities
        var allAttributes = sourceEntity.Attributes.Keys
            .Union(targetEntity.Attributes.Keys)
            .Where(attr => !ShouldExcludeField(attr, excludeFields, primaryIdField))
            .ToList();

        // Apply include allowlist if specified
        if (includeFields != null && includeFields.Count > 0)
        {
            allAttributes = allAttributes
                .Where(attr => includeFields.Contains(attr))
                .ToList();
        }

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

    public RelationshipComparisonResult CompareAssociations(
        string relationshipName,
        EntityCollection sourceAssociations,
        EntityCollection targetAssociations,
        string entity1IdField,
        string entity2IdField,
        Dictionary<Guid, string> entity1Names,
        Dictionary<Guid, string> entity2Names)
    {
        var result = new RelationshipComparisonResult
        {
            RelationshipName = relationshipName,
            SourceAssociationCount = sourceAssociations.Entities.Count,
            TargetAssociationCount = targetAssociations.Entities.Count
        };

        // Build composite key sets
        var sourceKeys = new HashSet<(Guid, Guid)>();
        var sourceByKey = new Dictionary<(Guid, Guid), Entity>();
        foreach (var entity in sourceAssociations.Entities)
        {
            var key = GetCompositeKey(entity, entity1IdField, entity2IdField);
            if (key.HasValue)
            {
                sourceKeys.Add(key.Value);
                sourceByKey[key.Value] = entity;
            }
        }

        var targetKeys = new HashSet<(Guid, Guid)>();
        foreach (var entity in targetAssociations.Entities)
        {
            var key = GetCompositeKey(entity, entity1IdField, entity2IdField);
            if (key.HasValue)
            {
                targetKeys.Add(key.Value);
            }
        }

        // New = in source but not target
        foreach (var key in sourceKeys.Except(targetKeys))
        {
            result.Differences.Add(new AssociationDifference
            {
                Entity1Id = key.Item1,
                Entity1Name = entity1Names.GetValueOrDefault(key.Item1, key.Item1.ToString()),
                Entity2Id = key.Item2,
                Entity2Name = entity2Names.GetValueOrDefault(key.Item2, key.Item2.ToString()),
                DifferenceType = DifferenceType.New
            });
        }

        // Deleted = in target but not source
        foreach (var key in targetKeys.Except(sourceKeys))
        {
            result.Differences.Add(new AssociationDifference
            {
                Entity1Id = key.Item1,
                Entity1Name = entity1Names.GetValueOrDefault(key.Item1, key.Item1.ToString()),
                Entity2Id = key.Item2,
                Entity2Name = entity2Names.GetValueOrDefault(key.Item2, key.Item2.ToString()),
                DifferenceType = DifferenceType.Deleted
            });
        }

        return result;
    }

    private static (Guid, Guid)? GetCompositeKey(Entity entity, string entity1IdField, string entity2IdField)
    {
        if (entity.Attributes.ContainsKey(entity1IdField) && entity.Attributes.ContainsKey(entity2IdField))
        {
            var id1 = (Guid)entity.Attributes[entity1IdField];
            var id2 = (Guid)entity.Attributes[entity2IdField];
            return (id1, id2);
        }
        return null;
    }

    /// <summary>
    /// Builds a lookup dictionary mapping record IDs to display names.
    /// </summary>
    public static Dictionary<Guid, string> BuildNameLookup(EntityCollection records, string nameField)
    {
        var lookup = new Dictionary<Guid, string>();
        foreach (var entity in records.Entities)
        {
            var name = entity.GetFormattedValue(nameField);
            if (name != null)
            {
                lookup[entity.Id] = name;
            }
        }
        return lookup;
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
