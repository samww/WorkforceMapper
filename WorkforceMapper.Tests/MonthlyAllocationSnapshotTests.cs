using WorkforceMapper.Server.Models;
using WorkforceMapper.Server.Services;
using Xunit;

namespace WorkforceMapper.Tests;

public class MonthlyAllocationSnapshotTests
{
    private static Resource MakeResource(int id, decimal monthlyRate, bool activeInMay = true) =>
        new()
        {
            Id = id, ResourceId = $"R{id:D5}", Name = $"Resource {id}",
            ResourceType = "Employee", Company = "Test Co", Seniority = "Analyst",
            RoleType = "Developer", Division = "Platform Engineering", Country = "UK",
            MonthlyRate = monthlyRate,
            Jan=0,Feb=0,Mar=0,Apr=0,May=activeInMay?1:0,Jun=0,Jul=0,Aug=0,Sep=0,Oct=0,Nov=0,Dec=0
        };

    private static Activity MakeSupportActivity(int id, int applicationId) =>
        new() { Id = id, Name = $"App {applicationId} - Support", ActivityType = "Support", ApplicationId = applicationId };

    private static Activity MakeActivity(int id, string activityType, int applicationId) =>
        new() { Id = id, Name = $"App {applicationId} - {activityType}", ActivityType = activityType, ApplicationId = applicationId };

    private static Mapping MakeMapping(Resource resource, Activity activity, string month, int percent) =>
        new() { ResourceId = resource.Id, ActivityId = activity.Id, Month = month, Percent = percent, Resource = resource, Activity = activity };

    // ── Tracer bullet ────────────────────────────────────────────────────────

    [Fact]
    public void AllocatedCost_EqualsRateTimesPercentSummedAcrossMappings()
    {
        var r = MakeResource(1, 10_000m);
        var a1 = MakeSupportActivity(1, 10);
        var a2 = MakeActivity(2, "Minor Changes", 10);
        var mappings = new List<Mapping>
        {
            MakeMapping(r, a1, "2026-05", 30),
            MakeMapping(r, a2, "2026-05", 50)
        };

        var snap = new MonthlyAllocationSnapshot("2026-05", [r], mappings);

        var ar = Assert.Single(snap.ActiveResources);
        // 10000 * 30/100 + 10000 * 50/100 = 3000 + 5000 = 8000
        Assert.Equal(8_000m, ar.AllocatedCost);
    }

    [Fact]
    public void IdleFte_And_IdleCost_DerivedFromUnallocatedPercent()
    {
        var r = MakeResource(1, 10_000m);
        var a1 = MakeSupportActivity(1, 10);
        var mappings = new List<Mapping> { MakeMapping(r, a1, "2026-05", 70) };

        var snap = new MonthlyAllocationSnapshot("2026-05", [r], mappings);

        var ar = Assert.Single(snap.ActiveResources);
        Assert.Equal(0.30, ar.IdleFte, precision: 10);
        Assert.Equal(3_000m, ar.IdleCost);
    }

    [Fact]
    public void FullyAllocatedResource_HasZeroIdleFteAndIdleCost()
    {
        var r = MakeResource(1, 8_000m);
        var a1 = MakeSupportActivity(1, 10);
        var mappings = new List<Mapping> { MakeMapping(r, a1, "2026-05", 100) };

        var snap = new MonthlyAllocationSnapshot("2026-05", [r], mappings);

        var ar = Assert.Single(snap.ActiveResources);
        Assert.Equal(0.0, ar.IdleFte);
        Assert.Equal(0m, ar.IdleCost);
    }

    [Fact]
    public void ActiveResourceAllocatedCost_MatchesSumOfActiveMappingAllocatedCosts()
    {
        var r = MakeResource(1, 12_000m);
        var a1 = MakeSupportActivity(1, 10);
        var a2 = MakeActivity(2, "Project", 20);
        var mappings = new List<Mapping>
        {
            MakeMapping(r, a1, "2026-05", 25),
            MakeMapping(r, a2, "2026-05", 75)
        };

        var snap = new MonthlyAllocationSnapshot("2026-05", [r], mappings);

        var ar = Assert.Single(snap.ActiveResources);
        var perMappingSum = snap.MappingsOf(r.Id).Sum(am => am.AllocatedCost);
        Assert.Equal(ar.AllocatedCost, perMappingSum);
    }

    [Fact]
    public void SupportCoverageOf_CountsOnlySupportMappings_MinorChangesAndProjectExcluded()
    {
        var r = MakeResource(1, 10_000m);
        var support     = MakeSupportActivity(1, 10);
        var minorChange = MakeActivity(2, "Minor Changes", 10);
        var project     = MakeActivity(3, "Project", 10);
        var mappings = new List<Mapping>
        {
            MakeMapping(r, support,     "2026-05", 40),
            MakeMapping(r, minorChange, "2026-05", 30),
            MakeMapping(r, project,     "2026-05", 30)
        };

        var snap = new MonthlyAllocationSnapshot("2026-05", [r], mappings);

        // Only Support 40% counts: 0.40
        Assert.Equal(0.40, snap.SupportCoverageOf(10), precision: 10);
    }

    [Fact]
    public void InactiveResource_ExcludedFromActiveResourcesAndActiveMappings()
    {
        var active   = MakeResource(1, 10_000m, activeInMay: true);
        var inactive = MakeResource(2, 8_000m,  activeInMay: false);
        var a1 = MakeSupportActivity(1, 10);
        var a2 = MakeSupportActivity(2, 20);
        var mappings = new List<Mapping>
        {
            MakeMapping(active,   a1, "2026-05", 100),
            MakeMapping(inactive, a2, "2026-05", 100)
        };

        var snap = new MonthlyAllocationSnapshot("2026-05", [active, inactive], mappings);

        Assert.Single(snap.ActiveResources);
        Assert.Equal(active.Id, snap.ActiveResources[0].Resource.Id);
        Assert.All(snap.ActiveMappings, am => Assert.Equal(active.Id, am.Resource.Id));
    }

    [Fact]
    public void AllocationPercent_SumsMultipleMappingPercents_ForOneResource()
    {
        var r = MakeResource(1, 10_000m);
        var a1 = MakeSupportActivity(1, 10);
        var a2 = MakeActivity(2, "Minor Changes", 10);
        var mappings = new List<Mapping>
        {
            MakeMapping(r, a1, "2026-05", 40),
            MakeMapping(r, a2, "2026-05", 60)
        };

        var snap = new MonthlyAllocationSnapshot("2026-05", [r], mappings);

        var ar = Assert.Single(snap.ActiveResources);
        Assert.Equal(100, ar.AllocationPercent);
    }
}
