namespace PowerApps.CLI.Models;

public class RefDataMigrateConfig
{
    public int BatchSize { get; set; } = 1000;
    public List<MigrateTableConfig> Tables { get; set; } = new();
    public List<ManyToManyConfig> ManyToManyRelationships { get; set; } = new();
}

public class ManyToManyConfig
{
    public string RelationshipName { get; set; } = string.Empty;
}

public class MigrateTableConfig
{
    public string LogicalName { get; set; } = string.Empty;
    public string? Filter { get; set; }
    public bool ManageState { get; set; } = false;
    public List<string> ExcludeColumns { get; set; } = new();
    public List<string> IncludeColumns { get; set; } = new();
}
