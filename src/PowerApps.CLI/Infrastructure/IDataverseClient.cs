using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Metadata.Query;
using Microsoft.Xrm.Sdk.Query;

namespace PowerApps.CLI.Infrastructure;

/// <summary>
/// Provides authentication and connection to Dataverse environments.
/// </summary>
public interface IDataverseClient
{

    /// <summary>
    /// Gets the organization name from the connected environment.
    /// </summary>
    /// <returns>The organization friendly name.</returns>
    string GetOrganizationName();

    /// <summary>
    /// Gets the environment URL from the connected environment.
    /// </summary>
    /// <returns>The environment URL.</returns>
    string GetEnvironmentUrl();

    /// <summary>
    /// Retrieves records from a table with optional FetchXML filter.
    /// </summary>
    /// <param name="entityName">The logical name of the table.</param>
    /// <param name="fetchXml">Optional FetchXML query. If null, retrieves all records.</param>
    /// <returns>Collection of entities matching the query.</returns>
    EntityCollection RetrieveRecords(string entityName, string? fetchXml = null);

    /// <summary>
    /// Retrieves multiple records using a QueryExpression.
    /// </summary>
    /// <param name="query">The query expression to execute.</param>
    /// <returns>Collection of entities matching the query.</returns>
    EntityCollection RetrieveMultiple(QueryExpression query);

    /// <summary>
    /// Executes an organization request.
    /// </summary>
    /// <param name="request">The organization request to execute.</param>
    /// <returns>The organization response.</returns>
    OrganizationResponse Execute(OrganizationRequest request);

    /// <summary>
    /// Gets all entity metadata from the environment.
    /// </summary>
    /// <returns>Dictionary mapping entity logical names to empty solution lists.</returns>
    Task<Dictionary<string, List<string>>> GetAllEntityMetadataAsync();

    /// <summary>
    /// Gets entities that belong to a specific solution.
    /// </summary>
    /// <param name="solutionName">The unique name of the solution.</param>
    /// <returns>Dictionary mapping entity logical names to the solution name.</returns>
    Task<Dictionary<string, List<string>>> GetEntitiesFromSolutionAsync(string solutionName);

    /// <summary>
    /// Retrieves entity metadata with specified filters.
    /// </summary>
    /// <param name="entityLogicalName">The logical name of the entity.</param>
    /// <param name="filters">The metadata filters to apply.</param>
    /// <returns>The entity metadata, or null if not found.</returns>
    Task<EntityMetadata?> GetEntityMetadataAsync(string entityLogicalName, EntityFilters filters);

    /// <summary>
    /// Retrieves workflow/process records, optionally filtered by solution names.
    /// </summary>
    /// <param name="solutions">Solution unique names to filter by. If empty, retrieves all processes.</param>
    /// <returns>Collection of workflow entities.</returns>
    EntityCollection RetrieveProcesses(List<string> solutions);

    /// <summary>
    /// Activates a workflow/process.
    /// </summary>
    /// <param name="processId">The ID of the process to activate.</param>
    void ActivateProcess(Guid processId);

    /// <summary>
    /// Deactivates a workflow/process.
    /// </summary>
    /// <param name="processId">The ID of the process to deactivate.</param>
    void DeactivateProcess(Guid processId);

    /// <summary>
    /// Retrieves duplicate detection rules, optionally filtered by solution names.
    /// </summary>
    /// <param name="solutions">Solution unique names to filter by. If empty, retrieves all rules.</param>
    /// <returns>Collection of duplicaterule entities.</returns>
    EntityCollection RetrieveDuplicateRules(List<string> solutions);

    /// <summary>
    /// Publishes (activates) a duplicate detection rule.
    /// </summary>
    /// <param name="ruleId">The ID of the duplicate detection rule to activate.</param>
    void ActivateDuplicateRule(Guid ruleId);

    /// <summary>
    /// Unpublishes (deactivates) a duplicate detection rule.
    /// </summary>
    /// <param name="ruleId">The ID of the duplicate detection rule to deactivate.</param>
    void DeactivateDuplicateRule(Guid ruleId);

    /// <summary>
    /// Retrieves records using a FetchXML query.
    /// </summary>
    /// <param name="fetchXml">The FetchXML query string.</param>
    /// <returns>Collection of entities matching the query.</returns>
    EntityCollection RetrieveRecordsByFetchXml(string fetchXml);

    /// <summary>
    /// Executes multiple organization requests in a single batch.
    /// </summary>
    /// <param name="requests">The collection of requests to execute.</param>
    /// <param name="continueOnError">Whether to continue processing if a request fails.</param>
    /// <returns>The batch execution response.</returns>
    ExecuteMultipleResponse ExecuteMultiple(OrganizationRequestCollection requests, bool continueOnError);

    /// <summary>
    /// Retrieves entity metadata including attribute definitions.
    /// </summary>
    /// <param name="entityLogicalName">The logical name of the entity.</param>
    /// <returns>The entity metadata.</returns>
    EntityMetadata GetEntityMetadata(string entityLogicalName);

    /// <summary>
    /// Retrieves many-to-many relationship metadata by relationship schema name.
    /// </summary>
    /// <param name="relationshipName">The schema name of the N:N relationship.</param>
    /// <returns>The many-to-many relationship metadata.</returns>
    ManyToManyRelationshipMetadata GetManyToManyRelationshipMetadata(string relationshipName);
}
