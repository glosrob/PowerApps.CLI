using Moq;
using PowerApps.CLI.Commands;
using PowerApps.CLI.Infrastructure;
using PowerApps.CLI.Services;
using Xunit;

namespace PowerApps.CLI.Tests.Commands;

public class SchemaCommandTests
{
    private readonly Mock<IConsoleLogger> _mockLogger;
    private readonly Mock<ISchemaService> _mockSchemaService;
    private readonly SchemaCommand _command;

    public SchemaCommandTests()
    {
        _mockLogger = new Mock<IConsoleLogger>();
        _mockSchemaService = new Mock<ISchemaService>();
        _command = new SchemaCommand(_mockLogger.Object, _mockSchemaService.Object);
    }

    [Fact]
    public async Task ExecuteAsync_WithNoUrlOrConnectionString_Returns1()
    {
        var result = await _command.ExecuteAsync(null, null, "output.json", "json", null, null, null);

        Assert.Equal(1, result);
        _mockLogger.Verify(l => l.LogError(It.Is<string>(s => s.Contains("--url or --connection-string"))), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithUrl_CallsExportSchemaAsync()
    {
        var result = await _command.ExecuteAsync(
            "https://test.crm.dynamics.com", null,
            "output.json", "json", "MySolution", "rob_", "excludeMe");

        Assert.Equal(0, result);
        _mockSchemaService.Verify(s => s.ExportSchemaAsync(
            "output.json", "json", "MySolution", "rob_", "excludeMe"), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithConnectionString_CallsExportSchemaAsync()
    {
        var result = await _command.ExecuteAsync(
            null, "AuthType=ClientSecret;Url=https://test.crm.dynamics.com",
            "output.xlsx", "xlsx", null, null, null);

        Assert.Equal(0, result);
        _mockSchemaService.Verify(s => s.ExportSchemaAsync(
            "output.xlsx", "xlsx", null, null, null), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenServiceThrows_Returns1()
    {
        _mockSchemaService
            .Setup(s => s.ExportSchemaAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ThrowsAsync(new Exception("Connection failed"));

        var result = await _command.ExecuteAsync(
            "https://test.crm.dynamics.com", null,
            "output.json", "json", null, null, null);

        Assert.Equal(1, result);
        _mockLogger.Verify(l => l.LogError(It.Is<string>(s => s.Contains("Connection failed"))), Times.Once);
    }

    [Fact]
    public void CreateCliCommand_ReturnsValidCommand()
    {
        var command = SchemaCommand.CreateCliCommand();

        Assert.NotNull(command);
        Assert.Equal("schema-export", command.Name);
    }
}
