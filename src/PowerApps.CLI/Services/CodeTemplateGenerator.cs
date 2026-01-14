using PowerApps.CLI.Models;
using System.Text;

namespace PowerApps.CLI.Services;

/// <summary>
/// Generates C# code templates for constants classes.
/// </summary>
public class CodeTemplateGenerator : ICodeTemplateGenerator
{
    private readonly bool _includeComments;
    private readonly bool _includeRelationships;
    private readonly IIdentifierFormatter _formatter;

    public CodeTemplateGenerator(bool includeComments, bool includeRelationships, IIdentifierFormatter formatter)
    {
        _includeComments = includeComments;
        _includeRelationships = includeRelationships;
        _formatter = formatter;
    }

    /// <summary>
    /// Generates a complete entity class.
    /// </summary>
    public string GenerateEntityClass(EntitySchema entity, string namespaceName)
    {
        var sb = new StringBuilder();
        var className = _formatter.ToIdentifier(entity.DisplayName ?? entity.SchemaName ?? entity.LogicalName);

        // Namespace
        sb.AppendLine($"namespace {namespaceName}");
        sb.AppendLine("{");

        // Class declaration
        AppendComment(sb, 4, $"Constants for the {entity.DisplayName ?? entity.LogicalName} entity.");
        sb.AppendLine($"    public static class {className}");
        sb.AppendLine("    {");

        // Entity metadata constants
        AppendComment(sb, 8, "Logical name of the entity.");
        sb.AppendLine($"        public const string EntityLogicalName = \"{entity.LogicalName}\";");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(entity.PrimaryIdAttribute))
        {
            AppendComment(sb, 8, "Primary ID attribute.");
            sb.AppendLine($"        public const string PrimaryIdAttribute = \"{entity.PrimaryIdAttribute}\";");
            sb.AppendLine();
        }

        if (!string.IsNullOrEmpty(entity.PrimaryNameAttribute))
        {
            AppendComment(sb, 8, "Primary Name attribute.");
            sb.AppendLine($"        public const string PrimaryNameAttribute = \"{entity.PrimaryNameAttribute}\";");
            sb.AppendLine();
        }

        // Attributes
        foreach (var attr in entity.Attributes)
        {
            if (attr.OptionSet == null || !IsStateOrStatusCode(attr.AttributeType))
            {
                AppendAttributeConstant(sb, attr);
            }
        }

        // State/Status codes
        AppendStateStatusCodes(sb, entity);

        // Local option sets (non-state/status)
        AppendLocalOptionSets(sb, entity);

        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Generates a global option set class.
    /// </summary>
    public string GenerateGlobalOptionSetClass(OptionSetSchema optionSet, string namespaceName)
    {
        var sb = new StringBuilder();
        var className = _formatter.ToIdentifier(optionSet.Name ?? "UnknownOptionSet");

        sb.AppendLine($"namespace {namespaceName}");
        sb.AppendLine("{");

        AppendComment(sb, 4, $"Global option set: {optionSet.Name ?? "Unknown"}");
        sb.AppendLine($"    public static class {className}");
        sb.AppendLine("    {");

        foreach (var option in optionSet.Options)
        {
            AppendOptionConstant(sb, option, 8);
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Combines multiple classes into a single file.
    /// </summary>
    public string GenerateSingleFile(string namespaceName, IEnumerable<string> classContents)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"namespace {namespaceName}");
        sb.AppendLine("{");

        foreach (var content in classContents)
        {
            // Extract just the class definition (remove namespace wrapper)
            var lines = content.Split(Environment.NewLine);
            var classLines = lines.Skip(2).SkipLast(2); // Skip first 2 lines (namespace + {) and last 2 lines (} + })
            sb.AppendLine(string.Join(Environment.NewLine, classLines));
            sb.AppendLine();
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private void AppendAttributeConstant(StringBuilder sb, AttributeSchema attr)
    {
        var attributeName = _formatter.ToIdentifier(attr.DisplayName ?? attr.SchemaName ?? attr.LogicalName);
        var comment = BuildAttributeComment(attr);

        AppendComment(sb, 8, comment);
        sb.AppendLine($"        public const string {attributeName} = \"{attr.LogicalName}\";");
        sb.AppendLine();
    }

    private void AppendStateStatusCodes(StringBuilder sb, EntitySchema entity)
    {
        // StateCode
        var stateCodeAttr = entity.Attributes.FirstOrDefault(a => a.AttributeType == "State");
        if (stateCodeAttr?.OptionSet != null && stateCodeAttr.OptionSet.Options.Count > 0)
        {
            AppendComment(sb, 8, $"State Code options for {entity.DisplayName ?? entity.LogicalName}.");
            sb.AppendLine("        public static class StateCode");
            sb.AppendLine("        {");

            foreach (var option in stateCodeAttr.OptionSet.Options)
            {
                AppendOptionConstant(sb, option, 12);
            }

            sb.AppendLine("        }");
            sb.AppendLine();
        }

        // StatusCode
        var statusCodeAttr = entity.Attributes.FirstOrDefault(a => a.AttributeType == "Status");
        if (statusCodeAttr?.OptionSet != null && statusCodeAttr.OptionSet.Options.Count > 0)
        {
            AppendComment(sb, 8, $"Status Code options for {entity.DisplayName ?? entity.LogicalName}.");
            sb.AppendLine("        public static class StatusCode");
            sb.AppendLine("        {");

            foreach (var option in statusCodeAttr.OptionSet.Options)
            {
                AppendOptionConstant(sb, option, 12);
            }

            sb.AppendLine("        }");
            sb.AppendLine();
        }
    }

    private void AppendLocalOptionSets(StringBuilder sb, EntitySchema entity)
    {
        var localOptionSetAttrs = entity.Attributes
            .Where(a => a.OptionSet != null && 
                        !a.OptionSet.IsGlobal && 
                        !IsStateOrStatusCode(a.AttributeType))
            .ToList();

        foreach (var attr in localOptionSetAttrs)
        {
            if (attr.OptionSet == null) continue;

            var className = _formatter.ToIdentifier(attr.DisplayName ?? attr.LogicalName) + "Options";

            AppendComment(sb, 8, $"{attr.DisplayName ?? attr.LogicalName} option set values.");
            sb.AppendLine($"        public static class {className}");
            sb.AppendLine("        {");

            foreach (var option in attr.OptionSet.Options)
            {
                AppendOptionConstant(sb, option, 12);
            }

            sb.AppendLine("        }");
            sb.AppendLine();
        }
    }

    private void AppendOptionConstant(StringBuilder sb, OptionSchema option, int indent)
    {
        var optionName = _formatter.ToIdentifier(option.Label ?? $"Option{option.Value}");
        var spaces = new string(' ', indent);

        AppendComment(sb, indent, option.Label ?? optionName);
        sb.AppendLine($"{spaces}public const int {optionName} = {option.Value};");
        sb.AppendLine();
    }

    private string BuildAttributeComment(AttributeSchema attr)
    {
        var parts = new List<string> { $"{attr.LogicalName} ({attr.AttributeType})" };

        if (attr.MaxLength.HasValue)
            parts.Add($"MaxLength: {attr.MaxLength}");

        if (attr.Targets != null && attr.Targets.Length > 0)
            parts.Add($"Targets: {string.Join(", ", attr.Targets)}");

        if (attr.OptionSet != null)
        {
            if (attr.OptionSet.IsGlobal)
                parts.Add($"Uses global option set: {attr.OptionSet.Name}");
            else if (!IsStateOrStatusCode(attr.AttributeType))
                parts.Add("Uses local option set");
        }

        return string.Join(" - ", parts);
    }

    private void AppendComment(StringBuilder sb, int indent, string comment)
    {
        if (!_includeComments)
            return;

        var spaces = new string(' ', indent);
        sb.AppendLine($"{spaces}/// <summary>");
        sb.AppendLine($"{spaces}/// {comment}");
        sb.AppendLine($"{spaces}/// </summary>");
    }

    private static bool IsStateOrStatusCode(string? attributeType)
    {
        return attributeType == "State" || attributeType == "Status";
    }
}
