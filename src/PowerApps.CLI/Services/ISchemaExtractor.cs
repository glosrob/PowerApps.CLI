using Microsoft.PowerPlatform.Dataverse.Client;
using PowerApps.CLI.Models;

namespace PowerApps.CLI.Services;

/// <summary>
/// Extracts metadata schema from Dataverse environments.
/// </summary>
public interface ISchemaExtractor
{
    /// <summary>
    /// Extracts metadata schema from a Dataverse environment.
    /// </summary>
    /// <param name="serviceClient">Connected Dataverse service client.</param>
    /// <param name="solutionNames">Optional comma-separated solution names to filter by.</param>
    /// <returns>Complete schema with entities, attributes, and relationships.</returns>
    Task<PowerAppsSchema> ExtractSchemaAsync(ServiceClient serviceClient, string? solutionNames = null);
}
