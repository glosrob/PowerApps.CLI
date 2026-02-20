using System.Text.Json;

namespace PowerApps.CLI.Models;

public class DataPatchConfig
{
    public List<PatchEntry> Patches { get; set; } = new();
}

public class PatchEntry
{
    public string Entity { get; set; } = string.Empty;
    public string KeyField { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string ValueField { get; set; } = string.Empty;
    public JsonElement Value { get; set; }
}
