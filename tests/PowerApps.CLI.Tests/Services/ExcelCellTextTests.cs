using PowerApps.CLI.Services;
using Xunit;

namespace PowerApps.CLI.Tests.Services;

public class ExcelCellTextTests
{
    private const int Max = ExcelCellText.MaxCellLength;

    #region FitList

    [Fact]
    public void FitList_UnderLimit_ShouldReturnJoinedValueUnchanged()
    {
        // Act
        var result = ExcelCellText.FitList(new[] { "account", "contact" }, out var dropped);

        // Assert
        Assert.Equal("account, contact", result);
        Assert.Equal(0, dropped);
    }

    [Fact]
    public void FitList_Empty_ShouldReturnEmptyString()
    {
        // Act
        var result = ExcelCellText.FitList(Array.Empty<string>(), out var dropped);

        // Assert
        Assert.Equal(string.Empty, result);
        Assert.Equal(0, dropped);
    }

    [Fact]
    public void FitList_ExactlyAtLimit_ShouldReturnJoinedValueUnchanged()
    {
        // Arrange — two items plus ", " totalling exactly the limit
        var first = new string('a', 100);
        var second = new string('b', Max - 100 - 2);

        // Act
        var result = ExcelCellText.FitList(new[] { first, second }, out var dropped);

        // Assert
        Assert.Equal(Max, result.Length);
        Assert.Equal($"{first}, {second}", result);
        Assert.Equal(0, dropped);
    }

    [Fact]
    public void FitList_OneCharOverLimit_ShouldDropLastItemAndAddMarker()
    {
        // Arrange
        var first = new string('a', 100);
        var second = new string('b', Max - 100 - 2 + 1);

        // Act
        var result = ExcelCellText.FitList(new[] { first, second }, out var dropped);

        // Assert
        Assert.Equal($"{first} … (+1 more)", result);
        Assert.Equal(1, dropped);
    }

    [Fact]
    public void FitList_ManyItems_ShouldKeepOnlyWholeItemsWithinLimit()
    {
        // Arrange
        var items = Enumerable.Range(1, 2000).Select(i => $"rob_longtablename{i:D5}").ToArray();

        // Act
        var result = ExcelCellText.FitList(items, out var dropped);

        // Assert
        Assert.True(result.Length <= Max);
        Assert.True(dropped > 0);
        Assert.EndsWith($" … (+{dropped.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)} more)", result);

        var kept = result[..result.LastIndexOf(" … ", StringComparison.Ordinal)].Split(", ");
        Assert.Equal(items.Take(items.Length - dropped), kept);
    }

    [Fact]
    public void FitList_ManyItems_ShouldKeepAsManyItemsAsPossible()
    {
        // Arrange
        var items = Enumerable.Range(1, 2000).Select(i => $"rob_longtablename{i:D5}").ToArray();

        // Act
        var result = ExcelCellText.FitList(items, out var dropped);

        // Assert — adding one more item (and its separator) would go over the limit
        var nextItem = items[items.Length - dropped];
        Assert.True(result.Length + ", ".Length + nextItem.Length > Max);
    }

    [Fact]
    public void FitList_DroppedCountOverOneThousand_ShouldUseInvariantThousandsSeparator()
    {
        // Arrange — one huge item that leaves room only for the marker, then 1,500 that cannot fit
        var items = new[] { new string('a', Max - 20) }
            .Concat(Enumerable.Range(1, 1500).Select(i => $"item{i}"))
            .ToArray();

        // Act
        var result = ExcelCellText.FitList(items, out var dropped);

        // Assert
        Assert.Equal(1500, dropped);
        Assert.EndsWith(" … (+1,500 more)", result);
    }

    [Fact]
    public void FitList_FirstItemTooLong_ShouldReturnMarkerOnly()
    {
        // Arrange
        var items = new[] { new string('a', Max + 1), "contact" };

        // Act
        var result = ExcelCellText.FitList(items, out var dropped);

        // Assert
        Assert.Equal("… (+2 more)", result);
        Assert.Equal(2, dropped);
    }

    [Fact]
    public void FitList_Null_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() => ExcelCellText.FitList(null!, out _));
    }

    #endregion

    #region FitText

    [Fact]
    public void FitText_UnderLimit_ShouldReturnValueUnchanged()
    {
        // Act
        var result = ExcelCellText.FitText("A short description", out var dropped);

        // Assert
        Assert.Equal("A short description", result);
        Assert.Equal(0, dropped);
    }

    [Fact]
    public void FitText_Empty_ShouldReturnEmpty()
    {
        // Act
        var result = ExcelCellText.FitText(string.Empty, out var dropped);

        // Assert
        Assert.Equal(string.Empty, result);
        Assert.Equal(0, dropped);
    }

    [Fact]
    public void FitText_ExactlyAtLimit_ShouldReturnValueUnchanged()
    {
        // Arrange
        var value = new string('x', Max);

        // Act
        var result = ExcelCellText.FitText(value, out var dropped);

        // Assert
        Assert.Same(value, result);
        Assert.Equal(0, dropped);
    }

    [Fact]
    public void FitText_OneCharOverLimit_ShouldCutShortWithCharCountMarker()
    {
        // Arrange
        var value = new string('x', Max + 1);

        // Act
        var result = ExcelCellText.FitText(value, out var dropped);

        // Assert
        var marker = $" … (+{dropped} chars)";
        Assert.Equal(Max, result.Length);
        Assert.EndsWith(marker, result);
        Assert.Equal(value.Length, result.Length - marker.Length + dropped);
    }

    [Fact]
    public void FitText_CutAtSurrogatePair_ShouldNotSplitThePair()
    {
        // Arrange — place an emoji (two UTF-16 chars) across every possible cut point near the end
        for (var offset = Max - 40; offset < Max; offset++)
        {
            var value = new string('x', offset) + "😀" + new string('y', 100);

            // Act
            var result = ExcelCellText.FitText(value, out var dropped);

            // Assert
            Assert.True(result.Length <= Max);
            var kept = result[..(value.Length - dropped)];
            Assert.False(char.IsHighSurrogate(kept[^1]), $"Split surrogate pair at offset {offset}");
            Assert.Equal(value[..kept.Length], kept);
        }
    }

    [Fact]
    public void FitText_Null_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() => ExcelCellText.FitText(null!, out _));
    }

    #endregion
}
