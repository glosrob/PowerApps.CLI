using PowerApps.CLI.Infrastructure;
using PowerApps.CLI.Services;
using Xunit;

namespace PowerApps.CLI.Tests.Services;

public class SchemaExtractorTests
{
    [Theory]
    [InlineData(null, 0)]
    [InlineData("", 0)]
    [InlineData("  ", 0)]
    [InlineData("RobSolution1", 1)]
    [InlineData("RobSolution1,RobSolution2", 2)]
    [InlineData("RobSolution1, RobSolution2, RobSolution3", 3)]
    [InlineData("  RobSolution1  ,  RobSolution2  ", 2)]
    [InlineData("Sol1,,Sol2", 2)] // Empty entries should be filtered
    public void ParseSolutionNames_ShouldHandleVariousInputs(string? input, int expectedCount)
    {
        // Arrange
        var extractorType = typeof(SchemaExtractor);
        var parseMethod = extractorType.GetMethod("ParseSolutionNames", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var mockMapper = new Moq.Mock<IMetadataMapper>();
        var mockDataverseClient = new Moq.Mock<IDataverseClient>();
        var extractor = new SchemaExtractor(mockMapper.Object, mockDataverseClient.Object);

        // Act
        var result = (List<string>?)parseMethod?.Invoke(extractor, new object?[] { input });

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedCount, result.Count);
    }

    [Fact]
    public void ParseSolutionNames_ShouldTrimWhitespace()
    {
        // Arrange
        var extractorType = typeof(SchemaExtractor);
        var parseMethod = extractorType.GetMethod("ParseSolutionNames", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var mockMapper = new Moq.Mock<IMetadataMapper>();
        var mockDataverseClient = new Moq.Mock<IDataverseClient>();
        var extractor = new SchemaExtractor(mockMapper.Object, mockDataverseClient.Object);

        // Act
        var result = (List<string>?)parseMethod?.Invoke(extractor, new object?[] { "  Sol1  ,  Sol2  " });

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Sol1", result);
        Assert.Contains("Sol2", result);
        Assert.DoesNotContain("  Sol1  ", result);
    }

    [Fact]
    public void Constructor_ShouldAcceptDependencies()
    {
        // Arrange
        var mockMapper = new Moq.Mock<IMetadataMapper>();
        var mockDataverseClient = new Moq.Mock<IDataverseClient>();

        // Act
        var extractor = new SchemaExtractor(mockMapper.Object, mockDataverseClient.Object);

        // Assert
        Assert.NotNull(extractor);
    }
}
