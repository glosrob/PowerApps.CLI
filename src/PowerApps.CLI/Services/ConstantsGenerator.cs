using PowerApps.CLI.Infrastructure;
using PowerApps.CLI.Models;

namespace PowerApps.CLI.Services;

/// <summary>
/// Orchestrates constants generation from filtered entities.
/// </summary>
public class ConstantsGenerator : IConstantsGenerator
{
    private readonly ICodeTemplateGenerator _templateGenerator;
    private readonly IConstantsFilter _filter;
    private readonly IFileWriter _fileWriter;
    private readonly IIdentifierFormatter _formatter;

    public ConstantsGenerator(
        ICodeTemplateGenerator templateGenerator,
        IConstantsFilter filter,
        IFileWriter fileWriter,
        IIdentifierFormatter formatter)
    {
        _templateGenerator = templateGenerator;
        _filter = filter;
        _fileWriter = fileWriter;
        _formatter = formatter;
    }

    /// <summary>
    /// Generates constants files for the given configuration.
    /// </summary>
    public async Task GenerateAsync(
        List<EntitySchema> entities,
        ConstantsOutputConfig outputConfig,
        IConsoleLogger logger)
    {
        logger.LogInfo($"Generating constants to: {outputConfig.OutputPath}");

        // Sort into a deterministic order before any dedup naming below runs — Dataverse metadata
        // query ordering is not guaranteed stable, so leaving this unsorted would let MakeUnique
        // assign e.g. "Email" vs "Email_" differently between otherwise-identical runs.
        entities = entities.OrderBy(e => e.LogicalName, StringComparer.OrdinalIgnoreCase).ToList();

        // Extract global option sets if needed
        List<OptionSetSchema>? globalOptionSets = null;
        if (outputConfig.IncludeGlobalOptionSets)
        {
            globalOptionSets = _filter.ExtractGlobalOptionSets(entities)
                .OrderBy(o => o.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            logger.LogInfo($"  Found {globalOptionSets.Count} global option set(s)");
        }

        if (outputConfig.SingleFile)
        {
            await GenerateSingleFileModeAsync(entities, globalOptionSets, outputConfig, logger);
        }
        else
        {
            await GenerateMultipleFilesModeAsync(entities, globalOptionSets, outputConfig, logger);
        }
    }

    private async Task GenerateSingleFileModeAsync(
        List<EntitySchema> entities,
        List<OptionSetSchema>? globalOptionSets,
        ConstantsOutputConfig outputConfig,
        IConsoleLogger logger)
    {
        // Generate entities file — namespace matches multi-file mode's {Namespace}.Tables so
        // generated code doesn't need to change if --single-file is toggled later.
        if (outputConfig.IncludeEntities && entities.Count > 0)
        {
            logger.LogInfo($"  Generating Tables.cs with {entities.Count} entit{(entities.Count == 1 ? "y" : "ies")}...");
            var tablesNamespace = $"{outputConfig.Namespace}.Tables";
            var usedClassNames = new HashSet<string>();
            var classContents = entities.Select(e =>
            {
                var className = _formatter.MakeUnique(
                    _formatter.ToIdentifier(e.DisplayName ?? e.SchemaName ?? e.LogicalName),
                    usedClassNames);
                usedClassNames.Add(className);
                return _templateGenerator.GenerateEntityClass(e, tablesNamespace, className);
            });
            var content = _templateGenerator.GenerateSingleFile(tablesNamespace, classContents);
            await _fileWriter.WriteTextAsync(Path.Combine(outputConfig.OutputPath, "Tables.cs"), content);
        }

        // Generate global option sets file — namespace matches multi-file mode's {Namespace}.Choices.
        if (outputConfig.IncludeGlobalOptionSets && globalOptionSets != null && globalOptionSets.Count > 0)
        {
            logger.LogInfo($"  Generating Choices.cs with {globalOptionSets.Count} option set(s)...");
            var choicesNamespace = $"{outputConfig.Namespace}.Choices";
            var usedClassNames = new HashSet<string>();
            var classContents = globalOptionSets.Select(o =>
            {
                var className = _formatter.MakeUnique(
                    _formatter.ToIdentifier(o.DisplayName ?? o.Name ?? "UnknownOptionSet"),
                    usedClassNames);
                usedClassNames.Add(className);
                return _templateGenerator.GenerateGlobalOptionSetClass(o, choicesNamespace, className);
            });
            var content = _templateGenerator.GenerateSingleFile(choicesNamespace, classContents);
            await _fileWriter.WriteTextAsync(Path.Combine(outputConfig.OutputPath, "Choices.cs"), content);
        }
    }

    private async Task GenerateMultipleFilesModeAsync(
        List<EntitySchema> entities,
        List<OptionSetSchema>? globalOptionSets,
        ConstantsOutputConfig outputConfig,
        IConsoleLogger logger)
    {
        // Generate entity files
        if (outputConfig.IncludeEntities && entities.Count > 0)
        {
            logger.LogInfo($"  Generating {entities.Count} entity file(s)...");
            var entitiesPath = Path.Combine(outputConfig.OutputPath, "Tables");
            var usedClassNames = new HashSet<string>();

            foreach (var entity in entities)
            {
                var className = _formatter.MakeUnique(
                    _formatter.ToIdentifier(entity.DisplayName ?? entity.SchemaName ?? entity.LogicalName),
                    usedClassNames);
                usedClassNames.Add(className);
                var fileName = $"{className}.cs";
                var content = _templateGenerator.GenerateEntityClass(entity, $"{outputConfig.Namespace}.Tables", className);
                await _fileWriter.WriteTextAsync(Path.Combine(entitiesPath, fileName), content);
            }
        }

        // Generate global option set files
        if (outputConfig.IncludeGlobalOptionSets && globalOptionSets != null && globalOptionSets.Count > 0)
        {
            logger.LogInfo($"  Generating {globalOptionSets.Count} option set file(s)...");
            var optionSetsPath = Path.Combine(outputConfig.OutputPath, "Choices");
            var usedClassNames = new HashSet<string>();

            foreach (var optionSet in globalOptionSets)
            {
                var className = _formatter.MakeUnique(
                    _formatter.ToIdentifier(optionSet.DisplayName ?? optionSet.Name ?? "UnknownOptionSet"),
                    usedClassNames);
                usedClassNames.Add(className);
                var fileName = $"{className}.cs";
                var content = _templateGenerator.GenerateGlobalOptionSetClass(optionSet, $"{outputConfig.Namespace}.Choices", className);
                await _fileWriter.WriteTextAsync(Path.Combine(optionSetsPath, fileName), content);
            }
        }
    }
}
