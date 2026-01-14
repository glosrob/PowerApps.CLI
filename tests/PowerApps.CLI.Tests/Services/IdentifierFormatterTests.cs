using PowerApps.CLI.Services;
using Xunit;

namespace PowerApps.CLI.Tests.Services;

public class IdentifierFormatterTests
{
    [Fact]
    public void ToIdentifier_SimpleString_ReturnsCapitalised()
    {
        // Arrange
        var formatter = new IdentifierFormatter();

        // Act
        var result = formatter.ToIdentifier("contact");

        // Assert
        Assert.Equal("Contact", result);
    }

    [Fact]
    public void ToIdentifier_WithUnderscore_ConvertsToPascalCase()
    {
        // Arrange
        var formatter = new IdentifierFormatter();

        // Act
        var result = formatter.ToIdentifier("new_customer");

        // Assert
        Assert.Equal("NewCustomer", result);
    }

    [Fact]
    public void ToIdentifier_WithSpaces_ConvertsToPascalCase()
    {
        // Arrange
        var formatter = new IdentifierFormatter();

        // Act
        var result = formatter.ToIdentifier("primary contact");

        // Assert
        Assert.Equal("PrimaryContact", result);
    }

    [Fact]
    public void ToIdentifier_WithLeadingDigit_PrependsUnderscore()
    {
        // Arrange
        var formatter = new IdentifierFormatter();

        // Act
        var result = formatter.ToIdentifier("123_field");

        // Assert
        Assert.StartsWith("_", result);
        Assert.Equal("_123Field", result);
    }

    [Fact]
    public void ToIdentifier_ReservedKeyword_AppendsUnderscore()
    {
        // Arrange
        var formatter = new IdentifierFormatter();

        // Act
        var result = formatter.ToIdentifier("class");

        // Assert
        Assert.Equal("Class_", result);
    }

    [Fact]
    public void ToIdentifier_MultipleReservedKeywords_AppendsUnderscore()
    {
        // Arrange
        var formatter = new IdentifierFormatter();

        // Act
        var results = new[]
        {
            formatter.ToIdentifier("namespace"),
            formatter.ToIdentifier("return"),
            formatter.ToIdentifier("void"),
            formatter.ToIdentifier("string")
        };

        // Assert
        Assert.Equal("Namespace_", results[0]);
        Assert.Equal("Return_", results[1]);
        Assert.Equal("Void_", results[2]);
        Assert.Equal("String_", results[3]);
    }

    [Fact]
    public void ToIdentifier_WithSpecialCharacters_RemovesInvalidChars()
    {
        // Arrange
        var formatter = new IdentifierFormatter();

        // Act
        var result = formatter.ToIdentifier("customer@email.com");

        // Assert
        Assert.Equal("CustomerEmailCom", result);
    }

    [Fact]
    public void ToIdentifier_WithParentheses_RemovesAndConvertsToPascalCase()
    {
        // Arrange
        var formatter = new IdentifierFormatter();

        // Act
        var result = formatter.ToIdentifier("field(name)");

        // Assert
        Assert.Equal("FieldName", result);
    }

    [Fact]
    public void ToIdentifier_EmptyString_ReturnsUnknown()
    {
        // Arrange
        var formatter = new IdentifierFormatter();

        // Act
        var result = formatter.ToIdentifier("");

        // Assert
        Assert.Equal("Unknown", result);
    }

    [Fact]
    public void ToIdentifier_NullString_ReturnsUnknown()
    {
        // Arrange
        var formatter = new IdentifierFormatter();

        // Act
        var result = formatter.ToIdentifier(null!);

        // Assert
        Assert.Equal("Unknown", result);
    }

    [Fact]
    public void ToIdentifier_OnlySpecialCharacters_ReturnsUnknown()
    {
        // Arrange
        var formatter = new IdentifierFormatter();

        // Act
        var result = formatter.ToIdentifier("@#$%");

        // Assert
        Assert.Equal("Unknown", result);
    }

    [Fact]
    public void ToIdentifier_WithPascalCaseDisabled_KeepsOriginalCase()
    {
        // Arrange
        var formatter = new IdentifierFormatter(usePascalCase: false);

        // Act
        var result = formatter.ToIdentifier("new_customer");

        // Assert
        Assert.Equal("New_customer", result); // Capitalises first letter only
    }

    [Fact]
    public void ToPascalCase_SimpleString_ReturnsCapitalised()
    {
        // Arrange
        var formatter = new IdentifierFormatter();

        // Act
        var result = formatter.ToPascalCase("account");

        // Assert
        Assert.Equal("Account", result);
    }

    [Fact]
    public void ToPascalCase_WithUnderscores_ConvertsToPascalCase()
    {
        // Arrange
        var formatter = new IdentifierFormatter();

        // Act
        var result = formatter.ToPascalCase("customer_account");

        // Assert
        Assert.Equal("CustomerAccount", result);
    }

    [Fact]
    public void ToPascalCase_WithHyphens_ConvertsToPascalCase()
    {
        // Arrange
        var formatter = new IdentifierFormatter();

        // Act
        var result = formatter.ToPascalCase("user-profile");

        // Assert
        Assert.Equal("UserProfile", result);
    }

    [Fact]
    public void ToPascalCase_WithPluralisationTrue_AddsSuffix()
    {
        // Arrange
        var formatter = new IdentifierFormatter();

        // Act
        var result = formatter.ToPascalCase("contact", pluralise: true);

        // Assert
        Assert.Equal("Contacts", result);
    }

    [Fact]
    public void ToPascalCase_AlreadyPlural_DoesNotAddSuffix()
    {
        // Arrange
        var formatter = new IdentifierFormatter();

        // Act
        var result = formatter.ToPascalCase("contacts", pluralise: true);

        // Assert
        Assert.Equal("Contacts", result);
    }

    [Fact]
    public void ToPascalCase_EmptyString_ReturnsEmpty()
    {
        // Arrange
        var formatter = new IdentifierFormatter();

        // Act
        var result = formatter.ToPascalCase("");

        // Assert
        Assert.Equal("", result);
    }

    [Fact]
    public void MakeUnique_NonDuplicateIdentifier_ReturnsOriginal()
    {
        // Arrange
        var formatter = new IdentifierFormatter();
        var existingIdentifiers = new HashSet<string> { "Contact", "Account" };

        // Act
        var result = formatter.MakeUnique("Lead", existingIdentifiers);

        // Assert
        Assert.Equal("Lead", result);
    }

    [Fact]
    public void MakeUnique_DuplicateIdentifier_AppendsGuid()
    {
        // Arrange
        var formatter = new IdentifierFormatter();
        var existingIdentifiers = new HashSet<string> { "Contact", "Account" };

        // Act
        var result = formatter.MakeUnique("Contact", existingIdentifiers);

        // Assert
        Assert.NotEqual("Contact", result);
        Assert.StartsWith("Contact_", result);
    }

    [Fact]
    public void MakeUnique_WithCustomSuffix_AppendsCustomSuffix()
    {
        // Arrange
        var formatter = new IdentifierFormatter();
        var existingIdentifiers = new HashSet<string> { "Contact" };

        // Act
        var result = formatter.MakeUnique("Contact", existingIdentifiers, "V2");

        // Assert
        Assert.Equal("Contact_V2", result);
    }

    [Fact]
    public void MakeUnique_MultipleDuplicates_AppendsCounter()
    {
        // Arrange
        var formatter = new IdentifierFormatter();
        var existingIdentifiers = new HashSet<string> { "Contact", "Contact_abc123", "Contact_abc123_1" };

        // Act
        var result = formatter.MakeUnique("Contact", existingIdentifiers, "abc123");

        // Assert
        Assert.Equal("Contact_abc123_2", result);
    }
}
