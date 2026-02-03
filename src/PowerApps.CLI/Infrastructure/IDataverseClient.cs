using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Metadata;
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
}
