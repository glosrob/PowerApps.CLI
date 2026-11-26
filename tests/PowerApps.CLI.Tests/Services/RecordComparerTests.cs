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

    private const string DateColumn = "rob_effectivefrom";

    [Fact]
    public void CompareRecords_SameStoredDate_DifferentFormattedValues_ReturnsNoDifference()
    {
        var id = Guid.NewGuid();
        var stored = new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);
        var source = CreateSettingRecord(id, DateColumn, stored, "01/03/2026 09:00");
        var target = CreateSettingRecord(id, DateColumn, stored, "3/1/2026 4:00 AM");

        var result = Compare(source, target);

        Assert.Empty(result.Differences);
    }

    [Fact]
    public void CompareRecords_DifferentStoredDates_IdenticalFormattedValues_ReturnsDifference()
    {
        var id = Guid.NewGuid();
        var source = CreateSettingRecord(id, DateColumn, new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc), "01/03/2026");
        var target = CreateSettingRecord(id, DateColumn, new DateTime(2026, 3, 1, 17, 30, 0, DateTimeKind.Utc), "01/03/2026");

        var field = Assert.Single(GetFieldDifferences(Compare(source, target)));

        Assert.Equal(DateColumn, field.FieldName);
        Assert.Equal("2026-03-01T09:00:00Z", field.SourceValue);
        Assert.Equal("2026-03-01T17:30:00Z", field.TargetValue);
    }

    [Fact]
    public void CompareRecords_DateOnOneSideNullOnOther_ReturnsDifference()
    {
        var id = Guid.NewGuid();
        var source = CreateSettingRecord(id, DateColumn, new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc), "01/03/2026 09:00");
        var target = CreateSettingRecord(id, DateColumn, null, null);

        var field = Assert.Single(GetFieldDifferences(Compare(source, target)));

        Assert.Equal("2026-03-01T09:00:00Z", field.SourceValue);
        Assert.Null(field.TargetValue);
    }

    [Fact]
    public void CompareRecords_DateOnOneSideMissingOnOther_ReturnsDifference()
    {
        var id = Guid.NewGuid();
        var source = CreateEntity("rob_setting", id, "Setting");
        var target = CreateSettingRecord(id, DateColumn, new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc), "01/03/2026 09:00");

        var field = Assert.Single(GetFieldDifferences(Compare(source, target)));

        Assert.Null(field.SourceValue);
        Assert.Equal("2026-03-01T09:00:00Z", field.TargetValue);
    }

    [Fact]
    public void CompareRecords_DateDifference_ReportsIsoValuesNotFormattedValues()
    {
        var id = Guid.NewGuid();
        var source = CreateSettingRecord(id, DateColumn, new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc), "01/03/2026 09:00");
        var target = CreateSettingRecord(id, DateColumn, new DateTime(2026, 4, 1, 9, 0, 0, DateTimeKind.Utc), "4/1/2026 4:00 AM");

        var field = Assert.Single(GetFieldDifferences(Compare(source, target)));

        Assert.Equal("2026-03-01T09:00:00Z", field.SourceValue);
        Assert.Equal("2026-04-01T09:00:00Z", field.TargetValue);
    }

    [Fact]
    public void CompareRecords_UnspecifiedDates_CompareAndDisplayWithoutShift()
    {
        var id = Guid.NewGuid();
        var same = new DateTime(2026, 3, 31, 23, 30, 0, DateTimeKind.Unspecified);

        var unchanged = Compare(
            CreateSettingRecord(id, DateColumn, same, "31/03/2026"),
            CreateSettingRecord(id, DateColumn, same, "3/31/2026"));
        Assert.Empty(unchanged.Differences);

        var changed = Compare(
            CreateSettingRecord(id, DateColumn, same, "31/03/2026"),
            CreateSettingRecord(id, DateColumn, new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Unspecified), "01/04/2026"));
        var field = Assert.Single(GetFieldDifferences(changed));
        Assert.Equal("2026-03-31T23:30:00", field.SourceValue);
        Assert.Equal("2026-04-01T00:00:00", field.TargetValue);
    }

    [Fact]
    public void CompareRecords_DateDifferingOnlyBySubSecond_ReportIncludesStoredValue()
    {
        var id = Guid.NewGuid();
        var stored = new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);
        var source = CreateSettingRecord(id, DateColumn, stored, null);
        var target = CreateSettingRecord(id, DateColumn, stored.AddMilliseconds(500), null);

        var field = Assert.Single(GetFieldDifferences(Compare(source, target)));

        Assert.Equal("2026-03-01T09:00:00Z (2026-03-01T09:00:00.0000000Z)", field.SourceValue);
        Assert.Equal("2026-03-01T09:00:00Z (2026-03-01T09:00:00.5000000Z)", field.TargetValue);
    }

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
        yield return new object[] { new OptionSetValue(100000001), "Standard", "Standard (EN-US)" };
        yield return new object[] { new Money(1234.50m), "£1,234.50", "1.234,50 €" };
        yield return new object[] { true, "Yes", "Oui" };
    }

    [Theory]
    [MemberData(nameof(DifferentStoredValueWithLabels))]
    public void CompareRecords_DifferentStoredValues_ReportShowsLabels(object sourceStored, string sourceLabel, object targetStored, string targetLabel)
    {
        var id = Guid.NewGuid();
        var source = CreateSettingRecord(id, "rob_value", sourceStored, sourceLabel);
        var target = CreateSettingRecord(id, "rob_value", targetStored, targetLabel);

        var field = Assert.Single(GetFieldDifferences(Compare(source, target)));

        Assert.Equal(sourceLabel, field.SourceValue);
        Assert.Equal(targetLabel, field.TargetValue);
    }

    public static IEnumerable<object[]> DifferentStoredValueWithLabels()
    {
        yield return new object[] { new OptionSetValue(100000001), "Standard", new OptionSetValue(100000002), "Premium" };
        yield return new object[] { new Money(10m), "£10.00", new Money(20m), "£20.00" };
        yield return new object[] { true, "Yes", false, "No" };
    }

    [Fact]
    public void CompareRecords_ChoicesWithSameLabelDifferentValue_ReportIncludesStoredValue()
    {
        var id = Guid.NewGuid();
        var source = CreateSettingRecord(id, "rob_category", new OptionSetValue(100000001), "Standard");
        var target = CreateSettingRecord(id, "rob_category", new OptionSetValue(100000002), "Standard");

        var field = Assert.Single(GetFieldDifferences(Compare(source, target)));

        Assert.Equal("Standard (100000001)", field.SourceValue);
        Assert.Equal("Standard (100000002)", field.TargetValue);
    }

    [Fact]
    public void CompareRecords_LookupSameIdDifferentName_ReturnsNoDifference()
    {
        var id = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var source = CreateSettingRecord(id, "rob_parentid", new EntityReference("rob_parent", parentId) { Name = "Old Name" }, "Old Name");
        var target = CreateSettingRecord(id, "rob_parentid", new EntityReference("rob_parent", parentId) { Name = "New Name" }, "New Name");

        Assert.Empty(Compare(source, target).Differences);
    }

    [Fact]
    public void CompareRecords_LookupDifferentIdSameName_ReturnsDifferenceWithIds()
    {
        var id = Guid.NewGuid();
        var sourceParentId = Guid.NewGuid();
        var targetParentId = Guid.NewGuid();
        var source = CreateSettingRecord(id, "rob_parentid", new EntityReference("rob_parent", sourceParentId) { Name = "Parent" }, "Parent");
        var target = CreateSettingRecord(id, "rob_parentid", new EntityReference("rob_parent", targetParentId) { Name = "Parent" }, "Parent");

        var field = Assert.Single(GetFieldDifferences(Compare(source, target)));

        Assert.Equal($"Parent {{{sourceParentId}}}", field.SourceValue);
        Assert.Equal($"Parent {{{targetParentId}}}", field.TargetValue);
    }

    [Fact]
    public void CompareRecords_LookupDifferentIdDifferentName_ReportsNamesOnly()
    {
        var id = Guid.NewGuid();
        var source = CreateSettingRecord(id, "rob_parentid", new EntityReference("rob_parent", Guid.NewGuid()) { Name = "Parent A" }, "Parent A");
        var target = CreateSettingRecord(id, "rob_parentid", new EntityReference("rob_parent", Guid.NewGuid()) { Name = "Parent B" }, "Parent B");

        var field = Assert.Single(GetFieldDifferences(Compare(source, target)));

        Assert.Equal("Parent A", field.SourceValue);
        Assert.Equal("Parent B", field.TargetValue);
    }

    [Fact]
    public void CompareRecords_MultiSelectSameOptionsDifferentOrder_ReturnsNoDifference()
    {
        var id = Guid.NewGuid();
        var source = CreateSettingRecord(id, "rob_regions",
            new OptionSetValueCollection { new OptionSetValue(1), new OptionSetValue(2) }, "North; South");
        var target = CreateSettingRecord(id, "rob_regions",
            new OptionSetValueCollection { new OptionSetValue(2), new OptionSetValue(1) }, "South; North");

        Assert.Empty(Compare(source, target).Differences);
    }

    [Fact]
    public void CompareRecords_MultiSelectDifferentOptions_ReturnsDifference()
    {
        var id = Guid.NewGuid();
        var source = CreateSettingRecord(id, "rob_regions",
            new OptionSetValueCollection { new OptionSetValue(1), new OptionSetValue(2) }, "North; South");
        var target = CreateSettingRecord(id, "rob_regions",
            new OptionSetValueCollection { new OptionSetValue(1) }, "North");

        var field = Assert.Single(GetFieldDifferences(Compare(source, target)));

        Assert.Equal("North; South", field.SourceValue);
        Assert.Equal("North", field.TargetValue);
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
        var lookupId = Guid.NewGuid();
        var utc = new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);
        var unspecified = new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Unspecified);

        yield return new object?[] { null, null };
        yield return new object?[] { "a", null };
        yield return new object?[] { null, "a" };
        yield return new object?[] { "a", "a" };
        yield return new object?[] { "a", "A" };
        yield return new object?[] { 1, 1 };
        yield return new object?[] { 1, 2 };
        yield return new object?[] { 1.5m, 1.50m };
        yield return new object?[] { true, false };
        yield return new object?[] { utc, utc };
        yield return new object?[] { utc, utc.AddMilliseconds(1) };
        yield return new object?[] { unspecified, unspecified };
        yield return new object?[] { new OptionSetValue(1), new OptionSetValue(1) };
        yield return new object?[] { new OptionSetValue(1), new OptionSetValue(2) };
        yield return new object?[] { new Money(5m), new Money(5m) };
        yield return new object?[] { new Money(5m), new Money(6m) };
        yield return new object?[] { new EntityReference("rob_parent", lookupId) { Name = "A" }, new EntityReference("rob_parent", lookupId) { Name = "B" } };
        yield return new object?[] { new EntityReference("rob_parent", lookupId) { Name = "A" }, new EntityReference("rob_parent", Guid.NewGuid()) { Name = "A" } };
        yield return new object?[] { new OptionSetValueCollection { new OptionSetValue(1), new OptionSetValue(2) }, new OptionSetValueCollection { new OptionSetValue(2), new OptionSetValue(1) } };
        yield return new object?[] { new OptionSetValueCollection { new OptionSetValue(1) }, new OptionSetValueCollection { new OptionSetValue(2) } };
    }

    [Fact]
    public void CompareRecords_NullOnOneSideMissingOnOther_ReturnsNoDifference()
    {
        var id = Guid.NewGuid();
        var source = CreateSettingRecord(id, DateColumn, null, null);
        var target = CreateEntity("rob_setting", id, "Setting");

        Assert.Empty(Compare(source, target).Differences);
        Assert.Empty(Compare(target, source).Differences);
    }

    [Fact]
    public void CompareRecords_ColumnOnTargetOnly_ReturnsDifferenceWithNullSource()
    {
        var id = Guid.NewGuid();
        var source = CreateEntity("rob_setting", id, "Setting");
        var target = CreateSettingRecord(id, "rob_category", new OptionSetValue(100000001), "Standard");

        var field = Assert.Single(GetFieldDifferences(Compare(source, target)));

        Assert.Equal("rob_category", field.FieldName);
        Assert.Null(field.SourceValue);
        Assert.Equal("Standard", field.TargetValue);
    }

    [Fact]
    public void CompareRecords_MoneyWithDifferentScale_ReturnsNoDifference()
    {
        var id = Guid.NewGuid();
        var source = CreateSettingRecord(id, "rob_price", new Money(10.0m), "£10.00");
        var target = CreateSettingRecord(id, "rob_price", new Money(10.0000m), "10,00 €");

        Assert.Empty(Compare(source, target).Differences);
    }

    [Fact]
    public void CompareRecords_DecimalWithDifferentScale_NoFormattedValues_ReturnsNoDifference()
    {
        var id = Guid.NewGuid();
        var source = CreateSettingRecord(id, "rob_rate", 1.0m, null);
        var target = CreateSettingRecord(id, "rob_rate", 1.00m, null);

        Assert.Empty(Compare(source, target).Differences);
    }

    [Fact]
    public void CompareRecords_MoneySameLabelDifferentValue_ReportIncludesStoredValue()
    {
        var id = Guid.NewGuid();
        var source = CreateSettingRecord(id, "rob_price", new Money(1.001m), "£1.00");
        var target = CreateSettingRecord(id, "rob_price", new Money(1.004m), "£1.00");

        var field = Assert.Single(GetFieldDifferences(Compare(source, target)));

        Assert.Equal("£1.00 (1.001)", field.SourceValue);
        Assert.Equal("£1.00 (1.004)", field.TargetValue);
    }

    [Fact]
    public void CompareRecords_UnspecifiedDateDifferingOnlyBySubSecond_StoredValueHasNoZ()
    {
        var id = Guid.NewGuid();
        var stored = new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Unspecified);
        var source = CreateSettingRecord(id, DateColumn, stored, null);
        var target = CreateSettingRecord(id, DateColumn, stored.AddMilliseconds(250), null);

        var field = Assert.Single(GetFieldDifferences(Compare(source, target)));

        Assert.Equal("2026-03-01T09:00:00 (2026-03-01T09:00:00.0000000)", field.SourceValue);
        Assert.Equal("2026-03-01T09:00:00 (2026-03-01T09:00:00.2500000)", field.TargetValue);
    }

    [Fact]
    public void CompareRecords_LookupSameNameDifferentId_NoFormattedValues_ReportIncludesIds()
    {
        var id = Guid.NewGuid();
        var sourceParentId = Guid.NewGuid();
        var targetParentId = Guid.NewGuid();
        var source = CreateSettingRecord(id, "rob_parentid", new EntityReference("rob_parent", sourceParentId) { Name = "Parent" }, null);
        var target = CreateSettingRecord(id, "rob_parentid", new EntityReference("rob_parent", targetParentId) { Name = "Parent" }, null);

        var field = Assert.Single(GetFieldDifferences(Compare(source, target)));

        Assert.Equal($"Parent {{{sourceParentId}}}", field.SourceValue);
        Assert.Equal($"Parent {{{targetParentId}}}", field.TargetValue);
    }

    [Fact]
    public void CompareRecords_MultiSelectSameLabelDifferentOptions_ReportIncludesStoredValues()
    {
        var id = Guid.NewGuid();
        var source = CreateSettingRecord(id, "rob_regions",
            new OptionSetValueCollection { new OptionSetValue(1), new OptionSetValue(2) }, "North; South");
        var target = CreateSettingRecord(id, "rob_regions",
            new OptionSetValueCollection { new OptionSetValue(1), new OptionSetValue(3) }, "North; South");

        var field = Assert.Single(GetFieldDifferences(Compare(source, target)));

        Assert.Equal("North; South (1,2)", field.SourceValue);
        Assert.Equal("North; South (1,3)", field.TargetValue);
    }

    [Fact]
    public void CompareRecords_OnlyDifferingColumnsReported_WhenSeveralColumnsCompared()
    {
        var id = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var source = CreateEntity("rob_setting", id, "Setting");
        source["rob_parentid"] = new EntityReference("rob_parent", parentId) { Name = "Old" };
        source["rob_category"] = new OptionSetValue(1);
        source["rob_code"] = "A";
        var target = CreateEntity("rob_setting", id, "Setting");
        target["rob_parentid"] = new EntityReference("rob_parent", parentId) { Name = "New" };
        target["rob_category"] = new OptionSetValue(1);
        target["rob_code"] = "B";

        var field = Assert.Single(GetFieldDifferences(Compare(source, target)));

        Assert.Equal("rob_code", field.FieldName);
    }

    [Fact]
    public void CompareRecords_ImageColumnWithSameBytes_ReturnsNoDifference()
    {
        // An all-columns query returns image columns as byte[]. Before #133 the formatted value
        // ("System.Byte[]") was compared; comparing by reference reports every such record.
        var id = Guid.NewGuid();
        var source = CreateSettingRecord(id, "rob_image", new byte[] { 1, 2, 3 }, null);
        var target = CreateSettingRecord(id, "rob_image", new byte[] { 1, 2, 3 }, null);

        Assert.Empty(Compare(source, target).Differences);
    }

    [Fact]
    public void CompareRecords_AliasedValueWithSameValue_ReturnsNoDifference()
    {
        var id = Guid.NewGuid();
        var source = CreateSettingRecord(id, "parent.rob_code", new AliasedValue("rob_parent", "rob_code", "ABC"), "ABC");
        var target = CreateSettingRecord(id, "parent.rob_code", new AliasedValue("rob_parent", "rob_code", "ABC"), "ABC");

        Assert.Empty(Compare(source, target).Differences);
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
