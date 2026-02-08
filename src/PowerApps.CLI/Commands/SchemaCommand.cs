using System.CommandLine;
using PowerApps.CLI.Services;
using PowerApps.CLI.Infrastructure;

namespace PowerApps.CLI.Commands;

/// <summary>
/// Handles the schema export command.
/// </summary>
public class SchemaCommand
{
    private readonly IConsoleLogger _logger;
    private readonly ISchemaService _schemaService;

    public SchemaCommand(IConsoleLogger logger, ISchemaService schemaService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _schemaService = schemaService ?? throw new ArgumentNullException(nameof(schemaService));
    }

    public async Task<int> ExecuteAsync(
        string? url,
        string? connectionString,
        string output,
        string format,
        string? solution,
        string? attributePrefix,
        string? excludeAttributes)
    {
        try
        {
            // Validate that either URL or connection string is provided
            if (string.IsNullOrWhiteSpace(url) && string.IsNullOrWhiteSpace(connectionString))
            {
                _logger.LogError("Either --url or --connection-string must be provided.");
                return 1;
            }

            _logger.LogInfo("PowerApps Schema Export");
            _logger.LogInfo("======================\n");

            if (!string.IsNullOrWhiteSpace(url))
            {
                _logger.LogVerbose($"Environment URL: {url}");
            }
            _logger.LogVerbose($"Solution: {solution ?? "(all metadata)"}");
            _logger.LogVerbose($"Output: {output}");
            _logger.LogVerbose($"Format: {format}");

            if (!string.IsNullOrWhiteSpace(attributePrefix))
            {
                _logger.LogVerbose($"Attribute Prefix Filter: {attributePrefix}");
            }

            if (!string.IsNullOrWhiteSpace(excludeAttributes))
            {
                _logger.LogVerbose($"Excluded Attributes: {excludeAttributes}");
            }

            _logger.LogInfo("Connecting to PowerApps environment...");

            // Export schema (service will handle the export logic)
            await _schemaService.ExportSchemaAsync(
                output,
                format,
                solution,
                attributePrefix,
                excludeAttributes);

            _logger.LogSuccess($"\n✓ Schema exported successfully!");
            _logger.LogInfo($"Output: {Path.GetFullPath(output)}");
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error: {ex.Message}");
            _logger.LogVerbose($"\nStack trace:\n{ex.StackTrace}");
            return 1;
        }
    }

    public static Command CreateCliCommand()
    {
        var schemaExportCommand = new Command("schema-export", "Extract metadata schema from PowerApps/Dataverse environments");

        var urlOption = new Option<string?>(
            aliases: new[] { "--url", "-u" },
            description: "PowerApps environment URL (e.g., https://org.crm.dynamics.com)")
        {
            IsRequired = false
        };

        var solutionOption = new Option<string?>(
            aliases: new[] { "--solution", "-s" },
            description: "Solution unique name(s) to filter by (comma-separated for multiple solutions, optional)");

        var outputOption = new Option<string>(
            aliases: new[] { "--output", "-o" },
            getDefaultValue: () => "powerapp-schema.json",
            description: "Output file path");

        var formatOption = new Option<string>(
            aliases: new[] { "--format", "-f" },
            getDefaultValue: () => "json",
            description: "Output format: json or xlsx")
        {
            ArgumentHelpName = "format"
        };

        var connectionStringOption = new Option<string?>(
            aliases: new[] { "--connection-string", "-c" },
            description: "Dataverse connection string (alternative to individual auth options)");

        var clientIdOption = new Option<string?>(
            aliases: new[] { "--client-id" },
            description: "Azure AD Application (Client) ID for service principal authentication");

        var clientSecretOption = new Option<string?>(
            aliases: new[] { "--client-secret" },
            description: "Azure AD Application Client Secret for service principal authentication");

        var verboseOption = new Option<bool>(
            aliases: new[] { "--verbose", "-v" },
            description: "Enable verbose output");

        var attributePrefixOption = new Option<string?>(
            aliases: new[] { "--attribute-prefix", "--attr-prefix" },
            description: "Only include attributes starting with this prefix (e.g., 'rob_')");

        var excludeAttributesOption = new Option<string?>(
            aliases: new[] { "--exclude-attributes", "--exclude-attrs" },
            description: "Comma-separated list of attribute names to exclude");

        schemaExportCommand.AddOption(urlOption);
        schemaExportCommand.AddOption(solutionOption);
        schemaExportCommand.AddOption(outputOption);
        schemaExportCommand.AddOption(formatOption);
        schemaExportCommand.AddOption(connectionStringOption);
        schemaExportCommand.AddOption(clientIdOption);
        schemaExportCommand.AddOption(clientSecretOption);
        schemaExportCommand.AddOption(verboseOption);
        schemaExportCommand.AddOption(attributePrefixOption);
        schemaExportCommand.AddOption(excludeAttributesOption);

        schemaExportCommand.SetHandler(async (context) =>
        {
            var url = context.ParseResult.GetValueForOption(urlOption);
            var solution = context.ParseResult.GetValueForOption(solutionOption);
            var output = context.ParseResult.GetValueForOption(outputOption)!;
            var format = context.ParseResult.GetValueForOption(formatOption)!;
            var connectionString = context.ParseResult.GetValueForOption(connectionStringOption);
            var clientId = context.ParseResult.GetValueForOption(clientIdOption);
            var clientSecret = context.ParseResult.GetValueForOption(clientSecretOption);
            var verbose = context.ParseResult.GetValueForOption(verboseOption);
            var attributePrefix = context.ParseResult.GetValueForOption(attributePrefixOption);
            var excludeAttributes = context.ParseResult.GetValueForOption(excludeAttributesOption);

            var logger = new ConsoleLogger { IsVerboseEnabled = verbose };
            var fileWriter = new FileWriter();
            var schemaExporter = new SchemaExporter(fileWriter);
            var dataverseClient = new DataverseClient(url ?? string.Empty, clientId, clientSecret, connectionString);
            var metadataMapper = new MetadataMapper();
            var schemaExtractor = new SchemaExtractor(metadataMapper, dataverseClient);
            var schemaService = new SchemaService(logger, schemaExporter, dataverseClient, schemaExtractor);

            var command = new SchemaCommand(logger, schemaService);
            context.ExitCode = await command.ExecuteAsync(
                url, connectionString, output, format, solution, attributePrefix, excludeAttributes);
        });

        return schemaExportCommand;
    }
}
