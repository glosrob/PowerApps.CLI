using PowerApps.CLI.Models;

namespace PowerApps.CLI.Services;

public interface IDataPatchReporter
{
    Task GenerateReportAsync(DataPatchSummary summary, string outputPath);
}
