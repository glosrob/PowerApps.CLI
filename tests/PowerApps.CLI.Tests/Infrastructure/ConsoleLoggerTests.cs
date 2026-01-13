namespace PowerApps.CLI.Tests.Infrastructure;

using PowerApps.CLI.Infrastructure;

public class ConsoleLoggerTests : IDisposable
{
    private readonly ConsoleLogger _logger;
    private readonly StringWriter _output;
    private readonly TextWriter _originalOutput;

    public ConsoleLoggerTests()
    {
        _logger = new ConsoleLogger();
        _output = new StringWriter();
        _originalOutput = Console.Out;
        Console.SetOut(_output);
    }

    public void Dispose()
    {
        Console.SetOut(_originalOutput);
        _output.Dispose();
    }

    [Fact]
    public void LogInfo_WritesToConsole()
    {
        // Arrange
        const string message = "Test info message";

        // Act
        _logger.LogInfo(message);

        // Assert
        var output = _output.ToString();
        Assert.Contains(message, output);
    }

    [Fact]
    public void LogWarning_WritesToConsole()
    {
        // Arrange
        const string message = "Test warning message";

        // Act
        _logger.LogWarning(message);

        // Assert
        var output = _output.ToString();
        Assert.Contains(message, output);
    }

    [Fact]
    public void LogError_WritesToConsole()
    {
        // Arrange
        const string message = "Test error message";

        // Act
        _logger.LogError(message);

        // Assert
        var output = _output.ToString();
        Assert.Contains(message, output);
    }

    [Fact]
    public void LogSuccess_WritesToConsole()
    {
        // Arrange
        const string message = "Test success message";

        // Act
        _logger.LogSuccess(message);

        // Assert
        var output = _output.ToString();
        Assert.Contains(message, output);
    }

    [Fact]
    public void LogVerbose_WhenEnabled_WritesToConsole()
    {
        // Arrange
        const string message = "Test verbose message";
        _logger.IsVerboseEnabled = true;

        // Act
        _logger.LogVerbose(message);

        // Assert
        var output = _output.ToString();
        Assert.Contains(message, output);
    }

    [Fact]
    public void LogVerbose_WhenDisabled_DoesNotWriteToConsole()
    {
        // Arrange
        const string message = "Test verbose message";
        _logger.IsVerboseEnabled = false;

        // Act
        _logger.LogVerbose(message);

        // Assert
        var output = _output.ToString();
        Assert.DoesNotContain(message, output);
    }

    [Fact]
    public void IsVerboseEnabled_DefaultsToFalse()
    {
        // Arrange
        var logger = new ConsoleLogger();

        // Assert
        Assert.False(logger.IsVerboseEnabled);
    }

    [Fact]
    public void IsVerboseEnabled_CanBeSet()
    {
        // Arrange
        var logger = new ConsoleLogger();

        // Act
        logger.IsVerboseEnabled = true;

        // Assert
        Assert.True(logger.IsVerboseEnabled);
    }

    [Fact]
    public void LogInfo_WithEmptyString_WritesToConsole()
    {
        // Act
        _logger.LogInfo(string.Empty);

        // Assert
        var output = _output.ToString();
        Assert.NotNull(output);
    }

    [Fact]
    public void LogError_WithMultilineMessage_WritesToConsole()
    {
        // Arrange
        const string message = "Line 1\nLine 2\nLine 3";

        // Act
        _logger.LogError(message);

        // Assert
        var output = _output.ToString();
        Assert.Contains("Line 1", output);
        Assert.Contains("Line 2", output);
        Assert.Contains("Line 3", output);
    }
}
