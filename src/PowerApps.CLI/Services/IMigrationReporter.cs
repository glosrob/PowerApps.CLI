using PowerApps.CLI.Models;

namespace PowerApps.CLI.Services;

public interface IMigrationReporter
{
    Task GenerateReportAsync(MigrationSummary summary, string outputPath);
}
