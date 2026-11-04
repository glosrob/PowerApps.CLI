namespace PowerApps.CLI.Models;

/// <summary>
/// Dataverse schema constants shared across the Infrastructure and Services layers.
/// These are platform-level values (option set codes, component type integers) that
/// would otherwise be duplicated as magic numbers in each class that reads or writes them.
/// </summary>
internal static class DataverseConstants
{
    // solutioncomponent.componenttype codes — a subset of the ComponentType SDK enum values
    // that are referenced directly in queries and component list building.
    internal const int ComponentTypeEntity = 1;
    internal const int ComponentTypeAttribute = 2;
    internal const int ComponentTypeSavedQuery = 26;
    internal const int ComponentTypeSavedQueryVisualization = 59;
    internal const int ComponentTypeSystemForm = 60;

    // The "Active" solution is Dataverse's built-in bucket for unmanaged customisations.
    internal const string ActiveSolutionUniqueName = "Active";

    // workflow.category option set codes.
    internal const int WorkflowCategoryWorkflow = 0;
    internal const int WorkflowCategoryBusinessRule = 2;
    internal const int WorkflowCategoryAction = 3;
    internal const int WorkflowCategoryBusinessProcessFlow = 4;
    internal const int WorkflowCategoryCloudFlow = 5;

    // Workflow (process) state and status codes.
    internal const int WorkflowStateActive = 1;
    internal const int WorkflowStatusActivated = 2;
    internal const int WorkflowStateInactive = 0;
    internal const int WorkflowStatusDraft = 1;

    // Duplicate rule state and status codes.
    internal const int DuplicateRuleStateInactive = 0;
    internal const int DuplicateRuleStatusUnpublished = 0;

    // Plugin step state and status codes — inverted relative to most entities: 0 = Enabled, 1 = Disabled.
    internal const int PluginStepStateEnabled = 0;
    internal const int PluginStepStatusEnabled = 1;
    internal const int PluginStepStateDisabled = 1;
    internal const int PluginStepStatusDisabled = 2;
}
