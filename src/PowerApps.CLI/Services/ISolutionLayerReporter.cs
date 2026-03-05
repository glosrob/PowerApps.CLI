using PowerApps.CLI.Models;

namespace PowerApps.CLI.Services;

public interface ISolutionLayerReporter
{
    Task GenerateReportAsync(SolutionLayerResult result, string outputPath);
}
