using System.CommandLine;
using System.Text.Json;
using PowerApps.CLI.Infrastructure;
using PowerApps.CLI.Models;
using PowerApps.CLI.Services;

namespace PowerApps.CLI.Commands;

/// <summary>
/// Handles the process-manage command.
/// </summary>
public static class ProcessManageCommand
{
    public static Command CreateCommand()
    {
        var command = new Command("process-manage", "Manage Dataverse process states (workflows, flows, business rules)");

        var configOption = new Option<string>(
            aliases: new[] { "--config" },
            description: "Path to JSON configuration file specifying solutions and inactive patterns")
        {
            IsRequired = true
        };

        var urlOption = new Option<string?>(
            aliases: new[] { "--url" },
            description: "Environment URL (e.g., https://dev.crm.dynamics.com)");

        var connectionOption = new Option<string?>(
            aliases: new[] { "--connection-string" },
            description: "Environment connection string");

        var clientIdOption = new Option<string?>(
            aliases: new[] { "--client-id" },
            description: "Azure AD Application (Client) ID");

        var clientSecretOption = new Option<string?>(
            aliases: new[] { "--client-secret" },
            description: "Azure AD Application Client Secret");

        var dryRunOption = new Option<bool>(
            aliases: new[] { "--dry-run" },
            description: "Preview changes without actually modifying process states");

        var outputOption = new Option<string>(
            aliases: new[] { "--output", "-o" },
            getDefaultValue: () => "process-report.xlsx",
            description: "Output Excel file path for report");

        var verboseOption = new Option<bool>(
            aliases: new[] { "--verbose", "-v" },
            description: "Enable verbose output");

        command.AddOption(configOption);
        command.AddOption(urlOption);
        command.AddOption(connectionOption);
        command.AddOption(clientIdOption);
        command.AddOption(clientSecretOption);
        command.AddOption(dryRunOption);
        command.AddOption(outputOption);
        command.AddOption(verboseOption);

        command.SetHandler(async (context) =>
        {
            var configPath = context.ParseResult.GetValueForOption(configOption)!;
            var url = context.ParseResult.GetValueForOption(urlOption);
            var connectionString = context.ParseResult.GetValueForOption(connectionOption);
            var clientId = context.ParseResult.GetValueForOption(clientIdOption);
            var clientSecret = context.ParseResult.GetValueForOption(clientSecretOption);
            var dryRun = context.ParseResult.GetValueForOption(dryRunOption);
            var output = context.ParseResult.GetValueForOption(outputOption)!;
            var verbose = context.ParseResult.GetValueForOption(verboseOption);

            // Create service instances
            var logger = new ConsoleLogger { IsVerboseEnabled = verbose };
            var fileWriter = new FileWriter();
            var processManager = new ProcessManager(logger);
            var processReporter = new ProcessReporter(fileWriter);

            try
            {
                logger.LogInfo(dryRun
                    ? "Starting process management (DRY RUN - no changes will be made)..."
                    : "Starting process management...");

                // Validate inputs
                if (string.IsNullOrWhiteSpace(url) && string.IsNullOrWhiteSpace(connectionString))
                {
                    logger.LogError("Either --url or --connection-string must be provided.");
                    context.ExitCode = 1;
                    return;
                }

                // Load configuration
                logger.LogInfo($"Loading configuration from: {configPath}");
                var config = await LoadConfigAsync(configPath, fileWriter);

                logger.LogInfo($"Configuration loaded: {config.Solutions.Count} solution(s), {config.InactivePatterns.Count} inactive pattern(s)");
                logger.LogInfoIfVerbose($"Max retries: {config.MaxRetries}");

                // Connect to environment
                logger.LogInfo("Connecting to environment...");
                var dataverseClient = new DataverseClient(url ?? string.Empty, clientId, clientSecret, connectionString);
                var envUrl = dataverseClient.GetEnvironmentUrl();
                logger.LogSuccess($"Connected to: {envUrl}");

                // Retrieve processes
                logger.LogInfo("Retrieving processes...");
                var processes = processManager.RetrieveProcesses(dataverseClient, config.Solutions);
                logger.LogInfo($"Found {processes.Count} process(es)");

                // Determine expected states
                logger.LogInfo("Analyzing process states...");
                processManager.DetermineExpectedStates(processes, config.InactivePatterns);

                var processesNeedingChange = processes.Count(p => p.CurrentState != p.ExpectedState);
                if (processesNeedingChange == 0)
                {
                    logger.LogSuccess("All processes are already in the expected state!");
                }
                else
                {
                    logger.LogInfo($"{processesNeedingChange} process(es) need state changes");
                }

                // Manage process states
                logger.LogInfo(dryRun ? "Simulating state changes..." : "Applying state changes...");
                var summary = processManager.ManageProcessStates(dataverseClient, processes, dryRun, config.MaxRetries);
                summary.EnvironmentUrl = envUrl;

                // Generate report
                logger.LogInfo($"Generating report: {output}");
                await processReporter.GenerateReportAsync(summary, output);

                // Display summary
                logger.LogInfo("");
                logger.LogInfo("=== Summary ===");
                logger.LogInfo($"Total Processes: {summary.TotalProcesses}");
                logger.LogInfo($"Activated: {summary.ActivatedCount}");
                logger.LogInfo($"Deactivated: {summary.DeactivatedCount}");
                logger.LogInfo($"Unchanged: {summary.UnchangedCount}");
                
                if (summary.FailedCount > 0)
                {
                    logger.LogError($"Failed: {summary.FailedCount}");
                }
                else
                {
                    logger.LogInfo($"Failed: {summary.FailedCount}");
                }

                logger.LogSuccess($"Report saved to: {output}");

                if (dryRun)
                {
                    logger.LogInfo("");
                    logger.LogInfo("This was a dry run. No changes were made.");
                    logger.LogInfo("Run without --dry-run to apply changes.");
                }

                // Set exit code
                if (summary.HasFailures)
                {
                    logger.LogWarning("Process management completed with failures");
                    context.ExitCode = 1;
                }
                else
                {
                    logger.LogSuccess("Process management completed successfully!");
                    context.ExitCode = 0;
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Error during process management: {ex.Message}");
                logger.LogVerbose(ex.ToString());
                context.ExitCode = 1;
            }
        });

        return command;
    }

    private static async Task<ProcessManageConfig> LoadConfigAsync(string configPath, IFileWriter fileWriter)
    {
        if (!fileWriter.FileExists(configPath))
        {
            throw new FileNotFoundException($"Configuration file not found: {configPath}");
        }

        var json = await fileWriter.ReadTextAsync(configPath);
        var config = JsonSerializer.Deserialize<ProcessManageConfig>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return config ?? throw new InvalidOperationException("Failed to parse configuration file");
    }
}
