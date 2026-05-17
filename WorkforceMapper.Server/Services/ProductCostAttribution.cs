using Microsoft.EntityFrameworkCore;
using WorkforceMapper.Server.Data;
using WorkforceMapper.Server.Models;

namespace WorkforceMapper.Server.Services;

public class ProductCostAttribution(WorkforceDbContext context)
{
    public async Task<ProductCostSummary> ComputeAsync(string month)
    {
        var snapshot = await MonthlyAllocationSnapshot.LoadAsync(context, month);

        var appProducts = await context.Applications
            .Include(a => a.ProductApplications).ThenInclude(pa => pa.Product)
            .ToDictionaryAsync(a => a.Id);

        var projectProducts = await context.Projects
            .Include(p => p.ProjectProducts).ThenInclude(pp => pp.Product)
            .ToDictionaryAsync(p => p.Id);

        var productCosts = new Dictionary<int, (string Name, decimal ArrUsd, decimal Cost)>();
        var unattributedCost = 0m;

        foreach (var am in snapshot.ActiveMappings)
        {
            if (am.Activity.ApplicationId is { } appId && appProducts.TryGetValue(appId, out var app))
            {
                var products = app.ProductApplications.Select(pa => pa.Product).ToList();
                if (products.Count == 0)
                    unattributedCost += am.AllocatedCost;
                else
                {
                    var share = am.AllocatedCost / products.Count;
                    foreach (var product in products)
                        AddCost(productCosts, product.Id, product.Name, product.ArrUsd, share);
                }
            }
            else if (am.Activity.ProjectId is { } projectId && projectProducts.TryGetValue(projectId, out var project))
            {
                var products = project.ProjectProducts.Select(pp => pp.Product).ToList();
                if (products.Count == 0)
                    unattributedCost += am.AllocatedCost;
                else
                {
                    var share = am.AllocatedCost / products.Count;
                    foreach (var product in products)
                        AddCost(productCosts, product.Id, product.Name, product.ArrUsd, share);
                }
            }
            else
            {
                unattributedCost += am.AllocatedCost;
            }
        }

        var results = productCosts
            .Select(kvp => new ProductCostResult(kvp.Key, kvp.Value.Name, kvp.Value.ArrUsd, Round1k(kvp.Value.Cost)))
            .OrderByDescending(r => r.TotalAllocatedCost)
            .ToList();

        if (unattributedCost > 0)
            results.Add(new ProductCostResult(null, "Unattributed", 0m, Round1k(unattributedCost)));

        var exactPayroll    = snapshot.ActiveResources.Sum(ar => ar.Resource.MonthlyRate);
        var exactMappedCost = snapshot.ActiveMappings.Sum(am => am.AllocatedCost);
        var totalPayroll    = Round1k(exactPayroll);
        var totalMappedCost = Round1k(exactMappedCost);
        var unmappedPayroll = Round1k(exactPayroll - exactMappedCost);
        var totalArrUsd     = productCosts.Values.Sum(v => v.ArrUsd);

        return new ProductCostSummary(totalPayroll, totalMappedCost, unmappedPayroll, totalArrUsd, results);
    }

    private static decimal Round1k(decimal v) =>
        Math.Round(v / 1000m, MidpointRounding.AwayFromZero) * 1000m;

    private static void AddCost(
        Dictionary<int, (string, decimal, decimal)> dict,
        int id, string name, decimal arr, decimal cost)
    {
        if (dict.TryGetValue(id, out var existing))
            dict[id] = (existing.Item1, existing.Item2, existing.Item3 + cost);
        else
            dict[id] = (name, arr, cost);
    }
}
