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

    public async Task<CapacitySummaryResult> GetCapacityByDivisionAsync(string month)
    {
        var resources = await context.Resources
            .Include(r => r.Mappings)
            .ToListAsync();

        var divisions = resources
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
                var allocationPct = Math.Round(count > 0 ? sumPct / count : 0m, 1);
                var typeSplit = list
                    .GroupBy(r => r.ResourceType)
                    .ToDictionary(g2 => g2.Key, g2 => g2.Count());

                return new CapacityResult(
                    g.Key, count, typeSplit, monthlyCost,
                    allocatedCost, monthlyCost - allocatedCost, allocationPct);
            })
            .ToList();

        var totalCount = divisions.Sum(d => d.ResourceCount);
        var totalSumPct = resources
            .Where(r => GetMonthFlag(r, month) == 1)
            .Sum(r => r.Mappings.Where(m => m.Month == month).Sum(m => (decimal)m.Percent));
        var overallAllocationPct = totalCount > 0 ? totalSumPct / totalCount : 0m;

        var fullyConstrained = divisions
            .Where(d => d.AllocationPct >= 100m)
            .Select(d => d.Division)
            .OrderBy(n => n)
            .ToList();

        return new CapacitySummaryResult(
            totalCount,
            divisions.Sum(d => d.MonthlyCost),
            divisions.Sum(d => d.AllocatedMonthlyCost),
            divisions.Sum(d => d.UnallocatedMonthlyCost),
            overallAllocationPct,
            divisions,
            fullyConstrained);
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

    public async Task<DonorCandidatesResult?> GetDonorCandidatesAsync(
        int? applicationId, string? applicationName, string month)
    {
        var appQuery = context.Applications.Include(a => a.Activities).AsQueryable();
        var application = applicationId.HasValue
            ? await appQuery.FirstOrDefaultAsync(a => a.Id == applicationId.Value)
            : await appQuery.FirstOrDefaultAsync(a =>
                a.Name.ToLower() == (applicationName ?? "").ToLower());
        if (application is null) return null;

        var division = application.Activities.FirstOrDefault()?.Division ?? "";

        // Current support coverage on the target app
        var targetSupportMappings = await context.Mappings
            .Where(m => m.Month == month && m.Activity.ActivityType == "Support"
                        && m.Activity.ApplicationId == application.Id)
            .Include(m => m.Resource)
            .ToListAsync();
        var currentCoverage = targetSupportMappings
            .Where(m => GetMonthFlag(m.Resource, month) == 1)
            .Sum(m => (double)m.Percent / 100.0);
        var gap = Math.Max(0.0, application.MinSupportFte - currentCoverage);

        // Resources already on the target's support are excluded from all tiers
        var excludedResourceIds = targetSupportMappings
            .Select(m => m.ResourceId)
            .ToHashSet();

        // All active mappings for the month (with resource + activity loaded)
        var allMappings = await context.Mappings
            .Where(m => m.Month == month)
            .Include(m => m.Resource)
            .Include(m => m.Activity)
            .ToListAsync();
        var activeMappings = allMappings.Where(m => GetMonthFlag(m.Resource, month) == 1).ToList();

        // Surplus support FTE per application
        var allApplications = await context.Applications.Include(a => a.Activities).ToListAsync();
        string AppDiv(Application a) => a.Activities.FirstOrDefault()?.Division ?? "";

        var appSurplus  = new Dictionary<int, double>();
        var appCoverage = new Dictionary<int, double>();
        foreach (var app in allApplications)
        {
            var cov = activeMappings
                .Where(m => m.Activity.ActivityType == "Support" && m.Activity.ApplicationId == app.Id)
                .Sum(m => m.Percent / 100.0);
            appCoverage[app.Id] = cov;
            appSurplus[app.Id]  = Math.Max(0, cov - app.RecommendedSupportFte);
        }

        var surplusSupportFteAvailable = allApplications
            .Where(a => AppDiv(a) == division)
            .Sum(a => appSurplus.GetValueOrDefault(a.Id));

        // Build tiers 1–6
        var tier1 = BuildSurplusSupportTier(activeMappings, allApplications, appSurplus, appCoverage, excludedResourceIds, division, gap, crossDiv: false);
        var tier2 = BuildNonSupportTier(activeMappings, excludedResourceIds, division, "Minor Changes", gap, crossDiv: false);
        var tier3 = BuildNonSupportTier(activeMappings, excludedResourceIds, division, "Project",       gap, crossDiv: false);
        var tier4 = BuildSurplusSupportTier(activeMappings, allApplications, appSurplus, appCoverage, excludedResourceIds, division, gap, crossDiv: true);
        var tier5 = BuildNonSupportTier(activeMappings, excludedResourceIds, division, "Minor Changes", gap, crossDiv: true);
        var tier6 = BuildNonSupportTier(activeMappings, excludedResourceIds, division, "Project",       gap, crossDiv: true);

        var sorted = tier1.Concat(tier2).Concat(tier3).Concat(tier4).Concat(tier5).Concat(tier6).ToList();

        var runningFte = 0.0;
        var withCumulative = sorted.Select(c =>
        {
            runningFte += c.AllocationFte;
            var cumCoverage = gap > 0 ? runningFte / gap * 100.0 : 0.0;
            return c with { CumulativeFte = runningFte, CumulativeGapCoveragePct = cumCoverage };
        }).ToList();

        var stoppingIdx = withCumulative.FindIndex(c => c.CumulativeGapCoveragePct >= 100.0);
        var candidates = withCumulative
            .Select((c, i) => c with { InFewestChangesCombination = stoppingIdx >= 0 && i <= stoppingIdx })
            .ToList();

        if (gap > 0 && stoppingIdx >= 0)
            candidates = ApplyLargestRemainders(candidates, stoppingIdx, gap);

        FewestChangesCombinationSummary? fewestChangesCombo = null;
        if (gap > 0 && stoppingIdx >= 0)
        {
            var comboRows = candidates.Where(c => c.InFewestChangesCombination).ToList();
            fewestChangesCombo = new FewestChangesCombinationSummary(
                comboRows.Count,
                comboRows.Select(c => c.ResourceName).Distinct().Count(),
                comboRows.Sum(c => c.AllocationFte),
                comboRows.Sum(c => c.ImpliedMonthlyCost),
                candidates[stoppingIdx].CumulativeGapCoveragePct);
        }

        return new DonorCandidatesResult(
            application.Id, application.Name, division,
            currentCoverage, application.MinSupportFte, application.RecommendedSupportFte,
            gap,
            candidates.Select(c => c.ResourceName).Distinct().Count(),
            candidates.Count,
            candidates,
            surplusSupportFteAvailable,
            fewestChangesCombo);
    }

    private static List<DonorCandidateRow> ApplyLargestRemainders(
        List<DonorCandidateRow> candidates, int stoppingIdx, double gap)
    {
        var count   = stoppingIdx + 1;
        var gapCovReached = candidates[stoppingIdx].CumulativeGapCoveragePct;
        var target  = (int)Math.Round(gapCovReached);
        target      = Math.Max(0, Math.Min(target, 100 * count));

        var unrounded = Enumerable.Range(0, count)
            .Select(i => candidates[i].AllocationFte / gap * 100.0)
            .ToArray();

        // Start each fewest-changes row at its floor, capped by the source mapping percent
        var adj = Enumerable.Range(0, count)
            .Select(i => Math.Min((int)Math.Floor(unrounded[i]), candidates[i].Percent))
            .ToArray();

        var deficit = target - adj.Sum();

        // Distribute remaining pp to rows with largest fractional part; tie-break: largest AllocationFte
        var order = Enumerable.Range(0, count)
            .OrderByDescending(i => unrounded[i] - Math.Floor(unrounded[i]))
            .ThenByDescending(i => candidates[i].AllocationFte)
            .ToList();

        var distributed = 0;
        foreach (var idx in order)
        {
            if (distributed >= deficit) break;
            if (adj[idx] < candidates[idx].Percent)
            {
                adj[idx]++;
                distributed++;
            }
        }

        return candidates
            .Select((c, i) => i < count ? c with { DonablePercent = adj[i] } : c)
            .ToList();
    }

    private static List<DonorCandidateRow> BuildSurplusSupportTier(
        List<Mapping> activeMappings,
        List<Application> allApplications,
        Dictionary<int, double> appSurplus,
        Dictionary<int, double> appCoverage,
        HashSet<int> excludedResourceIds,
        string targetDivision,
        double gap,
        bool crossDiv)
    {
        var rows = new List<DonorCandidateRow>();
        foreach (var app in allApplications)
        {
            var surplus  = appSurplus.GetValueOrDefault(app.Id);
            var coverage = appCoverage.GetValueOrDefault(app.Id);
            if (surplus <= 0 || coverage <= 0) continue;

            var supportMappings = activeMappings
                .Where(m => m.Activity.ActivityType == "Support"
                            && m.Activity.ApplicationId == app.Id
                            && !excludedResourceIds.Contains(m.ResourceId));

            foreach (var m in supportMappings)
            {
                var isSameDiv = m.Resource.Division == targetDivision;
                if (crossDiv == isSameDiv) continue; // crossDiv=false wants same-div; crossDiv=true wants cross-div

                var donatableFte = (m.Percent / 100.0) * (surplus / coverage);
                var impliedCost  = m.Resource.MonthlyRate * (decimal)donatableFte;
                var gapCoverage  = gap > 0 ? donatableFte / gap * 100.0 : 0.0;
                var donablePct   = (int)Math.Round(donatableFte * 100.0);
                rows.Add(new DonorCandidateRow(
                    m.Resource.Name, m.Activity.Name, m.Activity.ActivityType,
                    m.Percent, impliedCost, donatableFte, m.Resource.MonthlyRate, gapCoverage,
                    0.0, 0.0, crossDiv, DonablePercent: donablePct));
            }
        }
        return rows.OrderByDescending(r => r.AllocationFte).ToList();
    }

    private static List<DonorCandidateRow> BuildNonSupportTier(
        List<Mapping> activeMappings,
        HashSet<int> excludedResourceIds,
        string targetDivision,
        string activityType,
        double gap,
        bool crossDiv)
    {
        return activeMappings
            .Where(m => m.Activity.ActivityType == activityType
                        && !excludedResourceIds.Contains(m.ResourceId)
                        && (crossDiv ? m.Resource.Division != targetDivision
                                     : m.Resource.Division == targetDivision))
            .Select(m =>
            {
                var fte        = m.Percent / 100.0;
                var cost       = m.Resource.MonthlyRate * m.Percent / 100m;
                var costPerFte = m.Resource.MonthlyRate;
                var gapCov     = gap > 0 ? fte / gap * 100.0 : 0.0;
                return new DonorCandidateRow(
                    m.Resource.Name, m.Activity.Name, m.Activity.ActivityType,
                    m.Percent, cost, fte, costPerFte, gapCov,
                    0.0, 0.0, crossDiv, DonablePercent: m.Percent);
            })
            .OrderByDescending(r => r.AllocationFte)
            .ToList();
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
            divisionGroups.Sum(d => d.ResourceCount),
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
        var totalArrUsd = productCosts.Values.Sum(v => v.ArrUsd);

        return new ProductCostSummary(totalPayroll, totalMappedCost, totalArrUsd, results);
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

    public async Task<Resource?> GetResourceByPkAsync(int id) =>
        await context.Resources.FindAsync(id);

    public async Task<List<Mapping>> GetMappingsForResourceMonthAsync(int resourceId, string month) =>
        await context.Mappings
            .Where(m => m.ResourceId == resourceId && m.Month == month)
            .ToListAsync();

    public async Task UpsertMappingAsync(int resourceId, int activityId, string month, int percent)
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

    public async Task<ProductRiskSummary> GetProductRiskAsync(string month, int? productId = null)
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
        string AppDivision(int appId) => appRisks.FirstOrDefault(a => a.id == appId)?.division ?? "";

        var productList = products.Select(product =>
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

    public async Task<List<ApplicationRiskDto>> GetApplicationsAsync(
        string month, int? applicationId = null, string? applicationName = null)
    {
        var applications = await context.Applications
            .Include(a => a.Activities)
            .ToListAsync();

        var allProducts = await context.Products.ToListAsync();
        var productById = allProducts.ToDictionary(p => p.Id);

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

        // Transitive closure per product: direct apps + all their transitive dependencies
        var closureByProduct = paByProduct.ToDictionary(
            kvp => kvp.Key,
            kvp =>
            {
                var closure = new HashSet<int>(kvp.Value);
                foreach (var appId in kvp.Value)
                    foreach (var dep in graph.TransitiveDependenciesOf(appId))
                        closure.Add(dep);
                return closure;
            });

        // Reverse: appId → product ids whose closure includes this app
        var productIdsByApp = new Dictionary<int, List<int>>();
        foreach (var (productId, closure) in closureByProduct)
            foreach (var appId in closure)
            {
                if (!productIdsByApp.TryGetValue(appId, out var list))
                    productIdsByApp[appId] = list = [];
                list.Add(productId);
            }

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

        // First pass: build name + division lookup for all apps (needed for dependents projection)
        var nameById = applications.ToDictionary(a => a.Id, a => a.Name);
        var divisionById = applications.ToDictionary(
            a => a.Id, a => a.Activities.FirstOrDefault()?.Division ?? "");

        if (applicationId.HasValue)
            applications = applications.Where(a => a.Id == applicationId.Value).ToList();
        else if (applicationName is not null)
            applications = applications
                .Where(a => a.Name.Equals(applicationName, StringComparison.OrdinalIgnoreCase))
                .ToList();

        // Second pass: project DTOs with dependents populated from reverse traversal
        return applications
            .Select(app =>
            {
                var coverage = coverageByApp.TryGetValue(app.Id, out var c) ? c : 0.0;
                var risk = appRiskById[app.Id];
                var division = divisionById[app.Id];
                var products = (productIdsByApp.TryGetValue(app.Id, out var pids) ? pids : [])
                    .Where(pid => productById.ContainsKey(pid))
                    .Select(pid =>
                    {
                        var product = productById[pid];
                        var pRisk = ProductRiskFor(pid);
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
