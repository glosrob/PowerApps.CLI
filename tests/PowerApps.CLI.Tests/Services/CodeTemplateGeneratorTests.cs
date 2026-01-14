using PowerApps.CLI.Models;
using PowerApps.CLI.Services;
using Xunit;

namespace PowerApps.CLI.Tests.Services;

public class CodeTemplateGeneratorTests
{
    [Fact]
    public void GenerateEntityClass_BasicEntity_GeneratesCorrectClass()
    {
        // Arrange
        var formatter = new IdentifierFormatter();
        var generator = new CodeTemplateGenerator(includeComments: true, includeRelationships: true, formatter);
        var entity = new EntitySchema
        {
            LogicalName = "contact",
            DisplayName = "Contact",
            SchemaName = "Contact",
            PrimaryIdAttribute = "contactid",
            PrimaryNameAttribute = "fullname",
            Attributes = new List<AttributeSchema>()
        };

        // Act
        var result = generator.GenerateEntityClass(entity, "MyCompany.Constants");

        // Assert
        Assert.Contains("namespace MyCompany.Constants", result);
        Assert.Contains("public static class Contact", result);
        Assert.Contains("public const string EntityLogicalName = \"contact\";", result);
        Assert.Contains("public const string PrimaryIdAttribute = \"contactid\";", result);
        Assert.Contains("public const string PrimaryNameAttribute = \"fullname\";", result);
    }

    [Fact]
    public void GenerateEntityClass_WithAttribute_GeneratesAttributeConstant()
    {
        // Arrange
        var formatter = new IdentifierFormatter();
        var generator = new CodeTemplateGenerator(includeComments: true, includeRelationships: true, formatter);
        var entity = new EntitySchema
        {
            LogicalName = "contact",
            DisplayName = "Contact",
            Attributes = new List<AttributeSchema>
            {
                new AttributeSchema
                {
                    LogicalName = "firstname",
                    DisplayName = "First Name",
                    AttributeType = "String",
                    MaxLength = 100
                }
            }
        };

        // Act
        var result = generator.GenerateEntityClass(entity, "MyCompany.Constants");

        // Assert
        Assert.Contains("public const string FirstName = \"firstname\";", result);
        Assert.Contains("/// firstname (String) - MaxLength: 100", result);
    }

    [Fact]
    public void GenerateEntityClass_WithStateCode_GeneratesStateCodeClass()
    {
        // Arrange
        var formatter = new IdentifierFormatter();
        var generator = new CodeTemplateGenerator(includeComments: true, includeRelationships: true, formatter);
        var entity = new EntitySchema
        {
            LogicalName = "contact",
            DisplayName = "Contact",
            Attributes = new List<AttributeSchema>
            {
                new AttributeSchema
                {
                    LogicalName = "statecode",
                    AttributeType = "State",
                    OptionSet = new OptionSetSchema
                    {
                        IsGlobal = false,
                        Options = new List<OptionSchema>
                        {
                            new OptionSchema { Value = 0, Label = "Active" },
                            new OptionSchema { Value = 1, Label = "Inactive" }
                        }
                    }
                }
            }
        };

        // Act
        var result = generator.GenerateEntityClass(entity, "MyCompany.Constants");

        // Assert
        Assert.Contains("public static class StateCode", result);
        Assert.Contains("public const int Active = 0;", result);
        Assert.Contains("public const int Inactive = 1;", result);
    }

    [Fact]
    public void GenerateEntityClass_WithStatusCode_GeneratesStatusCodeClass()
    {
        // Arrange
        var formatter = new IdentifierFormatter();
        var generator = new CodeTemplateGenerator(includeComments: true, includeRelationships: true, formatter);
        var entity = new EntitySchema
        {
            LogicalName = "contact",
            DisplayName = "Contact",
            Attributes = new List<AttributeSchema>
            {
                new AttributeSchema
                {
                    LogicalName = "statuscode",
                    AttributeType = "Status",
                    OptionSet = new OptionSetSchema
                    {
                        IsGlobal = false,
                        Options = new List<OptionSchema>
                        {
                            new OptionSchema { Value = 1, Label = "Active" },
                            new OptionSchema { Value = 2, Label = "Inactive" }
                        }
                    }
                }
            }
        };

        // Act
        var result = generator.GenerateEntityClass(entity, "MyCompany.Constants");

        // Assert
        Assert.Contains("public static class StatusCode", result);
        Assert.Contains("public const int Active = 1;", result);
        Assert.Contains("public const int Inactive = 2;", result);
    }

    [Fact]
    public void GenerateEntityClass_WithLocalOptionSet_GeneratesOptionSetClass()
    {
        // Arrange
        var formatter = new IdentifierFormatter();
        var generator = new CodeTemplateGenerator(includeComments: true, includeRelationships: true, formatter);
        var entity = new EntitySchema
        {
            LogicalName = "contact",
            DisplayName = "Contact",
            Attributes = new List<AttributeSchema>
            {
                new AttributeSchema
                {
                    LogicalName = "preferredcontactmethodcode",
                    DisplayName = "Preferred Method of Contact",
                    AttributeType = "Picklist",
                    OptionSet = new OptionSetSchema
                    {
                        IsGlobal = false,
                        Options = new List<OptionSchema>
                        {
                            new OptionSchema { Value = 1, Label = "Email" },
                            new OptionSchema { Value = 2, Label = "Phone" },
                            new OptionSchema { Value = 3, Label = "Mail" }
                        }
                    }
                }
            }
        };

        // Act
        var result = generator.GenerateEntityClass(entity, "MyCompany.Constants");

        // Assert
        Assert.Contains("public static class PreferredMethodOfContactOptions", result);
        Assert.Contains("public const int Email = 1;", result);
        Assert.Contains("public const int Phone = 2;", result);
        Assert.Contains("public const int Mail = 3;", result);
    }

    [Fact]
    public void GenerateEntityClass_WithCommentsDisabled_DoesNotGenerateComments()
    {
        // Arrange
        var formatter = new IdentifierFormatter();
        var generator = new CodeTemplateGenerator(includeComments: false, includeRelationships: true, formatter);
        var entity = new EntitySchema
        {
            LogicalName = "contact",
            DisplayName = "Contact",
            Attributes = new List<AttributeSchema>
            {
                new AttributeSchema
                {
                    LogicalName = "firstname",
                    DisplayName = "First Name",
                    AttributeType = "String"
                }
            }
        };

        // Act
        var result = generator.GenerateEntityClass(entity, "MyCompany.Constants");

        // Assert
        Assert.DoesNotContain("///", result);
        Assert.DoesNotContain("<summary>", result);
    }

    [Fact]
    public void GenerateGlobalOptionSetClass_BasicOptionSet_GeneratesCorrectClass()
    {
        // Arrange
        var formatter = new IdentifierFormatter();
        var generator = new CodeTemplateGenerator(includeComments: true, includeRelationships: true, formatter);
        var optionSet = new OptionSetSchema
        {
            Name = "account_statuscode",
            IsGlobal = true,
            Options = new List<OptionSchema>
            {
                new OptionSchema { Value = 1, Label = "Active" },
                new OptionSchema { Value = 2, Label = "Inactive" }
            }
        };

        // Act
        var result = generator.GenerateGlobalOptionSetClass(optionSet, "MyCompany.Constants");

        // Assert
        Assert.Contains("namespace MyCompany.Constants", result);
        Assert.Contains("public static class AccountStatuscode", result);
        Assert.Contains("public const int Active = 1;", result);
        Assert.Contains("public const int Inactive = 2;", result);
    }

    [Fact]
    public void GenerateGlobalOptionSetClass_WithSpecialCharacters_SanitisesClassName()
    {
        // Arrange
        var formatter = new IdentifierFormatter();
        var generator = new CodeTemplateGenerator(includeComments: true, includeRelationships: true, formatter);
        var optionSet = new OptionSetSchema
        {
            Name = "contact_preferred-method",
            IsGlobal = true,
            Options = new List<OptionSchema>
            {
                new OptionSchema { Value = 1, Label = "Email" }
            }
        };

        // Act
        var result = generator.GenerateGlobalOptionSetClass(optionSet, "MyCompany.Constants");

        // Assert
        Assert.Contains("public static class ContactPreferredMethod", result);
    }

    [Fact]
    public void GenerateSingleFile_MultipleClasses_CombinesIntoOneFile()
    {
        // Arrange
        var formatter = new IdentifierFormatter();
        var generator = new CodeTemplateGenerator(includeComments: false, includeRelationships: true, formatter);
        
        var entity1 = new EntitySchema
        {
            LogicalName = "contact",
            DisplayName = "Contact",
            Attributes = new List<AttributeSchema>()
        };
        
        var entity2 = new EntitySchema
        {
            LogicalName = "account",
            DisplayName = "Account",
            Attributes = new List<AttributeSchema>()
        };

        var class1 = generator.GenerateEntityClass(entity1, "MyCompany.Constants");
        var class2 = generator.GenerateEntityClass(entity2, "MyCompany.Constants");

        // Act
        var result = generator.GenerateSingleFile("MyCompany.Constants", new[] { class1, class2 });

        // Assert
        Assert.Contains("namespace MyCompany.Constants", result);
        Assert.Contains("public static class Contact", result);
        Assert.Contains("public static class Account", result);
        Assert.Equal(1, result.Split("namespace MyCompany.Constants").Length - 1); // Only one namespace declaration
    }

    [Fact]
    public void GenerateEntityClass_WithLookupAttribute_IncludesTargets()
    {
        // Arrange
        var formatter = new IdentifierFormatter();
        var generator = new CodeTemplateGenerator(includeComments: true, includeRelationships: true, formatter);
        var entity = new EntitySchema
        {
            LogicalName = "contact",
            DisplayName = "Contact",
            Attributes = new List<AttributeSchema>
            {
                new AttributeSchema
                {
                    LogicalName = "parentcustomerid",
                    DisplayName = "Company Name",
                    AttributeType = "Lookup",
                    Targets = new[] { "account", "contact" }
                }
            }
        };

        // Act
        var result = generator.GenerateEntityClass(entity, "MyCompany.Constants");

        // Assert
        Assert.Contains("/// parentcustomerid (Lookup) - Targets: account, contact", result);
    }

    [Fact]
    public void GenerateEntityClass_WithGlobalOptionSet_IndicatesGlobal()
    {
        // Arrange
        var formatter = new IdentifierFormatter();
        var generator = new CodeTemplateGenerator(includeComments: true, includeRelationships: true, formatter);
        var entity = new EntitySchema
        {
            LogicalName = "contact",
            DisplayName = "Contact",
            Attributes = new List<AttributeSchema>
            {
                new AttributeSchema
                {
                    LogicalName = "customertypecode",
                    DisplayName = "Relationship Type",
                    AttributeType = "Picklist",
                    OptionSet = new OptionSetSchema
                    {
                        Name = "customertype",
                        IsGlobal = true,
                        Options = new List<OptionSchema>()
                    }
                }
            }
        };

        // Act
        var result = generator.GenerateEntityClass(entity, "MyCompany.Constants");

        // Assert
        Assert.Contains("Uses global option set: customertype", result);
        Assert.DoesNotContain("public static class RelationshipTypeOptions", result); // Should not generate nested class for global
    }

    [Fact]
    public void GenerateEntityClass_WithNoPrimaryAttributes_OmitsThoseConstants()
    {
        // Arrange
        var formatter = new IdentifierFormatter();
        var generator = new CodeTemplateGenerator(includeComments: true, includeRelationships: true, formatter);
        var entity = new EntitySchema
        {
            LogicalName = "activitypointer",
            DisplayName = "Activity",
            PrimaryIdAttribute = null,
            PrimaryNameAttribute = null,
            Attributes = new List<AttributeSchema>()
        };

        // Act
        var result = generator.GenerateEntityClass(entity, "MyCompany.Constants");

        // Assert
        Assert.Contains("public const string EntityLogicalName = \"activitypointer\";", result);
        Assert.DoesNotContain("PrimaryIdAttribute", result);
        Assert.DoesNotContain("PrimaryNameAttribute", result);
    }

    [Fact]
    public void GenerateEntityClass_OptionWithoutLabel_UsesValueAsFallback()
    {
        // Arrange
        var formatter = new IdentifierFormatter();
        var generator = new CodeTemplateGenerator(includeComments: true, includeRelationships: true, formatter);
        var entity = new EntitySchema
        {
            LogicalName = "contact",
            DisplayName = "Contact",
            Attributes = new List<AttributeSchema>
            {
                new AttributeSchema
                {
                    LogicalName = "statecode",
                    AttributeType = "State",
                    OptionSet = new OptionSetSchema
                    {
                        Options = new List<OptionSchema>
                        {
                            new OptionSchema { Value = 99, Label = null }
                        }
                    }
                }
            }
        };

        // Act
        var result = generator.GenerateEntityClass(entity, "MyCompany.Constants");

        // Assert
        Assert.Contains("public const int Option99 = 99;", result);
    }

    [Fact]
    public void GenerateEntityClass_WithMultipleLocalOptionSets_GeneratesAllNestedClasses()
    {
        // Arrange
        var formatter = new IdentifierFormatter();
        var generator = new CodeTemplateGenerator(includeComments: true, includeRelationships: true, formatter);
        var entity = new EntitySchema
        {
            LogicalName = "contact",
            DisplayName = "Contact",
            Attributes = new List<AttributeSchema>
            {
                new AttributeSchema
                {
                    LogicalName = "preferredcontactmethodcode",
                    DisplayName = "Preferred Method",
                    AttributeType = "Picklist",
                    OptionSet = new OptionSetSchema
                    {
                        IsGlobal = false,
                        Options = new List<OptionSchema> { new OptionSchema { Value = 1, Label = "Email" } }
                    }
                },
                new AttributeSchema
                {
                    LogicalName = "address1_addresstypecode",
                    DisplayName = "Address 1 Type",
                    AttributeType = "Picklist",
                    OptionSet = new OptionSetSchema
                    {
                        IsGlobal = false,
                        Options = new List<OptionSchema> { new OptionSchema { Value = 1, Label = "Bill To" } }
                    }
                }
            }
        };

        // Act
        var result = generator.GenerateEntityClass(entity, "MyCompany.Constants");

        // Assert
        Assert.Contains("public static class PreferredMethodOptions", result);
        Assert.Contains("public static class Address1TypeOptions", result);
    }
}
