namespace PowerApps.CLI.Models;

public class RelationshipSchema
{
    public string SchemaName { get; set; } = string.Empty;
    public string RelationshipType { get; set; } = string.Empty;

    // OneToMany properties
    public string? ReferencingEntity { get; set; }
    public string? ReferencingAttribute { get; set; }
    public string? ReferencedEntity { get; set; }
    public string? ReferencedAttribute { get; set; }

    // ManyToMany properties
    public string? Entity1LogicalName { get; set; }
    public string? Entity2LogicalName { get; set; }
    public string? IntersectEntityName { get; set; }

    public bool IsCustomRelationship { get; set; }

    public override string ToString()
    {
        if (RelationshipType == "OneToMany")
        {
            return $"Relationship: {SchemaName} (1:N) - {ReferencedEntity}.{ReferencedAttribute} → {ReferencingEntity}.{ReferencingAttribute} - Custom: {IsCustomRelationship}";
        }
        else if (RelationshipType == "ManyToMany")
        {
            return $"Relationship: {SchemaName} (N:N) - {Entity1LogicalName} ↔ {Entity2LogicalName} via {IntersectEntityName} - Custom: {IsCustomRelationship}";
        }
        return $"Relationship: {SchemaName} ({RelationshipType}) - Custom: {IsCustomRelationship}";
    }
}
