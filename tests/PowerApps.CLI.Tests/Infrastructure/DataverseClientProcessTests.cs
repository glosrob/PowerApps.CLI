using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Moq;
using PowerApps.CLI.Infrastructure;
using Xunit;

namespace PowerApps.CLI.Tests.Infrastructure;

public class DataverseClientProcessTests
{
    private readonly Mock<IOrganizationService> _mockOrgService;
    private readonly DataverseClient _client;

    public DataverseClientProcessTests()
    {
        _mockOrgService = new Mock<IOrganizationService>();
        _client = new DataverseClient(_mockOrgService.Object);
    }

    #region RetrieveProcesses Tests

    [Fact]
    public void RetrieveProcesses_WithNoSolutions_QueriesCorrectEntityAndColumns()
    {
        // Arrange
        QueryExpression? capturedQuery = null;
        _mockOrgService.Setup(s => s.RetrieveMultiple(It.IsAny<QueryBase>()))
            .Callback<QueryBase>(q => capturedQuery = q as QueryExpression)
            .Returns(new EntityCollection());

        // Act
        _client.RetrieveProcesses(new List<string>());

        // Assert
        Assert.NotNull(capturedQuery);
        Assert.Equal("workflow", capturedQuery!.EntityName);
        Assert.Contains("workflowid", capturedQuery.ColumnSet.Columns);
        Assert.Contains("name", capturedQuery.ColumnSet.Columns);
        Assert.Contains("statecode", capturedQuery.ColumnSet.Columns);
        Assert.Contains("category", capturedQuery.ColumnSet.Columns);
        Assert.Empty(capturedQuery.LinkEntities);
    }

    [Fact]
    public void RetrieveProcesses_WithOneSolution_AddsSingleLink()
    {
        // Arrange
        QueryExpression? capturedQuery = null;
        _mockOrgService.Setup(s => s.RetrieveMultiple(It.IsAny<QueryBase>()))
            .Callback<QueryBase>(q => capturedQuery = q as QueryExpression)
            .Returns(new EntityCollection());

        // Act
        _client.RetrieveProcesses(new List<string> { "SolutionA" });

        // Assert
        Assert.NotNull(capturedQuery);
        Assert.Single(capturedQuery!.LinkEntities);
    }

    [Fact]
    public void RetrieveProcesses_WithMultipleSolutions_UsesInConditionOnSingleLink()
    {
        // Arrange
        QueryExpression? capturedQuery = null;
        _mockOrgService.Setup(s => s.RetrieveMultiple(It.IsAny<QueryBase>()))
            .Callback<QueryBase>(q => capturedQuery = q as QueryExpression)
            .Returns(new EntityCollection());

        // Act
        _client.RetrieveProcesses(new List<string> { "SolutionA", "SolutionB" });

        // Assert — single join, IN condition with both names, not one join per solution
        Assert.NotNull(capturedQuery);
        Assert.Single(capturedQuery!.LinkEntities);
        var solutionLink = capturedQuery.LinkEntities[0].LinkEntities[0];
        var condition = Assert.Single(solutionLink.LinkCriteria.Conditions);
        Assert.Equal(ConditionOperator.In, condition.Operator);
        Assert.Contains("SolutionA", condition.Values.Cast<string>());
        Assert.Contains("SolutionB", condition.Values.Cast<string>());
    }

    #endregion

    #region ActivateProcess / DeactivateProcess Tests

    [Fact]
    public void ActivateProcess_SendsSetStateRequestWithCorrectValues()
    {
        // Arrange
        var processId = Guid.NewGuid();
        SetStateRequest? capturedRequest = null;
        _mockOrgService.Setup(s => s.Execute(It.IsAny<OrganizationRequest>()))
            .Callback<OrganizationRequest>(req => capturedRequest = req as SetStateRequest)
            .Returns(new OrganizationResponse());

        // Act
        _client.ActivateProcess(processId);

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.Equal("workflow", capturedRequest!.EntityMoniker.LogicalName);
        Assert.Equal(processId, capturedRequest.EntityMoniker.Id);
        Assert.Equal(1, capturedRequest.State.Value);  // Active
        Assert.Equal(2, capturedRequest.Status.Value); // Activated
    }

    [Fact]
    public void DeactivateProcess_SendsSetStateRequestWithCorrectValues()
    {
        // Arrange
        var processId = Guid.NewGuid();
        SetStateRequest? capturedRequest = null;
        _mockOrgService.Setup(s => s.Execute(It.IsAny<OrganizationRequest>()))
            .Callback<OrganizationRequest>(req => capturedRequest = req as SetStateRequest)
            .Returns(new OrganizationResponse());

        // Act
        _client.DeactivateProcess(processId);

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.Equal("workflow", capturedRequest!.EntityMoniker.LogicalName);
        Assert.Equal(processId, capturedRequest.EntityMoniker.Id);
        Assert.Equal(0, capturedRequest.State.Value);  // Inactive
        Assert.Equal(1, capturedRequest.Status.Value); // Draft
    }

    #endregion

    #region RetrieveDuplicateRules Tests

    [Fact]
    public void RetrieveDuplicateRules_WithNoSolutions_QueriesCorrectEntityAndColumns()
    {
        // Arrange
        QueryExpression? capturedQuery = null;
        _mockOrgService.Setup(s => s.RetrieveMultiple(It.IsAny<QueryBase>()))
            .Callback<QueryBase>(q => capturedQuery = q as QueryExpression)
            .Returns(new EntityCollection());

        // Act
        _client.RetrieveDuplicateRules(new List<string>());

        // Assert
        Assert.NotNull(capturedQuery);
        Assert.Equal("duplicaterule", capturedQuery!.EntityName);
        Assert.Contains("duplicateruleid", capturedQuery.ColumnSet.Columns);
        Assert.Contains("name", capturedQuery.ColumnSet.Columns);
        Assert.Contains("statecode", capturedQuery.ColumnSet.Columns);
        Assert.Empty(capturedQuery.LinkEntities);
    }

    #endregion

    [Fact]
    public void RetrieveDuplicateRules_WithMultipleSolutions_UsesInConditionOnSingleLink()
    {
        // Arrange
        QueryExpression? capturedQuery = null;
        _mockOrgService.Setup(s => s.RetrieveMultiple(It.IsAny<QueryBase>()))
            .Callback<QueryBase>(q => capturedQuery = q as QueryExpression)
            .Returns(new EntityCollection());

        // Act
        _client.RetrieveDuplicateRules(new List<string> { "SolutionA", "SolutionB" });

        // Assert
        Assert.NotNull(capturedQuery);
        Assert.Single(capturedQuery!.LinkEntities);
        var solutionLink = capturedQuery.LinkEntities[0].LinkEntities[0];
        var condition = Assert.Single(solutionLink.LinkCriteria.Conditions);
        Assert.Equal(ConditionOperator.In, condition.Operator);
        Assert.Contains("SolutionA", condition.Values.Cast<string>());
        Assert.Contains("SolutionB", condition.Values.Cast<string>());
    }

    #region ActivateDuplicateRule / DeactivateDuplicateRule Tests

    [Fact]
    public void ActivateDuplicateRule_SendsPublishDuplicateRuleRequest()
    {
        // Arrange
        var ruleId = Guid.NewGuid();
        PublishDuplicateRuleRequest? capturedRequest = null;
        _mockOrgService.Setup(s => s.Execute(It.IsAny<OrganizationRequest>()))
            .Callback<OrganizationRequest>(req => capturedRequest = req as PublishDuplicateRuleRequest)
            .Returns(new OrganizationResponse());

        // Act
        _client.ActivateDuplicateRule(ruleId);

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.Equal(ruleId, capturedRequest!.DuplicateRuleId);
    }

    [Fact]
    public void DeactivateDuplicateRule_SendsSetStateRequestWithCorrectValues()
    {
        // Arrange
        var ruleId = Guid.NewGuid();
        SetStateRequest? capturedRequest = null;
        _mockOrgService.Setup(s => s.Execute(It.IsAny<OrganizationRequest>()))
            .Callback<OrganizationRequest>(req => capturedRequest = req as SetStateRequest)
            .Returns(new OrganizationResponse());

        // Act
        _client.DeactivateDuplicateRule(ruleId);

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.Equal("duplicaterule", capturedRequest!.EntityMoniker.LogicalName);
        Assert.Equal(ruleId, capturedRequest.EntityMoniker.Id);
        Assert.Equal(0, capturedRequest.State.Value);  // Inactive
        Assert.Equal(0, capturedRequest.Status.Value); // Unpublished
    }

    #endregion
}
