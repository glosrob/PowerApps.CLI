using Microsoft.Xrm.Sdk;
using PowerApps.CLI.Models;
using PowerApps.CLI.Services;
using Xunit;

namespace PowerApps.CLI.Tests.Services;

public class RecordComparerTests
{
    private readonly RecordComparer _comparer;

    public RecordComparerTests()
    {
        _comparer = new RecordComparer();
    }

    #region CompareRecords - New Records Tests

    [Fact]
    public void CompareRecords_WithNewRecordsOnly_ReturnsNewDifferences()
    {
        // Arrange
        var sourceRecords = new EntityCollection
        {
            Entities =
            {
                CreateEntity("account", Guid.NewGuid(), "New Account 1"),
                CreateEntity("account", Guid.NewGuid(), "New Account 2")
            }
        };
        var targetRecords = new EntityCollection();
        var excludeFields = new HashSet<string>();

        // Act
        var result = _comparer.CompareRecords("account", sourceRecords, targetRecords, excludeFields);

        // Assert
        Assert.Equal(2, result.NewCount);
        Assert.Equal(0, result.ModifiedCount);
        Assert.Equal(0, result.DeletedCount);
        Assert.True(result.HasDifferences);
        Assert.All(result.Differences, d => Assert.Equal(DifferenceType.New, d.DifferenceType));
    }

    #endregion

    #region CompareRecords - Deleted Records Tests

    [Fact]
    public void CompareRecords_WithDeletedRecordsOnly_ReturnsDeletedDifferences()
    {
        // Arrange
        var sourceRecords = new EntityCollection();
        var targetRecords = new EntityCollection
        {
            Entities =
            {
                CreateEntity("account", Guid.NewGuid(), "Deleted Account 1"),
                CreateEntity("account", Guid.NewGuid(), "Deleted Account 2")
            }
        };
        var excludeFields = new HashSet<string>();

        // Act
        var result = _comparer.CompareRecords("account", sourceRecords, targetRecords, excludeFields);

        // Assert
        Assert.Equal(0, result.NewCount);
        Assert.Equal(0, result.ModifiedCount);
        Assert.Equal(2, result.DeletedCount);
        Assert.True(result.HasDifferences);
        Assert.All(result.Differences, d => Assert.Equal(DifferenceType.Deleted, d.DifferenceType));
    }

    #endregion

    #region CompareRecords - Modified Records Tests

    [Fact]
    public void CompareRecords_WithModifiedRecords_ReturnsFieldDifferences()
    {
        // Arrange
        var id = Guid.NewGuid();
        var sourceRecords = new EntityCollection
        {
            Entities =
            {
                CreateEntity("account", id, "Account 1", new Dictionary<string, object>
                {
                    { "telephone1", "555-1111" },
                    { "revenue", new Money(1000m) }
                })
            }
        };
        var targetRecords = new EntityCollection
        {
            Entities =
            {
                CreateEntity("account", id, "Account 1", new Dictionary<string, object>
                {
                    { "telephone1", "555-2222" },
                    { "revenue", new Money(1000m) }
                })
            }
        };
        var excludeFields = new HashSet<string>();

        // Act
        var result = _comparer.CompareRecords("account", sourceRecords, targetRecords, excludeFields);

        // Assert
        Assert.Equal(0, result.NewCount);
        Assert.Equal(1, result.ModifiedCount);
        Assert.Equal(0, result.DeletedCount);
        Assert.True(result.HasDifferences);
        
        var modifiedRecord = result.Differences.Single();
        Assert.Equal(DifferenceType.Modified, modifiedRecord.DifferenceType);
        Assert.Single(modifiedRecord.FieldDifferences);
        Assert.Equal("telephone1", modifiedRecord.FieldDifferences[0].FieldName);
    }

    [Fact]
    public void CompareRecords_WithIdenticalRecords_ReturnsNoDifferences()
    {
        // Arrange
        var id = Guid.NewGuid();
        var sourceRecords = new EntityCollection
        {
            Entities =
            {
                CreateEntity("account", id, "Account 1", new Dictionary<string, object>
                {
                    { "telephone1", "555-1111" }
                })
            }
        };
        var targetRecords = new EntityCollection
        {
            Entities =
            {
                CreateEntity("account", id, "Account 1", new Dictionary<string, object>
                {
                    { "telephone1", "555-1111" }
                })
            }
        };
        var excludeFields = new HashSet<string>();

        // Act
        var result = _comparer.CompareRecords("account", sourceRecords, targetRecords, excludeFields);

        // Assert
        Assert.Equal(0, result.NewCount);
        Assert.Equal(0, result.ModifiedCount);
        Assert.Equal(0, result.DeletedCount);
        Assert.False(result.HasDifferences);
        Assert.Empty(result.Differences);
    }

    #endregion

    #region Exclusion Tests

    [Fact]
    public void CompareRecords_ExcludesSystemFields()
    {
        // Arrange
        var id = Guid.NewGuid();
        var sourceRecords = new EntityCollection
        {
            Entities =
            {
                CreateEntity("account", id, "Account 1", new Dictionary<string, object>
                {
                    { "createdby", new EntityReference("systemuser", Guid.NewGuid()) },
                    { "modifiedon", DateTime.UtcNow }
                })
            }
        };
        var targetRecords = new EntityCollection
        {
            Entities =
            {
                CreateEntity("account", id, "Account 1", new Dictionary<string, object>
                {
                    { "createdby", new EntityReference("systemuser", Guid.NewGuid()) },
                    { "modifiedon", DateTime.UtcNow.AddHours(1) }
                })
            }
        };
        var excludeFields = new HashSet<string>();

        // Act
        var result = _comparer.CompareRecords("account", sourceRecords, targetRecords, excludeFields);

        // Assert - System fields should be excluded, no differences should be found
        Assert.False(result.HasDifferences);
        Assert.Empty(result.Differences);
    }

    [Fact]
    public void CompareRecords_ExcludesCustomFields()
    {
        // Arrange
        var id = Guid.NewGuid();
        var sourceRecords = new EntityCollection
        {
            Entities =
            {
                CreateEntity("account", id, "Account 1", new Dictionary<string, object>
                {
                    { "telephone1", "555-1111" },
                    { "customfield", "Value 1" }
                })
            }
        };
        var targetRecords = new EntityCollection
        {
            Entities =
            {
                CreateEntity("account", id, "Account 1", new Dictionary<string, object>
                {
                    { "telephone1", "555-1111" },
                    { "customfield", "Value 2" }
                })
            }
        };
        var excludeFields = new HashSet<string> { "customfield" };

        // Act
        var result = _comparer.CompareRecords("account", sourceRecords, targetRecords, excludeFields);

        // Assert - customfield should be excluded
        Assert.False(result.HasDifferences);
    }

    [Fact]
    public void CompareRecords_ExcludesPrimaryIdField()
    {
        // Arrange
        var id = Guid.NewGuid();
        var sourceRecords = new EntityCollection
        {
            Entities =
            {
                CreateEntity("account", id, "Account 1", new Dictionary<string, object>
                {
                    { "accountid", id },
                    { "telephone1", "555-1111" }
                })
            }
        };
        var targetRecords = new EntityCollection
        {
            Entities =
            {
                CreateEntity("account", id, "Account 1", new Dictionary<string, object>
                {
                    { "accountid", id },
                    { "telephone1", "555-1111" }
                })
            }
        };
        var excludeFields = new HashSet<string>();

        // Act
        var result = _comparer.CompareRecords("account", sourceRecords, targetRecords, excludeFields, primaryIdField: "accountid");

        // Assert - Primary ID should be excluded from comparison
        Assert.False(result.HasDifferences);
    }

    #endregion

    #region IncludeFields Tests

    [Fact]
    public void CompareRecords_WithIncludeFields_OnlyComparesSpecifiedFields()
    {
        // Arrange
        var id = Guid.NewGuid();
        var sourceRecords = new EntityCollection
        {
            Entities =
            {
                CreateEntity("account", id, "Account 1", new Dictionary<string, object>
                {
                    { "telephone1", "555-1111" },
                    { "fax", "555-9999" }
                })
            }
        };
        var targetRecords = new EntityCollection
        {
            Entities =
            {
                CreateEntity("account", id, "Account 1", new Dictionary<string, object>
                {
                    { "telephone1", "555-2222" }, // different
                    { "fax", "555-8888" }          // different but not in includeFields
                })
            }
        };
        var excludeFields = new HashSet<string>();
        var includeFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "telephone1" };

        // Act
        var result = _comparer.CompareRecords("account", sourceRecords, targetRecords, excludeFields, includeFields);

        // Assert - only telephone1 compared, so exactly one field difference
        Assert.Equal(1, result.ModifiedCount);
        var diff = result.Differences.Single();
        Assert.Single(diff.FieldDifferences);
        Assert.Equal("telephone1", diff.FieldDifferences[0].FieldName);
    }

    [Fact]
    public void CompareRecords_WithIncludeFields_IgnoresChangesOutsideAllowlist()
    {
        // Arrange
        var id = Guid.NewGuid();
        var sourceRecords = new EntityCollection
        {
            Entities =
            {
                CreateEntity("account", id, "Account 1", new Dictionary<string, object>
                {
                    { "telephone1", "555-1111" },
                    { "fax", "555-9999" }
                })
            }
        };
        var targetRecords = new EntityCollection
        {
            Entities =
            {
                CreateEntity("account", id, "Account 1", new Dictionary<string, object>
                {
                    { "telephone1", "555-1111" }, // same
                    { "fax", "555-8888" }          // different but outside allowlist
                })
            }
        };
        var excludeFields = new HashSet<string>();
        var includeFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "telephone1" };

        // Act
        var result = _comparer.CompareRecords("account", sourceRecords, targetRecords, excludeFields, includeFields);

        // Assert - fax difference is ignored, no differences found
        Assert.False(result.HasDifferences);
    }

    #endregion

    #region Mixed Scenario Tests

    [Fact]
    public void CompareRecords_WithMixedDifferences_ReturnsAllTypes()
    {
        // Arrange
        var newId = Guid.NewGuid();
        var modifiedId = Guid.NewGuid();
        var deletedId = Guid.NewGuid();
        var unchangedId = Guid.NewGuid();

        var sourceRecords = new EntityCollection
        {
            Entities =
            {
                CreateEntity("account", newId, "New Account"),
                CreateEntity("account", modifiedId, "Modified Account", new Dictionary<string, object>
                {
                    { "telephone1", "555-1111" }
                }),
                CreateEntity("account", unchangedId, "Unchanged Account")
            }
        };

        var targetRecords = new EntityCollection
        {
            Entities =
            {
                CreateEntity("account", modifiedId, "Modified Account", new Dictionary<string, object>
                {
                    { "telephone1", "555-2222" }
                }),
                CreateEntity("account", deletedId, "Deleted Account"),
                CreateEntity("account", unchangedId, "Unchanged Account")
            }
        };

        var excludeFields = new HashSet<string>();

        // Act
        var result = _comparer.CompareRecords("account", sourceRecords, targetRecords, excludeFields);

        // Assert
        Assert.Equal(1, result.NewCount);
        Assert.Equal(1, result.ModifiedCount);
        Assert.Equal(1, result.DeletedCount);
        Assert.True(result.HasDifferences);
        Assert.Equal(3, result.Differences.Count);
    }

    [Fact]
    public void CompareRecords_WithEmptyCollections_ReturnsNoDifferences()
    {
        // Arrange
        var sourceRecords = new EntityCollection();
        var targetRecords = new EntityCollection();
        var excludeFields = new HashSet<string>();

        // Act
        var result = _comparer.CompareRecords("account", sourceRecords, targetRecords, excludeFields);

        // Assert
        Assert.Equal(0, result.SourceRecordCount);
        Assert.Equal(0, result.TargetRecordCount);
        Assert.False(result.HasDifferences);
        Assert.Empty(result.Differences);
    }

    [Fact]
    public void CompareRecords_SetsTableNameCorrectly()
    {
        // Arrange
        var sourceRecords = new EntityCollection();
        var targetRecords = new EntityCollection();
        var excludeFields = new HashSet<string>();

        // Act
        var result = _comparer.CompareRecords("rob_customtable", sourceRecords, targetRecords, excludeFields);

        // Assert
        Assert.Equal("rob_customtable", result.TableName);
    }

    [Fact]
    public void CompareRecords_SetsRecordCountsCorrectly()
    {
        // Arrange
        var sourceRecords = new EntityCollection
        {
            Entities =
            {
                CreateEntity("account", Guid.NewGuid(), "Account 1"),
                CreateEntity("account", Guid.NewGuid(), "Account 2"),
                CreateEntity("account", Guid.NewGuid(), "Account 3")
            }
        };
        var targetRecords = new EntityCollection
        {
            Entities =
            {
                CreateEntity("account", Guid.NewGuid(), "Account 4"),
                CreateEntity("account", Guid.NewGuid(), "Account 5")
            }
        };
        var excludeFields = new HashSet<string>();

        // Act
        var result = _comparer.CompareRecords("account", sourceRecords, targetRecords, excludeFields);

        // Assert
        Assert.Equal(3, result.SourceRecordCount);
        Assert.Equal(2, result.TargetRecordCount);
    }

    #endregion

    #region CompareAssociations Tests

    [Fact]
    public void CompareAssociations_IdentifiesNewAssociations()
    {
        // Arrange
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var source = new EntityCollection
        {
            Entities = { CreateAssociation("contactleads", id1, id2, "contactid", "leadid") }
        };
        var target = new EntityCollection();
        var names1 = new Dictionary<Guid, string> { { id1, "John Smith" } };
        var names2 = new Dictionary<Guid, string> { { id2, "Hot Lead" } };

        // Act
        var result = _comparer.CompareAssociations("Contact to Lead", source, target, "contactid", "leadid", names1, names2);

        // Assert
        Assert.Equal(1, result.NewCount);
        Assert.Equal(0, result.DeletedCount);
        Assert.True(result.HasDifferences);
        var diff = result.Differences.Single();
        Assert.Equal(DifferenceType.New, diff.DifferenceType);
        Assert.Equal("John Smith", diff.Entity1Name);
        Assert.Equal("Hot Lead", diff.Entity2Name);
    }

    [Fact]
    public void CompareAssociations_IdentifiesDeletedAssociations()
    {
        // Arrange
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var source = new EntityCollection();
        var target = new EntityCollection
        {
            Entities = { CreateAssociation("contactleads", id1, id2, "contactid", "leadid") }
        };
        var names1 = new Dictionary<Guid, string> { { id1, "John Smith" } };
        var names2 = new Dictionary<Guid, string> { { id2, "Old Lead" } };

        // Act
        var result = _comparer.CompareAssociations("Contact to Lead", source, target, "contactid", "leadid", names1, names2);

        // Assert
        Assert.Equal(0, result.NewCount);
        Assert.Equal(1, result.DeletedCount);
        var diff = result.Differences.Single();
        Assert.Equal(DifferenceType.Deleted, diff.DifferenceType);
    }

    [Fact]
    public void CompareAssociations_MatchingAssociationsProduceNoDifferences()
    {
        // Arrange
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var association = CreateAssociation("contactleads", id1, id2, "contactid", "leadid");
        var source = new EntityCollection { Entities = { association } };
        // Create a separate entity with same composite key for target
        var targetAssociation = CreateAssociation("contactleads", id1, id2, "contactid", "leadid");
        var target = new EntityCollection { Entities = { targetAssociation } };
        var names1 = new Dictionary<Guid, string>();
        var names2 = new Dictionary<Guid, string>();

        // Act
        var result = _comparer.CompareAssociations("Contact to Lead", source, target, "contactid", "leadid", names1, names2);

        // Assert
        Assert.False(result.HasDifferences);
        Assert.Empty(result.Differences);
    }

    [Fact]
    public void CompareAssociations_FallsBackToGuidWhenNameMissing()
    {
        // Arrange
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var source = new EntityCollection
        {
            Entities = { CreateAssociation("contactleads", id1, id2, "contactid", "leadid") }
        };
        var target = new EntityCollection();
        var names1 = new Dictionary<Guid, string>(); // No name for id1
        var names2 = new Dictionary<Guid, string>(); // No name for id2

        // Act
        var result = _comparer.CompareAssociations("Contact to Lead", source, target, "contactid", "leadid", names1, names2);

        // Assert
        var diff = result.Differences.Single();
        Assert.Equal(id1.ToString(), diff.Entity1Name);
        Assert.Equal(id2.ToString(), diff.Entity2Name);
    }

    [Fact]
    public void CompareAssociations_EmptyCollectionsProduceNoDifferences()
    {
        // Arrange
        var source = new EntityCollection();
        var target = new EntityCollection();
        var names1 = new Dictionary<Guid, string>();
        var names2 = new Dictionary<Guid, string>();

        // Act
        var result = _comparer.CompareAssociations("Contact to Lead", source, target, "contactid", "leadid", names1, names2);

        // Assert
        Assert.False(result.HasDifferences);
        Assert.Equal(0, result.SourceAssociationCount);
        Assert.Equal(0, result.TargetAssociationCount);
    }

    [Fact]
    public void CompareAssociations_SetsCountsCorrectly()
    {
        // Arrange
        var source = new EntityCollection
        {
            Entities =
            {
                CreateAssociation("contactleads", Guid.NewGuid(), Guid.NewGuid(), "contactid", "leadid"),
                CreateAssociation("contactleads", Guid.NewGuid(), Guid.NewGuid(), "contactid", "leadid")
            }
        };
        var target = new EntityCollection
        {
            Entities =
            {
                CreateAssociation("contactleads", Guid.NewGuid(), Guid.NewGuid(), "contactid", "leadid")
            }
        };

        // Act
        var result = _comparer.CompareAssociations("Test", source, target, "contactid", "leadid",
            new Dictionary<Guid, string>(), new Dictionary<Guid, string>());

        // Assert
        Assert.Equal(2, result.SourceAssociationCount);
        Assert.Equal(1, result.TargetAssociationCount);
    }

    [Fact]
    public void CompareAssociations_SetsRelationshipName()
    {
        // Act
        var result = _comparer.CompareAssociations("My Relationship", new EntityCollection(), new EntityCollection(),
            "contactid", "leadid", new Dictionary<Guid, string>(), new Dictionary<Guid, string>());

        // Assert
        Assert.Equal("My Relationship", result.RelationshipName);
    }

    #endregion

    #region BuildNameLookup Tests

    [Fact]
    public void BuildNameLookup_BuildsCorrectMapping()
    {
        // Arrange
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var records = new EntityCollection
        {
            Entities =
            {
                CreateEntity("contact", id1, "John Smith"),
                CreateEntity("contact", id2, "Jane Doe")
            }
        };

        // Act
        var lookup = RecordComparer.BuildNameLookup(records, "name");

        // Assert
        Assert.Equal(2, lookup.Count);
        Assert.Equal("John Smith", lookup[id1]);
        Assert.Equal("Jane Doe", lookup[id2]);
    }

    [Fact]
    public void BuildNameLookup_HandlesMissingNameField()
    {
        // Arrange
        var id = Guid.NewGuid();
        var entity = new Entity("contact", id);
        // No "fullname" attribute set
        var records = new EntityCollection { Entities = { entity } };

        // Act
        var lookup = RecordComparer.BuildNameLookup(records, "fullname");

        // Assert
        Assert.Empty(lookup); // Should skip records without the name field
    }

    #endregion

    #region Helper Methods

    private static Entity CreateAssociation(string intersectEntity, Guid entity1Id, Guid entity2Id, string entity1IdField, string entity2IdField)
    {
        var entity = new Entity(intersectEntity, Guid.NewGuid());
        entity.Attributes[entity1IdField] = entity1Id;
        entity.Attributes[entity2IdField] = entity2Id;
        return entity;
    }

    private static Entity CreateEntity(string logicalName, Guid id, string name, Dictionary<string, object>? additionalAttributes = null)
    {
        var entity = new Entity(logicalName, id);
        entity.Attributes["name"] = name;

        if (additionalAttributes != null)
        {
            foreach (var kvp in additionalAttributes)
            {
                entity.Attributes[kvp.Key] = kvp.Value;
            }
        }

        return entity;
    }

    #endregion
}
