using PowerApps.CLI.Models;

namespace PowerApps.CLI.Services;

public interface ISolutionLayerService
{
    Task<SolutionLayerResult> GetUnmanagedLayersAsync(string solutionName, Action<int, int, int>? batchProgress = null, Action<string>? phaseLog = null);
}
