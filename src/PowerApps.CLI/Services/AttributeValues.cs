using System.Globalization;
using Microsoft.Xrm.Sdk;
using PowerApps.CLI.Infrastructure;

namespace PowerApps.CLI.Services;

/// <summary>
/// Compares and displays Dataverse column values. Comparison works on stored values, so the
/// result does not depend on the settings (date format, time zone, language) of the user who
/// queried each environment. Shared by <c>refdata-compare</c> and <c>refdata-migrate</c> so the
/// two commands agree on what counts as a change.
/// </summary>
public static class AttributeValues
{
    private const string UtcDateFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";
    private const string UnspecifiedDateFormat = "yyyy-MM-dd'T'HH:mm:ss";

    /// <summary>
    /// Returns true when two stored column values are the same.
    /// Lookups compare by record ID, Choices, Money and managed properties by value, and
    /// multi-select Choices by their set of option values (order is ignored).
    /// </summary>
    public static bool AreEqual(object? source, object? target)
    {
        if (source is null && target is null)
        {
            return true;
        }

        if (source is null || target is null)
        {
            return false;
        }

        return (source, target) switch
        {
            (EntityReference s, EntityReference t) => s.Id == t.Id,
            (OptionSetValue s, OptionSetValue t) => s.Value == t.Value,
            (OptionSetValueCollection s, OptionSetValueCollection t) => ChoiceValues(s).SetEquals(ChoiceValues(t)),
            (Money s, Money t) => s.Value == t.Value,
            (BooleanManagedProperty s, BooleanManagedProperty t) => s.Value == t.Value,
            _ => source.Equals(target)
        };
    }

    /// <summary>
    /// Returns the text to show for a column in a report. Dates are shown in ISO 8601, which does
    /// not depend on user settings: with a <c>Z</c> for UTC values, and without one for
    /// Date Only / Time-Zone Independent values (<see cref="DateTimeKind.Unspecified"/>), which are
    /// never shifted. Other types use the formatted value Dataverse returned.
    /// </summary>
    /// <returns>The display text, or null if the column is missing or has no value.</returns>
    public static string? FormatForDisplay(Entity entity, string attributeName)
    {
        if (entity == null)
        {
            throw new ArgumentNullException(nameof(entity));
        }

        if (!entity.Attributes.TryGetValue(attributeName, out var value) || value is null)
        {
            return null;
        }

        if (value is DateTime dateTime)
        {
            return FormatDate(dateTime);
        }

        return entity.GetFormattedValue(attributeName);
    }

    /// <summary>
    /// Returns the stored value as culture-invariant text, for telling two values apart when their
    /// display text is identical. Lookups give their record ID.
    /// </summary>
    public static string? FormatStoredValue(object? value)
    {
        return value switch
        {
            null => null,
            EntityReference entityRef => entityRef.Id.ToString(),
            OptionSetValue option => option.Value.ToString(CultureInfo.InvariantCulture),
            OptionSetValueCollection options => string.Join(",",
                options.Select(o => o?.Value.ToString(CultureInfo.InvariantCulture) ?? "null")),
            Money money => money.Value.ToString(CultureInfo.InvariantCulture),
            BooleanManagedProperty managed => managed.Value.ToString(CultureInfo.InvariantCulture),
            DateTime dateTime => dateTime.ToString("o", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        };
    }

    private static string FormatDate(DateTime value)
    {
        switch (value.Kind)
        {
            case DateTimeKind.Utc:
                return value.ToString(UtcDateFormat, CultureInfo.InvariantCulture);
            case DateTimeKind.Local:
                return value.ToUniversalTime().ToString(UtcDateFormat, CultureInfo.InvariantCulture);
            default:
                // Date Only / Time-Zone Independent: never convert, or the value would shift by
                // the offset of whichever machine runs the CLI.
                return value.ToString(UnspecifiedDateFormat, CultureInfo.InvariantCulture);
        }
    }

    private static HashSet<int?> ChoiceValues(OptionSetValueCollection options)
    {
        return options.Select(o => o?.Value).ToHashSet();
    }
}
