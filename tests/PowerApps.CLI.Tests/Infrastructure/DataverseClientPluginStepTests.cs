using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Moq;
using PowerApps.CLI.Infrastructure;
using Xunit;

namespace PowerApps.CLI.Tests.Infrastructure;

public class DataverseClientPluginStepTests
{
    private readonly Mock<IOrganizationService> _mockOrgService;
    private readonly DataverseClient _client;

    public DataverseClientPluginStepTests()
    {
        _mockOrgService = new Mock<IOrganizationService>();
        _client = new DataverseClient(_mockOrgService.Object);
    }

    #region RetrievePluginSteps Tests

    [Fact]
    public void RetrievePluginSteps_WithNoSolutions_QueriesCorrectEntityAndColumns()
    {
        // Arrange
        QueryExpression? capturedQuery = null;
        _mockOrgService.Setup(s => s.RetrieveMultiple(It.IsAny<QueryBase>()))
            .Callback<QueryBase>(q => capturedQuery = q as QueryExpression)
            .Returns(new EntityCollection());

        // Act
        _client.RetrievePluginSteps(new List<string>());

        // Assert
        Assert.NotNull(capturedQuery);
        Assert.Equal("sdkmessageprocessingstep", capturedQuery!.EntityName);
        Assert.Contains("sdkmessageprocessingstepid", capturedQuery.ColumnSet.Columns);
        Assert.Contains("name", capturedQuery.ColumnSet.Columns);
        Assert.Contains("statecode", capturedQuery.ColumnSet.Columns);
        Assert.Empty(capturedQuery.LinkEntities);
    }

    [Fact]
    public void RetrievePluginSteps_WithMultipleSolutions_UsesInConditionOnSingleLink()
    {
        // Arrange
        QueryExpression? capturedQuery = null;
        _mockOrgService.Setup(s => s.RetrieveMultiple(It.IsAny<QueryBase>()))
            .Callback<QueryBase>(q => capturedQuery = q as QueryExpression)
            .Returns(new EntityCollection());

        // Act
        _client.RetrievePluginSteps(new List<string> { "SolutionA", "SolutionB" });

        // Assert
        Assert.NotNull(capturedQuery);
        Assert.Single(capturedQuery!.LinkEntities);
        var solutionLink = capturedQuery.LinkEntities[0].LinkEntities[0];
        var condition = Assert.Single(solutionLink.LinkCriteria.Conditions);
        Assert.Equal(ConditionOperator.In, condition.Operator);
        Assert.Contains("SolutionA", condition.Values.Cast<string>());
        Assert.Contains("SolutionB", condition.Values.Cast<string>());
    }

    [Fact]
    public void RetrievePluginSteps_ReturnsClientResult()
    {
        // Arrange
        var stepId = Guid.NewGuid();
        var entity = new Entity("sdkmessageprocessingstep", stepId);
        var expected = new EntityCollection(new List<Entity> { entity });
        _mockOrgService.Setup(s => s.RetrieveMultiple(It.IsAny<QueryBase>())).Returns(expected);

        // Act
        var result = _client.RetrievePluginSteps(new List<string>());

        // Assert
        Assert.Same(expected, result);
    }

    #endregion

    #region EnablePluginStep Tests

    [Fact]
    public void EnablePluginStep_SendsSetStateRequestWithCorrectValues()
    {
        // Arrange
        var stepId = Guid.NewGuid();
        SetStateRequest? capturedRequest = null;
        _mockOrgService.Setup(s => s.Execute(It.IsAny<OrganizationRequest>()))
            .Callback<OrganizationRequest>(req => capturedRequest = req as SetStateRequest)
            .Returns(new OrganizationResponse());

        // Act
        _client.EnablePluginStep(stepId);

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.Equal("sdkmessageprocessingstep", capturedRequest!.EntityMoniker.LogicalName);
        Assert.Equal(stepId, capturedRequest.EntityMoniker.Id);
        Assert.Equal(0, capturedRequest.State.Value);
        Assert.Equal(1, capturedRequest.Status.Value);
    }

    #endregion

    #region DisablePluginStep Tests

    [Fact]
    public void DisablePluginStep_SendsSetStateRequestWithCorrectValues()
    {
        // Arrange
        var stepId = Guid.NewGuid();
        SetStateRequest? capturedRequest = null;
        _mockOrgService.Setup(s => s.Execute(It.IsAny<OrganizationRequest>()))
            .Callback<OrganizationRequest>(req => capturedRequest = req as SetStateRequest)
            .Returns(new OrganizationResponse());

        // Act
        _client.DisablePluginStep(stepId);

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.Equal("sdkmessageprocessingstep", capturedRequest!.EntityMoniker.LogicalName);
        Assert.Equal(stepId, capturedRequest.EntityMoniker.Id);
        Assert.Equal(1, capturedRequest.State.Value);
        Assert.Equal(2, capturedRequest.Status.Value);
    }

    #endregion
}
