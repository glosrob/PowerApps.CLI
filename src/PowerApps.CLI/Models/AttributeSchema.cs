namespace PowerApps.CLI.Models;

public class AttributeSchema
{
    public string LogicalName { get; set; } = string.Empty;
    public string SchemaName { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public string? AttributeType { get; set; }
    public bool IsCustomAttribute { get; set; }
    public bool IsPrimaryId { get; set; }
    public bool IsPrimaryName { get; set; }
    public bool IsAuditEnabled { get; set; }
    public bool IsValidForCreate { get; set; }
    public bool IsValidForUpdate { get; set; }
    public bool IsValidForRead { get; set; }
    public string? RequiredLevel { get; set; }
    public int? MaxLength { get; set; }
    public string? Format { get; set; }
    public double? MinValue { get; set; }
    public double? MaxValue { get; set; }
    public int? Precision { get; set; }
    public OptionSetSchema? OptionSet { get; set; }
    public string[]? Targets { get; set; }

    public override string ToString()
    {
        return DisplayName ?? LogicalName ?? SchemaName;
    }
}
