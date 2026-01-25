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

    #region Helper Methods

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
