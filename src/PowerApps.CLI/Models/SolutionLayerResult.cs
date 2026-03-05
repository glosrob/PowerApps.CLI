namespace PowerApps.CLI.Models;

public class SolutionLayerResult
{
    public string SolutionName { get; set; } = string.Empty;
    public string EnvironmentUrl { get; set; } = string.Empty;
    public DateTime ReportDate { get; set; }
    public int TotalComponentsChecked { get; set; }
    public List<LayeredComponent> LayeredComponents { get; set; } = new();

    public bool HasUnmanagedLayers => LayeredComponents.Any();
}

public class LayeredComponent
{
    public string ComponentName { get; set; } = string.Empty;
    public string ComponentType { get; set; } = string.Empty;
    public string UnmanagedLayerOwner { get; set; } = string.Empty;
    public List<string> AllLayers { get; set; } = new();
}
