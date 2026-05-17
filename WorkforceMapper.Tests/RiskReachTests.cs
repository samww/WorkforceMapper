using WorkforceMapper.Server.Services;

namespace WorkforceMapper.Tests;

public class RiskReachTests
{
    [Fact]
    public void ProductClosure_ReturnsDirectAndTransitiveApplications()
    {
        var graph = new DependencyGraph([(1, 2), (2, 3)], [(10, 1)]);
        var reach = new RiskReach(
            graph,
            [(10, 1)],
            [new RiskReachProduct(10, "Fieldwork", 1_000_000m)],
            [
                new RiskReachApplication(1, "FieldOps", "Client Services"),
                new RiskReachApplication(2, "QuotaManager", "Client Services"),
                new RiskReachApplication(3, "AuthService", "Platform Engineering")
            ]);

        var result = reach.ProductClosure(10);

        Assert.Equal([1, 2, 3], result.Order().ToArray());
    }

    [Fact]
    public void ProductsReachedByApplication_ReturnsProductsWhoseClosuresContainTheApplication_OrderedByName()
    {
        var graph = new DependencyGraph([(1, 3), (2, 3)], [(10, 1), (20, 2), (30, 4)]);
        var reach = new RiskReach(
            graph,
            [(10, 1), (20, 2), (30, 4)],
            [
                new RiskReachProduct(10, "Zoo Analytics", 1_000_000m),
                new RiskReachProduct(20, "Alpha Fieldwork", 2_000_000m),
                new RiskReachProduct(30, "Billing", 3_000_000m)
            ],
            [
                new RiskReachApplication(1, "FieldOps", "Client Services"),
                new RiskReachApplication(2, "QuotaManager", "Client Services"),
                new RiskReachApplication(3, "AuthService", "Platform Engineering"),
                new RiskReachApplication(4, "BillingApi", "Platform Engineering")
            ]);

        var result = reach.ProductsReachedByApplication(3);

        Assert.Equal(["Alpha Fieldwork", "Zoo Analytics"], result.Select(p => p.Name).ToArray());
    }

    [Fact]
    public void TransitiveDependentApplications_ReturnsAppsOrderedByName()
    {
        var graph = new DependencyGraph([(1, 3), (2, 3), (4, 1)], []);
        var reach = new RiskReach(
            graph,
            [],
            [],
            [
                new RiskReachApplication(1, "FieldOps", "Client Services"),
                new RiskReachApplication(2, "Alpha Quota", "Client Services"),
                new RiskReachApplication(3, "AuthService", "Platform Engineering"),
                new RiskReachApplication(4, "Route Planner", "Client Services")
            ]);

        var result = reach.TransitiveDependentApplications(3);

        Assert.Equal(["Alpha Quota", "FieldOps", "Route Planner"], result.Select(a => a.Name).ToArray());
    }

    [Fact]
    public void ProductRiskApplications_ReturnsClosureAppsOrderedByRiskDescendingThenName()
    {
        var graph = new DependencyGraph([(1, 3), (2, 4)], [(10, 1), (10, 2)]);
        var reach = new RiskReach(
            graph,
            [(10, 1), (10, 2)],
            [new RiskReachProduct(10, "Fieldwork", 1_000_000m)],
            [
                new RiskReachApplication(1, "Zoo Direct", "Client Services"),
                new RiskReachApplication(2, "Alpha Direct", "Client Services"),
                new RiskReachApplication(3, "Billing", "Platform Engineering"),
                new RiskReachApplication(4, "AuthService", "Platform Engineering")
            ]);
        var riskByApp = new Dictionary<int, RiskLevel>
        {
            [1] = RiskLevel.Medium,
            [2] = RiskLevel.High,
            [3] = RiskLevel.High,
            [4] = RiskLevel.Low
        };

        var result = reach.ProductRiskApplications(10, riskByApp);

        Assert.Equal(
            ["Alpha Direct:High", "Billing:High", "Zoo Direct:Medium", "AuthService:Low"],
            result.Select(a => $"{a.Application.Name}:{a.Risk}").ToArray());
    }

    [Fact]
    public void DirectApplicationRisks_ReturnDirectAppsAndDirectDependenciesOrderedByRiskDescendingThenName()
    {
        var edges = new (int ApplicationId, int DependsOnApplicationId)[] { (1, 3), (1, 4), (2, 4) };
        var graph = new DependencyGraph(edges, [(10, 1), (10, 2)]);
        var reach = new RiskReach(
            graph,
            [(10, 1), (10, 2)],
            [new RiskReachProduct(10, "Fieldwork", 1_000_000m)],
            [
                new RiskReachApplication(1, "Zoo Direct", "Client Services"),
                new RiskReachApplication(2, "Alpha Direct", "Client Services"),
                new RiskReachApplication(3, "Billing", "Platform Engineering"),
                new RiskReachApplication(4, "AuthService", "Platform Engineering")
            ],
            edges);
        var riskByApp = new Dictionary<int, RiskLevel>
        {
            [1] = RiskLevel.Medium,
            [2] = RiskLevel.High,
            [3] = RiskLevel.High,
            [4] = RiskLevel.Low
        };

        var directApps = reach.DirectApplicationRisks(10, riskByApp);
        var directDependencies = reach.DirectDependencyRisks(1, riskByApp);

        Assert.Equal(["Alpha Direct:High", "Zoo Direct:Medium"], directApps.Select(a => $"{a.Application.Name}:{a.Risk}").ToArray());
        Assert.Equal(["Billing:High", "AuthService:Low"], directDependencies.Select(a => $"{a.Application.Name}:{a.Risk}").ToArray());
    }
}
