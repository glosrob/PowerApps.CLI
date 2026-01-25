namespace PowerApps.CLI.Models;

public class OptionSetSchema
{
    public string? Name { get; set; }
    public string? DisplayName { get; set; }
    public bool IsGlobal { get; set; }
    public List<OptionSchema> Options { get; set; } = new();

    public override string ToString()
    {
        return string.IsNullOrEmpty(DisplayName ?? Name) ? "Unknown OptionSet" : (DisplayName ?? Name ?? "Unknown OptionSet");
    }
}
