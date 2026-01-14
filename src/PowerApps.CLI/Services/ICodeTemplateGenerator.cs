using PowerApps.CLI.Models;

namespace PowerApps.CLI.Services;

/// <summary>
/// Generates C# code templates for constants classes.
/// </summary>
public interface ICodeTemplateGenerator
{
    /// <summary>
    /// Generates a complete entity class.
    /// </summary>
    string GenerateEntityClass(EntitySchema entity, string namespaceName);

    /// <summary>
    /// Generates a global option set class.
    /// </summary>
    string GenerateGlobalOptionSetClass(OptionSetSchema optionSet, string namespaceName);

    /// <summary>
    /// Combines multiple classes into a single file.
    /// </summary>
    string GenerateSingleFile(string namespaceName, IEnumerable<string> classContents);
}
