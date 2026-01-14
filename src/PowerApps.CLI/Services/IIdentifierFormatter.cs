namespace PowerApps.CLI.Services;

/// <summary>
/// Formats strings into valid C# identifiers.
/// </summary>
public interface IIdentifierFormatter
{
    /// <summary>
    /// Converts a name to a valid C# identifier.
    /// </summary>
    string ToIdentifier(string input);

    /// <summary>
    /// Converts a name to PascalCase, optionally pluralising it.
    /// </summary>
    string ToPascalCase(string input, bool pluralise = false);

    /// <summary>
    /// Generates a unique identifier when duplicates are found.
    /// </summary>
    string MakeUnique(string identifier, HashSet<string> existingIdentifiers, string? suffix = null);
}
