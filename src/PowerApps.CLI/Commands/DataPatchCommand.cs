using System.CommandLine;
using System.Text.Json;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using PowerApps.CLI.Infrastructure;
using PowerApps.CLI.Models;
using PowerApps.CLI.Services;

namespace PowerApps.CLI.Commands;

/// <summary>
/// Handles the data-patch command — targeted field-level updates to Dataverse records.
/// </summary>
public class DataPatchCommand
{
    private readonly IConsoleLogger _logger;
    private readonly IDataverseClient _client;
    private readonly IDataPatchReporter _reporter;
    private readonly IFileWriter _fileWriter;

    public DataPatchCommand(
        IConsoleLogger logger,
        IDataverseClient client,
        IDataPatchReporter reporter,
        IFileWriter fileWriter)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _reporter = reporter ?? throw new ArgumentNullException(nameof(reporter));
        _fileWriter = fileWriter ?? throw new ArgumentNullException(nameof(fileWriter));
    }

    public async Task<int> ExecuteAsync(string? configPath, string? configJson, string output)
    {
        try
        {
            _logger.LogInfo("Starting data patch...");

            var config = configJson is not null
                ? ParseConfig(configJson)
                : await LoadConfigAsync(configPath!);

            if (config.Patches.Count == 0)
            {
                _logger.LogError("No patches defined in configuration.");
                return 1;
            }

            _logger.LogInfo($"Configuration loaded: {config.Patches.Count} patch(es) to apply");

            _logger.LogInfo("Connecting to environment...");
            var envUrl = _client.GetEnvironmentUrl();
            _logger.LogSuccess($"Connected to: {envUrl}");

            var summary = new DataPatchSummary { EnvironmentUrl = envUrl };

            foreach (var patch in config.Patches)
            {
                _logger.LogInfo($"Patching {patch.Entity} where {patch.KeyField} = '{patch.Key}'...");
                var result = ApplyPatch(patch);
                summary.Results.Add(result);

                switch (result.Status)
                {
                    case PatchStatus.Updated:
                        _logger.LogSuccess($"  Updated {patch.ValueField}: '{result.OldValue}' -> '{result.NewValue}'");
                        break;
                    case PatchStatus.Unchanged:
                        _logger.LogInfo($"  Unchanged — {patch.ValueField} already set to '{result.OldValue}'");
                        break;
                    case PatchStatus.NotFound:
                        _logger.LogError($"  Not found: no record matched {patch.KeyField} = '{patch.Key}'");
                        break;
                    case PatchStatus.AmbiguousMatch:
                        _logger.LogError($"  Ambiguous: multiple records matched {patch.KeyField} = '{patch.Key}'");
                        break;
                    case PatchStatus.Error:
                        _logger.LogError($"  Error: {result.ErrorMessage}");
                        break;
                }
            }

            _logger.LogInfo("");
            _logger.LogInfo("=== Summary ===");
            _logger.LogInfo($"Updated:   {summary.UpdatedCount}");
            _logger.LogInfo($"Unchanged: {summary.UnchangedCount}");

            if (summary.HasFailures)
                _logger.LogError($"Failed:    {summary.FailedCount}");
            else
                _logger.LogInfo($"Failed:    {summary.FailedCount}");

            _logger.LogInfo($"Generating report: {output}");
            await _reporter.GenerateReportAsync(summary, output);
            _logger.LogSuccess($"Report saved to: {output}");

            if (summary.HasFailures)
            {
                _logger.LogWarning("Data patch completed with failures.");
                return 1;
            }

            _logger.LogSuccess("Data patch completed successfully!");
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error during data patch: {ex.Message}");
            _logger.LogVerbose(ex.ToString());
            return 1;
        }
    }

    private PatchResult ApplyPatch(PatchEntry patch)
    {
        var result = new PatchResult
        {
            Entity = patch.Entity,
            Key = patch.Key,
            Field = patch.ValueField,
            NewValue = ValueToString(patch.Value)
        };

        try
        {
            var fetchXml = $@"<fetch top='2'>
  <entity name='{patch.Entity}'>
    <attribute name='{patch.ValueField}' />
    <filter>
      <condition attribute='{patch.KeyField}' operator='eq' value='{patch.Key}' />
    </filter>
  </entity>
</fetch>";

            var records = _client.RetrieveRecordsByFetchXml(fetchXml);

            if (records.Entities.Count == 0)
            {
                result.Status = PatchStatus.NotFound;
                return result;
            }

            if (records.Entities.Count > 1)
            {
                result.Status = PatchStatus.AmbiguousMatch;
                return result;
            }

            var record = records.Entities[0];
            var currentValue = record.Contains(patch.ValueField)
                ? record[patch.ValueField]?.ToString()
                : null;

            result.OldValue = currentValue;

            if (string.Equals(currentValue, result.NewValue, StringComparison.Ordinal))
            {
                result.Status = PatchStatus.Unchanged;
                return result;
            }

            var updateEntity = new Entity(patch.Entity, record.Id);
            updateEntity[patch.ValueField] = ToTypedValue(patch.Value);
            _client.Execute(new UpdateRequest { Target = updateEntity });

            result.Status = PatchStatus.Updated;
            return result;
        }
        catch (Exception ex)
        {
            result.Status = PatchStatus.Error;
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

    private static object? ToTypedValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String  => element.GetString(),
        JsonValueKind.True    => true,
        JsonValueKind.False   => false,
        JsonValueKind.Number  => element.TryGetInt32(out var i) ? (object)i : element.GetDouble(),
        JsonValueKind.Null    => null,
        _                     => element.ToString()
    };

    private static string? ValueToString(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Null => null,
        _                  => ToTypedValue(element)?.ToString()
    };

    private async Task<DataPatchConfig> LoadConfigAsync(string configPath)
    {
        if (!_fileWriter.FileExists(configPath))
            throw new FileNotFoundException($"Configuration file not found: {configPath}");

        var json = await _fileWriter.ReadTextAsync(configPath);
        return ParseConfig(json);
    }

    private static DataPatchConfig ParseConfig(string json)
    {
        var config = JsonSerializer.Deserialize<DataPatchConfig>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        return config ?? throw new InvalidOperationException("Failed to parse configuration.");
    }

    public static Command CreateCliCommand()
    {
        var command = new Command("data-patch", "Apply targeted field-level updates to Dataverse records");

        var configOption = new Option<string?>(
            aliases: new[] { "--config" },
            description: "Path to JSON configuration file");

        var configJsonOption = new Option<string?>(
            aliases: new[] { "--config-json" },
            description: "Inline JSON configuration (e.g. retrieved from Key Vault in a pipeline)");

        var outputOption = new Option<string>(
            aliases: new[] { "--output", "-o" },
            getDefaultValue: () => "data-patch-report.xlsx",
            description: "Output Excel report file path");

        var urlOption = new Option<string?>(
            aliases: new[] { "--url" },
            description: "Environment URL (e.g., https://yourorg.crm.dynamics.com)");

        var connectionOption = new Option<string?>(
            aliases: new[] { "--connection-string" },
            description: "Environment connection string");

        var clientIdOption = new Option<string?>(
            aliases: new[] { "--client-id" },
            description: "Azure AD Application (Client) ID");

        var clientSecretOption = new Option<string?>(
            aliases: new[] { "--client-secret" },
            description: "Azure AD Application Client Secret");

        var verboseOption = new Option<bool>(
            aliases: new[] { "--verbose", "-v" },
            description: "Enable verbose output");

        command.AddOption(configOption);
        command.AddOption(configJsonOption);
        command.AddOption(outputOption);
        command.AddOption(urlOption);
        command.AddOption(connectionOption);
        command.AddOption(clientIdOption);
        command.AddOption(clientSecretOption);
        command.AddOption(verboseOption);

        command.SetHandler(async (context) =>
        {
            var configPath  = context.ParseResult.GetValueForOption(configOption);
            var configJson  = context.ParseResult.GetValueForOption(configJsonOption);
            var output      = context.ParseResult.GetValueForOption(outputOption)!;
            var url         = context.ParseResult.GetValueForOption(urlOption);
            var connString  = context.ParseResult.GetValueForOption(connectionOption);
            var clientId    = context.ParseResult.GetValueForOption(clientIdOption);
            var clientSecret = context.ParseResult.GetValueForOption(clientSecretOption);
            var verbose     = context.ParseResult.GetValueForOption(verboseOption);

            var logger = new ConsoleLogger { IsVerboseEnabled = verbose };

            if (configPath is null && configJson is null)
            {
                logger.LogError("Either --config or --config-json must be provided.");
                context.ExitCode = 1;
                return;
            }

            if (configPath is not null && configJson is not null)
            {
                logger.LogError("--config and --config-json are mutually exclusive.");
                context.ExitCode = 1;
                return;
            }

            if (string.IsNullOrWhiteSpace(url) && string.IsNullOrWhiteSpace(connString))
            {
                logger.LogError("Either --url or --connection-string must be provided.");
                context.ExitCode = 1;
                return;
            }

            var fileWriter = new FileWriter();
            var client = new DataverseClient(url ?? string.Empty, clientId, clientSecret, connString);
            var reporter = new DataPatchReporter(fileWriter);

            var cmd = new DataPatchCommand(logger, client, reporter, fileWriter);
            context.ExitCode = await cmd.ExecuteAsync(configPath, configJson, output);
        });

        return command;
    }
}
