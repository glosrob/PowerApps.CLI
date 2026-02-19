namespace PowerApps.CLI.Models;

public class MigrationSummary
{
    public string SourceEnvironment { get; set; } = string.Empty;
    public string TargetEnvironment { get; set; } = string.Empty;
    public DateTime ExecutionDate { get; set; } = DateTime.UtcNow;
    public bool IsDryRun { get; set; }
    public TimeSpan Duration { get; set; }
    public List<TableMigrationResult> TableResults { get; set; } = new();
    public List<ManyToManyMigrationResult> ManyToManyResults { get; set; } = new();

    public int TotalRecords => TableResults.Sum(t => t.RecordCount);
    public int TotalUpserted => TableResults.Sum(t => t.UpsertedCount);
    public int TotalLookupsPatched => TableResults.Sum(t => t.LookupsPatchedCount);
    public int TotalStateChanges => TableResults.Sum(t => t.StateChangesCount);
    public int TotalSkipped => TableResults.Sum(t => t.SkippedCount);
    public int TotalAssociated => ManyToManyResults.Sum(r => r.AssociatedCount);
    public int TotalDisassociated => ManyToManyResults.Sum(r => r.DisassociatedCount);
    public int TotalErrors => TableResults.Sum(t => t.Errors.Count) + ManyToManyResults.Sum(r => r.Errors.Count);
    public bool HasErrors => TotalErrors > 0;
}

public class TableMigrationResult
{
    public string TableName { get; set; } = string.Empty;
    public int RecordCount { get; set; }
    public int UpsertedCount { get; set; }
    public int LookupsPatchedCount { get; set; }
    public int StateChangesCount { get; set; }
    public int SkippedCount { get; set; }
    public List<RecordError> Errors { get; set; } = new();
}

public class RecordError
{
    public string TableName { get; set; } = string.Empty;
    public Guid RecordId { get; set; }
    public string Phase { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
}

public class ManyToManyMigrationResult
{
    public string RelationshipName { get; set; } = string.Empty;
    public string Entity1Name { get; set; } = string.Empty;
    public string Entity2Name { get; set; } = string.Empty;
    public int SourceCount { get; set; }
    public int TargetExistingCount { get; set; }
    public int AssociatedCount { get; set; }
    public int DisassociatedCount { get; set; }
    public List<RecordError> Errors { get; set; } = new();
}
