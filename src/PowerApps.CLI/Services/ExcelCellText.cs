using System.Globalization;

namespace PowerApps.CLI.Services;

/// <summary>
/// Fits text into a single Excel cell. Excel (and ClosedXML) rejects any cell value longer than
/// <see cref="MaxCellLength"/> characters, so long values are cut short and given a marker that
/// says how much was dropped.
/// </summary>
public static class ExcelCellText
{
    /// <summary>
    /// Excel's hard limit on the number of characters in one cell.
    /// </summary>
    public const int MaxCellLength = 32767;

    /// <summary>
    /// The separator placed between list items.
    /// </summary>
    public const string ListSeparator = ", ";

    /// <summary>
    /// Joins <paramref name="items"/> with <see cref="ListSeparator"/>. If the result is too long
    /// for a cell, keeps as many whole items as fit and adds an "… (+N more)" marker.
    /// </summary>
    /// <param name="items">The list items to join.</param>
    /// <param name="droppedItems">The number of items left out; 0 when everything fits.</param>
    /// <returns>A value no longer than <see cref="MaxCellLength"/>.</returns>
    public static string FitList(IReadOnlyList<string> items, out int droppedItems)
    {
        ArgumentNullException.ThrowIfNull(items);

        var full = string.Join(ListSeparator, items);
        if (full.Length <= MaxCellLength)
        {
            droppedItems = 0;
            return full;
        }

        // Find the largest number of leading items that fit alongside the marker. The marker's
        // length depends on how many items are dropped, so it is worked out for each candidate.
        var keep = 0;
        var joinedLength = 0;
        for (var candidate = 1; candidate <= items.Count; candidate++)
        {
            var itemLength = items[candidate - 1].Length;
            var candidateLength = candidate == 1 ? itemLength : joinedLength + ListSeparator.Length + itemLength;
            var markerLength = ListMarker(items.Count - candidate, hasPrefix: true).Length;
            if (candidateLength + markerLength > MaxCellLength)
            {
                break;
            }

            keep = candidate;
            joinedLength = candidateLength;
        }

        droppedItems = items.Count - keep;
        if (keep == 0)
        {
            return ListMarker(droppedItems, hasPrefix: false);
        }

        return string.Join(ListSeparator, items.Take(keep)) + ListMarker(droppedItems, hasPrefix: true);
    }

    /// <summary>
    /// Returns <paramref name="value"/> unchanged if it fits in a cell; otherwise cuts it short
    /// and adds an "… (+N chars)" marker. Never splits a surrogate pair.
    /// </summary>
    /// <param name="value">The text to fit.</param>
    /// <param name="droppedChars">The number of characters left out; 0 when everything fits.</param>
    /// <returns>A value no longer than <see cref="MaxCellLength"/>.</returns>
    public static string FitText(string value, out int droppedChars)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (value.Length <= MaxCellLength)
        {
            droppedChars = 0;
            return value;
        }

        // The marker's length depends on the dropped count, so step back until both fit and the
        // cut does not land between the two halves of a surrogate pair.
        var keep = MaxCellLength;
        while (keep > 0 &&
               (keep + TextMarker(value.Length - keep).Length > MaxCellLength ||
                char.IsHighSurrogate(value[keep - 1])))
        {
            keep--;
        }

        droppedChars = value.Length - keep;
        return value[..keep] + TextMarker(droppedChars);
    }

    private static string ListMarker(int dropped, bool hasPrefix) =>
        $"{(hasPrefix ? " " : string.Empty)}… (+{FormatCount(dropped)} more)";

    private static string TextMarker(int dropped) => $" … (+{FormatCount(dropped)} chars)";

    internal static string FormatCount(int count) => count.ToString("N0", CultureInfo.InvariantCulture);
}
