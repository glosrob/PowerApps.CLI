using System.CommandLine;
using PowerApps.CLI.Infrastructure;
using PowerApps.CLI.Services;

namespace PowerApps.CLI.Commands;

/// <summary>
/// Handles the solution-layers command — reports unmanaged layers on a solution's components post-deployment.
/// </summary>
public class SolutionLayersCommand
{
    private readonly IConsoleLogger _logger;
    private readonly IDataverseClient _client;
    private readonly ISolutionLayerService _service;
    private readonly ISolutionLayerReporter _reporter;

    public SolutionLayersCommand(
        IConsoleLogger logger,
        IDataverseClient client,
        ISolutionLayerService service,
        ISolutionLayerReporter reporter)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _reporter = reporter ?? throw new ArgumentNullException(nameof(reporter));
    }

    public async Task<int> ExecuteAsync(string solution, string output)
    {
        try
        {
            _logger.LogInfo($"Checking solution layers for: {solution}");

            var envUrl = _client.GetEnvironmentUrl();
            _logger.LogSuccess($"Connected to: {envUrl}");

            _logger.LogInfo("Retrieving solution components...");
            var componentCountLogged = false;
            var result = await _service.GetUnmanagedLayersAsync(solution, (componentCount, current, total) =>
            {
                if (!componentCountLogged)
                {
                    _logger.LogVerbose($"  {componentCount} component(s) to check (includes attributes expanded from entity metadata). Querying layer data...");
                    componentCountLogged = true;
                }
                if (current % 50 == 0 || current == total)
                    _logger.LogInfo($"  Querying component layers: {current}/{total}...");
            });
            _logger.LogInfo($"Layer analysis complete. {result.TotalComponentsChecked} component(s) checked.");

            if (!result.HasUnmanagedLayers)
            {
                _logger.LogSuccess("No unmanaged layers detected. All components are clean.");
            }
            else
            {
                _logger.LogWarning($"{result.LayeredComponents.Count} component(s) have unmanaged layers:");
                foreach (var component in result.LayeredComponents)
                {
                    _logger.LogWarning($"  [{component.ComponentType}] {component.ComponentName}");
                }
            }

            _logger.LogInfo($"Generating report: {output}");
            await _reporter.GenerateReportAsync(result, output);
            _logger.LogSuccess($"Report saved to: {output}");

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error checking solution layers: {ex.Message}");
            _logger.LogVerbose(ex.ToString());
            return 1;
        }
    }

    public static Command CreateCliCommand()
    {
        var command = new Command("solution-layers", "Report unmanaged layers on a solution's components post-deployment");

        var solutionOption = new Option<string>(
            aliases: new[] { "--solution", "-s" },
            description: "Unique name of the solution to inspect")
        {
            IsRequired = true
        };

        var outputOption = new Option<string>(
            aliases: new[] { "--output", "-o" },
            getDefaultValue: () => "solution-layers.xlsx",
            description: "Output Excel report file path");

        var urlOption = new Option<string?>(
            aliases: new[] { "--url", "-u" },
            description: "Environment URL (e.g. https://yourorg.crm.dynamics.com)");

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

        command.AddOption(solutionOption);
        command.AddOption(outputOption);
        command.AddOption(urlOption);
        command.AddOption(connectionOption);
        command.AddOption(clientIdOption);
        command.AddOption(clientSecretOption);
        command.AddOption(verboseOption);

        command.SetHandler(async (context) =>
        {
            var solution    = context.ParseResult.GetValueForOption(solutionOption)!;
            var output      = context.ParseResult.GetValueForOption(outputOption)!;
            var url         = context.ParseResult.GetValueForOption(urlOption);
            var connString  = context.ParseResult.GetValueForOption(connectionOption);
            var clientId    = context.ParseResult.GetValueForOption(clientIdOption);
            var clientSecret = context.ParseResult.GetValueForOption(clientSecretOption);
            var verbose     = context.ParseResult.GetValueForOption(verboseOption);

            var logger = new ConsoleLogger { IsVerboseEnabled = verbose };

            if (string.IsNullOrWhiteSpace(url) && string.IsNullOrWhiteSpace(connString))
            {
                logger.LogError("Either --url or --connection-string must be provided.");
                context.ExitCode = 1;
                return;
            }

            var fileWriter = new FileWriter();
            var client = new DataverseClient(url ?? string.Empty, clientId, clientSecret, connString);
            var service = new SolutionLayerService(client);
            var reporter = new SolutionLayerReporter(fileWriter);

            var cmd = new SolutionLayersCommand(logger, client, service, reporter);
            context.ExitCode = await cmd.ExecuteAsync(solution, output);
        });

        return command;
    }
}
