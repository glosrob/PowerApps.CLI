using PowerApps.CLI.IntegrationTests.Infrastructure;
using PowerApps.CLI.Services;
using Xunit;

namespace PowerApps.CLI.IntegrationTests.SolutionLayers;

/// <summary>
/// Integration tests for <see cref="SolutionLayerService"/> against the XRTSoftLayerTests solution.
///
/// Required environment state — these unmanaged customisations must exist in the target environment:
///   [AppModule]  Layer Tests        — model-driven app (site map change also surfaces this)
///   [Attribute]  xrtlt_choicefield  — option added to the global choice
///   [SiteMap]    Layer Tests        — item added to the model-driven app site map
///   [Workflow]   Example Action for Layers
///   [Workflow]   Example Business Rule
///   [Workflow]   Example Flow for Layers
///   [SystemForm] (main form for Layer Test)
///   [SavedQuery] Inactive Layer Test view
///   [Attribute]  statuscode         — Status Reason label change
///
/// The following attributes are intentionally left clean and must NOT have unmanaged layers:
///   xrtlt_boolfield
/// </summary>
[Collection("Dataverse")]
[Trait("Category", "Integration")]
public class SolutionLayersTests(DataverseFixture fixture)
{
    private const string LayerTestSolution = "XRTSoftLayerTests";

    private SolutionLayerService CreateService() => new(fixture.Client);

    // -------------------------------------------------------------------------
    // Sanity check
    // -------------------------------------------------------------------------

    [SkippableFact]
    public async Task GetUnmanagedLayers_TotalComponentsChecked_IsPositiveAsync()
    {
        Skip.If(fixture.ConfigurationError is not null, fixture.ConfigurationError);

        var result = await CreateService().GetUnmanagedLayersAsync(LayerTestSolution);

        Assert.True(result.TotalComponentsChecked > 0);
    }

    // -------------------------------------------------------------------------
    // Detected unmanaged layers
    // -------------------------------------------------------------------------

    [SkippableFact]
    public async Task GetUnmanagedLayers_LayeredAttribute_IsDetectedAsync()
    {
        Skip.If(fixture.ConfigurationError is not null, fixture.ConfigurationError);

        var result = await CreateService().GetUnmanagedLayersAsync(LayerTestSolution);

        Assert.Contains(result.LayeredComponents,
            c => c.ComponentName == "xrtlt_choicefield" && c.ComponentType == "Attribute");
    }

    [SkippableFact]
    public async Task GetUnmanagedLayers_LayeredSiteMap_IsDetectedAsync()
    {
        Skip.If(fixture.ConfigurationError is not null, fixture.ConfigurationError);

        var result = await CreateService().GetUnmanagedLayersAsync(LayerTestSolution);

        Assert.Contains(result.LayeredComponents,
            c => c.ComponentName == "Layer Tests" && c.ComponentType == "SiteMap");
    }

    [SkippableFact]
    public async Task GetUnmanagedLayers_LayeredAction_IsDetectedAsync()
    {
        Skip.If(fixture.ConfigurationError is not null, fixture.ConfigurationError);

        var result = await CreateService().GetUnmanagedLayersAsync(LayerTestSolution);

        Assert.Contains(result.LayeredComponents,
            c => c.ComponentName == "Example Action for Layers" && c.ComponentType == "Workflow");
    }

    [SkippableFact]
    public async Task GetUnmanagedLayers_LayeredBusinessRule_IsDetectedAsync()
    {
        Skip.If(fixture.ConfigurationError is not null, fixture.ConfigurationError);

        var result = await CreateService().GetUnmanagedLayersAsync(LayerTestSolution);

        Assert.Contains(result.LayeredComponents,
            c => c.ComponentName == "Example Business Rule" && c.ComponentType == "Workflow");
    }

    [SkippableFact]
    public async Task GetUnmanagedLayers_LayeredFlow_IsDetectedAsync()
    {
        Skip.If(fixture.ConfigurationError is not null, fixture.ConfigurationError);

        var result = await CreateService().GetUnmanagedLayersAsync(LayerTestSolution);

        Assert.Contains(result.LayeredComponents,
            c => c.ComponentName == "Example Flow for Layers" && c.ComponentType == "Workflow");
    }

    [SkippableFact]
    public async Task GetUnmanagedLayers_LayeredAppModule_IsDetectedAsync()
    {
        Skip.If(fixture.ConfigurationError is not null, fixture.ConfigurationError);

        var result = await CreateService().GetUnmanagedLayersAsync(LayerTestSolution);

        Assert.Contains(result.LayeredComponents,
            c => c.ComponentName == "Layer Tests" && c.ComponentType == "AppModule");
    }

    [SkippableFact]
    public async Task GetUnmanagedLayers_LayeredForm_IsDetectedAsync()
    {
        Skip.If(fixture.ConfigurationError is not null, fixture.ConfigurationError);

        var result = await CreateService().GetUnmanagedLayersAsync(LayerTestSolution);

        Assert.Contains(result.LayeredComponents,
            c => c.ComponentType == "SystemForm");
    }

    [SkippableFact]
    public async Task GetUnmanagedLayers_LayeredView_IsDetectedAsync()
    {
        Skip.If(fixture.ConfigurationError is not null, fixture.ConfigurationError);

        var result = await CreateService().GetUnmanagedLayersAsync(LayerTestSolution);

        Assert.Contains(result.LayeredComponents,
            c => c.ComponentType == "SavedQuery");
    }

    [SkippableFact]
    public async Task GetUnmanagedLayers_LayeredSystemAttributeLabel_IsDetectedAsync()
    {
        Skip.If(fixture.ConfigurationError is not null, fixture.ConfigurationError);

        var result = await CreateService().GetUnmanagedLayersAsync(LayerTestSolution);

        Assert.Contains(result.LayeredComponents,
            c => c.ComponentName == "statuscode" && c.ComponentType == "Attribute");
    }

    // -------------------------------------------------------------------------
    // Clean component — must not be reported
    // -------------------------------------------------------------------------

    [SkippableFact]
    public async Task GetUnmanagedLayers_CleanAttribute_IsNotReportedAsync()
    {
        Skip.If(fixture.ConfigurationError is not null, fixture.ConfigurationError);

        var result = await CreateService().GetUnmanagedLayersAsync(LayerTestSolution);

        Assert.DoesNotContain(result.LayeredComponents,
            c => c.ComponentName == "xrtlt_boolfield");
    }

    // -------------------------------------------------------------------------
    // Unknown solution
    // -------------------------------------------------------------------------

    [SkippableFact]
    public async Task GetUnmanagedLayers_UnknownSolution_ReturnsEmptyAsync()
    {
        Skip.If(fixture.ConfigurationError is not null, fixture.ConfigurationError);

        var result = await CreateService().GetUnmanagedLayersAsync("DoesNotExist");

        Assert.Equal(0, result.TotalComponentsChecked);
        Assert.False(result.HasUnmanagedLayers);
    }
}
