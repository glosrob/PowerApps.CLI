using PowerApps.CLI.Infrastructure;

namespace PowerApps.CLI.Services;

/// <summary>
/// Extracts and exports schema from PowerApps/Dataverse environments.
/// </summary>
public class SchemaService : ISchemaService
{
    private readonly IConsoleLogger _logger;
    private readonly ISchemaExporter _schemaExporter;
    private readonly IDataverseClient _dataverseClient;
    private readonly ISchemaExtractor _schemaExtractor;

    public SchemaService(
        IConsoleLogger logger,
        ISchemaExporter schemaExporter,
        IDataverseClient dataverseClient,
        ISchemaExtractor schemaExtractor)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _schemaExporter = schemaExporter ?? throw new ArgumentNullException(nameof(schemaExporter));
        _dataverseClient = dataverseClient ?? throw new ArgumentNullException(nameof(dataverseClient));
        _schemaExtractor = schemaExtractor ?? throw new ArgumentNullException(nameof(schemaExtractor));
    }

    public async Task ExportSchemaAsync(
        string? url,
        string outputPath,
        string format,
        string? solutionName = null,
        string? connectionString = null,
        string? clientId = null,
        string? clientSecret = null,
        string? attributePrefix = null,
        string? excludeAttributes = null)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("Output path cannot be null or whitespace.", nameof(outputPath));
        }

        // Validate format
        var validFormats = new[] { "json", "xlsx" };
        format = format.ToLowerInvariant();
        if (!validFormats.Contains(format))
        {
            throw new ArgumentException($"Invalid format '{format}'. Supported formats: {string.Join(", ", validFormats)}", nameof(format));
        }

        var orgName = _dataverseClient.GetOrganizationName();
        _logger.LogSuccess($"✓ Connected to {orgName}");

        _logger.LogInfo("Extracting schema...");

        var schema = await _schemaExtractor.ExtractSchemaAsync(solutionName);
        
        _logger.LogSuccess($"✓ Extracted {schema.Entities?.Count ?? 0} entities");

        // Export schema to file
        _logger.LogInfo($"Writing {format.ToUpperInvariant()} file...");
        await _schemaExporter.ExportAsync(schema, outputPath, format);
        
        _logger.LogSuccess($"✓ Schema exported to {outputPath}");
    }

    private static string? ExtractUrlFromConnectionString(string connectionString)
    {
        // Extract URL from connection string for logging
        var urlPattern = "Url=([^;]+)";
        var match = System.Text.RegularExpressions.Regex.Match(connectionString, urlPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : null;
    }
}
