using Microsoft.Xrm.Sdk.Metadata;
using PowerApps.CLI.Models;

namespace PowerApps.CLI.Services;

public interface IMetadataMapper
{
    EntitySchema MapEntity(EntityMetadata entityMetadata);
    AttributeSchema MapAttribute(AttributeMetadata attributeMetadata);
    OptionSetSchema? MapOptionSet(AttributeMetadata attributeMetadata);
    RelationshipSchema MapOneToManyRelationship(OneToManyRelationshipMetadata relationship);
    RelationshipSchema MapManyToManyRelationship(ManyToManyRelationshipMetadata relationship);
}
