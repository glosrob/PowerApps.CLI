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
                    ValueField = "anc_employmentstatus",
                    Value = JsonDocument.Parse("749500000").RootElement,
                    Type = "optionset"
                }
            }
        };
        SetupConfigFile("config.json", config);

        var recordId = Guid.NewGuid();
        var existingRecord = new Entity("contact", recordId);
        existingRecord["anc_employmentstatus"] = new OptionSetValue(100000000);
        _mockClient.Setup(c => c.RetrieveRecordsByFetchXml(It.IsAny<string>()))
            .Returns(new EntityCollection(new List<Entity> { existingRecord }));
        _mockClient.Setup(c => c.Execute(It.IsAny<UpdateRequest>()))
            .Returns(new UpdateResponse());

        // Act
        var result = await _command.ExecuteAsync("config.json", null, "report.xlsx");

        // Assert — update called with OptionSetValue, not plain int
        Assert.Equal(0, result);
        _mockClient.Verify(c => c.Execute(It.Is<UpdateRequest>(r =>
            r.Target["anc_employmentstatus"] != null &&
            r.Target["anc_employmentstatus"].GetType() == typeof(OptionSetValue) &&
            ((OptionSetValue)r.Target["anc_employmentstatus"]).Value == 749500000)),
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
                    ValueField = "anc_employmentstatus",
                    Value = JsonDocument.Parse("749500000").RootElement,
                    Type = "optionset"
                }
            }
        };
        SetupConfigFile("config.json", config);

        var existingRecord = new Entity("contact", Guid.NewGuid());
        existingRecord["anc_employmentstatus"] = new OptionSetValue(749500000);
        _mockClient.Setup(c => c.RetrieveRecordsByFetchXml(It.IsAny<string>()))
            .Returns(new EntityCollection(new List<Entity> { existingRecord }));

        // Act
        var result = await _command.ExecuteAsync("config.json", null, "report.xlsx");

        // Assert — no update because value is already correct
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
}
