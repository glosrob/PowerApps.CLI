using Moq;
using PowerApps.CLI.Commands;
using PowerApps.CLI.Infrastructure;
using PowerApps.CLI.Models;
using PowerApps.CLI.Services;
using Xunit;

namespace PowerApps.CLI.Tests.Commands;

public class RefDataMigrateCommandTests
{
    private readonly Mock<IConsoleLogger> _mockLogger;
    private readonly Mock<IDataverseClient> _mockSourceClient;
    private readonly Mock<IDataverseClient> _mockTargetClient;
    private readonly Mock<IRefDataMigrator> _mockMigrator;
    private readonly Mock<IMigrationReporter> _mockReporter;
    private readonly Mock<IFileWriter> _mockFileWriter;
    private readonly RefDataMigrateCommand _command;

    public RefDataMigrateCommandTests()
    {
        _mockLogger = new Mock<IConsoleLogger>();
        _mockSourceClient = new Mock<IDataverseClient>();
        _mockTargetClient = new Mock<IDataverseClient>();
        _mockMigrator = new Mock<IRefDataMigrator>();
        _mockReporter = new Mock<IMigrationReporter>();
        _mockFileWriter = new Mock<IFileWriter>();

        _mockSourceClient.Setup(c => c.GetEnvironmentUrl()).Returns("https://source.crm.dynamics.com");
        _mockTargetClient.Setup(c => c.GetEnvironmentUrl()).Returns("https://target.crm.dynamics.com");

        _command = new RefDataMigrateCommand(
            _mockLogger.Object,
            _mockSourceClient.Object,
            _mockTargetClient.Object,
            _mockMigrator.Object,
            _mockReporter.Object,
            _mockFileWriter.Object);
    }

    private void SetupConfigFile(string configPath, RefDataMigrateConfig config)
    {
        _mockFileWriter.Setup(f => f.FileExists(configPath)).Returns(true);
        var json = System.Text.Json.JsonSerializer.Serialize(config);
        _mockFileWriter.Setup(f => f.ReadTextAsync(configPath)).ReturnsAsync(json);
    }

    [Fact]
    public async Task ExecuteAsync_LoadsConfigAndCallsMigrator()
    {
        // Arrange
        var config = new RefDataMigrateConfig
        {
            Tables = new List<MigrateTableConfig>
            {
                new() { LogicalName = "account" },
                new() { LogicalName = "contact" }
            }
        };
        SetupConfigFile("config.json", config);

        _mockMigrator.Setup(m => m.MigrateAsync(It.IsAny<RefDataMigrateConfig>(), false, false))
            .ReturnsAsync(new MigrationSummary());

        // Act
        var result = await _command.ExecuteAsync("config.json", "report.xlsx", false);

        // Assert
        Assert.Equal(0, result);
        _mockMigrator.Verify(m => m.MigrateAsync(
            It.Is<RefDataMigrateConfig>(c => c.Tables.Count == 2),
            false, false), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_GeneratesReport()
    {
        // Arrange
        var config = new RefDataMigrateConfig
        {
            Tables = new List<MigrateTableConfig> { new() { LogicalName = "account" } }
        };
        SetupConfigFile("config.json", config);

        var summary = new MigrationSummary();
        _mockMigrator.Setup(m => m.MigrateAsync(It.IsAny<RefDataMigrateConfig>(), false, false))
            .ReturnsAsync(summary);

        // Act
        await _command.ExecuteAsync("config.json", "output.xlsx", false);

        // Assert
        _mockReporter.Verify(r => r.GenerateReportAsync(summary, "output.xlsx"), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ForwardsDryRunFlag()
    {
        // Arrange
        var config = new RefDataMigrateConfig
        {
            Tables = new List<MigrateTableConfig> { new() { LogicalName = "account" } }
        };
        SetupConfigFile("config.json", config);

        _mockMigrator.Setup(m => m.MigrateAsync(It.IsAny<RefDataMigrateConfig>(), true, false))
            .ReturnsAsync(new MigrationSummary());

        // Act
        await _command.ExecuteAsync("config.json", "report.xlsx", true);

        // Assert
        _mockMigrator.Verify(m => m.MigrateAsync(It.IsAny<RefDataMigrateConfig>(), true, false), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyTables_Returns1()
    {
        // Arrange
        var config = new RefDataMigrateConfig { Tables = new List<MigrateTableConfig>() };
        SetupConfigFile("config.json", config);

        // Act
        var result = await _command.ExecuteAsync("config.json", "report.xlsx", false);

        // Assert
        Assert.Equal(1, result);
    }

    [Fact]
    public async Task ExecuteAsync_WhenConfigNotFound_Returns1()
    {
        // Arrange
        _mockFileWriter.Setup(f => f.FileExists("missing.json")).Returns(false);

        // Act
        var result = await _command.ExecuteAsync("missing.json", "report.xlsx", false);

        // Assert
        Assert.Equal(1, result);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMigrationHasErrors_Returns1()
    {
        // Arrange
        var config = new RefDataMigrateConfig
        {
            Tables = new List<MigrateTableConfig> { new() { LogicalName = "account" } }
        };
        SetupConfigFile("config.json", config);

        var summary = new MigrationSummary
        {
            TableResults = new List<TableMigrationResult>
            {
                new()
                {
                    TableName = "account",
                    Errors = new List<RecordError>
                    {
                        new() { ErrorMessage = "Test error" }
                    }
                }
            }
        };
        _mockMigrator.Setup(m => m.MigrateAsync(It.IsAny<RefDataMigrateConfig>(), false, false))
            .ReturnsAsync(summary);

        // Act
        var result = await _command.ExecuteAsync("config.json", "report.xlsx", false);

        // Assert
        Assert.Equal(1, result);
    }

    [Fact]
    public void CreateCliCommand_ReturnsValidCommand()
    {
        var command = RefDataMigrateCommand.CreateCliCommand();

        Assert.NotNull(command);
        Assert.Equal("refdata-migrate", command.Name);
    }
}
