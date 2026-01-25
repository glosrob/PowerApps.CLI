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
    /// Optional display name for the table (e.g., "Gender"). If not specified, logical name will be used.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Optional logical name of the primary name field to use for display (e.g., "name", "rob_name").
    /// If not specified, common name attributes will be tried.
    /// </summary>
    public string? PrimaryNameField { get; set; }

    /// <summary>
    /// Optional logical name of the primary ID field (e.g., "accountid", "rob_categoryid").
    /// If not specified, the field will be auto-detected.
    /// </summary>
    public string? PrimaryIdField { get; set; }

    /// <summary>
    /// Optional FetchXML filter to apply when retrieving records.
    /// </summary>
    public string? Filter { get; set; }

    /// <summary>
    /// Additional field names to exclude for this specific table.
    /// </summary>
    public List<string> ExcludeFields { get; set; } = new();
}
