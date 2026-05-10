using Microsoft.EntityFrameworkCore;
using WorkforceMapper.Server.Data;
using WorkforceMapper.Server.Models;
using WorkforceMapper.Server.Services;
using Xunit;

namespace WorkforceMapper.Tests;

public class DataServiceTests
{
    private static WorkforceDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<WorkforceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var ctx = new WorkforceDbContext(options);
        ctx.Database.EnsureCreated();
        return ctx;
    }

    [Fact]
    public async Task GetActivities_ReturnsAllActivities_WhenSeeded()
    {
        await using var context = CreateContext();
        var service = new DataService(context);

        var result = await service.GetActivitiesAsync();

        Assert.Equal(48, result.Count);
    }

    [Fact]
    public async Task GetMappings_ReturnsCorrectMappings_ForResource()
    {
        await using var context = CreateContext();

        var resource = new Resource
        {
            ResourceId = "PI00001", Name = "Test Resource", ResourceType = "Employee",
            Company = "Test Co", Seniority = "Analyst", RoleType = "Developer",
            Division = "Platform Engineering", Country = "UK", MonthlyRate = 9000m,
            Jan = 1, Feb = 1, Mar = 1, Apr = 1, May = 1,
            Jun = 1, Jul = 1, Aug = 1, Sep = 1, Oct = 1, Nov = 1, Dec = 1
        };
        context.Resources.Add(resource);
        await context.SaveChangesAsync();

        // Activity 1 and 3 are seeded via HasData (PE activities)
        context.Mappings.AddRange(
            new Mapping { ResourceId = resource.Id, ActivityId = 1, Month = "2026-05", Percent = 40},
            new Mapping { ResourceId = resource.Id, ActivityId = 3, Month = "2026-05", Percent = 60}
        );
        await context.SaveChangesAsync();

        var service = new DataService(context);
        var results = await service.GetResourcesAsync("Platform Engineering");

        var found = Assert.Single(results);
        Assert.Equal(2, found.Mappings.Count);
        Assert.Contains(found.Mappings, m => m.ActivityId == 1 && m.Percent == 40);
        Assert.Contains(found.Mappings, m => m.ActivityId == 3 && m.Percent == 60);
    }

    // ── get_capacity_by_division (summary envelope) ───────────────────────────

    [Fact]
    public async Task GetCapacityByDivision_TotalResourceCount_SumsAcrossDivisions()
    {
        // 2 PE resources active in May + 1 Analytics resource active in May = 3 total
        await using var context = CreateContext();
        var pe1 = MakePeResource("CAP00001", "PE One");
        var pe2 = MakePeResource("CAP00002", "PE Two");
        var analytics = new Resource
        {
            ResourceId = "CAP00003", Name = "Analytics One", ResourceType = "Employee",
            Company = "Test Co", Seniority = "Analyst", RoleType = "Developer",
            Division = "Analytics", Country = "UK", MonthlyRate = 7000m,
            Jan=0,Feb=0,Mar=0,Apr=0,May=1,Jun=0,Jul=0,Aug=0,Sep=0,Oct=0,Nov=0,Dec=0
        };
        context.Resources.AddRange(pe1, pe2, analytics);
        await context.SaveChangesAsync();
        context.Mappings.AddRange(
            new Mapping { ResourceId = pe1.Id,       ActivityId = 1, Month = "2026-05", Percent = 100 },
            new Mapping { ResourceId = pe2.Id,       ActivityId = 1, Month = "2026-05", Percent = 100 },
            new Mapping { ResourceId = analytics.Id, ActivityId = 9, Month = "2026-05", Percent = 100 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetCapacityByDivisionAsync("2026-05");

        Assert.Equal(3, result.TotalResourceCount);
    }

    [Fact]
    public async Task GetCapacityByDivision_OverallAllocationPct_IsWeightedByHeadcount_NotAverageOfAverages()
    {
        // PE: 1 resource at 100% → division alloc_pct = 100
        // Analytics: 9 resources at 50% each → division alloc_pct = 50
        // Naive average of averages = (100 + 50) / 2 = 75 — WRONG
        // Correct weighted = (1*100 + 9*50) / 10 = 550 / 10 = 55
        await using var context = CreateContext();
        var pe = MakePeResource("CAP00010", "PE Solo");
        var analyticsResources = Enumerable.Range(1, 9).Select(i => new Resource
        {
            ResourceId = $"CAP01{i:000}", Name = $"Analytics {i}", ResourceType = "Employee",
            Company = "Test Co", Seniority = "Analyst", RoleType = "Developer",
            Division = "Analytics", Country = "UK", MonthlyRate = 7000m,
            Jan=0,Feb=0,Mar=0,Apr=0,May=1,Jun=0,Jul=0,Aug=0,Sep=0,Oct=0,Nov=0,Dec=0
        }).ToList();
        context.Resources.Add(pe);
        context.Resources.AddRange(analyticsResources);
        await context.SaveChangesAsync();
        context.Mappings.Add(new Mapping { ResourceId = pe.Id, ActivityId = 1, Month = "2026-05", Percent = 100 });
        // Activity 9 is an Analytics Support activity
        foreach (var r in analyticsResources)
            context.Mappings.Add(new Mapping { ResourceId = r.Id, ActivityId = 9, Month = "2026-05", Percent = 50 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetCapacityByDivisionAsync("2026-05");

        Assert.Equal(55m, result.OverallAllocationPct);
    }

    [Fact]
    public async Task GetCapacityByDivision_ReturnsCorrectAllocationPct()
    {
        // 3 PE resources, each mapped at 60% + 52% = 112% per resource
        // allocation_pct = sum(percents) / active_count = 336 / 3 = 112
        await using var context = CreateContext();

        var peResources = Enumerable.Range(1, 3).Select(i => new Resource
        {
            ResourceId = $"PE{i:00000}",
            Name = $"PE Resource {i}",
            ResourceType = "Employee",
            Company = "Test Co",
            Seniority = "Senior Analyst",
            RoleType = "Developer",
            Division = "Platform Engineering",
            Country = "UK",
            MonthlyRate = 10000m,
            Jan = 0, Feb = 0, Mar = 0, Apr = 0, May = 1,
            Jun = 0, Jul = 0, Aug = 0, Sep = 0, Oct = 0, Nov = 0, Dec = 0
        }).ToList();
        context.Resources.AddRange(peResources);
        await context.SaveChangesAsync();

        foreach (var r in peResources)
        {
            context.Mappings.AddRange(
                new Mapping { ResourceId = r.Id, ActivityId = 1, Month = "2026-05", Percent = 60},
                new Mapping { ResourceId = r.Id, ActivityId = 3, Month = "2026-05", Percent = 52}
            );
        }
        await context.SaveChangesAsync();

        var service = new DataService(context);
        var result = await service.GetCapacityByDivisionAsync("2026-05");

        var pe = result.Divisions.Single(r => r.Division == "Platform Engineering");
        Assert.Equal(3, pe.ResourceCount);
        Assert.Equal(112m, pe.AllocationPct);
    }

    // --- Cycle 10: regression — Unattributed line unchanged post risk feature ---

    [Fact]
    public async Task GetCostByProduct_Unattributed_LineIsUnchanged_PostRiskFeature()
    {
        // AuthService (app 4) has no product_applications entries, so support cost
        // for resources mapped to AuthService is Unattributed. This must hold after #12.
        await using var context = CreateContext();

        var resource = new Resource
        {
            ResourceId = "UA00001", Name = "Unattrib Resource", ResourceType = "Employee",
            Company = "Test Co", Seniority = "Analyst", RoleType = "Developer",
            Division = "Platform Engineering", Country = "UK", MonthlyRate = 10000m,
            Jan = 1, Feb = 1, Mar = 1, Apr = 1, May = 1,
            Jun = 1, Jul = 1, Aug = 1, Sep = 1, Oct = 1, Nov = 1, Dec = 1
        };
        context.Resources.Add(resource);
        await context.SaveChangesAsync();

        // Activity 5 = AuthService - Support (seeded); AuthService has no ProductApplications
        context.Mappings.Add(new Mapping { ResourceId = resource.Id, ActivityId = 5, Month = "2026-05", Percent = 100});
        await context.SaveChangesAsync();

        var service = new DataService(context);
        var summary = await service.GetCostByProductAsync("2026-05");

        var unattributed = summary.Products.SingleOrDefault(p => p.ProductName == "Unattributed");
        Assert.NotNull(unattributed);
        Assert.Equal(10000m, unattributed.TotalAllocatedCost);
    }

    [Fact]
    public async Task GetResources_Returns80Resources_WhenSeeded()
    {
        await using var context = CreateContext();

        var resources = Enumerable.Range(1, 80).Select(i => new Resource
        {
            ResourceId = $"PI{i:00000}",
            Name = $"Resource {i}",
            ResourceType = "Employee",
            Company = "Test Co",
            Seniority = "Analyst",
            RoleType = "Developer",
            Division = "Platform Engineering",
            Country = "UK",
            MonthlyRate = 5000m,
            Jan = 1, Feb = 1, Mar = 1, Apr = 1, May = 1,
            Jun = 1, Jul = 1, Aug = 1, Sep = 1, Oct = 1, Nov = 1, Dec = 1
        });
        context.Resources.AddRange(resources);
        await context.SaveChangesAsync();

        var service = new DataService(context);
        var result = await service.GetResourcesAsync();

        Assert.Equal(80, result.Count);
    }

    // ── get_donor_candidates ─────────────────────────────────────────────────

    private static Resource MakePeResource(string id, string name, decimal rate = 10000m) => new()
    {
        ResourceId = id, Name = name, ResourceType = "Employee",
        Company = "Test Co", Seniority = "Analyst", RoleType = "Developer",
        Division = "Platform Engineering", Country = "UK", MonthlyRate = rate,
        Jan = 0, Feb = 0, Mar = 0, Apr = 0, May = 1,
        Jun = 0, Jul = 0, Aug = 0, Sep = 0, Oct = 0, Nov = 0, Dec = 0
    };

    // Regression: get_donor_candidates must accept application_name when ID is unknown
    [Fact]
    public async Task GetDonorCandidates_LooksUpByName_WhenApplicationNameProvided()
    {
        // The model knows "AuthService" from the query but not its numeric ID (4).
        // Passing null ID + name must resolve to the correct application.
        await using var context = CreateContext();
        var svc = new DataService(context);

        var result = await svc.GetDonorCandidatesAsync(null, "AuthService", "2026-05");

        Assert.NotNull(result);
        Assert.Equal(4, result.ApplicationId);
        Assert.Equal("AuthService", result.ApplicationName);
    }

    [Fact]
    public async Task GetDonorCandidates_ReturnsNull_ForUnknownApplicationName()
    {
        await using var context = CreateContext();
        var svc = new DataService(context);

        var result = await svc.GetDonorCandidatesAsync(null, "DoesNotExist", "2026-05");

        Assert.Null(result);
    }

    // Cycle 7 — Returns null for unknown application_id
    [Fact]
    public async Task GetDonorCandidates_ReturnsNull_ForUnknownApplicationId()
    {
        await using var context = CreateContext();
        var svc = new DataService(context);

        var result = await svc.GetDonorCandidatesAsync(9999, null, "2026-05");

        Assert.Null(result);
    }

    // Cycle 6 — Coverage summary fields are correct
    [Fact]
    public async Task GetDonorCandidates_ReturnsCoverageFields_Correctly()
    {
        await using var context = CreateContext();
        // Two resources: one on AuthService Support (30%), one on a project (donor)
        var supporter = MakePeResource("DC00006a", "Frank Support");
        var donor     = MakePeResource("DC00006b", "Greta Donor");
        context.Resources.AddRange(supporter, donor);
        await context.SaveChangesAsync();
        // Activity 5 = "AuthService - Support"; Activity 7 = "P01 ... - Project"
        context.Mappings.AddRange(
            new Mapping { ResourceId = supporter.Id, ActivityId = 5, Month = "2026-05", Percent = 30 },
            new Mapping { ResourceId = donor.Id,     ActivityId = 7, Month = "2026-05", Percent = 40 }
        );
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        // AuthService min_support_fte = 4.0 (from seeded data)
        var result = await svc.GetDonorCandidatesAsync(4, null, "2026-05");

        Assert.NotNull(result);
        Assert.Equal(4, result.ApplicationId);
        Assert.Equal("AuthService", result.ApplicationName);
        Assert.Equal("Platform Engineering", result.Division);
        Assert.Equal(0.30, result.CurrentCoverageFte, precision: 5);
        Assert.Equal(4.0,  result.MinSupportFte);
        Assert.Equal(3.70, result.GapFte, precision: 5);       // 4.0 - 0.30
    }

    // Pre-computed counts must match unique resources and total allocation rows
    [Fact]
    public async Task GetDonorCandidates_ReturnsPrecomputedCounts_Correctly()
    {
        await using var context = CreateContext();
        // Two donors: one with two separate project allocations (two rows), one with one
        var donorA = MakePeResource("DC00006c", "Alice Two");
        var donorB = MakePeResource("DC00006d", "Bob One");
        context.Resources.AddRange(donorA, donorB);
        await context.SaveChangesAsync();
        // Activity 7 and 8 are both PE project activities
        context.Mappings.AddRange(
            new Mapping { ResourceId = donorA.Id, ActivityId = 7, Month = "2026-05", Percent = 30 },
            new Mapping { ResourceId = donorA.Id, ActivityId = 8, Month = "2026-05", Percent = 30 },
            new Mapping { ResourceId = donorB.Id, ActivityId = 7, Month = "2026-05", Percent = 40 }
        );
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(4, null, "2026-05");

        Assert.NotNull(result);
        // 3 allocation rows in total, across 2 unique resources
        Assert.Equal(3, result.TotalAllocationCount);
        Assert.Equal(2, result.UniqueResourceCount);
        // Sanity: counts match the actual list
        Assert.Equal(result.TotalAllocationCount, result.Candidates.Count);
        Assert.Equal(result.UniqueResourceCount,
            result.Candidates.Select(c => c.ResourceName).Distinct().Count());
    }

    // Cycle 5 — Inactive resources for the queried month are excluded
    [Fact]
    public async Task GetDonorCandidates_ExcludesInactiveResources_ForQueriedMonth()
    {
        await using var context = CreateContext();
        var inactive = new Resource
        {
            ResourceId = "DC00005", Name = "Eve Inactive", ResourceType = "Employee",
            Company = "Test Co", Seniority = "Analyst", RoleType = "Developer",
            Division = "Platform Engineering", Country = "UK", MonthlyRate = 10000m,
            Jan = 1, Feb = 1, Mar = 1, Apr = 1, May = 0,   // inactive in May
            Jun = 1, Jul = 1, Aug = 1, Sep = 1, Oct = 1, Nov = 1, Dec = 1
        };
        context.Resources.Add(inactive);
        await context.SaveChangesAsync();
        context.Mappings.Add(new Mapping { ResourceId = inactive.Id, ActivityId = 7, Month = "2026-05", Percent = 50 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(4, null, "2026-05");

        Assert.NotNull(result);
        Assert.Empty(result.Candidates);
    }

    // Cycle 4 — Resources from other divisions are excluded
    [Fact]
    public async Task GetDonorCandidates_ExcludesResources_FromOtherDivisions()
    {
        await using var context = CreateContext();
        var analyticsResource = new Resource
        {
            ResourceId = "DC00004", Name = "Dave Analytics", ResourceType = "Employee",
            Company = "Test Co", Seniority = "Analyst", RoleType = "Developer",
            Division = "Analytics", Country = "UK", MonthlyRate = 10000m,
            Jan = 0, Feb = 0, Mar = 0, Apr = 0, May = 1,
            Jun = 0, Jul = 0, Aug = 0, Sep = 0, Oct = 0, Nov = 0, Dec = 0
        };
        context.Resources.Add(analyticsResource);
        await context.SaveChangesAsync();
        // Activity 19 = "P07 SegmentBuilder Enhancement - Project" (Analytics division)
        context.Mappings.Add(new Mapping { ResourceId = analyticsResource.Id, ActivityId = 19, Month = "2026-05", Percent = 40 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        // AuthService is Platform Engineering — Analytics resource should not appear
        var result = await svc.GetDonorCandidatesAsync(4, null, "2026-05");

        Assert.NotNull(result);
        Assert.Empty(result.Candidates);
    }

    // Cycle 3 — Support allocations are excluded (not deferrable)
    [Fact]
    public async Task GetDonorCandidates_ExcludesSupport_Allocations()
    {
        await using var context = CreateContext();
        var resource = MakePeResource("DC00003", "Carol Support");
        context.Resources.Add(resource);
        await context.SaveChangesAsync();
        // Activity 1 = "CoreDataWarehouse - Support" (Platform Engineering)
        context.Mappings.Add(new Mapping { ResourceId = resource.Id, ActivityId = 1, Month = "2026-05", Percent = 50 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(4, null, "2026-05");

        Assert.NotNull(result);
        Assert.Empty(result.Candidates);
    }

    // Cycle 2 — Minor Changes allocations also surface as candidates
    [Fact]
    public async Task GetDonorCandidates_ReturnsMinorChangesAllocations_InAppDivision()
    {
        await using var context = CreateContext();
        var resource = MakePeResource("DC00002", "Bob MinorChg");
        context.Resources.Add(resource);
        await context.SaveChangesAsync();
        // Activity 35 = "SurveyEngine - Minor Changes" (Platform Engineering)
        context.Mappings.Add(new Mapping { ResourceId = resource.Id, ActivityId = 35, Month = "2026-05", Percent = 25 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(4, null, "2026-05");

        Assert.NotNull(result);
        Assert.Single(result.Candidates, c =>
            c.ResourceName == "Bob MinorChg" &&
            c.ActivityName == "SurveyEngine - Minor Changes" &&
            c.ActivityType == "Minor Changes" &&
            c.Percent == 25);
    }

    // Cycle 1 — Project allocations in the app's division surface as candidates
    [Fact]
    public async Task GetDonorCandidates_ReturnsProjectAllocations_InAppDivision()
    {
        await using var context = CreateContext();
        var resource = MakePeResource("DC00001", "Alice Project");
        context.Resources.Add(resource);
        await context.SaveChangesAsync();
        // Activity 7 = "P01 CoreDataWarehouse Cloud Migration - Project" (Platform Engineering)
        context.Mappings.Add(new Mapping { ResourceId = resource.Id, ActivityId = 7, Month = "2026-05", Percent = 30 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        // ApplicationId 4 = AuthService (Platform Engineering)
        var result = await svc.GetDonorCandidatesAsync(4, null, "2026-05");

        Assert.NotNull(result);
        Assert.Single(result.Candidates, c =>
            c.ResourceName == "Alice Project" &&
            c.ActivityName == "P01 CoreDataWarehouse Cloud Migration - Project" &&
            c.ActivityType == "Project" &&
            c.Percent == 30);
    }

    // Candidates sorted cheapest-per-FTE first
    [Fact]
    public async Task GetDonorCandidates_SortsByCostPerFte_Ascending()
    {
        await using var context = CreateContext();
        // cheap resource ($5,000/month), expensive resource ($13,000/month), both 25% on a project
        var cheap     = new Resource { ResourceId = "DC00007a", Name = "Cheap Donor",     ResourceType = "Employee", Company = "Test Co", Seniority = "Analyst", RoleType = "Developer", Division = "Platform Engineering", Country = "UK", MonthlyRate = 5000m,  Jan=0,Feb=0,Mar=0,Apr=0,May=1,Jun=0,Jul=0,Aug=0,Sep=0,Oct=0,Nov=0,Dec=0 };
        var expensive = new Resource { ResourceId = "DC00007b", Name = "Expensive Donor", ResourceType = "Employee", Company = "Test Co", Seniority = "Analyst", RoleType = "Developer", Division = "Platform Engineering", Country = "UK", MonthlyRate = 13000m, Jan=0,Feb=0,Mar=0,Apr=0,May=1,Jun=0,Jul=0,Aug=0,Sep=0,Oct=0,Nov=0,Dec=0 };
        context.Resources.AddRange(cheap, expensive);
        await context.SaveChangesAsync();
        // Activity 7 = PE project activity
        context.Mappings.AddRange(
            new Mapping { ResourceId = cheap.Id,     ActivityId = 7, Month = "2026-05", Percent = 25 },
            new Mapping { ResourceId = expensive.Id, ActivityId = 7, Month = "2026-05", Percent = 25 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(4, null, "2026-05");

        Assert.NotNull(result);
        Assert.True(result.Candidates.Count >= 2);
        var ourTwo = result.Candidates.Where(c => c.ResourceName is "Cheap Donor" or "Expensive Donor").ToList();
        Assert.Equal(2, ourTwo.Count);
        Assert.Equal("Cheap Donor", ourTwo[0].ResourceName);
        Assert.Equal("Expensive Donor", ourTwo[1].ResourceName);
    }

    // gap_coverage_pct reflects fraction of FTE gap each candidate closes
    [Fact]
    public async Task GetDonorCandidates_GapCoveragePct_ReflectsAllocationFractionOfGap()
    {
        await using var context = CreateContext();
        // AuthService min = 4.0 FTE, no existing support → gap = 4.0 FTE
        // donor at 40% → 0.40 FTE → covers 10% of gap
        var donor = MakePeResource("DC00008", "Gap Donor");
        context.Resources.Add(donor);
        await context.SaveChangesAsync();
        context.Mappings.Add(new Mapping { ResourceId = donor.Id, ActivityId = 7, Month = "2026-05", Percent = 40 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(4, null, "2026-05");

        Assert.NotNull(result);
        var row = Assert.Single(result.Candidates, c => c.ResourceName == "Gap Donor");
        Assert.Equal(0.40, row.AllocationFte, precision: 5);
        Assert.Equal(10.0, row.GapCoveragePct, precision: 3); // 0.40 / 4.0 * 100
        // Single-row cumulative fields equal the per-row values
        Assert.Equal(0.40, row.CumulativeFte, precision: 5);
        Assert.Equal(10.0, row.CumulativeGapCoveragePct, precision: 3);
    }

    // cumulative_fte and cumulative_gap_coverage_pct accumulate in sort order
    [Fact]
    public async Task GetDonorCandidates_CumulativeFields_AccumulateInSortOrder()
    {
        await using var context = CreateContext();
        // AuthService min = 4.0 FTE, no support → gap = 4.0 FTE
        // cheap resource ($5,000/month) at 40% → 0.40 FTE → 10% gap coverage
        // expensive ($13,000/month) at 60% → 0.60 FTE → 15% gap coverage
        // Sorted cheap-first; cumulative after row 2 = 1.0 FTE = 25% gap coverage
        var cheap     = new Resource { ResourceId = "DC00009a", Name = "Cumul Cheap",     ResourceType = "Employee", Company = "Test Co", Seniority = "Analyst", RoleType = "Developer", Division = "Platform Engineering", Country = "UK", MonthlyRate = 5000m,  Jan=0,Feb=0,Mar=0,Apr=0,May=1,Jun=0,Jul=0,Aug=0,Sep=0,Oct=0,Nov=0,Dec=0 };
        var expensive = new Resource { ResourceId = "DC00009b", Name = "Cumul Expensive", ResourceType = "Employee", Company = "Test Co", Seniority = "Analyst", RoleType = "Developer", Division = "Platform Engineering", Country = "UK", MonthlyRate = 13000m, Jan=0,Feb=0,Mar=0,Apr=0,May=1,Jun=0,Jul=0,Aug=0,Sep=0,Oct=0,Nov=0,Dec=0 };
        context.Resources.AddRange(cheap, expensive);
        await context.SaveChangesAsync();
        context.Mappings.AddRange(
            new Mapping { ResourceId = cheap.Id,     ActivityId = 7, Month = "2026-05", Percent = 40 },
            new Mapping { ResourceId = expensive.Id, ActivityId = 7, Month = "2026-05", Percent = 60 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(4, null, "2026-05");

        Assert.NotNull(result);
        var ourTwo = result.Candidates.Where(c => c.ResourceName is "Cumul Cheap" or "Cumul Expensive").ToList();
        Assert.Equal(2, ourTwo.Count);
        var cheapRow     = ourTwo[0]; // sorted first (cheaper rate)
        var expensiveRow = ourTwo[1];

        Assert.Equal(0.40, cheapRow.CumulativeFte, precision: 5);
        Assert.Equal(10.0, cheapRow.CumulativeGapCoveragePct, precision: 3);
        Assert.Equal(1.00, expensiveRow.CumulativeFte, precision: 5);
        Assert.Equal(25.0, expensiveRow.CumulativeGapCoveragePct, precision: 3);
    }

    // ── get_unallocated_resources ─────────────────────────────────────────────

    [Fact]
    public async Task GetUnallocatedResources_TotalResourceCount_SumsAcrossDivisions()
    {
        // Two active resources below 100% allocation: one PE, one Analytics.
        // TotalResourceCount must equal 2 (sum across both division groups).
        await using var context = CreateContext();
        var pe = new Resource
        {
            ResourceId = "UR00001", Name = "PE Idle", ResourceType = "Employee",
            Company = "Test Co", Seniority = "Analyst", RoleType = "Developer",
            Division = "Platform Engineering", Country = "UK", MonthlyRate = 8000m,
            Jan=0,Feb=0,Mar=0,Apr=0,May=1,Jun=0,Jul=0,Aug=0,Sep=0,Oct=0,Nov=0,Dec=0
        };
        var analytics = new Resource
        {
            ResourceId = "UR00002", Name = "Analytics Idle", ResourceType = "Employee",
            Company = "Test Co", Seniority = "Analyst", RoleType = "Developer",
            Division = "Analytics", Country = "UK", MonthlyRate = 7000m,
            Jan=0,Feb=0,Mar=0,Apr=0,May=1,Jun=0,Jul=0,Aug=0,Sep=0,Oct=0,Nov=0,Dec=0
        };
        context.Resources.AddRange(pe, analytics);
        await context.SaveChangesAsync();
        // Each mapped at 80% → 20% idle → both below 100% threshold
        context.Mappings.AddRange(
            new Mapping { ResourceId = pe.Id,        ActivityId = 1, Month = "2026-05", Percent = 80 },
            new Mapping { ResourceId = analytics.Id, ActivityId = 9, Month = "2026-05", Percent = 80 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetUnallocatedResourcesAsync(100, null, "2026-05");

        Assert.Equal(2, result.TotalResourceCount);
    }

    // get_application_risk must accept application_name so the LLM can filter to one app
    // without knowing its numeric ID — same contract as get_donor_candidates
    [Fact]
    public async Task GetApplications_FiltersByName_ReturnsOnlyAuthService()
    {
        await using var context = CreateContext();
        var svc = new DataService(context);

        var results = await svc.GetApplicationsAsync("2026-05", applicationName: "AuthService");

        var single = Assert.Single(results);
        Assert.Equal("AuthService", single.name);
        Assert.Equal(4, single.id);
    }

    [Fact]
    public async Task GetApplications_ByName_CaseInsensitive()
    {
        await using var context = CreateContext();
        var svc = new DataService(context);

        var results = await svc.GetApplicationsAsync("2026-05", applicationName: "authservice");

        Assert.Single(results);
    }

    // dependent_app_count is pre-computed so the LLM never has to count the transitive_dependent_apps list
    [Fact]
    public async Task GetApplications_DependentAppCount_Is17_ForAuthService()
    {
        // AuthService: 14 direct dependents + 3 indirect via CoreDataWarehouse = 17 total
        await using var context = CreateContext();
        var svc = new DataService(context);

        var results = await svc.GetApplicationsAsync("2026-05", applicationId: 4);

        var authService = Assert.Single(results);
        Assert.Equal(17, authService.dependent_app_count);
        Assert.Equal(17, authService.transitive_dependent_apps.Count);
    }

    // total_dependent_arr_usd is pre-computed on get_application_risk, and all 7 products appear
    // in dependent_products for AuthService (products 6+7 depend on AuthService only via direct
    // app-level deps, not via CoreDataWarehouse — this exercises the non-trivial transitive path)
    [Fact]
    public async Task GetApplications_TotalDependentArrUsd_EqualsProductArrSum()
    {
        await using var context = CreateContext();
        var svc = new DataService(context);

        var results = await svc.GetApplicationsAsync("2026-05", applicationId: 4); // AuthService

        var authService = Assert.Single(results);
        var expectedTotal = authService.dependent_products.Sum(p => p.arr_usd);
        Assert.Equal(expectedTotal, authService.total_dependent_arr_usd);
        Assert.True(authService.total_dependent_arr_usd > 0);
    }

    [Fact]
    public async Task GetApplications_AllSevenProducts_InDependentProducts_ForAuthService()
    {
        // ConsumerPanel (P6) and FieldworkServices (P7) reach AuthService only via direct
        // app deps (PanelIQ→Auth, WebCollect→Auth, etc.) — no CoreDataWarehouse hop.
        // Verifies the reverse-closure logic captures these, not just products 1-5.
        await using var context = CreateContext();
        var svc = new DataService(context);

        var results = await svc.GetApplicationsAsync("2026-05", applicationId: 4);

        var authService = Assert.Single(results);
        Assert.Equal(7, authService.dependent_products.Count);
        Assert.Contains(authService.dependent_products, p => p.name == "ConsumerPanel");
        Assert.Contains(authService.dependent_products, p => p.name == "FieldworkServices");
        Assert.Equal(28900000m, authService.total_dependent_arr_usd); // 6.2+5.1+4.6+4.2+3.8+3.4+1.6 M
    }

    // ── get_product_risk (summary envelope) ──────────────────────────────────

    [Fact]
    public async Task GetProductRisk_HighRiskProductCount_MatchesProductsAtHighRisk()
    {
        // Seed: AuthService is HIGH (3 FTE, min 4.0), so all 7 products become HIGH.
        // high_risk_product_count must equal 7; medium and low must be 0.
        await using var context = CreateContext();
        var resources = Enumerable.Range(1, 3).Select(i => new Resource
        {
            ResourceId = $"PR{i:00000}", Name = $"PR Resource {i}", ResourceType = "Employee",
            Company = "Test Co", Seniority = "Analyst", RoleType = "Developer",
            Division = "Platform Engineering", Country = "UK", MonthlyRate = 10000m,
            Jan=0,Feb=0,Mar=0,Apr=0,May=1,Jun=0,Jul=0,Aug=0,Sep=0,Oct=0,Nov=0,Dec=0
        }).ToList();
        context.Resources.AddRange(resources);
        await context.SaveChangesAsync();
        foreach (var r in resources)
            context.Mappings.Add(new Mapping { ResourceId = r.Id, ActivityId = 5, Month = "2026-05", Percent = 100 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetProductRiskAsync("2026-05");

        Assert.Equal(7, result.HighRiskProductCount);
        Assert.Equal(0, result.MediumRiskProductCount);
        Assert.Equal(0, result.LowRiskProductCount);
    }

    [Fact]
    public async Task GetProductRisk_TotalHighRiskArrUsd_SumsArrForHighRiskProducts()
    {
        // All 7 products are HIGH when AuthService is HIGH.
        // Total ARR across all products = $28.9M (from seed data) → that's what TotalHighRiskArrUsd must equal.
        await using var context = CreateContext();
        var resources = Enumerable.Range(1, 3).Select(i => new Resource
        {
            ResourceId = $"ARR{i:00000}", Name = $"ARR Resource {i}", ResourceType = "Employee",
            Company = "Test Co", Seniority = "Analyst", RoleType = "Developer",
            Division = "Platform Engineering", Country = "UK", MonthlyRate = 10000m,
            Jan=0,Feb=0,Mar=0,Apr=0,May=1,Jun=0,Jul=0,Aug=0,Sep=0,Oct=0,Nov=0,Dec=0
        }).ToList();
        context.Resources.AddRange(resources);
        await context.SaveChangesAsync();
        foreach (var r in resources)
            context.Mappings.Add(new Mapping { ResourceId = r.Id, ActivityId = 5, Month = "2026-05", Percent = 100 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetProductRiskAsync("2026-05");

        // All 7 at risk → high ARR = total ARR; medium = 0
        var expectedHighArr = result.Products.Sum(p => p.arr);
        Assert.Equal(expectedHighArr, result.TotalHighRiskArrUsd);
        Assert.Equal(0m, result.TotalMediumRiskArrUsd);
    }

    // ── Regression: product risk propagation from AuthService ────────────────
    // Scenario: AuthService is HIGH (3.0 FTE coverage, below 4.0 min).
    // All of AuthService's transitive dependents are LOW (adequate coverage).
    // Every product has at least one app that transitively depends on AuthService,
    // so all 7 products must be HIGH. Previously the agent reported only 5.
    [Fact]
    public async Task GetProductRisk_AllSevenProductsHigh_WhenOnlyAuthServiceIsHighRisk()
    {
        await using var context = CreateContext();

        // 3 resources active only in May
        var resources = Enumerable.Range(1, 3).Select(i => new Resource
        {
            ResourceId = $"RG{i:00000}", Name = $"Reg Resource {i}", ResourceType = "Employee",
            Company = "Test Co", Seniority = "Analyst", RoleType = "Developer",
            Division = "Platform Engineering", Country = "UK", MonthlyRate = 10000m,
            Jan = 0, Feb = 0, Mar = 0, Apr = 0, May = 1,
            Jun = 0, Jul = 0, Aug = 0, Sep = 0, Oct = 0, Nov = 0, Dec = 0
        }).ToList();
        context.Resources.AddRange(resources);
        await context.SaveChangesAsync();

        // Map each resource 100% to AuthService Support (activity 5) → 3.0 FTE
        // AuthService min=4.0, so 3.0 FTE < 4.0 → HIGH
        foreach (var r in resources)
            context.Mappings.Add(new Mapping { ResourceId = r.Id, ActivityId = 5, Month = "2026-05", Percent = 100});

        // All other apps have 0 FTE mappings → also HIGH individually, but that
        // doesn't affect the structural assertion: every product's closure contains
        // AuthService, so every product must be HIGH regardless.
        await context.SaveChangesAsync();

        var service = new DataService(context);
        var summary = await service.GetProductRiskAsync("2026-05");

        Assert.Equal(7, summary.Products.Count);
        Assert.All(summary.Products, p => Assert.Equal("high", p.risk));

        var consumerPanel = summary.Products.Single(p => p.name == "ConsumerPanel");
        var fieldwork = summary.Products.Single(p => p.name == "FieldworkServices");
        Assert.Equal("high", consumerPanel.risk);
        Assert.Equal("high", fieldwork.risk);

        // AuthService must appear in contributing_apps for both products
        Assert.Contains(consumerPanel.contributing_apps, a => a.name == "AuthService");
        Assert.Contains(fieldwork.contributing_apps, a => a.name == "AuthService");
    }
}
