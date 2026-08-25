using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Moq;
using PowerApps.CLI.Commands;
using PowerApps.CLI.Infrastructure;
using PowerApps.CLI.Models;
using PowerApps.CLI.Services;
using System.Text.Json;
using Xunit;

namespace PowerApps.CLI.Tests.Commands;

public class DataPatchCommandTests
{
    private readonly Mock<IConsoleLogger> _mockLogger;
    private readonly Mock<IDataverseClient> _mockClient;
    private readonly Mock<IDataPatchReporter> _mockReporter;
    private readonly Mock<IFileWriter> _mockFileWriter;
    private readonly DataPatchCommand _command;

    public DataPatchCommandTests()
    {
        _mockLogger = new Mock<IConsoleLogger>();
        _mockClient = new Mock<IDataverseClient>();
        _mockReporter = new Mock<IDataPatchReporter>();
        _mockFileWriter = new Mock<IFileWriter>();

        _mockClient.Setup(c => c.GetEnvironmentUrl()).Returns("https://target.crm.dynamics.com");

        _command = new DataPatchCommand(
            _mockLogger.Object,
            _mockClient.Object,
            _mockReporter.Object,
            _mockFileWriter.Object);
    }

    private void SetupConfigFile(string configPath, DataPatchConfig config)
    {
        _mockFileWriter.Setup(f => f.FileExists(configPath)).Returns(true);
        var json = JsonSerializer.Serialize(config);
        _mockFileWriter.Setup(f => f.ReadTextAsync(configPath)).ReturnsAsync(json);
    }

    private static EntityCollection SingleRecord(string entityName, Guid id, string field, string value)
    {
        var entity = new Entity(entityName, id);
        entity[field] = value;
        return new EntityCollection(new List<Entity> { entity });
    }

    private static EntityCollection EmptyCollection() => new EntityCollection();

    private static DataPatchConfig SinglePatchConfig(string newValue = "new-value") => new DataPatchConfig
    {
        Patches = new List<PatchEntry>
        {
            new()
            {
                Entity = "mspp_sitesetting",
                KeyField = "mspp_name",
                Key = "Auth/ClientId",
                ValueField = "mspp_value",
                Value = JsonDocument.Parse($"\"{newValue}\"").RootElement
            }
        }
    };

    [Fact]
    public async Task ExecuteAsync_WhenValueDiffers_UpdatesRecord()
    {
        // Arrange
        var config = SinglePatchConfig("new-value");
        SetupConfigFile("config.json", config);

        var recordId = Guid.NewGuid();
        _mockClient.Setup(c => c.RetrieveRecordsByFetchXml(It.IsAny<string>()))
            .Returns(SingleRecord("mspp_sitesetting", recordId, "mspp_value", "old-value"));
        _mockClient.Setup(c => c.Execute(It.IsAny<UpdateRequest>()))
            .Returns(new UpdateResponse());

        // Act
        var result = await _command.ExecuteAsync("config.json", null, "report.xlsx");

        // Assert
        Assert.Equal(0, result);
        _mockClient.Verify(c => c.Execute(It.Is<UpdateRequest>(r =>
            r.Target.LogicalName == "mspp_sitesetting" &&
            r.Target.Id == recordId &&
            (string)r.Target["mspp_value"] == "new-value")), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenValueUnchanged_SkipsUpdate()
    {
        // Arrange
        var config = SinglePatchConfig("same-value");
        SetupConfigFile("config.json", config);

        _mockClient.Setup(c => c.RetrieveRecordsByFetchXml(It.IsAny<string>()))
            .Returns(SingleRecord("mspp_sitesetting", Guid.NewGuid(), "mspp_value", "same-value"));

        // Act
        var result = await _command.ExecuteAsync("config.json", null, "report.xlsx");

        // Assert
        Assert.Equal(0, result);
        _mockClient.Verify(c => c.Execute(It.IsAny<UpdateRequest>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRecordNotFound_Returns1()
    {
        // Arrange
        var config = SinglePatchConfig();
        SetupConfigFile("config.json", config);

        _mockClient.Setup(c => c.RetrieveRecordsByFetchXml(It.IsAny<string>()))
            .Returns(EmptyCollection());

        // Act
        var result = await _command.ExecuteAsync("config.json", null, "report.xlsx");

        // Assert
        Assert.Equal(1, result);
        _mockClient.Verify(c => c.Execute(It.IsAny<UpdateRequest>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAmbiguousMatch_Returns1()
    {
        // Arrange
        var config = SinglePatchConfig();
        SetupConfigFile("config.json", config);

        var twoRecords = new EntityCollection(new List<Entity>
        {
            new Entity("mspp_sitesetting", Guid.NewGuid()),
            new Entity("mspp_sitesetting", Guid.NewGuid())
        });
        _mockClient.Setup(c => c.RetrieveRecordsByFetchXml(It.IsAny<string>()))
            .Returns(twoRecords);

        // Act
        var result = await _command.ExecuteAsync("config.json", null, "report.xlsx");

        // Assert
        Assert.Equal(1, result);
        _mockClient.Verify(c => c.Execute(It.IsAny<UpdateRequest>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUpdateThrows_Returns1()
    {
        // Arrange
        var config = SinglePatchConfig("new-value");
        SetupConfigFile("config.json", config);

        _mockClient.Setup(c => c.RetrieveRecordsByFetchXml(It.IsAny<string>()))
            .Returns(SingleRecord("mspp_sitesetting", Guid.NewGuid(), "mspp_value", "old-value"));
        _mockClient.Setup(c => c.Execute(It.IsAny<UpdateRequest>()))
            .Throws(new InvalidOperationException("Update failed"));

        // Act
        var result = await _command.ExecuteAsync("config.json", null, "report.xlsx");

        // Assert
        Assert.Equal(1, result);
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyPatches_Returns1()
    {
        // Arrange
        var config = new DataPatchConfig { Patches = new List<PatchEntry>() };
        SetupConfigFile("config.json", config);

        // Act
        var result = await _command.ExecuteAsync("config.json", null, "report.xlsx");

        // Assert
        Assert.Equal(1, result);
    }

    [Fact]
    public async Task ExecuteAsync_WithConfigJson_ParsesAndAppliesPatches()
    {
        // Arrange
        var json = """
            {
              "patches": [
                {
                  "entity": "mspp_sitesetting",
                  "keyField": "mspp_name",
                  "key": "Auth/ClientId",
                  "valueField": "mspp_value",
                  "value": "inline-value"
                }
              ]
            }
            """;

        _mockClient.Setup(c => c.RetrieveRecordsByFetchXml(It.IsAny<string>()))
            .Returns(SingleRecord("mspp_sitesetting", Guid.NewGuid(), "mspp_value", "old-value"));
        _mockClient.Setup(c => c.Execute(It.IsAny<UpdateRequest>()))
            .Returns(new UpdateResponse());

        // Act
        var result = await _command.ExecuteAsync(null, json, "report.xlsx");

        // Assert
        Assert.Equal(0, result);
        _mockClient.Verify(c => c.Execute(It.Is<UpdateRequest>(r =>
            (string)r.Target["mspp_value"] == "inline-value")), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_GeneratesReport()
    {
        // Arrange
        var config = SinglePatchConfig("new-value");
        SetupConfigFile("config.json", config);

        _mockClient.Setup(c => c.RetrieveRecordsByFetchXml(It.IsAny<string>()))
            .Returns(SingleRecord("mspp_sitesetting", Guid.NewGuid(), "mspp_value", "old-value"));
        _mockClient.Setup(c => c.Execute(It.IsAny<UpdateRequest>()))
            .Returns(new UpdateResponse());

        // Act
        await _command.ExecuteAsync("config.json", null, "output.xlsx");

        // Assert
        _mockReporter.Verify(r => r.GenerateReportAsync(
            It.IsAny<DataPatchSummary>(), "output.xlsx"), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ConfigFileNotFound_Returns1()
    {
        // Arrange
        _mockFileWriter.Setup(f => f.FileExists("missing.json")).Returns(false);

        // Act
        var result = await _command.ExecuteAsync("missing.json", null, "report.xlsx");

        // Assert
        Assert.Equal(1, result);
    }

    [Fact]
    public async Task ExecuteAsync_WithDateType_ParsesStringAsDateTime()
    {
        // Arrange
        var config = new DataPatchConfig
        {
            Patches = new List<PatchEntry>
            {
                new()
                {
                    Entity = "contact",
                    KeyField = "fullname",
                    Key = "Robert Tilling",
                    ValueField = "birthdate",
                    Value = JsonDocument.Parse("\"2026-01-01\"").RootElement,
                    Type = "date"
                }
            }
        };
        SetupConfigFile("config.json", config);

        var recordId = Guid.NewGuid();
        var existingRecord = new Entity("contact", recordId);
        existingRecord["birthdate"] = new DateTime(2000, 6, 15);
        _mockClient.Setup(c => c.RetrieveRecordsByFetchXml(It.IsAny<string>()))
            .Returns(new EntityCollection(new List<Entity> { existingRecord }));
        _mockClient.Setup(c => c.Execute(It.IsAny<UpdateRequest>()))
            .Returns(new UpdateResponse());

        // Act
        var result = await _command.ExecuteAsync("config.json", null, "report.xlsx");

        // Assert — update called with a DateTime value, not a string
        Assert.Equal(0, result);
        _mockClient.Verify(c => c.Execute(It.Is<UpdateRequest>(r =>
            r.Target["birthdate"] != null &&
            r.Target["birthdate"].GetType() == typeof(DateTime) &&
            ((DateTime)r.Target["birthdate"]).Year == 2026 &&
            ((DateTime)r.Target["birthdate"]).Month == 1 &&
            ((DateTime)r.Target["birthdate"]).Day == 1)),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithOptionSetType_WrapsInOptionSetValue()
    {
        // Arrange
        var config = new DataPatchConfig
        {
            Patches = new List<PatchEntry>
            {
                new()
                {
                    Entity = "contact",
                    KeyField = "fullname",
                    Key = "Robert Tilling",
                    ValueField = "rob_employmentstatus",
                    Value = JsonDocument.Parse("749500000").RootElement,
                    Type = "optionset"
                }
            }
        };
        SetupConfigFile("config.json", config);

        var recordId = Guid.NewGuid();
        var existingRecord = new Entity("contact", recordId);
        existingRecord["rob_employmentstatus"] = new OptionSetValue(100000000);
        _mockClient.Setup(c => c.RetrieveRecordsByFetchXml(It.IsAny<string>()))
            .Returns(new EntityCollection(new List<Entity> { existingRecord }));
        _mockClient.Setup(c => c.Execute(It.IsAny<UpdateRequest>()))
            .Returns(new UpdateResponse());

        // Act
        var result = await _command.ExecuteAsync("config.json", null, "report.xlsx");

        // Assert — update called with OptionSetValue, not plain int
        Assert.Equal(0, result);
        _mockClient.Verify(c => c.Execute(It.Is<UpdateRequest>(r =>
            r.Target["rob_employmentstatus"] != null &&
            r.Target["rob_employmentstatus"].GetType() == typeof(OptionSetValue) &&
            ((OptionSetValue)r.Target["rob_employmentstatus"]).Value == 749500000)),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithOptionSetType_WhenUnchanged_SkipsUpdate()
    {
        // Arrange
        var config = new DataPatchConfig
        {
            Patches = new List<PatchEntry>
            {
                new()
                {
                    Entity = "contact",
                    KeyField = "fullname",
                    Key = "Robert Tilling",
                    ValueField = "rob_employmentstatus",
                    Value = JsonDocument.Parse("749500000").RootElement,
                    Type = "optionset"
                }
            }
        };
        SetupConfigFile("config.json", config);

        var existingRecord = new Entity("contact", Guid.NewGuid());
        existingRecord["rob_employmentstatus"] = new OptionSetValue(749500000);
        _mockClient.Setup(c => c.RetrieveRecordsByFetchXml(It.IsAny<string>()))
            .Returns(new EntityCollection(new List<Entity> { existingRecord }));

        // Act
        var result = await _command.ExecuteAsync("config.json", null, "report.xlsx");

        // Assert — no update because value is already correct
        Assert.Equal(0, result);
        _mockClient.Verify(c => c.Execute(It.IsAny<UpdateRequest>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WithLookupType_WrapsInEntityReference()
    {
        // Arrange
        var targetId = Guid.NewGuid();
        var config = new DataPatchConfig
        {
            Patches = new List<PatchEntry>
            {
                new()
                {
                    Entity = "contact",
                    KeyField = "fullname",
                    Key = "Robert Tilling",
                    ValueField = "rob_somelookup",
                    Value = JsonDocument.Parse($$"""{"logicalName":"account","id":"{{targetId}}"}""").RootElement,
                    Type = "lookup"
                }
            }
        };
        SetupConfigFile("config.json", config);

        var recordId = Guid.NewGuid();
        var existingRecord = new Entity("contact", recordId);
        existingRecord["rob_somelookup"] = new EntityReference("account", Guid.NewGuid()); // different GUID
        _mockClient.Setup(c => c.RetrieveRecordsByFetchXml(It.IsAny<string>()))
            .Returns(new EntityCollection(new List<Entity> { existingRecord }));
        _mockClient.Setup(c => c.Execute(It.IsAny<UpdateRequest>()))
            .Returns(new UpdateResponse());

        // Act
        var result = await _command.ExecuteAsync("config.json", null, "report.xlsx");

        // Assert — update called with EntityReference, not a plain GUID string
        Assert.Equal(0, result);
        _mockClient.Verify(c => c.Execute(It.Is<UpdateRequest>(r =>
            r.Target["rob_somelookup"] != null &&
            r.Target["rob_somelookup"].GetType() == typeof(EntityReference) &&
            ((EntityReference)r.Target["rob_somelookup"]).LogicalName == "account" &&
            ((EntityReference)r.Target["rob_somelookup"]).Id == targetId)),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithLookupType_WhenUnchanged_SkipsUpdate()
    {
        // Arrange
        var targetId = Guid.NewGuid();
        var config = new DataPatchConfig
        {
            Patches = new List<PatchEntry>
            {
                new()
                {
                    Entity = "contact",
                    KeyField = "fullname",
                    Key = "Robert Tilling",
                    ValueField = "rob_somelookup",
                    Value = JsonDocument.Parse($$"""{"logicalName":"account","id":"{{targetId}}"}""").RootElement,
                    Type = "lookup"
                }
            }
        };
        SetupConfigFile("config.json", config);

        var existingRecord = new Entity("contact", Guid.NewGuid());
        existingRecord["rob_somelookup"] = new EntityReference("account", targetId); // same GUID
        _mockClient.Setup(c => c.RetrieveRecordsByFetchXml(It.IsAny<string>()))
            .Returns(new EntityCollection(new List<Entity> { existingRecord }));

        // Act
        var result = await _command.ExecuteAsync("config.json", null, "report.xlsx");

        // Assert — no update because the GUID already matches
        Assert.Equal(0, result);
        _mockClient.Verify(c => c.Execute(It.IsAny<UpdateRequest>()), Times.Never);
    }

    [Fact]
    public void CreateCliCommand_ReturnsValidCommand()
    {
        var command = DataPatchCommand.CreateCliCommand();

        Assert.NotNull(command);
        Assert.Equal("data-patch", command.Name);
    }

    [Fact]
    public async Task ApplyPatch_LogsFetchXml()
    {
        var config = SinglePatchConfig("new-value");
        SetupConfigFile("config.json", config);
        _mockClient.Setup(c => c.RetrieveRecordsByFetchXml(It.IsAny<string>()))
            .Returns(SingleRecord("mspp_sitesetting", Guid.NewGuid(), "mspp_value", "old-value"));
        _mockClient.Setup(c => c.Execute(It.IsAny<UpdateRequest>()))
            .Returns(new UpdateResponse());

        await _command.ExecuteAsync("config.json", null, "report.xlsx");

        _mockLogger.Verify(l => l.LogVerbose(It.Is<string>(s => s.Contains("<fetch"))), Times.Once);
    }

    [Fact]
    public async Task ApplyPatch_LogsResultCount()
    {
        var config = SinglePatchConfig("new-value");
        SetupConfigFile("config.json", config);
        _mockClient.Setup(c => c.RetrieveRecordsByFetchXml(It.IsAny<string>()))
            .Returns(SingleRecord("mspp_sitesetting", Guid.NewGuid(), "mspp_value", "old-value"));
        _mockClient.Setup(c => c.Execute(It.IsAny<UpdateRequest>()))
            .Returns(new UpdateResponse());

        await _command.ExecuteAsync("config.json", null, "report.xlsx");

        _mockLogger.Verify(l => l.LogVerbose(It.Is<string>(s => s.Contains("1 record(s)"))), Times.Once);
    }

    [Fact]
    public async Task ApplyPatch_FetchXmlUsesCount2NotTop()
    {
        var config = SinglePatchConfig();
        SetupConfigFile("config.json", config);
        _mockClient.Setup(c => c.RetrieveRecordsByFetchXml(It.IsAny<string>()))
            .Returns(EmptyCollection());

        await _command.ExecuteAsync("config.json", null, "report.xlsx");

        _mockClient.Verify(c => c.RetrieveRecordsByFetchXml(
            It.Is<string>(xml => xml.Contains("count='2'") && !xml.Contains("top="))), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_GeneratesReport_WithCorrectSummaryContents()
    {
        var config = SinglePatchConfig("new-value");
        SetupConfigFile("config.json", config);
        _mockClient.Setup(c => c.RetrieveRecordsByFetchXml(It.IsAny<string>()))
            .Returns(SingleRecord("mspp_sitesetting", Guid.NewGuid(), "mspp_value", "old-value"));
        _mockClient.Setup(c => c.Execute(It.IsAny<UpdateRequest>()))
            .Returns(new UpdateResponse());

        await _command.ExecuteAsync("config.json", null, "report.xlsx");

        _mockReporter.Verify(r => r.GenerateReportAsync(
            It.Is<DataPatchSummary>(s =>
                s.EnvironmentUrl == "https://target.crm.dynamics.com" &&
                s.UpdatedCount == 1 &&
                s.FailedCount == 0),
            "report.xlsx"), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithGuidType_ParsesStringAsGuid()
    {
        var targetGuid = Guid.NewGuid();
        var config = new DataPatchConfig
        {
            Patches = new List<PatchEntry>
            {
                new()
                {
                    Entity = "contact",
                    KeyField = "fullname",
                    Key = "Robert Tilling",
                    ValueField = "someguidfield",
                    Value = JsonDocument.Parse($"\"{targetGuid}\"").RootElement,
                    Type = "guid"
                }
            }
        };
        SetupConfigFile("config.json", config);

        var existingRecord = new Entity("contact", Guid.NewGuid());
        existingRecord["someguidfield"] = Guid.NewGuid();
        _mockClient.Setup(c => c.RetrieveRecordsByFetchXml(It.IsAny<string>()))
            .Returns(new EntityCollection(new List<Entity> { existingRecord }));
        _mockClient.Setup(c => c.Execute(It.IsAny<UpdateRequest>()))
            .Returns(new UpdateResponse());

        var result = await _command.ExecuteAsync("config.json", null, "report.xlsx");

        Assert.Equal(0, result);
        _mockClient.Verify(c => c.Execute(It.Is<UpdateRequest>(r =>
            r.Target["someguidfield"] is Guid &&
            (Guid)r.Target["someguidfield"] == targetGuid)), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithDatetimeType_ParsesStringAsDateTime()
    {
        var config = new DataPatchConfig
        {
            Patches = new List<PatchEntry>
            {
                new()
                {
                    Entity = "xrt_integrationtest",
                    KeyField = "xrt_name",
                    Key = "DataPatch-Test-1",
                    ValueField = "xrt_datetimefield",
                    Value = JsonDocument.Parse("\"2026-01-15T09:30:00Z\"").RootElement,
                    Type = "datetime"
                }
            }
        };
        SetupConfigFile("config.json", config);

        var existingRecord = new Entity("xrt_integrationtest", Guid.NewGuid());
        existingRecord["xrt_datetimefield"] = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        _mockClient.Setup(c => c.RetrieveRecordsByFetchXml(It.IsAny<string>()))
            .Returns(new EntityCollection(new List<Entity> { existingRecord }));
        _mockClient.Setup(c => c.Execute(It.IsAny<UpdateRequest>()))
            .Returns(new UpdateResponse());

        var result = await _command.ExecuteAsync("config.json", null, "report.xlsx");

        Assert.Equal(0, result);
        _mockClient.Verify(c => c.Execute(It.Is<UpdateRequest>(r =>
            r.Target["xrt_datetimefield"] is DateTime &&
            ((DateTime)r.Target["xrt_datetimefield"]).Year == 2026 &&
            ((DateTime)r.Target["xrt_datetimefield"]).Month == 1 &&
            ((DateTime)r.Target["xrt_datetimefield"]).Day == 15)),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRetrieveThrows_Returns1()
    {
        var config = SinglePatchConfig();
        SetupConfigFile("config.json", config);
        _mockClient.Setup(c => c.RetrieveRecordsByFetchXml(It.IsAny<string>()))
            .Throws(new InvalidOperationException("Retrieve failed"));

        var result = await _command.ExecuteAsync("config.json", null, "report.xlsx");

        Assert.Equal(1, result);
        _mockClient.Verify(c => c.Execute(It.IsAny<UpdateRequest>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WithMoneyField_WhenUnchanged_SkipsUpdate()
    {
        var config = new DataPatchConfig
        {
            Patches = new List<PatchEntry>
            {
                new()
                {
                    Entity = "xrt_integrationtest",
                    KeyField = "xrt_name",
                    Key = "DataPatch-Test-1",
                    ValueField = "xrt_currencyfield",
                    Value = JsonDocument.Parse("100").RootElement
                }
            }
        };
        SetupConfigFile("config.json", config);

        var existingRecord = new Entity("xrt_integrationtest", Guid.NewGuid());
        existingRecord["xrt_currencyfield"] = new Money(100m);
        _mockClient.Setup(c => c.RetrieveRecordsByFetchXml(It.IsAny<string>()))
            .Returns(new EntityCollection(new List<Entity> { existingRecord }));

        var result = await _command.ExecuteAsync("config.json", null, "report.xlsx");

        Assert.Equal(0, result);
        _mockClient.Verify(c => c.Execute(It.IsAny<UpdateRequest>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WithMultiplePatches_PartialSuccess_Returns1()
    {
        var config = new DataPatchConfig
        {
            Patches = new List<PatchEntry>
            {
                new()
                {
                    Entity = "mspp_sitesetting",
                    KeyField = "mspp_name",
                    Key = "Found-Key",
                    ValueField = "mspp_value",
                    Value = JsonDocument.Parse("\"new-value\"").RootElement
                },
                new()
                {
                    Entity = "mspp_sitesetting",
                    KeyField = "mspp_name",
                    Key = "Missing-Key",
                    ValueField = "mspp_value",
                    Value = JsonDocument.Parse("\"new-value\"").RootElement
                }
            }
        };
        SetupConfigFile("config.json", config);

        _mockClient.SetupSequence(c => c.RetrieveRecordsByFetchXml(It.IsAny<string>()))
            .Returns(SingleRecord("mspp_sitesetting", Guid.NewGuid(), "mspp_value", "old-value"))
            .Returns(EmptyCollection());
        _mockClient.Setup(c => c.Execute(It.IsAny<UpdateRequest>()))
            .Returns(new UpdateResponse());

        var result = await _command.ExecuteAsync("config.json", null, "report.xlsx");

        Assert.Equal(1, result);
        _mockClient.Verify(c => c.Execute(It.IsAny<UpdateRequest>()), Times.Once);
        _mockReporter.Verify(r => r.GenerateReportAsync(
            It.Is<DataPatchSummary>(s => s.UpdatedCount == 1 && s.FailedCount == 1),
            It.IsAny<string>()), Times.Once);
    }
}
