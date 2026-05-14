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

    [Fact]
    public async Task GetCapacityByDivision_FullyConstrainedDivisions_ListsOnlyDivisionsAt100Pct()
    {
        // Division A: 1 resource at 100% → fully constrained
        // Division B: 1 resource at 80% → not fully constrained
        await using var context = CreateContext();

        var resA = new Resource
        {
            ResourceId = "FC00001", Name = "Full A", ResourceType = "Employee",
            Company = "Test Co", Seniority = "Analyst", RoleType = "Developer",
            Division = "Division A", Country = "UK", MonthlyRate = 5000m,
            Jan = 0, Feb = 0, Mar = 0, Apr = 0, May = 1,
            Jun = 0, Jul = 0, Aug = 0, Sep = 0, Oct = 0, Nov = 0, Dec = 0
        };
        var resB = new Resource
        {
            ResourceId = "FC00002", Name = "Slack B", ResourceType = "Employee",
            Company = "Test Co", Seniority = "Analyst", RoleType = "Developer",
            Division = "Division B", Country = "UK", MonthlyRate = 5000m,
            Jan = 0, Feb = 0, Mar = 0, Apr = 0, May = 1,
            Jun = 0, Jul = 0, Aug = 0, Sep = 0, Oct = 0, Nov = 0, Dec = 0
        };
        context.Resources.AddRange(resA, resB);
        await context.SaveChangesAsync();

        context.Mappings.Add(new Mapping { ResourceId = resA.Id, ActivityId = 1, Month = "2026-05", Percent = 100 });
        context.Mappings.Add(new Mapping { ResourceId = resB.Id, ActivityId = 1, Month = "2026-05", Percent = 80 });
        await context.SaveChangesAsync();

        var service = new DataService(context);
        var result = await service.GetCapacityByDivisionAsync("2026-05");

        Assert.Contains("Division A", result.FullyConstrainedDivisions);
        Assert.DoesNotContain("Division B", result.FullyConstrainedDivisions);
    }

    [Fact]
    public async Task GetCapacityByDivision_FullyConstrainedDivisions_ListsMultipleDivisions_WhenAllAt100Pct()
    {
        // Two divisions both at 100%: both should appear in FullyConstrainedDivisions
        await using var context = CreateContext();

        foreach (var (id, div) in new[] { ("FC00010", "Div X"), ("FC00011", "Div Y") })
        {
            var r = new Resource
            {
                ResourceId = id, Name = $"Resource {id}", ResourceType = "Employee",
                Company = "Test Co", Seniority = "Analyst", RoleType = "Developer",
                Division = div, Country = "UK", MonthlyRate = 5000m,
                Jan = 0, Feb = 0, Mar = 0, Apr = 0, May = 1,
                Jun = 0, Jul = 0, Aug = 0, Sep = 0, Oct = 0, Nov = 0, Dec = 0
            };
            context.Resources.Add(r);
            await context.SaveChangesAsync();
            context.Mappings.Add(new Mapping { ResourceId = r.Id, ActivityId = 1, Month = "2026-05", Percent = 100 });
            await context.SaveChangesAsync();
        }

        var service = new DataService(context);
        var result = await service.GetCapacityByDivisionAsync("2026-05");

        Assert.Equal(2, result.FullyConstrainedDivisions.Count);
        Assert.Contains("Div X", result.FullyConstrainedDivisions);
        Assert.Contains("Div Y", result.FullyConstrainedDivisions);
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

    // Cycle 4 (updated for #29) — cross-div resources surface as tier 6 with alignment flag
    [Fact]
    public async Task GetDonorCandidates_CrossDivisionalResources_AppearAsTier6_WithAlignmentFlag()
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
        var result = await svc.GetDonorCandidatesAsync(4, null, "2026-05");

        Assert.NotNull(result);
        var row = Assert.Single(result.Candidates, c => c.ResourceName == "Dave Analytics");
        Assert.True(row.RequiresCrossDivisionalAlignment);
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

    // Candidates sorted largest-allocation-FTE first within each tier
    [Fact]
    public async Task GetDonorCandidates_SortsByAllocationFte_Descending()
    {
        await using var context = CreateContext();
        // small resource at 15%, large resource at 40% — large FTE should sort first
        var small = new Resource { ResourceId = "DC00007a", Name = "Small Donor", ResourceType = "Employee", Company = "Test Co", Seniority = "Analyst", RoleType = "Developer", Division = "Platform Engineering", Country = "UK", MonthlyRate = 5000m,  Jan=0,Feb=0,Mar=0,Apr=0,May=1,Jun=0,Jul=0,Aug=0,Sep=0,Oct=0,Nov=0,Dec=0 };
        var large = new Resource { ResourceId = "DC00007b", Name = "Large Donor",  ResourceType = "Employee", Company = "Test Co", Seniority = "Analyst", RoleType = "Developer", Division = "Platform Engineering", Country = "UK", MonthlyRate = 13000m, Jan=0,Feb=0,Mar=0,Apr=0,May=1,Jun=0,Jul=0,Aug=0,Sep=0,Oct=0,Nov=0,Dec=0 };
        context.Resources.AddRange(small, large);
        await context.SaveChangesAsync();
        // Activity 7 = PE project activity
        context.Mappings.AddRange(
            new Mapping { ResourceId = small.Id, ActivityId = 7, Month = "2026-05", Percent = 15 },
            new Mapping { ResourceId = large.Id, ActivityId = 7, Month = "2026-05", Percent = 40 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(4, null, "2026-05");

        Assert.NotNull(result);
        Assert.True(result.Candidates.Count >= 2);
        var ourTwo = result.Candidates.Where(c => c.ResourceName is "Small Donor" or "Large Donor").ToList();
        Assert.Equal(2, ourTwo.Count);
        Assert.Equal("Large Donor", ourTwo[0].ResourceName);
        Assert.Equal("Small Donor", ourTwo[1].ResourceName);
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

    // cumulative_fte and cumulative_gap_coverage_pct accumulate in sort order (largest FTE first)
    [Fact]
    public async Task GetDonorCandidates_CumulativeFields_AccumulateInSortOrder()
    {
        await using var context = CreateContext();
        // AuthService min = 4.0 FTE, no support → gap = 4.0 FTE
        // small resource at 40% → 0.40 FTE → 10% gap coverage
        // large resource at 60% → 0.60 FTE → 15% gap coverage
        // Sorted largest-FTE-first: large (60%) then small (40%); cumulative after row 2 = 1.0 FTE = 25%
        var small = new Resource { ResourceId = "DC00009a", Name = "Cumul Small", ResourceType = "Employee", Company = "Test Co", Seniority = "Analyst", RoleType = "Developer", Division = "Platform Engineering", Country = "UK", MonthlyRate = 5000m,  Jan=0,Feb=0,Mar=0,Apr=0,May=1,Jun=0,Jul=0,Aug=0,Sep=0,Oct=0,Nov=0,Dec=0 };
        var large = new Resource { ResourceId = "DC00009b", Name = "Cumul Large", ResourceType = "Employee", Company = "Test Co", Seniority = "Analyst", RoleType = "Developer", Division = "Platform Engineering", Country = "UK", MonthlyRate = 13000m, Jan=0,Feb=0,Mar=0,Apr=0,May=1,Jun=0,Jul=0,Aug=0,Sep=0,Oct=0,Nov=0,Dec=0 };
        context.Resources.AddRange(small, large);
        await context.SaveChangesAsync();
        context.Mappings.AddRange(
            new Mapping { ResourceId = small.Id, ActivityId = 7, Month = "2026-05", Percent = 40 },
            new Mapping { ResourceId = large.Id, ActivityId = 7, Month = "2026-05", Percent = 60 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(4, null, "2026-05");

        Assert.NotNull(result);
        var ourTwo = result.Candidates.Where(c => c.ResourceName is "Cumul Small" or "Cumul Large").ToList();
        Assert.Equal(2, ourTwo.Count);
        var largeRow = ourTwo[0]; // sorted first (larger FTE)
        var smallRow = ourTwo[1];

        Assert.Equal(0.60, largeRow.CumulativeFte, precision: 5);
        Assert.Equal(15.0, largeRow.CumulativeGapCoveragePct, precision: 3);
        Assert.Equal(1.00, smallRow.CumulativeFte, precision: 5);
        Assert.Equal(25.0, smallRow.CumulativeGapCoveragePct, precision: 3);
    }

    // ── Issue #29: six-tier donor candidate pipeline ─────────────────────────

    // Case 4 (tracer bullet) — tier 4–6 rows have RequiresCrossDivisionalAlignment = true; tiers 1–3 have false
    [Fact]
    public async Task GetDonorCandidates_Tier456_RequiresCrossDivisionalAlignment_True()
    {
        await using var context = CreateContext();
        var peResource       = MakePeResource("T4A00001", "PE Tier3 Donor");
        var analyticsResource = new Resource
        {
            ResourceId = "T4A00002", Name = "Analytics Tier6 Donor", ResourceType = "Employee",
            Company = "Test Co", Seniority = "Analyst", RoleType = "Developer",
            Division = "Analytics", Country = "UK", MonthlyRate = 10000m,
            Jan=0,Feb=0,Mar=0,Apr=0,May=1,Jun=0,Jul=0,Aug=0,Sep=0,Oct=0,Nov=0,Dec=0
        };
        context.Resources.AddRange(peResource, analyticsResource);
        await context.SaveChangesAsync();
        // Activity 7 = PE project (tier 3); Activity 19 = Analytics project (tier 6)
        context.Mappings.AddRange(
            new Mapping { ResourceId = peResource.Id,       ActivityId = 7,  Month = "2026-05", Percent = 50 },
            new Mapping { ResourceId = analyticsResource.Id, ActivityId = 19, Month = "2026-05", Percent = 50 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(4, null, "2026-05");

        Assert.NotNull(result);
        var peRow       = Assert.Single(result.Candidates, c => c.ResourceName == "PE Tier3 Donor");
        var analyticsRow = Assert.Single(result.Candidates, c => c.ResourceName == "Analytics Tier6 Donor");
        Assert.False(peRow.RequiresCrossDivisionalAlignment);
        Assert.True(analyticsRow.RequiresCrossDivisionalAlignment);
    }

    // Case 6 — Resource already mapped to the target app's Support activity does not appear in any tier
    [Fact]
    public async Task GetDonorCandidates_ResourceOnTargetSupport_ExcludedFromAllTiers()
    {
        await using var context = CreateContext();
        var supporter      = MakePeResource("T6A00001", "Target Supporter");
        var projectDonor   = MakePeResource("T6A00002", "PE Project Donor");
        context.Resources.AddRange(supporter, projectDonor);
        await context.SaveChangesAsync();
        // Activity 5 = AuthService - Support (the target); Activity 7 = PE project
        context.Mappings.AddRange(
            new Mapping { ResourceId = supporter.Id,    ActivityId = 5, Month = "2026-05", Percent = 30 },
            new Mapping { ResourceId = projectDonor.Id, ActivityId = 7, Month = "2026-05", Percent = 40 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(4, null, "2026-05");

        Assert.NotNull(result);
        Assert.DoesNotContain(result.Candidates, c => c.ResourceName == "Target Supporter");
        Assert.Contains(result.Candidates, c => c.ResourceName == "PE Project Donor");
    }

    // Case 5a — Application at exactly recommended_support_fte yields no tier-1 donor
    [Fact]
    public async Task GetDonorCandidates_Tier1_ExactlyAtRecommendedFte_YieldsNoDonor()
    {
        // Custom PE app: recommended = 1.0; 1 resource at 100% → coverage = recommended → surplus = 0
        await using var context = CreateContext();
        context.Applications.Add(new Application { Id = 99, Name = "ExactApp", MinSupportFte = 0.5, RecommendedSupportFte = 1.0 });
        context.Activities.Add(new Activity { Id = 199, Name = "ExactApp - Support", ActivityType = "Support", ApplicationId = 99, Division = "Platform Engineering" });
        await context.SaveChangesAsync();

        var resource = MakePeResource("T5A00001", "Exactly Recommended");
        context.Resources.Add(resource);
        await context.SaveChangesAsync();
        context.Mappings.Add(new Mapping { ResourceId = resource.Id, ActivityId = 199, Month = "2026-05", Percent = 100 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(4, null, "2026-05");

        Assert.NotNull(result);
        Assert.DoesNotContain(result.Candidates, c => c.ResourceName == "Exactly Recommended");
    }

    // Case 5b — Application one percent-point above recommended yields a tier-1 donor with surplus FTE only
    [Fact]
    public async Task GetDonorCandidates_Tier1_OnePercentAboveRecommended_YieldsSurplusFteOnly()
    {
        // Custom PE app: recommended = 0.50; resource at 51% → coverage = 0.51 → surplus = 0.01
        // donatable FTE = 0.51 * (0.01 / 0.51) = 0.01
        await using var context = CreateContext();
        context.Applications.Add(new Application { Id = 100, Name = "SlightSurplusApp", MinSupportFte = 0.3, RecommendedSupportFte = 0.5 });
        context.Activities.Add(new Activity { Id = 200, Name = "SlightSurplusApp - Support", ActivityType = "Support", ApplicationId = 100, Division = "Platform Engineering" });
        await context.SaveChangesAsync();

        var resource = MakePeResource("T5B00001", "OneAbove Resource");
        context.Resources.Add(resource);
        await context.SaveChangesAsync();
        context.Mappings.Add(new Mapping { ResourceId = resource.Id, ActivityId = 200, Month = "2026-05", Percent = 51 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(4, null, "2026-05");

        Assert.NotNull(result);
        var row = Assert.Single(result.Candidates, c => c.ResourceName == "OneAbove Resource");
        Assert.Equal(0.01, row.AllocationFte, precision: 5);
        Assert.False(row.RequiresCrossDivisionalAlignment);
    }

    // Case 1 — Tier 1 candidate appears before tier 2 candidate
    [Fact]
    public async Task GetDonorCandidates_Tier1_AppearsBefore_Tier2()
    {
        await using var context = CreateContext();
        context.Applications.Add(new Application { Id = 100, Name = "SurplusApp", MinSupportFte = 0.3, RecommendedSupportFte = 0.5 });
        context.Activities.Add(new Activity { Id = 200, Name = "SurplusApp - Support", ActivityType = "Support", ApplicationId = 100, Division = "Platform Engineering" });
        await context.SaveChangesAsync();

        var tier1Resource = MakePeResource("T1B00001", "Tier1 Donor");
        var tier2Resource = MakePeResource("T1B00002", "Tier2 Donor");
        context.Resources.AddRange(tier1Resource, tier2Resource);
        await context.SaveChangesAsync();
        context.Mappings.AddRange(
            new Mapping { ResourceId = tier1Resource.Id, ActivityId = 200, Month = "2026-05", Percent = 51 }, // surplus support
            new Mapping { ResourceId = tier2Resource.Id, ActivityId = 35,  Month = "2026-05", Percent = 25 }); // Minor Changes
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(4, null, "2026-05");

        Assert.NotNull(result);
        var tier1Idx = result.Candidates.FindIndex(c => c.ResourceName == "Tier1 Donor");
        var tier2Idx = result.Candidates.FindIndex(c => c.ResourceName == "Tier2 Donor");
        Assert.True(tier1Idx >= 0 && tier2Idx >= 0, "Both candidates must appear");
        Assert.True(tier1Idx < tier2Idx, "Tier-1 must appear before tier-2");
    }

    // Case 2 — Tier 2 candidate appears before tier 3 candidate
    [Fact]
    public async Task GetDonorCandidates_Tier2_AppearsBefore_Tier3()
    {
        await using var context = CreateContext();
        var tier2Resource = MakePeResource("T2B00001", "Tier2 MinorChg");
        var tier3Resource = MakePeResource("T2B00002", "Tier3 Project");
        context.Resources.AddRange(tier2Resource, tier3Resource);
        await context.SaveChangesAsync();
        // Activity 35 = SurveyEngine - Minor Changes (PE); Activity 7 = PE project
        context.Mappings.AddRange(
            new Mapping { ResourceId = tier2Resource.Id, ActivityId = 35, Month = "2026-05", Percent = 25 },
            new Mapping { ResourceId = tier3Resource.Id, ActivityId = 7,  Month = "2026-05", Percent = 30 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(4, null, "2026-05");

        Assert.NotNull(result);
        var tier2Idx = result.Candidates.FindIndex(c => c.ResourceName == "Tier2 MinorChg");
        var tier3Idx = result.Candidates.FindIndex(c => c.ResourceName == "Tier3 Project");
        Assert.True(tier2Idx >= 0 && tier3Idx >= 0, "Both candidates must appear");
        Assert.True(tier2Idx < tier3Idx, "Tier-2 must appear before tier-3");
    }

    // Case 3 — Tiers 1–3 all appear before tiers 4–6
    [Fact]
    public async Task GetDonorCandidates_Tiers123_AppearBefore_Tiers456()
    {
        // Same-div tier-3 (PE Project) must appear before cross-div tier-6 (Analytics Project)
        await using var context = CreateContext();
        var peResource = MakePeResource("T3B00001", "PE Project Tier3");
        var analyticsResource = new Resource
        {
            ResourceId = "T3B00002", Name = "Analytics Project Tier6", ResourceType = "Employee",
            Company = "Test Co", Seniority = "Analyst", RoleType = "Developer",
            Division = "Analytics", Country = "UK", MonthlyRate = 10000m,
            Jan=0,Feb=0,Mar=0,Apr=0,May=1,Jun=0,Jul=0,Aug=0,Sep=0,Oct=0,Nov=0,Dec=0
        };
        context.Resources.AddRange(peResource, analyticsResource);
        await context.SaveChangesAsync();
        context.Mappings.AddRange(
            new Mapping { ResourceId = peResource.Id,        ActivityId = 7,  Month = "2026-05", Percent = 40 },
            new Mapping { ResourceId = analyticsResource.Id, ActivityId = 19, Month = "2026-05", Percent = 40 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(4, null, "2026-05");

        Assert.NotNull(result);
        var sameDivIdx  = result.Candidates.FindIndex(c => c.ResourceName == "PE Project Tier3");
        var crossDivIdx = result.Candidates.FindIndex(c => c.ResourceName == "Analytics Project Tier6");
        Assert.True(sameDivIdx >= 0 && crossDivIdx >= 0);
        Assert.True(sameDivIdx < crossDivIdx, "Same-div (tier 3) must appear before cross-div (tier 6)");
    }

    // Case 7 — CumulativeFte accumulates correctly across a tier boundary
    [Fact]
    public async Task GetDonorCandidates_CumulativeFte_AccumulatesAcrossTierBoundary()
    {
        // Tier-1 surplus support: donatable = 0.01 FTE
        // Tier-2 Minor Changes: 0.25 FTE
        // CumulativeFte at tier-2 row = 0.01 + 0.25 = 0.26
        await using var context = CreateContext();
        context.Applications.Add(new Application { Id = 100, Name = "SurplusApp", MinSupportFte = 0.3, RecommendedSupportFte = 0.5 });
        context.Activities.Add(new Activity { Id = 200, Name = "SurplusApp - Support", ActivityType = "Support", ApplicationId = 100, Division = "Platform Engineering" });
        await context.SaveChangesAsync();

        var tier1Resource = MakePeResource("T7A00001", "Tier1 Cumul");
        var tier2Resource = MakePeResource("T7A00002", "Tier2 Cumul");
        context.Resources.AddRange(tier1Resource, tier2Resource);
        await context.SaveChangesAsync();
        context.Mappings.AddRange(
            new Mapping { ResourceId = tier1Resource.Id, ActivityId = 200, Month = "2026-05", Percent = 51 }, // donatable = 0.01
            new Mapping { ResourceId = tier2Resource.Id, ActivityId = 35,  Month = "2026-05", Percent = 25 }); // 0.25 FTE
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(4, null, "2026-05");

        Assert.NotNull(result);
        var tier1Row = Assert.Single(result.Candidates, c => c.ResourceName == "Tier1 Cumul");
        var tier2Row = Assert.Single(result.Candidates, c => c.ResourceName == "Tier2 Cumul");
        var tier1Idx = result.Candidates.IndexOf(tier1Row);
        var tier2Idx = result.Candidates.IndexOf(tier2Row);
        Assert.True(tier1Idx < tier2Idx, "Tier-1 must precede tier-2 in the list");
        Assert.Equal(0.01, tier1Row.AllocationFte, precision: 5);
        Assert.Equal(0.01 + 0.25, tier2Row.CumulativeFte, precision: 5);
    }

    // Case 8 — SurplusSupportFteAvailable equals the sum of surplus FTE across same-division applications
    [Fact]
    public async Task GetDonorCandidates_SurplusSupportFteAvailable_SumOfSameDivisionSurplus()
    {
        // ObservabilityStack (app 5, PE, recommended=2.0): 3 resources at 100% → coverage=3.0 → surplus=1.0
        // APIGateway (app 2, PE, recommended=2.5): 3 resources at 100% → coverage=3.0 → surplus=0.5
        // All other PE apps: no seeded mappings → coverage=0 → surplus=0
        // SurplusSupportFteAvailable = 1.0 + 0.5 = 1.5
        await using var context = CreateContext();

        var obsResources = Enumerable.Range(1, 3).Select(i => new Resource
        {
            ResourceId = $"T8A{i:00000}", Name = $"ObsStack Res {i}", ResourceType = "Employee",
            Company = "Test Co", Seniority = "Analyst", RoleType = "Developer",
            Division = "Platform Engineering", Country = "UK", MonthlyRate = 10000m,
            Jan=0,Feb=0,Mar=0,Apr=0,May=1,Jun=0,Jul=0,Aug=0,Sep=0,Oct=0,Nov=0,Dec=0
        }).ToList();
        var apiResources = Enumerable.Range(1, 3).Select(i => new Resource
        {
            ResourceId = $"T8B{i:00000}", Name = $"APIGateway Res {i}", ResourceType = "Employee",
            Company = "Test Co", Seniority = "Analyst", RoleType = "Developer",
            Division = "Platform Engineering", Country = "UK", MonthlyRate = 10000m,
            Jan=0,Feb=0,Mar=0,Apr=0,May=1,Jun=0,Jul=0,Aug=0,Sep=0,Oct=0,Nov=0,Dec=0
        }).ToList();
        context.Resources.AddRange(obsResources);
        context.Resources.AddRange(apiResources);
        await context.SaveChangesAsync();
        // Activity 6 = ObservabilityStack - Support; Activity 3 = APIGateway - Support
        foreach (var r in obsResources)
            context.Mappings.Add(new Mapping { ResourceId = r.Id, ActivityId = 6, Month = "2026-05", Percent = 100 });
        foreach (var r in apiResources)
            context.Mappings.Add(new Mapping { ResourceId = r.Id, ActivityId = 3, Month = "2026-05", Percent = 100 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(4, null, "2026-05");

        Assert.NotNull(result);
        Assert.Equal(1.5, result.SurplusSupportFteAvailable, precision: 5);
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

    // ── Issue #31: largest-remainders rounding for DonablePercent ────────────

    // Cycle 3 — LR tie-break: equal fractional parts → larger AllocationFte gets the extra pp
    [Fact]
    public async Task GetDonorCandidates_LR_TieBreakByAllocationFte()
    {
        // gap = 1.5 FTE; 3 donors at 80%, 50%, 30% (tier-3, PE project)
        // donatable_pct unrounded: 53.33, 33.33, 20.00
        // D(80%) and D(50%) both have frac 0.33; deficit = 1 (only one gets the extra pp)
        // D(80%) has larger AllocationFte → must win the tie-break and get 54, not 53
        await using var context = CreateContext();
        context.Applications.Add(new Application { Id = 103, Name = "TieBreakApp", MinSupportFte = 1.5, RecommendedSupportFte = 0.5 });
        context.Activities.Add(new Activity { Id = 203, Name = "TieBreakApp - Support", ActivityType = "Support", ApplicationId = 103, Division = "Platform Engineering" });
        await context.SaveChangesAsync();

        var d80 = MakePeResource("LR00020", "TB Donor 80");
        var d50 = MakePeResource("LR00021", "TB Donor 50");
        var d30 = MakePeResource("LR00022", "TB Donor 30");
        context.Resources.AddRange(d80, d50, d30);
        await context.SaveChangesAsync();
        // Activity 7 = PE project (tier-3)
        context.Mappings.AddRange(
            new Mapping { ResourceId = d80.Id, ActivityId = 7, Month = "2026-05", Percent = 80 },
            new Mapping { ResourceId = d50.Id, ActivityId = 7, Month = "2026-05", Percent = 50 },
            new Mapping { ResourceId = d30.Id, ActivityId = 7, Month = "2026-05", Percent = 30 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(103, null, "2026-05");

        Assert.NotNull(result);
        var comboRows = result.Candidates.Where(c => c.InFewestChangesCombination).ToList();
        Assert.Equal(3, comboRows.Count);
        var row80 = comboRows.Single(c => c.ResourceName == "TB Donor 80");
        var row50 = comboRows.Single(c => c.ResourceName == "TB Donor 50");
        var row30 = comboRows.Single(c => c.ResourceName == "TB Donor 30");
        // deficit = round(106.67) - (53+33+20) = 107 - 106 = 1
        // D80 and D50 tie on frac 0.33; D80 wins (larger AllocationFte) → 54
        Assert.Equal(54, row80.DonablePercent);
        Assert.Equal(33, row50.DonablePercent);
        Assert.Equal(20, row30.DonablePercent);
    }

    // Cycle 4 — when gap cannot be closed, no InFewestChangesCombination rows; independent rounding unchanged
    [Fact]
    public async Task GetDonorCandidates_LR_NoopWhenGapCannotBeClosed()
    {
        // gap = 4.0 FTE (AuthService); only 1 small donor at 30% → cumulative = 7.5%, never reaches 100%
        // No fewest-changes set → LR pass must not run; DonablePercent is unchanged from independent rounding
        await using var context = CreateContext();
        var donor = MakePeResource("LR00030", "Tiny Donor");
        context.Resources.Add(donor);
        await context.SaveChangesAsync();
        context.Mappings.Add(new Mapping { ResourceId = donor.Id, ActivityId = 7, Month = "2026-05", Percent = 30 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(4, null, "2026-05"); // AuthService, gap = 4.0

        Assert.NotNull(result);
        Assert.Null(result.FewestChangesCombination);
        Assert.All(result.Candidates, c => Assert.False(c.InFewestChangesCombination));
        // Independent rounding: DonablePercent = m.Percent = 30 for a tier-3 donor
        var row = Assert.Single(result.Candidates, c => c.ResourceName == "Tiny Donor");
        Assert.Equal(30, row.DonablePercent);
    }

    // Cycle 2 — no row's DonablePercent may exceed its source mapping Percent
    [Fact]
    public async Task GetDonorCandidates_LR_CapRespected()
    {
        // gap = 0.5 FTE; 1 donor at 60% → GapCovPct = 120%.
        // Without the cap, LR would produce DonablePercent = 120; cap must hold it at 60.
        await using var context = CreateContext();
        context.Applications.Add(new Application { Id = 102, Name = "CapTestApp", MinSupportFte = 0.5, RecommendedSupportFte = 0.2 });
        context.Activities.Add(new Activity { Id = 202, Name = "CapTestApp - Support", ActivityType = "Support", ApplicationId = 102, Division = "Platform Engineering" });
        await context.SaveChangesAsync();

        var donor = MakePeResource("LR00010", "Cap Donor");
        context.Resources.Add(donor);
        await context.SaveChangesAsync();
        context.Mappings.Add(new Mapping { ResourceId = donor.Id, ActivityId = 35, Month = "2026-05", Percent = 60 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(102, null, "2026-05");

        Assert.NotNull(result);
        var comboRows = result.Candidates.Where(c => c.InFewestChangesCombination).ToList();
        Assert.All(comboRows, row => Assert.True(row.DonablePercent <= row.Percent,
            $"DonablePercent {row.DonablePercent} exceeds source Percent {row.Percent}"));
    }

    // Cycle 1 (tracer) — sum(DonablePercent for combo rows) == round(GapCoverageReachedPct)
    [Fact]
    public async Task GetDonorCandidates_LR_SumMatchesHeadline()
    {
        // gap = 1.5 FTE; 2 donors at 100% Minor Changes → cumulative = 133.3%
        // Independent rounding gives DonablePercent = 100 + 100 = 200, not 133.
        // Largest-remainders must produce sum == round(133.3) = 133.
        await using var context = CreateContext();
        context.Applications.Add(new Application { Id = 101, Name = "LRTestApp", MinSupportFte = 1.5, RecommendedSupportFte = 0.5 });
        context.Activities.Add(new Activity { Id = 201, Name = "LRTestApp - Support", ActivityType = "Support", ApplicationId = 101, Division = "Platform Engineering" });
        await context.SaveChangesAsync();

        var donorA = MakePeResource("LR00001", "LR Donor A");
        var donorB = MakePeResource("LR00002", "LR Donor B");
        context.Resources.AddRange(donorA, donorB);
        await context.SaveChangesAsync();
        // Activity 35 = SurveyEngine - Minor Changes (PE, tier-2)
        context.Mappings.AddRange(
            new Mapping { ResourceId = donorA.Id, ActivityId = 35, Month = "2026-05", Percent = 100 },
            new Mapping { ResourceId = donorB.Id, ActivityId = 35, Month = "2026-05", Percent = 100 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(101, null, "2026-05");

        Assert.NotNull(result);
        var comboRows = result.Candidates.Where(c => c.InFewestChangesCombination).ToList();
        Assert.Equal(2, comboRows.Count);
        var expectedTarget = (int)Math.Round(result.FewestChangesCombination!.GapCoverageReachedPct);
        Assert.Equal(expectedTarget, comboRows.Sum(c => c.DonablePercent));
    }

    // ── Issue #32: trim get_donor_candidates response ────────────────────────

    // Cycle 2 — TotalAvailableCandidatesCount reflects pre-trim count
    [Fact]
    public async Task GetDonorCandidates_TotalAvailableCandidatesCount_ReflectsPreTrimCount()
    {
        // App with gap 1.0 FTE; 2 donors close the gap (combo = 2 rows); 6 extra donors fill the rest.
        // Total = 8 candidates. Trimmed = 2 (combo) + 5 (buffer) = 7.
        // TotalAvailableCandidatesCount must == 8 (the pre-trim count).
        await using var context = CreateContext();
        context.Applications.Add(new Application { Id = 110, Name = "TrimApp", MinSupportFte = 1.0, RecommendedSupportFte = 0.5 });
        await context.SaveChangesAsync();

        // 2 combo donors (60% each → cumulative hits 120% of 1.0 FTE gap)
        var combo1 = MakePeResource("TR32B001", "Combo1");
        var combo2 = MakePeResource("TR32B002", "Combo2");
        // 6 buffer/extra donors at 10% each
        var extras = Enumerable.Range(3, 6).Select(i => MakePeResource($"TR32B{i:000}", $"Extra{i}")).ToList();
        context.Resources.Add(combo1);
        context.Resources.Add(combo2);
        context.Resources.AddRange(extras);
        await context.SaveChangesAsync();

        // Activity 7 = PE project (tier-3)
        context.Mappings.Add(new Mapping { ResourceId = combo1.Id, ActivityId = 7, Month = "2026-05", Percent = 60 });
        context.Mappings.Add(new Mapping { ResourceId = combo2.Id, ActivityId = 7, Month = "2026-05", Percent = 60 });
        foreach (var e in extras)
            context.Mappings.Add(new Mapping { ResourceId = e.Id, ActivityId = 7, Month = "2026-05", Percent = 10 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(110, null, "2026-05");

        Assert.NotNull(result);
        Assert.NotNull(result.FewestChangesCombination);
        Assert.Equal(8, result.TotalAvailableCandidatesCount);
        Assert.True(result.Candidates.Count < result.TotalAvailableCandidatesCount,
            "Trimmed list should be shorter than total available");
    }

    // Cycle 3 — trimmed list is exactly combo_count + DonorCandidateBufferRows (5)
    [Fact]
    public async Task GetDonorCandidates_Trimming_CandidatesCount_EqualsComboCountPlusBuffer()
    {
        // Same seed as cycle 2: 2 combo rows + 6 extras = 8 total.
        // After trim: 2 + 5 = 7.
        await using var context = CreateContext();
        context.Applications.Add(new Application { Id = 111, Name = "TrimCountApp", MinSupportFte = 1.0, RecommendedSupportFte = 0.5 });
        await context.SaveChangesAsync();

        var combo1 = MakePeResource("TR32C001", "Count Combo1");
        var combo2 = MakePeResource("TR32C002", "Count Combo2");
        var extras = Enumerable.Range(3, 6).Select(i => MakePeResource($"TR32C{i:000}", $"Count Extra{i}")).ToList();
        context.Resources.Add(combo1);
        context.Resources.Add(combo2);
        context.Resources.AddRange(extras);
        await context.SaveChangesAsync();

        context.Mappings.Add(new Mapping { ResourceId = combo1.Id, ActivityId = 7, Month = "2026-05", Percent = 60 });
        context.Mappings.Add(new Mapping { ResourceId = combo2.Id, ActivityId = 7, Month = "2026-05", Percent = 60 });
        foreach (var e in extras)
            context.Mappings.Add(new Mapping { ResourceId = e.Id, ActivityId = 7, Month = "2026-05", Percent = 10 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(111, null, "2026-05");

        Assert.NotNull(result);
        Assert.NotNull(result.FewestChangesCombination);
        var comboCount = result.FewestChangesCombination.RowCount;
        Assert.Equal(comboCount + 5, result.Candidates.Count);
    }

    // Cycle 4 — every InFewestChangesCombination row survives trimming
    [Fact]
    public async Task GetDonorCandidates_Trimming_AllFewestChangesRowsPreserved()
    {
        // 2 combo rows close the gap; 6 extras are trimmed at the buffer boundary.
        // All rows with InFewestChangesCombination == true must still appear.
        await using var context = CreateContext();
        context.Applications.Add(new Application { Id = 112, Name = "TrimPreserveApp", MinSupportFte = 1.0, RecommendedSupportFte = 0.5 });
        await context.SaveChangesAsync();

        var combo1 = MakePeResource("TR32D001", "Preserve Combo1");
        var combo2 = MakePeResource("TR32D002", "Preserve Combo2");
        var extras = Enumerable.Range(3, 6).Select(i => MakePeResource($"TR32D{i:000}", $"Preserve Extra{i}")).ToList();
        context.Resources.Add(combo1);
        context.Resources.Add(combo2);
        context.Resources.AddRange(extras);
        await context.SaveChangesAsync();

        context.Mappings.Add(new Mapping { ResourceId = combo1.Id, ActivityId = 7, Month = "2026-05", Percent = 60 });
        context.Mappings.Add(new Mapping { ResourceId = combo2.Id, ActivityId = 7, Month = "2026-05", Percent = 60 });
        foreach (var e in extras)
            context.Mappings.Add(new Mapping { ResourceId = e.Id, ActivityId = 7, Month = "2026-05", Percent = 10 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(112, null, "2026-05");

        Assert.NotNull(result);
        Assert.NotNull(result.FewestChangesCombination);
        var comboRows = result.Candidates.Where(c => c.InFewestChangesCombination).ToList();
        Assert.Equal(result.FewestChangesCombination.RowCount, comboRows.Count);
        Assert.Contains(result.Candidates, c => c.ResourceName == "Preserve Combo1" && c.InFewestChangesCombination);
        Assert.Contains(result.Candidates, c => c.ResourceName == "Preserve Combo2" && c.InFewestChangesCombination);
    }

    // Cycle 5 — no fewest-changes path: returns top DonorCandidateBufferRows * 2 (10) rows
    [Fact]
    public async Task GetDonorCandidates_NoFewestChanges_ReturnTopBufferTimesTwo()
    {
        // AuthService gap = 4.0 FTE; 12 donors at 10% each → cumulative never reaches 100%.
        // No fewest-changes combo → limit = DonorCandidateBufferRows * 2 = 10.
        // candidates.Count must be 10, TotalAvailableCandidatesCount must be 12.
        // No row has InFewestChangesCombination == true.
        await using var context = CreateContext();
        var donors = Enumerable.Range(1, 12).Select(i => MakePeResource($"TR32E{i:000}", $"NFC Donor {i}")).ToList();
        context.Resources.AddRange(donors);
        await context.SaveChangesAsync();
        foreach (var d in donors)
            context.Mappings.Add(new Mapping { ResourceId = d.Id, ActivityId = 7, Month = "2026-05", Percent = 10 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(4, null, "2026-05"); // AuthService

        Assert.NotNull(result);
        Assert.Null(result.FewestChangesCombination);
        Assert.All(result.Candidates, c => Assert.False(c.InFewestChangesCombination));
        Assert.Equal(10, result.Candidates.Count);
        Assert.Equal(12, result.TotalAvailableCandidatesCount);
    }

    // Cycle 1 (tracer) — small pool below buffer threshold: no trimming occurs
    [Fact]
    public async Task GetDonorCandidates_SmallPool_NoTrimming_CountMatchesTotal()
    {
        // AuthService gap = 4.0 FTE; 3 donors at 10% each → cumulative = 7.5%, never closes.
        // Total = 3 < DonorCandidateBufferRows * 2 (10) → no trimming.
        // candidates.Count must equal TotalAvailableCandidatesCount.
        await using var context = CreateContext();
        var donors = Enumerable.Range(1, 3).Select(i => MakePeResource($"TR32A{i:000}", $"Small Donor {i}")).ToList();
        context.Resources.AddRange(donors);
        await context.SaveChangesAsync();
        foreach (var d in donors)
            context.Mappings.Add(new Mapping { ResourceId = d.Id, ActivityId = 7, Month = "2026-05", Percent = 10 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(4, null, "2026-05");

        Assert.NotNull(result);
        Assert.Equal(result.TotalAvailableCandidatesCount, result.Candidates.Count);
        Assert.Equal(3, result.TotalAvailableCandidatesCount);
    }

    // ── Issue #33: pre-rendered MappingChanges list ──────────────────────────

    // Cycle 1 (tracer) — MappingChanges count == FewestChangesCombination.RowCount
    [Fact]
    public async Task GetDonorCandidates_MappingChanges_CountMatchesRowCount()
    {
        // 2 donors at 100% Minor Changes close a 1.5 FTE gap → combo of 2
        await using var context = CreateContext();
        context.Applications.Add(new Application { Id = 101, Name = "LRTestApp", MinSupportFte = 1.5, RecommendedSupportFte = 0.5 });
        context.Activities.Add(new Activity { Id = 201, Name = "LRTestApp - Support", ActivityType = "Support", ApplicationId = 101, Division = "Platform Engineering" });
        await context.SaveChangesAsync();

        var donorA = MakePeResource("MC33A001", "MC Donor A");
        var donorB = MakePeResource("MC33A002", "MC Donor B");
        context.Resources.AddRange(donorA, donorB);
        await context.SaveChangesAsync();
        context.Mappings.AddRange(
            new Mapping { ResourceId = donorA.Id, ActivityId = 35, Month = "2026-05", Percent = 100 },
            new Mapping { ResourceId = donorB.Id, ActivityId = 35, Month = "2026-05", Percent = 100 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(101, null, "2026-05");

        Assert.NotNull(result);
        Assert.NotNull(result.FewestChangesCombination);
        Assert.Equal(result.FewestChangesCombination.RowCount,
                     result.FewestChangesCombination.MappingChanges.Count);
    }

    // Cycle 2 — MappingChanges[i] contains the ResourceName of the i-th combo candidate
    [Fact]
    public async Task GetDonorCandidates_MappingChanges_OrderMatchesCandidateOrder()
    {
        // 2 donors at different percents — the larger-FTE one sorts first in the combo
        // MappingChanges[0] must name the first combo candidate, MappingChanges[1] the second
        await using var context = CreateContext();
        context.Applications.Add(new Application { Id = 101, Name = "LRTestApp", MinSupportFte = 1.5, RecommendedSupportFte = 0.5 });
        context.Activities.Add(new Activity { Id = 201, Name = "LRTestApp - Support", ActivityType = "Support", ApplicationId = 101, Division = "Platform Engineering" });
        await context.SaveChangesAsync();

        var donorA = MakePeResource("MC33B001", "Order Donor A");
        var donorB = MakePeResource("MC33B002", "Order Donor B");
        context.Resources.AddRange(donorA, donorB);
        await context.SaveChangesAsync();
        // Activity 35 = SurveyEngine - Minor Changes (PE, tier-2); donorA at 100% sorts first (larger FTE)
        context.Mappings.AddRange(
            new Mapping { ResourceId = donorA.Id, ActivityId = 35, Month = "2026-05", Percent = 100 },
            new Mapping { ResourceId = donorB.Id, ActivityId = 35, Month = "2026-05", Percent = 80 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(101, null, "2026-05");

        Assert.NotNull(result);
        Assert.NotNull(result.FewestChangesCombination);
        var comboRows = result.Candidates.Where(c => c.InFewestChangesCombination).ToList();
        Assert.Equal(2, comboRows.Count);
        for (var i = 0; i < comboRows.Count; i++)
            Assert.Contains(comboRows[i].ResourceName, result.FewestChangesCombination.MappingChanges[i]);
    }

    // Cycle 3 — Tier 1 sentence contains "reduce" and correct arithmetic
    [Fact]
    public async Task GetDonorCandidates_MappingChanges_Tier1_HasReduceClause()
    {
        // 4 PE resources each at 100% on BigSurplusApp (recommended=0) → surplus=4.0 FTE
        // each donates 1.0 FTE → closes AuthService 4.0 FTE gap exactly (4 combo rows)
        // LR: target=100, 4 rows at 25 each → DonablePercent=25
        // Expected: each sentence contains "reduce", "100%", "75%" (100-25=75)
        await using var context = CreateContext();
        context.Applications.Add(new Application { Id = 100, Name = "BigSurplusApp", MinSupportFte = 0.0, RecommendedSupportFte = 0.0 });
        context.Activities.Add(new Activity { Id = 200, Name = "BigSurplusApp - Support", ActivityType = "Support", ApplicationId = 100, Division = "Platform Engineering" });
        await context.SaveChangesAsync();

        var donors = Enumerable.Range(1, 4).Select(i => MakePeResource($"MC33C{i:000}", $"T1R Donor {i}")).ToList();
        context.Resources.AddRange(donors);
        await context.SaveChangesAsync();
        foreach (var d in donors)
            context.Mappings.Add(new Mapping { ResourceId = d.Id, ActivityId = 200, Month = "2026-05", Percent = 100 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        // AuthService (Id=4) gap = 4.0 FTE; 4 donors × 1.0 FTE closes it exactly
        var result = await svc.GetDonorCandidatesAsync(4, null, "2026-05");

        Assert.NotNull(result);
        Assert.NotNull(result.FewestChangesCombination);
        Assert.All(result.FewestChangesCombination.MappingChanges, s =>
        {
            Assert.Contains("reduce", s);
            Assert.Contains("100%", s);
            Assert.Contains("75%", s); // 100 - 25 = 75
        });
    }

    // Cycle 4 — Tier 2 sentence contains "remove" and source activity name
    [Fact]
    public async Task GetDonorCandidates_MappingChanges_Tier2_HasRemoveClause()
    {
        // 2 PE donors at 100% Minor Changes (SurveyEngine - Minor Changes, activity 35)
        // close a 1.5 FTE gap → LR → DonablePercent in each sentence
        // Expected sentence: "... (remove SurveyEngine - Minor Changes mapping, currently 100%)"
        await using var context = CreateContext();
        context.Applications.Add(new Application { Id = 101, Name = "LRTestApp", MinSupportFte = 1.5, RecommendedSupportFte = 0.5 });
        context.Activities.Add(new Activity { Id = 201, Name = "LRTestApp - Support", ActivityType = "Support", ApplicationId = 101, Division = "Platform Engineering" });
        await context.SaveChangesAsync();

        var donorA = MakePeResource("MC33D001", "T2 Donor A");
        var donorB = MakePeResource("MC33D002", "T2 Donor B");
        context.Resources.AddRange(donorA, donorB);
        await context.SaveChangesAsync();
        context.Mappings.AddRange(
            new Mapping { ResourceId = donorA.Id, ActivityId = 35, Month = "2026-05", Percent = 100 },
            new Mapping { ResourceId = donorB.Id, ActivityId = 35, Month = "2026-05", Percent = 100 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(101, null, "2026-05");

        Assert.NotNull(result);
        Assert.NotNull(result.FewestChangesCombination);
        Assert.All(result.FewestChangesCombination.MappingChanges, s =>
        {
            Assert.Contains("remove", s);
            Assert.Contains("SurveyEngine - Minor Changes", s);
            Assert.Contains("100%", s);
        });
    }

    // Cycle 5 — Cross-divisional rows have sign-off suffix naming both divisions
    [Fact]
    public async Task GetDonorCandidates_MappingChanges_CrossDiv_HasSignOffSuffix()
    {
        // Analytics project donor (tier-6) closes a tiny gap on a PE app with no same-div candidates
        // Expected suffix: "— requires cross-divisional sign-off (Analytics → Platform Engineering)"
        await using var context = CreateContext();
        context.Applications.Add(new Application { Id = 101, Name = "LRTestApp", MinSupportFte = 1.5, RecommendedSupportFte = 0.5 });
        context.Activities.Add(new Activity { Id = 201, Name = "LRTestApp - Support", ActivityType = "Support", ApplicationId = 101, Division = "Platform Engineering" });
        await context.SaveChangesAsync();

        // Analytics resource on an Analytics project activity (tier-6 cross-div to PE target)
        // Activity 19 = "P07 SegmentBuilder Enhancement - Project" (Analytics division)
        var analyticsResource = new Resource
        {
            ResourceId = "MC33E001", Name = "Analytics XDiv", ResourceType = "Employee",
            Company = "Test Co", Seniority = "Analyst", RoleType = "Developer",
            Division = "Analytics", Country = "UK", MonthlyRate = 10000m,
            Jan=0,Feb=0,Mar=0,Apr=0,May=1,Jun=0,Jul=0,Aug=0,Sep=0,Oct=0,Nov=0,Dec=0
        };
        context.Resources.Add(analyticsResource);
        await context.SaveChangesAsync();
        // 2 donors at 100% each to close 1.5 FTE gap
        var extraResource = new Resource
        {
            ResourceId = "MC33E002", Name = "Analytics XDiv2", ResourceType = "Employee",
            Company = "Test Co", Seniority = "Analyst", RoleType = "Developer",
            Division = "Analytics", Country = "UK", MonthlyRate = 10000m,
            Jan=0,Feb=0,Mar=0,Apr=0,May=1,Jun=0,Jul=0,Aug=0,Sep=0,Oct=0,Nov=0,Dec=0
        };
        context.Resources.Add(extraResource);
        await context.SaveChangesAsync();
        context.Mappings.AddRange(
            new Mapping { ResourceId = analyticsResource.Id, ActivityId = 19, Month = "2026-05", Percent = 100 },
            new Mapping { ResourceId = extraResource.Id,     ActivityId = 19, Month = "2026-05", Percent = 100 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(101, null, "2026-05");

        Assert.NotNull(result);
        Assert.NotNull(result.FewestChangesCombination);
        Assert.All(result.FewestChangesCombination.MappingChanges, s =>
        {
            Assert.Contains("requires cross-divisional sign-off", s);
            Assert.Contains("Analytics", s);
            Assert.Contains("Platform Engineering", s);
        });
    }

    // Cycle 6 — No decimal points adjacent to % tokens in any sentence
    [Fact]
    public async Task GetDonorCandidates_MappingChanges_PercentTokensAreIntegerOnly()
    {
        // Reuse the 2-donor Minor Changes setup; both DonablePercent values are integers
        await using var context = CreateContext();
        context.Applications.Add(new Application { Id = 101, Name = "LRTestApp", MinSupportFte = 1.5, RecommendedSupportFte = 0.5 });
        context.Activities.Add(new Activity { Id = 201, Name = "LRTestApp - Support", ActivityType = "Support", ApplicationId = 101, Division = "Platform Engineering" });
        await context.SaveChangesAsync();

        var donorA = MakePeResource("MC33F001", "Int Donor A");
        var donorB = MakePeResource("MC33F002", "Int Donor B");
        context.Resources.AddRange(donorA, donorB);
        await context.SaveChangesAsync();
        context.Mappings.AddRange(
            new Mapping { ResourceId = donorA.Id, ActivityId = 35, Month = "2026-05", Percent = 100 },
            new Mapping { ResourceId = donorB.Id, ActivityId = 35, Month = "2026-05", Percent = 100 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(101, null, "2026-05");

        Assert.NotNull(result);
        Assert.NotNull(result.FewestChangesCombination);
        // No sentence should contain a decimal point immediately before %
        Assert.All(result.FewestChangesCombination.MappingChanges,
            s => Assert.DoesNotMatch(@"\d+\.\d+%", s));
    }
}
