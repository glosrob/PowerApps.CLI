using System.CommandLine;
using System.Text.Json;
using PowerApps.CLI.Infrastructure;
using PowerApps.CLI.Models;
using PowerApps.CLI.Services;

namespace PowerApps.CLI.Commands;

/// <summary>
/// Handles the process-manage command.
/// </summary>
public class ProcessManageCommand
{
    private readonly IConsoleLogger _logger;
    private readonly IDataverseClient _dataverseClient;
    private readonly IProcessManager _processManager;
    private readonly IProcessReporter _processReporter;
    private readonly IFileWriter _fileWriter;

    public ProcessManageCommand(
        IConsoleLogger logger,
        IDataverseClient dataverseClient,
        IProcessManager processManager,
        IProcessReporter processReporter,
        IFileWriter fileWriter)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dataverseClient = dataverseClient ?? throw new ArgumentNullException(nameof(dataverseClient));
        _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
        _processReporter = processReporter ?? throw new ArgumentNullException(nameof(processReporter));
        _fileWriter = fileWriter ?? throw new ArgumentNullException(nameof(fileWriter));
    }

    public async Task<int> ExecuteAsync(string configPath, string output, bool dryRun)
    {
        try
        {
            _logger.LogInfo(dryRun
                ? "Starting process management (DRY RUN - no changes will be made)..."
                : "Starting process management...");

            // Load configuration
            _logger.LogInfo($"Loading configuration from: {configPath}");
            var config = await LoadConfigAsync(configPath);

            _logger.LogInfo($"Configuration loaded: {config.Solutions.Count} solution(s), {config.InactivePatterns.Count} inactive pattern(s)");
            _logger.LogInfoIfVerbose($"Max retries: {config.MaxRetries}");

            // Connect to environment
            _logger.LogInfo("Connecting to environment...");
            var envUrl = _dataverseClient.GetEnvironmentUrl();
            _logger.LogSuccess($"Connected to: {envUrl}");

            // Retrieve processes
            _logger.LogInfo("Retrieving processes...");
            var processes = _processManager.RetrieveProcesses(config.Solutions);
            _logger.LogInfo($"Found {processes.Count} process(es)");

            // Determine expected states
            _logger.LogInfo("Analyzing process states...");
            _processManager.DetermineExpectedStates(processes, config.InactivePatterns);

            var processesNeedingChange = processes.Count(p => p.CurrentState != p.ExpectedState);
            if (processesNeedingChange == 0)
            {
                _logger.LogSuccess("All processes are already in the expected state!");
            }
            else
            {
                _logger.LogInfo($"{processesNeedingChange} process(es) need state changes");
            }

            // Manage process states
            _logger.LogInfo(dryRun ? "Simulating state changes..." : "Applying state changes...");
            var summary = _processManager.ManageProcessStates(processes, dryRun, config.MaxRetries);
            summary.EnvironmentUrl = envUrl;

            // Generate report
            _logger.LogInfo($"Generating report: {output}");
            await _processReporter.GenerateReportAsync(summary, output);

            // Display summary
            _logger.LogInfo("");
            _logger.LogInfo("=== Summary ===");
            _logger.LogInfo($"Total Processes: {summary.TotalProcesses}");
            _logger.LogInfo($"Activated: {summary.ActivatedCount}");
            _logger.LogInfo($"Deactivated: {summary.DeactivatedCount}");
            _logger.LogInfo($"Unchanged: {summary.UnchangedCount}");

            if (summary.FailedCount > 0)
            {
                _logger.LogError($"Failed: {summary.FailedCount}");
            }
            else
            {
                _logger.LogInfo($"Failed: {summary.FailedCount}");
            }

            _logger.LogSuccess($"Report saved to: {output}");

            if (dryRun)
            {
                _logger.LogInfo("");
                _logger.LogInfo("This was a dry run. No changes were made.");
                _logger.LogInfo("Run without --dry-run to apply changes.");
            }

            // Set exit code
            if (summary.HasFailures)
            {
                _logger.LogWarning("Process management completed with failures");
                return 1;
            }

            _logger.LogSuccess("Process management completed successfully!");
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error during process management: {ex.Message}");
            _logger.LogVerbose(ex.ToString());
            return 1;
        }
    }

    public static Command CreateCliCommand()
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

            // Validate inputs
            var logger = new ConsoleLogger { IsVerboseEnabled = verbose };

            if (string.IsNullOrWhiteSpace(url) && string.IsNullOrWhiteSpace(connectionString))
            {
                logger.LogError("Either --url or --connection-string must be provided.");
                context.ExitCode = 1;
                return;
            }

            // Create service instances
            var fileWriter = new FileWriter();
            var dataverseClient = new DataverseClient(url ?? string.Empty, clientId, clientSecret, connectionString);
            var processManager = new ProcessManager(logger, dataverseClient);
            var processReporter = new ProcessReporter(fileWriter);

            var cmd = new ProcessManageCommand(logger, dataverseClient, processManager, processReporter, fileWriter);
            context.ExitCode = await cmd.ExecuteAsync(configPath, output, dryRun);
        });

        return command;
    }

    private async Task<ProcessManageConfig> LoadConfigAsync(string configPath)
    {
        if (!_fileWriter.FileExists(configPath))
        {
            throw new FileNotFoundException($"Configuration file not found: {configPath}");
        }

        var json = await _fileWriter.ReadTextAsync(configPath);
        var config = JsonSerializer.Deserialize<ProcessManageConfig>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return config ?? throw new InvalidOperationException("Failed to parse configuration file");
    }
}
