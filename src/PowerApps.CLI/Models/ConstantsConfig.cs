namespace PowerApps.CLI.Models;

/// <summary>
/// Configuration for constants generation.
/// </summary>
public class ConstantsConfig
{
    public bool SingleFile { get; set; }
    public bool IncludeEntities { get; set; } = true;
    public bool IncludeGlobalOptionSets { get; set; } = true;
    public bool IncludeReferenceData { get; set; }
    public bool IncludeComments { get; set; } = true;
    public bool IncludeRelationships { get; set; } = true;
    public bool PascalCaseConversion { get; set; } = true;
    public string? AttributePrefix { get; set; }
    public List<string> ExcludeAttributes { get; set; } = new();
    public List<string> ExcludeEntities { get; set; } = new();

    public override string ToString()
    {
        var parts = new List<string>();
        if (IncludeEntities) parts.Add("Entities");
        if (IncludeGlobalOptionSets) parts.Add("OptionSets");
        if (IncludeReferenceData) parts.Add("RefData");
        return parts.Count > 0 ? string.Join(", ", parts) : "No generation configured";
    }
}

public class ConstantsOutputConfig
{
    public string OutputPath { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public bool SingleFile { get; set; }
    public bool IncludeEntities { get; set; } = true;
    public bool IncludeGlobalOptionSets { get; set; } = true;
    public bool IncludeRelationships { get; set; } = true;
    public bool IncludeReferenceData { get; set; }
    public bool IncludeComments { get; set; } = true;
    public bool PascalCaseConversion { get; set; } = true;
    public List<string> ExcludeAttributes { get; set; } = new();

    public override string ToString()
    {
        return $"{Namespace} -> {OutputPath}";
    }
}
