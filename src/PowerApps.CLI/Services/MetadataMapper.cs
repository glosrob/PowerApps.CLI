using Microsoft.Xrm.Sdk.Metadata;
using PowerApps.CLI.Models;

namespace PowerApps.CLI.Services;

public class MetadataMapper : IMetadataMapper
{
    public EntitySchema MapEntity(EntityMetadata entityMetadata)
    {
        return new EntitySchema
        {
            LogicalName = entityMetadata.LogicalName,
            SchemaName = entityMetadata.SchemaName,
            DisplayName = entityMetadata.DisplayName?.UserLocalizedLabel?.Label,
            Description = entityMetadata.Description?.UserLocalizedLabel?.Label,
            PrimaryIdAttribute = entityMetadata.PrimaryIdAttribute,
            PrimaryNameAttribute = entityMetadata.PrimaryNameAttribute,
            EntitySetName = entityMetadata.EntitySetName,
            IsCustomEntity = entityMetadata.IsCustomEntity ?? false,
            IsActivity = entityMetadata.IsActivity ?? false,
            IsAuditEnabled = entityMetadata.IsAuditEnabled?.Value ?? false,
            OwnershipType = entityMetadata.OwnershipType?.ToString()
        };
    }

    public AttributeSchema MapAttribute(AttributeMetadata attributeMetadata)
    {
        var attribute = new AttributeSchema
        {
            LogicalName = attributeMetadata.LogicalName,
            SchemaName = attributeMetadata.SchemaName,
            DisplayName = attributeMetadata.DisplayName?.UserLocalizedLabel?.Label,
            Description = attributeMetadata.Description?.UserLocalizedLabel?.Label,
            AttributeType = attributeMetadata.AttributeType?.ToString(),
            IsCustomAttribute = attributeMetadata.IsCustomAttribute ?? false,
            IsPrimaryId = attributeMetadata.IsPrimaryId ?? false,
            IsPrimaryName = attributeMetadata.IsPrimaryName ?? false,
            IsAuditEnabled = attributeMetadata.IsAuditEnabled?.Value ?? false,
            IsValidForCreate = attributeMetadata.IsValidForCreate ?? false,
            IsValidForUpdate = attributeMetadata.IsValidForUpdate ?? false,
            IsValidForRead = attributeMetadata.IsValidForRead ?? false,
            RequiredLevel = attributeMetadata.RequiredLevel?.Value.ToString()
        };

        // Type-specific mappings
        if (attributeMetadata is StringAttributeMetadata stringAttr)
        {
            attribute.MaxLength = stringAttr.MaxLength;
            attribute.Format = stringAttr.Format?.ToString();
        }
        else if (attributeMetadata is DateTimeAttributeMetadata dateTimeAttr)
        {
            attribute.Format = dateTimeAttr.Format?.ToString();
        }
        else if (attributeMetadata is IntegerAttributeMetadata intAttr)
        {
            attribute.MinValue = intAttr.MinValue;
            attribute.MaxValue = intAttr.MaxValue;
        }
        else if (attributeMetadata is DecimalAttributeMetadata decimalAttr)
        {
            attribute.MinValue = (double?)decimalAttr.MinValue;
            attribute.MaxValue = (double?)decimalAttr.MaxValue;
            attribute.Precision = decimalAttr.Precision;
        }
        else if (attributeMetadata is DoubleAttributeMetadata doubleAttr)
        {
            attribute.MinValue = doubleAttr.MinValue;
            attribute.MaxValue = doubleAttr.MaxValue;
        }
        else if (attributeMetadata is MoneyAttributeMetadata moneyAttr)
        {
            attribute.MinValue = (double?)moneyAttr.MinValue;
            attribute.MaxValue = (double?)moneyAttr.MaxValue;
            attribute.Precision = moneyAttr.Precision;
        }
        else if (attributeMetadata is LookupAttributeMetadata lookupAttr)
        {
            attribute.Targets = lookupAttr.Targets;
        }

        // Map option sets
        attribute.OptionSet = MapOptionSet(attributeMetadata);

        return attribute;
    }

    public OptionSetSchema? MapOptionSet(AttributeMetadata attributeMetadata)
    {
        if (attributeMetadata is PicklistAttributeMetadata picklistAttr && picklistAttr.OptionSet != null)
        {
            var optionSet = picklistAttr.OptionSet;
            return new OptionSetSchema
            {
                Name = optionSet.Name,
                IsGlobal = optionSet.IsGlobal ?? false,
                Options = optionSet.Options.Select(o => new OptionSchema
                {
                    Value = o.Value ?? 0,
                    Label = o.Label?.UserLocalizedLabel?.Label
                }).ToList()
            };
        }

        if (attributeMetadata is StateAttributeMetadata stateAttr && stateAttr.OptionSet != null)
        {
            return new OptionSetSchema
            {
                Name = stateAttr.OptionSet.Name,
                IsGlobal = false,
                Options = stateAttr.OptionSet.Options.Select(o => new OptionSchema
                {
                    Value = o.Value ?? 0,
                    Label = o.Label?.UserLocalizedLabel?.Label
                }).ToList()
            };
        }

        if (attributeMetadata is StatusAttributeMetadata statusAttr && statusAttr.OptionSet != null)
        {
            return new OptionSetSchema
            {
                Name = statusAttr.OptionSet.Name,
                IsGlobal = false,
                Options = statusAttr.OptionSet.Options.Select(o => new OptionSchema
                {
                    Value = o.Value ?? 0,
                    Label = o.Label?.UserLocalizedLabel?.Label
                }).ToList()
            };
        }

        return null;
    }

    public RelationshipSchema MapOneToManyRelationship(OneToManyRelationshipMetadata relationship)
    {
        return new RelationshipSchema
        {
            SchemaName = relationship.SchemaName,
            RelationshipType = "OneToMany",
            ReferencingEntity = relationship.ReferencingEntity,
            ReferencingAttribute = relationship.ReferencingAttribute,
            ReferencedEntity = relationship.ReferencedEntity,
            ReferencedAttribute = relationship.ReferencedAttribute,
            IsCustomRelationship = relationship.IsCustomRelationship ?? false
        };
    }

    public RelationshipSchema MapManyToManyRelationship(ManyToManyRelationshipMetadata relationship)
    {
        return new RelationshipSchema
        {
            SchemaName = relationship.SchemaName,
            RelationshipType = "ManyToMany",
            Entity1LogicalName = relationship.Entity1LogicalName,
            Entity2LogicalName = relationship.Entity2LogicalName,
            IntersectEntityName = relationship.IntersectEntityName,
            IsCustomRelationship = relationship.IsCustomRelationship ?? false
        };
    }
}
