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

    #region Stored Value Comparison Tests

    // These tests check how RecordComparer applies AttributeValues: what it compares, which
    // columns it reports, and when it adds the stored value. The per-type rules themselves are
    // in AttributeValuesTests.

    private static readonly Guid ParentA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ParentB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Theory]
    [MemberData(nameof(SameStoredValueDifferentLabel))]
    public void CompareRecords_SameStoredValue_DifferentLabels_ReturnsNoDifference(object stored, string sourceLabel, string targetLabel)
    {
        var id = Guid.NewGuid();
        var source = CreateSettingRecord(id, "rob_value", stored, sourceLabel);
        var target = CreateSettingRecord(id, "rob_value", stored, targetLabel);

        Assert.Empty(Compare(source, target).Differences);
    }

    public static IEnumerable<object[]> SameStoredValueDifferentLabel()
    {
        // Labels depend on the settings of the user who queried each environment.
        yield return new object[] { new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc), "01/03/2026 09:00", "3/1/2026 4:00 AM" };
        yield return new object[] { new OptionSetValue(100000001), "Standard", "Standard (EN-US)" };
        yield return new object[] { new Money(1234.50m), "£1,234.50", "1.234,50 €" };
        // A renamed lookup target is not a difference.
        yield return new object[] { new EntityReference("rob_parent", ParentA) { Name = "Old Name" }, "Old Name", "New Name" };
    }

    [Fact]
    public void CompareRecords_DifferentStoredDates_IdenticalLabels_ReportsIsoValues()
    {
        var id = Guid.NewGuid();
        var source = CreateSettingRecord(id, "rob_effectivefrom", new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc), "01/03/2026");
        var target = CreateSettingRecord(id, "rob_effectivefrom", new DateTime(2026, 3, 1, 17, 30, 0, DateTimeKind.Utc), "01/03/2026");

        var field = Assert.Single(GetFieldDifferences(Compare(source, target)));

        Assert.Equal("rob_effectivefrom", field.FieldName);
        Assert.Equal("2026-03-01T09:00:00Z", field.SourceValue);
        Assert.Equal("2026-03-01T17:30:00Z", field.TargetValue);
    }

    /// <summary>When the two display strings differ, they are reported as they are, with no suffix.</summary>
    [Theory]
    [MemberData(nameof(DifferentDisplayRows))]
    public void CompareRecords_DifferentDisplay_ReportsDisplayWithoutStoredValue(
        object sourceStored, string? sourceLabel, object targetStored, string? targetLabel, string expectedSource, string expectedTarget)
    {
        var id = Guid.NewGuid();
        var source = CreateSettingRecord(id, "rob_value", sourceStored, sourceLabel);
        var target = CreateSettingRecord(id, "rob_value", targetStored, targetLabel);

        var field = Assert.Single(GetFieldDifferences(Compare(source, target)));

        Assert.Equal(expectedSource, field.SourceValue);
        Assert.Equal(expectedTarget, field.TargetValue);
    }

    public static IEnumerable<object?[]> DifferentDisplayRows()
    {
        yield return new object?[] { new OptionSetValue(100000001), "Standard", new OptionSetValue(100000002), "Premium", "Standard", "Premium" };
        yield return new object?[] { new Money(10m), "£10.00", new Money(20m), "£20.00", "£10.00", "£20.00" };
        yield return new object?[]
        {
            new EntityReference("rob_parent", ParentA) { Name = "Parent A" }, "Parent A",
            new EntityReference("rob_parent", ParentB) { Name = "Parent B" }, "Parent B",
            "Parent A", "Parent B"
        };
        yield return new object?[] { new byte[] { 1, 2, 3 }, null, new byte[] { 1, 2 }, null, "3 bytes", "2 bytes" };
    }

    /// <summary>
    /// Decision 1 on #133: when the values differ but read the same, the stored value is added
    /// to both sides, as {guid} for a lookup and (value) for anything else.
    /// </summary>
    [Theory]
    [MemberData(nameof(IdenticalDisplayRows))]
    public void CompareRecords_IdenticalDisplay_AddsStoredValueToBothSides(
        object sourceStored, string? sourceLabel, object targetStored, string? targetLabel, string expectedSource, string expectedTarget)
    {
        var id = Guid.NewGuid();
        var source = CreateSettingRecord(id, "rob_value", sourceStored, sourceLabel);
        var target = CreateSettingRecord(id, "rob_value", targetStored, targetLabel);

        var field = Assert.Single(GetFieldDifferences(Compare(source, target)));

        Assert.Equal(expectedSource, field.SourceValue);
        Assert.Equal(expectedTarget, field.TargetValue);
    }

    public static IEnumerable<object?[]> IdenticalDisplayRows()
    {
        var utc = new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

        yield return new object?[]
        {
            new EntityReference("rob_parent", ParentA) { Name = "Parent" }, "Parent",
            new EntityReference("rob_parent", ParentB) { Name = "Parent" }, "Parent",
            $"Parent {{{ParentA}}}", $"Parent {{{ParentB}}}"
        };
        // The lookup check runs on the unwrapped value.
        yield return new object?[]
        {
            new AliasedValue("rob_parent", "rob_parentid", new EntityReference("rob_parent", ParentA) { Name = "Parent" }), "Parent",
            new AliasedValue("rob_parent", "rob_parentid", new EntityReference("rob_parent", ParentB) { Name = "Parent" }), "Parent",
            $"Parent {{{ParentA}}}", $"Parent {{{ParentB}}}"
        };
        yield return new object?[] { new OptionSetValue(100000001), "Standard", new OptionSetValue(100000002), "Standard", "Standard (100000001)", "Standard (100000002)" };
        yield return new object?[] { new Money(1.001m), "£1.00", new Money(1.004m), "£1.00", "£1.00 (1.001)", "£1.00 (1.004)" };
        yield return new object?[]
        {
            new OptionSetValueCollection { new OptionSetValue(1), new OptionSetValue(2) }, "North; South",
            new OptionSetValueCollection { new OptionSetValue(1), new OptionSetValue(3) }, "North; South",
            "North; South (1,2)", "North; South (1,3)"
        };
        yield return new object?[]
        {
            utc, null, utc.AddMilliseconds(500), null,
            "2026-03-01T09:00:00Z (2026-03-01T09:00:00.0000000Z)", "2026-03-01T09:00:00Z (2026-03-01T09:00:00.5000000Z)"
        };
        yield return new object?[]
        {
            new byte[] { 1, 2, 3 }, null, new byte[] { 1, 2, 4 }, null,
            "3 bytes (SHA-256 039058C6F2C0)", "3 bytes (" + AttributeValues.FormatStoredValue(new byte[] { 1, 2, 4 }) + ")"
        };
    }

    [Theory]
    [MemberData(nameof(ConsistencyPairs))]
    public void CompareRecords_ReportsDifferenceExactlyWhenAttributeValuesAreNotEqual(object? sourceValue, object? targetValue)
    {
        var id = Guid.NewGuid();
        var source = CreateSettingRecord(id, "rob_value", sourceValue, null);
        var target = CreateSettingRecord(id, "rob_value", targetValue, null);

        var reported = Compare(source, target).Differences.Any();

        Assert.Equal(!AttributeValues.AreEqual(sourceValue, targetValue), reported);
    }

    public static IEnumerable<object?[]> ConsistencyPairs()
    {
        var utc = new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

        // One equal and one unequal pair per comparison rule.
        yield return new object?[] { null, null };
        yield return new object?[] { "a", "a" };
        yield return new object?[] { "a", "A" };
        yield return new object?[] { 1.5m, 1.50m };
        yield return new object?[] { utc, utc };
        yield return new object?[] { utc, utc.AddMilliseconds(1) };
        yield return new object?[] { new OptionSetValue(1), new OptionSetValue(1) };
        yield return new object?[] { new OptionSetValue(1), new OptionSetValue(2) };
        yield return new object?[] { new Money(5m), new Money(5m) };
        yield return new object?[] { new Money(5m), new Money(6m) };
        yield return new object?[] { new EntityReference("rob_parent", ParentA) { Name = "A" }, new EntityReference("rob_parent", ParentA) { Name = "B" } };
        yield return new object?[] { new EntityReference("rob_parent", ParentA) { Name = "A" }, new EntityReference("rob_parent", ParentB) { Name = "A" } };
        yield return new object?[] { new OptionSetValueCollection { new OptionSetValue(1), new OptionSetValue(2) }, new OptionSetValueCollection { new OptionSetValue(2), new OptionSetValue(1) } };
        yield return new object?[] { new OptionSetValueCollection { new OptionSetValue(1) }, new OptionSetValueCollection { new OptionSetValue(2) } };
        yield return new object?[] { new byte[] { 1, 2, 3 }, new byte[] { 1, 2, 3 } };
        yield return new object?[] { new byte[] { 1, 2, 3 }, new byte[] { 1, 2, 4 } };
        yield return new object?[] { new AliasedValue("rob_parent", "rob_code", "ABC"), new AliasedValue("rob_parent", "rob_code", "ABC") };
        yield return new object?[] { new AliasedValue("rob_parent", "rob_code", "ABC"), new AliasedValue("rob_parent", "rob_code", "XYZ") };

        // Typed value against null, both orders.
        object[] typed =
        {
            new EntityReference("rob_parent", ParentA) { Name = "A" },
            new OptionSetValue(1),
            new Money(5m),
            new OptionSetValueCollection { new OptionSetValue(1) },
            new byte[] { 1, 2, 3 },
            utc
        };
        foreach (var value in typed)
        {
            yield return new object?[] { value, null };
            yield return new object?[] { null, value };
        }
    }

    /// <summary>
    /// A missing column counts as null; the side with a value shows its label and no stored
    /// value is added, since the display strings already differ.
    /// </summary>
    [Theory]
    [MemberData(nameof(LabelledValueVersusEmpty))]
    public void CompareRecords_LabelledValueVersusNullOrMissing_ReportsLabelAndNullWithoutSuffix(
        string column, object value, string label, bool targetHasValue, bool emptySideIsMissing)
    {
        var id = Guid.NewGuid();
        var withValue = CreateSettingRecord(id, column, value, label);
        var empty = emptySideIsMissing
            ? CreateEntity("rob_setting", id, "Setting")
            : CreateSettingRecord(id, column, null, null);

        var result = targetHasValue ? Compare(empty, withValue) : Compare(withValue, empty);

        var field = Assert.Single(GetFieldDifferences(result));
        Assert.Equal(column, field.FieldName);
        var shown = targetHasValue ? field.TargetValue : field.SourceValue;
        var other = targetHasValue ? field.SourceValue : field.TargetValue;
        Assert.Equal(label, shown);
        Assert.Null(other);
        Assert.DoesNotContain(" (", shown);
        Assert.DoesNotContain(" {", shown);
    }

    public static IEnumerable<object[]> LabelledValueVersusEmpty()
    {
        var columns = new (string Column, Func<object> Value, string Label)[]
        {
            ("rob_parentid", () => new EntityReference("rob_parent", Guid.NewGuid()) { Name = "Parent A" }, "Parent A"),
            ("rob_category", () => new OptionSetValue(100000001), "Standard"),
            ("rob_price", () => new Money(12.5m), "£12.50")
        };

        foreach (var (column, value, label) in columns)
        {
            foreach (var targetHasValue in new[] { false, true })
            {
                foreach (var emptySideIsMissing in new[] { false, true })
                {
                    yield return new object[] { column, value(), label, targetHasValue, emptySideIsMissing };
                }
            }
        }
    }

    [Fact]
    public void CompareRecords_NullOnOneSideMissingOnOther_ReturnsNoDifference()
    {
        var id = Guid.NewGuid();
        var source = CreateSettingRecord(id, "rob_effectivefrom", null, null);
        var target = CreateEntity("rob_setting", id, "Setting");

        Assert.Empty(Compare(source, target).Differences);
        Assert.Empty(Compare(target, source).Differences);
    }

    [Fact]
    public void CompareRecords_OnlyDifferingColumnsReported_WhenSeveralColumnsCompared()
    {
        var id = Guid.NewGuid();
        var source = CreateEntity("rob_setting", id, "Setting");
        source["rob_parentid"] = new EntityReference("rob_parent", ParentA) { Name = "Old" };
        source["rob_category"] = new OptionSetValue(1);
        source["rob_code"] = "A";
        var target = CreateEntity("rob_setting", id, "Setting");
        target["rob_parentid"] = new EntityReference("rob_parent", ParentA) { Name = "New" };
        target["rob_category"] = new OptionSetValue(1);
        target["rob_code"] = "B";

        var field = Assert.Single(GetFieldDifferences(Compare(source, target)));

        Assert.Equal("rob_code", field.FieldName);
    }

    #endregion

    #region BuildNameLookup Tests

    [Fact]
    public void BuildNameLookup_UsesFormattedValueWhenPresent()
    {
        var id = Guid.NewGuid();
        var entity = new Entity("rob_setting", id);
        entity["rob_parentid"] = new EntityReference("rob_parent", Guid.NewGuid()) { Name = "Raw Name" };
        entity.FormattedValues["rob_parentid"] = "Formatted Name";
        var records = new EntityCollection { Entities = { entity } };

        var lookup = RecordComparer.BuildNameLookup(records, "rob_parentid");

        Assert.Equal("Formatted Name", lookup[id]);
    }

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

    private TableComparisonResult Compare(Entity source, Entity target)
    {
        return _comparer.CompareRecords(
            "rob_setting",
            new EntityCollection { Entities = { source } },
            new EntityCollection { Entities = { target } },
            new HashSet<string>(),
            primaryIdField: "rob_settingid");
    }

    private static List<FieldDifference> GetFieldDifferences(TableComparisonResult result)
    {
        var record = Assert.Single(result.Differences);
        Assert.Equal(DifferenceType.Modified, record.DifferenceType);
        return record.FieldDifferences;
    }

    private static Entity CreateSettingRecord(Guid id, string column, object? value, string? formattedValue)
    {
        var entity = CreateEntity("rob_setting", id, "Setting");
        entity.Attributes[column] = value;
        if (formattedValue != null)
        {
            entity.FormattedValues[column] = formattedValue;
        }

        return entity;
    }

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
