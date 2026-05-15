using Microsoft.EntityFrameworkCore;
using WorkforceMapper.Server.Data;
using WorkforceMapper.Server.Models;

namespace WorkforceMapper.Server.Services;

public sealed record ActiveResource(
    Resource Resource,
    int      AllocationPercent,
    decimal  AllocatedCost,
    decimal  IdleCost,
    double   IdleFte);

public sealed record ActiveMapping(
    Resource Resource,
    Activity Activity,
    int      Percent,
    decimal  AllocatedCost);

public sealed class MonthlyAllocationSnapshot
{
    private readonly Dictionary<int, ActiveResource>       _byResourceId;
    private readonly Dictionary<int, List<ActiveMapping>>  _mappingsByResourceId;
    private readonly Dictionary<int, List<ActiveMapping>>  _mappingsByActivityId;

    public string Month { get; }
    public IReadOnlyList<ActiveResource> ActiveResources { get; }
    public IReadOnlyList<ActiveMapping>  ActiveMappings  { get; }

    public ActiveResource? Resource(int resourceId) =>
        _byResourceId.GetValueOrDefault(resourceId);

    public IReadOnlyList<ActiveMapping> MappingsOf(int resourceId) =>
        _mappingsByResourceId.TryGetValue(resourceId, out var list) ? list : [];

    public IReadOnlyList<ActiveMapping> MappingsOn(int activityId) =>
        _mappingsByActivityId.TryGetValue(activityId, out var list) ? list : [];

    public double SupportCoverageOf(int applicationId) =>
        _mappingsByActivityId.Values
            .SelectMany(x => x)
            .Where(am => am.Activity.ActivityType == "Support"
                      && am.Activity.ApplicationId == applicationId)
            .Sum(am => am.Percent / 100.0);

    public MonthlyAllocationSnapshot(
        string month,
        IReadOnlyList<Resource> activeResources,
        IReadOnlyList<Mapping>  activeMappings)
    {
        Month = month;

        var filteredResources = activeResources
            .Where(r => r.IsActiveIn(month))
            .ToList();

        var activeIds = filteredResources.Select(r => r.Id).ToHashSet();

        var filteredMappings = activeMappings
            .Where(m => activeIds.Contains(m.ResourceId))
            .ToList();

        var mappingsByResource = filteredMappings
            .GroupBy(m => m.ResourceId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var resourceLookup = filteredResources.ToDictionary(r => r.Id);

        var builtMappings = new List<ActiveMapping>();
        var builtResources = new List<ActiveResource>();

        foreach (var r in filteredResources)
        {
            var rMappings = mappingsByResource.TryGetValue(r.Id, out var ms) ? ms : [];
            var ams = rMappings
                .Select(m => new ActiveMapping(m.Resource, m.Activity, m.Percent,
                    m.Resource.MonthlyRate * m.Percent / 100m))
                .ToList();

            var allocationPct = ams.Sum(am => am.Percent);
            var allocatedCost = ams.Sum(am => am.AllocatedCost);
            var idleCost      = r.MonthlyRate * (100 - allocationPct) / 100m;
            var idleFte       = (100 - allocationPct) / 100.0;

            builtResources.Add(new ActiveResource(r, allocationPct, allocatedCost, idleCost, idleFte));
            builtMappings.AddRange(ams);
        }

        ActiveResources = builtResources;
        ActiveMappings  = builtMappings;

        _byResourceId         = builtResources.ToDictionary(ar => ar.Resource.Id);
        _mappingsByResourceId = builtMappings.GroupBy(am => am.Resource.Id)
            .ToDictionary(g => g.Key, g => g.ToList());
        _mappingsByActivityId = builtMappings.GroupBy(am => am.Activity.Id)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    public static async Task<MonthlyAllocationSnapshot> LoadAsync(
        WorkforceDbContext context, string month)
    {
        var resources = await context.Resources.ToListAsync();
        var activeResources = resources.Where(r => r.IsActiveIn(month)).ToList();
        var activeIds = activeResources.Select(r => r.Id).ToHashSet();

        var mappings = await context.Mappings
            .Where(m => m.Month == month)
            .Include(m => m.Activity)
            .Include(m => m.Resource)
            .ToListAsync();

        var activeMappings = mappings.Where(m => activeIds.Contains(m.ResourceId)).ToList();

        return new MonthlyAllocationSnapshot(month, activeResources, activeMappings);
    }
}
