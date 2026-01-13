using PowerApps.CLI.Models;

namespace PowerApps.CLI.Services;

/// <summary>
/// Exports PowerApps schema to various file formats.
/// </summary>
public interface ISchemaExporter
{
    /// <summary>
    /// Exports the schema to a file in the specified format.
    /// </summary>
    /// <param name="schema">The schema to export.</param>
    /// <param name="outputPath">The path where the file should be written.</param>
    /// <param name="format">The output format (json, xlsx).</param>
    Task ExportAsync(PowerAppsSchema schema, string outputPath, string format);
}
