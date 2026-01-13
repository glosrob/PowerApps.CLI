using System.CommandLine;
using PowerApps.CLI.Services;
using PowerApps.CLI.Infrastructure;

namespace PowerApps.CLI.Commands;

/// <summary>
/// Handles the schema export command.
/// </summary>
public static class SchemaCommand
{
    public static Command CreateCommand()
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
            var dataverseClient = new DataverseClient();
            var fileWriter = new FileWriter();
            var metadataMapper = new MetadataMapper();
            var schemaExtractor = new SchemaExtractor(metadataMapper, dataverseClient);
            var schemaExporter = new SchemaExporter(fileWriter);
            var schemaService = new SchemaService(dataverseClient, logger, schemaExtractor, schemaExporter);

            await ExecuteExportAsync(
                schemaService,
                fileWriter,
                logger,
                url,
                solution,
                output,
                format,
                connectionString,
                clientId,
                clientSecret,
                attributePrefix,
                excludeAttributes);
        });

        return schemaExportCommand;
    }

    private static async Task ExecuteExportAsync(
        ISchemaService schemaService,
        IFileWriter fileWriter,
        IConsoleLogger logger,
        string? url,
        string? solution,
        string output,
        string format,
        string? connectionString,
        string? clientId,
        string? clientSecret,
        string? attributePrefix,
        string? excludeAttributes)
    {
        try
        {
            // Validate that either URL or connection string is provided
            if (string.IsNullOrWhiteSpace(url) && string.IsNullOrWhiteSpace(connectionString))
            {
                logger.LogError("Either --url or --connection-string must be provided.");
                Environment.Exit(1);
                return;
            }

            logger.LogInfo("PowerApps Schema Export");
            logger.LogInfo("======================\n");
            
            if (!string.IsNullOrWhiteSpace(url))
            {
                logger.LogVerbose($"Environment URL: {url}");
            }
            logger.LogVerbose($"Solution: {solution ?? "(all metadata)"}");
            logger.LogVerbose($"Output: {output}");
            logger.LogVerbose($"Format: {format}");
            
            if (!string.IsNullOrWhiteSpace(attributePrefix))
            {
                logger.LogVerbose($"Attribute Prefix Filter: {attributePrefix}");
            }
            
            if (!string.IsNullOrWhiteSpace(excludeAttributes))
            {
                logger.LogVerbose($"Excluded Attributes: {excludeAttributes}");
            }
            
            logger.LogInfo("Connecting to PowerApps environment...");

            // Export schema (service will handle the export logic)
            await schemaService.ExportSchemaAsync(
                url,
                output,
                format,
                solution,
                connectionString,
                clientId,
                clientSecret,
                attributePrefix,
                excludeAttributes);

            logger.LogSuccess($"\n✓ Schema exported successfully!");
            logger.LogInfo($"Output: {Path.GetFullPath(output)}");
        }
        catch (Exception ex)
        {
            logger.LogError($"Error: {ex.Message}");
            logger.LogVerbose($"\nStack trace:\n{ex.StackTrace}");
            Environment.Exit(1);
        }
    }
}
