namespace PowerApps.CLI.Services;

/// <summary>
/// Provides schema extraction and export functionality for PowerApps/Dataverse environments.
/// </summary>
public interface ISchemaService
{
    /// <summary>
    /// Exports schema from a Dataverse environment to a file.
    /// </summary>
    /// <param name="outputPath">The output file path.</param>
    /// <param name="format">The output format (json, xlsx).</param>
    /// <param name="solutionName">Optional solution name to filter by.</param>
    /// <param name="attributePrefix">Optional attribute prefix filter.</param>
    /// <param name="excludeAttributes">Optional comma-separated list of attributes to exclude.</param>
    Task ExportSchemaAsync(
        string outputPath,
        string format,
        string? solutionName = null,
        string? attributePrefix = null,
        string? excludeAttributes = null);
}
