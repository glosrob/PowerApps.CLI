namespace PowerApps.CLI.Models;

/// <summary>
/// Overall comparison results across all tables.
/// </summary>
public class ComparisonResult
{
    /// <summary>
    /// Source environment URL.
    /// </summary>
    public string SourceEnvironment { get; set; } = string.Empty;

    /// <summary>
    /// Target environment URL.
    /// </summary>
    public string TargetEnvironment { get; set; } = string.Empty;

    /// <summary>
    /// Comparison timestamp.
    /// </summary>
    public DateTime ComparisonDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Results for each table compared.
    /// </summary>
    public List<TableComparisonResult> TableResults { get; set; } = new();

    /// <summary>
    /// Whether any differences were found across all tables.
    /// </summary>
    public bool HasAnyDifferences => TableResults.Any(t => t.HasDifferences);
}
