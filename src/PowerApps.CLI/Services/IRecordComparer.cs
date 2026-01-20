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
    /// <param name="primaryNameField">Optional logical name of the primary name field for display.</param>
    /// <param name="primaryIdField">Optional logical name of the primary ID field to exclude from comparison.</param>
    /// <returns>Comparison result with all differences.</returns>
    TableComparisonResult CompareRecords(
        string tableName,
        EntityCollection sourceRecords,
        EntityCollection targetRecords,
        HashSet<string> excludeFields,
        string? primaryNameField = null,
        string? primaryIdField = null);
}
