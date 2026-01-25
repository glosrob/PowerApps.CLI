namespace PowerApps.CLI.Models;

/// <summary>
/// Represents a difference between two records.
/// </summary>
public class RecordDifference
{
    /// <summary>
    /// Record ID (GUID).
    /// </summary>
    public Guid RecordId { get; set; }

    /// <summary>
    /// Primary name/display value of the record.
    /// </summary>
    public string RecordName { get; set; } = string.Empty;

    /// <summary>
    /// Type of difference.
    /// </summary>
    public DifferenceType DifferenceType { get; set; }

    /// <summary>
    /// Field-level differences (empty for NEW/DELETED).
    /// </summary>
    public List<FieldDifference> FieldDifferences { get; set; } = new();
}

/// <summary>
/// Represents a difference in a single field.
/// </summary>
public class FieldDifference
{
    /// <summary>
    /// Field logical name.
    /// </summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>
    /// Value in source environment (formatted).
    /// </summary>
    public string? SourceValue { get; set; }

    /// <summary>
    /// Value in target environment (formatted).
    /// </summary>
    public string? TargetValue { get; set; }
}

/// <summary>
/// Type of difference between records.
/// </summary>
public enum DifferenceType
{
    /// <summary>
    /// Record exists only in source (needs deployment).
    /// </summary>
    New,

    /// <summary>
    /// Record exists in both but has field differences.
    /// </summary>
    Modified,

    /// <summary>
    /// Record exists only in target (orphaned).
    /// </summary>
    Deleted
}
