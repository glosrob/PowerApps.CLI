using System.CommandLine;
using System.Text.Json;
using PowerApps.CLI.Infrastructure;
using PowerApps.CLI.Models;
using PowerApps.CLI.Services;

namespace PowerApps.CLI.Commands;

public class RefDataMigrateCommand
{
    private readonly IConsoleLogger _logger;
    private readonly IDataverseClient _sourceClient;
    private readonly IDataverseClient _targetClient;
    private readonly IRefDataMigrator _migrator;
    private readonly IMigrationReporter _reporter;
    private readonly IFileWriter _fileWriter;

    public RefDataMigrateCommand(
        IConsoleLogger logger,
        IDataverseClient sourceClient,
        IDataverseClient targetClient,
        IRefDataMigrator migrator,
        IMigrationReporter reporter,
        IFileWriter fileWriter)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _sourceClient = sourceClient ?? throw new ArgumentNullException(nameof(sourceClient));
        _targetClient = targetClient ?? throw new ArgumentNullException(nameof(targetClient));
        _migrator = migrator ?? throw new ArgumentNullException(nameof(migrator));
        _reporter = reporter ?? throw new ArgumentNullException(nameof(reporter));
        _fileWriter = fileWriter ?? throw new ArgumentNullException(nameof(fileWriter));
    }

    public async Task<int> ExecuteAsync(string configPath, string output, bool dryRun, bool force = false)
    {
        try
        {
            _logger.LogInfo("Starting reference data migration...");

            if (dryRun)
            {
                _logger.LogWarning("DRY RUN MODE - no changes will be made to the target environment");
            }

            // Load configuration
            _logger.LogInfo($"Loading configuration from: {configPath}");
            var config = await LoadConfigAsync(configPath);

            if (config.Tables.Count == 0)
            {
                _logger.LogError("No tables specified in configuration file.");
                return 1;
            }

            _logger.LogInfo($"Configuration loaded: {config.Tables.Count} table(s) to migrate (batch size: {config.BatchSize})");

            // Log environment info
            var sourceEnv = _sourceClient.GetEnvironmentUrl();
            var targetEnv = _targetClient.GetEnvironmentUrl();
            _logger.LogSuccess($"Source: {sourceEnv}");
            _logger.LogSuccess($"Target: {targetEnv}");

            // Execute migration
            var summary = await _migrator.MigrateAsync(config, dryRun, force);

            // Generate report
            _logger.LogInfo($"Generating migration report: {output}");
            await _reporter.GenerateReportAsync(summary, output);

            // Log summary
            _logger.LogInfo("");
            _logger.LogInfo($"Migration {(dryRun ? "preview" : "complete")} in {summary.Duration:mm\\:ss\\.fff}");
            _logger.LogInfo($"  Total records: {summary.TotalRecords}");
            _logger.LogInfo($"  Upserted: {summary.TotalUpserted}");
            _logger.LogInfo($"  Lookups patched: {summary.TotalLookupsPatched}");
            _logger.LogInfo($"  State changes: {summary.TotalStateChanges}");
            _logger.LogInfo($"  Skipped (unchanged): {summary.TotalSkipped}");
            if (summary.ManyToManyResults.Count > 0)
            {
                _logger.LogInfo($"  N:N associated: {summary.TotalAssociated}");
                _logger.LogInfo($"  N:N disassociated: {summary.TotalDisassociated}");
            }

            if (summary.HasErrors)
            {
                _logger.LogWarning($"  Errors: {summary.TotalErrors}");
            }

            _logger.LogSuccess($"Report saved to: {output}");
            return summary.HasErrors ? 1 : 0;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error during migration: {ex.Message}");
            _logger.LogVerbose(ex.ToString());
            return 1;
        }
    }

    public static Command CreateCliCommand()
    {
        var command = new Command("refdata-migrate", "Migrate reference data from source to target Dataverse environment");

        var configOption = new Option<string>(
            aliases: new[] { "--config" },
            description: "Path to JSON configuration file specifying tables to migrate")
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
            getDefaultValue: () => "migration-report.xlsx",
            description: "Output Excel report file path");

        var dryRunOption = new Option<bool>(
            aliases: new[] { "--dry-run" },
            description: "Preview mode - no changes will be made to the target environment");

        var forceOption = new Option<bool>(
            aliases: new[] { "--force" },
            description: "Force full sync - push all records regardless of whether they have changed");

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
        command.AddOption(dryRunOption);
        command.AddOption(forceOption);
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
            var dryRun = context.ParseResult.GetValueForOption(dryRunOption);
            var force = context.ParseResult.GetValueForOption(forceOption);
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
            var fileWriter = new FileWriter();
            var migrator = new RefDataMigrator(logger, sourceDataverseClient, targetDataverseClient);
            var reporter = new MigrationReporter(fileWriter);

            var cmd = new RefDataMigrateCommand(logger, sourceDataverseClient, targetDataverseClient, migrator, reporter, fileWriter);
            context.ExitCode = await cmd.ExecuteAsync(configPath, output, dryRun, force);
        });

        return command;
    }

    private async Task<RefDataMigrateConfig> LoadConfigAsync(string configPath)
    {
        if (!_fileWriter.FileExists(configPath))
        {
            throw new FileNotFoundException($"Configuration file not found: {configPath}");
        }

        var json = await _fileWriter.ReadTextAsync(configPath);
        var config = JsonSerializer.Deserialize<RefDataMigrateConfig>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return config ?? throw new InvalidOperationException("Failed to parse configuration file");
    }
}
