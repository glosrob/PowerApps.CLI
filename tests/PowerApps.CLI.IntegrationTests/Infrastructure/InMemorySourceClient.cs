using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using PowerApps.CLI.Infrastructure;

namespace PowerApps.CLI.IntegrationTests.Infrastructure;

/// <summary>
/// In-memory stand-in for a source Dataverse environment, so refdata-migrate can be exercised end to
/// end against a single real (target) environment. Only the members the migrator calls on its source
/// client for table data are implemented; anything else throws so an unexpected call fails loudly
/// rather than returning misleading empty data.
/// </summary>
public class InMemorySourceClient : IDataverseClient
{
    private readonly Dictionary<string, List<Entity>> _tables = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Adds a source record. Null attributes are dropped to mirror Dataverse, which omits null columns
    /// from retrieved records — the behaviour behind #114, so tests must not paper over it.
    /// </summary>
    public InMemorySourceClient AddRecord(Entity record)
    {
        var stored = new Entity(record.LogicalName, record.Id);
        foreach (var attr in record.Attributes)
        {
            if (attr.Value is not null)
            {
                stored[attr.Key] = attr.Value;
            }
        }

        if (!_tables.TryGetValue(record.LogicalName, out var records))
        {
            records = new List<Entity>();
            _tables[record.LogicalName] = records;
        }
        records.Add(stored);
        return this;
    }

    public string GetEnvironmentUrl() => "https://in-memory-source.local";

    public EntityCollection RetrieveRecords(string entityName, string? fetchXml = null)
    {
        if (fetchXml is not null)
        {
            throw new NotSupportedException("InMemorySourceClient does not evaluate FetchXML filters.");
        }

        // Return copies so the migrator can't mutate the test's source data between runs
        var records = _tables.TryGetValue(entityName, out var stored)
            ? stored.Select(Clone).ToList()
            : new List<Entity>();
        return new EntityCollection(records) { EntityName = entityName };
    }

    private static Entity Clone(Entity source)
    {
        var copy = new Entity(source.LogicalName, source.Id);
        foreach (var attr in source.Attributes)
        {
            copy[attr.Key] = attr.Value;
        }
        return copy;
    }

    public string GetOrganizationName() => throw NotUsed();
    public EntityCollection RetrieveMultiple(QueryExpression query) => throw NotUsed();
    public OrganizationResponse Execute(OrganizationRequest request) => throw NotUsed();
    public Task<Dictionary<string, List<string>>> GetAllEntityMetadataAsync() => throw NotUsed();
    public Task<Dictionary<string, List<string>>> GetEntitiesFromSolutionAsync(string solutionName) => throw NotUsed();
    public Task<EntityMetadata?> GetEntityMetadataAsync(string entityLogicalName, EntityFilters filters) => throw NotUsed();
    public EntityCollection RetrieveProcesses(List<string> solutions) => throw NotUsed();
    public void ActivateProcess(Guid processId) => throw NotUsed();
    public void DeactivateProcess(Guid processId) => throw NotUsed();
    public EntityCollection RetrieveDuplicateRules(List<string> solutions) => throw NotUsed();
    public void ActivateDuplicateRule(Guid ruleId) => throw NotUsed();
    public void DeactivateDuplicateRule(Guid ruleId) => throw NotUsed();
    public EntityCollection RetrievePluginSteps(List<string> solutions) => throw NotUsed();
    public void EnablePluginStep(Guid stepId) => throw NotUsed();
    public void DisablePluginStep(Guid stepId) => throw NotUsed();
    public EntityCollection RetrieveRecordsByFetchXml(string fetchXml) => throw NotUsed();
    public ExecuteMultipleResponse ExecuteMultiple(OrganizationRequestCollection requests, bool continueOnError) => throw NotUsed();
    public EntityMetadata GetEntityMetadata(string entityLogicalName) => throw NotUsed();
    public ManyToManyRelationshipMetadata GetManyToManyRelationshipMetadata(string relationshipName) => throw NotUsed();
    public Task<EntityCollection> GetSolutionComponentLayersAsync(string solutionName, Action<int, int, int>? batchProgress = null, Action<string>? phaseLog = null) => throw NotUsed();

    private static NotSupportedException NotUsed([System.Runtime.CompilerServices.CallerMemberName] string member = "")
        => new($"InMemorySourceClient.{member} is not implemented — the source side of refdata-migrate shouldn't need it.");
}
