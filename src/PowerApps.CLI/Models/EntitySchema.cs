namespace PowerApps.CLI.Models;

public class EntitySchema
{
    public string LogicalName { get; set; } = string.Empty;
    public string SchemaName { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public string? PrimaryIdAttribute { get; set; }
    public string? PrimaryNameAttribute { get; set; }
    public string? EntitySetName { get; set; }
    public bool IsCustomEntity { get; set; }
    public bool IsActivity { get; set; }
    public bool IsAuditEnabled { get; set; }
    public string? OwnershipType { get; set; }
    public List<string>? FoundInSolutions { get; set; }
    public List<AttributeSchema> Attributes { get; set; } = new();

    public override string ToString()
    {
        return DisplayName ?? LogicalName;
    }
}
