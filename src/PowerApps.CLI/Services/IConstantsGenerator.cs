using PowerApps.CLI.Infrastructure;
using PowerApps.CLI.Models;

namespace PowerApps.CLI.Services;

/// <summary>
/// Orchestrates constants generation from filtered entities.
/// </summary>
public interface IConstantsGenerator
{
    /// <summary>
    /// Generates constants files for the given configuration.
    /// </summary>
    Task GenerateAsync(
        List<EntitySchema> entities, 
        ConstantsOutputConfig outputConfig, 
        IConsoleLogger logger);
}
