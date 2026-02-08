using System.CommandLine;
using System.Text.Json;
using PowerApps.CLI.Infrastructure;
using PowerApps.CLI.Models;
using PowerApps.CLI.Services;

namespace PowerApps.CLI.Commands;

/// <summary>
/// Handles the refdata-compare command.
/// </summary>
public class RefDataCompareCommand
{
    private readonly IConsoleLogger _logger;
    private readonly IDataverseClient _sourceClient;
    private readonly IDataverseClient _targetClient;
    private readonly IRecordComparer _recordComparer;
    private readonly IComparisonReporter _comparisonReporter;
    private readonly IFileWriter _fileWriter;

    public RefDataCompareCommand(
        IConsoleLogger logger,
        IDataverseClient sourceClient,
        IDataverseClient targetClient,
        IRecordComparer recordComparer,
        IComparisonReporter comparisonReporter,
        IFileWriter fileWriter)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _sourceClient = sourceClient ?? throw new ArgumentNullException(nameof(sourceClient));
        _targetClient = targetClient ?? throw new ArgumentNullException(nameof(targetClient));
        _recordComparer = recordComparer ?? throw new ArgumentNullException(nameof(recordComparer));
        _comparisonReporter = comparisonReporter ?? throw new ArgumentNullException(nameof(comparisonReporter));
        _fileWriter = fileWriter ?? throw new ArgumentNullException(nameof(fileWriter));
    }

    public async Task<int> ExecuteAsync(string configPath, string output)
    {
        try
        {
            _logger.LogInfo("Starting reference data comparison...");

            // Load configuration
            _logger.LogInfo($"Loading configuration from: {configPath}");
            var config = await LoadConfigAsync(configPath);

            if (config.Tables.Count == 0)
            {
                _logger.LogError("No tables specified in configuration file.");
                return 1;
            }

            _logger.LogInfo($"Configuration loaded: {config.Tables.Count} table(s) to compare");

            // Connect to source environment
            _logger.LogInfo("Connecting to source environment...");
            var sourceEnv = _sourceClient.GetEnvironmentUrl();
            _logger.LogSuccess($"Connected to source: {sourceEnv}");

            // Connect to target environment
            _logger.LogInfo("Connecting to target environment...");
            var targetEnv = _targetClient.GetEnvironmentUrl();
            _logger.LogSuccess($"Connected to target: {targetEnv}");

            // Build exclusion list
            var excludeFields = BuildExcludeFieldSet(config);

            // Compare each table
            var comparisonResult = new ComparisonResult
            {
                SourceEnvironment = sourceEnv,
                TargetEnvironment = targetEnv,
                ComparisonDate = DateTime.UtcNow
            };

            foreach (var tableConfig in config.Tables)
            {
                _logger.LogInfo($"Comparing table: {tableConfig.LogicalName}");

                // Build FetchXML if filter provided
                string? fetchXml = null;
                if (!string.IsNullOrWhiteSpace(tableConfig.Filter))
                {
                    fetchXml = $@"<fetch><entity name='{tableConfig.LogicalName}'>{tableConfig.Filter}</entity></fetch>";
                }

                // Retrieve records from both environments
                _logger.LogInfoIfVerbose($"  Retrieving source records...");
                var sourceRecords = _sourceClient.RetrieveRecords(tableConfig.LogicalName, fetchXml);

                _logger.LogInfoIfVerbose($"  Retrieving target records...");
                var targetRecords = _targetClient.RetrieveRecords(tableConfig.LogicalName, fetchXml);

                _logger.LogInfoIfVerbose($"  Source: {sourceRecords.Entities.Count} record(s), Target: {targetRecords.Entities.Count} record(s)");

                // Build table-specific exclusions
                var tableExcludeFields = new HashSet<string>(excludeFields, StringComparer.OrdinalIgnoreCase);
                foreach (var field in tableConfig.ExcludeFields)
                {
                    tableExcludeFields.Add(field);
                }

                // Compare records
                var tableResult = _recordComparer.CompareRecords(
                    tableConfig.LogicalName,
                    sourceRecords,
                    targetRecords,
                    tableExcludeFields,
                    tableConfig.PrimaryNameField,
                    tableConfig.PrimaryIdField);

                // Use display name if provided
                if (!string.IsNullOrWhiteSpace(tableConfig.DisplayName))
                {
                    tableResult.TableName = tableConfig.DisplayName;
                }

                comparisonResult.TableResults.Add(tableResult);

                if (tableResult.HasDifferences)
                {
                    _logger.LogWarning($"  Found differences: New={tableResult.NewCount}, Modified={tableResult.ModifiedCount}, Deleted={tableResult.DeletedCount}");
                }
                else
                {
                    _logger.LogSuccess($"  No differences - table is in sync");
                }
            }

            // Generate report
            _logger.LogInfo($"Generating comparison report: {output}");
            await _comparisonReporter.GenerateReportAsync(comparisonResult, output);

            if (comparisonResult.HasAnyDifferences)
            {
                _logger.LogWarning($"Comparison complete: Differences found in {comparisonResult.TableResults.Count(t => t.HasDifferences)} table(s)");
            }
            else
            {
                _logger.LogSuccess("Comparison complete: All tables are in sync!");
            }

            _logger.LogSuccess($"Report saved to: {output}");
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error during comparison: {ex.Message}");
            _logger.LogVerbose(ex.ToString());
            return 1;
        }
    }

    public static Command CreateCliCommand()
    {
        var command = new Command("refdata-compare", "Compare reference data between source and target Dataverse environments");

        var configOption = new Option<string>(
            aliases: new[] { "--config" },
            description: "Path to JSON configuration file specifying tables and filters")
        {
            IsRequired = true
        };

        var sourceUrlOption = new Option<string?>(
            aliases: new[] { "--source-url" },
            description: "Source environment URL (e.g., https://dev.crm.dynamics.com)");

        var targetUrlOption = new Option<string?>(
            aliases: new[] { "--target-url" },
            description: "Target environment URL (e.g., https://test.crm.dynamics.com)");

        var sourceConnectionOption = new Option<string?>(
            aliases: new[] { "--source-connection" },
            description: "Source environment connection string");

        var targetConnectionOption = new Option<string?>(
            aliases: new[] { "--target-connection" },
            description: "Target environment connection string");

        var clientIdOption = new Option<string?>(
            aliases: new[] { "--client-id" },
            description: "Azure AD Application (Client) ID (used for both environments)");

        var clientSecretOption = new Option<string?>(
            aliases: new[] { "--client-secret" },
            description: "Azure AD Application Client Secret (used for both environments)");

        var outputOption = new Option<string>(
            aliases: new[] { "--output", "-o" },
            getDefaultValue: () => "refdata-comparison.xlsx",
            description: "Output Excel file path");

        var verboseOption = new Option<bool>(
            aliases: new[] { "--verbose", "-v" },
            description: "Enable verbose output");

        command.AddOption(configOption);
        command.AddOption(sourceUrlOption);
        command.AddOption(targetUrlOption);
        command.AddOption(sourceConnectionOption);
        command.AddOption(targetConnectionOption);
        command.AddOption(clientIdOption);
        command.AddOption(clientSecretOption);
        command.AddOption(outputOption);
        command.AddOption(verboseOption);

        command.SetHandler(async (context) =>
        {
            var configPath = context.ParseResult.GetValueForOption(configOption)!;
            var sourceUrl = context.ParseResult.GetValueForOption(sourceUrlOption);
            var targetUrl = context.ParseResult.GetValueForOption(targetUrlOption);
            var sourceConnection = context.ParseResult.GetValueForOption(sourceConnectionOption);
            var targetConnection = context.ParseResult.GetValueForOption(targetConnectionOption);
            var clientId = context.ParseResult.GetValueForOption(clientIdOption);
            var clientSecret = context.ParseResult.GetValueForOption(clientSecretOption);
            var output = context.ParseResult.GetValueForOption(outputOption)!;
            var verbose = context.ParseResult.GetValueForOption(verboseOption);

            var logger = new ConsoleLogger { IsVerboseEnabled = verbose };

            // Validate inputs
            if (string.IsNullOrWhiteSpace(sourceUrl) && string.IsNullOrWhiteSpace(sourceConnection))
            {
                logger.LogError("Either --source-url or --source-connection must be provided.");
                context.ExitCode = 1;
                return;
            }

            if (string.IsNullOrWhiteSpace(targetUrl) && string.IsNullOrWhiteSpace(targetConnection))
            {
                logger.LogError("Either --target-url or --target-connection must be provided.");
                context.ExitCode = 1;
                return;
            }

            // Create service instances
            var sourceDataverseClient = new DataverseClient(sourceUrl ?? string.Empty, clientId, clientSecret, sourceConnection);
            var targetDataverseClient = new DataverseClient(targetUrl ?? string.Empty, clientId, clientSecret, targetConnection);
            var recordComparer = new RecordComparer();
            var fileWriter = new FileWriter();
            var comparisonReporter = new ComparisonReporter(fileWriter);

            var cmd = new RefDataCompareCommand(logger, sourceDataverseClient, targetDataverseClient, recordComparer, comparisonReporter, fileWriter);
            context.ExitCode = await cmd.ExecuteAsync(configPath, output);
        });

        return command;
    }

    private async Task<RefDataCompareConfig> LoadConfigAsync(string configPath)
    {
        if (!_fileWriter.FileExists(configPath))
        {
            throw new FileNotFoundException($"Configuration file not found: {configPath}");
        }

        var json = await _fileWriter.ReadTextAsync(configPath);
        var config = JsonSerializer.Deserialize<RefDataCompareConfig>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return config ?? throw new InvalidOperationException("Failed to parse configuration file");
    }

    private static HashSet<string> BuildExcludeFieldSet(RefDataCompareConfig config)
    {
        var excludeFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Add system fields if enabled
        if (config.ExcludeSystemFields)
        {
            excludeFields.UnionWith(new[]
            {
                "createdby", "createdon", "createdonbehalfby",
                "modifiedby", "modifiedon", "modifiedonbehalfby",
                "ownerid", "owninguser", "owningteam", "owningbusinessunit",
                "versionnumber", "importsequencenumber", "overriddencreatedon"
            });
        }

        // Add global excludes
        foreach (var field in config.GlobalExcludeFields)
        {
            excludeFields.Add(field);
        }

        return excludeFields;
    }
}
