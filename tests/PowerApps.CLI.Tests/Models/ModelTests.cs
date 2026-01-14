using PowerApps.CLI.Models;
using Xunit;

namespace PowerApps.CLI.Tests.Models;

public class ModelTests
{
    [Fact]
    public void PowerAppsSchema_ToString_WithSingleSolution_ShouldReturnOrganisationAndSolution()
    {
        // Arrange
        var schema = new PowerAppsSchema
        {
            OrganisationName = "Contoso",
            SolutionNames = new List<string> { "SalesApp" }
        };

        // Act
        var result = schema.ToString();

        // Assert
        Assert.Equal("Contoso  (SalesApp)", result);
    }

    [Fact]
    public void PowerAppsSchema_ToString_WithoutSolutionName_ShouldReturnOrganisationOnly()
    {
        // Arrange
        var schema = new PowerAppsSchema
        {
            OrganisationName = "Contoso"
        };

        // Act
        var result = schema.ToString();

        // Assert
        Assert.Equal("Contoso", result);
    }

    [Fact]
    public void PowerAppsSchema_ToString_WithMultipleSolutions_ShouldReturnCommaSeparated()
    {
        // Arrange
        var schema = new PowerAppsSchema
        {
            OrganisationName = "Contoso",
            SolutionNames = new List<string> { "RobSolution1", "RobSolution2", "RobSolution3" }
        };

        // Act
        var result = schema.ToString();

        // Assert
        Assert.Equal("Contoso  (RobSolution1, RobSolution2, RobSolution3)", result);
    }

    [Fact]
    public void EntitySchema_ToString_WithDisplayName_ShouldReturnDisplayName()
    {
        // Arrange
        var entity = new EntitySchema
        {
            LogicalName = "account",
            DisplayName = "Account"
        };

        // Act
        var result = entity.ToString();

        // Assert
        Assert.Equal("Account", result);
    }

    [Fact]
    public void EntitySchema_ToString_WithoutDisplayName_ShouldReturnLogicalName()
    {
        // Arrange
        var entity = new EntitySchema
        {
            LogicalName = "account"
        };

        // Act
        var result = entity.ToString();

        // Assert
        Assert.Equal("account", result);
    }

    [Fact]
    public void AttributeSchema_ToString_WithDisplayName_ShouldReturnDisplayName()
    {
        // Arrange
        var attribute = new AttributeSchema
        {
            LogicalName = "name",
            SchemaName = "Name",
            DisplayName = "Account Name"
        };

        // Act
        var result = attribute.ToString();

        // Assert
        Assert.Equal("Account Name", result);
    }

    [Fact]
    public void AttributeSchema_ToString_WithoutDisplayName_ShouldReturnLogicalName()
    {
        // Arrange
        var attribute = new AttributeSchema
        {
            LogicalName = "name",
            SchemaName = "Name"
        };

        // Act
        var result = attribute.ToString();

        // Assert
        Assert.Equal("name", result);
    }

    [Fact]
    public void AttributeSchema_ToString_WithOnlySchemaName_ShouldReturnLogicalName()
    {
        // Arrange - LogicalName defaults to empty string, so it will be used over SchemaName
        var attribute = new AttributeSchema
        {
            SchemaName = "Name"
        };

        // Act
        var result = attribute.ToString();

        // Assert
        Assert.Equal("", result);
    }

    [Fact]
    public void OptionSetSchema_ToString_WithName_ShouldReturnName()
    {
        // Arrange
        var optionSet = new OptionSetSchema
        {
            Name = "account_industrycode"
        };

        // Act
        var result = optionSet.ToString();

        // Assert
        Assert.Equal("account_industrycode", result);
    }

    [Fact]
    public void OptionSetSchema_ToString_WithoutName_ShouldReturnUnknownOptionSet()
    {
        // Arrange
        var optionSet = new OptionSetSchema();

        // Act
        var result = optionSet.ToString();

        // Assert
        Assert.Equal("Unknown OptionSet", result);
    }

    [Fact]
    public void OptionSchema_ToString_WithLabel_ShouldReturnLabel()
    {
        // Arrange
        var option = new OptionSchema
        {
            Value = 1,
            Label = "Accounting"
        };

        // Act
        var result = option.ToString();

        // Assert
        Assert.Equal("Accounting", result);
    }

    [Fact]
    public void OptionSchema_ToString_WithoutLabel_ShouldReturnValue()
    {
        // Arrange
        var option = new OptionSchema
        {
            Value = 42
        };

        // Act
        var result = option.ToString();

        // Assert
        Assert.Equal("42", result);
    }

    [Fact]
    public void RelationshipSchema_ToString_OneToMany_ShouldFormatCorrectly()
    {
        // Arrange
        var relationship = new RelationshipSchema
        {
            SchemaName = "account_primary_contact",
            RelationshipType = "OneToMany",
            ReferencedEntity = "contact",
            ReferencedAttribute = "contactid",
            ReferencingEntity = "account",
            ReferencingAttribute = "primarycontactid",
            IsCustomRelationship = false
        };

        // Act
        var result = relationship.ToString();

        // Assert
        Assert.Contains("account_primary_contact", result);
        Assert.Contains("1:N", result);
        Assert.Contains("contact.contactid", result);
        Assert.Contains("account.primarycontactid", result);
        Assert.Contains("Custom: False", result);
    }

    [Fact]
    public void RelationshipSchema_ToString_ManyToMany_ShouldFormatCorrectly()
    {
        // Arrange
        var relationship = new RelationshipSchema
        {
            SchemaName = "accountleads_association",
            RelationshipType = "ManyToMany",
            Entity1LogicalName = "account",
            Entity2LogicalName = "lead",
            IntersectEntityName = "accountleads",
            IsCustomRelationship = true
        };

        // Act
        var result = relationship.ToString();

        // Assert
        Assert.Contains("accountleads_association", result);
        Assert.Contains("N:N", result);
        Assert.Contains("account", result);
        Assert.Contains("lead", result);
        Assert.Contains("accountleads", result);
        Assert.Contains("Custom: True", result);
    }

    [Fact]
    public void RelationshipSchema_ToString_UnknownType_ShouldIncludeTypeInOutput()
    {
        // Arrange
        var relationship = new RelationshipSchema
        {
            SchemaName = "unknown_relationship",
            RelationshipType = "Unknown"
        };

        // Act
        var result = relationship.ToString();

        // Assert
        Assert.Contains("unknown_relationship", result);
        Assert.Contains("Unknown", result);
        Assert.Contains("Custom: False", result);
    }

    [Fact]
    public void ConstantsConfig_ToString_WithAllOptionsEnabled_ShouldListAllTypes()
    {
        // Arrange
        var config = new ConstantsConfig
        {
            IncludeEntities = true,
            IncludeGlobalOptionSets = true,
            IncludeReferenceData = true
        };

        // Act
        var result = config.ToString();

        // Assert
        Assert.Equal("Entities, OptionSets, RefData", result);
    }

    [Fact]
    public void ConstantsConfig_ToString_WithOnlyEntities_ShouldReturnEntitiesOnly()
    {
        // Arrange
        var config = new ConstantsConfig
        {
            IncludeEntities = true,
            IncludeGlobalOptionSets = false,
            IncludeReferenceData = false
        };

        // Act
        var result = config.ToString();

        // Assert
        Assert.Equal("Entities", result);
    }

    [Fact]
    public void ConstantsConfig_ToString_WithNoOptionsEnabled_ShouldReturnNoGeneration()
    {
        // Arrange
        var config = new ConstantsConfig
        {
            IncludeEntities = false,
            IncludeGlobalOptionSets = false,
            IncludeReferenceData = false
        };

        // Act
        var result = config.ToString();

        // Assert
        Assert.Equal("No generation configured", result);
    }

    [Fact]
    public void ConstantsConfig_DefaultValues_ShouldHaveCorrectDefaults()
    {
        // Arrange & Act
        var config = new ConstantsConfig();

        // Assert
        Assert.False(config.SingleFile);
        Assert.True(config.IncludeEntities);
        Assert.True(config.IncludeGlobalOptionSets);
        Assert.False(config.IncludeReferenceData);
        Assert.True(config.IncludeComments);
        Assert.True(config.IncludeRelationships);
        Assert.True(config.PascalCaseConversion);
        Assert.Null(config.AttributePrefix);
        Assert.Empty(config.ExcludeAttributes);
        Assert.Empty(config.ExcludeEntities);
    }

    [Fact]
    public void ConstantsOutputConfig_ToString_ShouldReturnNamespaceAndPath()
    {
        // Arrange
        var outputConfig = new ConstantsOutputConfig
        {
            Namespace = "MyCompany.Constants",
            OutputPath = "./Generated/Constants"
        };

        // Act
        var result = outputConfig.ToString();

        // Assert
        Assert.Equal("MyCompany.Constants -> ./Generated/Constants", result);
    }

    [Fact]
    public void ConstantsOutputConfig_DefaultValues_ShouldHaveCorrectDefaults()
    {
        // Arrange & Act
        var config = new ConstantsOutputConfig();

        // Assert
        Assert.Equal(string.Empty, config.OutputPath);
        Assert.Equal(string.Empty, config.Namespace);
        Assert.False(config.SingleFile);
        Assert.True(config.IncludeEntities);
        Assert.True(config.IncludeGlobalOptionSets);
        Assert.True(config.IncludeRelationships);
        Assert.False(config.IncludeReferenceData);
        Assert.True(config.IncludeComments);
        Assert.True(config.PascalCaseConversion);
        Assert.Empty(config.ExcludeAttributes);
    }
}
