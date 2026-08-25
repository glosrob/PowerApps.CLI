namespace PowerApps.CLI.Infrastructure;

/// <summary>
/// Allocates unique, Excel-legal worksheet names.
/// </summary>
/// <remarks>
/// Excel worksheet names are limited to 31 characters, cannot contain \ / ? * [ ] and must be
/// unique within a workbook (case-insensitively). Truncating a long name to 31 characters can
/// therefore produce a collision - two tables whose logical names share a 31-character prefix
/// both truncate to the same name. This allocator tracks the names it has handed out and
/// disambiguates collisions with a tilde suffix (~2, ~3, ...), trimming the base name so the
/// result still fits within the 31-character limit.
///
/// The allocator is stateful: <see cref="Allocate"/> returns a different name each time it is
/// called with the same input. Callers that need a name in more than one place (creating the
/// sheet, then linking to it) must allocate once and reuse the result.
/// </remarks>
public class WorksheetNameAllocator
{
    private const int MaxLength = 31;
    private const string FallbackName = "Sheet";

    private static readonly char[] IllegalCharacters = { '\\', '/', '?', '*', '[', ']' };

    private readonly HashSet<string> _usedNames = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Reserves a fixed worksheet name so later allocations cannot collide with it.
    /// </summary>
    /// <remarks>
    /// Use this for sheets added outside the allocator, such as "Summary" or "Attributes".
    /// </remarks>
    public void Reserve(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        _usedNames.Add(name);
    }

    /// <summary>
    /// Returns a unique, Excel-legal worksheet name derived from <paramref name="rawName"/>.
    /// </summary>
    public string Allocate(string? rawName)
    {
        var baseName = Sanitise(rawName);

        if (_usedNames.Add(baseName))
        {
            return baseName;
        }

        for (var suffixNumber = 2; ; suffixNumber++)
        {
            var suffix = $"~{suffixNumber}";
            var trimmedBase = baseName.Length + suffix.Length > MaxLength
                ? baseName.Substring(0, MaxLength - suffix.Length)
                : baseName;
            var candidate = trimmedBase + suffix;

            if (_usedNames.Add(candidate))
            {
                return candidate;
            }
        }
    }

    private static string Sanitise(string? rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
        {
            return FallbackName;
        }

        var sanitised = rawName;
        foreach (var illegalCharacter in IllegalCharacters)
        {
            sanitised = sanitised.Replace(illegalCharacter, '_');
        }

        if (sanitised.Length > MaxLength)
        {
            sanitised = sanitised.Substring(0, MaxLength);
        }

        return sanitised;
    }
}
