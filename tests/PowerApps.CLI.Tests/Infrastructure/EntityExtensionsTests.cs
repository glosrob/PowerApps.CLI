using Microsoft.Xrm.Sdk;
using PowerApps.CLI.Infrastructure;
using Xunit;

namespace PowerApps.CLI.Tests.Infrastructure;

public class EntityExtensionsTests
{
    #region GetFormattedValue Tests

    [Fact]
    public void GetFormattedValue_WithFormattedValue_ReturnsFormattedValue()
    {
        // Arrange
        var entity = new Entity("account", Guid.NewGuid());
        entity.Attributes["statuscode"] = new OptionSetValue(1);
        entity.FormattedValues["statuscode"] = "Active";

        // Act
        var result = entity.GetFormattedValue("statuscode");

        // Assert
        Assert.Equal("Active", result);
    }

    [Fact]
    public void GetFormattedValue_WithoutFormattedValue_ReturnsRawValue()
    {
        // Arrange
        var entity = new Entity("account", Guid.NewGuid());
        entity.Attributes["name"] = "Test Account";

        // Act
        var result = entity.GetFormattedValue("name");

        // Assert
        Assert.Equal("Test Account", result);
    }

    [Fact]
    public void GetFormattedValue_WithEntityReference_ReturnsName()
    {
        // Arrange
        var entity = new Entity("account", Guid.NewGuid());
        var ownerId = Guid.NewGuid();
        entity.Attributes["ownerid"] = new EntityReference("systemuser", ownerId)
        {
            Name = "John Doe"
        };

        // Act
        var result = entity.GetFormattedValue("ownerid");

        // Assert
        Assert.Equal("John Doe", result);
    }

    [Fact]
    public void GetFormattedValue_WithEntityReferenceNoName_ReturnsId()
    {
        // Arrange
        var entity = new Entity("account", Guid.NewGuid());
        var ownerId = Guid.NewGuid();
        entity.Attributes["ownerid"] = new EntityReference("systemuser", ownerId);

        // Act
        var result = entity.GetFormattedValue("ownerid");

        // Assert
        Assert.Equal(ownerId.ToString(), result);
    }

    [Fact]
    public void GetFormattedValue_WithOptionSetValue_ReturnsNumericValue()
    {
        // Arrange
        var entity = new Entity("account", Guid.NewGuid());
        entity.Attributes["statuscode"] = new OptionSetValue(1);

        // Act
        var result = entity.GetFormattedValue("statuscode");

        // Assert
        Assert.Equal("1", result);
    }

    [Fact]
    public void GetFormattedValue_WithMoney_ReturnsFormattedDecimal()
    {
        // Arrange
        var entity = new Entity("account", Guid.NewGuid());
        entity.Attributes["revenue"] = new Money(1234.56m);

        // Act
        var result = entity.GetFormattedValue("revenue");

        // Assert
        Assert.Equal("1234.56", result);
    }

    [Fact]
    public void GetFormattedValue_WithNullValue_ReturnsNull()
    {
        // Arrange
        var entity = new Entity("account", Guid.NewGuid());
        entity.Attributes["description"] = null;

        // Act
        var result = entity.GetFormattedValue("description");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetFormattedValue_WithMissingAttribute_ReturnsNull()
    {
        // Arrange
        var entity = new Entity("account", Guid.NewGuid());

        // Act
        var result = entity.GetFormattedValue("nonexistent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetFormattedValue_WithNullEntity_ThrowsArgumentNullException()
    {
        // Arrange
        Entity? entity = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => entity!.GetFormattedValue("test"));
    }

    #endregion

    #region GetRecordName Tests

    [Fact]
    public void GetRecordName_WithPrimaryNameField_UsesSpecifiedField()
    {
        // Arrange
        var entity = new Entity("rob_category", Guid.NewGuid());
        entity.Attributes["rob_name"] = "Test Category";
        entity.Attributes["name"] = "Wrong Name";

        // Act
        var result = entity.GetRecordName("rob_name");

        // Assert
        Assert.Equal("Test Category", result);
    }

    [Fact]
    public void GetRecordName_WithNameAttribute_ReturnsName()
    {
        // Arrange
        var entity = new Entity("account", Guid.NewGuid());
        entity.Attributes["name"] = "Test Account";

        // Act
        var result = entity.GetRecordName();

        // Assert
        Assert.Equal("Test Account", result);
    }

    [Fact]
    public void GetRecordName_WithEntityNameAttribute_ReturnsEntityName()
    {
        // Arrange
        var entity = new Entity("contact", Guid.NewGuid());
        entity.Attributes["contactname"] = "John Smith";

        // Act
        var result = entity.GetRecordName();

        // Assert
        Assert.Equal("John Smith", result);
    }

    [Fact]
    public void GetRecordName_WithFullNameAttribute_ReturnsFullName()
    {
        // Arrange
        var entity = new Entity("contact", Guid.NewGuid());
        entity.Attributes["fullname"] = "Jane Doe";

        // Act
        var result = entity.GetRecordName();

        // Assert
        Assert.Equal("Jane Doe", result);
    }

    [Fact]
    public void GetRecordName_WithNoNameAttributes_ReturnsId()
    {
        // Arrange
        var id = Guid.NewGuid();
        var entity = new Entity("account", id);

        // Act
        var result = entity.GetRecordName();

        // Assert
        Assert.Equal(id.ToString(), result);
    }

    [Fact]
    public void GetRecordName_WithNullPrimaryNameField_FallsBackToCommonAttributes()
    {
        // Arrange
        var entity = new Entity("account", Guid.NewGuid());
        entity.Attributes["name"] = "Fallback Name";

        // Act
        var result = entity.GetRecordName(null);

        // Assert
        Assert.Equal("Fallback Name", result);
    }

    [Fact]
    public void GetRecordName_WithInvalidPrimaryNameField_FallsBackToCommonAttributes()
    {
        // Arrange
        var entity = new Entity("account", Guid.NewGuid());
        entity.Attributes["name"] = "Fallback Name";

        // Act
        var result = entity.GetRecordName("nonexistent");

        // Assert
        Assert.Equal("Fallback Name", result);
    }

    [Fact]
    public void GetRecordName_WithNullEntity_ThrowsArgumentNullException()
    {
        // Arrange
        Entity? entity = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => entity!.GetRecordName());
    }

    #endregion
}
