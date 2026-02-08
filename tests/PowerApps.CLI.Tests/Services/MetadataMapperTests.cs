using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Metadata;
using PowerApps.CLI.Services;
using Xunit;

namespace PowerApps.CLI.Tests.Services;

public class MetadataMapperTests
{
    private readonly MetadataMapper _mapper;

    public MetadataMapperTests()
    {
        _mapper = new MetadataMapper();
    }

    [Fact]
    public void MapEntity_ShouldMapBasicProperties()
    {
        // Arrange
        var entityMetadata = new EntityMetadata
        {
            LogicalName = "account",
            SchemaName = "Account",
            EntitySetName = "accounts"
        };
        entityMetadata.DisplayName = new Label(new LocalizedLabel("Account", 1033), Array.Empty<LocalizedLabel>());
        entityMetadata.Description = new Label(new LocalizedLabel("Business account", 1033), Array.Empty<LocalizedLabel>());

        // Act
        var result = _mapper.MapEntity(entityMetadata);

        // Assert
        Assert.Equal("account", result.LogicalName);
        Assert.Equal("Account", result.SchemaName);
        Assert.Equal("Account", result.DisplayName);
        Assert.Equal("Business account", result.Description);
        Assert.Equal("accounts", result.EntitySetName);
    }

    [Fact]
    public void MapEntity_ShouldHandleNullableProperties()
    {
        // Arrange
        var entityMetadata = new EntityMetadata
        {
            LogicalName = "rob_customentity",
            SchemaName = "rob_CustomEntity"
        };

        // Act
        var result = _mapper.MapEntity(entityMetadata);

        // Assert
        Assert.Equal("rob_customentity", result.LogicalName);
        Assert.Null(result.DisplayName);
        Assert.Null(result.Description);
    }

    [Fact]
    public void MapAttribute_ShouldMapStringAttribute()
    {
        // Arrange
        var attributeMetadata = new StringAttributeMetadata
        {
            LogicalName = "name",
            SchemaName = "Name",
            MaxLength = 100,
            Format = StringFormat.Text
        };
        attributeMetadata.DisplayName = new Label(new LocalizedLabel("Name", 1033), Array.Empty<LocalizedLabel>());

        // Act
        var result = _mapper.MapAttribute(attributeMetadata);

        // Assert
        Assert.Equal("name", result.LogicalName);
        Assert.Equal("Name", result.DisplayName);
        Assert.Equal("String", result.AttributeType);
        Assert.Equal(100, result.MaxLength);
        Assert.Equal("Text", result.Format);
    }

    [Fact]
    public void MapAttribute_ShouldMapIntegerAttribute()
    {
        // Arrange
        var attributeMetadata = new IntegerAttributeMetadata
        {
            LogicalName = "age",
            SchemaName = "Age",
            MinValue = 0,
            MaxValue = 120
        };

        // Act
        var result = _mapper.MapAttribute(attributeMetadata);

        // Assert
        Assert.Equal("age", result.LogicalName);
        Assert.Equal("Integer", result.AttributeType);
        Assert.Equal(0, result.MinValue);
        Assert.Equal(120, result.MaxValue);
    }

    [Fact]
    public void MapAttribute_ShouldMapLookupAttribute()
    {
        // Arrange
        var attributeMetadata = new LookupAttributeMetadata
        {
            LogicalName = "customerid",
            SchemaName = "CustomerId",
            Targets = new[] { "account", "contact" }
        };

        // Act
        var result = _mapper.MapAttribute(attributeMetadata);

        // Assert
        Assert.Equal("customerid", result.LogicalName);
        Assert.Equal("Lookup", result.AttributeType);
        Assert.NotNull(result.Targets);
        Assert.Equal(2, result.Targets!.Length);
        Assert.Contains("account", result.Targets);
        Assert.Contains("contact", result.Targets);
    }

    [Fact]
    public void MapOptionSet_ShouldMapPicklistAttribute()
    {
        // Arrange
        var optionSet = new OptionSetMetadata
        {
            Name = "statecode",
            DisplayName = new Label(new LocalizedLabel("State Code", 1033), Array.Empty<LocalizedLabel>()),
            IsGlobal = false
        };
        optionSet.Options.Add(new OptionMetadata(new Label(new LocalizedLabel("Active", 1033), Array.Empty<LocalizedLabel>()), 0));
        optionSet.Options.Add(new OptionMetadata(new Label(new LocalizedLabel("Inactive", 1033), Array.Empty<LocalizedLabel>()), 1));

        var attributeMetadata = new PicklistAttributeMetadata
        {
            LogicalName = "statecode",
            OptionSet = optionSet
        };

        // Act
        var result = _mapper.MapOptionSet(attributeMetadata);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("statecode", result!.Name);
        Assert.Equal("State Code", result.DisplayName);
        Assert.False(result.IsGlobal);
        Assert.Equal(2, result.Options.Count);
        Assert.Equal(0, result.Options[0].Value);
        Assert.Equal("Active", result.Options[0].Label);
        Assert.Equal(1, result.Options[1].Value);
        Assert.Equal("Inactive", result.Options[1].Label);
    }

    [Fact]
    public void MapOptionSet_ShouldReturnNullForNonPicklistAttribute()
    {
        // Arrange
        var attributeMetadata = new StringAttributeMetadata
        {
            LogicalName = "name"
        };

        // Act
        var result = _mapper.MapOptionSet(attributeMetadata);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void MapOneToManyRelationship_ShouldMapProperties()
    {
        // Arrange
        var relationship = new OneToManyRelationshipMetadata
        {
            SchemaName = "account_contact",
            ReferencingEntity = "contact",
            ReferencingAttribute = "parentcustomerid",
            ReferencedEntity = "account",
            ReferencedAttribute = "accountid",
            IsCustomRelationship = false
        };

        // Act
        var result = _mapper.MapOneToManyRelationship(relationship);

        // Assert
        Assert.Equal("account_contact", result.SchemaName);
        Assert.Equal("OneToMany", result.RelationshipType);
        Assert.Equal("contact", result.ReferencingEntity);
        Assert.Equal("parentcustomerid", result.ReferencingAttribute);
        Assert.Equal("account", result.ReferencedEntity);
        Assert.Equal("accountid", result.ReferencedAttribute);
        Assert.False(result.IsCustomRelationship);
    }

    [Fact]
    public void MapManyToManyRelationship_ShouldMapProperties()
    {
        // Arrange
        var relationship = new ManyToManyRelationshipMetadata
        {
            SchemaName = "contactleads_association",
            Entity1LogicalName = "contact",
            Entity2LogicalName = "lead",
            IntersectEntityName = "contactleads",
            IsCustomRelationship = true
        };

        // Act
        var result = _mapper.MapManyToManyRelationship(relationship);

        // Assert
        Assert.Equal("contactleads_association", result.SchemaName);
        Assert.Equal("ManyToMany", result.RelationshipType);
        Assert.Equal("contact", result.Entity1LogicalName);
        Assert.Equal("lead", result.Entity2LogicalName);
        Assert.Equal("contactleads", result.IntersectEntityName);
        Assert.True(result.IsCustomRelationship);
    }

    [Fact]
    public void MapAttribute_ShouldMapDecimalAttributeWithPrecision()
    {
        // Arrange
        var attributeMetadata = new DecimalAttributeMetadata
        {
            LogicalName = "amount",
            SchemaName = "Amount",
            MinValue = 0.00m,
            MaxValue = 999999.99m,
            Precision = 2
        };

        // Act
        var result = _mapper.MapAttribute(attributeMetadata);

        // Assert
        Assert.Equal("amount", result.LogicalName);
        Assert.Equal(0.00, result.MinValue);
        Assert.Equal(999999.99, result.MaxValue);
        Assert.Equal(2, result.Precision);
    }

    [Fact]
    public void MapAttribute_ShouldMapDateTimeAttribute()
    {
        // Arrange
        var attributeMetadata = new DateTimeAttributeMetadata
        {
            LogicalName = "createdon",
            SchemaName = "CreatedOn",
            Format = DateTimeFormat.DateAndTime
        };

        // Act
        var result = _mapper.MapAttribute(attributeMetadata);

        // Assert
        Assert.Equal("createdon", result.LogicalName);
        Assert.Equal("DateAndTime", result.Format);
    }

    [Fact]
    public void MapEntity_ShouldMapIsAuditEnabled_WhenTrue()
    {
        // Arrange
        var entityMetadata = new EntityMetadata
        {
            LogicalName = "account",
            SchemaName = "Account",
            IsAuditEnabled = new BooleanManagedProperty(true)
        };

        // Act
        var result = _mapper.MapEntity(entityMetadata);

        // Assert
        Assert.True(result.IsAuditEnabled);
    }

    [Fact]
    public void MapEntity_ShouldMapIsAuditEnabled_WhenFalse()
    {
        // Arrange
        var entityMetadata = new EntityMetadata
        {
            LogicalName = "account",
            SchemaName = "Account",
            IsAuditEnabled = new BooleanManagedProperty(false)
        };

        // Act
        var result = _mapper.MapEntity(entityMetadata);

        // Assert
        Assert.False(result.IsAuditEnabled);
    }

    [Fact]
    public void MapEntity_ShouldMapIsAuditEnabled_WhenNull()
    {
        // Arrange
        var entityMetadata = new EntityMetadata
        {
            LogicalName = "account",
            SchemaName = "Account",
            IsAuditEnabled = null
        };

        // Act
        var result = _mapper.MapEntity(entityMetadata);

        // Assert
        Assert.False(result.IsAuditEnabled); // Should default to false
    }

    [Fact]
    public void MapAttribute_ShouldMapIsAuditEnabled_WhenTrue()
    {
        // Arrange
        var attributeMetadata = new StringAttributeMetadata
        {
            LogicalName = "name",
            SchemaName = "Name",
            IsAuditEnabled = new BooleanManagedProperty(true)
        };

        // Act
        var result = _mapper.MapAttribute(attributeMetadata);

        // Assert
        Assert.True(result.IsAuditEnabled);
    }

    [Fact]
    public void MapAttribute_ShouldMapIsAuditEnabled_WhenFalse()
    {
        // Arrange
        var attributeMetadata = new StringAttributeMetadata
        {
            LogicalName = "name",
            SchemaName = "Name",
            IsAuditEnabled = new BooleanManagedProperty(false)
        };

        // Act
        var result = _mapper.MapAttribute(attributeMetadata);

        // Assert
        Assert.False(result.IsAuditEnabled);
    }

    [Fact]
    public void MapAttribute_ShouldMapIsAuditEnabled_WhenNull()
    {
        // Arrange
        var attributeMetadata = new StringAttributeMetadata
        {
            LogicalName = "name",
            SchemaName = "Name",
            IsAuditEnabled = null
        };

        // Act
        var result = _mapper.MapAttribute(attributeMetadata);

        // Assert
        Assert.False(result.IsAuditEnabled); // Should default to false
    }

    [Fact]
    public void MapAttribute_ShouldMapDoubleAttribute()
    {
        // Arrange
        var attributeMetadata = new DoubleAttributeMetadata
        {
            LogicalName = "latitude",
            SchemaName = "Latitude",
            MinValue = -90.0,
            MaxValue = 90.0
        };

        // Act
        var result = _mapper.MapAttribute(attributeMetadata);

        // Assert
        Assert.Equal("latitude", result.LogicalName);
        Assert.Equal("Double", result.AttributeType);
        Assert.Equal(-90.0, result.MinValue);
        Assert.Equal(90.0, result.MaxValue);
    }

    [Fact]
    public void MapAttribute_ShouldMapMoneyAttribute()
    {
        // Arrange
        var attributeMetadata = new MoneyAttributeMetadata
        {
            LogicalName = "revenue",
            SchemaName = "Revenue",
            MinValue = 0,
            MaxValue = 1000000,
            Precision = 4
        };

        // Act
        var result = _mapper.MapAttribute(attributeMetadata);

        // Assert
        Assert.Equal("revenue", result.LogicalName);
        Assert.Equal("Money", result.AttributeType);
        Assert.Equal(0, result.MinValue);
        Assert.Equal(1000000, result.MaxValue);
        Assert.Equal(4, result.Precision);
    }

    [Fact]
    public void MapAttribute_ShouldHandlePlainAttributeMetadata()
    {
        // Arrange - BooleanAttributeMetadata has no type-specific branch
        var attributeMetadata = new BooleanAttributeMetadata
        {
            LogicalName = "isactive",
            SchemaName = "IsActive"
        };

        // Act
        var result = _mapper.MapAttribute(attributeMetadata);

        // Assert
        Assert.Equal("isactive", result.LogicalName);
        Assert.Equal("Boolean", result.AttributeType);
        Assert.Null(result.MaxLength);
        Assert.Null(result.MinValue);
        Assert.Null(result.MaxValue);
        Assert.Null(result.Targets);
    }

    [Fact]
    public void MapOptionSet_ShouldMapStateAttribute()
    {
        // Arrange
        var optionSet = new OptionSetMetadata
        {
            Name = "statecode",
            DisplayName = new Label(new LocalizedLabel("Status", 1033), Array.Empty<LocalizedLabel>())
        };
        optionSet.Options.Add(new OptionMetadata(new Label(new LocalizedLabel("Active", 1033), Array.Empty<LocalizedLabel>()), 0));
        optionSet.Options.Add(new OptionMetadata(new Label(new LocalizedLabel("Inactive", 1033), Array.Empty<LocalizedLabel>()), 1));

        var attributeMetadata = new StateAttributeMetadata
        {
            LogicalName = "statecode",
            OptionSet = optionSet
        };

        // Act
        var result = _mapper.MapOptionSet(attributeMetadata);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("statecode", result!.Name);
        Assert.Equal("Status", result.DisplayName);
        Assert.False(result.IsGlobal);
        Assert.Equal(2, result.Options.Count);
        Assert.Equal("Active", result.Options[0].Label);
    }

    [Fact]
    public void MapOptionSet_ShouldMapStatusAttribute()
    {
        // Arrange
        var optionSet = new OptionSetMetadata
        {
            Name = "statuscode",
            DisplayName = new Label(new LocalizedLabel("Status Reason", 1033), Array.Empty<LocalizedLabel>())
        };
        optionSet.Options.Add(new OptionMetadata(new Label(new LocalizedLabel("Open", 1033), Array.Empty<LocalizedLabel>()), 1));
        optionSet.Options.Add(new OptionMetadata(new Label(new LocalizedLabel("Closed", 1033), Array.Empty<LocalizedLabel>()), 2));

        var attributeMetadata = new StatusAttributeMetadata
        {
            LogicalName = "statuscode",
            OptionSet = optionSet
        };

        // Act
        var result = _mapper.MapOptionSet(attributeMetadata);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("statuscode", result!.Name);
        Assert.Equal("Status Reason", result.DisplayName);
        Assert.False(result.IsGlobal);
        Assert.Equal(2, result.Options.Count);
    }

    [Fact]
    public void MapOptionSet_ShouldReturnNullForPicklistWithNullOptionSet()
    {
        // Arrange
        var attributeMetadata = new PicklistAttributeMetadata
        {
            LogicalName = "priority",
            OptionSet = null
        };

        // Act
        var result = _mapper.MapOptionSet(attributeMetadata);

        // Assert
        Assert.Null(result);
    }
}
