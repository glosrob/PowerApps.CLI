using System.CommandLine;
using System.Text.Json;
using PowerApps.CLI.Infrastructure;
using PowerApps.CLI.Models;
using PowerApps.CLI.Services;

namespace PowerApps.CLI.Commands;

/// <summary>
/// Handles the constants generation command.
/// </summary>
public class ConstantsCommand
{
    private readonly IConsoleLogger _logger;
    private readonly ISchemaExtractor _schemaExtractor;
    private readonly IConstantsFilter _constantsFilter;
    private readonly IConstantsGenerator _constantsGenerator;

    public ConstantsCommand(
        IConsoleLogger logger,
        ISchemaExtractor schemaExtractor,
        IConstantsFilter constantsFilter,
        IConstantsGenerator constantsGenerator)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _schemaExtractor = schemaExtractor ?? throw new ArgumentNullException(nameof(schemaExtractor));
        _constantsFilter = constantsFilter ?? throw new ArgumentNullException(nameof(constantsFilter));
        _constantsGenerator = constantsGenerator ?? throw new ArgumentNullException(nameof(constantsGenerator));
    }

    public async Task<int> ExecuteAsync(
        ConstantsConfig? config,
        string? url,
        string? solution,
        string output,
        string namespaceName,
        bool singleFile,
        string? connectionString,
        bool includeEntities,
        bool includeOptionSets,
        string? excludeEntities,
        string? excludeAttributes,
        string? attributePrefix,
        bool pascalCase)
    {
        try
        {
            // Validate that either URL or connection string is provided
            if (string.IsNullOrWhiteSpace(url) && string.IsNullOrWhiteSpace(connectionString))
            {
                _logger.LogError("Either --url or --connection-string must be provided.");
                return 1;
            }

            _logger.LogInfo("PowerApps Constants Generator");
            _logger.LogInfo("============================\n");

            if (!string.IsNullOrWhiteSpace(url))
            {
                _logger.LogVerbose($"Environment URL: {url}");
            }
            _logger.LogVerbose($"Solution: {solution ?? "(all metadata)"}");
            _logger.LogVerbose($"Output: {output}");
            _logger.LogVerbose($"Namespace: {namespaceName}");
            _logger.LogVerbose($"Mode: {(singleFile ? "Single file" : "Multiple files")}");

            _logger.LogInfo("Connecting to Dataverse...");

            _logger.LogInfo("Extracting metadata...");

            // Extract schema from solution
            var schema = await _schemaExtractor.ExtractSchemaAsync(solution);
            var entities = schema.Entities;

            _logger.LogInfo($"Retrieved {entities.Count} entit{(entities.Count == 1 ? "y" : "ies")}");

            // Apply filtering based on config or CLI options
            ConstantsConfig filterConfig;

            if (config != null)
            {
                // Use config file settings
                filterConfig = config;
            }
            else
            {
                // Create config from CLI options
                var excludeEntitiesList = string.IsNullOrWhiteSpace(excludeEntities)
                    ? []
                    : excludeEntities.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(e => e.Trim()).ToList();
                var excludeAttributesList = string.IsNullOrWhiteSpace(excludeAttributes)
                    ? []
                    : excludeAttributes.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(a => a.Trim()).ToList();

                filterConfig = new ConstantsConfig
                {
                    ExcludeEntities = excludeEntitiesList,
                    ExcludeAttributes = excludeAttributesList,
                    AttributePrefix = attributePrefix,
                    PascalCaseConversion = pascalCase,
                    SingleFile = singleFile,
                    IncludeEntities = includeEntities,
                    IncludeGlobalOptionSets = includeOptionSets
                };
            }

            // Filter entities
            if (filterConfig.ExcludeEntities.Count > 0)
            {
                _logger.LogVerbose($"Excluding {filterConfig.ExcludeEntities.Count} entit{(filterConfig.ExcludeEntities.Count == 1 ? "y" : "ies")}");
                entities = _constantsFilter.FilterEntities(entities, filterConfig);
                _logger.LogInfo($"After filtering: {entities.Count} entit{(entities.Count == 1 ? "y" : "ies")}");
            }

            // Filter attributes on each entity
            if (filterConfig.ExcludeAttributes.Count > 0 || !string.IsNullOrWhiteSpace(filterConfig.AttributePrefix))
            {
                for (int i = 0; i < entities.Count; i++)
                {
                    entities[i] = _constantsFilter.FilterAttributes(entities[i], filterConfig);
                }
            }

            // Create output configuration
            var outputConfig = new ConstantsOutputConfig
            {
                OutputPath = output,
                Namespace = namespaceName,
                SingleFile = filterConfig.SingleFile,
                IncludeEntities = filterConfig.IncludeEntities,
                IncludeGlobalOptionSets = filterConfig.IncludeGlobalOptionSets,
                IncludeReferenceData = filterConfig.IncludeReferenceData,
                PascalCaseConversion = filterConfig.PascalCaseConversion,
                ExcludeAttributes = filterConfig.ExcludeAttributes
            };

            // Generate constants
            await _constantsGenerator.GenerateAsync(entities, outputConfig, _logger);

            _logger.LogSuccess($"\n✓ Constants generated successfully!");
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
        var constantsGenerateCommand = new Command("constants-generate", "Generate C# constants from Dataverse metadata");

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
            getDefaultValue: () => "./Generated",
            description: "Output directory path");

        var namespaceOption = new Option<string>(
            aliases: new[] { "--namespace", "-n" },
            getDefaultValue: () => "MyCompany.Constants",
            description: "Root namespace for generated files");

        var singleFileOption = new Option<bool>(
            aliases: new[] { "--single-file" },
            getDefaultValue: () => false,
            description: "Generate single files (Entities.cs, OptionSets.cs) instead of multiple files");

        var configOption = new Option<string?>(
            aliases: new[] { "--config", "-c" },
            description: "JSON configuration file path (overrides other options)");

        var connectionStringOption = new Option<string?>(
            aliases: new[] { "--connection-string" },
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

        var includeEntitiesOption = new Option<bool>(
            aliases: new[] { "--include-entities" },
            getDefaultValue: () => true,
            description: "Include entity/table constants");

        var includeOptionSetsOption = new Option<bool>(
            aliases: new[] { "--include-option-sets" },
            getDefaultValue: () => true,
            description: "Include global option set/choice constants");

        var excludeEntitiesOption = new Option<string?>(
            aliases: new[] { "--exclude-entities" },
            description: "Comma-separated list of entity logical names to exclude");

        var excludeAttributesOption = new Option<string?>(
            aliases: new[] { "--exclude-attributes" },
            description: "Comma-separated list of attribute logical names to exclude");

        var attributePrefixOption = new Option<string?>(
            aliases: new[] { "--attribute-prefix" },
            description: "Only include attributes with this prefix (e.g., 'rob_')");

        var pascalCaseOption = new Option<bool>(
            aliases: new[] { "--pascal-case" },
            getDefaultValue: () => true,
            description: "Convert identifiers to PascalCase");

        constantsGenerateCommand.AddOption(urlOption);
        constantsGenerateCommand.AddOption(solutionOption);
        constantsGenerateCommand.AddOption(outputOption);
        constantsGenerateCommand.AddOption(namespaceOption);
        constantsGenerateCommand.AddOption(singleFileOption);
        constantsGenerateCommand.AddOption(configOption);
        constantsGenerateCommand.AddOption(connectionStringOption);
        constantsGenerateCommand.AddOption(clientIdOption);
        constantsGenerateCommand.AddOption(clientSecretOption);
        constantsGenerateCommand.AddOption(verboseOption);
        constantsGenerateCommand.AddOption(includeEntitiesOption);
        constantsGenerateCommand.AddOption(includeOptionSetsOption);
        constantsGenerateCommand.AddOption(excludeEntitiesOption);
        constantsGenerateCommand.AddOption(excludeAttributesOption);
        constantsGenerateCommand.AddOption(attributePrefixOption);
        constantsGenerateCommand.AddOption(pascalCaseOption);

        constantsGenerateCommand.SetHandler(async (context) =>
        {
            var configPath = context.ParseResult.GetValueForOption(configOption);
            var url = context.ParseResult.GetValueForOption(urlOption);
            var solution = context.ParseResult.GetValueForOption(solutionOption);
            var output = context.ParseResult.GetValueForOption(outputOption)!;
            var namespaceName = context.ParseResult.GetValueForOption(namespaceOption)!;
            var singleFile = context.ParseResult.GetValueForOption(singleFileOption);
            var connectionString = context.ParseResult.GetValueForOption(connectionStringOption);
            var clientId = context.ParseResult.GetValueForOption(clientIdOption);
            var clientSecret = context.ParseResult.GetValueForOption(clientSecretOption);
            var verbose = context.ParseResult.GetValueForOption(verboseOption);
            var includeEntities = context.ParseResult.GetValueForOption(includeEntitiesOption);
            var includeOptionSets = context.ParseResult.GetValueForOption(includeOptionSetsOption);
            var excludeEntities = context.ParseResult.GetValueForOption(excludeEntitiesOption);
            var excludeAttributes = context.ParseResult.GetValueForOption(excludeAttributesOption);
            var attributePrefix = context.ParseResult.GetValueForOption(attributePrefixOption);
            var pascalCase = context.ParseResult.GetValueForOption(pascalCaseOption);

            var logger = new ConsoleLogger { IsVerboseEnabled = verbose };

            // Create services
            var fileWriter = new FileWriter();
            var metadataMapper = new MetadataMapper();
            var identifierFormatter = new IdentifierFormatter(pascalCase);
            var templateGenerator = new CodeTemplateGenerator(true, true, identifierFormatter);
            var constantsFilter = new ConstantsFilter();
            var constantsGenerator = new ConstantsGenerator(templateGenerator, constantsFilter, fileWriter);

            // Load configuration
            ConstantsConfig? config = null;
            if (!string.IsNullOrWhiteSpace(configPath))
            {
                logger.LogVerbose($"Loading configuration from: {configPath}");
                config = await LoadConfigAsync(configPath, logger);
                if (config == null)
                {
                    context.ExitCode = 1;
                    return;
                }
            }

            // Create Dataverse client and schema extractor
            var dataverseClient = new DataverseClient(
                url ?? string.Empty,
                clientId,
                clientSecret,
                connectionString);
            var schemaExtractor = new SchemaExtractor(metadataMapper, dataverseClient);

            var command = new ConstantsCommand(logger, schemaExtractor, constantsFilter, constantsGenerator);
            context.ExitCode = await command.ExecuteAsync(
                config, url, solution, output, namespaceName, singleFile,
                connectionString, includeEntities, includeOptionSets,
                excludeEntities, excludeAttributes, attributePrefix, pascalCase);
        });

        return constantsGenerateCommand;
    }

    private static async Task<ConstantsConfig?> LoadConfigAsync(string configPath, IConsoleLogger logger)
    {
        try
        {
            var json = await File.ReadAllTextAsync(configPath);
            var config = JsonSerializer.Deserialize<ConstantsConfig>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (config == null)
            {
                logger.LogError("Failed to parse configuration file.");
                return null;
            }

            logger.LogVerbose($"Configuration loaded: {config}");
            return config;
        }
        catch (FileNotFoundException)
        {
            logger.LogError($"Configuration file not found: {configPath}");
            return null;
        }
        catch (JsonException ex)
        {
            logger.LogError($"Invalid JSON in configuration file: {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError($"Error loading configuration: {ex.Message}");
            return null;
        }
    }
}
