using Microsoft.EntityFrameworkCore;
using WorkforceMapper.Server.Data;
using WorkforceMapper.Server.Models;

namespace WorkforceMapper.Server.Services;

public class ProductRiskReporter(WorkforceDbContext context)
{
    public async Task<ProductRiskSummary> ReportAsync(string month, int? productId = null)
    {
        var appRisks = await new ApplicationRiskReporter(context).ReportAsync(month);
        var riskByAppId = appRisks.ToDictionary(a => a.id, a =>
            Enum.Parse<RiskLevel>(a.risk, ignoreCase: true));

        var edges = await context.ApplicationDependencies
            .Select(d => new { d.ApplicationId, d.DependsOnApplicationId })
            .ToListAsync();
        var productApps = await context.ProductApplications
            .Select(pa => new { pa.ProductId, pa.ApplicationId })
            .ToListAsync();

        var graph = new DependencyGraph(
            edges.Select(e => (e.ApplicationId, e.DependsOnApplicationId)),
            productApps.Select(pa => (pa.ProductId, pa.ApplicationId)));

        var products = await context.Products.ToListAsync();
        if (productId.HasValue)
            products = products.Where(p => p.Id == productId.Value).ToList();

        var paByProduct = productApps
            .GroupBy(pa => pa.ProductId)
            .ToDictionary(g => g.Key, g => g.Select(pa => pa.ApplicationId).ToHashSet());

        var directDepsOfApp = edges
            .GroupBy(e => e.ApplicationId)
            .ToDictionary(g => g.Key, g => g.Select(e => e.DependsOnApplicationId).ToList());

        string RiskStr(int appId) =>
            (riskByAppId.TryGetValue(appId, out var r) ? r : RiskLevel.Low).ToString().ToLowerInvariant();

        string AppName(int appId) => appRisks.FirstOrDefault(a => a.id == appId)?.name ?? "";
        string AppDivision(int appId) => appRisks.FirstOrDefault(a => a.id == appId)?.division ?? "";

        var paTuples = productApps.Select(pa => (pa.ProductId, pa.ApplicationId));

        var productList = products.Select(product =>
        {
            var directAppIds = paByProduct.TryGetValue(product.Id, out var d) ? d : [];
            var closure = BuildProductClosure(product.Id, paTuples, graph);

            var contributing = closure
                .Select(id => new { id, risk = riskByAppId.TryGetValue(id, out var r) ? r : RiskLevel.Low, name = AppName(id) })
                .Where(a => a.name != "")
                .OrderByDescending(a => a.risk)
                .ThenBy(a => a.name)
                .Take(10)
                .Select(a => new ContributingAppDto(a.id, a.name, a.risk.ToString().ToLowerInvariant(), AppDivision(a.id)))
                .ToList();

            var maxRisk = closure
                .Select(id => riskByAppId.TryGetValue(id, out var r) ? r : RiskLevel.Low)
                .Aggregate(RiskLevel.Low, (acc, r) => r > acc ? r : acc);

            var directAppNodes = directAppIds
                .Select(appId =>
                {
                    var deps = (directDepsOfApp.TryGetValue(appId, out var depList) ? depList : [])
                        .Where(depId => AppName(depId) != "")
                        .Select(depId => new ContributingAppDto(depId, AppName(depId), RiskStr(depId), AppDivision(depId)))
                        .OrderByDescending(a => a.risk)
                        .ThenBy(a => a.name)
                        .ToList();
                    return new DirectAppNodeDto(appId, AppName(appId), RiskStr(appId), deps);
                })
                .Where(a => a.name != "")
                .OrderByDescending(a => a.risk)
                .ThenBy(a => a.name)
                .ToList();

            return new ProductRiskDto(
                product.Id,
                product.Name,
                product.ArrUsd,
                maxRisk.ToString().ToLowerInvariant(),
                contributing,
                directAppNodes
            );
        })
        .OrderBy(p => p.id)
        .ToList();

        return new ProductRiskSummary(
            productList.Count(p => p.risk == "high"),
            productList.Count(p => p.risk == "medium"),
            productList.Count(p => p.risk == "low"),
            productList.Where(p => p.risk == "high").Sum(p => p.arr),
            productList.Where(p => p.risk == "medium").Sum(p => p.arr),
            productList);
    }

    internal static HashSet<int> BuildProductClosure(
        int productId,
        IEnumerable<(int ProductId, int ApplicationId)> productApps,
        DependencyGraph graph)
    {
        var directAppIds = productApps
            .Where(pa => pa.ProductId == productId)
            .Select(pa => pa.ApplicationId)
            .ToList();
        var closure = new HashSet<int>(directAppIds);
        foreach (var appId in directAppIds)
            foreach (var dep in graph.TransitiveDependenciesOf(appId))
                closure.Add(dep);
        return closure;
    }
}
