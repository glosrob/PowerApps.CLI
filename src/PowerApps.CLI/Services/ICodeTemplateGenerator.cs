using PowerApps.CLI.Models;

namespace PowerApps.CLI.Services;

/// <summary>
/// Generates C# code templates for constants classes.
/// </summary>
public interface ICodeTemplateGenerator
{
    /// <summary>
    /// Generates a complete entity class. If <paramref name="classNameOverride"/> is supplied, it is
    /// used instead of computing the class name from the entity's display name — used by callers that
    /// need to deduplicate class names across multiple entities being generated together.
    /// </summary>
    string GenerateEntityClass(EntitySchema entity, string namespaceName, string? classNameOverride = null);

    /// <summary>
    /// Generates a global option set class. If <paramref name="classNameOverride"/> is supplied, it is
    /// used instead of computing the class name from the option set's display name — used by callers that
    /// need to deduplicate class names across multiple option sets being generated together.
    /// </summary>
    string GenerateGlobalOptionSetClass(OptionSetSchema optionSet, string namespaceName, string? classNameOverride = null);

    /// <summary>
    /// Combines multiple classes into a single file.
    /// </summary>
    string GenerateSingleFile(string namespaceName, IEnumerable<string> classContents);
}
