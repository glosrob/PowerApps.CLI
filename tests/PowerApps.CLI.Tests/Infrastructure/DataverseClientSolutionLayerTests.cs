using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using Moq;
using PowerApps.CLI.Infrastructure;
using Xunit;

namespace PowerApps.CLI.Tests.Infrastructure;

public class DataverseClientSolutionLayerTests
{
    private const int UnmappedTypeCode = 9999;

    private readonly Mock<IOrganizationService> _mockOrgService;
    private readonly DataverseClient _client;
    private readonly List<QueryExpression> _layerQueries = new();
    private readonly List<string> _phaseLog = new();

    public DataverseClientSolutionLayerTests()
    {
        _mockOrgService = new Mock<IOrganizationService>();
        _client = new DataverseClient(_mockOrgService.Object);

        // solutioncomponentdefinition contributes nothing, mirroring the real behaviour for 430-432.
        _mockOrgService
            .Setup(s => s.RetrieveMultiple(It.Is<QueryBase>(q => ((QueryExpression)q).EntityName == "solutioncomponentdefinition")))
            .Returns(new EntityCollection());

        // Capture each msdyn_componentlayer query and answer with an empty result.
        _mockOrgService
            .Setup(s => s.Execute(It.IsAny<ExecuteMultipleRequest>()))
            .Returns<OrganizationRequest>(r => BuildExecuteMultipleResponse((ExecuteMultipleRequest)r));
    }

    [Theory]
    [InlineData(430, "EntityAnalyticsConfig")]
    [InlineData(431, "AttributeImageConfig")]
    [InlineData(432, "EntityImageConfig")]
    public async Task GetSolutionComponentLayersAsync_ImageAndAnalyticsConfigTypes_QueryWithCorrectRoutingKey(int typeCode, string expectedRoutingKey)
    {
        // Arrange
        var componentId = Guid.NewGuid();
        SetupSolutionComponents((componentId, typeCode));

        // Act
        await _client.GetSolutionComponentLayersAsync("TestSolution", phaseLog: _phaseLog.Add);

        // Assert
        var query = Assert.Single(_layerQueries);
        Assert.Equal("msdyn_componentlayer", query.EntityName);
        Assert.Contains(query.Criteria.Conditions, c =>
            c.AttributeName == "msdyn_solutioncomponentname" && (string)c.Values[0] == expectedRoutingKey);
        Assert.Contains(query.Criteria.Conditions, c =>
            c.AttributeName == "msdyn_componentid" && (Guid)c.Values[0] == componentId);
        Assert.DoesNotContain(_phaseLog, m => m.Contains("no routing key"));
    }

    [Fact]
    public async Task GetSolutionComponentLayersAsync_UnmappedType_WarningIncludesChoiceLabel()
    {
        // Arrange
        SetupSolutionComponents((Guid.NewGuid(), UnmappedTypeCode));
        SetupComponentTypeLabels((UnmappedTypeCode, "Mystery Component"));

        // Act
        await _client.GetSolutionComponentLayersAsync("TestSolution", phaseLog: _phaseLog.Add);

        // Assert
        Assert.Contains(_phaseLog, m =>
            m.Contains($"[{UnmappedTypeCode} (Mystery Component)]") && m.Contains("no routing key"));
        Assert.Empty(_layerQueries);
    }

    [Fact]
    public async Task GetSolutionComponentLayersAsync_UnmappedTypeWithoutLabel_WarningShowsBareCode()
    {
        // Arrange
        SetupSolutionComponents((Guid.NewGuid(), UnmappedTypeCode));
        SetupComponentTypeLabels((1, "Entity"));

        // Act
        await _client.GetSolutionComponentLayersAsync("TestSolution", phaseLog: _phaseLog.Add);

        // Assert
        Assert.Contains(_phaseLog, m => m.Contains($"[{UnmappedTypeCode}]") && m.Contains("no routing key"));
    }

    [Fact]
    public async Task GetSolutionComponentLayersAsync_LabelLookupFails_WarningFallsBackToBareCode()
    {
        // Arrange
        SetupSolutionComponents((Guid.NewGuid(), UnmappedTypeCode));
        _mockOrgService
            .Setup(s => s.Execute(It.IsAny<RetrieveOptionSetRequest>()))
            .Throws(new InvalidOperationException("metadata unavailable"));

        // Act
        await _client.GetSolutionComponentLayersAsync("TestSolution", phaseLog: _phaseLog.Add);

        // Assert
        Assert.Contains(_phaseLog, m => m.Contains($"[{UnmappedTypeCode}]") && m.Contains("no routing key"));
    }

    [Fact]
    public async Task GetSolutionComponentLayersAsync_AllTypesMapped_DoesNotQueryChoiceLabels()
    {
        // Arrange
        SetupSolutionComponents((Guid.NewGuid(), 431), (Guid.NewGuid(), 432));

        // Act
        await _client.GetSolutionComponentLayersAsync("TestSolution", phaseLog: _phaseLog.Add);

        // Assert
        _mockOrgService.Verify(s => s.Execute(It.IsAny<RetrieveOptionSetRequest>()), Times.Never);
        Assert.Equal(2, _layerQueries.Count);
    }

    private void SetupSolutionComponents(params (Guid Id, int TypeCode)[] components)
    {
        var collection = new EntityCollection();
        foreach (var (id, typeCode) in components)
        {
            var entity = new Entity("solutioncomponent", Guid.NewGuid());
            entity["objectid"] = id;
            entity["componenttype"] = new OptionSetValue(typeCode);
            collection.Entities.Add(entity);
        }

        _mockOrgService
            .Setup(s => s.RetrieveMultiple(It.Is<QueryBase>(q => ((QueryExpression)q).EntityName == "solutioncomponent")))
            .Returns(collection);
    }

    private void SetupComponentTypeLabels(params (int Value, string Label)[] options)
    {
        var optionSet = new OptionSetMetadata();
        foreach (var (value, label) in options)
        {
            var localized = new LocalizedLabel(label, 1033);
            var optionLabel = new Label(localized, new[] { localized });
            optionSet.Options.Add(new OptionMetadata(optionLabel, value));
        }

        var response = new RetrieveOptionSetResponse();
        response.Results["OptionSetMetadata"] = optionSet;

        _mockOrgService
            .Setup(s => s.Execute(It.IsAny<RetrieveOptionSetRequest>()))
            .Returns(response);
    }

    private ExecuteMultipleResponse BuildExecuteMultipleResponse(ExecuteMultipleRequest request)
    {
        var items = new ExecuteMultipleResponseItemCollection();
        for (var i = 0; i < request.Requests.Count; i++)
        {
            var retrieve = (RetrieveMultipleRequest)request.Requests[i];
            lock (_layerQueries)
            {
                _layerQueries.Add((QueryExpression)retrieve.Query);
            }

            var retrieveResponse = new RetrieveMultipleResponse();
            retrieveResponse.Results["EntityCollection"] = new EntityCollection();
            items.Add(new ExecuteMultipleResponseItem { RequestIndex = i, Response = retrieveResponse });
        }

        var response = new ExecuteMultipleResponse();
        response.Results["Responses"] = items;
        return response;
    }
}
