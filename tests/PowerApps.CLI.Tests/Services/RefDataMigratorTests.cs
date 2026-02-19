using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Moq;
using PowerApps.CLI.Infrastructure;
using PowerApps.CLI.Models;
using PowerApps.CLI.Services;
using Xunit;

namespace PowerApps.CLI.Tests.Services;

public class RefDataMigratorTests
{
    private readonly Mock<IConsoleLogger> _mockLogger;
    private readonly Mock<IDataverseClient> _mockSourceClient;
    private readonly Mock<IDataverseClient> _mockTargetClient;
    private readonly RefDataMigrator _migrator;

    public RefDataMigratorTests()
    {
        _mockLogger = new Mock<IConsoleLogger>();
        _mockSourceClient = new Mock<IDataverseClient>();
        _mockTargetClient = new Mock<IDataverseClient>();

        _mockSourceClient.Setup(c => c.GetEnvironmentUrl()).Returns("https://source.crm.dynamics.com");
        _mockTargetClient.Setup(c => c.GetEnvironmentUrl()).Returns("https://target.crm.dynamics.com");

        _migrator = new RefDataMigrator(_mockLogger.Object, _mockSourceClient.Object, _mockTargetClient.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new RefDataMigrator(null!, _mockSourceClient.Object, _mockTargetClient.Object));
        Assert.Equal("logger", ex.ParamName);
    }

    [Fact]
    public void Constructor_WithNullSourceClient_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new RefDataMigrator(_mockLogger.Object, null!, _mockTargetClient.Object));
        Assert.Equal("sourceClient", ex.ParamName);
    }

    [Fact]
    public void Constructor_WithNullTargetClient_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new RefDataMigrator(_mockLogger.Object, _mockSourceClient.Object, null!));
        Assert.Equal("targetClient", ex.ParamName);
    }

    #endregion

    #region Pass 1 - Flat Data Upsert Tests

    [Fact]
    public async Task MigrateAsync_Pass1_UpsertsOnlyFlatDataStrippingLookups()
    {
        // Arrange
        var config = CreateConfig("account");
        var metadata = CreateMetadata("account", "accountid", "name", "industrycode", "parentaccountid");
        SetupMetadata("account", metadata);

        var record = new Entity("account", Guid.NewGuid());
        record["name"] = "Test Account";
        record["industrycode"] = new OptionSetValue(1);
        record["parentaccountid"] = new EntityReference("account", Guid.NewGuid());
        SetupSourceRecords("account", record);

        var executeMultipleResponse = CreateSuccessResponse(1);
        SetupExecuteMultiple(executeMultipleResponse);

        // Capture the first ExecuteMultiple call
        OrganizationRequestCollection? capturedRequests = null;
        _mockTargetClient.Setup(c => c.ExecuteMultiple(It.IsAny<OrganizationRequestCollection>(), It.IsAny<bool>()))
            .Callback<OrganizationRequestCollection, bool>((reqs, _) => capturedRequests ??= reqs)
            .Returns(executeMultipleResponse);

        // Act
        var result = await _migrator.MigrateAsync(config, dryRun: false, force: true);

        // Assert - Pass 1 should contain flat data only (no EntityReference)
        Assert.NotNull(capturedRequests);
        Assert.Single(capturedRequests);
        var upsert = Assert.IsType<UpsertRequest>(capturedRequests[0]);
        Assert.True(upsert.Target.Contains("name"));
        Assert.True(upsert.Target.Contains("industrycode"));
        Assert.False(upsert.Target.Contains("parentaccountid"));
    }

    [Fact]
    public async Task MigrateAsync_Pass1_SetsUpsertedCountCorrectly()
    {
        // Arrange
        var config = CreateConfig("account");
        var metadata = CreateMetadata("account", "accountid", "name");
        SetupMetadata("account", metadata);

        var record1 = new Entity("account", Guid.NewGuid());
        record1["name"] = "Account 1";
        var record2 = new Entity("account", Guid.NewGuid());
        record2["name"] = "Account 2";
        SetupSourceRecords("account", record1, record2);

        SetupExecuteMultiple(CreateSuccessResponse(2));

        // Act
        var result = await _migrator.MigrateAsync(config, dryRun: false, force: true);

        // Assert
        Assert.Equal(2, result.TableResults[0].UpsertedCount);
        Assert.Equal(2, result.TableResults[0].RecordCount);
    }

    #endregion

    #region Pass 2 - Lookup Patching Tests

    [Fact]
    public async Task MigrateAsync_Pass2_PatchesLookupsInSecondPass()
    {
        // Arrange
        var config = CreateConfig("account");
        var metadata = CreateMetadata("account", "accountid", "name", "parentaccountid");
        SetupMetadata("account", metadata);

        var parentRef = new EntityReference("account", Guid.NewGuid());
        var record = new Entity("account", Guid.NewGuid());
        record["name"] = "Child Account";
        record["parentaccountid"] = parentRef;
        SetupSourceRecords("account", record);

        SetupExecuteMultiple(CreateSuccessResponse(1));

        // Act
        var result = await _migrator.MigrateAsync(config, dryRun: false, force: true);

        // Assert - Two ExecuteMultiple calls: pass 1 (flat) and pass 2 (lookups)
        _mockTargetClient.Verify(c => c.ExecuteMultiple(It.IsAny<OrganizationRequestCollection>(), true), Times.Exactly(2));
        Assert.Equal(1, result.TableResults[0].LookupsPatchedCount);
    }

    [Fact]
    public async Task MigrateAsync_Pass2_SkippedWhenNoLookups()
    {
        // Arrange
        var config = CreateConfig("account");
        var metadata = CreateMetadata("account", "accountid", "name");
        SetupMetadata("account", metadata);

        var record = new Entity("account", Guid.NewGuid());
        record["name"] = "Simple Account";
        SetupSourceRecords("account", record);

        SetupExecuteMultiple(CreateSuccessResponse(1));

        // Act
        var result = await _migrator.MigrateAsync(config, dryRun: false, force: true);

        // Assert - Only one ExecuteMultiple call (pass 1 only)
        _mockTargetClient.Verify(c => c.ExecuteMultiple(It.IsAny<OrganizationRequestCollection>(), true), Times.Once);
        Assert.Equal(0, result.TableResults[0].LookupsPatchedCount);
    }

    #endregion

    #region Pass 3 - State Management Tests

    [Fact]
    public async Task MigrateAsync_Pass3_SetsStateWhenManageStateEnabled()
    {
        // Arrange
        var config = CreateConfig("account", manageState: true);
        var metadata = CreateMetadata("account", "accountid", "name");
        SetupMetadata("account", metadata);

        var record = new Entity("account", Guid.NewGuid());
        record["name"] = "Inactive Account";
        record["statecode"] = new OptionSetValue(1); // Inactive
        record["statuscode"] = new OptionSetValue(2);
        SetupSourceRecords("account", record);

        SetupExecuteMultiple(CreateSuccessResponse(1));

        // Act
        var result = await _migrator.MigrateAsync(config, dryRun: false, force: true);

        // Assert - Pass 1 (upsert) + Pass 3 (state) = 2 calls
        _mockTargetClient.Verify(c => c.ExecuteMultiple(It.IsAny<OrganizationRequestCollection>(), true), Times.Exactly(2));
        Assert.Equal(1, result.TableResults[0].StateChangesCount);
    }

    [Fact]
    public async Task MigrateAsync_Pass3_SkippedWhenManageStateDisabled()
    {
        // Arrange
        var config = CreateConfig("account", manageState: false);
        var metadata = CreateMetadata("account", "accountid", "name");
        SetupMetadata("account", metadata);

        var record = new Entity("account", Guid.NewGuid());
        record["name"] = "Inactive Account";
        record["statecode"] = new OptionSetValue(1);
        record["statuscode"] = new OptionSetValue(2);
        SetupSourceRecords("account", record);

        SetupExecuteMultiple(CreateSuccessResponse(1));

        // Act
        var result = await _migrator.MigrateAsync(config, dryRun: false, force: true);

        // Assert - Only pass 1 (no state change)
        _mockTargetClient.Verify(c => c.ExecuteMultiple(It.IsAny<OrganizationRequestCollection>(), true), Times.Once);
        Assert.Equal(0, result.TableResults[0].StateChangesCount);
    }

    [Fact]
    public async Task MigrateAsync_Pass3_SkipsRecordsWithActiveState()
    {
        // Arrange
        var config = CreateConfig("account", manageState: true);
        var metadata = CreateMetadata("account", "accountid", "name");
        SetupMetadata("account", metadata);

        var record = new Entity("account", Guid.NewGuid());
        record["name"] = "Active Account";
        record["statecode"] = new OptionSetValue(0); // Active - no state change needed
        record["statuscode"] = new OptionSetValue(1);
        SetupSourceRecords("account", record);

        SetupExecuteMultiple(CreateSuccessResponse(1));

        // Act
        var result = await _migrator.MigrateAsync(config, dryRun: false, force: true);

        // Assert - Only pass 1 (no state change since statecode is 0)
        _mockTargetClient.Verify(c => c.ExecuteMultiple(It.IsAny<OrganizationRequestCollection>(), true), Times.Once);
        Assert.Equal(0, result.TableResults[0].StateChangesCount);
    }

    #endregion

    #region Dry Run Tests

    [Fact]
    public async Task MigrateAsync_DryRun_DoesNotCallExecuteMultiple()
    {
        // Arrange
        var config = CreateConfig("account");
        var metadata = CreateMetadata("account", "accountid", "name", "parentaccountid");
        SetupMetadata("account", metadata);

        var record = new Entity("account", Guid.NewGuid());
        record["name"] = "Test";
        record["parentaccountid"] = new EntityReference("account", Guid.NewGuid());
        record["statecode"] = new OptionSetValue(1);
        record["statuscode"] = new OptionSetValue(2);
        SetupSourceRecords("account", record);

        // Act
        var result = await _migrator.MigrateAsync(config, dryRun: true, force: true);

        // Assert
        _mockTargetClient.Verify(c => c.ExecuteMultiple(It.IsAny<OrganizationRequestCollection>(), It.IsAny<bool>()), Times.Never);
        Assert.Equal(1, result.TableResults[0].UpsertedCount);
        Assert.Equal(1, result.TableResults[0].LookupsPatchedCount);
        Assert.True(result.IsDryRun);
    }

    #endregion

    #region Batch Size Tests

    [Fact]
    public async Task MigrateAsync_RespectsBatchSize()
    {
        // Arrange - 3 records with batch size of 2 = 2 batches
        var config = new RefDataMigrateConfig
        {
            BatchSize = 2,
            Tables = new List<MigrateTableConfig> { new() { LogicalName = "account" } }
        };
        var metadata = CreateMetadata("account", "accountid", "name");
        SetupMetadata("account", metadata);

        var records = new List<Entity>();
        for (int i = 0; i < 3; i++)
        {
            var record = new Entity("account", Guid.NewGuid());
            record["name"] = $"Account {i}";
            records.Add(record);
        }
        SetupSourceRecords("account", records.ToArray());

        SetupExecuteMultiple(CreateSuccessResponse(2));

        // Act
        var result = await _migrator.MigrateAsync(config, dryRun: false, force: true);

        // Assert - 2 batches for pass 1
        _mockTargetClient.Verify(c => c.ExecuteMultiple(It.IsAny<OrganizationRequestCollection>(), true), Times.Exactly(2));
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task MigrateAsync_CapturesFaultedResponses()
    {
        // Arrange
        var config = CreateConfig("account");
        var metadata = CreateMetadata("account", "accountid", "name");
        SetupMetadata("account", metadata);

        var record = new Entity("account", Guid.NewGuid());
        record["name"] = "Bad Account";
        SetupSourceRecords("account", record);

        var faultedResponse = CreateFaultedResponse(0, "Duplicate key error");
        SetupExecuteMultiple(faultedResponse);

        // Act
        var result = await _migrator.MigrateAsync(config, dryRun: false, force: true);

        // Assert
        Assert.Single(result.TableResults[0].Errors);
        Assert.Equal("Duplicate key error", result.TableResults[0].Errors[0].ErrorMessage);
        Assert.Equal(record.Id, result.TableResults[0].Errors[0].RecordId);
        Assert.True(result.HasErrors);
    }

    #endregion

    #region Column Filtering Tests

    [Fact]
    public async Task MigrateAsync_ExcludesSystemFields()
    {
        // Arrange
        var config = CreateConfig("account");
        var metadata = CreateMetadata("account", "accountid", "name", "createdby", "modifiedon");
        SetupMetadata("account", metadata);

        var record = new Entity("account", Guid.NewGuid());
        record["name"] = "Test";
        record["createdby"] = new EntityReference("systemuser", Guid.NewGuid());
        record["modifiedon"] = DateTime.UtcNow;
        SetupSourceRecords("account", record);

        OrganizationRequestCollection? captured = null;
        _mockTargetClient.Setup(c => c.ExecuteMultiple(It.IsAny<OrganizationRequestCollection>(), It.IsAny<bool>()))
            .Callback<OrganizationRequestCollection, bool>((reqs, _) => captured ??= reqs)
            .Returns(CreateSuccessResponse(1));

        // Act
        await _migrator.MigrateAsync(config, dryRun: false, force: true);

        // Assert - Only name should be in the upsert, not system fields
        Assert.NotNull(captured);
        var upsert = Assert.IsType<UpsertRequest>(captured[0]);
        Assert.True(upsert.Target.Contains("name"));
        Assert.False(upsert.Target.Contains("createdby"));
        Assert.False(upsert.Target.Contains("modifiedon"));
    }

    [Fact]
    public async Task MigrateAsync_ExcludesUserSpecifiedColumns()
    {
        // Arrange
        var config = new RefDataMigrateConfig
        {
            Tables = new List<MigrateTableConfig>
            {
                new() { LogicalName = "account", ExcludeFields = new List<string> { "description" } }
            }
        };
        var metadata = CreateMetadata("account", "accountid", "name", "description");
        SetupMetadata("account", metadata);

        var record = new Entity("account", Guid.NewGuid());
        record["name"] = "Test";
        record["description"] = "Should be excluded";
        SetupSourceRecords("account", record);

        OrganizationRequestCollection? captured = null;
        _mockTargetClient.Setup(c => c.ExecuteMultiple(It.IsAny<OrganizationRequestCollection>(), It.IsAny<bool>()))
            .Callback<OrganizationRequestCollection, bool>((reqs, _) => captured ??= reqs)
            .Returns(CreateSuccessResponse(1));

        // Act
        await _migrator.MigrateAsync(config, dryRun: false, force: true);

        // Assert
        Assert.NotNull(captured);
        var upsert = Assert.IsType<UpsertRequest>(captured[0]);
        Assert.True(upsert.Target.Contains("name"));
        Assert.False(upsert.Target.Contains("description"));
    }

    [Fact]
    public async Task MigrateAsync_ExcludesNonWritableColumns()
    {
        // Arrange
        var config = CreateConfig("account");

        // Create metadata where "computedfield" is not writable
        var metadata = CreateEntityMetadata("account", "accountid",
            CreateAttributeMetadata("name", true, true),
            CreateAttributeMetadata("computedfield", false, false));
        SetupMetadata("account", metadata);

        var record = new Entity("account", Guid.NewGuid());
        record["name"] = "Test";
        record["computedfield"] = "Computed";
        SetupSourceRecords("account", record);

        OrganizationRequestCollection? captured = null;
        _mockTargetClient.Setup(c => c.ExecuteMultiple(It.IsAny<OrganizationRequestCollection>(), It.IsAny<bool>()))
            .Callback<OrganizationRequestCollection, bool>((reqs, _) => captured ??= reqs)
            .Returns(CreateSuccessResponse(1));

        // Act
        await _migrator.MigrateAsync(config, dryRun: false, force: true);

        // Assert
        Assert.NotNull(captured);
        var upsert = Assert.IsType<UpsertRequest>(captured[0]);
        Assert.True(upsert.Target.Contains("name"));
        Assert.False(upsert.Target.Contains("computedfield"));
    }

    [Fact]
    public async Task MigrateAsync_IncludeFields_OnlySyncsSpecifiedFields()
    {
        // Arrange
        var config = new RefDataMigrateConfig
        {
            Tables = new List<MigrateTableConfig>
            {
                new() { LogicalName = "team", IncludeFields = new List<string> { "name", "description" } }
            }
        };
        var metadata = CreateMetadata("team", "teamid", "name", "description", "emailaddress", "organizationid");
        SetupMetadata("team", metadata);

        var record = new Entity("team", Guid.NewGuid());
        record["name"] = "Test Team";
        record["description"] = "A team";
        record["emailaddress"] = "team@test.com";
        record["organizationid"] = new EntityReference("organization", Guid.NewGuid());
        SetupSourceRecords("team", record);

        OrganizationRequestCollection? captured = null;
        _mockTargetClient.Setup(c => c.ExecuteMultiple(It.IsAny<OrganizationRequestCollection>(), It.IsAny<bool>()))
            .Callback<OrganizationRequestCollection, bool>((reqs, _) => captured ??= reqs)
            .Returns(CreateSuccessResponse(1));

        // Act
        await _migrator.MigrateAsync(config, dryRun: false, force: true);

        // Assert - Only name and description should be included
        Assert.NotNull(captured);
        var upsert = Assert.IsType<UpsertRequest>(captured[0]);
        Assert.True(upsert.Target.Contains("name"));
        Assert.True(upsert.Target.Contains("description"));
        Assert.False(upsert.Target.Contains("emailaddress"));
        Assert.False(upsert.Target.Contains("organizationid"));
    }

    [Fact]
    public async Task MigrateAsync_IncludeFields_StillExcludesSystemFields()
    {
        // Arrange - include a system field in includeColumns, it should still be excluded
        var config = new RefDataMigrateConfig
        {
            Tables = new List<MigrateTableConfig>
            {
                new() { LogicalName = "account", IncludeFields = new List<string> { "name", "createdby" } }
            }
        };
        var metadata = CreateMetadata("account", "accountid", "name", "createdby");
        SetupMetadata("account", metadata);

        var record = new Entity("account", Guid.NewGuid());
        record["name"] = "Test";
        record["createdby"] = new EntityReference("systemuser", Guid.NewGuid());
        SetupSourceRecords("account", record);

        OrganizationRequestCollection? captured = null;
        _mockTargetClient.Setup(c => c.ExecuteMultiple(It.IsAny<OrganizationRequestCollection>(), It.IsAny<bool>()))
            .Callback<OrganizationRequestCollection, bool>((reqs, _) => captured ??= reqs)
            .Returns(CreateSuccessResponse(1));

        // Act
        await _migrator.MigrateAsync(config, dryRun: false, force: true);

        // Assert - createdby is a system field so still excluded even if in includeColumns
        Assert.NotNull(captured);
        var upsert = Assert.IsType<UpsertRequest>(captured[0]);
        Assert.True(upsert.Target.Contains("name"));
        Assert.False(upsert.Target.Contains("createdby"));
    }

    [Fact]
    public async Task MigrateAsync_EmptyIncludeFields_SyncsAllWritableFields()
    {
        // Arrange - empty includeColumns means sync everything (existing behaviour)
        var config = new RefDataMigrateConfig
        {
            Tables = new List<MigrateTableConfig>
            {
                new() { LogicalName = "account", IncludeFields = new List<string>() }
            }
        };
        var metadata = CreateMetadata("account", "accountid", "name", "description");
        SetupMetadata("account", metadata);

        var record = new Entity("account", Guid.NewGuid());
        record["name"] = "Test";
        record["description"] = "A description";
        SetupSourceRecords("account", record);

        OrganizationRequestCollection? captured = null;
        _mockTargetClient.Setup(c => c.ExecuteMultiple(It.IsAny<OrganizationRequestCollection>(), It.IsAny<bool>()))
            .Callback<OrganizationRequestCollection, bool>((reqs, _) => captured ??= reqs)
            .Returns(CreateSuccessResponse(1));

        // Act
        await _migrator.MigrateAsync(config, dryRun: false, force: true);

        // Assert - both fields should be included
        Assert.NotNull(captured);
        var upsert = Assert.IsType<UpsertRequest>(captured[0]);
        Assert.True(upsert.Target.Contains("name"));
        Assert.True(upsert.Target.Contains("description"));
    }

    #endregion

    #region Empty Table Tests

    [Fact]
    public async Task MigrateAsync_EmptyTable_ReturnsZeroCounts()
    {
        // Arrange
        var config = CreateConfig("account");
        var metadata = CreateMetadata("account", "accountid", "name");
        SetupMetadata("account", metadata);
        SetupSourceRecords("account");

        // Act
        var result = await _migrator.MigrateAsync(config, dryRun: false, force: true);

        // Assert
        Assert.Equal(0, result.TableResults[0].RecordCount);
        Assert.Equal(0, result.TableResults[0].UpsertedCount);
        _mockTargetClient.Verify(c => c.ExecuteMultiple(It.IsAny<OrganizationRequestCollection>(), It.IsAny<bool>()), Times.Never);
    }

    #endregion

    #region Summary Tests

    [Fact]
    public async Task MigrateAsync_PopulatesSummaryCorrectly()
    {
        // Arrange
        var config = CreateConfig("account");
        var metadata = CreateMetadata("account", "accountid", "name");
        SetupMetadata("account", metadata);

        var record = new Entity("account", Guid.NewGuid());
        record["name"] = "Test";
        SetupSourceRecords("account", record);
        SetupExecuteMultiple(CreateSuccessResponse(1));

        // Act
        var result = await _migrator.MigrateAsync(config, dryRun: false, force: true);

        // Assert
        Assert.Equal("https://source.crm.dynamics.com", result.SourceEnvironment);
        Assert.Equal("https://target.crm.dynamics.com", result.TargetEnvironment);
        Assert.False(result.IsDryRun);
        Assert.Single(result.TableResults);
        Assert.True(result.Duration.TotalMilliseconds >= 0);
    }

    #endregion

    #region N:N Relationship Tests

    [Fact]
    public async Task MigrateAsync_Pass4_AssociatesMissingRelationships()
    {
        // Arrange
        var config = new RefDataMigrateConfig
        {
            Tables = new List<MigrateTableConfig>(),
            Relationships = new List<ManyToManyConfig>
            {
                new() { RelationshipName = "entity1_entity2" }
            }
        };

        var relMetadata = CreateManyToManyMetadata("entity1_entity2", "entity1_entity2", "entity1", "entity1id", "entity2", "entity2id");
        _mockSourceClient.Setup(c => c.GetManyToManyRelationshipMetadata("entity1_entity2")).Returns(relMetadata);

        // Source has 2 associations
        var pair1Id1 = Guid.NewGuid();
        var pair1Id2 = Guid.NewGuid();
        var pair2Id1 = Guid.NewGuid();
        var pair2Id2 = Guid.NewGuid();

        var sourceRecord1 = new Entity("entity1_entity2");
        sourceRecord1["entity1id"] = pair1Id1;
        sourceRecord1["entity2id"] = pair1Id2;
        var sourceRecord2 = new Entity("entity1_entity2");
        sourceRecord2["entity1id"] = pair2Id1;
        sourceRecord2["entity2id"] = pair2Id2;

        _mockSourceClient.Setup(c => c.RetrieveRecordsByFetchXml(It.IsAny<string>()))
            .Returns(new EntityCollection(new List<Entity> { sourceRecord1, sourceRecord2 }));

        // Target has 0 existing
        _mockTargetClient.Setup(c => c.RetrieveRecordsByFetchXml(It.IsAny<string>()))
            .Returns(new EntityCollection());

        SetupExecuteMultiple(CreateSuccessResponse(2));

        // Act
        var result = await _migrator.MigrateAsync(config, dryRun: false, force: true);

        // Assert
        Assert.Single(result.ManyToManyResults);
        Assert.Equal(2, result.ManyToManyResults[0].SourceCount);
        Assert.Equal(0, result.ManyToManyResults[0].TargetExistingCount);
        Assert.Equal(2, result.ManyToManyResults[0].AssociatedCount);
        Assert.Equal(0, result.ManyToManyResults[0].DisassociatedCount);
    }

    [Fact]
    public async Task MigrateAsync_Pass4_DisassociatesRemovedRelationships()
    {
        // Arrange
        var config = new RefDataMigrateConfig
        {
            Tables = new List<MigrateTableConfig>(),
            Relationships = new List<ManyToManyConfig>
            {
                new() { RelationshipName = "entity1_entity2" }
            }
        };

        var relMetadata = CreateManyToManyMetadata("entity1_entity2", "entity1_entity2", "entity1", "entity1id", "entity2", "entity2id");
        _mockSourceClient.Setup(c => c.GetManyToManyRelationshipMetadata("entity1_entity2")).Returns(relMetadata);

        // Source has 0 associations
        _mockSourceClient.Setup(c => c.RetrieveRecordsByFetchXml(It.IsAny<string>()))
            .Returns(new EntityCollection());

        // Target has 1 existing that should be removed
        var targetRecord = new Entity("entity1_entity2");
        targetRecord["entity1id"] = Guid.NewGuid();
        targetRecord["entity2id"] = Guid.NewGuid();

        _mockTargetClient.Setup(c => c.RetrieveRecordsByFetchXml(It.IsAny<string>()))
            .Returns(new EntityCollection(new List<Entity> { targetRecord }));

        SetupExecuteMultiple(CreateSuccessResponse(1));

        // Act
        var result = await _migrator.MigrateAsync(config, dryRun: false, force: true);

        // Assert
        Assert.Single(result.ManyToManyResults);
        Assert.Equal(0, result.ManyToManyResults[0].AssociatedCount);
        Assert.Equal(1, result.ManyToManyResults[0].DisassociatedCount);
    }

    [Fact]
    public async Task MigrateAsync_Pass4_SkipsExistingAssociations()
    {
        // Arrange
        var config = new RefDataMigrateConfig
        {
            Tables = new List<MigrateTableConfig>(),
            Relationships = new List<ManyToManyConfig>
            {
                new() { RelationshipName = "entity1_entity2" }
            }
        };

        var relMetadata = CreateManyToManyMetadata("entity1_entity2", "entity1_entity2", "entity1", "entity1id", "entity2", "entity2id");
        _mockSourceClient.Setup(c => c.GetManyToManyRelationshipMetadata("entity1_entity2")).Returns(relMetadata);

        // Same association exists in both source and target
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        var record = new Entity("entity1_entity2");
        record["entity1id"] = id1;
        record["entity2id"] = id2;

        _mockSourceClient.Setup(c => c.RetrieveRecordsByFetchXml(It.IsAny<string>()))
            .Returns(new EntityCollection(new List<Entity> { record }));
        _mockTargetClient.Setup(c => c.RetrieveRecordsByFetchXml(It.IsAny<string>()))
            .Returns(new EntityCollection(new List<Entity> { record }));

        // Act
        var result = await _migrator.MigrateAsync(config, dryRun: false, force: true);

        // Assert - no ExecuteMultiple calls for associations (already in sync)
        Assert.Equal(0, result.ManyToManyResults[0].AssociatedCount);
        Assert.Equal(0, result.ManyToManyResults[0].DisassociatedCount);
        _mockTargetClient.Verify(c => c.ExecuteMultiple(It.IsAny<OrganizationRequestCollection>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task MigrateAsync_Pass4_DryRunDoesNotExecute()
    {
        // Arrange
        var config = new RefDataMigrateConfig
        {
            Tables = new List<MigrateTableConfig>(),
            Relationships = new List<ManyToManyConfig>
            {
                new() { RelationshipName = "entity1_entity2" }
            }
        };

        var relMetadata = CreateManyToManyMetadata("entity1_entity2", "entity1_entity2", "entity1", "entity1id", "entity2", "entity2id");
        _mockSourceClient.Setup(c => c.GetManyToManyRelationshipMetadata("entity1_entity2")).Returns(relMetadata);

        var sourceRecord = new Entity("entity1_entity2");
        sourceRecord["entity1id"] = Guid.NewGuid();
        sourceRecord["entity2id"] = Guid.NewGuid();

        _mockSourceClient.Setup(c => c.RetrieveRecordsByFetchXml(It.IsAny<string>()))
            .Returns(new EntityCollection(new List<Entity> { sourceRecord }));
        _mockTargetClient.Setup(c => c.RetrieveRecordsByFetchXml(It.IsAny<string>()))
            .Returns(new EntityCollection());

        // Act
        var result = await _migrator.MigrateAsync(config, dryRun: true, force: true);

        // Assert
        Assert.Equal(1, result.ManyToManyResults[0].AssociatedCount);
        _mockTargetClient.Verify(c => c.ExecuteMultiple(It.IsAny<OrganizationRequestCollection>(), It.IsAny<bool>()), Times.Never);
    }

    #endregion

    #region Diff-Based Sync Tests

    [Fact]
    public async Task MigrateAsync_DiffMode_SkipsUnchangedRecords()
    {
        // Arrange
        var recordId = Guid.NewGuid();
        var metadata = CreateMetadata("account", "accountid", "name");
        SetupMetadata("account", metadata);

        var sourceRecord = new Entity("account", recordId);
        sourceRecord["name"] = "Test";
        SetupSourceRecords("account", sourceRecord);

        var targetRecord = new Entity("account", recordId);
        targetRecord["name"] = "Test";
        SetupTargetRecords("account", targetRecord);

        SetupExecuteMultiple(CreateSuccessResponse(0));
        var config = CreateConfig("account");

        // Act
        var result = await _migrator.MigrateAsync(config, dryRun: false, force: false);

        // Assert
        Assert.Equal(1, result.TableResults[0].RecordCount);
        Assert.Equal(1, result.TableResults[0].SkippedCount);
        _mockTargetClient.Verify(c => c.ExecuteMultiple(It.IsAny<OrganizationRequestCollection>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task MigrateAsync_DiffMode_IncludesNewRecords()
    {
        // Arrange
        var recordId = Guid.NewGuid();
        var metadata = CreateMetadata("account", "accountid", "name");
        SetupMetadata("account", metadata);

        var sourceRecord = new Entity("account", recordId);
        sourceRecord["name"] = "New Record";
        SetupSourceRecords("account", sourceRecord);

        // Target has no records
        SetupTargetRecords("account");

        SetupExecuteMultiple(CreateSuccessResponse(1));
        var config = CreateConfig("account");

        // Act
        var result = await _migrator.MigrateAsync(config, dryRun: false, force: false);

        // Assert
        Assert.Equal(0, result.TableResults[0].SkippedCount);
        Assert.Equal(1, result.TableResults[0].UpsertedCount);
    }

    [Fact]
    public async Task MigrateAsync_DiffMode_DetectsChangedFlatValues()
    {
        // Arrange
        var recordId = Guid.NewGuid();
        var metadata = CreateMetadata("account", "accountid", "name");
        SetupMetadata("account", metadata);

        var sourceRecord = new Entity("account", recordId);
        sourceRecord["name"] = "Updated Name";
        SetupSourceRecords("account", sourceRecord);

        var targetRecord = new Entity("account", recordId);
        targetRecord["name"] = "Old Name";
        SetupTargetRecords("account", targetRecord);

        SetupExecuteMultiple(CreateSuccessResponse(1));
        var config = CreateConfig("account");

        // Act
        var result = await _migrator.MigrateAsync(config, dryRun: false, force: false);

        // Assert
        Assert.Equal(0, result.TableResults[0].SkippedCount);
        Assert.Equal(1, result.TableResults[0].UpsertedCount);
    }

    [Fact]
    public async Task MigrateAsync_DiffMode_DetectsChangedLookupValues()
    {
        // Arrange
        var recordId = Guid.NewGuid();
        var metadata = CreateMetadata("account", "accountid", "name", "parentid");
        SetupMetadata("account", metadata);

        var sourceRecord = new Entity("account", recordId);
        sourceRecord["name"] = "Test";
        sourceRecord["parentid"] = new EntityReference("account", Guid.NewGuid());
        SetupSourceRecords("account", sourceRecord);

        var targetRecord = new Entity("account", recordId);
        targetRecord["name"] = "Test";
        targetRecord["parentid"] = new EntityReference("account", Guid.NewGuid());
        SetupTargetRecords("account", targetRecord);

        SetupExecuteMultiple(CreateSuccessResponse(1));
        var config = CreateConfig("account");

        // Act
        var result = await _migrator.MigrateAsync(config, dryRun: false, force: false);

        // Assert - flat unchanged so skipped from flat, but lookups changed so patched
        Assert.Equal(1, result.TableResults[0].LookupsPatchedCount);
    }

    [Fact]
    public async Task MigrateAsync_DiffMode_DetectsChangedState()
    {
        // Arrange
        var recordId = Guid.NewGuid();
        var metadata = CreateMetadata("account", "accountid", "name");
        SetupMetadata("account", metadata);

        var sourceRecord = new Entity("account", recordId);
        sourceRecord["name"] = "Test";
        sourceRecord["statecode"] = new OptionSetValue(1);
        sourceRecord["statuscode"] = new OptionSetValue(2);
        SetupSourceRecords("account", sourceRecord);

        var targetRecord = new Entity("account", recordId);
        targetRecord["name"] = "Test";
        targetRecord["statecode"] = new OptionSetValue(0);
        targetRecord["statuscode"] = new OptionSetValue(1);
        SetupTargetRecords("account", targetRecord);

        SetupExecuteMultiple(CreateSuccessResponse(1));
        var config = CreateConfig("account", manageState: true);

        // Act
        var result = await _migrator.MigrateAsync(config, dryRun: false, force: false);

        // Assert
        Assert.Equal(1, result.TableResults[0].StateChangesCount);
    }

    [Fact]
    public async Task MigrateAsync_DiffMode_SkipsMatchingState()
    {
        // Arrange
        var recordId = Guid.NewGuid();
        var metadata = CreateMetadata("account", "accountid", "name");
        SetupMetadata("account", metadata);

        var sourceRecord = new Entity("account", recordId);
        sourceRecord["name"] = "Test";
        sourceRecord["statecode"] = new OptionSetValue(1);
        sourceRecord["statuscode"] = new OptionSetValue(2);
        SetupSourceRecords("account", sourceRecord);

        var targetRecord = new Entity("account", recordId);
        targetRecord["name"] = "Test";
        targetRecord["statecode"] = new OptionSetValue(1);
        targetRecord["statuscode"] = new OptionSetValue(2);
        SetupTargetRecords("account", targetRecord);

        SetupExecuteMultiple(CreateSuccessResponse(0));
        var config = CreateConfig("account", manageState: true);

        // Act
        var result = await _migrator.MigrateAsync(config, dryRun: false, force: false);

        // Assert - state matches, so no SetState needed
        Assert.Equal(0, result.TableResults[0].StateChangesCount);
    }

    [Fact]
    public async Task MigrateAsync_ForceMode_IncludesAllRecordsEvenIfUnchanged()
    {
        // Arrange
        var recordId = Guid.NewGuid();
        var metadata = CreateMetadata("account", "accountid", "name");
        SetupMetadata("account", metadata);

        var sourceRecord = new Entity("account", recordId);
        sourceRecord["name"] = "Test";
        SetupSourceRecords("account", sourceRecord);

        // No target records set up (force mode doesn't retrieve them)
        SetupExecuteMultiple(CreateSuccessResponse(1));
        var config = CreateConfig("account");

        // Act
        var result = await _migrator.MigrateAsync(config, dryRun: false, force: true);

        // Assert
        Assert.Equal(0, result.TableResults[0].SkippedCount);
        Assert.Equal(1, result.TableResults[0].UpsertedCount);
        _mockTargetClient.Verify(c => c.RetrieveRecords("account", It.IsAny<string?>()), Times.Never);
    }

    #endregion

    #region AttributeValuesEqual Tests

    [Fact]
    public void AttributeValuesEqual_BothNull_ReturnsTrue()
    {
        Assert.True(RefDataMigrator.AttributeValuesEqual(null, null));
    }

    [Fact]
    public void AttributeValuesEqual_OneNull_ReturnsFalse()
    {
        Assert.False(RefDataMigrator.AttributeValuesEqual("test", null));
        Assert.False(RefDataMigrator.AttributeValuesEqual(null, "test"));
    }

    [Fact]
    public void AttributeValuesEqual_EntityReference_ComparesByIdOnly()
    {
        var id = Guid.NewGuid();
        var ref1 = new EntityReference("account", id) { Name = "Name A" };
        var ref2 = new EntityReference("account", id) { Name = "Name B" };
        var ref3 = new EntityReference("account", Guid.NewGuid());

        Assert.True(RefDataMigrator.AttributeValuesEqual(ref1, ref2));
        Assert.False(RefDataMigrator.AttributeValuesEqual(ref1, ref3));
    }

    [Fact]
    public void AttributeValuesEqual_OptionSetValue_ComparesByValue()
    {
        Assert.True(RefDataMigrator.AttributeValuesEqual(new OptionSetValue(1), new OptionSetValue(1)));
        Assert.False(RefDataMigrator.AttributeValuesEqual(new OptionSetValue(1), new OptionSetValue(2)));
    }

    [Fact]
    public void AttributeValuesEqual_Money_ComparesByValue()
    {
        Assert.True(RefDataMigrator.AttributeValuesEqual(new Money(100.00m), new Money(100.00m)));
        Assert.False(RefDataMigrator.AttributeValuesEqual(new Money(100.00m), new Money(200.00m)));
    }

    [Fact]
    public void AttributeValuesEqual_Strings_UsesEquals()
    {
        Assert.True(RefDataMigrator.AttributeValuesEqual("hello", "hello"));
        Assert.False(RefDataMigrator.AttributeValuesEqual("hello", "world"));
    }

    [Fact]
    public void AttributeValuesEqual_Integers_UsesEquals()
    {
        Assert.True(RefDataMigrator.AttributeValuesEqual(42, 42));
        Assert.False(RefDataMigrator.AttributeValuesEqual(42, 99));
    }

    #endregion

    #region Helper Methods

    private RefDataMigrateConfig CreateConfig(string tableName, bool manageState = false)
    {
        return new RefDataMigrateConfig
        {
            Tables = new List<MigrateTableConfig>
            {
                new() { LogicalName = tableName, ManageState = manageState }
            }
        };
    }

    private void SetupMetadata(string tableName, EntityMetadata metadata)
    {
        _mockTargetClient.Setup(c => c.GetEntityMetadata(tableName)).Returns(metadata);
    }

    private void SetupSourceRecords(string tableName, params Entity[] records)
    {
        var collection = new EntityCollection(records.ToList());
        _mockSourceClient.Setup(c => c.RetrieveRecords(tableName, It.IsAny<string?>())).Returns(collection);
    }

    private void SetupExecuteMultiple(ExecuteMultipleResponse response)
    {
        _mockTargetClient.Setup(c => c.ExecuteMultiple(It.IsAny<OrganizationRequestCollection>(), It.IsAny<bool>()))
            .Returns(response);
    }

    private EntityMetadata CreateMetadata(string entityName, string primaryKey, params string[] columnNames)
    {
        var attrs = columnNames.Select(c => CreateAttributeMetadata(c, true, true)).ToArray();
        return CreateEntityMetadata(entityName, primaryKey, attrs);
    }

    private EntityMetadata CreateEntityMetadata(string entityName, string primaryKey, params AttributeMetadata[] attributes)
    {
        var metadata = new EntityMetadata();

        // Set PrimaryIdAttribute via reflection (it's read-only)
        var primaryIdProp = typeof(EntityMetadata).GetProperty("PrimaryIdAttribute");
        primaryIdProp!.SetValue(metadata, primaryKey);

        // Set Attributes via reflection
        var attrField = typeof(EntityMetadata).GetField("_attributes",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (attrField != null)
        {
            attrField.SetValue(metadata, attributes);
        }
        else
        {
            // Try property setter via reflection
            var attrProp = typeof(EntityMetadata).GetProperty("Attributes");
            attrProp!.SetValue(metadata, attributes);
        }

        return metadata;
    }

    private AttributeMetadata CreateAttributeMetadata(string logicalName, bool isValidForCreate, bool isValidForUpdate)
    {
        var attr = new StringAttributeMetadata();

        var logicalNameProp = typeof(AttributeMetadata).GetProperty("LogicalName");
        logicalNameProp!.SetValue(attr, logicalName);

        var createProp = typeof(AttributeMetadata).GetProperty("IsValidForCreate");
        createProp!.SetValue(attr, (bool?)isValidForCreate);

        var updateProp = typeof(AttributeMetadata).GetProperty("IsValidForUpdate");
        updateProp!.SetValue(attr, (bool?)isValidForUpdate);

        return attr;
    }

    private ExecuteMultipleResponse CreateSuccessResponse(int count)
    {
        var response = new ExecuteMultipleResponse();

        // ExecuteMultipleResponse.Responses is read-only, set via Results
        var results = new ExecuteMultipleResponseItemCollection();
        response.Results["Responses"] = results;
        response.Results["IsFaulted"] = false;

        return response;
    }

    private ExecuteMultipleResponse CreateFaultedResponse(int faultIndex, string errorMessage)
    {
        var response = new ExecuteMultipleResponse();

        var results = new ExecuteMultipleResponseItemCollection();
        results.Add(new ExecuteMultipleResponseItem
        {
            RequestIndex = faultIndex,
            Fault = new OrganizationServiceFault { Message = errorMessage }
        });

        response.Results["Responses"] = results;
        response.Results["IsFaulted"] = true;

        return response;
    }

    private void SetupTargetRecords(string tableName, params Entity[] records)
    {
        var collection = new EntityCollection(records.ToList());
        _mockTargetClient.Setup(c => c.RetrieveRecords(tableName, It.IsAny<string?>())).Returns(collection);
    }

    private ManyToManyRelationshipMetadata CreateManyToManyMetadata(
        string schemaName, string intersectEntity,
        string entity1Name, string entity1Key,
        string entity2Name, string entity2Key)
    {
        var metadata = new ManyToManyRelationshipMetadata();

        typeof(ManyToManyRelationshipMetadata).GetProperty("SchemaName")!.SetValue(metadata, schemaName);
        typeof(ManyToManyRelationshipMetadata).GetProperty("IntersectEntityName")!.SetValue(metadata, intersectEntity);
        typeof(ManyToManyRelationshipMetadata).GetProperty("Entity1LogicalName")!.SetValue(metadata, entity1Name);
        typeof(ManyToManyRelationshipMetadata).GetProperty("Entity1IntersectAttribute")!.SetValue(metadata, entity1Key);
        typeof(ManyToManyRelationshipMetadata).GetProperty("Entity2LogicalName")!.SetValue(metadata, entity2Name);
        typeof(ManyToManyRelationshipMetadata).GetProperty("Entity2IntersectAttribute")!.SetValue(metadata, entity2Key);

        return metadata;
    }

    #endregion
}
