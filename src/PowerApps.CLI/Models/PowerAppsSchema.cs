namespace PowerApps.CLI.Models;

public class PowerAppsSchema
{
    public DateTime ExtractedDate { get; set; }
    public string EnvironmentUrl { get; set; } = string.Empty;
    public string OrganisationName { get; set; } = string.Empty;
    public List<string>? SolutionNames { get; set; }
    public List<string>? SolutionComponents { get; set; }
    public List<EntitySchema> Entities { get; set; } = new();
    public List<RelationshipSchema> Relationships { get; set; } = new();

    public override string ToString()
    {
        if (SolutionNames != null && SolutionNames.Count > 0)
        {
            return $"{OrganisationName}  ({string.Join(", ", SolutionNames)})";
        }
        return OrganisationName;
    }
}
