using PowerApps.CLI.Infrastructure;
using Xunit;

namespace PowerApps.CLI.Tests.Infrastructure;

public class WorksheetNameAllocatorTests
{
    [Fact]
    public void Allocate_DistinctShortNames_ShouldReturnNamesUnchanged()
    {
        var allocator = new WorksheetNameAllocator();

        Assert.Equal("Account", allocator.Allocate("Account"));
        Assert.Equal("Contact", allocator.Allocate("Contact"));
    }

    [Fact]
    public void Allocate_NameOverLimit_ShouldTruncateTo31Characters()
    {
        var allocator = new WorksheetNameAllocator();

        var result = allocator.Allocate("anc_informationrequesttype_anc_first");

        Assert.Equal("anc_informationrequesttype_anc_", result);
        Assert.Equal(31, result.Length);
    }

    [Fact]
    public void Allocate_NamesSharing31CharacterPrefix_ShouldReturnDistinctNames()
    {
        var allocator = new WorksheetNameAllocator();

        var first = allocator.Allocate("anc_informationrequesttype_anc_first");
        var second = allocator.Allocate("anc_informationrequesttype_anc_second");

        Assert.Equal("anc_informationrequesttype_anc_", first);
        Assert.Equal("anc_informationrequesttype_an~2", second);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Allocate_ThreeCollidingNames_ShouldIncrementSuffix()
    {
        var allocator = new WorksheetNameAllocator();

        var first = allocator.Allocate("anc_informationrequesttype_anc_first");
        var second = allocator.Allocate("anc_informationrequesttype_anc_second");
        var third = allocator.Allocate("anc_informationrequesttype_anc_third");

        Assert.Equal("anc_informationrequesttype_an~3", third);
        Assert.Equal(3, new[] { first, second, third }.Distinct().Count());
    }

    [Fact]
    public void Allocate_CollidingNames_ShouldStayWithinLengthLimit()
    {
        var allocator = new WorksheetNameAllocator();

        var names = Enumerable.Range(1, 12)
            .Select(i => allocator.Allocate($"anc_informationrequesttype_anc_{i}"))
            .ToList();

        Assert.All(names, name => Assert.True(name.Length <= 31, $"'{name}' is {name.Length} characters"));
        Assert.Equal(names.Count, names.Distinct().Count());
    }

    [Fact]
    public void Allocate_ShortCollidingNames_ShouldAppendSuffixWithoutTruncating()
    {
        var allocator = new WorksheetNameAllocator();

        allocator.Allocate("Account");
        var second = allocator.Allocate("Account");

        Assert.Equal("Account~2", second);
    }

    [Fact]
    public void Allocate_NameDifferingOnlyByCase_ShouldBeTreatedAsCollision()
    {
        // Excel worksheet names are compared case-insensitively.
        var allocator = new WorksheetNameAllocator();

        allocator.Allocate("Account");
        var second = allocator.Allocate("ACCOUNT");

        Assert.Equal("ACCOUNT~2", second);
    }

    [Fact]
    public void Allocate_NameCollidingWithReservedName_ShouldBeDisambiguated()
    {
        var allocator = new WorksheetNameAllocator();
        allocator.Reserve("Summary");

        var result = allocator.Allocate("Summary");

        Assert.Equal("Summary~2", result);
    }

    [Fact]
    public void Allocate_IllegalCharacters_ShouldBeReplacedWithUnderscores()
    {
        var allocator = new WorksheetNameAllocator();

        var result = allocator.Allocate(@"Account\Contact/Lead?Case*Opp[1]");

        // 32 characters in, so the trailing ']' is truncated away.
        Assert.Equal("Account_Contact_Lead_Case_Opp_1", result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Allocate_NullOrBlankName_ShouldFallBackToDefault(string? name)
    {
        var allocator = new WorksheetNameAllocator();

        var result = allocator.Allocate(name);

        Assert.Equal("Sheet", result);
    }

    [Fact]
    public void Allocate_RepeatedBlankNames_ShouldStillBeUnique()
    {
        var allocator = new WorksheetNameAllocator();

        var first = allocator.Allocate(null);
        var second = allocator.Allocate(null);

        Assert.Equal("Sheet", first);
        Assert.Equal("Sheet~2", second);
    }

    [Fact]
    public void Reserve_NullOrBlankName_ShouldThrow()
    {
        var allocator = new WorksheetNameAllocator();

        Assert.Throws<ArgumentNullException>(() => allocator.Reserve(null!));
        Assert.Throws<ArgumentException>(() => allocator.Reserve("   "));
    }
}
