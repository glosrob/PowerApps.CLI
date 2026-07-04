using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using Moq;
using PowerApps.CLI.Infrastructure;
using Xunit;

namespace PowerApps.CLI.Tests.Infrastructure;

public class DataverseClientSolutionLayerTests
{
    // Component type code that requires no attribute/form/view/chart expansion (Phase 1b/1c),
    // keeping these tests focused on the Phase 1d Active-solution filter and Phase 2 ColumnSet.
    private const int SimpleComponentTypeCode = 29; // "Workflow" in the static type-name map.

    private readonly Mock<IOrganizationService> _mockOrgService;
    private readonly DataverseClient _client;

    private readonly List<QueryExpression> _capturedActiveQueries = new();
    private readonly List<(int PageNumber, string? PagingCookie)> _capturedActivePageInfos = new();
    private readonly List<RetrieveMultipleRequest> _capturedPhase2Requests = new();

    public DataverseClientSolutionLayerTests()
    {
        _mockOrgService = new Mock<IOrganizationService>();
        _client = new DataverseClient(_mockOrgService.Object);
    }

    private static Entity MakeComponentEntity(Guid objectId, int typeCode)
    {
        var entity = new Entity("solutioncomponent");
        entity["objectid"] = objectId;
        entity["componenttype"] = new OptionSetValue(typeCode);
        return entity;
    }

    private static Entity MakeActiveIdEntity(Guid objectId)
    {
        var entity = new Entity("solutioncomponent");
        entity["objectid"] = objectId;
        return entity;
    }

    private static string GetLinkedSolutionUniqueName(QueryExpression query)
    {
        var link = query.LinkEntities.Single();
        var condition = link.LinkCriteria.Conditions.Single(c => c.AttributeName == "uniquename");
        return (string)condition.Values[0];
    }

    /// <summary>
    /// Wires up RetrieveMultiple to dispatch based on query shape: Phase 1's solutioncomponent
    /// query (joined to the target solution), Phase 1d's solutioncomponent query (joined to
    /// "Active"), and GetModernComponentTypeNamesAsync's solutioncomponentdefinition query
    /// (returned empty so the static type-name map is used as-is).
    /// </summary>
    private void SetupPipeline(
        string solutionName,
        IReadOnlyList<Entity> phase1Components,
        Func<QueryExpression, EntityCollection> activeSolutionResponder)
    {
        _mockOrgService
            .Setup(s => s.RetrieveMultiple(It.IsAny<QueryBase>()))
            .Returns<QueryBase>(q =>
            {
                var qe = (QueryExpression)q;

                if (qe.EntityName == "solutioncomponentdefinition")
                {
                    return new EntityCollection();
                }

                if (qe.EntityName == "solutioncomponent")
                {
                    var targetUniqueName = GetLinkedSolutionUniqueName(qe);
                    if (targetUniqueName == "Active")
                    {
                        _capturedActiveQueries.Add(qe);
                        // Snapshot PageInfo as primitives — the same QueryExpression instance is
                        // mutated in place across paging calls, so capturing the reference alone
                        // would only ever reflect its final state.
                        _capturedActivePageInfos.Add((qe.PageInfo.PageNumber, qe.PageInfo.PagingCookie));
                        return activeSolutionResponder(qe);
                    }

                    Assert.Equal(solutionName, targetUniqueName);
                    return new EntityCollection(phase1Components.ToList());
                }

                return new EntityCollection();
            });

        _mockOrgService
            .Setup(s => s.Execute(It.IsAny<OrganizationRequest>()))
            .Returns<OrganizationRequest>(req =>
            {
                var multipleRequest = (ExecuteMultipleRequest)req;
                foreach (var inner in multipleRequest.Requests)
                {
                    _capturedPhase2Requests.Add((RetrieveMultipleRequest)inner);
                }

                var response = new ExecuteMultipleResponse();
                response.Results["Responses"] = new ExecuteMultipleResponseItemCollection();
                response.Results["IsFaulted"] = false;
                return response;
            });
    }

    private static Guid GetComponentId(RetrieveMultipleRequest request)
    {
        var query = (QueryExpression)request.Query;
        var condition = query.Criteria.Conditions.Single(c => c.AttributeName == "msdyn_componentid");
        return (Guid)condition.Values[0];
    }

    [Fact]
    public async Task GetSolutionComponentLayersAsync_ActiveSolutionQuery_TargetsCorrectEntityLinkAndColumn()
    {
        var componentId = Guid.NewGuid();
        SetupPipeline(
            "TestSolution",
            new[] { MakeComponentEntity(componentId, SimpleComponentTypeCode) },
            _ => new EntityCollection(new List<Entity> { MakeActiveIdEntity(componentId) }));

        await _client.GetSolutionComponentLayersAsync("TestSolution");

        var activeQuery = Assert.Single(_capturedActiveQueries);
        Assert.Equal("solutioncomponent", activeQuery.EntityName);
        Assert.Equal(new[] { "objectid" }, activeQuery.ColumnSet.Columns);

        var link = Assert.Single(activeQuery.LinkEntities);
        Assert.Equal("solution", link.LinkToEntityName);
        var condition = Assert.Single(link.LinkCriteria.Conditions);
        Assert.Equal("uniquename", condition.AttributeName);
        Assert.Equal(ConditionOperator.Equal, condition.Operator);
        Assert.Equal("Active", condition.Values[0]);
    }

    [Fact]
    public async Task GetSolutionComponentLayersAsync_ActiveSolutionQuery_PagesWhenMoreRecordsIsTrue()
    {
        var idOnPage1 = Guid.NewGuid();
        var idOnPage2 = Guid.NewGuid();
        var callCount = 0;

        SetupPipeline(
            "TestSolution",
            new[]
            {
                MakeComponentEntity(idOnPage1, SimpleComponentTypeCode),
                MakeComponentEntity(idOnPage2, SimpleComponentTypeCode)
            },
            _ =>
            {
                callCount++;
                if (callCount == 1)
                {
                    return new EntityCollection(new List<Entity> { MakeActiveIdEntity(idOnPage1) })
                    {
                        MoreRecords = true,
                        PagingCookie = "cookie-page-1"
                    };
                }

                return new EntityCollection(new List<Entity> { MakeActiveIdEntity(idOnPage2) })
                {
                    MoreRecords = false
                };
            });

        await _client.GetSolutionComponentLayersAsync("TestSolution");

        Assert.Equal(2, callCount);
        Assert.Equal(2, _capturedActivePageInfos.Count);
        Assert.Equal(1, _capturedActivePageInfos[0].PageNumber);
        Assert.Null(_capturedActivePageInfos[0].PagingCookie);
        Assert.Equal(2, _capturedActivePageInfos[1].PageNumber);
        Assert.Equal("cookie-page-1", _capturedActivePageInfos[1].PagingCookie);

        // Both pages' IDs should have made it into the Active set, so both components
        // are queried in Phase 2.
        var queriedIds = _capturedPhase2Requests.Select(GetComponentId).ToList();
        Assert.Contains(idOnPage1, queriedIds);
        Assert.Contains(idOnPage2, queriedIds);
    }

    [Fact]
    public async Task GetSolutionComponentLayersAsync_ComponentNotInActiveSolution_IsExcludedFromPhase2()
    {
        var activeComponentId = Guid.NewGuid();
        var inactiveComponentId = Guid.NewGuid();

        SetupPipeline(
            "TestSolution",
            new[]
            {
                MakeComponentEntity(activeComponentId, SimpleComponentTypeCode),
                MakeComponentEntity(inactiveComponentId, SimpleComponentTypeCode)
            },
            _ => new EntityCollection(new List<Entity> { MakeActiveIdEntity(activeComponentId) }));

        await _client.GetSolutionComponentLayersAsync("TestSolution");

        var queriedIds = _capturedPhase2Requests.Select(GetComponentId).ToList();
        Assert.Contains(activeComponentId, queriedIds);
        Assert.DoesNotContain(inactiveComponentId, queriedIds);
    }

    [Fact]
    public async Task GetSolutionComponentLayersAsync_Phase2Query_UsesExplicitColumnSetNotAllColumns()
    {
        var componentId = Guid.NewGuid();
        SetupPipeline(
            "TestSolution",
            new[] { MakeComponentEntity(componentId, SimpleComponentTypeCode) },
            _ => new EntityCollection(new List<Entity> { MakeActiveIdEntity(componentId) }));

        await _client.GetSolutionComponentLayersAsync("TestSolution");

        var request = Assert.Single(_capturedPhase2Requests);
        var query = (QueryExpression)request.Query;

        Assert.False(query.ColumnSet.AllColumns);
        var expectedColumns = new[] { "msdyn_componentid", "msdyn_order", "msdyn_solutionname", "msdyn_name", "msdyn_solutioncomponentname" };
        Assert.Equal(expectedColumns.OrderBy(c => c), query.ColumnSet.Columns.OrderBy(c => c));
    }
}
