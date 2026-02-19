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
                It.IsAny<HashSet<string>>(), It.IsAny<ISet<string>?>(), It.IsAny<string?>(), It.IsAny<string?>()))
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
                It.IsAny<HashSet<string>>(), It.IsAny<ISet<string>?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(new TableComparisonResult());

        // Act
        await _command.ExecuteAsync("config.json", "report.xlsx");

        // Assert
        _mockComparer.Verify(c => c.CompareRecords(
            "account", sourceRecords, targetRecords,
            It.IsAny<HashSet<string>>(), It.IsAny<ISet<string>?>(), "name", "accountid"), Times.Once);
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
                It.IsAny<HashSet<string>>(), It.IsAny<ISet<string>?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(new TableComparisonResult());

        // Act
        await _command.ExecuteAsync("config.json", "output.xlsx");

        // Assert
        _mockReporter.Verify(r => r.GenerateReportAsync(
            It.IsAny<ComparisonResult>(), "output.xlsx"), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyTablesAndRelationships_Returns1()
    {
        // Arrange
        var config = new RefDataCompareConfig
        {
            Tables = new List<RefDataTableConfig>(),
            Relationships = new List<RefDataRelationshipConfig>()
        };
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
    public async Task ExecuteAsync_WithRelationships_RetrievesIntersectAndRelatedEntities()
    {
        // Arrange
        var config = new RefDataCompareConfig
        {
            Tables = new List<RefDataTableConfig>(),
            Relationships = new List<RefDataRelationshipConfig>
            {
                new()
                {
                    RelationshipName = "contact_leads",
                    IntersectEntity = "contactleads",
                    DisplayName = "Contact to Lead",
                    Entity1 = "contact",
                    Entity1IdField = "contactid",
                    Entity1NameField = "fullname",
                    Entity2 = "lead",
                    Entity2IdField = "leadid",
                    Entity2NameField = "fullname"
                }
            }
        };
        SetupConfigFile("config.json", config);

        _mockSourceClient.Setup(c => c.RetrieveRecords(It.IsAny<string>(), It.IsAny<string?>()))
            .Returns(new EntityCollection());
        _mockTargetClient.Setup(c => c.RetrieveRecords(It.IsAny<string>(), It.IsAny<string?>()))
            .Returns(new EntityCollection());
        _mockComparer.Setup(c => c.CompareAssociations(
                It.IsAny<string>(), It.IsAny<EntityCollection>(), It.IsAny<EntityCollection>(),
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Dictionary<Guid, string>>(), It.IsAny<Dictionary<Guid, string>>()))
            .Returns(new RelationshipComparisonResult());

        // Act
        await _command.ExecuteAsync("config.json", "report.xlsx");

        // Assert - intersect entity retrieved from both environments
        _mockSourceClient.Verify(c => c.RetrieveRecords("contactleads", null), Times.Once);
        _mockTargetClient.Verify(c => c.RetrieveRecords("contactleads", null), Times.Once);
        // Name resolution entities retrieved from source only
        _mockSourceClient.Verify(c => c.RetrieveRecords("contact", null), Times.Once);
        _mockSourceClient.Verify(c => c.RetrieveRecords("lead", null), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithRelationships_CallsCompareAssociations()
    {
        // Arrange
        var config = new RefDataCompareConfig
        {
            Tables = new List<RefDataTableConfig>(),
            Relationships = new List<RefDataRelationshipConfig>
            {
                new()
                {
                    RelationshipName = "contact_leads",
                    IntersectEntity = "contactleads",
                    DisplayName = "Contact to Lead",
                    Entity1 = "contact",
                    Entity1IdField = "contactid",
                    Entity2 = "lead",
                    Entity2IdField = "leadid"
                }
            }
        };
        SetupConfigFile("config.json", config);

        _mockSourceClient.Setup(c => c.RetrieveRecords(It.IsAny<string>(), It.IsAny<string?>()))
            .Returns(new EntityCollection());
        _mockTargetClient.Setup(c => c.RetrieveRecords(It.IsAny<string>(), It.IsAny<string?>()))
            .Returns(new EntityCollection());
        _mockComparer.Setup(c => c.CompareAssociations(
                It.IsAny<string>(), It.IsAny<EntityCollection>(), It.IsAny<EntityCollection>(),
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Dictionary<Guid, string>>(), It.IsAny<Dictionary<Guid, string>>()))
            .Returns(new RelationshipComparisonResult());

        // Act
        await _command.ExecuteAsync("config.json", "report.xlsx");

        // Assert
        _mockComparer.Verify(c => c.CompareAssociations(
            "Contact to Lead",
            It.IsAny<EntityCollection>(), It.IsAny<EntityCollection>(),
            "contactid", "leadid",
            It.IsAny<Dictionary<Guid, string>>(), It.IsAny<Dictionary<Guid, string>>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyRelationships_SkipsRelationshipComparison()
    {
        // Arrange
        var config = new RefDataCompareConfig
        {
            Tables = new List<RefDataTableConfig>
            {
                new() { LogicalName = "account" }
            },
            Relationships = new List<RefDataRelationshipConfig>()
        };
        SetupConfigFile("config.json", config);

        _mockSourceClient.Setup(c => c.RetrieveRecords(It.IsAny<string>(), It.IsAny<string?>()))
            .Returns(new EntityCollection());
        _mockTargetClient.Setup(c => c.RetrieveRecords(It.IsAny<string>(), It.IsAny<string?>()))
            .Returns(new EntityCollection());
        _mockComparer.Setup(c => c.CompareRecords(
                It.IsAny<string>(), It.IsAny<EntityCollection>(), It.IsAny<EntityCollection>(),
                It.IsAny<HashSet<string>>(), It.IsAny<ISet<string>?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(new TableComparisonResult());

        // Act
        await _command.ExecuteAsync("config.json", "report.xlsx");

        // Assert - CompareAssociations should never be called
        _mockComparer.Verify(c => c.CompareAssociations(
            It.IsAny<string>(), It.IsAny<EntityCollection>(), It.IsAny<EntityCollection>(),
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<Dictionary<Guid, string>>(), It.IsAny<Dictionary<Guid, string>>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_RelationshipWithoutExplicitFields_CallsMetadataLookup()
    {
        // Arrange — only RelationshipName provided, no explicit entity fields
        var config = new RefDataCompareConfig
        {
            Tables = new List<RefDataTableConfig>(),
            Relationships = new List<RefDataRelationshipConfig>
            {
                new() { RelationshipName = "contact_leads" }
            }
        };
        SetupConfigFile("config.json", config);

        var metadata = new Microsoft.Xrm.Sdk.Metadata.ManyToManyRelationshipMetadata
        {
            IntersectEntityName = "contactleads",
            Entity1LogicalName = "contact",
            Entity1IntersectAttribute = "contactid",
            Entity2LogicalName = "lead",
            Entity2IntersectAttribute = "leadid"
        };
        _mockSourceClient.Setup(c => c.GetManyToManyRelationshipMetadata("contact_leads")).Returns(metadata);
        _mockSourceClient.Setup(c => c.RetrieveRecords(It.IsAny<string>(), It.IsAny<string?>()))
            .Returns(new EntityCollection());
        _mockTargetClient.Setup(c => c.RetrieveRecords(It.IsAny<string>(), It.IsAny<string?>()))
            .Returns(new EntityCollection());
        _mockComparer.Setup(c => c.CompareAssociations(
                It.IsAny<string>(), It.IsAny<EntityCollection>(), It.IsAny<EntityCollection>(),
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Dictionary<Guid, string>>(), It.IsAny<Dictionary<Guid, string>>()))
            .Returns(new RelationshipComparisonResult());

        // Act
        await _command.ExecuteAsync("config.json", "report.xlsx");

        // Assert — metadata lookup was called, intersect entity was resolved from it
        _mockSourceClient.Verify(c => c.GetManyToManyRelationshipMetadata("contact_leads"), Times.Once);
        _mockSourceClient.Verify(c => c.RetrieveRecords("contactleads", null), Times.Once);
        _mockTargetClient.Verify(c => c.RetrieveRecords("contactleads", null), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithIncludeFields_PassesAllowlistToComparer()
    {
        // Arrange
        var config = new RefDataCompareConfig
        {
            Tables = new List<RefDataTableConfig>
            {
                new() { LogicalName = "account", IncludeFields = new List<string> { "name", "telephone1" } }
            }
        };
        SetupConfigFile("config.json", config);

        _mockSourceClient.Setup(c => c.RetrieveRecords(It.IsAny<string>(), It.IsAny<string?>()))
            .Returns(new EntityCollection());
        _mockTargetClient.Setup(c => c.RetrieveRecords(It.IsAny<string>(), It.IsAny<string?>()))
            .Returns(new EntityCollection());
        _mockComparer.Setup(c => c.CompareRecords(
                It.IsAny<string>(), It.IsAny<EntityCollection>(), It.IsAny<EntityCollection>(),
                It.IsAny<HashSet<string>>(), It.IsAny<ISet<string>?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(new TableComparisonResult());

        // Act
        await _command.ExecuteAsync("config.json", "report.xlsx");

        // Assert — CompareRecords was called with a non-null includeFields set containing our 2 fields
        _mockComparer.Verify(c => c.CompareRecords(
            "account",
            It.IsAny<EntityCollection>(),
            It.IsAny<EntityCollection>(),
            It.IsAny<HashSet<string>>(),
            It.Is<ISet<string>?>(s => s != null && s.Contains("name") && s.Contains("telephone1")),
            It.IsAny<string?>(),
            It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public void CreateCliCommand_ReturnsValidCommand()
    {
        var command = RefDataCompareCommand.CreateCliCommand();

        Assert.NotNull(command);
        Assert.Equal("refdata-compare", command.Name);
    }
}
