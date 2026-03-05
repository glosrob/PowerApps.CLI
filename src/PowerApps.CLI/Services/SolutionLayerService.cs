using PowerApps.CLI.Infrastructure;
using PowerApps.CLI.Models;

namespace PowerApps.CLI.Services;

/// <summary>
/// Queries Dataverse for solution component layers and identifies components with unmanaged layers.
/// </summary>
public class SolutionLayerService : ISolutionLayerService
{
    private readonly IDataverseClient _client;

    public SolutionLayerService(IDataverseClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public async Task<SolutionLayerResult> GetUnmanagedLayersAsync(string solutionName, Action<int, int, int>? batchProgress = null, Action<string>? phaseLog = null)
    {
        var result = new SolutionLayerResult
        {
            SolutionName = solutionName,
            EnvironmentUrl = _client.GetEnvironmentUrl(),
            ReportDate = DateTime.UtcNow
        };

        var layers = await _client.GetSolutionComponentLayersAsync(solutionName, batchProgress, phaseLog);

        // Group layers by component ID so we can inspect the full stack per component.
        var componentGroups = layers.Entities
            .GroupBy(e => e.Contains("msdyn_componentid") ? e["msdyn_componentid"].ToString() ?? string.Empty : string.Empty)
            .Where(g => !string.IsNullOrEmpty(g.Key))
            .ToList();

        result.TotalComponentsChecked = componentGroups.Count;

        foreach (var group in componentGroups)
        {
            var topLayer = group.OrderByDescending(e => e.GetAttributeValue<int>("msdyn_order")).First();
            var topSolutionName = topLayer.GetAttributeValue<string>("msdyn_solutionname") ?? string.Empty;

            // "Active" is the unmanaged customisations bucket in Dataverse.
            if (!topSolutionName.Equals("Active", StringComparison.OrdinalIgnoreCase))
                continue;

            var componentName = topLayer.GetAttributeValue<string>("msdyn_name") ?? group.Key;
            var componentType = topLayer.GetAttributeValue<string>("msdyn_solutioncomponentname") ?? "Unknown";

            var allLayerNames = group
                .OrderBy(e => e.GetAttributeValue<int>("msdyn_order"))
                .Select(e => e.GetAttributeValue<string>("msdyn_solutionname") ?? "Unknown")
                .ToList();

            result.LayeredComponents.Add(new LayeredComponent
            {
                ComponentName = componentName,
                ComponentType = componentType,
                UnmanagedLayerOwner = "Active (Unmanaged Customisations)",
                AllLayers = allLayerNames
            });
        }

        // Sort by component type then name for a consistent, readable report.
        result.LayeredComponents = result.LayeredComponents
            .OrderBy(c => c.ComponentType)
            .ThenBy(c => c.ComponentName)
            .ToList();

        return result;
    }
}
