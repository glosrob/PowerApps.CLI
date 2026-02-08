using Microsoft.Xrm.Sdk;
using Moq;
using PowerApps.CLI.Commands;
using PowerApps.CLI.Infrastructure;
using PowerApps.CLI.Models;
using PowerApps.CLI.Services;
using Xunit;

namespace PowerApps.CLI.Tests.Commands;

public class RefDataCompareCommandTests
{
    private readonly Mock<IConsoleLogger> _mockLogger;
    private readonly Mock<IDataverseClient> _mockSourceClient;
    private readonly Mock<IDataverseClient> _mockTargetClient;
    private readonly Mock<IRecordComparer> _mockComparer;
    private readonly Mock<IComparisonReporter> _mockReporter;
    private readonly Mock<IFileWriter> _mockFileWriter;
    private readonly RefDataCompareCommand _command;

    public RefDataCompareCommandTests()
    {
        _mockLogger = new Mock<IConsoleLogger>();
        _mockSourceClient = new Mock<IDataverseClient>();
        _mockTargetClient = new Mock<IDataverseClient>();
        _mockComparer = new Mock<IRecordComparer>();
        _mockReporter = new Mock<IComparisonReporter>();
        _mockFileWriter = new Mock<IFileWriter>();

        _mockSourceClient.Setup(c => c.GetEnvironmentUrl()).Returns("https://source.crm.dynamics.com");
        _mockTargetClient.Setup(c => c.GetEnvironmentUrl()).Returns("https://target.crm.dynamics.com");

        _command = new RefDataCompareCommand(
            _mockLogger.Object,
            _mockSourceClient.Object,
            _mockTargetClient.Object,
            _mockComparer.Object,
            _mockReporter.Object,
            _mockFileWriter.Object);
    }

    private void SetupConfigFile(string configPath, RefDataCompareConfig config)
    {
        _mockFileWriter.Setup(f => f.FileExists(configPath)).Returns(true);
        var json = System.Text.Json.JsonSerializer.Serialize(config);
        _mockFileWriter.Setup(f => f.ReadTextAsync(configPath)).ReturnsAsync(json);
    }

    [Fact]
    public async Task ExecuteAsync_ComparesEachTableInConfig()
    {
        // Arrange
        var config = new RefDataCompareConfig
        {
            Tables = new List<RefDataTableConfig>
            {
                new() { LogicalName = "account" },
                new() { LogicalName = "contact" }
            }
        };
        SetupConfigFile("config.json", config);

        _mockSourceClient.Setup(c => c.RetrieveRecords(It.IsAny<string>(), It.IsAny<string?>()))
            .Returns(new EntityCollection());
        _mockTargetClient.Setup(c => c.RetrieveRecords(It.IsAny<string>(), It.IsAny<string?>()))
            .Returns(new EntityCollection());
        _mockComparer.Setup(c => c.CompareRecords(
                It.IsAny<string>(), It.IsAny<EntityCollection>(), It.IsAny<EntityCollection>(),
                It.IsAny<HashSet<string>>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(new TableComparisonResult());

        // Act
        await _command.ExecuteAsync("config.json", "report.xlsx");

        // Assert
        _mockSourceClient.Verify(c => c.RetrieveRecords("account", null), Times.Once);
        _mockSourceClient.Verify(c => c.RetrieveRecords("contact", null), Times.Once);
        _mockTargetClient.Verify(c => c.RetrieveRecords("account", null), Times.Once);
        _mockTargetClient.Verify(c => c.RetrieveRecords("contact", null), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_PassesRecordsToComparer()
    {
        // Arrange
        var config = new RefDataCompareConfig
        {
            Tables = new List<RefDataTableConfig>
            {
                new() { LogicalName = "account", PrimaryNameField = "name", PrimaryIdField = "accountid" }
            }
        };
        SetupConfigFile("config.json", config);

        var sourceRecords = new EntityCollection();
        var targetRecords = new EntityCollection();
        _mockSourceClient.Setup(c => c.RetrieveRecords("account", null)).Returns(sourceRecords);
        _mockTargetClient.Setup(c => c.RetrieveRecords("account", null)).Returns(targetRecords);
        _mockComparer.Setup(c => c.CompareRecords(
                It.IsAny<string>(), It.IsAny<EntityCollection>(), It.IsAny<EntityCollection>(),
                It.IsAny<HashSet<string>>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(new TableComparisonResult());

        // Act
        await _command.ExecuteAsync("config.json", "report.xlsx");

        // Assert
        _mockComparer.Verify(c => c.CompareRecords(
            "account", sourceRecords, targetRecords,
            It.IsAny<HashSet<string>>(), "name", "accountid"), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_GeneratesReport()
    {
        // Arrange
        var config = new RefDataCompareConfig
        {
            Tables = new List<RefDataTableConfig>
            {
                new() { LogicalName = "account" }
            }
        };
        SetupConfigFile("config.json", config);

        _mockSourceClient.Setup(c => c.RetrieveRecords(It.IsAny<string>(), It.IsAny<string?>()))
            .Returns(new EntityCollection());
        _mockTargetClient.Setup(c => c.RetrieveRecords(It.IsAny<string>(), It.IsAny<string?>()))
            .Returns(new EntityCollection());
        _mockComparer.Setup(c => c.CompareRecords(
                It.IsAny<string>(), It.IsAny<EntityCollection>(), It.IsAny<EntityCollection>(),
                It.IsAny<HashSet<string>>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(new TableComparisonResult());

        // Act
        await _command.ExecuteAsync("config.json", "output.xlsx");

        // Assert
        _mockReporter.Verify(r => r.GenerateReportAsync(
            It.IsAny<ComparisonResult>(), "output.xlsx"), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyTables_Returns1()
    {
        // Arrange
        var config = new RefDataCompareConfig { Tables = new List<RefDataTableConfig>() };
        SetupConfigFile("config.json", config);

        // Act
        var result = await _command.ExecuteAsync("config.json", "report.xlsx");

        // Assert
        Assert.Equal(1, result);
    }

    [Fact]
    public async Task ExecuteAsync_WhenExceptionOccurs_Returns1()
    {
        // Arrange
        _mockFileWriter.Setup(f => f.FileExists("config.json")).Returns(false);

        // Act
        var result = await _command.ExecuteAsync("config.json", "report.xlsx");

        // Assert
        Assert.Equal(1, result);
    }

    [Fact]
    public void CreateCliCommand_ReturnsValidCommand()
    {
        var command = RefDataCompareCommand.CreateCliCommand();

        Assert.NotNull(command);
        Assert.Equal("refdata-compare", command.Name);
    }
}
