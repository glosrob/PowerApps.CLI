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

    /// <summary>
    /// N:N relationships to compare.
    /// </summary>
    public List<RefDataRelationshipConfig> Relationships { get; set; } = new();
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
    /// Optional display name for the table (e.g., "Category"). If not specified, logical name will be used.
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

/// <summary>
/// Configuration for a single N:N relationship comparison.
/// </summary>
public class RefDataRelationshipConfig
{
    /// <summary>
    /// The intersect entity logical name (e.g., "contactleads").
    /// </summary>
    public string IntersectEntity { get; set; } = string.Empty;

    /// <summary>
    /// Display name for the relationship (used in report sheet tab).
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Logical name of the first related entity (e.g., "contact").
    /// </summary>
    public string Entity1 { get; set; } = string.Empty;

    /// <summary>
    /// ID column name for entity1 in the intersect entity (e.g., "contactid").
    /// </summary>
    public string Entity1IdField { get; set; } = string.Empty;

    /// <summary>
    /// Primary name field for entity1 display resolution (e.g., "fullname").
    /// </summary>
    public string? Entity1NameField { get; set; } = "name";

    /// <summary>
    /// Logical name of the second related entity (e.g., "lead").
    /// </summary>
    public string Entity2 { get; set; } = string.Empty;

    /// <summary>
    /// ID column name for entity2 in the intersect entity (e.g., "leadid").
    /// </summary>
    public string Entity2IdField { get; set; } = string.Empty;

    /// <summary>
    /// Primary name field for entity2 display resolution (e.g., "fullname").
    /// </summary>
    public string? Entity2NameField { get; set; } = "name";
}
