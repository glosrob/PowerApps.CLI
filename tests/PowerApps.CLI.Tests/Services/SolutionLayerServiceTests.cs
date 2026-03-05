using Microsoft.Xrm.Sdk;
using PowerApps.CLI.Infrastructure;
using PowerApps.CLI.Services;

namespace PowerApps.CLI.Tests.Services;

public class SolutionLayerServiceTests
{
    private readonly Mock<IDataverseClient> _mockClient;
    private readonly SolutionLayerService _service;

    public SolutionLayerServiceTests()
    {
        _mockClient = new Mock<IDataverseClient>();
        _mockClient.Setup(c => c.GetEnvironmentUrl()).Returns("https://test.crm.dynamics.com");
        _service = new SolutionLayerService(_mockClient.Object);
    }

    private static Entity MakeLayer(string componentId, string componentName, string componentTypeName, string solutionName, int order)
    {
        var entity = new Entity("msdyn_componentlayer");
        entity["msdyn_componentid"] = componentId;
        entity["msdyn_name"] = componentName;
        entity["msdyn_solutioncomponentname"] = componentTypeName;
        entity["msdyn_solutionname"] = solutionName;
        entity["msdyn_order"] = order;
        return entity;
    }

    [Fact]
    public async Task GetUnmanagedLayersAsync_WhenNoLayers_ReturnsEmptyResult()
    {
        _mockClient.Setup(c => c.GetSolutionComponentLayersAsync("MySolution", It.IsAny<Action<int, int, int>?>()))
            .ReturnsAsync(new EntityCollection());

        var result = await _service.GetUnmanagedLayersAsync("MySolution");

        Assert.Equal("MySolution", result.SolutionName);
        Assert.False(result.HasUnmanagedLayers);
        Assert.Empty(result.LayeredComponents);
    }

    [Fact]
    public async Task GetUnmanagedLayersAsync_WhenTopLayerIsActive_FlagsComponent()
    {
        // Two layers: Active on top (order 2), managed solution below (order 1)
        var layers = new EntityCollection(new List<Entity>
        {
            MakeLayer("comp-1", "rob_myentity", "Entity", "Active", 2),
            MakeLayer("comp-1", "rob_myentity", "Entity", "MySolution", 1),
        });

        _mockClient.Setup(c => c.GetSolutionComponentLayersAsync("MySolution", It.IsAny<Action<int, int, int>?>()))
            .ReturnsAsync(layers);

        var result = await _service.GetUnmanagedLayersAsync("MySolution");

        Assert.True(result.HasUnmanagedLayers);
        Assert.Single(result.LayeredComponents);
        Assert.Equal("rob_myentity", result.LayeredComponents[0].ComponentName);
        Assert.Equal("Entity", result.LayeredComponents[0].ComponentType);
        Assert.Equal("Active (Unmanaged Customisations)", result.LayeredComponents[0].UnmanagedLayerOwner);
    }

    [Fact]
    public async Task GetUnmanagedLayersAsync_WhenTopLayerIsManaged_DoesNotFlagComponent()
    {
        // Two layers: managed solution on top (order 2), Active below (order 1) — clean state
        var layers = new EntityCollection(new List<Entity>
        {
            MakeLayer("comp-1", "rob_myentity", "Entity", "MySolution", 2),
            MakeLayer("comp-1", "rob_myentity", "Entity", "Active", 1),
        });

        _mockClient.Setup(c => c.GetSolutionComponentLayersAsync("MySolution", It.IsAny<Action<int, int, int>?>()))
            .ReturnsAsync(layers);

        var result = await _service.GetUnmanagedLayersAsync("MySolution");

        Assert.False(result.HasUnmanagedLayers);
        Assert.Empty(result.LayeredComponents);
    }

    [Fact]
    public async Task GetUnmanagedLayersAsync_LayerStackIsOrderedBottomToTop()
    {
        var layers = new EntityCollection(new List<Entity>
        {
            MakeLayer("comp-1", "rob_form", "Form", "Active", 3),
            MakeLayer("comp-1", "rob_form", "Form", "MySolution", 2),
            MakeLayer("comp-1", "rob_form", "Form", "BaseLayer", 1),
        });

        _mockClient.Setup(c => c.GetSolutionComponentLayersAsync("MySolution", It.IsAny<Action<int, int, int>?>()))
            .ReturnsAsync(layers);

        var result = await _service.GetUnmanagedLayersAsync("MySolution");

        var component = result.LayeredComponents.Single();
        Assert.Equal(new[] { "BaseLayer", "MySolution", "Active" }, component.AllLayers);
    }

    [Fact]
    public async Task GetUnmanagedLayersAsync_UsesComponentTypeNameDirectlyFromDataverse()
    {
        // msdyn_solutioncomponentname is returned as a string by Dataverse — no mapping needed
        var layers = new EntityCollection(new List<Entity>
        {
            MakeLayer("comp-1", "rob_myform", "Model-driven Form", "Active", 2),
            MakeLayer("comp-1", "rob_myform", "Model-driven Form", "MySolution", 1),
        });

        _mockClient.Setup(c => c.GetSolutionComponentLayersAsync("MySolution", It.IsAny<Action<int, int, int>?>()))
            .ReturnsAsync(layers);

        var result = await _service.GetUnmanagedLayersAsync("MySolution");

        Assert.Equal("Model-driven Form", result.LayeredComponents.Single().ComponentType);
    }

    [Fact]
    public async Task GetUnmanagedLayersAsync_WhenComponentTypeNameMissing_FallsBackToUnknown()
    {
        var entity = new Entity("msdyn_componentlayer");
        entity["msdyn_componentid"] = "comp-1";
        entity["msdyn_name"] = "SomeComponent";
        // msdyn_solutioncomponentname deliberately omitted
        entity["msdyn_solutionname"] = "Active";
        entity["msdyn_order"] = 2;

        _mockClient.Setup(c => c.GetSolutionComponentLayersAsync("MySolution", It.IsAny<Action<int, int, int>?>()))
            .ReturnsAsync(new EntityCollection(new List<Entity> { entity }));

        var result = await _service.GetUnmanagedLayersAsync("MySolution");

        Assert.Equal("Unknown", result.LayeredComponents.Single().ComponentType);
    }

    [Fact]
    public async Task GetUnmanagedLayersAsync_MultipleComponents_OnlyFlagsUnmanagedOnes()
    {
        var layers = new EntityCollection(new List<Entity>
        {
            // comp-1: Active on top — flagged
            MakeLayer("comp-1", "rob_entity_a", "Entity", "Active", 2),
            MakeLayer("comp-1", "rob_entity_a", "Entity", "MySolution", 1),
            // comp-2: Managed on top — clean
            MakeLayer("comp-2", "rob_entity_b", "Entity", "MySolution", 2),
            MakeLayer("comp-2", "rob_entity_b", "Entity", "Active", 1),
            // comp-3: Only managed layer — clean
            MakeLayer("comp-3", "rob_form_a", "Form", "MySolution", 1),
        });

        _mockClient.Setup(c => c.GetSolutionComponentLayersAsync("MySolution", It.IsAny<Action<int, int, int>?>()))
            .ReturnsAsync(layers);

        var result = await _service.GetUnmanagedLayersAsync("MySolution");

        Assert.Single(result.LayeredComponents);
        Assert.Equal("rob_entity_a", result.LayeredComponents[0].ComponentName);
    }

    [Fact]
    public async Task GetUnmanagedLayersAsync_ResultsSortedByTypeThenName()
    {
        var layers = new EntityCollection(new List<Entity>
        {
            MakeLayer("comp-3", "Z_form", "Form", "Active", 2),
            MakeLayer("comp-3", "Z_form", "Form", "MySolution", 1),
            MakeLayer("comp-1", "A_entity", "Entity", "Active", 2),
            MakeLayer("comp-1", "A_entity", "Entity", "MySolution", 1),
            MakeLayer("comp-2", "B_entity", "Entity", "Active", 2),
            MakeLayer("comp-2", "B_entity", "Entity", "MySolution", 1),
        });

        _mockClient.Setup(c => c.GetSolutionComponentLayersAsync("MySolution", It.IsAny<Action<int, int, int>?>()))
            .ReturnsAsync(layers);

        var result = await _service.GetUnmanagedLayersAsync("MySolution");

        Assert.Equal(3, result.LayeredComponents.Count);
        Assert.Equal("Entity", result.LayeredComponents[0].ComponentType);
        Assert.Equal("A_entity", result.LayeredComponents[0].ComponentName);
        Assert.Equal("Entity", result.LayeredComponents[1].ComponentType);
        Assert.Equal("B_entity", result.LayeredComponents[1].ComponentName);
        Assert.Equal("Form", result.LayeredComponents[2].ComponentType);
    }

    [Fact]
    public async Task GetUnmanagedLayersAsync_SetsEnvironmentUrlAndSolutionName()
    {
        _mockClient.Setup(c => c.GetSolutionComponentLayersAsync("MySolution", It.IsAny<Action<int, int, int>?>()))
            .ReturnsAsync(new EntityCollection());

        var result = await _service.GetUnmanagedLayersAsync("MySolution");

        Assert.Equal("MySolution", result.SolutionName);
        Assert.Equal("https://test.crm.dynamics.com", result.EnvironmentUrl);
    }
}
