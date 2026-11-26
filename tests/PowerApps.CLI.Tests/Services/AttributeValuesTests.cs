using System.Globalization;
using PowerApps.CLI.Infrastructure;
using Microsoft.Xrm.Sdk;
using PowerApps.CLI.Services;
using Xunit;

namespace PowerApps.CLI.Tests.Services;

public class AttributeValuesTests
{
    #region AreEqual Tests

    [Fact]
    public void AreEqual_BothNull_ReturnsTrue()
    {
        Assert.True(AttributeValues.AreEqual(null, null));
    }

    [Fact]
    public void AreEqual_OneNull_ReturnsFalse()
    {
        Assert.False(AttributeValues.AreEqual("test", null));
        Assert.False(AttributeValues.AreEqual(null, "test"));
    }

    [Fact]
    public void AreEqual_EntityReference_ComparesByIdOnly()
    {
        var id = Guid.NewGuid();
        var ref1 = new EntityReference("account", id) { Name = "Name A" };
        var ref2 = new EntityReference("account", id) { Name = "Name B" };
        var ref3 = new EntityReference("account", Guid.NewGuid()) { Name = "Name A" };

        Assert.True(AttributeValues.AreEqual(ref1, ref2));
        Assert.False(AttributeValues.AreEqual(ref1, ref3));
    }

    [Fact]
    public void AreEqual_OptionSetValue_ComparesByValue()
    {
        Assert.True(AttributeValues.AreEqual(new OptionSetValue(1), new OptionSetValue(1)));
        Assert.False(AttributeValues.AreEqual(new OptionSetValue(1), new OptionSetValue(2)));
    }

    [Fact]
    public void AreEqual_Money_ComparesByValue()
    {
        Assert.True(AttributeValues.AreEqual(new Money(100.00m), new Money(100.00m)));
        Assert.False(AttributeValues.AreEqual(new Money(100.00m), new Money(200.00m)));
    }

    [Fact]
    public void AreEqual_BooleanManagedProperty_ComparesByValue()
    {
        Assert.True(AttributeValues.AreEqual(new BooleanManagedProperty(true), new BooleanManagedProperty(true)));
        Assert.False(AttributeValues.AreEqual(new BooleanManagedProperty(true), new BooleanManagedProperty(false)));
    }

    [Fact]
    public void AreEqual_Strings_UsesEquals()
    {
        Assert.True(AttributeValues.AreEqual("hello", "hello"));
        Assert.False(AttributeValues.AreEqual("hello", "world"));
        Assert.False(AttributeValues.AreEqual("hello", "Hello"));
    }

    [Fact]
    public void AreEqual_Integers_UsesEquals()
    {
        Assert.True(AttributeValues.AreEqual(42, 42));
        Assert.False(AttributeValues.AreEqual(42, 99));
    }

    [Fact]
    public void AreEqual_DateTimes_ComparesTicksIgnoringKind()
    {
        var utc = new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);
        var unspecified = new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Unspecified);

        Assert.True(AttributeValues.AreEqual(utc, utc));
        Assert.True(AttributeValues.AreEqual(utc, unspecified));
        Assert.False(AttributeValues.AreEqual(utc, utc.AddSeconds(1)));
    }

    [Fact]
    public void AreEqual_MultiSelectChoices_SameValuesDifferentOrder_ReturnsTrue()
    {
        var source = new OptionSetValueCollection { new OptionSetValue(1), new OptionSetValue(2), new OptionSetValue(3) };
        var target = new OptionSetValueCollection { new OptionSetValue(3), new OptionSetValue(1), new OptionSetValue(2) };

        Assert.True(AttributeValues.AreEqual(source, target));
    }

    [Fact]
    public void AreEqual_MultiSelectChoices_DifferentValues_ReturnsFalse()
    {
        var source = new OptionSetValueCollection { new OptionSetValue(1), new OptionSetValue(2) };
        var target = new OptionSetValueCollection { new OptionSetValue(1), new OptionSetValue(3) };

        Assert.False(AttributeValues.AreEqual(source, target));
    }

    [Fact]
    public void AreEqual_MultiSelectChoices_SubsetOfOther_ReturnsFalse()
    {
        var source = new OptionSetValueCollection { new OptionSetValue(1), new OptionSetValue(2) };
        var target = new OptionSetValueCollection { new OptionSetValue(1) };

        Assert.False(AttributeValues.AreEqual(source, target));
        Assert.False(AttributeValues.AreEqual(target, source));
    }

    [Fact]
    public void AreEqual_MultiSelectChoices_BothEmpty_ReturnsTrue()
    {
        Assert.True(AttributeValues.AreEqual(new OptionSetValueCollection(), new OptionSetValueCollection()));
    }

    [Fact]
    public void AreEqual_MultiSelectChoices_VersusNull_ReturnsFalse()
    {
        var options = new OptionSetValueCollection { new OptionSetValue(1) };

        Assert.False(AttributeValues.AreEqual(options, null));
        Assert.False(AttributeValues.AreEqual(null, options));
    }

    [Fact]
    public void AreEqual_DifferentTypes_ReturnsFalse()
    {
        Assert.False(AttributeValues.AreEqual(new OptionSetValue(1), 1));
        Assert.False(AttributeValues.AreEqual("1", 1));
    }

    #endregion

    #region FormatForDisplay Tests

    [Fact]
    public void FormatForDisplay_UtcDate_UsesIsoFormatWithZ()
    {
        var entity = new Entity("rob_setting", Guid.NewGuid());
        entity["rob_effectivefrom"] = new DateTime(2026, 3, 1, 9, 5, 7, DateTimeKind.Utc);
        entity.FormattedValues["rob_effectivefrom"] = "01/03/2026 09:05";

        Assert.Equal("2026-03-01T09:05:07Z", AttributeValues.FormatForDisplay(entity, "rob_effectivefrom"));
    }

    [Fact]
    public void FormatForDisplay_UnspecifiedDate_UsesIsoFormatWithoutZAndIsNotShifted()
    {
        // 23:30 on the last day of a month: any time-zone conversion on a machine that is not on
        // UTC moves the hour, and often the day and month, so the string would not match.
        var entity = new Entity("rob_setting", Guid.NewGuid());
        entity["rob_effectivefrom"] = new DateTime(2026, 3, 31, 23, 30, 0, DateTimeKind.Unspecified);
        entity.FormattedValues["rob_effectivefrom"] = "31/03/2026";

        Assert.Equal("2026-03-31T23:30:00", AttributeValues.FormatForDisplay(entity, "rob_effectivefrom"));
    }

    [Fact]
    public void FormatForDisplay_UnspecifiedDate_DoesNotMatchAnyShiftedValue()
    {
        var value = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var entity = new Entity("rob_setting", Guid.NewGuid());
        entity["rob_effectivefrom"] = value;

        var display = AttributeValues.FormatForDisplay(entity, "rob_effectivefrom");

        Assert.Equal("2026-07-01T00:00:00", display);
        if (TimeZoneInfo.Local.GetUtcOffset(value) != TimeSpan.Zero)
        {
            Assert.NotEqual(value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss"), display);
            Assert.NotEqual(value.ToLocalTime().ToString("yyyy-MM-dd'T'HH:mm:ss"), display);
        }
    }

    [Fact]
    public void FormatForDisplay_LocalDate_IsShownAsUtc()
    {
        var utc = new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);
        var entity = new Entity("rob_setting", Guid.NewGuid());
        entity["rob_effectivefrom"] = utc.ToLocalTime();

        Assert.Equal("2026-03-01T09:00:00Z", AttributeValues.FormatForDisplay(entity, "rob_effectivefrom"));
    }

    [Fact]
    public void FormatForDisplay_Choice_UsesFormattedValue()
    {
        var entity = new Entity("rob_setting", Guid.NewGuid());
        entity["rob_category"] = new OptionSetValue(100000001);
        entity.FormattedValues["rob_category"] = "Standard";

        Assert.Equal("Standard", AttributeValues.FormatForDisplay(entity, "rob_category"));
    }

    [Fact]
    public void FormatForDisplay_LookupWithoutFormattedValue_FallsBackToName()
    {
        var entity = new Entity("rob_setting", Guid.NewGuid());
        entity["rob_parentid"] = new EntityReference("rob_parent", Guid.NewGuid()) { Name = "Parent A" };

        Assert.Equal("Parent A", AttributeValues.FormatForDisplay(entity, "rob_parentid"));
    }

    [Fact]
    public void FormatForDisplay_String_ReturnsValue()
    {
        var entity = new Entity("rob_setting", Guid.NewGuid());
        entity["rob_code"] = "ABC";

        Assert.Equal("ABC", AttributeValues.FormatForDisplay(entity, "rob_code"));
    }

    [Fact]
    public void FormatForDisplay_MissingColumn_ReturnsNull()
    {
        var entity = new Entity("rob_setting", Guid.NewGuid());

        Assert.Null(AttributeValues.FormatForDisplay(entity, "rob_effectivefrom"));
    }

    [Fact]
    public void FormatForDisplay_NullValue_ReturnsNullEvenWithFormattedValue()
    {
        var entity = new Entity("rob_setting", Guid.NewGuid());
        entity["rob_effectivefrom"] = null;
        entity.FormattedValues["rob_effectivefrom"] = "stale";

        Assert.Null(AttributeValues.FormatForDisplay(entity, "rob_effectivefrom"));
    }

    [Fact]
    public void FormatForDisplay_NullEntity_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => AttributeValues.FormatForDisplay(null!, "rob_code"));
    }

    #endregion

    #region FormatStoredValue Tests

    [Fact]
    public void FormatStoredValue_FormatsEachTypeInvariantly()
    {
        var id = Guid.NewGuid();

        Assert.Null(AttributeValues.FormatStoredValue(null));
        Assert.Equal(id.ToString(), AttributeValues.FormatStoredValue(new EntityReference("rob_parent", id) { Name = "X" }));
        Assert.Equal("100000001", AttributeValues.FormatStoredValue(new OptionSetValue(100000001)));
        Assert.Equal("1,2", AttributeValues.FormatStoredValue(new OptionSetValueCollection { new OptionSetValue(1), new OptionSetValue(2) }));
        Assert.Equal("1234.5", AttributeValues.FormatStoredValue(new Money(1234.5m)));
        Assert.Equal("True", AttributeValues.FormatStoredValue(new BooleanManagedProperty(true)));
        Assert.Equal("1.5", AttributeValues.FormatStoredValue(1.5m));
        Assert.Equal("2026-03-01T09:00:00.0010000Z",
            AttributeValues.FormatStoredValue(new DateTime(2026, 3, 1, 9, 0, 0, 1, DateTimeKind.Utc)));
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void AreEqual_MultiSelectChoices_DuplicateOptions_TreatedAsSet()
    {
        // Deliberate: Dataverse cannot store the same option twice in a multi-select column, so
        // the comparison is by set. A change to count duplicates should be a conscious decision.
        var withDuplicate = new OptionSetValueCollection { new OptionSetValue(1), new OptionSetValue(1), new OptionSetValue(2) };
        var distinct = new OptionSetValueCollection { new OptionSetValue(2), new OptionSetValue(1) };

        Assert.True(AttributeValues.AreEqual(withDuplicate, distinct));
        Assert.True(AttributeValues.AreEqual(distinct, withDuplicate));
    }

    [Fact]
    public void AreEqual_MultiSelectChoices_NullElement_DoesNotThrow()
    {
        var source = new OptionSetValueCollection { null!, new OptionSetValue(1) };
        var target = new OptionSetValueCollection { new OptionSetValue(1), null! };

        Assert.True(AttributeValues.AreEqual(source, target));
        Assert.False(AttributeValues.AreEqual(source, new OptionSetValueCollection { new OptionSetValue(1) }));
    }

    [Fact]
    public void AreEqual_MultiSelectVersusSingleChoice_ReturnsFalse()
    {
        Assert.False(AttributeValues.AreEqual(
            new OptionSetValueCollection { new OptionSetValue(1) }, new OptionSetValue(1)));
    }

    [Fact]
    public void AreEqual_DecimalsWithDifferentScale_ReturnsTrue()
    {
        Assert.True(AttributeValues.AreEqual(1.0m, 1.00m));
        Assert.True(AttributeValues.AreEqual(new Money(1.0m), new Money(1.00m)));
    }

    [Fact]
    public void AreEqual_Guids_ComparesByValue()
    {
        var id = Guid.NewGuid();

        Assert.True(AttributeValues.AreEqual(id, new Guid(id.ToString())));
        Assert.False(AttributeValues.AreEqual(id, Guid.NewGuid()));
    }

    [Fact]
    public void AreEqual_ByteArraysWithSameContent_ReturnsTrue()
    {
        // Image columns come back as byte[] from an all-columns query. Reference equality would
        // report every record with an image as changed.
        Assert.True(AttributeValues.AreEqual(new byte[] { 1, 2, 3 }, new byte[] { 1, 2, 3 }));
        Assert.False(AttributeValues.AreEqual(new byte[] { 1, 2, 3 }, new byte[] { 1, 2, 4 }));
    }

    [Fact]
    public void AreEqual_AliasedValuesWithSameValue_ReturnsTrue()
    {
        // A table filter can contain a link-entity, which returns AliasedValue columns.
        var source = new AliasedValue("rob_parent", "rob_code", "ABC");
        var target = new AliasedValue("rob_parent", "rob_code", "ABC");

        Assert.True(AttributeValues.AreEqual(source, target));
        Assert.False(AttributeValues.AreEqual(source, new AliasedValue("rob_parent", "rob_code", "XYZ")));
    }

    [Fact]
    public void FormatStoredValue_UsesInvariantCultureWhenCurrentCultureDiffers()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            Assert.Equal("1234.5", AttributeValues.FormatStoredValue(new Money(1234.5m)));
            Assert.Equal("1.5", AttributeValues.FormatStoredValue(1.5m));
            Assert.Equal("0.25", AttributeValues.FormatStoredValue(0.25d));
            Assert.Equal("1234567", AttributeValues.FormatStoredValue(1234567));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void FormatStoredValue_UnspecifiedDate_HasNoZ()
    {
        Assert.Equal("2026-03-01T09:00:00.5000000",
            AttributeValues.FormatStoredValue(new DateTime(2026, 3, 1, 9, 0, 0, 500, DateTimeKind.Unspecified)));
    }

    [Fact]
    public void FormatStoredValue_StringAndGuid_ReturnedAsIs()
    {
        var id = Guid.NewGuid();

        Assert.Equal("abc", AttributeValues.FormatStoredValue("abc"));
        Assert.Equal(id.ToString(), AttributeValues.FormatStoredValue(id));
    }

    [Fact]
    public void FormatForDisplay_Dates_UseGregorianDigitsWhenCurrentCultureUsesAnotherCalendar()
    {
        // th-TH uses the Buddhist calendar (2026 is 2569); the ISO output must not follow it.
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("th-TH");
            var entity = new Entity("rob_setting", Guid.NewGuid());
            entity["rob_effectivefrom"] = new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);
            entity["rob_validuntil"] = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Unspecified);

            Assert.Equal("2026-03-01T09:00:00Z", AttributeValues.FormatForDisplay(entity, "rob_effectivefrom"));
            Assert.Equal("2026-03-01T00:00:00", AttributeValues.FormatForDisplay(entity, "rob_validuntil"));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void FormatForDisplay_Date_IgnoresFormattedValue()
    {
        var entity = new Entity("rob_setting", Guid.NewGuid());
        entity["rob_effectivefrom"] = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Unspecified);
        entity.FormattedValues["rob_effectivefrom"] = "01/03/2026";

        Assert.Equal("2026-03-01T00:00:00", AttributeValues.FormatForDisplay(entity, "rob_effectivefrom"));
    }

    #endregion

    #region Image, Aliased and Multi-select Display Tests

    [Fact]
    public void AreEqual_ByteArrays_DifferentLengthsAndEmpty()
    {
        Assert.False(AttributeValues.AreEqual(new byte[] { 1, 2 }, new byte[] { 1, 2, 3 }));
        Assert.True(AttributeValues.AreEqual(Array.Empty<byte>(), Array.Empty<byte>()));
        Assert.False(AttributeValues.AreEqual(new byte[] { 1 }, null));
    }

    [Fact]
    public void AreEqual_AliasedValue_ComparedWithItsWrappedValueRules()
    {
        var id = Guid.NewGuid();
        var source = new AliasedValue("rob_parent", "rob_parentid", new EntityReference("rob_parent", id) { Name = "A" });
        var target = new AliasedValue("rob_parent", "rob_parentid", new EntityReference("rob_parent", id) { Name = "B" });

        Assert.True(AttributeValues.AreEqual(source, target));
        Assert.True(AttributeValues.AreEqual(new AliasedValue("rob_parent", "rob_code", null), null));
        Assert.False(AttributeValues.AreEqual(new AliasedValue("rob_parent", "rob_code", "ABC"), null));
    }

    [Fact]
    public void FormatForDisplay_ImageColumn_ShowsByteCount()
    {
        var entity = new Entity("rob_setting", Guid.NewGuid());
        entity["rob_image"] = new byte[] { 1, 2, 3 };

        Assert.Equal("3 bytes", AttributeValues.FormatForDisplay(entity, "rob_image"));

        entity["rob_image"] = new byte[] { 1 };
        Assert.Equal("1 byte", AttributeValues.FormatForDisplay(entity, "rob_image"));
    }

    [Fact]
    public void FormatStoredValue_ImageColumn_ShowsHashPrefix()
    {
        var stored = AttributeValues.FormatStoredValue(new byte[] { 1, 2, 3 });

        Assert.Equal("SHA-256 039058C6F2C0", stored);
        Assert.NotEqual(stored, AttributeValues.FormatStoredValue(new byte[] { 1, 2, 4 }));
    }

    [Fact]
    public void FormatForDisplay_MultiSelectWithoutFormattedValue_ShowsOptionValues()
    {
        var entity = new Entity("rob_setting", Guid.NewGuid());
        entity["rob_regions"] = new OptionSetValueCollection { new OptionSetValue(1), new OptionSetValue(2) };

        Assert.Equal("1, 2", AttributeValues.FormatForDisplay(entity, "rob_regions"));
    }

    [Fact]
    public void FormatForDisplay_AliasedValue_ShowsWrappedValue()
    {
        var entity = new Entity("rob_setting", Guid.NewGuid());
        entity["parent.rob_code"] = new AliasedValue("rob_parent", "rob_code", "ABC");
        entity["parent.rob_effectivefrom"] = new AliasedValue("rob_parent", "rob_effectivefrom",
            new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc));
        entity["parent.rob_empty"] = new AliasedValue("rob_parent", "rob_empty", null);

        Assert.Equal("ABC", AttributeValues.FormatForDisplay(entity, "parent.rob_code"));
        Assert.Equal("2026-03-01T09:00:00Z", AttributeValues.FormatForDisplay(entity, "parent.rob_effectivefrom"));
        Assert.Null(AttributeValues.FormatForDisplay(entity, "parent.rob_empty"));
    }

    [Fact]
    public void FormatStoredValue_AliasedValue_UsesWrappedValue()
    {
        Assert.Equal("100000001", AttributeValues.FormatStoredValue(new AliasedValue("rob_parent", "rob_category", new OptionSetValue(100000001))));
    }

    #endregion

    #region Display Fallback Parity Tests

    /// <summary>
    /// For every type the fix did not mean to change, FormatForDisplay must give exactly what
    /// EntityExtensions.GetFormattedValue gives, with and without a formatted value.
    /// </summary>
    [Theory]
    [MemberData(nameof(UnchangedDisplayTypes))]
    public void FormatForDisplay_NonDateTypes_MatchGetFormattedValue(object value)
    {
        var withoutFormatted = new Entity("rob_setting", Guid.NewGuid());
        withoutFormatted["rob_value"] = value;
        var withFormatted = new Entity("rob_setting", Guid.NewGuid());
        withFormatted["rob_value"] = value;
        withFormatted.FormattedValues["rob_value"] = "Label";

        Assert.Equal(withoutFormatted.GetFormattedValue("rob_value"), AttributeValues.FormatForDisplay(withoutFormatted, "rob_value"));
        Assert.Equal(withFormatted.GetFormattedValue("rob_value"), AttributeValues.FormatForDisplay(withFormatted, "rob_value"));
    }

    [Theory]
    [MemberData(nameof(UnchangedDisplayTypes))]
    public void FormatForDisplay_NonDateTypes_MatchGetFormattedValueUnderAnotherCulture(object value)
    {
        // Money uses F2 and numbers use ToString() in the current culture, as GetFormattedValue does.
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            var entity = new Entity("rob_setting", Guid.NewGuid());
            entity["rob_value"] = value;

            Assert.Equal(entity.GetFormattedValue("rob_value"), AttributeValues.FormatForDisplay(entity, "rob_value"));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    public static IEnumerable<object[]> UnchangedDisplayTypes()
    {
        yield return new object[] { "ABC" };
        yield return new object[] { string.Empty };
        yield return new object[] { 1234567 };
        yield return new object[] { 1234567890123L };
        yield return new object[] { 1234.5m };
        yield return new object[] { 0.25d };
        yield return new object[] { true };
        yield return new object[] { Guid.Parse("6f9619ff-8b86-d011-b42d-00c04fc964ff") };
        yield return new object[] { new OptionSetValue(100000001) };
        yield return new object[] { new Money(1234.567m) };
        yield return new object[] { new BooleanManagedProperty(true) };
        yield return new object[] { new EntityReference("rob_parent", Guid.Parse("6f9619ff-8b86-d011-b42d-00c04fc964ff")) { Name = "Parent" } };
        yield return new object[] { new EntityReference("rob_parent", Guid.Parse("6f9619ff-8b86-d011-b42d-00c04fc964ff")) };
    }

    #endregion

    #region Aliased Value And Image Edge Case Tests

    [Fact]
    public void FormatForDisplay_AliasedValue_UsesFormattedValueKeyedUnderAlias()
    {
        var entity = new Entity("rob_setting", Guid.NewGuid());
        entity["parent.rob_category"] = new AliasedValue("rob_parent", "rob_category", new OptionSetValue(100000001));
        entity.FormattedValues["parent.rob_category"] = "Standard";
        entity["parent.rob_ownerid"] = new AliasedValue("rob_parent", "rob_ownerid",
            new EntityReference("rob_owner", Guid.NewGuid()) { Name = "Raw Name" });
        entity.FormattedValues["parent.rob_ownerid"] = "Formatted Name";

        Assert.Equal("Standard", AttributeValues.FormatForDisplay(entity, "parent.rob_category"));
        Assert.Equal("Formatted Name", AttributeValues.FormatForDisplay(entity, "parent.rob_ownerid"));
    }

    [Fact]
    public void FormatForDisplay_AliasedValue_WithoutFormattedValue_UsesWrappedFallback()
    {
        var parentId = Guid.NewGuid();
        var entity = new Entity("rob_setting", Guid.NewGuid());
        entity["parent.rob_ownerid"] = new AliasedValue("rob_parent", "rob_ownerid", new EntityReference("rob_owner", parentId));
        entity["parent.rob_price"] = new AliasedValue("rob_parent", "rob_price", new Money(5m));

        Assert.Equal(parentId.ToString(), AttributeValues.FormatForDisplay(entity, "parent.rob_ownerid"));
        Assert.Equal(5m.ToString("F2"), AttributeValues.FormatForDisplay(entity, "parent.rob_price"));
    }

    [Fact]
    public void Unwrap_NestedAliasedValues_ReturnsInnermostValue()
    {
        var nested = new AliasedValue("rob_a", "rob_code", new AliasedValue("rob_b", "rob_code", "ABC"));

        Assert.Equal("ABC", AttributeValues.Unwrap(nested));
        Assert.Null(AttributeValues.Unwrap(new AliasedValue("rob_a", "rob_code", new AliasedValue("rob_b", "rob_code", null))));
        Assert.Null(AttributeValues.Unwrap(null));
    }

    [Fact]
    public void AreEqual_NestedAliasedValue_ComparesInnermostValues()
    {
        var nested = new AliasedValue("rob_a", "rob_code", new AliasedValue("rob_b", "rob_code", new OptionSetValue(1)));

        Assert.True(AttributeValues.AreEqual(nested, new OptionSetValue(1)));
        Assert.True(AttributeValues.AreEqual(new OptionSetValue(1), nested));
        Assert.False(AttributeValues.AreEqual(nested, new OptionSetValue(2)));
    }

    [Fact]
    public void FormatForDisplay_NestedAliasedDate_UsesIsoFormat()
    {
        var entity = new Entity("rob_setting", Guid.NewGuid());
        entity["a.rob_effectivefrom"] = new AliasedValue("rob_a", "rob_effectivefrom",
            new AliasedValue("rob_b", "rob_effectivefrom", new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Unspecified)));
        entity.FormattedValues["a.rob_effectivefrom"] = "01/03/2026";

        Assert.Equal("2026-03-01T00:00:00", AttributeValues.FormatForDisplay(entity, "a.rob_effectivefrom"));
    }

    [Fact]
    public void FormatStoredValue_ImageHash_IsDeterministicAcrossInstances()
    {
        var first = AttributeValues.FormatStoredValue(new byte[] { 9, 8, 7, 6 });
        var second = AttributeValues.FormatStoredValue(new byte[] { 9, 8, 7, 6 });

        Assert.Equal(first, second);
        Assert.Matches("^SHA-256 [0-9A-F]{12}$", first);
    }

    [Fact]
    public void FormatStoredValue_EmptyImage_HashesEmptyInput()
    {
        // SHA-256 of no bytes is e3b0c442 98fc1c14...
        Assert.Equal("SHA-256 E3B0C44298FC", AttributeValues.FormatStoredValue(Array.Empty<byte>()));
    }

    [Fact]
    public void FormatForDisplay_EmptyImage_ShowsZeroBytes()
    {
        var entity = new Entity("rob_setting", Guid.NewGuid());
        entity["rob_image"] = Array.Empty<byte>();

        Assert.Equal("0 bytes", AttributeValues.FormatForDisplay(entity, "rob_image"));
    }

    [Fact]
    public void FormatForDisplay_EmptyMultiSelectWithoutFormattedValue_ReturnsEmptyString()
    {
        var entity = new Entity("rob_setting", Guid.NewGuid());
        entity["rob_regions"] = new OptionSetValueCollection();

        Assert.Equal(string.Empty, AttributeValues.FormatForDisplay(entity, "rob_regions"));
    }

    #endregion
}
