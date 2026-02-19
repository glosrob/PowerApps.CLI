using PowerApps.CLI.Models;

namespace PowerApps.CLI.Services;

public interface IRefDataMigrator
{
    Task<MigrationSummary> MigrateAsync(RefDataMigrateConfig config, bool dryRun, bool force);
}
