namespace PowerApps.CLI.Models;

/// <summary>
/// Results of comparing a single table across environments.
/// </summary>
public class TableComparisonResult
{
    /// <summary>
    /// Logical name of the table.
    /// </summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>
    /// Total records in source environment.
    /// </summary>
    public int SourceRecordCount { get; set; }

    /// <summary>
    /// Total records in target environment.
    /// </summary>
    public int TargetRecordCount { get; set; }

    /// <summary>
    /// All differences found for this table.
    /// </summary>
    public List<RecordDifference> Differences { get; set; } = new();

    /// <summary>
    /// Number of new records (source only).
    /// </summary>
    public int NewCount => Differences.Count(d => d.DifferenceType == DifferenceType.New);

    /// <summary>
    /// Number of modified records.
    /// </summary>
    public int ModifiedCount => Differences.Count(d => d.DifferenceType == DifferenceType.Modified);

    /// <summary>
    /// Number of deleted records (target only).
    /// </summary>
    public int DeletedCount => Differences.Count(d => d.DifferenceType == DifferenceType.Deleted);

    /// <summary>
    /// Whether this table has any differences.
    /// </summary>
    public bool HasDifferences => Differences.Any();
}
