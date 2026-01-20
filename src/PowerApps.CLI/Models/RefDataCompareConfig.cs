namespace PowerApps.CLI.Models;

/// <summary>
/// Configuration for reference data comparison.
/// </summary>
public class RefDataCompareConfig
{
    /// <summary>
    /// Exclude standard system fields (createdby, modifiedby, etc.)
    /// </summary>
    public bool ExcludeSystemFields { get; set; } = true;

    /// <summary>
    /// Additional field names to exclude globally across all tables.
    /// </summary>
    public List<string> GlobalExcludeFields { get; set; } = new();

    /// <summary>
    /// Tables to compare.
    /// </summary>
    public List<RefDataTableConfig> Tables { get; set; } = new();
}

/// <summary>
/// Configuration for a single table comparison.
/// </summary>
public class RefDataTableConfig
{
    /// <summary>
    /// Logical name of the table (e.g., "rob_category").
    /// </summary>
    public string LogicalName { get; set; } = string.Empty;

    /// <summary>
    /// Optional FetchXML filter to apply when retrieving records.
    /// </summary>
    public string? Filter { get; set; }

    /// <summary>
    /// Additional field names to exclude for this specific table.
    /// </summary>
    public List<string> ExcludeFields { get; set; } = new();
}
