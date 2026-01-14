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

    public ConstantsGenerator(
        ICodeTemplateGenerator templateGenerator,
        IConstantsFilter filter,
        IFileWriter fileWriter)
    {
        _templateGenerator = templateGenerator;
        _filter = filter;
        _fileWriter = fileWriter;
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

        // Extract global option sets if needed
        List<OptionSetSchema>? globalOptionSets = null;
        if (outputConfig.IncludeGlobalOptionSets)
        {
            globalOptionSets = _filter.ExtractGlobalOptionSets(entities);
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
        // Generate entities file
        if (outputConfig.IncludeEntities && entities.Count > 0)
        {
            logger.LogInfo($"  Generating Tables.cs with {entities.Count} entit{(entities.Count == 1 ? "y" : "ies")}...");
            var classContents = entities.Select(e => 
                _templateGenerator.GenerateEntityClass(e, outputConfig.Namespace));
            var content = _templateGenerator.GenerateSingleFile(outputConfig.Namespace, classContents);
            await _fileWriter.WriteTextAsync(Path.Combine(outputConfig.OutputPath, "Tables.cs"), content);
        }

        // Generate global option sets file
        if (outputConfig.IncludeGlobalOptionSets && globalOptionSets != null && globalOptionSets.Count > 0)
        {
            logger.LogInfo($"  Generating Choices.cs with {globalOptionSets.Count} option set(s)...");
            var classContents = globalOptionSets.Select(o =>
                _templateGenerator.GenerateGlobalOptionSetClass(o, outputConfig.Namespace));
            var content = _templateGenerator.GenerateSingleFile(outputConfig.Namespace, classContents);
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
            var formatter = new IdentifierFormatter(outputConfig.PascalCaseConversion);

            foreach (var entity in entities)
            {
                var className = formatter.ToIdentifier(entity.DisplayName ?? entity.SchemaName ?? entity.LogicalName);
                var fileName = $"{className}.cs";
                var content = _templateGenerator.GenerateEntityClass(entity, $"{outputConfig.Namespace}.Tables");
                await _fileWriter.WriteTextAsync(Path.Combine(entitiesPath, fileName), content);
            }
        }

        // Generate global option set files
        if (outputConfig.IncludeGlobalOptionSets && globalOptionSets != null && globalOptionSets.Count > 0)
        {
            logger.LogInfo($"  Generating {globalOptionSets.Count} option set file(s)...");
            var optionSetsPath = Path.Combine(outputConfig.OutputPath, "Choices");
            var formatter = new IdentifierFormatter(outputConfig.PascalCaseConversion);

            foreach (var optionSet in globalOptionSets)
            {
                var className = formatter.ToIdentifier(optionSet.Name ?? "UnknownOptionSet");
                var fileName = $"{className}.cs";
                var content = _templateGenerator.GenerateGlobalOptionSetClass(optionSet, $"{outputConfig.Namespace}.Choices");
                await _fileWriter.WriteTextAsync(Path.Combine(optionSetsPath, fileName), content);
            }
        }
    }
}
