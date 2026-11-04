using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using PowerApps.CLI.Infrastructure;
using PowerApps.CLI.IntegrationTests.Infrastructure;
using PowerApps.CLI.Models;
using PowerApps.CLI.Services;
using Xunit;

namespace PowerApps.CLI.IntegrationTests.RefDataMigrate;

/// <summary>
/// Exercises refdata-migrate against the real integration environment as the target, with an
/// <see cref="InMemorySourceClient"/> standing in for the source. The point is to prove Dataverse
/// accepts what the migrator sends (null lookup patches, Update-based state changes), which the unit
/// tests can only assert at the request level.
/// </summary>
[Collection("Dataverse")]
[Trait("Category", "Integration")]
public class RefDataMigrateTests : IAsyncLifetime
{
    private readonly DataverseFixture _fixture;

    private static Guid _lookupTargetId;

    // xrt_integrationothertest has no workflows, business rules or plugin steps registered on it, so these
    // tests don't depend on the state the process-manage fixtures leave xrt_integrationtest's processes in.
    private const string TestEntity = "xrt_integrationothertest";
    private const string LookupField = "xrt_parent";
    private const string LookupTargetEntity = TestEntity;
    private const string LookupTargetName = "Data Patch Test";
    private const string NamePrefix = "RefDataMigrate-IT-";

    private const int StateActive = 0;
    private const int StateInactive = 1;
    private const int StatusActive = 1;
    private const int StatusInactive = 2;

    // Fixed IDs so a run that dies before teardown is cleaned up by the next run's InitializeAsync
    private static readonly Guid ClearLookupId = new("7e57da7a-0119-4000-8000-000000000001");
    private static readonly Guid DeactivateId = new("7e57da7a-0119-4000-8000-000000000002");
    private static readonly Guid ReactivateId = new("7e57da7a-0119-4000-8000-000000000003");
    private static readonly Guid NewInactiveId = new("7e57da7a-0119-4000-8000-000000000004");
    private static readonly Guid RerunId = new("7e57da7a-0119-4000-8000-000000000005");

    private static readonly Guid[] AllTestIds = { ClearLookupId, DeactivateId, ReactivateId, NewInactiveId, RerunId };

    public RefDataMigrateTests(DataverseFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        if (_fixture.ConfigurationError is not null)
        {
            return Task.CompletedTask;
        }

        if (_lookupTargetId == Guid.Empty)
        {
            var records = _fixture.Client.RetrieveRecordsByFetchXml($@"<fetch count='1'>
  <entity name='{LookupTargetEntity}'>
    <attribute name='{LookupTargetEntity}id' />
    <filter>
      <condition attribute='xrt_name' operator='eq' value='{LookupTargetName}' />
    </filter>
  </entity>
</fetch>");

            if (records.Entities.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Lookup target '{LookupTargetName}' not found in {LookupTargetEntity} — ensure prerequisite data exists before running integration tests.");
            }

            _lookupTargetId = records.Entities[0].Id;
        }

        DeleteTestRecords();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        if (_fixture.ConfigurationError is null)
        {
            DeleteTestRecords();
        }

        return Task.CompletedTask;
    }

    // -------------------------------------------------------------------------
    // #114 — values cleared in source are cleared in target
    // -------------------------------------------------------------------------

    [SkippableFact]
    public async Task Migrate_LookupNullInSource_ClearsLookupInTargetAsync()
    {
        Skip.If(_fixture.ConfigurationError is not null, _fixture.ConfigurationError);

        CreateTargetRecord(ClearLookupId, "ClearLookup", lookup: LookupTargetRef());
        var source = new InMemorySourceClient().AddRecord(SourceRecord(ClearLookupId, "ClearLookup"));

        var result = await MigrateAsync(source, manageState: false);

        AssertNoErrors(result);
        Assert.Equal(1, result.LookupsPatchedCount);
        Assert.Null(RetrieveTarget(ClearLookupId).GetAttributeValue<EntityReference>(LookupField));
    }

    // -------------------------------------------------------------------------
    // #116 — manageState via Update (not SetState), including reactivation
    // -------------------------------------------------------------------------

    [SkippableFact]
    public async Task Migrate_InactiveInSource_DeactivatesTargetAsync()
    {
        Skip.If(_fixture.ConfigurationError is not null, _fixture.ConfigurationError);

        CreateTargetRecord(DeactivateId, "Deactivate");
        var source = new InMemorySourceClient()
            .AddRecord(SourceRecord(DeactivateId, "Deactivate", StateInactive, StatusInactive));

        var result = await MigrateAsync(source, manageState: true);

        AssertNoErrors(result);
        Assert.Equal(1, result.StateChangesCount);
        AssertTargetState(DeactivateId, StateInactive, StatusInactive);
    }

    [SkippableFact]
    public async Task Migrate_ActiveInSource_ReactivatesTargetAsync()
    {
        Skip.If(_fixture.ConfigurationError is not null, _fixture.ConfigurationError);

        CreateTargetRecord(ReactivateId, "Reactivate", inactive: true);
        var source = new InMemorySourceClient()
            .AddRecord(SourceRecord(ReactivateId, "Reactivate", StateActive, StatusActive));

        var result = await MigrateAsync(source, manageState: true);

        AssertNoErrors(result);
        Assert.Equal(1, result.StateChangesCount);
        AssertTargetState(ReactivateId, StateActive, StatusActive);
    }

    [SkippableFact]
    public async Task Migrate_NewInactiveRecord_CreatesThenDeactivatesAsync()
    {
        Skip.If(_fixture.ConfigurationError is not null, _fixture.ConfigurationError);

        var source = new InMemorySourceClient()
            .AddRecord(SourceRecord(NewInactiveId, "NewInactive", StateInactive, StatusInactive, LookupTargetRef()));

        var result = await MigrateAsync(source, manageState: true);

        AssertNoErrors(result);
        Assert.Equal(1, result.UpsertedCount);
        Assert.Equal(1, result.StateChangesCount);
        var target = RetrieveTarget(NewInactiveId);
        Assert.Equal(NamePrefix + "NewInactive", target.GetAttributeValue<string>("xrt_name"));
        Assert.Equal(_lookupTargetId, target.GetAttributeValue<EntityReference>(LookupField)?.Id);
        AssertTargetState(NewInactiveId, StateInactive, StatusInactive);
    }

    // -------------------------------------------------------------------------
    // Diff mode — a second run against an already-migrated target is a no-op
    // -------------------------------------------------------------------------

    [SkippableFact]
    public async Task Migrate_SecondRun_ReportsRecordUnchangedAsync()
    {
        Skip.If(_fixture.ConfigurationError is not null, _fixture.ConfigurationError);

        CreateTargetRecord(RerunId, "Rerun", lookup: LookupTargetRef());
        var source = new InMemorySourceClient()
            .AddRecord(SourceRecord(RerunId, "Rerun-Renamed", StateInactive, StatusInactive));

        var first = await MigrateAsync(source, manageState: true);
        AssertNoErrors(first);

        var second = await MigrateAsync(source, manageState: true);

        AssertNoErrors(second);
        Assert.Equal(1, second.SkippedCount);
        Assert.Equal(0, second.UpsertedCount);
        Assert.Equal(0, second.LookupsPatchedCount);
        Assert.Equal(0, second.StateChangesCount);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private async Task<TableMigrationResult> MigrateAsync(InMemorySourceClient source, bool manageState)
    {
        // IncludeFields scopes the run to the columns these tests control. The hand-built source records
        // don't carry every column Dataverse defaults on create, and since #114 diff mode would clear any
        // target-only value — correct for a real source, but noise here.
        var config = new RefDataMigrateConfig
        {
            Tables = new List<MigrateTableConfig>
            {
                new()
                {
                    LogicalName = TestEntity,
                    ManageState = manageState,
                    IncludeFields = new List<string> { "xrt_name", LookupField }
                }
            }
        };

        var migrator = new RefDataMigrator(new ConsoleLogger(), source, _fixture.Client);
        var summary = await migrator.MigrateAsync(config, dryRun: false, force: false);
        return Assert.Single(summary.TableResults);
    }

    private static Entity SourceRecord(Guid id, string name, int? state = null, int? status = null, EntityReference? lookup = null)
    {
        var record = new Entity(TestEntity, id)
        {
            ["xrt_name"] = NamePrefix + name,
            [LookupField] = lookup
        };

        if (state.HasValue)
        {
            record["statecode"] = new OptionSetValue(state.Value);
        }
        if (status.HasValue)
        {
            record["statuscode"] = new OptionSetValue(status.Value);
        }

        return record;
    }

    private void CreateTargetRecord(Guid id, string name, EntityReference? lookup = null, bool inactive = false)
    {
        var record = new Entity(TestEntity, id)
        {
            ["xrt_name"] = NamePrefix + name
        };
        if (lookup is not null)
        {
            record[LookupField] = lookup;
        }
        _fixture.Client.Execute(new CreateRequest { Target = record });

        // Records can't be created inactive, so deactivate as a separate step
        if (inactive)
        {
            _fixture.Client.Execute(new UpdateRequest
            {
                Target = new Entity(TestEntity, id)
                {
                    ["statecode"] = new OptionSetValue(StateInactive),
                    ["statuscode"] = new OptionSetValue(StatusInactive)
                }
            });
        }
    }

    private Entity RetrieveTarget(Guid id)
    {
        var response = (RetrieveResponse)_fixture.Client.Execute(new RetrieveRequest
        {
            Target = new EntityReference(TestEntity, id),
            ColumnSet = new ColumnSet("xrt_name", LookupField, "statecode", "statuscode")
        });
        return response.Entity;
    }

    private void AssertTargetState(Guid id, int expectedState, int expectedStatus)
    {
        var target = RetrieveTarget(id);
        Assert.Equal(expectedState, target.GetAttributeValue<OptionSetValue>("statecode").Value);
        Assert.Equal(expectedStatus, target.GetAttributeValue<OptionSetValue>("statuscode").Value);
    }

    private static void AssertNoErrors(TableMigrationResult result)
    {
        Assert.True(result.Errors.Count == 0,
            "Migration reported errors: " + string.Join("; ", result.Errors.Select(e => $"[{e.Phase}] {e.ErrorMessage}")));
    }

    private static EntityReference LookupTargetRef() => new(LookupTargetEntity, _lookupTargetId);

    private void DeleteTestRecords()
    {
        foreach (var id in AllTestIds)
        {
            try
            {
                _fixture.Client.Execute(new DeleteRequest { Target = new EntityReference(TestEntity, id) });
            }
            catch (System.ServiceModel.FaultException<OrganizationServiceFault>)
            {
                // Not present — nothing to clean up
            }
        }
    }
}
