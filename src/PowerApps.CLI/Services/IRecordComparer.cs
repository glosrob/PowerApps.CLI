using Microsoft.Xrm.Sdk;
using PowerApps.CLI.Models;

namespace PowerApps.CLI.Services;

/// <summary>
/// Compares records between source and target environments.
/// </summary>
public interface IRecordComparer
{
    /// <summary>
    /// Compares records from source and target environments and identifies differences.
    /// </summary>
    /// <param name="tableName">Logical name of the table being compared.</param>
    /// <param name="sourceRecords">Records from source environment.</param>
    /// <param name="targetRecords">Records from target environment.</param>
    /// <param name="excludeFields">Field names to exclude from comparison.</param>
    /// <param name="includeFields">If non-empty, only these fields will be compared (allowlist). Applied after excludeFields.</param>
    /// <param name="primaryNameField">Optional logical name of the primary name field for display.</param>
    /// <param name="primaryIdField">Optional logical name of the primary ID field to exclude from comparison.</param>
    /// <returns>Comparison result with all differences.</returns>
    TableComparisonResult CompareRecords(
        string tableName,
        EntityCollection sourceRecords,
        EntityCollection targetRecords,
        HashSet<string> excludeFields,
        ISet<string>? includeFields = null,
        string? primaryNameField = null,
        string? primaryIdField = null);

    /// <summary>
    /// Compares N:N associations between source and target environments.
    /// </summary>
    /// <param name="relationshipName">Display name for the relationship.</param>
    /// <param name="sourceAssociations">Association records from source environment.</param>
    /// <param name="targetAssociations">Association records from target environment.</param>
    /// <param name="entity1IdField">ID column name for entity1 in the intersect entity.</param>
    /// <param name="entity2IdField">ID column name for entity2 in the intersect entity.</param>
    /// <param name="entity1Names">Lookup dictionary mapping entity1 IDs to display names.</param>
    /// <param name="entity2Names">Lookup dictionary mapping entity2 IDs to display names.</param>
    /// <returns>Comparison result with association differences.</returns>
    RelationshipComparisonResult CompareAssociations(
        string relationshipName,
        EntityCollection sourceAssociations,
        EntityCollection targetAssociations,
        string entity1IdField,
        string entity2IdField,
        Dictionary<Guid, string> entity1Names,
        Dictionary<Guid, string> entity2Names);
}
