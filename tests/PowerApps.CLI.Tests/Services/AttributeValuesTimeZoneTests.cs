using System.Reflection;
using Microsoft.Xrm.Sdk;
using PowerApps.CLI.Services;
using Xunit;

namespace PowerApps.CLI.Tests.Services;

/// <summary>
/// Changing <see cref="TimeZoneInfo.Local"/> is process-wide, so these tests run on their own,
/// after every parallel test has finished.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public class LocalTimeZoneCollection
{
    public const string Name = "Local time zone";
}

/// <summary>
/// Date display tests that run with a fake local time zone (UTC+05:30, no daylight saving), so a
/// stray ToUniversalTime/ToLocalTime is caught on any machine, including a CI runner on UTC.
/// </summary>
[Collection(LocalTimeZoneCollection.Name)]
public class AttributeValuesTimeZoneTests
{
    private const string Column = "rob_effectivefrom";

    private static readonly TimeZoneInfo FakeZone = TimeZoneInfo.CreateCustomTimeZone(
        "rob_test_zone", TimeSpan.FromHours(5.5), "rob test zone", "rob test zone");

    [Fact]
    public void FormatForDisplay_UnspecifiedDate_IsNotShiftedWhenLocalZoneIsNotUtc()
    {
        var value = new DateTime(2026, 3, 31, 23, 30, 0, DateTimeKind.Unspecified);

        var display = WithLocalTimeZone(FakeZone, () =>
        {
            // Proves the fake zone is in effect: a converted value would read differently.
            Assert.Equal(new DateTime(2026, 3, 31, 18, 0, 0), value.ToUniversalTime());
            Assert.Equal(new DateTime(2026, 4, 1, 5, 0, 0), value.ToLocalTime());

            return AttributeValues.FormatForDisplay(CreateRecord(value), Column);
        });

        Assert.Equal("2026-03-31T23:30:00", display);
    }

    [Fact]
    public void FormatForDisplay_UtcDate_IsNotShiftedWhenLocalZoneIsNotUtc()
    {
        var value = new DateTime(2026, 3, 31, 23, 30, 0, DateTimeKind.Utc);

        var display = WithLocalTimeZone(FakeZone,
            () => AttributeValues.FormatForDisplay(CreateRecord(value), Column));

        Assert.Equal("2026-03-31T23:30:00Z", display);
    }

    [Fact]
    public void FormatForDisplay_LocalDate_IsConvertedToUtcWhenLocalZoneIsNotUtc()
    {
        // On a UTC machine a Local value needs no conversion, so only a non-UTC zone shows
        // whether ToUniversalTime is actually applied.
        var local = new DateTime(2026, 4, 1, 5, 0, 0, DateTimeKind.Local);

        var display = WithLocalTimeZone(FakeZone,
            () => AttributeValues.FormatForDisplay(CreateRecord(local), Column));

        Assert.Equal("2026-03-31T23:30:00Z", display);
    }

    [Fact]
    public void FormatStoredValue_UnspecifiedDate_IsNotShiftedWhenLocalZoneIsNotUtc()
    {
        var value = new DateTime(2026, 3, 31, 23, 30, 0, 500, DateTimeKind.Unspecified);

        var stored = WithLocalTimeZone(FakeZone, () => AttributeValues.FormatStoredValue(value));

        Assert.Equal("2026-03-31T23:30:00.5000000", stored);
    }

    [Fact]
    public void CompareRecords_UnspecifiedDates_AreNotShiftedWhenLocalZoneIsNotUtc()
    {
        var id = Guid.NewGuid();
        var source = CreateRecord(new DateTime(2026, 3, 31, 23, 30, 0, DateTimeKind.Unspecified), id);
        var target = CreateRecord(new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Unspecified), id);

        var result = WithLocalTimeZone(FakeZone, () => new RecordComparer().CompareRecords(
            "rob_setting",
            new EntityCollection { Entities = { source } },
            new EntityCollection { Entities = { target } },
            new HashSet<string>(),
            primaryIdField: "rob_settingid"));

        var field = Assert.Single(Assert.Single(result.Differences).FieldDifferences);
        Assert.Equal("2026-03-31T23:30:00", field.SourceValue);
        Assert.Equal("2026-04-01T00:00:00", field.TargetValue);
    }

    private static Entity CreateRecord(DateTime value, Guid? id = null)
    {
        var entity = new Entity("rob_setting", id ?? Guid.NewGuid());
        entity[Column] = value;
        return entity;
    }

    /// <summary>
    /// Runs <paramref name="action"/> with <see cref="TimeZoneInfo.Local"/> replaced, then restores
    /// the machine's zone. Fails rather than silently passing if the runtime's internals change.
    /// </summary>
    private static T WithLocalTimeZone<T>(TimeZoneInfo zone, Func<T> action)
    {
        var cachedDataField = typeof(TimeZoneInfo).GetField("s_cachedData", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(cachedDataField);
        var cachedData = cachedDataField!.GetValue(null);
        Assert.NotNull(cachedData);
        var localField = cachedData!.GetType().GetField("_localTimeZone", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(localField);

        localField!.SetValue(cachedData, zone);
        try
        {
            Assert.Equal(zone.Id, TimeZoneInfo.Local.Id);
            return action();
        }
        finally
        {
            TimeZoneInfo.ClearCachedData();
        }
    }
}
