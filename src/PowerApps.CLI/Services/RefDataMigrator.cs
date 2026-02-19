using System.Diagnostics;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using PowerApps.CLI.Infrastructure;
using PowerApps.CLI.Models;

namespace PowerApps.CLI.Services;

public class RefDataMigrator : IRefDataMigrator
{
    private static readonly HashSet<string> SystemFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "createdby", "createdon", "createdonbehalfby",
        "modifiedby", "modifiedon", "modifiedonbehalfby",
        "ownerid", "owninguser", "owningteam", "owningbusinessunit",
        "versionnumber", "importsequencenumber", "overriddencreatedon",
        "timezoneruleversionnumber", "utcconversiontimezonecode"
    };

    private static readonly HashSet<string> StateFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "statecode", "statuscode"
    };

    private readonly IConsoleLogger _logger;
    private readonly IDataverseClient _sourceClient;
    private readonly IDataverseClient _targetClient;

    public RefDataMigrator(IConsoleLogger logger, IDataverseClient sourceClient, IDataverseClient targetClient)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _sourceClient = sourceClient ?? throw new ArgumentNullException(nameof(sourceClient));
        _targetClient = targetClient ?? throw new ArgumentNullException(nameof(targetClient));
    }

    public async Task<MigrationSummary> MigrateAsync(RefDataMigrateConfig config, bool dryRun, bool force = false)
    {
        var stopwatch = Stopwatch.StartNew();
        var summary = new MigrationSummary
        {
            SourceEnvironment = _sourceClient.GetEnvironmentUrl(),
            TargetEnvironment = _targetClient.GetEnvironmentUrl(),
            IsDryRun = dryRun
        };

        if (!force)
        {
            _logger.LogInfo("Diff mode enabled (use --force to push all records)");
        }

        // Prepare all tables: retrieve metadata, source records, classify columns
        var prepTimer = Stopwatch.StartNew();
        var preparedTables = new List<PreparedTable>();
        foreach (var tableConfig in config.Tables)
        {
            var prepared = await PrepareTableAsync(tableConfig, force);
            preparedTables.Add(prepared);
            summary.TableResults.Add(prepared.Result);
        }
        prepTimer.Stop();
        _logger.LogInfo($"\nPreparation completed in {prepTimer.Elapsed:mm\\:ss\\.fff}");

        // Pass 1: Upsert flat data for ALL tables
        _logger.LogInfo("");
        _logger.LogInfo("=== Pass 1: Upserting flat data ===");
        foreach (var table in preparedTables)
        {
            if (table.FlatEntities.Count == 0) continue;

            _logger.LogInfo($"  {table.TableName} ({table.FlatEntities.Count} records)...");
            var tableTimer = Stopwatch.StartNew();
            if (!dryRun)
            {
                var errors = ExecuteBatchUpserts(table.FlatEntities, config.BatchSize, table.TableName, "Upsert (flat)");
                table.Result.UpsertedCount = table.FlatEntities.Count - errors.Count;
                table.Result.Errors.AddRange(errors);
            }
            else
            {
                table.Result.UpsertedCount = table.FlatEntities.Count;
                _logger.LogInfoIfVerbose($"    [DRY RUN] Would upsert {table.FlatEntities.Count} records");
            }
            tableTimer.Stop();
            _logger.LogInfo($"    Completed in {tableTimer.Elapsed:mm\\:ss\\.fff}");
        }

        // Pass 2: Patch lookups for ALL tables
        var tablesWithLookups = preparedTables.Where(t => t.LookupEntities.Count > 0).ToList();
        if (tablesWithLookups.Count > 0)
        {
            _logger.LogInfo("");
            _logger.LogInfo("=== Pass 2: Patching lookups ===");
            foreach (var table in tablesWithLookups)
            {
                _logger.LogInfo($"  {table.TableName} ({table.LookupEntities.Count} records)...");
                var tableTimer = Stopwatch.StartNew();
                if (!dryRun)
                {
                    var errors = ExecuteBatchUpserts(table.LookupEntities, config.BatchSize, table.TableName, "Patch (lookups)");
                    table.Result.LookupsPatchedCount = table.LookupEntities.Count - errors.Count;
                    table.Result.Errors.AddRange(errors);
                }
                else
                {
                    table.Result.LookupsPatchedCount = table.LookupEntities.Count;
                    _logger.LogInfoIfVerbose($"    [DRY RUN] Would patch lookups for {table.LookupEntities.Count} records");
                }
                tableTimer.Stop();
                _logger.LogInfo($"    Completed in {tableTimer.Elapsed:mm\\:ss\\.fff}");
            }
        }

        // Pass 3: Set state for ALL tables that have it configured
        var tablesWithState = preparedTables.Where(t => t.StateRecords.Count > 0).ToList();
        if (tablesWithState.Count > 0)
        {
            _logger.LogInfo("");
            _logger.LogInfo("=== Pass 3: Setting state ===");
            foreach (var table in tablesWithState)
            {
                _logger.LogInfo($"  {table.TableName} ({table.StateRecords.Count} records)...");
                var tableTimer = Stopwatch.StartNew();
                if (!dryRun)
                {
                    var errors = ExecuteBatchStateChanges(table.StateRecords, table.TableName, config.BatchSize);
                    table.Result.StateChangesCount = table.StateRecords.Count - errors.Count;
                    table.Result.Errors.AddRange(errors);
                }
                else
                {
                    table.Result.StateChangesCount = table.StateRecords.Count;
                    _logger.LogInfoIfVerbose($"    [DRY RUN] Would set state for {table.StateRecords.Count} records");
                }
                tableTimer.Stop();
                _logger.LogInfo($"    Completed in {tableTimer.Elapsed:mm\\:ss\\.fff}");
            }
        }

        // Pass 4: Sync N:N relationships
        if (config.Relationships.Count > 0)
        {
            _logger.LogInfo("");
            _logger.LogInfo("=== Pass 4: Syncing N:N relationships ===");
            foreach (var m2mConfig in config.Relationships)
            {
                var m2mTimer = Stopwatch.StartNew();
                try
                {
                    var m2mResult = await SyncManyToManyAsync(m2mConfig, config.BatchSize, dryRun);
                    summary.ManyToManyResults.Add(m2mResult);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"  Error syncing {m2mConfig.RelationshipName}: {ex.Message}");
                    summary.ManyToManyResults.Add(new ManyToManyMigrationResult
                    {
                        RelationshipName = m2mConfig.RelationshipName,
                        Errors = { new RecordError
                        {
                            TableName = m2mConfig.RelationshipName,
                            Phase = "N:N Sync",
                            ErrorMessage = ex.Message
                        }}
                    });
                }
                m2mTimer.Stop();
                _logger.LogInfo($"    Completed in {m2mTimer.Elapsed:mm\\:ss\\.fff}");
            }
        }

        stopwatch.Stop();
        summary.Duration = stopwatch.Elapsed;
        return summary;
    }

    private async Task<PreparedTable> PrepareTableAsync(MigrateTableConfig tableConfig, bool force)
    {
        var tableName = tableConfig.LogicalName;
        var result = new TableMigrationResult { TableName = tableName };

        _logger.LogInfo($"Preparing table: {tableName}");

        // Retrieve metadata from target to discover writable columns and primary key
        _logger.LogInfoIfVerbose($"  Retrieving metadata for {tableName}...");
        var metadata = _targetClient.GetEntityMetadata(tableName);
        var primaryKeyField = metadata.PrimaryIdAttribute;
        var writableColumns = GetWritableColumns(metadata, tableConfig.ExcludeFields, tableConfig.IncludeFields);

        _logger.LogInfoIfVerbose($"  Primary key: {primaryKeyField}, Writable columns: {writableColumns.Count}");

        // Retrieve source records
        _logger.LogInfoIfVerbose($"  Retrieving source records...");
        string? fetchXml = null;
        if (!string.IsNullOrWhiteSpace(tableConfig.Filter))
        {
            fetchXml = $@"<fetch><entity name='{tableName}'>{tableConfig.Filter}</entity></fetch>";
        }
        var sourceRecords = await Task.Run(() => _sourceClient.RetrieveRecords(tableName, fetchXml));
        result.RecordCount = sourceRecords.Entities.Count;

        // Retrieve target records for diff comparison (unless force mode)
        Dictionary<Guid, Entity>? targetRecordMap = null;
        if (!force)
        {
            _logger.LogInfoIfVerbose($"  Retrieving target records for diff...");
            var targetRecords = await Task.Run(() => _targetClient.RetrieveRecords(tableName, fetchXml));
            targetRecordMap = targetRecords.Entities.ToDictionary(e => e.Id);
            _logger.LogInfo($"  Found {result.RecordCount} source, {targetRecordMap.Count} target record(s)");
        }
        else
        {
            _logger.LogInfo($"  Found {result.RecordCount} record(s)");
        }

        // Classify columns and build pass 1 + pass 2 entities
        var flatEntities = new List<Entity>();
        var lookupEntities = new List<Entity>();
        var skippedCount = 0;

        foreach (var record in sourceRecords.Entities)
        {
            var flatEntity = new Entity(tableName, record.Id);
            var lookupEntity = new Entity(tableName, record.Id);
            var hasLookups = false;

            // Check if target has this record (for diff mode)
            Entity? targetRecord = null;
            var isNewRecord = force || targetRecordMap == null || !targetRecordMap.TryGetValue(record.Id, out targetRecord);

            var hasFlatChanges = isNewRecord;
            var hasLookupChanges = false;

            foreach (var attr in record.Attributes)
            {
                if (string.Equals(attr.Key, primaryKeyField, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (StateFields.Contains(attr.Key))
                    continue;
                if (!writableColumns.Contains(attr.Key))
                    continue;

                if (attr.Value is EntityReference)
                {
                    lookupEntity[attr.Key] = attr.Value;
                    hasLookups = true;

                    // Check if this lookup changed
                    if (!isNewRecord && !hasLookupChanges)
                    {
                        var targetValue = targetRecord!.Contains(attr.Key) ? targetRecord[attr.Key] : null;
                        if (!AttributeValuesEqual(attr.Value, targetValue))
                        {
                            hasLookupChanges = true;
                        }
                    }
                }
                else
                {
                    flatEntity[attr.Key] = attr.Value;

                    // Check if this flat value changed
                    if (!isNewRecord && !hasFlatChanges)
                    {
                        var targetValue = targetRecord!.Contains(attr.Key) ? targetRecord[attr.Key] : null;
                        if (!AttributeValuesEqual(attr.Value, targetValue))
                        {
                            hasFlatChanges = true;
                        }
                    }
                }
            }

            if (hasFlatChanges)
            {
                flatEntities.Add(flatEntity);
            }
            if (hasLookups && (isNewRecord || hasLookupChanges))
            {
                lookupEntities.Add(lookupEntity);
            }
            if (!hasFlatChanges && !(hasLookups && (isNewRecord || hasLookupChanges)))
            {
                skippedCount++;
            }
        }

        result.SkippedCount = skippedCount;
        if (!force && skippedCount > 0)
        {
            _logger.LogInfo($"  Diff: {flatEntities.Count} to upsert, {lookupEntities.Count} lookups to patch, {skippedCount} unchanged");
        }

        // Identify state records (if configured)
        var stateRecords = new List<Entity>();
        if (tableConfig.ManageState)
        {
            var sourceInactiveRecords = sourceRecords.Entities
                .Where(r => r.Contains("statecode") && ((OptionSetValue)r["statecode"]).Value != 0);

            if (force || targetRecordMap == null)
            {
                stateRecords = sourceInactiveRecords.ToList();
            }
            else
            {
                // Only include records whose state actually differs from target
                foreach (var record in sourceInactiveRecords)
                {
                    if (!targetRecordMap.TryGetValue(record.Id, out var targetRecord))
                    {
                        // New record — will need state set after upsert
                        stateRecords.Add(record);
                        continue;
                    }

                    var sourceState = ((OptionSetValue)record["statecode"]).Value;
                    var targetState = targetRecord.Contains("statecode")
                        ? ((OptionSetValue)targetRecord["statecode"]).Value
                        : 0;

                    if (sourceState != targetState)
                    {
                        stateRecords.Add(record);
                    }
                }
            }
        }

        return new PreparedTable
        {
            TableName = tableName,
            Result = result,
            FlatEntities = flatEntities,
            LookupEntities = lookupEntities,
            StateRecords = stateRecords
        };
    }

    private async Task<ManyToManyMigrationResult> SyncManyToManyAsync(RefDataRelationshipConfig m2mConfig, int batchSize, bool dryRun)
    {
        var relationshipName = m2mConfig.RelationshipName;
        var result = new ManyToManyMigrationResult { RelationshipName = relationshipName };

        _logger.LogInfo($"  Relationship: {relationshipName}");

        // Resolve relationship details — use explicit fields if all present,
        // otherwise call metadata API (single fast lookup per relationship).
        string intersectEntity, entity1Name, entity1Key, entity2Name, entity2Key;
        if (m2mConfig.HasExplicitFields)
        {
            intersectEntity = m2mConfig.IntersectEntity!;
            entity1Name = m2mConfig.Entity1!;
            entity1Key = m2mConfig.Entity1IdField!;
            entity2Name = m2mConfig.Entity2!;
            entity2Key = m2mConfig.Entity2IdField!;
            _logger.LogInfoIfVerbose($"    Using explicit relationship fields (skipping metadata lookup)");
        }
        else
        {
            _logger.LogInfoIfVerbose($"    Retrieving relationship metadata...");
            var relMetadata = _sourceClient.GetManyToManyRelationshipMetadata(relationshipName);
            intersectEntity = relMetadata.IntersectEntityName;
            entity1Name = relMetadata.Entity1LogicalName;
            entity1Key = relMetadata.Entity1IntersectAttribute;
            entity2Name = relMetadata.Entity2LogicalName;
            entity2Key = relMetadata.Entity2IntersectAttribute;
        }

        result.Entity1Name = entity1Name;
        result.Entity2Name = entity2Name;

        _logger.LogInfoIfVerbose($"    {entity1Name} <-> {entity2Name} via {intersectEntity}");

        // Query source intersection entity for all associations
        var sourceFetchXml = $@"<fetch><entity name='{intersectEntity}'><attribute name='{entity1Key}' /><attribute name='{entity2Key}' /></entity></fetch>";
        var sourceAssociations = await Task.Run(() => _sourceClient.RetrieveRecordsByFetchXml(sourceFetchXml));
        var sourcePairs = sourceAssociations.Entities
            .Select(e => (Entity1Id: (Guid)e[entity1Key], Entity2Id: (Guid)e[entity2Key]))
            .ToHashSet();
        result.SourceCount = sourcePairs.Count;

        // Query target intersection entity for existing associations
        var targetAssociations = await Task.Run(() => _targetClient.RetrieveRecordsByFetchXml(sourceFetchXml));
        var targetPairs = targetAssociations.Entities
            .Select(e => (Entity1Id: (Guid)e[entity1Key], Entity2Id: (Guid)e[entity2Key]))
            .ToHashSet();
        result.TargetExistingCount = targetPairs.Count;

        // Diff: associate (in source, not in target) and disassociate (in target, not in source)
        var toAssociate = sourcePairs.Except(targetPairs).ToList();
        var toDisassociate = targetPairs.Except(sourcePairs).ToList();

        _logger.LogInfo($"    Source: {sourcePairs.Count}, Target existing: {targetPairs.Count}, To associate: {toAssociate.Count}, To disassociate: {toDisassociate.Count}");

        if (!dryRun)
        {
            // Associate missing
            if (toAssociate.Count > 0)
            {
                var errors = ExecuteBatchAssociations(toAssociate, entity1Name, entity2Name, relationshipName, batchSize, isAssociate: true);
                result.AssociatedCount = toAssociate.Count - errors.Count;
                result.Errors.AddRange(errors);
            }

            // Disassociate removed
            if (toDisassociate.Count > 0)
            {
                var errors = ExecuteBatchAssociations(toDisassociate, entity1Name, entity2Name, relationshipName, batchSize, isAssociate: false);
                result.DisassociatedCount = toDisassociate.Count - errors.Count;
                result.Errors.AddRange(errors);
            }
        }
        else
        {
            result.AssociatedCount = toAssociate.Count;
            result.DisassociatedCount = toDisassociate.Count;
            _logger.LogInfoIfVerbose($"    [DRY RUN] Would associate {toAssociate.Count}, disassociate {toDisassociate.Count}");
        }

        return result;
    }

    private List<RecordError> ExecuteBatchAssociations(
        List<(Guid Entity1Id, Guid Entity2Id)> pairs,
        string entity1Name, string entity2Name, string relationshipName,
        int batchSize, bool isAssociate)
    {
        var errors = new List<RecordError>();
        var phase = isAssociate ? "Associate" : "Disassociate";

        for (var i = 0; i < pairs.Count; i += batchSize)
        {
            var batch = pairs.Skip(i).Take(batchSize).ToList();
            var requests = new OrganizationRequestCollection();

            foreach (var (entity1Id, entity2Id) in batch)
            {
                if (isAssociate)
                {
                    requests.Add(new AssociateRequest
                    {
                        Target = new EntityReference(entity1Name, entity1Id),
                        RelatedEntities = new EntityReferenceCollection
                        {
                            new EntityReference(entity2Name, entity2Id)
                        },
                        Relationship = new Relationship(relationshipName)
                    });
                }
                else
                {
                    requests.Add(new DisassociateRequest
                    {
                        Target = new EntityReference(entity1Name, entity1Id),
                        RelatedEntities = new EntityReferenceCollection
                        {
                            new EntityReference(entity2Name, entity2Id)
                        },
                        Relationship = new Relationship(relationshipName)
                    });
                }
            }

            _logger.LogInfoIfVerbose($"    Executing {phase.ToLower()} batch {(i / batchSize) + 1} ({batch.Count} requests)...");
            var response = _targetClient.ExecuteMultiple(requests, continueOnError: true);

            if (response.IsFaulted)
            {
                foreach (var item in response.Responses)
                {
                    if (item.Fault != null)
                    {
                        var pair = batch[item.RequestIndex];
                        errors.Add(new RecordError
                        {
                            TableName = relationshipName,
                            RecordId = pair.Entity1Id,
                            Phase = phase,
                            ErrorMessage = item.Fault.Message
                        });
                        _logger.LogWarningIfVerbose($"    {phase} error for {pair.Entity1Id} <-> {pair.Entity2Id}: {item.Fault.Message}");
                    }
                }
            }
        }

        return errors;
    }

    private class PreparedTable
    {
        public string TableName { get; set; } = string.Empty;
        public TableMigrationResult Result { get; set; } = new();
        public List<Entity> FlatEntities { get; set; } = new();
        public List<Entity> LookupEntities { get; set; } = new();
        public List<Entity> StateRecords { get; set; } = new();
    }

    public static bool AttributeValuesEqual(object? source, object? target)
    {
        if (source is null && target is null) return true;
        if (source is null || target is null) return false;

        return (source, target) switch
        {
            (EntityReference s, EntityReference t) => s.Id == t.Id,
            (OptionSetValue s, OptionSetValue t) => s.Value == t.Value,
            (Money s, Money t) => s.Value == t.Value,
            (BooleanManagedProperty s, BooleanManagedProperty t) => s.Value == t.Value,
            _ => source.Equals(target)
        };
    }

    private HashSet<string> GetWritableColumns(EntityMetadata metadata, List<string> excludeColumns, List<string> includeColumns)
    {
        var hasIncludeList = includeColumns.Count > 0;
        var includeSet = hasIncludeList
            ? new HashSet<string>(includeColumns, StringComparer.OrdinalIgnoreCase)
            : null;

        var writable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var attr in metadata.Attributes)
        {
            if (string.IsNullOrEmpty(attr.LogicalName))
                continue;
            if (SystemFields.Contains(attr.LogicalName))
                continue;
            if (StateFields.Contains(attr.LogicalName))
                continue;
            if (excludeColumns.Contains(attr.LogicalName, StringComparer.OrdinalIgnoreCase))
                continue;
            if (hasIncludeList && !includeSet!.Contains(attr.LogicalName))
                continue;

            var isWritable = (attr.IsValidForCreate == true) || (attr.IsValidForUpdate == true);
            if (isWritable)
            {
                writable.Add(attr.LogicalName);
            }
        }

        return writable;
    }

    private List<RecordError> ExecuteBatchUpserts(List<Entity> entities, int batchSize, string tableName, string phase)
    {
        var errors = new List<RecordError>();

        for (var i = 0; i < entities.Count; i += batchSize)
        {
            var batch = entities.Skip(i).Take(batchSize).ToList();
            var requests = new OrganizationRequestCollection();

            foreach (var entity in batch)
            {
                requests.Add(new UpsertRequest { Target = entity });
            }

            _logger.LogInfoIfVerbose($"    Executing batch {(i / batchSize) + 1} ({batch.Count} requests)...");
            var response = _targetClient.ExecuteMultiple(requests, continueOnError: true);

            if (response.IsFaulted)
            {
                foreach (var item in response.Responses)
                {
                    if (item.Fault != null)
                    {
                        var recordId = batch[item.RequestIndex].Id;
                        errors.Add(new RecordError
                        {
                            TableName = tableName,
                            RecordId = recordId,
                            Phase = phase,
                            ErrorMessage = item.Fault.Message
                        });
                        _logger.LogWarningIfVerbose($"    Error for record {recordId}: {item.Fault.Message}");
                    }
                }
            }
        }

        return errors;
    }

    private List<RecordError> ExecuteBatchStateChanges(List<Entity> records, string tableName, int batchSize)
    {
        var errors = new List<RecordError>();

        for (var i = 0; i < records.Count; i += batchSize)
        {
            var batch = records.Skip(i).Take(batchSize).ToList();
            var requests = new OrganizationRequestCollection();

            foreach (var record in batch)
            {
                var stateCode = ((OptionSetValue)record["statecode"]).Value;
                var statusCode = record.Contains("statuscode")
                    ? ((OptionSetValue)record["statuscode"]).Value
                    : -1;

                requests.Add(new SetStateRequest
                {
                    EntityMoniker = new EntityReference(tableName, record.Id),
                    State = new OptionSetValue(stateCode),
                    Status = new OptionSetValue(statusCode)
                });
            }

            _logger.LogInfoIfVerbose($"    Executing state batch {(i / batchSize) + 1} ({batch.Count} requests)...");
            var response = _targetClient.ExecuteMultiple(requests, continueOnError: true);

            if (response.IsFaulted)
            {
                foreach (var item in response.Responses)
                {
                    if (item.Fault != null)
                    {
                        var recordId = batch[item.RequestIndex].Id;
                        errors.Add(new RecordError
                        {
                            TableName = tableName,
                            RecordId = recordId,
                            Phase = "Set State",
                            ErrorMessage = item.Fault.Message
                        });
                        _logger.LogWarningIfVerbose($"    State error for record {recordId}: {item.Fault.Message}");
                    }
                }
            }
        }

        return errors;
    }
}
