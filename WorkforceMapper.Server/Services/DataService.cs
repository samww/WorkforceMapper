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

    public async Task<ActivityAllocationsResult> GetActivityAllocationsAsync(
        int? activityId, string? division, string? activityType, string? activityName, string month)
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
        if (activityName != null)
            pairs = pairs.Where(x => x.Mapping.Activity.Name.Contains(activityName, StringComparison.OrdinalIgnoreCase));

        var rows = pairs.Select(x => new AllocationResult(
            x.Mapping.ActivityId,
            x.Mapping.Activity.Name,
            x.Mapping.Activity.ActivityType,
            x.Resource.Id,
            x.Resource.Name,
            x.Resource.Division,
            x.Mapping.Percent,
            x.Resource.MonthlyRate * x.Mapping.Percent / 100m
        )).ToList();

        return new ActivityAllocationsResult(
            rows.Select(r => r.ResourceDbId).Distinct().Count(),
            rows.Sum(r => r.ImpliedMonthlyCost),
            rows
        );
    }

    public async Task<UnallocatedResourcesResult> GetUnallocatedResourcesAsync(
        int thresholdPct, string? division, string? month)
    {
        var m = month ?? Constants.DefaultMonth;
        var resources = await context.Resources
            .Include(r => r.Mappings)
            .ToListAsync();

        var rows = resources
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

        var divisionGroups = rows
            .GroupBy(r => r.Division)
            .Select(g => new UnallocatedDivisionGroup(
                g.Key,
                g.Count(),
                g.Sum(r => r.IdleMonthlyCost),
                g.ToList()))
            .ToList();

        return new UnallocatedResourcesResult(
            divisionGroups.Sum(d => d.TotalIdleMonthlyCost),
            divisionGroups);
    }

    public async Task<ProductCostSummary> GetCostByProductAsync(string month)
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
        var activeMappings = mappings.Where(m => GetMonthFlag(m.Resource, month) == 1).ToList();

        foreach (var mapping in activeMappings)
        {
            var cost = mapping.Resource.MonthlyRate * mapping.Percent / 100m;
            var activity = mapping.Activity;

            if (activity.Application is { } app)
            {
                var products = app.ProductApplications.Select(pa => pa.Product).ToList();
                if (products.Count == 0)
                    unattributedCost += cost;
                else
                {
                    var share = cost / products.Count;
                    foreach (var product in products)
                        AddCost(productCosts, product.Id, product.Name, product.ArrUsd, share);
                }
            }
            else if (activity.Project is { } project)
            {
                var products = project.ProjectProducts.Select(pp => pp.Product).ToList();
                if (products.Count == 0)
                    unattributedCost += cost;
                else
                {
                    var share = cost / products.Count;
                    foreach (var product in products)
                        AddCost(productCosts, product.Id, product.Name, product.ArrUsd, share);
                }
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

        var totalPayroll = (await context.Resources.ToListAsync())
            .Where(r => GetMonthFlag(r, month) == 1)
            .Sum(r => r.MonthlyRate);
        var totalMappedCost = activeMappings.Sum(m => m.Resource.MonthlyRate * m.Percent / 100m);

        return new ProductCostSummary(totalPayroll, totalMappedCost, results);
    }

    public async Task<List<OverallocatedResult>> GetOverallocatedResourcesAsync(
        string? division, int thresholdPct, string? month)
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
                return new OverallocatedResult(
                    r.ResourceId, r.Name, r.Company, r.ResourceType, r.Division,
                    totalPct, totalPct - thresholdPct);
            })
            .Where(r => r.AllocationPct > thresholdPct)
            .ToList();
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

    public async Task<List<ProductRiskDto>> GetProductRiskAsync(string month, int? productId = null)
    {
        var appRisks = await GetApplicationsAsync(month);
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

        // One-hop neighbours per app for the depth-2 tree
        var directDepsOfApp = edges
            .GroupBy(e => e.ApplicationId)
            .ToDictionary(g => g.Key, g => g.Select(e => e.DependsOnApplicationId).ToList());

        string RiskStr(int appId) =>
            (riskByAppId.TryGetValue(appId, out var r) ? r : RiskLevel.Low).ToString().ToLowerInvariant();

        string AppName(int appId) => appRisks.FirstOrDefault(a => a.id == appId)?.name ?? "";

        return products.Select(product =>
        {
            var directAppIds = paByProduct.TryGetValue(product.Id, out var d) ? d : [];
            var closure = new HashSet<int>(directAppIds);
            foreach (var appId in directAppIds)
                foreach (var dep in graph.TransitiveDependenciesOf(appId))
                    closure.Add(dep);

            var contributing = closure
                .Select(id => new { id, risk = riskByAppId.TryGetValue(id, out var r) ? r : RiskLevel.Low, name = AppName(id) })
                .Where(a => a.name != "")
                .OrderByDescending(a => a.risk)
                .ThenBy(a => a.name)
                .Take(10)
                .Select(a => new ContributingAppDto(a.id, a.name, a.risk.ToString().ToLowerInvariant()))
                .ToList();

            var maxRisk = closure
                .Select(id => riskByAppId.TryGetValue(id, out var r) ? r : RiskLevel.Low)
                .Aggregate(RiskLevel.Low, (acc, r) => r > acc ? r : acc);

            var directAppNodes = directAppIds
                .Select(appId =>
                {
                    var deps = (directDepsOfApp.TryGetValue(appId, out var depList) ? depList : [])
                        .Where(depId => AppName(depId) != "")
                        .Select(depId => new ContributingAppDto(depId, AppName(depId), RiskStr(depId)))
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
    }

    public async Task<List<ApplicationRiskDto>> GetApplicationsAsync(string month)
    {
        var applications = await context.Applications
            .Include(a => a.Activities)
            .Include(a => a.ProductApplications)
                .ThenInclude(pa => pa.Product)
            .ToListAsync();

        var supportMappings = await context.Mappings
            .Where(m => m.Month == month && m.Activity.ActivityType == "Support")
            .Include(m => m.Resource)
            .Include(m => m.Activity)
            .ToListAsync();

        var coverageByApp = supportMappings
            .Where(m => GetMonthFlag(m.Resource, month) == 1)
            .GroupBy(m => m.Activity.ApplicationId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(m => (double)m.Percent / 100.0));

        var appRiskById = applications.ToDictionary(
            app => app.Id,
            app =>
            {
                var cov = coverageByApp.TryGetValue(app.Id, out var c) ? c : 0.0;
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

        RiskLevel ProductRiskFor(int productId)
        {
            var directApps = paByProduct.TryGetValue(productId, out var d) ? d : [];
            var closure = new HashSet<int>(directApps);
            foreach (var appId in directApps)
                foreach (var dep in graph.TransitiveDependenciesOf(appId))
                    closure.Add(dep);
            return closure
                .Select(id => appRiskById.TryGetValue(id, out var r) ? r : RiskLevel.Low)
                .Aggregate(RiskLevel.Low, (acc, r) => r > acc ? r : acc);
        }

        return applications
            .Select(app =>
            {
                var coverage = coverageByApp.TryGetValue(app.Id, out var c) ? c : 0.0;
                var risk = appRiskById[app.Id];
                var division = app.Activities.FirstOrDefault()?.Division ?? "";
                var products = app.ProductApplications
                    .Select(pa =>
                    {
                        var pRisk = ProductRiskFor(pa.Product.Id);
                        return new ProductRefDto(pa.Product.Id, pa.Product.Name, pa.Product.ArrUsd,
                            pRisk.ToString().ToLowerInvariant());
                    })
                    .OrderBy(p => p.name)
                    .ToList();
                return new ApplicationRiskDto(
                    app.Id, app.Name, division,
                    app.MinSupportFte, app.RecommendedSupportFte,
                    coverage, risk.ToString().ToLowerInvariant(),
                    products
                );
            })
            .OrderBy(a => a.id)
            .ToList();
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
