using PowerApps.CLI.Models;
using PowerApps.CLI.Services;
using Xunit;

namespace PowerApps.CLI.Tests.Services;

public class ConstantsFilterTests
{
    [Fact]
    public void FilterEntities_NoExclusions_ReturnsAllEntities()
    {
        // Arrange
        var filter = new ConstantsFilter();
        var config = new ConstantsConfig { ExcludeEntities = new List<string>() };
        var entities = new List<EntitySchema>
        {
            new EntitySchema { LogicalName = "contact" },
            new EntitySchema { LogicalName = "account" },
            new EntitySchema { LogicalName = "lead" }
        };

        // Act
        var result = filter.FilterEntities(entities, config);

        // Assert
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void FilterEntities_WithExclusions_RemovesExcludedEntities()
    {
        // Arrange
        var filter = new ConstantsFilter();
        var config = new ConstantsConfig 
        { 
            ExcludeEntities = new List<string> { "systemuser", "team" }
        };
        var entities = new List<EntitySchema>
        {
            new EntitySchema { LogicalName = "contact" },
            new EntitySchema { LogicalName = "systemuser" },
            new EntitySchema { LogicalName = "team" },
            new EntitySchema { LogicalName = "account" }
        };

        // Act
        var result = filter.FilterEntities(entities, config);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, e => e.LogicalName == "contact");
        Assert.Contains(result, e => e.LogicalName == "account");
        Assert.DoesNotContain(result, e => e.LogicalName == "systemuser");
        Assert.DoesNotContain(result, e => e.LogicalName == "team");
    }

    [Fact]
    public void FilterEntities_CaseInsensitive_RemovesEntities()
    {
        // Arrange
        var filter = new ConstantsFilter();
        var config = new ConstantsConfig 
        { 
            ExcludeEntities = new List<string> { "CONTACT" }
        };
        var entities = new List<EntitySchema>
        {
            new EntitySchema { LogicalName = "contact" },
            new EntitySchema { LogicalName = "account" }
        };

        // Act
        var result = filter.FilterEntities(entities, config);

        // Assert
        Assert.Single(result);
        Assert.Equal("account", result[0].LogicalName);
    }

    [Fact]
    public void FilterAttributes_NoExclusions_ReturnsAllAttributes()
    {
        // Arrange
        var filter = new ConstantsFilter();
        var config = new ConstantsConfig 
        { 
            ExcludeAttributes = new List<string>(),
            AttributePrefix = null
        };
        var entity = new EntitySchema
        {
            LogicalName = "contact",
            Attributes = new List<AttributeSchema>
            {
                new AttributeSchema { LogicalName = "firstname" },
                new AttributeSchema { LogicalName = "lastname" },
                new AttributeSchema { LogicalName = "emailaddress1" }
            }
        };

        // Act
        var result = filter.FilterAttributes(entity, config);

        // Assert
        Assert.Equal(3, result.Attributes.Count);
    }

    [Fact]
    public void FilterAttributes_WithExclusions_RemovesExcludedAttributes()
    {
        // Arrange
        var filter = new ConstantsFilter();
        var config = new ConstantsConfig 
        { 
            ExcludeAttributes = new List<string> { "createdon", "modifiedon", "createdby" }
        };
        var entity = new EntitySchema
        {
            LogicalName = "contact",
            Attributes = new List<AttributeSchema>
            {
                new AttributeSchema { LogicalName = "firstname" },
                new AttributeSchema { LogicalName = "createdon" },
                new AttributeSchema { LogicalName = "modifiedon" },
                new AttributeSchema { LogicalName = "lastname" }
            }
        };

        // Act
        var result = filter.FilterAttributes(entity, config);

        // Assert
        Assert.Equal(2, result.Attributes.Count);
        Assert.Contains(result.Attributes, a => a.LogicalName == "firstname");
        Assert.Contains(result.Attributes, a => a.LogicalName == "lastname");
        Assert.DoesNotContain(result.Attributes, a => a.LogicalName == "createdon");
    }

    [Fact]
    public void FilterAttributes_WithPrefix_ReturnsOnlyPrefixedAttributes()
    {
        // Arrange
        var filter = new ConstantsFilter();
        var config = new ConstantsConfig 
        { 
            AttributePrefix = "new_"
        };
        var entity = new EntitySchema
        {
            LogicalName = "contact",
            Attributes = new List<AttributeSchema>
            {
                new AttributeSchema { LogicalName = "firstname" },
                new AttributeSchema { LogicalName = "new_customfield1" },
                new AttributeSchema { LogicalName = "new_customfield2" },
                new AttributeSchema { LogicalName = "lastname" }
            }
        };

        // Act
        var result = filter.FilterAttributes(entity, config);

        // Assert
        Assert.Equal(2, result.Attributes.Count);
        Assert.All(result.Attributes, a => Assert.StartsWith("new_", a.LogicalName));
    }

    [Fact]
    public void FilterAttributes_PrefixCaseInsensitive_FiltersCorrectly()
    {
        // Arrange
        var filter = new ConstantsFilter();
        var config = new ConstantsConfig 
        { 
            AttributePrefix = "NEW_"
        };
        var entity = new EntitySchema
        {
            LogicalName = "contact",
            Attributes = new List<AttributeSchema>
            {
                new AttributeSchema { LogicalName = "new_customfield" },
                new AttributeSchema { LogicalName = "firstname" }
            }
        };

        // Act
        var result = filter.FilterAttributes(entity, config);

        // Assert
        Assert.Single(result.Attributes);
        Assert.Equal("new_customfield", result.Attributes[0].LogicalName);
    }

    [Fact]
    public void FilterAttributes_BothPrefixAndExclusions_AppliesBothFilters()
    {
        // Arrange
        var filter = new ConstantsFilter();
        var config = new ConstantsConfig 
        { 
            AttributePrefix = "new_",
            ExcludeAttributes = new List<string> { "new_excludeme" }
        };
        var entity = new EntitySchema
        {
            LogicalName = "contact",
            Attributes = new List<AttributeSchema>
            {
                new AttributeSchema { LogicalName = "firstname" },
                new AttributeSchema { LogicalName = "new_customfield" },
                new AttributeSchema { LogicalName = "new_excludeme" },
                new AttributeSchema { LogicalName = "new_anotherfield" }
            }
        };

        // Act
        var result = filter.FilterAttributes(entity, config);

        // Assert
        Assert.Equal(2, result.Attributes.Count);
        Assert.Contains(result.Attributes, a => a.LogicalName == "new_customfield");
        Assert.Contains(result.Attributes, a => a.LogicalName == "new_anotherfield");
        Assert.DoesNotContain(result.Attributes, a => a.LogicalName == "new_excludeme");
    }

    [Fact]
    public void FilterAttributes_PreservesEntityMetadata()
    {
        // Arrange
        var filter = new ConstantsFilter();
        var config = new ConstantsConfig();
        var entity = new EntitySchema
        {
            LogicalName = "contact",
            SchemaName = "Contact",
            DisplayName = "Contact",
            Description = "Person or organization",
            PrimaryIdAttribute = "contactid",
            PrimaryNameAttribute = "fullname",
            IsCustomEntity = false,
            Attributes = new List<AttributeSchema>()
        };

        // Act
        var result = filter.FilterAttributes(entity, config);

        // Assert
        Assert.Equal("contact", result.LogicalName);
        Assert.Equal("Contact", result.SchemaName);
        Assert.Equal("Contact", result.DisplayName);
        Assert.Equal("Person or organization", result.Description);
        Assert.Equal("contactid", result.PrimaryIdAttribute);
        Assert.Equal("fullname", result.PrimaryNameAttribute);
        Assert.False(result.IsCustomEntity);
    }

    [Fact]
    public void FilterAttributes_SkipVirtualFields_RemovesDerivedAttributes()
    {
        // Arrange
        var filter = new ConstantsFilter();
        var config = new ConstantsConfig { SkipVirtualFields = true };
        var entity = new EntitySchema
        {
            LogicalName = "contact",
            Attributes = new List<AttributeSchema>
            {
                new AttributeSchema { LogicalName = "firstname" },
                new AttributeSchema { LogicalName = "createdbyname", AttributeOf = "createdby" },
                new AttributeSchema { LogicalName = "owneridname", AttributeOf = "ownerid" },
                new AttributeSchema { LogicalName = "lastname" }
            }
        };

        // Act
        var result = filter.FilterAttributes(entity, config);

        // Assert
        Assert.Equal(2, result.Attributes.Count);
        Assert.Contains(result.Attributes, a => a.LogicalName == "firstname");
        Assert.Contains(result.Attributes, a => a.LogicalName == "lastname");
        Assert.DoesNotContain(result.Attributes, a => a.LogicalName == "createdbyname");
        Assert.DoesNotContain(result.Attributes, a => a.LogicalName == "owneridname");
    }

    [Fact]
    public void FilterAttributes_SkipVirtualFieldsFalse_RetainsDerivedAttributes()
    {
        // Arrange
        var filter = new ConstantsFilter();
        var config = new ConstantsConfig { SkipVirtualFields = false };
        var entity = new EntitySchema
        {
            LogicalName = "contact",
            Attributes = new List<AttributeSchema>
            {
                new AttributeSchema { LogicalName = "firstname" },
                new AttributeSchema { LogicalName = "createdbyname", AttributeOf = "createdby" }
            }
        };

        // Act
        var result = filter.FilterAttributes(entity, config);

        // Assert
        Assert.Equal(2, result.Attributes.Count);
        Assert.Contains(result.Attributes, a => a.LogicalName == "createdbyname");
    }

    [Fact]
    public void FilterAttributes_SkipVirtualFields_RetainsAttributesWithNoAttributeOf()
    {
        // Arrange
        var filter = new ConstantsFilter();
        var config = new ConstantsConfig { SkipVirtualFields = true };
        var entity = new EntitySchema
        {
            LogicalName = "contact",
            Attributes = new List<AttributeSchema>
            {
                new AttributeSchema { LogicalName = "firstname", AttributeOf = null },
                new AttributeSchema { LogicalName = "lastname", AttributeOf = string.Empty }
            }
        };

        // Act
        var result = filter.FilterAttributes(entity, config);

        // Assert
        Assert.Equal(2, result.Attributes.Count);
    }

    [Fact]
    public void FilterAttributes_SkipVirtualFieldsWithExclusions_AppliesBothFilters()
    {
        // Arrange
        var filter = new ConstantsFilter();
        var config = new ConstantsConfig
        {
            SkipVirtualFields = true,
            ExcludeAttributes = new List<string> { "createdon" }
        };
        var entity = new EntitySchema
        {
            LogicalName = "contact",
            Attributes = new List<AttributeSchema>
            {
                new AttributeSchema { LogicalName = "firstname" },
                new AttributeSchema { LogicalName = "createdon" },
                new AttributeSchema { LogicalName = "createdbyname", AttributeOf = "createdby" },
                new AttributeSchema { LogicalName = "lastname" }
            }
        };

        // Act
        var result = filter.FilterAttributes(entity, config);

        // Assert
        Assert.Equal(2, result.Attributes.Count);
        Assert.Contains(result.Attributes, a => a.LogicalName == "firstname");
        Assert.Contains(result.Attributes, a => a.LogicalName == "lastname");
    }

    [Fact]
    public void FilterAttributes_SkipVirtualFields_RemovesVirtualTypeAttributes()
    {
        // Arrange
        var filter = new ConstantsFilter();
        var config = new ConstantsConfig { SkipVirtualFields = true };
        var entity = new EntitySchema
        {
            LogicalName = "contact",
            Attributes = new List<AttributeSchema>
            {
                new AttributeSchema { LogicalName = "firstname" },
                new AttributeSchema { LogicalName = "entityimage_url", AttributeType = "Virtual" },
                new AttributeSchema { LogicalName = "entityimage_timestamp", AttributeType = "Virtual" },
                new AttributeSchema { LogicalName = "lastname" }
            }
        };

        // Act
        var result = filter.FilterAttributes(entity, config);

        // Assert
        Assert.Equal(2, result.Attributes.Count);
        Assert.Contains(result.Attributes, a => a.LogicalName == "firstname");
        Assert.Contains(result.Attributes, a => a.LogicalName == "lastname");
        Assert.DoesNotContain(result.Attributes, a => a.LogicalName == "entityimage_url");
        Assert.DoesNotContain(result.Attributes, a => a.LogicalName == "entityimage_timestamp");
    }

    [Fact]
    public void FilterAttributes_SkipVirtualFields_RemovesBothAttributeOfAndVirtualType()
    {
        // Arrange
        var filter = new ConstantsFilter();
        var config = new ConstantsConfig { SkipVirtualFields = true };
        var entity = new EntitySchema
        {
            LogicalName = "contact",
            Attributes = new List<AttributeSchema>
            {
                new AttributeSchema { LogicalName = "firstname" },
                new AttributeSchema { LogicalName = "createdbyname", AttributeOf = "createdby" },
                new AttributeSchema { LogicalName = "entityimage_url", AttributeType = "Virtual" },
                new AttributeSchema { LogicalName = "lastname" }
            }
        };

        // Act
        var result = filter.FilterAttributes(entity, config);

        // Assert
        Assert.Equal(2, result.Attributes.Count);
        Assert.Contains(result.Attributes, a => a.LogicalName == "firstname");
        Assert.Contains(result.Attributes, a => a.LogicalName == "lastname");
        Assert.DoesNotContain(result.Attributes, a => a.LogicalName == "createdbyname");
        Assert.DoesNotContain(result.Attributes, a => a.LogicalName == "entityimage_url");
    }

    [Fact]
    public void ExtractGlobalOptionSets_NoGlobalOptionSets_ReturnsEmptyList()
    {
        // Arrange
        var filter = new ConstantsFilter();
        var entities = new List<EntitySchema>
        {
            new EntitySchema
            {
                LogicalName = "contact",
                Attributes = new List<AttributeSchema>
                {
                    new AttributeSchema 
                    { 
                        LogicalName = "statecode",
                        OptionSet = new OptionSetSchema { IsGlobal = false }
                    }
                }
            }
        };

        // Act
        var result = filter.ExtractGlobalOptionSets(entities);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ExtractGlobalOptionSets_WithGlobalOptionSets_ReturnsUniqueSets()
    {
        // Arrange
        var filter = new ConstantsFilter();
        var entities = new List<EntitySchema>
        {
            new EntitySchema
            {
                LogicalName = "contact",
                Attributes = new List<AttributeSchema>
                {
                    new AttributeSchema 
                    { 
                        LogicalName = "preferredcontactmethodcode",
                        OptionSet = new OptionSetSchema 
                        { 
                            Name = "contactmethod",
                            IsGlobal = true,
                            Options = new List<OptionSchema>
                            {
                                new OptionSchema { Value = 1, Label = "Email" }
                            }
                        }
                    }
                }
            },
            new EntitySchema
            {
                LogicalName = "lead",
                Attributes = new List<AttributeSchema>
                {
                    new AttributeSchema 
                    { 
                        LogicalName = "preferredcontactmethodcode",
                        OptionSet = new OptionSetSchema 
                        { 
                            Name = "contactmethod",
                            IsGlobal = true,
                            Options = new List<OptionSchema>
                            {
                                new OptionSchema { Value = 1, Label = "Email" }
                            }
                        }
                    }
                }
            }
        };

        // Act
        var result = filter.ExtractGlobalOptionSets(entities);

        // Assert
        Assert.Single(result);
        Assert.Equal("contactmethod", result[0].Name);
    }

    [Fact]
    public void ExtractGlobalOptionSets_MultipleGlobalOptionSets_ReturnsSortedList()
    {
        // Arrange
        var filter = new ConstantsFilter();
        var entities = new List<EntitySchema>
        {
            new EntitySchema
            {
                LogicalName = "contact",
                Attributes = new List<AttributeSchema>
                {
                    new AttributeSchema 
                    { 
                        LogicalName = "field1",
                        OptionSet = new OptionSetSchema 
                        { 
                            Name = "zebra_optionset",
                            IsGlobal = true
                        }
                    },
                    new AttributeSchema 
                    { 
                        LogicalName = "field2",
                        OptionSet = new OptionSetSchema 
                        { 
                            Name = "alpha_optionset",
                            IsGlobal = true
                        }
                    }
                }
            }
        };

        // Act
        var result = filter.ExtractGlobalOptionSets(entities);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("alpha_optionset", result[0].Name);
        Assert.Equal("zebra_optionset", result[1].Name);
    }
}
