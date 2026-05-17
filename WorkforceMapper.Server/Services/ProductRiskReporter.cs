using Microsoft.EntityFrameworkCore;
using WorkforceMapper.Server.Data;
using WorkforceMapper.Server.Models;

namespace WorkforceMapper.Server.Services;

public class ProductRiskReporter(WorkforceDbContext context)
{
    public async Task<ProductRiskSummary> ReportAsync(string month, int? productId = null)
    {
        var applications = await context.Applications
            .Include(a => a.Activities)
            .ToListAsync();
        var snapshot = await MonthlyAllocationSnapshot.LoadAsync(context, month);

        var riskByAppId = applications.ToDictionary(
            app => app.Id,
            app => RiskCalculator.ClassifyApplication(
                snapshot.SupportCoverageOf(app.Id),
                app.MinSupportFte,
                app.RecommendedSupportFte));

        var edges = await context.ApplicationDependencies
            .Select(d => new { d.ApplicationId, d.DependsOnApplicationId })
            .ToListAsync();
        var productApps = await context.ProductApplications
            .Select(pa => new { pa.ProductId, pa.ApplicationId })
            .ToListAsync();

        var products = await context.Products.ToListAsync();
        var graph = new DependencyGraph(
            edges.Select(e => (e.ApplicationId, e.DependsOnApplicationId)),
            productApps.Select(pa => (pa.ProductId, pa.ApplicationId)));
        var reach = new RiskReach(
            graph,
            productApps.Select(pa => (pa.ProductId, pa.ApplicationId)),
            products.Select(p => new RiskReachProduct(p.Id, p.Name, p.ArrUsd)),
            applications.Select(a => new RiskReachApplication(
                a.Id,
                a.Name,
                a.Activities.FirstOrDefault()?.Division ?? "")),
            edges.Select(e => (e.ApplicationId, e.DependsOnApplicationId)));

        if (productId.HasValue)
            products = products.Where(p => p.Id == productId.Value).ToList();

        var productList = products.Select(product =>
        {
            var riskApplications = reach.ProductRiskApplications(product.Id, riskByAppId);

            var contributing = riskApplications
                .Take(10)
                .Select(a => new ContributingAppDto(
                    a.Application.Id,
                    a.Application.Name,
                    a.Risk.ToString().ToLowerInvariant(),
                    a.Application.Division))
                .ToList();

            var maxRisk = RiskCalculator.AggregateProductRisk(riskApplications.Select(a => a.Risk));

            var directAppNodes = reach.DirectApplicationRisks(product.Id, riskByAppId)
                .Select(app =>
                {
                    var deps = reach.DirectDependencyRisks(app.Application.Id, riskByAppId)
                        .Select(dep => new ContributingAppDto(
                            dep.Application.Id,
                            dep.Application.Name,
                            dep.Risk.ToString().ToLowerInvariant(),
                            dep.Application.Division))
                        .ToList();
                    return new DirectAppNodeDto(
                        app.Application.Id,
                        app.Application.Name,
                        app.Risk.ToString().ToLowerInvariant(),
                        deps);
                })
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
}
