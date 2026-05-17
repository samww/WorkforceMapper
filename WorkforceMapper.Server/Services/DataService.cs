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
            resources = resources.Where(r => r.IsActiveIn(month)).ToList();

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
        var snapshot = await MonthlyAllocationSnapshot.LoadAsync(context, month);

        var divisions = snapshot.ActiveResources
            .GroupBy(ar => ar.Resource.Division)
            .Select(g =>
            {
                var list  = g.ToList();
                var count = list.Count;
                var monthlyCost   = list.Sum(ar => ar.Resource.MonthlyRate);
                var allocatedCost = list.Sum(ar => ar.AllocatedCost);
                var sumPct        = (decimal)list.Sum(ar => ar.AllocationPercent);
                var allocationPct = Math.Round(count > 0 ? sumPct / count : 0m, 1);
                var typeSplit     = list
                    .GroupBy(ar => ar.Resource.ResourceType)
                    .ToDictionary(g2 => g2.Key, g2 => g2.Count());
                var idleFte = Math.Round((decimal)list.Sum(ar => ar.IdleFte), 2);
                return new CapacityResult(
                    g.Key, count, typeSplit, monthlyCost,
                    allocatedCost, monthlyCost - allocatedCost, allocationPct, idleFte);
            })
            .ToList();

        var totalCount        = divisions.Sum(d => d.ResourceCount);
        var totalSumPct       = (decimal)snapshot.ActiveResources.Sum(ar => ar.AllocationPercent);
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
        var snapshot = await MonthlyAllocationSnapshot.LoadAsync(context, month);

        var mappings = snapshot.ActiveMappings
            .Where(am => division == null || am.Resource.Division == division);

        if (activityId != null)
            mappings = mappings.Where(am => am.Activity.Id == activityId.Value);
        if (activityType != null)
            mappings = mappings.Where(am => am.Activity.ActivityType == activityType);
        if (activityName != null)
            mappings = mappings.Where(am => am.Activity.Name.Contains(activityName, StringComparison.OrdinalIgnoreCase));

        var rows = mappings.Select(am => new AllocationResult(
            am.Activity.Id,
            am.Activity.Name,
            am.Activity.ActivityType,
            am.Resource.Id,
            am.Resource.Name,
            am.Resource.Division,
            am.Percent,
            am.AllocatedCost
        )).ToList();

        return new ActivityAllocationsResult(
            rows.Select(r => r.ResourceDbId).Distinct().Count(),
            rows.Sum(r => r.ImpliedMonthlyCost),
            rows
        );
    }

    public async Task<DonorCandidatesResult?> GetDonorCandidatesAsync(
        int? applicationId, string? applicationName, string month) =>
        await new DonorCandidateAlgorithm(context).FindAsync(applicationId, applicationName, month);

    public async Task<UnallocatedResourcesResult> GetUnallocatedResourcesAsync(
        int thresholdPct, string? division, string? month)
    {
        var m = month ?? Constants.DefaultMonth;
        var snapshot = await MonthlyAllocationSnapshot.LoadAsync(context, m);

        var rows = snapshot.ActiveResources
            .Where(ar => division == null || ar.Resource.Division == division)
            .Where(ar => ar.AllocationPercent < thresholdPct)
            .Select(ar => new UnallocatedResult(
                ar.Resource.ResourceId, ar.Resource.Name, ar.Resource.Division,
                ar.Resource.ResourceType, ar.Resource.MonthlyRate,
                ar.AllocationPercent, ar.IdleCost))
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

    public async Task<ProductCostSummary> GetCostByProductAsync(string month) =>
        await new ProductCostAttribution(context).ComputeAsync(month);

    public async Task<List<OverallocatedResult>> GetOverallocatedResourcesAsync(
        string? division, int thresholdPct, string? month)
    {
        var m = month ?? Constants.DefaultMonth;
        var snapshot = await MonthlyAllocationSnapshot.LoadAsync(context, m);

        return snapshot.ActiveResources
            .Where(ar => division == null || ar.Resource.Division == division)
            .Where(ar => ar.AllocationPercent > thresholdPct)
            .Select(ar => new OverallocatedResult(
                ar.Resource.ResourceId, ar.Resource.Name, ar.Resource.Company,
                ar.Resource.ResourceType, ar.Resource.Division,
                ar.AllocationPercent, ar.AllocationPercent - thresholdPct))
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

    public async Task<List<ProjectAllocationDto>> GetProjectsAsync(
        string month, int? projectId = null, string? projectName = null)
    {
        var snapshot = await MonthlyAllocationSnapshot.LoadAsync(context, month);

        var query = context.Projects
            .Include(p => p.Activities)
            .AsQueryable();

        if (projectId.HasValue)
            query = query.Where(p => p.Id == projectId.Value);
        if (projectName != null)
            query = query.Where(p => p.Name.Contains(projectName));

        var projects = await query.OrderBy(p => p.Name).ToListAsync();

        return projects.Select(p => new ProjectAllocationDto(
            p.Id,
            p.Name,
            p.Activities.Select(a => a.Division).Distinct().OrderBy(d => d).ToList(),
            snapshot.ProjectAllocationOf(p.Id)
        )).ToList();
    }

    public async Task<ProductRiskSummary> GetProductRiskAsync(string month, int? productId = null) =>
        await new ProductRiskReporter(context).ReportAsync(month, productId);

    public async Task<List<ApplicationRiskDto>> GetApplicationsAsync(
        string month, int? applicationId = null, string? applicationName = null) =>
        await new ApplicationRiskReporter(context).ReportAsync(month, applicationId, applicationName);

    public async Task<AtRiskApplicationFixDto?> GetAtRiskApplicationFixAsync(
        int? applicationId, string? applicationName, string month)
    {
        var apps = await GetApplicationsAsync(month, applicationId, applicationName);
        var app  = apps.FirstOrDefault();
        if (app is null) return null;
        var donor = await GetDonorCandidatesAsync(applicationId ?? app.id, applicationName, month);
        return AtRiskApplicationFixDto.Assemble(app, donor);
    }
}
