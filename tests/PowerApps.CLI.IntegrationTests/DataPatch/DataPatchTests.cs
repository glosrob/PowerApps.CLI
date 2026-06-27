using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using PowerApps.CLI.Commands;
using PowerApps.CLI.Infrastructure;
using PowerApps.CLI.IntegrationTests.Infrastructure;
using PowerApps.CLI.Services;
using System.Text.Json;
using Xunit;

namespace PowerApps.CLI.IntegrationTests.DataPatch;

[Collection("Dataverse")]
[Trait("Category", "Integration")]
public class DataPatchTests : IAsyncLifetime
{
    private readonly DataverseFixture _fixture;

    // Cached statically — IDs are stable for the lifetime of the environment; re-fetching
    // before every test would waste 8 round-trips per run for data that never changes.
    private static Guid _testRecordId;
    private static Guid _otherTestRecordId;

    private const string TestEntity = "xrt_integrationtest";
    private const string TestKey = "DataPatch-Test-1";
    private const string AmbiguousKey = "DataPatch-Ambiguous";
    private const string KeyField = "xrt_name";
    private const string OtherTestEntity = "xrt_integrationothertest";
    private const string OtherTestKey = "Data Patch Test";

    public DataPatchTests(DataverseFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        if (_fixture.ConfigurationError is not null) return Task.CompletedTask;

        if (_testRecordId == Guid.Empty)
        {
            var records = _fixture.Client.RetrieveRecordsByFetchXml($@"<fetch count='1'>
  <entity name='{TestEntity}'>
    <attribute name='{TestEntity}id' />
    <filter>
      <condition attribute='{KeyField}' operator='eq' value='{TestKey}' />
    </filter>
  </entity>
</fetch>");

            if (records.Entities.Count == 0)
                throw new InvalidOperationException(
                    $"Test record '{TestKey}' not found in {TestEntity} — ensure prerequisite data exists before running integration tests.");

            _testRecordId = records.Entities[0].Id;
        }

        if (_otherTestRecordId == Guid.Empty)
        {
            var otherRecords = _fixture.Client.RetrieveRecordsByFetchXml($@"<fetch count='1'>
  <entity name='{OtherTestEntity}'>
    <attribute name='{OtherTestEntity}id' />
    <filter>
      <condition attribute='{KeyField}' operator='eq' value='{OtherTestKey}' />
    </filter>
  </entity>
</fetch>");

            if (otherRecords.Entities.Count == 0)
                throw new InvalidOperationException(
                    $"Test record '{OtherTestKey}' not found in {OtherTestEntity} — ensure prerequisite data exists before running integration tests.");

            _otherTestRecordId = otherRecords.Entities[0].Id;
        }

        // Prerequisite: two records with xrt_name = AmbiguousKey must exist in TestEntity.
        // If fewer than two exist, the AmbiguousMatch test would pass vacuously (exit 1 = NotFound,
        // not AmbiguousMatch), masking a missing test-data setup problem.
        var ambiguousRecords = _fixture.Client.RetrieveRecordsByFetchXml($@"<fetch count='3'>
  <entity name='{TestEntity}'>
    <attribute name='{TestEntity}id' />
    <filter>
      <condition attribute='{KeyField}' operator='eq' value='{AmbiguousKey}' />
    </filter>
  </entity>
</fetch>");

        if (ambiguousRecords.Entities.Count < 2)
            throw new InvalidOperationException(
                $"Expected at least 2 records with {KeyField}='{AmbiguousKey}' in {TestEntity} for the AmbiguousMatch test — found {ambiguousRecords.Entities.Count}.");

        var reset = new Entity(TestEntity, _testRecordId)
        {
            ["xrt_multilinetext"] = "pre-test-reset",
            ["xrt_localchoice"] = new OptionSetValue(971940000),
            ["xrt_lookupfield"] = null
        };
        _fixture.Client.Execute(new UpdateRequest { Target = reset });

        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // -------------------------------------------------------------------------
    // Happy path — string field
    // -------------------------------------------------------------------------

    [SkippableFact]
    public async Task ApplyPatch_StringField_UpdatesValueInDataverseAsync()
    {
        Skip.If(_fixture.ConfigurationError is not null, _fixture.ConfigurationError);

        var newValue = $"it-{Guid.NewGuid():N}";
        var configJson = StringPatchJson("xrt_multilinetext", newValue);
        var outputPath = TempReport();

        try
        {
            var exitCode = await CreateCommand().ExecuteAsync(null, configJson, outputPath);

            Assert.Equal(0, exitCode);
            var record = FetchTestRecord("xrt_multilinetext");
            Assert.Equal(newValue, record.GetAttributeValue<string>("xrt_multilinetext"));
        }
        finally { TryDelete(outputPath); }
    }

    [SkippableFact]
    public async Task ApplyPatch_WhenValueAlreadyCurrent_ExitsSuccessAsync()
    {
        Skip.If(_fixture.ConfigurationError is not null, _fixture.ConfigurationError);

        // InitializeAsync already set xrt_multilinetext to "pre-test-reset"; patching
        // the same value verifies the unchanged path returns exit 0 and leaves the value intact.
        var configJson = StringPatchJson("xrt_multilinetext", "pre-test-reset");
        var outputPath = TempReport();

        try
        {
            var exitCode = await CreateCommand().ExecuteAsync(null, configJson, outputPath);

            Assert.Equal(0, exitCode);
            var record = FetchTestRecord("xrt_multilinetext");
            Assert.Equal("pre-test-reset", record.GetAttributeValue<string>("xrt_multilinetext"));
        }
        finally { TryDelete(outputPath); }
    }

    // -------------------------------------------------------------------------
    // Error cases
    // -------------------------------------------------------------------------

    [SkippableFact]
    public async Task ApplyPatch_KeyNotFound_ExitsWithFailureAsync()
    {
        Skip.If(_fixture.ConfigurationError is not null, _fixture.ConfigurationError);

        var configJson = StringPatchJson("xrt_multilinetext", "irrelevant", key: "does-not-exist-99999");
        var outputPath = TempReport();

        try
        {
            var exitCode = await CreateCommand().ExecuteAsync(null, configJson, outputPath);

            Assert.Equal(1, exitCode);
        }
        finally { TryDelete(outputPath); }
    }

    [SkippableFact]
    public async Task ApplyPatch_AmbiguousMatch_ExitsWithFailureAsync()
    {
        Skip.If(_fixture.ConfigurationError is not null, _fixture.ConfigurationError);

        var configJson = StringPatchJson("xrt_multilinetext", "irrelevant", key: AmbiguousKey);
        var outputPath = TempReport();

        try
        {
            var exitCode = await CreateCommand().ExecuteAsync(null, configJson, outputPath);

            Assert.Equal(1, exitCode);
        }
        finally { TryDelete(outputPath); }
    }

    // -------------------------------------------------------------------------
    // Type hints — verify the round-trip into Dataverse
    // -------------------------------------------------------------------------

    [SkippableFact]
    public async Task ApplyPatch_OptionSetField_SetsValueInDataverseAsync()
    {
        Skip.If(_fixture.ConfigurationError is not null, _fixture.ConfigurationError);

        var configJson = JsonSerializer.Serialize(new
        {
            patches = new[]
            {
                new { entity = TestEntity, keyField = KeyField, key = TestKey, valueField = "xrt_localchoice", value = 971940001, type = "optionset" }
            }
        });
        var outputPath = TempReport();

        try
        {
            var exitCode = await CreateCommand().ExecuteAsync(null, configJson, outputPath);

            Assert.Equal(0, exitCode);
            var record = FetchTestRecord("xrt_localchoice");
            Assert.Equal(971940001, record.GetAttributeValue<OptionSetValue>("xrt_localchoice")?.Value);
        }
        finally { TryDelete(outputPath); }
    }

    [SkippableFact]
    public async Task ApplyPatch_LookupField_SetsReferenceInDataverseAsync()
    {
        Skip.If(_fixture.ConfigurationError is not null, _fixture.ConfigurationError);

        var configJson = JsonSerializer.Serialize(new
        {
            patches = new[]
            {
                new
                {
                    entity = TestEntity,
                    keyField = KeyField,
                    key = TestKey,
                    valueField = "xrt_lookupfield",
                    value = new { logicalName = OtherTestEntity, id = _otherTestRecordId.ToString() },
                    type = "lookup"
                }
            }
        });
        var outputPath = TempReport();

        try
        {
            var exitCode = await CreateCommand().ExecuteAsync(null, configJson, outputPath);

            Assert.Equal(0, exitCode);
            var record = FetchTestRecord("xrt_lookupfield");
            Assert.Equal(_otherTestRecordId, record.GetAttributeValue<EntityReference>("xrt_lookupfield")?.Id);
        }
        finally { TryDelete(outputPath); }
    }

    // -------------------------------------------------------------------------
    // Multi-patch
    // -------------------------------------------------------------------------

    [SkippableFact]
    public async Task ApplyPatch_MultiplePatchesInConfig_UpdatesAllFieldsAsync()
    {
        Skip.If(_fixture.ConfigurationError is not null, _fixture.ConfigurationError);

        var newText = $"multi-{Guid.NewGuid():N}";
        var configJson = JsonSerializer.Serialize(new
        {
            patches = new object[]
            {
                new { entity = TestEntity, keyField = KeyField, key = TestKey, valueField = "xrt_multilinetext", value = newText },
                new { entity = TestEntity, keyField = KeyField, key = TestKey, valueField = "xrt_localchoice", value = 971940001, type = "optionset" }
            }
        });
        var outputPath = TempReport();

        try
        {
            var exitCode = await CreateCommand().ExecuteAsync(null, configJson, outputPath);

            Assert.Equal(0, exitCode);
            var record = FetchTestRecord("xrt_multilinetext", "xrt_localchoice");
            Assert.Equal(newText, record.GetAttributeValue<string>("xrt_multilinetext"));
            Assert.Equal(971940001, record.GetAttributeValue<OptionSetValue>("xrt_localchoice")?.Value);
        }
        finally { TryDelete(outputPath); }
    }

    // -------------------------------------------------------------------------
    // Report
    // -------------------------------------------------------------------------

    [SkippableFact]
    public async Task ApplyPatch_ReportFile_IsWrittenAfterPatchAsync()
    {
        Skip.If(_fixture.ConfigurationError is not null, _fixture.ConfigurationError);

        var configJson = StringPatchJson("xrt_multilinetext", $"report-test-{Guid.NewGuid():N}");
        var outputPath = TempReport();

        try
        {
            await CreateCommand().ExecuteAsync(null, configJson, outputPath);

            Assert.True(File.Exists(outputPath), "Expected report file to exist.");
            Assert.True(new FileInfo(outputPath).Length > 0, "Expected report file to be non-empty.");
        }
        finally { TryDelete(outputPath); }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private DataPatchCommand CreateCommand() =>
        new(new ConsoleLogger(), _fixture.Client, new DataPatchReporter(new FileWriter()), new FileWriter());

    private static string StringPatchJson(string valueField, string value, string key = TestKey) =>
        JsonSerializer.Serialize(new
        {
            patches = new[] { new { entity = TestEntity, keyField = KeyField, key, valueField, value } }
        });

    private Entity FetchTestRecord(params string[] attributes)
    {
        var attrs = string.Join("\n    ", attributes.Select(a => $"<attribute name='{a}' />"));
        var xml = $@"<fetch count='1'>
  <entity name='{TestEntity}'>
    {attrs}
    <filter>
      <condition attribute='{KeyField}' operator='eq' value='{TestKey}' />
    </filter>
  </entity>
</fetch>";
        return _fixture.Client.RetrieveRecordsByFetchXml(xml).Entities[0];
    }

    private static string TempReport() =>
        Path.Combine(Path.GetTempPath(), $"data-patch-it-{Guid.NewGuid():N}.xlsx");

    private static void TryDelete(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }
}
