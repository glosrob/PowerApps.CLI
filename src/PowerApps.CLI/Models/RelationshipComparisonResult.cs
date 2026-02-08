namespace PowerApps.CLI.Models;

/// <summary>
/// Results for a single N:N relationship comparison.
/// </summary>
public class RelationshipComparisonResult
{
    /// <summary>
    /// Display name for the relationship.
    /// </summary>
    public string RelationshipName { get; set; } = string.Empty;

    /// <summary>
    /// Intersect entity logical name.
    /// </summary>
    public string IntersectEntity { get; set; } = string.Empty;

    /// <summary>
    /// Total associations in source environment.
    /// </summary>
    public int SourceAssociationCount { get; set; }

    /// <summary>
    /// Total associations in target environment.
    /// </summary>
    public int TargetAssociationCount { get; set; }

    /// <summary>
    /// All association differences found.
    /// </summary>
    public List<AssociationDifference> Differences { get; set; } = new();

    /// <summary>
    /// Count of associations only in source (needs deployment).
    /// </summary>
    public int NewCount => Differences.Count(d => d.DifferenceType == DifferenceType.New);

    /// <summary>
    /// Count of associations only in target (orphaned).
    /// </summary>
    public int DeletedCount => Differences.Count(d => d.DifferenceType == DifferenceType.Deleted);

    /// <summary>
    /// Whether this relationship has any differences.
    /// </summary>
    public bool HasDifferences => Differences.Count > 0;
}

/// <summary>
/// Represents a difference in a single N:N association.
/// </summary>
public class AssociationDifference
{
    /// <summary>
    /// ID of the first related entity record.
    /// </summary>
    public Guid Entity1Id { get; set; }

    /// <summary>
    /// Display name of the first related entity record.
    /// </summary>
    public string? Entity1Name { get; set; }

    /// <summary>
    /// ID of the second related entity record.
    /// </summary>
    public Guid Entity2Id { get; set; }

    /// <summary>
    /// Display name of the second related entity record.
    /// </summary>
    public string? Entity2Name { get; set; }

    /// <summary>
    /// Type of difference (New or Deleted only — associations cannot be Modified).
    /// </summary>
    public DifferenceType DifferenceType { get; set; }
}
