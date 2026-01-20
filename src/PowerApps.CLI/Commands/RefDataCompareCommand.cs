using System.CommandLine;
using System.Text.Json;
using PowerApps.CLI.Infrastructure;
using PowerApps.CLI.Models;
using PowerApps.CLI.Services;

namespace PowerApps.CLI.Commands;

/// <summary>
/// Handles the refdata-compare command.
/// </summary>
public static class RefDataCompareCommand
{
    public static Command CreateCommand()
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

            // Create service instances
            var logger = new ConsoleLogger { IsVerboseEnabled = verbose };
            var dataverseClient = new DataverseClient();
            var recordComparer = new RecordComparer();
            var fileWriter = new FileWriter();
            var comparisonReporter = new ComparisonReporter(fileWriter);

            try
            {
                logger.LogInfo("Starting reference data comparison...");

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

                // Load configuration
                logger.LogInfo($"Loading configuration from: {configPath}");
                var config = await LoadConfigAsync(configPath);

                if (config.Tables.Count == 0)
                {
                    logger.LogError("No tables specified in configuration file.");
                    context.ExitCode = 1;
                    return;
                }

                logger.LogInfo($"Configuration loaded: {config.Tables.Count} table(s) to compare");

                // Connect to source environment
                logger.LogInfo("Connecting to source environment...");
                var sourceClient = await dataverseClient.ConnectAsync(
                    sourceUrl ?? string.Empty,
                    clientId,
                    clientSecret,
                    sourceConnection);
                var sourceEnv = dataverseClient.GetEnvironmentUrl(sourceClient);
                logger.LogSuccess($"Connected to source: {sourceEnv}");

                // Connect to target environment
                logger.LogInfo("Connecting to target environment...");
                var targetClient = await dataverseClient.ConnectAsync(
                    targetUrl ?? string.Empty,
                    clientId,
                    clientSecret,
                    targetConnection);
                var targetEnv = dataverseClient.GetEnvironmentUrl(targetClient);
                logger.LogSuccess($"Connected to target: {targetEnv}");

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
                    logger.LogInfo($"Comparing table: {tableConfig.LogicalName}");

                    // Build FetchXML if filter provided
                    string? fetchXml = null;
                    if (!string.IsNullOrWhiteSpace(tableConfig.Filter))
                    {
                        fetchXml = $@"<fetch><entity name='{tableConfig.LogicalName}'>{tableConfig.Filter}</entity></fetch>";
                    }

                    // Retrieve records from both environments
                    if (verbose)
                    {
                        logger.LogInfo($"  Retrieving source records...");
                    }
                    var sourceRecords = dataverseClient.RetrieveRecords(sourceClient, tableConfig.LogicalName, fetchXml);

                    if (verbose)
                    {
                        logger.LogInfo($"  Retrieving target records...");
                    }
                    var targetRecords = dataverseClient.RetrieveRecords(targetClient, tableConfig.LogicalName, fetchXml);

                    if (verbose)
                    {
                        logger.LogInfo($"  Source: {sourceRecords.Entities.Count} record(s), Target: {targetRecords.Entities.Count} record(s)");
                    }

                    // Build table-specific exclusions
                    var tableExcludeFields = new HashSet<string>(excludeFields, StringComparer.OrdinalIgnoreCase);
                    foreach (var field in tableConfig.ExcludeFields)
                    {
                        tableExcludeFields.Add(field);
                    }

                    // Compare records
                    var tableResult = recordComparer.CompareRecords(
                        tableConfig.LogicalName,
                        sourceRecords,
                        targetRecords,
                        tableExcludeFields);

                    comparisonResult.TableResults.Add(tableResult);

                    if (tableResult.HasDifferences)
                    {
                        logger.LogWarning($"  Found differences: New={tableResult.NewCount}, Modified={tableResult.ModifiedCount}, Deleted={tableResult.DeletedCount}");
                    }
                    else
                    {
                        logger.LogSuccess($"  No differences - table is in sync");
                    }
                }

                // Generate report
                logger.LogInfo($"Generating comparison report: {output}");
                await comparisonReporter.GenerateReportAsync(comparisonResult, output);

                if (comparisonResult.HasAnyDifferences)
                {
                    logger.LogWarning($"Comparison complete: Differences found in {comparisonResult.TableResults.Count(t => t.HasDifferences)} table(s)");
                }
                else
                {
                    logger.LogSuccess("Comparison complete: All tables are in sync!");
                }

                logger.LogSuccess($"Report saved to: {output}");
            }
            catch (Exception ex)
            {
                logger.LogError($"Error during comparison: {ex.Message}");
                if (verbose)
                {
                    logger.LogError(ex.ToString());
                }
                context.ExitCode = 1;
            }
        });

        return command;
    }

    private static async Task<RefDataCompareConfig> LoadConfigAsync(string configPath)
    {
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException($"Configuration file not found: {configPath}");
        }

        var json = await File.ReadAllTextAsync(configPath);
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
            // These are also handled in RecordComparer, but we include them here for clarity
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
