namespace PowerApps.CLI.Models;

public class RefDataMigrateConfig
{
    public int BatchSize { get; set; } = 1000;
    public List<MigrateTableConfig> Tables { get; set; } = new();
    public List<RefDataRelationshipConfig> Relationships { get; set; } = new();
}

public class MigrateTableConfig
{
    public string LogicalName { get; set; } = string.Empty;
    public string? Filter { get; set; }
    public bool ManageState { get; set; } = false;
    public List<string> ExcludeFields { get; set; } = new();
    public List<string> IncludeFields { get; set; } = new();
}
