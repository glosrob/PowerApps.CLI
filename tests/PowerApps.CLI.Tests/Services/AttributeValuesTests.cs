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
}
