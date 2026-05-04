using Microsoft.EntityFrameworkCore;
using WorkforceMapper.Server.Data;
using WorkforceMapper.Server.Models;

namespace WorkforceMapper.Server.Services;

public class DataService(WorkforceDbContext context)
{
    public async Task<(int ResourceCount, int MappingCount)> GetHealthAsync()
    {
        var resourceCount = await context.Resources.CountAsync();
        var mappingCount = await context.Mappings.CountAsync();
        return (resourceCount, mappingCount);
    }

    public async Task<List<Resource>> GetResourcesAsync(string? division = null, string? month = null)
    {
        var query = context.Resources
            .Include(r => r.Mappings)
            .ThenInclude(m => m.Activity)
            .AsQueryable();

        if (division != null)
            query = query.Where(r => r.Division == division);

        var resources = await query.ToListAsync();

        if (month != null)
            resources = resources.Where(r => GetMonthFlag(r, month) == 1).ToList();

        return resources;
    }

    public async Task<List<Activity>> GetActivitiesAsync(string? division = null, string? activityType = null)
    {
        var query = context.Activities
            .Include(a => a.Application)
            .Include(a => a.Project)
            .AsQueryable();

        if (division != null)
            query = query.Where(a => a.Division == division);
        if (activityType != null)
            query = query.Where(a => a.ActivityType == activityType);

        return await query.ToListAsync();
    }

    public async Task<List<CapacityResult>> GetCapacityByDivisionAsync(string month)
    {
        var resources = await context.Resources
            .Include(r => r.Mappings)
            .ToListAsync();

        return resources
            .Where(r => GetMonthFlag(r, month) == 1)
            .GroupBy(r => r.Division)
            .Select(g =>
            {
                var list = g.ToList();
                var count = list.Count;
                var monthlyCost = list.Sum(r => r.MonthlyRate);
                var allocatedCost = list.Sum(r =>
                    r.Mappings.Where(m => m.Month == month).Sum(m => r.MonthlyRate * m.Percent / 100m));
                var sumPct = list.Sum(r =>
                    r.Mappings.Where(m => m.Month == month).Sum(m => m.Percent));
                var allocationPct = count > 0 ? sumPct / count : 0m;
                var typeSplit = list
                    .GroupBy(r => r.ResourceType)
                    .ToDictionary(g2 => g2.Key, g2 => g2.Count());

                return new CapacityResult(
                    g.Key, count, typeSplit, monthlyCost,
                    allocatedCost, monthlyCost - allocatedCost, allocationPct);
            })
            .ToList();
    }

    public async Task<List<AllocationResult>> GetActivityAllocationsAsync(
        int? activityId, string? division, string? activityType, string month)
    {
        var resources = await context.Resources
            .Include(r => r.Mappings)
            .ThenInclude(m => m.Activity)
            .ToListAsync();

        var active = resources
            .Where(r => GetMonthFlag(r, month) == 1)
            .Where(r => division == null || r.Division == division);

        var pairs = active.SelectMany(
            r => r.Mappings.Where(m => m.Month == month),
            (r, m) => (Resource: r, Mapping: m));

        if (activityId != null)
            pairs = pairs.Where(x => x.Mapping.ActivityId == activityId.Value);
        if (activityType != null)
            pairs = pairs.Where(x => x.Mapping.Activity.ActivityType == activityType);

        return pairs.Select(x => new AllocationResult(
            x.Mapping.ActivityId,
            x.Mapping.Activity.Name,
            x.Mapping.Activity.ActivityType,
            x.Resource.Id,
            x.Resource.Name,
            x.Resource.Division,
            x.Mapping.Percent,
            x.Resource.MonthlyRate * x.Mapping.Percent / 100m
        )).ToList();
    }

    public async Task<List<UnallocatedResult>> GetUnallocatedResourcesAsync(
        int thresholdPct, string? division, string? month)
    {
        var m = month ?? Constants.DefaultMonth;
        var resources = await context.Resources
            .Include(r => r.Mappings)
            .ToListAsync();

        return resources
            .Where(r => GetMonthFlag(r, m) == 1)
            .Where(r => division == null || r.Division == division)
            .Select(r =>
            {
                var totalPct = r.Mappings.Where(mapping => mapping.Month == m).Sum(mapping => mapping.Percent);
                return new UnallocatedResult(
                    r.ResourceId, r.Name, r.Division, r.ResourceType, r.MonthlyRate,
                    totalPct, r.MonthlyRate * (100m - totalPct) / 100m);
            })
            .Where(r => r.AllocationPct < thresholdPct)
            .ToList();
    }

    public async Task<List<ProductCostResult>> GetCostByProductAsync(string month)
    {
        var mappings = await context.Mappings
            .Where(m => m.Month == month)
            .Include(m => m.Resource)
            .Include(m => m.Activity)
                .ThenInclude(a => a.Application)
                    .ThenInclude(app => app!.ProductApplications)
                        .ThenInclude(pa => pa.Product)
            .Include(m => m.Activity)
                .ThenInclude(a => a.Project)
                    .ThenInclude(proj => proj!.ProjectProducts)
                        .ThenInclude(pp => pp.Product)
            .ToListAsync();

        var productCosts = new Dictionary<int, (string Name, decimal ArrUsd, decimal Cost)>();
        var unattributedCost = 0m;

        foreach (var mapping in mappings.Where(m => GetMonthFlag(m.Resource, month) == 1))
        {
            var cost = mapping.Resource.MonthlyRate * mapping.Percent / 100m;
            var activity = mapping.Activity;

            if (activity.Application is { } app)
            {
                var products = app.ProductApplications.Select(pa => pa.Product).ToList();
                if (products.Count == 0)
                    unattributedCost += cost;
                else
                    foreach (var product in products)
                        AddCost(productCosts, product.Id, product.Name, product.ArrUsd, cost);
            }
            else if (activity.Project is { } project)
            {
                var products = project.ProjectProducts.Select(pp => pp.Product).ToList();
                if (products.Count == 0)
                    unattributedCost += cost;
                else
                    foreach (var product in products)
                        AddCost(productCosts, product.Id, product.Name, product.ArrUsd, cost);
            }
            else
            {
                unattributedCost += cost;
            }
        }

        var results = productCosts
            .Select(kvp => new ProductCostResult(kvp.Key, kvp.Value.Name, kvp.Value.ArrUsd, kvp.Value.Cost))
            .OrderByDescending(r => r.TotalAllocatedCost)
            .ToList();

        if (unattributedCost > 0)
            results.Add(new ProductCostResult(null, "Unattributed", 0m, unattributedCost));

        return results;
    }

    public async Task UpsertMappingAsync(int resourceId, int activityId, string month, decimal percent)
    {
        var existing = await context.Mappings.FindAsync(resourceId, activityId, month);
        if (existing is not null)
            existing.Percent = percent;
        else
            context.Mappings.Add(new Mapping { ResourceId = resourceId, ActivityId = activityId, Month = month, Percent = percent });
        await context.SaveChangesAsync();
    }

    public async Task<bool> DeleteMappingAsync(int resourceId, int activityId, string month)
    {
        var mapping = await context.Mappings.FindAsync(resourceId, activityId, month);
        if (mapping is null) return false;
        context.Mappings.Remove(mapping);
        await context.SaveChangesAsync();
        return true;
    }

    private static void AddCost(
        Dictionary<int, (string, decimal, decimal)> dict,
        int id, string name, decimal arr, decimal cost)
    {
        if (dict.TryGetValue(id, out var existing))
            dict[id] = (existing.Item1, existing.Item2, existing.Item3 + cost);
        else
            dict[id] = (name, arr, cost);
    }

    private static int GetMonthFlag(Resource r, string month) =>
        month[5..] switch
        {
            "01" => r.Jan,
            "02" => r.Feb,
            "03" => r.Mar,
            "04" => r.Apr,
            "05" => r.May,
            "06" => r.Jun,
            "07" => r.Jul,
            "08" => r.Aug,
            "09" => r.Sep,
            "10" => r.Oct,
            "11" => r.Nov,
            "12" => r.Dec,
            _   => throw new ArgumentException($"Unsupported month: {month}")
        };
}
