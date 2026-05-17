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
        var snapshot = await MonthlyAllocationSnapshot.LoadAsync(context, month);

        var appRiskById = applications.ToDictionary(
            app => app.Id,
            app =>
            {
                var coverage = snapshot.SupportCoverageOf(app.Id);
                return RiskCalculator.ClassifyApplication(
                    coverage,
                    app.MinSupportFte,
                    app.RecommendedSupportFte);
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
        var reach = new RiskReach(
            graph,
            productApps.Select(pa => (pa.ProductId, pa.ApplicationId)),
            allProducts.Select(p => new RiskReachProduct(p.Id, p.Name, p.ArrUsd)),
            applications.Select(a => new RiskReachApplication(
                a.Id,
                a.Name,
                a.Activities.FirstOrDefault()?.Division ?? "")),
            edges.Select(e => (e.ApplicationId, e.DependsOnApplicationId)));

        RiskLevel ProductRiskFor(int productId)
        {
            var riskApplications = reach.ProductRiskApplications(productId, appRiskById);
            return RiskCalculator.AggregateProductRisk(riskApplications.Select(a => a.Risk));
        }

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
                var risk = appRiskById[app.Id];
                var division = divisionById[app.Id];
                var products = reach.ProductsReachedByApplication(app.Id)
                    .Select(product =>
                    {
                        var productRisk = ProductRiskFor(product.Id);
                        return new ProductRefDto(
                            product.Id,
                            product.Name,
                            product.ArrUsd,
                            productRisk.ToString().ToLowerInvariant());
                    })
                    .ToList();
                var transitiveDependentApps = reach.TransitiveDependentApplications(app.Id)
                    .Select(dependentApp =>
                    {
                        var depRisk = appRiskById.TryGetValue(dependentApp.Id, out var r) ? r : RiskLevel.Low;
                        return new ContributingAppDto(
                            dependentApp.Id,
                            dependentApp.Name,
                            depRisk.ToString().ToLowerInvariant(),
                            dependentApp.Division);
                    })
                    .ToList();
                var totalArr = products.Sum(p => p.arr_usd);
                var n = products.Count;
                var riskReason = risk switch
                {
                    RiskLevel.High => $"Coverage {coverage:F1} FTE is {app.MinSupportFte - coverage:F1} FTE below the minimum {app.MinSupportFte:F1} FTE (recommended {app.RecommendedSupportFte:F1}); risk classification: high.",
                    RiskLevel.Medium => $"Coverage {coverage:F1} FTE meets the minimum {app.MinSupportFte:F1} FTE but is {app.RecommendedSupportFte - coverage:F1} FTE below the recommended {app.RecommendedSupportFte:F1} FTE; risk classification: medium.",
                    _ => $"Coverage {coverage:F1} FTE is at or above the recommended {app.RecommendedSupportFte:F1} FTE; risk classification: low."
                };
                var productWord = n == 1 ? "product" : "products";
                var productList = n > 0 ? $": {string.Join(", ", products.Select(p => p.name))}." : ".";
                var depSummary = $"Exposes ${(double)totalArr / 1_000_000:F1}M ARR across {n} {productWord}{productList}";

                return new ApplicationRiskDto(
                    app.Id, app.Name, division,
                    app.MinSupportFte, app.RecommendedSupportFte,
                    coverage, risk.ToString().ToLowerInvariant(),
                    products, transitiveDependentApps,
                    totalArr,
                    transitiveDependentApps.Count,
                    riskReason,
                    depSummary
                );
            })
            .OrderBy(a => a.id)
            .ToList();
    }
}
