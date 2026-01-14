using System.Text;

namespace PowerApps.CLI.Services;

/// <summary>
/// Handles conversion of Dataverse names to valid C# identifiers.
/// </summary>
public class IdentifierFormatter : IIdentifierFormatter
{
    private readonly bool _usePascalCase;
    private static readonly HashSet<string> ReservedKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
        "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
        "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
        "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
        "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
        "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed",
        "short", "sizeof", "stackalloc", "static", "string", "struct", "switch", "this",
        "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort",
        "using", "virtual", "void", "volatile", "while"
    };

    public IdentifierFormatter(bool usePascalCase = true)
    {
        _usePascalCase = usePascalCase;
    }

    /// <summary>
    /// Converts a name to a valid C# identifier.
    /// </summary>
    public string ToIdentifier(string input)
    {
        if (string.IsNullOrEmpty(input))
            return "Unknown";

        // Apply PascalCase first (if enabled) to preserve word boundaries
        var sanitised = input;
        if (_usePascalCase)
        {
            sanitised = ToPascalCase(sanitised);
        }

        // Remove invalid characters
        sanitised = SanitiseCharacters(sanitised);

        if (string.IsNullOrEmpty(sanitised))
            return "Unknown";

        // Handle leading digit
        if (char.IsDigit(sanitised[0]))
        {
            sanitised = "_" + sanitised;
        }

        // Handle reserved keywords
        if (ReservedKeywords.Contains(sanitised))
        {
            sanitised += "_";
        }

        // Ensure first letter is capital (in case PascalCase was disabled)
        if (sanitised.Length > 0 && char.IsLower(sanitised[0]))
        {
            sanitised = char.ToUpper(sanitised[0]) + sanitised.Substring(1);
        }

        return sanitised;
    }

    /// <summary>
    /// Converts a name to PascalCase, optionally pluralising it.
    /// </summary>
    public string ToPascalCase(string input, bool pluralise = false)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        // Remove parentheses and other special characters, replacing with spaces for word boundary
        var cleaned = input;
        foreach (var ch in new[] { '(', ')', '[', ']', '{', '}', '.', ',', '!', '?', ';', ':', '@', '#', '$', '%', '^', '&', '*', '+', '=', '<', '>', '/', '\\', '|' })
        {
            cleaned = cleaned.Replace(ch, ' ');
        }

        // Split on common separators
        var parts = cleaned.Split(new[] { '_', ' ', '-' }, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0)
            return input;

        // Convert each part to title case
        var result = string.Join("", parts.Select(p =>
            p.Length > 0 ? char.ToUpper(p[0]) + p.Substring(1).ToLower() : p));

        // Ensure first letter is capital
        if (result.Length > 0 && char.IsLower(result[0]))
        {
            result = char.ToUpper(result[0]) + result.Substring(1);
        }

        // Pluralise if requested
        if (pluralise && !result.EndsWith("s", StringComparison.OrdinalIgnoreCase))
        {
            result += "s";
        }

        return result;
    }

    /// <summary>
    /// Removes invalid characters from a string (assumes already PascalCased).
    /// </summary>
    private string SanitiseCharacters(string input)
    {
        var sanitised = new StringBuilder();

        foreach (var c in input)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
            {
                sanitised.Append(c);
            }
            // Skip all other characters (spaces, dots, dashes, special chars, etc.)
        }

        return sanitised.ToString();
    }

    /// <summary>
    /// Generates a unique identifier when duplicates are found.
    /// </summary>
    public string MakeUnique(string identifier, HashSet<string> existingIdentifiers, string? suffix = null)
    {
        if (!existingIdentifiers.Contains(identifier))
            return identifier;

        // Append suffix (e.g., short GUID)
        var uniqueIdentifier = suffix != null
            ? $"{identifier}_{suffix}"
            : $"{identifier}_{Guid.NewGuid().ToString().Substring(0, 6)}";

        // Ensure uniqueness
        int counter = 1;
        var baseIdentifier = uniqueIdentifier;
        while (existingIdentifiers.Contains(uniqueIdentifier))
        {
            uniqueIdentifier = $"{baseIdentifier}_{counter++}";
        }

        return uniqueIdentifier;
    }
}
