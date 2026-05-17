namespace WorkforceMapper.Server.Services;

public sealed record RiskReachProduct(int Id, string Name, decimal ArrUsd);

public sealed record RiskReachApplication(int Id, string Name, string Division);

public sealed record RiskReachApplicationRisk(RiskReachApplication Application, RiskLevel Risk);

public sealed class RiskReach
{
    private readonly DependencyGraph _graph;
    private readonly Dictionary<int, HashSet<int>> _appsByProduct;
    private readonly Dictionary<int, HashSet<int>> _directDependenciesByApplication;
    private readonly Dictionary<int, RiskReachProduct> _productsById;
    private readonly Dictionary<int, RiskReachApplication> _applicationsById;

    public RiskReach(
        DependencyGraph graph,
        IEnumerable<(int ProductId, int ApplicationId)> productApps,
        IEnumerable<RiskReachProduct> products,
        IEnumerable<RiskReachApplication> applications,
        IEnumerable<(int ApplicationId, int DependsOnApplicationId)>? dependencyEdges = null)
    {
        _graph = graph;
        _appsByProduct = productApps
            .GroupBy(pa => pa.ProductId)
            .ToDictionary(g => g.Key, g => g.Select(pa => pa.ApplicationId).ToHashSet());
        _directDependenciesByApplication = (dependencyEdges ?? [])
            .GroupBy(edge => edge.ApplicationId)
            .ToDictionary(g => g.Key, g => g.Select(edge => edge.DependsOnApplicationId).ToHashSet());
        _productsById = products.ToDictionary(p => p.Id);
        _applicationsById = applications.ToDictionary(a => a.Id);
    }

    public IReadOnlySet<int> ProductClosure(int productId)
    {
        var directAppIds = _appsByProduct.TryGetValue(productId, out var appIds) ? appIds : [];
        var closure = new HashSet<int>(directAppIds);
        foreach (var appId in directAppIds)
            foreach (var dep in _graph.TransitiveDependenciesOf(appId))
                closure.Add(dep);

        return closure;
    }

    public IReadOnlyList<RiskReachProduct> ProductsReachedByApplication(int applicationId) =>
        _productsById.Values
            .Where(product => ProductClosure(product.Id).Contains(applicationId))
            .OrderBy(product => product.Name)
            .ToList();

    public IReadOnlyList<RiskReachApplication> TransitiveDependentApplications(int applicationId) =>
        _graph.TransitiveDependentsOf(applicationId)
            .Where(_applicationsById.ContainsKey)
            .Select(id => _applicationsById[id])
            .OrderBy(application => application.Name)
            .ToList();

    public IReadOnlyList<RiskReachApplicationRisk> ProductRiskApplications(
        int productId,
        IReadOnlyDictionary<int, RiskLevel> riskByApplicationId) =>
        ProductClosure(productId)
            .Where(_applicationsById.ContainsKey)
            .Select(id => new RiskReachApplicationRisk(
                _applicationsById[id],
                riskByApplicationId.TryGetValue(id, out var risk) ? risk : RiskLevel.Low))
            .OrderByDescending(application => application.Risk)
            .ThenBy(application => application.Application.Name)
            .ToList();

    public IReadOnlyList<RiskReachApplicationRisk> DirectApplicationRisks(
        int productId,
        IReadOnlyDictionary<int, RiskLevel> riskByApplicationId) =>
        (_appsByProduct.TryGetValue(productId, out var directAppIds) ? directAppIds : [])
            .ToRiskReachApplicationRisks(_applicationsById, riskByApplicationId);

    public IReadOnlyList<RiskReachApplicationRisk> DirectDependencyRisks(
        int applicationId,
        IReadOnlyDictionary<int, RiskLevel> riskByApplicationId) =>
        (_directDependenciesByApplication.TryGetValue(applicationId, out var dependencyIds) ? dependencyIds : [])
            .ToRiskReachApplicationRisks(_applicationsById, riskByApplicationId);
}

internal static class RiskReachOrdering
{
    public static IReadOnlyList<RiskReachApplicationRisk> ToRiskReachApplicationRisks(
        this IEnumerable<int> applicationIds,
        IReadOnlyDictionary<int, RiskReachApplication> applicationsById,
        IReadOnlyDictionary<int, RiskLevel> riskByApplicationId) =>
        applicationIds
            .Where(applicationsById.ContainsKey)
            .Select(id => new RiskReachApplicationRisk(
                applicationsById[id],
                riskByApplicationId.TryGetValue(id, out var risk) ? risk : RiskLevel.Low))
            .OrderByDescending(application => application.Risk)
            .ThenBy(application => application.Application.Name)
            .ToList();
}
