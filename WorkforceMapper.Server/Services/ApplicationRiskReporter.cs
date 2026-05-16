using Microsoft.EntityFrameworkCore;
using WorkforceMapper.Server.Data;
using WorkforceMapper.Server.Models;

namespace WorkforceMapper.Server.Services;

public class ApplicationRiskReporter(WorkforceDbContext context)
{
    public async Task<List<ApplicationRiskDto>> ReportAsync(
        string month, int? applicationId = null, string? applicationName = null)
    {
        var applications = await context.Applications
            .Include(a => a.Activities)
            .ToListAsync();

        var allProducts = await context.Products.ToListAsync();
        var productById = allProducts.ToDictionary(p => p.Id);

        var snapshot = await MonthlyAllocationSnapshot.LoadAsync(context, month);

        var appRiskById = applications.ToDictionary(
            app => app.Id,
            app =>
            {
                var cov = snapshot.SupportCoverageOf(app.Id);
                return RiskCalculator.ClassifyApplication(cov, app.MinSupportFte, app.RecommendedSupportFte);
            });

        var edges = await context.ApplicationDependencies
            .Select(d => new { d.ApplicationId, d.DependsOnApplicationId })
            .ToListAsync();
        var productApps = await context.ProductApplications
            .Select(pa => new { pa.ProductId, pa.ApplicationId })
            .ToListAsync();

        var graph = new DependencyGraph(
            edges.Select(e => (e.ApplicationId, e.DependsOnApplicationId)),
            productApps.Select(pa => (pa.ProductId, pa.ApplicationId)));

        var paByProduct = productApps
            .GroupBy(pa => pa.ProductId)
            .ToDictionary(g => g.Key, g => g.Select(pa => pa.ApplicationId).ToHashSet());

        var paTuples = productApps.Select(pa => (pa.ProductId, pa.ApplicationId));

        // Transitive closure per product using the shared BuildProductClosure helper
        var closureByProduct = paByProduct.ToDictionary(
            kvp => kvp.Key,
            kvp => ProductRiskReporter.BuildProductClosure(kvp.Key, paTuples, graph));

        // Reverse: appId → product ids whose closure includes this app
        var productIdsByApp = new Dictionary<int, List<int>>();
        foreach (var (pid, closure) in closureByProduct)
            foreach (var appId in closure)
            {
                if (!productIdsByApp.TryGetValue(appId, out var list))
                    productIdsByApp[appId] = list = [];
                list.Add(pid);
            }

        RiskLevel ProductRiskFor(int pid)
        {
            var closure = ProductRiskReporter.BuildProductClosure(pid, paTuples, graph);
            return closure
                .Select(id => appRiskById.TryGetValue(id, out var r) ? r : RiskLevel.Low)
                .Aggregate(RiskLevel.Low, (acc, r) => r > acc ? r : acc);
        }

        var nameById     = applications.ToDictionary(a => a.Id, a => a.Name);
        var divisionById = applications.ToDictionary(
            a => a.Id, a => a.Activities.FirstOrDefault()?.Division ?? "");

        if (applicationId.HasValue)
            applications = applications.Where(a => a.Id == applicationId.Value).ToList();
        else if (applicationName is not null)
            applications = applications
                .Where(a => a.Name.Equals(applicationName, StringComparison.OrdinalIgnoreCase))
                .ToList();

        return applications
            .Select(app =>
            {
                var coverage = snapshot.SupportCoverageOf(app.Id);
                var risk     = appRiskById[app.Id];
                var division = divisionById[app.Id];
                var products = (productIdsByApp.TryGetValue(app.Id, out var pids) ? pids : [])
                    .Where(pid => productById.ContainsKey(pid))
                    .Select(pid =>
                    {
                        var product = productById[pid];
                        var pRisk   = ProductRiskFor(pid);
                        return new ProductRefDto(pid, product.Name, product.ArrUsd,
                            pRisk.ToString().ToLowerInvariant());
                    })
                    .OrderBy(p => p.name)
                    .ToList();
                var transitiveDependentApps = graph.TransitiveDependentsOf(app.Id)
                    .Where(id => nameById.ContainsKey(id))
                    .Select(id =>
                    {
                        var depRisk = appRiskById.TryGetValue(id, out var r) ? r : RiskLevel.Low;
                        return new ContributingAppDto(id, nameById[id], depRisk.ToString().ToLowerInvariant(), divisionById[id]);
                    })
                    .OrderBy(d => d.name)
                    .ToList();
                return new ApplicationRiskDto(
                    app.Id, app.Name, division,
                    app.MinSupportFte, app.RecommendedSupportFte,
                    coverage, risk.ToString().ToLowerInvariant(),
                    products, transitiveDependentApps,
                    products.Sum(p => p.arr_usd),
                    transitiveDependentApps.Count
                );
            })
            .OrderBy(a => a.id)
            .ToList();
    }
}
