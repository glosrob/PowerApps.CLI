using System.Globalization;
using Microsoft.Xrm.Sdk;
using PowerApps.CLI.Infrastructure;
using PowerApps.CLI.Services;
using Xunit;

namespace PowerApps.CLI.Tests.Services;

/// <summary>
/// The comparison and display rules shared by refdata-compare and refdata-migrate. One row per
/// rule: RecordComparerTests only checks how RecordComparer applies them.
/// </summary>
public class AttributeValuesTests
{
    private static readonly Guid FixedId = Guid.Parse("6f9619ff-8b86-d011-b42d-00c04fc964ff");

    #region AreEqual Tests

    [Theory]
    [MemberData(nameof(EqualPairs))]
    public void AreEqual_EqualValues_ReturnsTrue(string rule, object? source, object? target)
    {
        Assert.True(AttributeValues.AreEqual(source, target), rule);
        Assert.True(AttributeValues.AreEqual(target, source), rule);
    }

    public static IEnumerable<object?[]> EqualPairs()
    {
        var utc = new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

        yield return Row("both null", null, null);
        yield return Row("lookup compares by ID only",
            new EntityReference("rob_parent", FixedId) { Name = "Name A" },
            new EntityReference("rob_parent", FixedId) { Name = "Name B" });
        yield return Row("Choice by value", new OptionSetValue(1), new OptionSetValue(1));
        yield return Row("Money by value", new Money(100m), new Money(100m));
        yield return Row("Money ignores scale", new Money(1.0m), new Money(1.00m));
        yield return Row("decimal ignores scale", 1.0m, 1.00m);
        yield return Row("managed property by value", new BooleanManagedProperty(true), new BooleanManagedProperty(true));
        yield return Row("string", "hello", "hello");
        yield return Row("int", 42, 42);
        yield return Row("Yes/No", true, true);
        yield return Row("Guid by value", FixedId, new Guid(FixedId.ToString()));
        yield return Row("date", utc, utc);
        yield return Row("date ignores Kind (same ticks)", utc, new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Unspecified));
        yield return Row("multi-select ignores order", Options(1, 2, 3), Options(3, 1, 2));
        // Deliberate: Dataverse cannot store an option twice, so multi-select compares as a set.
        yield return Row("multi-select is a set", Options(1, 1, 2), Options(2, 1));
        yield return Row("multi-select both empty", Options(), Options());
        yield return Row("multi-select with null element",
            new OptionSetValueCollection { null!, new OptionSetValue(1) },
            new OptionSetValueCollection { new OptionSetValue(1), null! });
        // Image columns come back as byte[]; reference equality reported every image as changed.
        yield return Row("image by content", new byte[] { 1, 2, 3 }, new byte[] { 1, 2, 3 });
        yield return Row("image both empty", Array.Empty<byte>(), Array.Empty<byte>());
        // A filter with a link-entity returns AliasedValue columns; compare what they wrap.
        yield return Row("aliased by wrapped value",
            new AliasedValue("rob_parent", "rob_code", "ABC"), new AliasedValue("rob_parent", "rob_code", "ABC"));
        yield return Row("aliased uses wrapped type's rule",
            new AliasedValue("rob_parent", "rob_parentid", new EntityReference("rob_parent", FixedId) { Name = "A" }),
            new AliasedValue("rob_parent", "rob_parentid", new EntityReference("rob_parent", FixedId) { Name = "B" }));
        yield return Row("nested aliased against plain value",
            new AliasedValue("rob_a", "rob_code", new AliasedValue("rob_b", "rob_code", new OptionSetValue(1))),
            new OptionSetValue(1));
        yield return Row("aliased null against null", new AliasedValue("rob_parent", "rob_code", null), null);
    }

    [Theory]
    [MemberData(nameof(DifferentPairs))]
    public void AreEqual_DifferentValues_ReturnsFalse(string rule, object? source, object? target)
    {
        Assert.False(AttributeValues.AreEqual(source, target), rule);
        Assert.False(AttributeValues.AreEqual(target, source), rule);
    }

    public static IEnumerable<object?[]> DifferentPairs()
    {
        var utc = new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

        yield return Row("lookup with different ID, same name",
            new EntityReference("rob_parent", FixedId) { Name = "Name A" },
            new EntityReference("rob_parent", Guid.NewGuid()) { Name = "Name A" });
        yield return Row("Choice", new OptionSetValue(1), new OptionSetValue(2));
        yield return Row("Money", new Money(100m), new Money(200m));
        yield return Row("managed property", new BooleanManagedProperty(true), new BooleanManagedProperty(false));
        yield return Row("string is case-sensitive", "hello", "Hello");
        yield return Row("int", 42, 99);
        yield return Row("Yes/No", true, false);
        yield return Row("Guid", FixedId, Guid.NewGuid());
        yield return Row("date one second apart", utc, utc.AddSeconds(1));
        yield return Row("multi-select different options", Options(1, 2), Options(1, 3));
        yield return Row("multi-select subset", Options(1, 2), Options(1));
        yield return Row("multi-select null element counts", new OptionSetValueCollection { null!, new OptionSetValue(1) }, Options(1));
        yield return Row("multi-select against single Choice", Options(1), new OptionSetValue(1));
        yield return Row("Choice against int", new OptionSetValue(1), 1);
        yield return Row("string against int", "1", 1);
        yield return Row("image content", new byte[] { 1, 2, 3 }, new byte[] { 1, 2, 4 });
        yield return Row("image length", new byte[] { 1, 2 }, new byte[] { 1, 2, 3 });
        yield return Row("aliased wrapped value",
            new AliasedValue("rob_parent", "rob_code", "ABC"), new AliasedValue("rob_parent", "rob_code", "XYZ"));
        yield return Row("nested aliased against plain value",
            new AliasedValue("rob_a", "rob_code", new AliasedValue("rob_b", "rob_code", new OptionSetValue(1))),
            new OptionSetValue(2));
    }

    /// <summary>
    /// Guards the order in AreEqual: the null check must run before any type-specific rule, so
    /// a typed value against null (or an alias wrapping null) is never treated as equal.
    /// </summary>
    [Theory]
    [MemberData(nameof(TypedValues))]
    public void AreEqual_TypedValueVersusNull_ReturnsFalseInBothOrders(object value)
    {
        Assert.False(AttributeValues.AreEqual(value, null));
        Assert.False(AttributeValues.AreEqual(null, value));
    }

    [Theory]
    [MemberData(nameof(TypedValues))]
    public void AreEqual_TypedValueVersusAliasedNull_ReturnsFalseInBothOrders(object value)
    {
        var aliasedNull = new AliasedValue("rob_parent", "rob_value", null);

        Assert.False(AttributeValues.AreEqual(value, aliasedNull));
        Assert.False(AttributeValues.AreEqual(aliasedNull, value));
    }

    public static IEnumerable<object[]> TypedValues()
    {
        yield return new object[] { new EntityReference("rob_parent", Guid.NewGuid()) { Name = "Parent" } };
        yield return new object[] { new OptionSetValue(100000001) };
        yield return new object[] { new Money(10m) };
        yield return new object[] { new BooleanManagedProperty(true) };
        yield return new object[] { false };
        yield return new object[] { new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc) };
        yield return new object[] { new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Unspecified) };
        yield return new object[] { 0m };
        yield return new object[] { new OptionSetValueCollection { new OptionSetValue(1) } };
        yield return new object[] { new OptionSetValueCollection() };
        yield return new object[] { new byte[] { 1, 2, 3 } };
        yield return new object[] { Array.Empty<byte>() };
        yield return new object[] { new AliasedValue("rob_parent", "rob_code", "ABC") };
    }

    #endregion

    #region FormatForDisplay Tests

    /// <summary>
    /// Dates are ISO 8601 whatever FormattedValues says: Z for UTC, no Z and no shift for
    /// Unspecified (Date Only / Time-Zone Independent), Local converted to UTC. The time-zone
    /// shift itself is guarded on any machine by AttributeValuesTimeZoneTests.
    /// </summary>
    [Theory]
    [MemberData(nameof(DateDisplayRows))]
    public void FormatForDisplay_Dates_UseIso8601AndIgnoreFormattedValue(string rule, object stored, string expected)
    {
        var entity = new Entity("rob_setting", Guid.NewGuid());
        entity["rob_effectivefrom"] = stored;
        entity.FormattedValues["rob_effectivefrom"] = "01/03/2026 09:05";

        Assert.Equal(expected, AttributeValues.FormatForDisplay(entity, "rob_effectivefrom"));
        _ = rule; // names the row in test output
    }

    public static IEnumerable<object[]> DateDisplayRows()
    {
        yield return new object[] { "UTC gets Z", new DateTime(2026, 3, 1, 9, 5, 7, DateTimeKind.Utc), "2026-03-01T09:05:07Z" };
        yield return new object[] { "Unspecified has no Z", new DateTime(2026, 3, 31, 23, 30, 0, DateTimeKind.Unspecified), "2026-03-31T23:30:00" };
        yield return new object[] { "Unspecified midnight", new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Unspecified), "2026-07-01T00:00:00" };
        yield return new object[] { "Local shown as UTC", new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc).ToLocalTime(), "2026-03-01T09:00:00Z" };
        yield return new object[] { "aliased date",
            new AliasedValue("rob_parent", "rob_effectivefrom", new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc)), "2026-03-01T09:00:00Z" };
        yield return new object[] { "nested aliased date",
            new AliasedValue("rob_a", "rob_effectivefrom",
                new AliasedValue("rob_b", "rob_effectivefrom", new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Unspecified))),
            "2026-03-01T00:00:00" };
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

    /// <summary>
    /// For every type #133 did not mean to change, FormatForDisplay must give exactly what
    /// EntityExtensions.GetFormattedValue gives (pinned in EntityExtensionsTests), with and
    /// without a formatted value.
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
        yield return new object[] { FixedId };
        yield return new object[] { new OptionSetValue(100000001) };
        yield return new object[] { new Money(1234.567m) };
        yield return new object[] { new BooleanManagedProperty(true) };
        yield return new object[] { new EntityReference("rob_parent", FixedId) { Name = "Parent" } };
        yield return new object[] { new EntityReference("rob_parent", FixedId) };
    }

    /// <summary>The fallbacks #133 added or changed, used when there is no formatted value.</summary>
    [Theory]
    [MemberData(nameof(NewFallbackRows))]
    public void FormatForDisplay_WithoutFormattedValue_UsesFallback(string rule, object stored, string expected)
    {
        var entity = new Entity("rob_setting", Guid.NewGuid());
        entity["rob_value"] = stored;

        Assert.Equal(expected, AttributeValues.FormatForDisplay(entity, "rob_value"));
        _ = rule; // names the row in test output
    }

    public static IEnumerable<object[]> NewFallbackRows()
    {
        yield return new object[] { "multi-select lists values", Options(1, 2), "1, 2" };
        yield return new object[] { "empty multi-select", Options(), string.Empty };
        yield return new object[] { "image shows byte count", new byte[] { 1, 2, 3 }, "3 bytes" };
        yield return new object[] { "one-byte image", new byte[] { 1 }, "1 byte" };
        yield return new object[] { "empty image", Array.Empty<byte>(), "0 bytes" };
        yield return new object[] { "aliased string", new AliasedValue("rob_parent", "rob_code", "ABC"), "ABC" };
        yield return new object[] { "aliased lookup without name shows ID",
            new AliasedValue("rob_parent", "rob_ownerid", new EntityReference("rob_owner", FixedId)), FixedId.ToString() };
        yield return new object[] { "aliased Money uses F2",
            new AliasedValue("rob_parent", "rob_price", new Money(5m)), 5m.ToString("F2") };
    }

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

    [Theory]
    [InlineData("missing column")]
    [InlineData("null value")]
    [InlineData("aliased null")]
    public void FormatForDisplay_NoValue_ReturnsNullEvenWithFormattedValue(string kind)
    {
        var entity = new Entity("rob_setting", Guid.NewGuid());
        if (kind == "null value")
        {
            entity["rob_value"] = null;
        }
        else if (kind == "aliased null")
        {
            entity["rob_value"] = new AliasedValue("rob_parent", "rob_value", null);
        }

        entity.FormattedValues["rob_value"] = "stale";

        Assert.Null(AttributeValues.FormatForDisplay(entity, "rob_value"));
    }

    [Fact]
    public void FormatForDisplay_NullEntity_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => AttributeValues.FormatForDisplay(null!, "rob_code"));
    }

    #endregion

    #region FormatStoredValue Tests

    [Theory]
    [MemberData(nameof(StoredValueRows))]
    public void FormatStoredValue_FormatsEachTypeInvariantly(string rule, object? stored, string? expected)
    {
        Assert.Equal(expected, AttributeValues.FormatStoredValue(stored));
        _ = rule; // names the row in test output
    }

    public static IEnumerable<object?[]> StoredValueRows()
    {
        yield return Row("null", null, null);
        yield return Row("lookup gives ID", new EntityReference("rob_parent", FixedId) { Name = "X" }, FixedId.ToString());
        yield return Row("Choice", new OptionSetValue(100000001), "100000001");
        yield return Row("multi-select in stored order", Options(2, 1), "2,1");
        yield return Row("multi-select null element", new OptionSetValueCollection { new OptionSetValue(1), null! }, "1,null");
        yield return Row("Money", new Money(1234.5m), "1234.5");
        yield return Row("managed property", new BooleanManagedProperty(true), "True");
        yield return Row("decimal", 1.5m, "1.5");
        yield return Row("UTC date round-trip with Z", new DateTime(2026, 3, 1, 9, 0, 0, 1, DateTimeKind.Utc), "2026-03-01T09:00:00.0010000Z");
        yield return Row("Unspecified date round-trip without Z", new DateTime(2026, 3, 1, 9, 0, 0, 500, DateTimeKind.Unspecified), "2026-03-01T09:00:00.5000000");
        yield return Row("string", "abc", "abc");
        yield return Row("Guid", FixedId, FixedId.ToString());
        yield return Row("image hash prefix", new byte[] { 1, 2, 3 }, "SHA-256 039058C6F2C0");
        // SHA-256 of no bytes is e3b0c442 98fc1c14...
        yield return Row("empty image hash", Array.Empty<byte>(), "SHA-256 E3B0C44298FC");
        yield return Row("aliased uses wrapped value", new AliasedValue("rob_parent", "rob_category", new OptionSetValue(100000001)), "100000001");
    }

    [Fact]
    public void FormatStoredValue_ImageHash_DiffersForDifferentContent()
    {
        Assert.NotEqual(
            AttributeValues.FormatStoredValue(new byte[] { 1, 2, 3 }),
            AttributeValues.FormatStoredValue(new byte[] { 1, 2, 4 }));
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

    #endregion

    #region Unwrap Tests

    [Fact]
    public void Unwrap_NestedAliasedValues_ReturnsInnermostValue()
    {
        var nested = new AliasedValue("rob_a", "rob_code", new AliasedValue("rob_b", "rob_code", "ABC"));

        Assert.Equal("ABC", AttributeValues.Unwrap(nested));
        Assert.Null(AttributeValues.Unwrap(new AliasedValue("rob_a", "rob_code", new AliasedValue("rob_b", "rob_code", null))));
        Assert.Null(AttributeValues.Unwrap(null));
        Assert.Equal(42, AttributeValues.Unwrap(42));
    }

    #endregion

    #region Helpers

    private static object?[] Row(string rule, object? first, object? second)
    {
        return new[] { rule, first, second };
    }

    private static OptionSetValueCollection Options(params int[] values)
    {
        var options = new OptionSetValueCollection();
        foreach (var value in values)
        {
            options.Add(new OptionSetValue(value));
        }

        return options;
    }

    #endregion
}
