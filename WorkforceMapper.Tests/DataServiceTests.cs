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

    // --- Cycle 11: inactive resource excluded from product cost (#40) ---

    [Fact]
    public async Task GetCostByProduct_InactiveResource_ExcludedFromProductCost()
    {
        await using var context = CreateContext();

        var resource = new Resource
        {
            ResourceId = "IN00001", Name = "Inactive Resource", ResourceType = "Employee",
            Company = "Test Co", Seniority = "Analyst", RoleType = "Developer",
            Division = "Platform Engineering", Country = "UK", MonthlyRate = 10000m,
            Jan = 1, Feb = 1, Mar = 1, Apr = 1, May = 0,
            Jun = 1, Jul = 1, Aug = 1, Sep = 1, Oct = 1, Nov = 1, Dec = 1
        };
        context.Resources.Add(resource);
        await context.SaveChangesAsync();

        // Activity 1 = CoreDataWarehouse - Support (seeded); app 1 IS linked to products
        context.Mappings.Add(new Mapping { ResourceId = resource.Id, ActivityId = 1, Month = "2026-05", Percent = 100 });
        await context.SaveChangesAsync();

        var service = new DataService(context);
        var summary = await service.GetCostByProductAsync("2026-05");

        Assert.Equal(0m, summary.TotalMappedCost);
        Assert.Empty(summary.Products);
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

    // When gap is not closeable all donors are pipeline candidates (no combo rows)
    [Fact]
    public async Task GetDonorCandidates_WhenGapNotCloseable_AllCandidatesArePipeline()
    {
        await using var context = CreateContext();
        // Two donors: one with two separate project allocations (two rows), one with one
        var donorA = MakePeResource("DC00006c", "Alice Two");
        var donorB = MakePeResource("DC00006d", "Bob One");
        context.Resources.AddRange(donorA, donorB);
        await context.SaveChangesAsync();
        // Activity 7 and 8 are both PE project activities; Activity 6 = ObsStack Support pads to 100%
        // ObsStack coverage = 0.40 + 0.60 = 1.0 FTE < 2.0 recommended → no surplus row
        context.Mappings.AddRange(
            new Mapping { ResourceId = donorA.Id, ActivityId = 7, Month = "2026-05", Percent = 30 },
            new Mapping { ResourceId = donorA.Id, ActivityId = 8, Month = "2026-05", Percent = 30 },
            new Mapping { ResourceId = donorA.Id, ActivityId = 6, Month = "2026-05", Percent = 40 },
            new Mapping { ResourceId = donorB.Id, ActivityId = 7, Month = "2026-05", Percent = 40 },
            new Mapping { ResourceId = donorB.Id, ActivityId = 6, Month = "2026-05", Percent = 60 }
        );
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(4, null, "2026-05");

        Assert.NotNull(result);
        // AuthService gap = 4.0 FTE; total donor pool = 1.0 FTE → gap not closeable, no combo
        Assert.Null(result.FewestChangesCombination);
        Assert.Empty(result.Candidates);
        Assert.Equal(3, result.TotalAvailableCandidatesCount);
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
        // Custom PE target (min=0.90): PE project (0.50, T4) + Analytics project (0.40, T8) = 0.90 → both in combo.
        // Both padded to 100% → no idle FTE.
        await using var context = CreateContext();
        context.Applications.Add(new Application { Id = 611, Name = "CrossDivProjApp", MinSupportFte = 0.90, RecommendedSupportFte = 0.1 });
        context.Activities.Add(new Activity { Id = 611, Name = "CrossDivProjApp - Support", ActivityType = "Support", ApplicationId = 611, Division = "Platform Engineering" });
        await context.SaveChangesAsync();

        var peResource = MakePeResource("DC00004a", "PE Tier4 Donor");
        var analyticsResource = new Resource
        {
            ResourceId = "DC00004", Name = "Dave Analytics", ResourceType = "Employee",
            Company = "Test Co", Seniority = "Analyst", RoleType = "Developer",
            Division = "Analytics", Country = "UK", MonthlyRate = 10000m,
            Jan = 0, Feb = 0, Mar = 0, Apr = 0, May = 1,
            Jun = 0, Jul = 0, Aug = 0, Sep = 0, Oct = 0, Nov = 0, Dec = 0
        };
        context.Resources.AddRange(peResource, analyticsResource);
        await context.SaveChangesAsync();
        // Activity 7 = PE project; Activity 19 = Analytics project
        // Activity 6 / Activity 9 pad each to 100% → no idle FTE
        context.Mappings.AddRange(
            new Mapping { ResourceId = peResource.Id,       ActivityId = 7,  Month = "2026-05", Percent = 50 },
            new Mapping { ResourceId = peResource.Id,       ActivityId = 6,  Month = "2026-05", Percent = 50 },
            new Mapping { ResourceId = analyticsResource.Id, ActivityId = 19, Month = "2026-05", Percent = 40 },
            new Mapping { ResourceId = analyticsResource.Id, ActivityId = 9,  Month = "2026-05", Percent = 60 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(611, null, "2026-05");

        Assert.NotNull(result);
        var row = Assert.Single(result.Candidates, c => c.ResourceName == "Dave Analytics" && c.ActivityType == "Project");
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
        // Activity 1 = "CoreDataWarehouse - Support" (Platform Engineering) — at 100% so no idle FTE
        context.Mappings.Add(new Mapping { ResourceId = resource.Id, ActivityId = 1, Month = "2026-05", Percent = 100 });
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
        // Custom PE app with gap=0.25 so Bob's 25% Minor Changes closes it and lands in combo.
        await using var context = CreateContext();
        context.Applications.Add(new Application { Id = 500, Name = "MinChgTestApp", MinSupportFte = 0.25, RecommendedSupportFte = 0.1 });
        context.Activities.Add(new Activity { Id = 500, Name = "MinChgTestApp - Support", ActivityType = "Support", ApplicationId = 500, Division = "Platform Engineering" });
        await context.SaveChangesAsync();

        var resource = MakePeResource("DC00002", "Bob MinorChg");
        context.Resources.Add(resource);
        await context.SaveChangesAsync();
        // Activity 35 = "SurveyEngine - Minor Changes" (PE); Activity 6 pads to 100% → no idle FTE
        context.Mappings.AddRange(
            new Mapping { ResourceId = resource.Id, ActivityId = 35, Month = "2026-05", Percent = 25 },
            new Mapping { ResourceId = resource.Id, ActivityId = 6,  Month = "2026-05", Percent = 75 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(500, null, "2026-05");

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
        // Custom PE app with gap=0.30 so Alice's 30% project closes it and lands in combo.
        await using var context = CreateContext();
        context.Applications.Add(new Application { Id = 501, Name = "ProjTestApp", MinSupportFte = 0.30, RecommendedSupportFte = 0.1 });
        context.Activities.Add(new Activity { Id = 501, Name = "ProjTestApp - Support", ActivityType = "Support", ApplicationId = 501, Division = "Platform Engineering" });
        await context.SaveChangesAsync();

        var resource = MakePeResource("DC00001", "Alice Project");
        context.Resources.Add(resource);
        await context.SaveChangesAsync();
        // Activity 7 = "P01 CoreDataWarehouse Cloud Migration - Project" (PE); Activity 6 pads to 100% → no idle FTE
        context.Mappings.AddRange(
            new Mapping { ResourceId = resource.Id, ActivityId = 7, Month = "2026-05", Percent = 30 },
            new Mapping { ResourceId = resource.Id, ActivityId = 6, Month = "2026-05", Percent = 70 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(501, null, "2026-05");

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
        // Custom PE app with gap=0.55 so both donors (0.40+0.15) are needed and in combo.
        await using var context = CreateContext();
        context.Applications.Add(new Application { Id = 502, Name = "SortTestApp", MinSupportFte = 0.55, RecommendedSupportFte = 0.1 });
        context.Activities.Add(new Activity { Id = 502, Name = "SortTestApp - Support", ActivityType = "Support", ApplicationId = 502, Division = "Platform Engineering" });
        await context.SaveChangesAsync();

        // small resource at 15%, large resource at 40% — large FTE should sort first
        var small = new Resource { ResourceId = "DC00007a", Name = "Small Donor", ResourceType = "Employee", Company = "Test Co", Seniority = "Analyst", RoleType = "Developer", Division = "Platform Engineering", Country = "UK", MonthlyRate = 5000m,  Jan=0,Feb=0,Mar=0,Apr=0,May=1,Jun=0,Jul=0,Aug=0,Sep=0,Oct=0,Nov=0,Dec=0 };
        var large = new Resource { ResourceId = "DC00007b", Name = "Large Donor",  ResourceType = "Employee", Company = "Test Co", Seniority = "Analyst", RoleType = "Developer", Division = "Platform Engineering", Country = "UK", MonthlyRate = 13000m, Jan=0,Feb=0,Mar=0,Apr=0,May=1,Jun=0,Jul=0,Aug=0,Sep=0,Oct=0,Nov=0,Dec=0 };
        context.Resources.AddRange(small, large);
        await context.SaveChangesAsync();
        // Activity 6 = ObsStack Support pads each to 100% → no idle FTE
        context.Mappings.AddRange(
            new Mapping { ResourceId = small.Id, ActivityId = 7, Month = "2026-05", Percent = 15 },
            new Mapping { ResourceId = small.Id, ActivityId = 6, Month = "2026-05", Percent = 85 },
            new Mapping { ResourceId = large.Id, ActivityId = 7, Month = "2026-05", Percent = 40 },
            new Mapping { ResourceId = large.Id, ActivityId = 6, Month = "2026-05", Percent = 60 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(502, null, "2026-05");

        Assert.NotNull(result);
        var ourTwo = result.Candidates.Where(c => c.ResourceName is "Small Donor" or "Large Donor").ToList();
        Assert.Equal(2, ourTwo.Count);
        Assert.Equal("Large Donor", ourTwo[0].ResourceName);
        Assert.Equal("Small Donor", ourTwo[1].ResourceName);
    }

    // gap_coverage_pct reflects fraction of FTE gap each candidate closes
    [Fact]
    public async Task GetDonorCandidates_GapCoveragePct_ReflectsAllocationFractionOfGap()
    {
        // Custom PE app with gap=0.40. Donor at 40% project exactly closes it (single-row combo).
        // GapCoveragePct = 0.40 / 0.40 * 100 = 100.0. Cumulative fields equal per-row values.
        await using var context = CreateContext();
        context.Applications.Add(new Application { Id = 503, Name = "GapPctTestApp", MinSupportFte = 0.40, RecommendedSupportFte = 0.1 });
        context.Activities.Add(new Activity { Id = 503, Name = "GapPctTestApp - Support", ActivityType = "Support", ApplicationId = 503, Division = "Platform Engineering" });
        await context.SaveChangesAsync();

        var donor = MakePeResource("DC00008", "Gap Donor");
        context.Resources.Add(donor);
        await context.SaveChangesAsync();
        // Activity 7 = PE project (40%); Activity 6 = ObsStack Support pads to 100% → no idle FTE
        context.Mappings.AddRange(
            new Mapping { ResourceId = donor.Id, ActivityId = 7, Month = "2026-05", Percent = 40 },
            new Mapping { ResourceId = donor.Id, ActivityId = 6, Month = "2026-05", Percent = 60 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(503, null, "2026-05");

        Assert.NotNull(result);
        var row = Assert.Single(result.Candidates, c => c.ResourceName == "Gap Donor" && c.ActivityType == "Project");
        Assert.Equal(0.40, row.AllocationFte, precision: 5);
        Assert.Equal(100.0, row.GapCoveragePct, precision: 3); // 0.40 / 0.40 * 100
        // Single-row combo: cumulative fields equal the per-row values
        Assert.Equal(0.40, row.CumulativeFte, precision: 5);
        Assert.Equal(100.0, row.CumulativeGapCoveragePct, precision: 3);
    }

    // cumulative_fte and cumulative_gap_coverage_pct accumulate in sort order (largest FTE first)
    [Fact]
    public async Task GetDonorCandidates_CumulativeFields_AccumulateInSortOrder()
    {
        // Custom PE app with gap=1.0. Large (60%) + Small (40%) exactly close it.
        // Sorted largest-FTE-first: large=0.60 (60% coverage), small=1.00 (100% coverage).
        await using var context = CreateContext();
        context.Applications.Add(new Application { Id = 504, Name = "CumulTestApp", MinSupportFte = 1.0, RecommendedSupportFte = 0.1 });
        context.Activities.Add(new Activity { Id = 504, Name = "CumulTestApp - Support", ActivityType = "Support", ApplicationId = 504, Division = "Platform Engineering" });
        await context.SaveChangesAsync();

        var small = new Resource { ResourceId = "DC00009a", Name = "Cumul Small", ResourceType = "Employee", Company = "Test Co", Seniority = "Analyst", RoleType = "Developer", Division = "Platform Engineering", Country = "UK", MonthlyRate = 5000m,  Jan=0,Feb=0,Mar=0,Apr=0,May=1,Jun=0,Jul=0,Aug=0,Sep=0,Oct=0,Nov=0,Dec=0 };
        var large = new Resource { ResourceId = "DC00009b", Name = "Cumul Large", ResourceType = "Employee", Company = "Test Co", Seniority = "Analyst", RoleType = "Developer", Division = "Platform Engineering", Country = "UK", MonthlyRate = 13000m, Jan=0,Feb=0,Mar=0,Apr=0,May=1,Jun=0,Jul=0,Aug=0,Sep=0,Oct=0,Nov=0,Dec=0 };
        context.Resources.AddRange(small, large);
        await context.SaveChangesAsync();
        context.Mappings.AddRange(
            new Mapping { ResourceId = small.Id, ActivityId = 7, Month = "2026-05", Percent = 40 },
            new Mapping { ResourceId = small.Id, ActivityId = 6, Month = "2026-05", Percent = 60 },
            new Mapping { ResourceId = large.Id, ActivityId = 7, Month = "2026-05", Percent = 60 },
            new Mapping { ResourceId = large.Id, ActivityId = 6, Month = "2026-05", Percent = 40 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(504, null, "2026-05");

        Assert.NotNull(result);
        var ourTwo = result.Candidates.Where(c => c.ResourceName is "Cumul Small" or "Cumul Large").ToList();
        Assert.Equal(2, ourTwo.Count);
        var largeRow = ourTwo[0]; // sorted first (larger FTE)
        var smallRow = ourTwo[1];

        Assert.Equal(0.60, largeRow.CumulativeFte, precision: 5);
        Assert.Equal(60.0, largeRow.CumulativeGapCoveragePct, precision: 3);
        Assert.Equal(1.00, smallRow.CumulativeFte, precision: 5);
        Assert.Equal(100.0, smallRow.CumulativeGapCoveragePct, precision: 3);
    }

    // ── Issue #29: six-tier donor candidate pipeline ─────────────────────────

    // Case 4 (tracer bullet) — tier 5–8 rows have RequiresCrossDivisionalAlignment = true; tiers 1–4 have false
    [Fact]
    public async Task GetDonorCandidates_Tier5678_RequiresCrossDivisionalAlignment_True()
    {
        // Custom PE app with gap=1.0; PE donor (50%) + Analytics donor (50%) both needed → both in combo
        await using var context = CreateContext();
        context.Applications.Add(new Application { Id = 606, Name = "CrossDivApp", MinSupportFte = 1.0, RecommendedSupportFte = 0.1 });
        context.Activities.Add(new Activity { Id = 606, Name = "CrossDivApp - Support", ActivityType = "Support", ApplicationId = 606, Division = "Platform Engineering" });
        await context.SaveChangesAsync();

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
        // Activity 7 = PE project (tier 4); Activity 19 = Analytics project (tier 8)
        // Activity 6 / Activity 9 pad each to 100% → no idle FTE
        context.Mappings.AddRange(
            new Mapping { ResourceId = peResource.Id,        ActivityId = 7,  Month = "2026-05", Percent = 50 },
            new Mapping { ResourceId = peResource.Id,        ActivityId = 6,  Month = "2026-05", Percent = 50 },
            new Mapping { ResourceId = analyticsResource.Id, ActivityId = 19, Month = "2026-05", Percent = 50 },
            new Mapping { ResourceId = analyticsResource.Id, ActivityId = 9,  Month = "2026-05", Percent = 50 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(606, null, "2026-05");

        Assert.NotNull(result);
        var peRow        = Assert.Single(result.Candidates, c => c.ResourceName == "PE Tier3 Donor"       && c.ActivityType == "Project");
        var analyticsRow = Assert.Single(result.Candidates, c => c.ResourceName == "Analytics Tier6 Donor" && c.ActivityType == "Project");
        Assert.False(peRow.RequiresCrossDivisionalAlignment);
        Assert.True(analyticsRow.RequiresCrossDivisionalAlignment);
    }

    // Case 6 — Resource already mapped to target app's Support with idle capacity appears in T1 as existing contributor
    [Fact]
    public async Task GetDonorCandidates_ResourceOnTargetSupport_ExcludedFromAllTiers()
    {
        // Custom PE app (min=1.10); supporter on that app's Support (30%, 70% idle) + project donor (40%).
        // Combo: supporter idle (0.70, T1, existing contributor) + project donor (0.40, T4) = 1.10 FTE.
        await using var context = CreateContext();
        context.Applications.Add(new Application { Id = 612, Name = "SupportExclusionApp", MinSupportFte = 1.10, RecommendedSupportFte = 0.1 });
        context.Activities.Add(new Activity { Id = 612, Name = "SupportExclusionApp - Support", ActivityType = "Support", ApplicationId = 612, Division = "Platform Engineering" });
        await context.SaveChangesAsync();

        var supporter    = MakePeResource("T6A00001", "Target Supporter");
        var projectDonor = MakePeResource("T6A00002", "PE Project Donor");
        context.Resources.AddRange(supporter, projectDonor);
        await context.SaveChangesAsync();
        // Supporter at 30% on custom app's Support → existing contributor, 70% idle
        // Project donor at 40% on PE project; Activity 6 pads to 100% → no idle FTE
        context.Mappings.AddRange(
            new Mapping { ResourceId = supporter.Id,    ActivityId = 612, Month = "2026-05", Percent = 30 },
            new Mapping { ResourceId = projectDonor.Id, ActivityId = 7,   Month = "2026-05", Percent = 40 },
            new Mapping { ResourceId = projectDonor.Id, ActivityId = 6,   Month = "2026-05", Percent = 60 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(612, null, "2026-05");

        Assert.NotNull(result);
        // Supporter has 70% idle → appears in T1 as an existing contributor (increase not add)
        var supporterRow = Assert.Single(result.Candidates, c => c.ResourceName == "Target Supporter" && c.ActivityType == "Idle");
        Assert.True(supporterRow.IsExistingContributor);
        Assert.Equal(30, supporterRow.CurrentTargetSupportPct);
        Assert.Contains(result.Candidates, c => c.ResourceName == "PE Project Donor");
    }

    // Case 5a — Application at exactly recommended_support_fte yields no Tier-2 surplus-Support donor
    [Fact]
    public async Task GetDonorCandidates_Tier2_ExactlyAtRecommendedFte_YieldsNoDonor()
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

    // Case 5b — Application one percent-point above recommended yields a Tier-2 surplus-Support donor with surplus FTE only
    [Fact]
    public async Task GetDonorCandidates_Tier2_OnePercentAboveRecommended_YieldsSurplusFteOnly()
    {
        // SlightSurplusApp: recommended=0.50; resource at 51% → coverage=0.51 → surplus=0.01
        // Custom target PE app (min=0.01) closes the gap with just that 0.01 FTE surplus row.
        // Resource padded to 100% → no idle FTE (idle would surface before surplus).
        await using var context = CreateContext();
        context.Applications.Add(new Application { Id = 100, Name = "SlightSurplusApp", MinSupportFte = 0.3, RecommendedSupportFte = 0.5 });
        context.Activities.Add(new Activity { Id = 200, Name = "SlightSurplusApp - Support", ActivityType = "Support", ApplicationId = 100, Division = "Platform Engineering" });
        context.Applications.Add(new Application { Id = 607, Name = "TinyGapApp", MinSupportFte = 0.01, RecommendedSupportFte = 0.001 });
        context.Activities.Add(new Activity { Id = 607, Name = "TinyGapApp - Support", ActivityType = "Support", ApplicationId = 607, Division = "Platform Engineering" });
        await context.SaveChangesAsync();

        var resource = MakePeResource("T5B00001", "OneAbove Resource");
        context.Resources.Add(resource);
        await context.SaveChangesAsync();
        // 51% on SlightSurplusApp Support + 49% pad → 100% allocated, no idle FTE
        context.Mappings.AddRange(
            new Mapping { ResourceId = resource.Id, ActivityId = 200, Month = "2026-05", Percent = 51 },
            new Mapping { ResourceId = resource.Id, ActivityId = 6,   Month = "2026-05", Percent = 49 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(607, null, "2026-05");

        Assert.NotNull(result);
        var row = Assert.Single(result.Candidates, c => c.ResourceName == "OneAbove Resource" && c.ActivityType == "Support");
        Assert.Equal(0.01, row.AllocationFte, precision: 5);
        Assert.False(row.RequiresCrossDivisionalAlignment);
    }

    // Case 1 — Tier 2 (surplus Support) candidate appears before Tier 3 (Minor Changes) candidate
    [Fact]
    public async Task GetDonorCandidates_Tier2_AppearsBefore_Tier3()
    {
        // SurplusApp: recommended=0.50; Tier1 Donor at 51% → surplus=0.01 FTE.
        // Custom target PE app (min=0.26): surplus (0.01) + minor changes (0.25) = 0.26 → both in combo.
        // Both padded to 100% → no idle FTE (idle would surface before surplus/minor-changes).
        await using var context = CreateContext();
        context.Applications.Add(new Application { Id = 100, Name = "SurplusApp", MinSupportFte = 0.3, RecommendedSupportFte = 0.5 });
        context.Activities.Add(new Activity { Id = 200, Name = "SurplusApp - Support", ActivityType = "Support", ApplicationId = 100, Division = "Platform Engineering" });
        context.Applications.Add(new Application { Id = 608, Name = "Tier2Tier3App", MinSupportFte = 0.26, RecommendedSupportFte = 0.1 });
        context.Activities.Add(new Activity { Id = 608, Name = "Tier2Tier3App - Support", ActivityType = "Support", ApplicationId = 608, Division = "Platform Engineering" });
        await context.SaveChangesAsync();

        var tier1Resource = MakePeResource("T1B00001", "Tier1 Donor");
        var tier2Resource = MakePeResource("T1B00002", "Tier2 Donor");
        context.Resources.AddRange(tier1Resource, tier2Resource);
        await context.SaveChangesAsync();
        // Activity 6 pads each to 100% → no idle FTE
        context.Mappings.AddRange(
            new Mapping { ResourceId = tier1Resource.Id, ActivityId = 200, Month = "2026-05", Percent = 51 },
            new Mapping { ResourceId = tier1Resource.Id, ActivityId = 6,   Month = "2026-05", Percent = 49 },
            new Mapping { ResourceId = tier2Resource.Id, ActivityId = 35,  Month = "2026-05", Percent = 25 },
            new Mapping { ResourceId = tier2Resource.Id, ActivityId = 6,   Month = "2026-05", Percent = 75 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(608, null, "2026-05");

        Assert.NotNull(result);
        // "Tier1 Donor" is a surplus support row (tier 2); "Tier2 Donor" is minor-changes (tier 3)
        var tier1Idx = result.Candidates.FindIndex(c => c.ResourceName == "Tier1 Donor" && c.ActivityType == "Support");
        var tier2Idx = result.Candidates.FindIndex(c => c.ResourceName == "Tier2 Donor" && c.ActivityType == "Minor Changes");
        Assert.True(tier1Idx >= 0 && tier2Idx >= 0, "Both candidates must appear");
        Assert.True(tier1Idx < tier2Idx, "Surplus Support must appear before Minor Changes");
    }

    // Case 2 — Tier 3 (Minor Changes) candidate appears before Tier 4 (Project) candidate
    [Fact]
    public async Task GetDonorCandidates_Tier3_AppearsBefore_Tier4()
    {
        // Custom PE target app (min=0.55): minor changes (0.25) + project (0.30) = 0.55 → both in combo.
        // Both padded to 100% → no idle FTE.
        await using var context = CreateContext();
        context.Applications.Add(new Application { Id = 609, Name = "Tier3Tier4App", MinSupportFte = 0.55, RecommendedSupportFte = 0.1 });
        context.Activities.Add(new Activity { Id = 609, Name = "Tier3Tier4App - Support", ActivityType = "Support", ApplicationId = 609, Division = "Platform Engineering" });
        await context.SaveChangesAsync();

        var tier2Resource = MakePeResource("T2B00001", "Tier2 MinorChg");
        var tier3Resource = MakePeResource("T2B00002", "Tier3 Project");
        context.Resources.AddRange(tier2Resource, tier3Resource);
        await context.SaveChangesAsync();
        // Activity 35 = SurveyEngine - Minor Changes (PE); Activity 7 = PE project
        // Activity 6 pads each to 100% → no idle FTE
        context.Mappings.AddRange(
            new Mapping { ResourceId = tier2Resource.Id, ActivityId = 35, Month = "2026-05", Percent = 25 },
            new Mapping { ResourceId = tier2Resource.Id, ActivityId = 6,  Month = "2026-05", Percent = 75 },
            new Mapping { ResourceId = tier3Resource.Id, ActivityId = 7,  Month = "2026-05", Percent = 30 },
            new Mapping { ResourceId = tier3Resource.Id, ActivityId = 6,  Month = "2026-05", Percent = 70 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(609, null, "2026-05");

        Assert.NotNull(result);
        var tier2Idx = result.Candidates.FindIndex(c => c.ResourceName == "Tier2 MinorChg");
        var tier3Idx = result.Candidates.FindIndex(c => c.ResourceName == "Tier3 Project");
        Assert.True(tier2Idx >= 0 && tier3Idx >= 0, "Both candidates must appear");
        Assert.True(tier2Idx < tier3Idx, "Tier-3 (Minor Changes) must appear before Tier-4 (Project)");
    }

    // Case 3 — Tiers 1–4 (same div) all appear before tiers 5–8 (cross div)
    [Fact]
    public async Task GetDonorCandidates_Tiers1234_AppearBefore_Tiers5678()
    {
        // Custom PE target (min=0.80): PE project (0.40, T4) + Analytics project (0.40, T8) = 0.80 → both in combo.
        // Both padded to 100% → no idle FTE.
        await using var context = CreateContext();
        context.Applications.Add(new Application { Id = 610, Name = "CrossTierApp", MinSupportFte = 0.80, RecommendedSupportFte = 0.1 });
        context.Activities.Add(new Activity { Id = 610, Name = "CrossTierApp - Support", ActivityType = "Support", ApplicationId = 610, Division = "Platform Engineering" });
        await context.SaveChangesAsync();

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
        // Activity 7 = PE project; Activity 19 = Analytics project
        // Activity 6 / Activity 9 pad each to 100% → no idle FTE
        context.Mappings.AddRange(
            new Mapping { ResourceId = peResource.Id,        ActivityId = 7,  Month = "2026-05", Percent = 40 },
            new Mapping { ResourceId = peResource.Id,        ActivityId = 6,  Month = "2026-05", Percent = 60 },
            new Mapping { ResourceId = analyticsResource.Id, ActivityId = 19, Month = "2026-05", Percent = 40 },
            new Mapping { ResourceId = analyticsResource.Id, ActivityId = 9,  Month = "2026-05", Percent = 60 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(610, null, "2026-05");

        Assert.NotNull(result);
        var sameDivIdx  = result.Candidates.FindIndex(c => c.ResourceName == "PE Project Tier3");
        var crossDivIdx = result.Candidates.FindIndex(c => c.ResourceName == "Analytics Project Tier6");
        Assert.True(sameDivIdx >= 0 && crossDivIdx >= 0);
        Assert.True(sameDivIdx < crossDivIdx, "Same-div (Tier 4) must appear before cross-div (Tier 8)");
    }

    // Case 7 — CumulativeFte accumulates correctly across a tier boundary
    [Fact]
    public async Task GetDonorCandidates_CumulativeFte_AccumulatesAcrossTierBoundary()
    {
        // Tier-2 surplus support: donatable = 0.01 FTE; Tier-3 Minor Changes: 0.25 FTE
        // CumulativeFte at tier-3 row = 0.01 + 0.25 = 0.26
        // Custom PE target (min=0.26) so both rows land in the fewest-changes combo.
        await using var context = CreateContext();
        context.Applications.Add(new Application { Id = 100, Name = "SurplusApp", MinSupportFte = 0.3, RecommendedSupportFte = 0.5 });
        context.Activities.Add(new Activity { Id = 200, Name = "SurplusApp - Support", ActivityType = "Support", ApplicationId = 100, Division = "Platform Engineering" });
        context.Applications.Add(new Application { Id = 605, Name = "CumulBoundaryApp", MinSupportFte = 0.26, RecommendedSupportFte = 0.1 });
        context.Activities.Add(new Activity { Id = 605, Name = "CumulBoundaryApp - Support", ActivityType = "Support", ApplicationId = 605, Division = "Platform Engineering" });
        await context.SaveChangesAsync();

        var tier1Resource = MakePeResource("T7A00001", "Tier1 Cumul");
        var tier2Resource = MakePeResource("T7A00002", "Tier2 Cumul");
        context.Resources.AddRange(tier1Resource, tier2Resource);
        await context.SaveChangesAsync();
        // Activity 6 pads both to 100% → no idle FTE
        context.Mappings.AddRange(
            new Mapping { ResourceId = tier1Resource.Id, ActivityId = 200, Month = "2026-05", Percent = 51 }, // donatable = 0.01
            new Mapping { ResourceId = tier1Resource.Id, ActivityId = 6,   Month = "2026-05", Percent = 49 },
            new Mapping { ResourceId = tier2Resource.Id, ActivityId = 35,  Month = "2026-05", Percent = 25 }, // 0.25 FTE
            new Mapping { ResourceId = tier2Resource.Id, ActivityId = 6,   Month = "2026-05", Percent = 75 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(605, null, "2026-05");

        Assert.NotNull(result);
        var tier1Row = Assert.Single(result.Candidates, c => c.ResourceName == "Tier1 Cumul" && c.ActivityType == "Support");
        var tier2Row = Assert.Single(result.Candidates, c => c.ResourceName == "Tier2 Cumul" && c.ActivityType == "Minor Changes");
        var tier1Idx = result.Candidates.IndexOf(tier1Row);
        var tier2Idx = result.Candidates.IndexOf(tier2Row);
        Assert.True(tier1Idx < tier2Idx, "Tier-2 must precede tier-3 in the list");
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

    // ── idle FTE tier (issue #35) ─────────────────────────────────────────────

    // Cycle 1 — Idle resource appears as ActivityType "Idle" in candidates
    [Fact]
    public async Task GetDonorCandidates_IdleResource_SurfacesWithActivityTypeIdle()
    {
        // PE resource at 57% total allocation → 43% idle → AllocationFte = 0.43
        // Custom PE target (min=0.43) so Emily's idle row closes the gap and lands in combo.
        await using var context = CreateContext();
        context.Applications.Add(new Application { Id = 601, Name = "IdleTestApp", MinSupportFte = 0.43, RecommendedSupportFte = 0.1 });
        context.Activities.Add(new Activity { Id = 601, Name = "IdleTestApp - Support", ActivityType = "Support", ApplicationId = 601, Division = "Platform Engineering" });
        await context.SaveChangesAsync();

        var idleResource = MakePeResource("I1A00001", "Emily Idle");
        context.Resources.Add(idleResource);
        await context.SaveChangesAsync();
        // Activity 35 = SurveyEngine - Minor Changes (PE) → 57% allocation, 43% idle
        context.Mappings.Add(new Mapping { ResourceId = idleResource.Id, ActivityId = 35, Month = "2026-05", Percent = 57 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(601, null, "2026-05");

        Assert.NotNull(result);
        var idleRow = Assert.Single(result.Candidates, c => c.ResourceName == "Emily Idle" && c.ActivityType == "Idle");
        Assert.Equal("Idle capacity", idleRow.ActivityName);
        Assert.Equal(0.43, idleRow.AllocationFte, precision: 5);
        Assert.Equal(57, idleRow.Percent);
        Assert.False(idleRow.RequiresCrossDivisionalAlignment);
    }

    // Cycle 3 — Resource on target app's Support with idle capacity appears in T1 as existing contributor
    [Fact]
    public async Task GetDonorCandidates_ResourceOnTargetSupport_ExcludedFromIdleTier()
    {
        // Custom PE app (min=1.30): supporter idle (0.70, existing contributor) + regular idle (0.60) = 1.30 → both in combo.
        // Regular Donor has 40% project + 60% idle; supporter has 30% on custom app Support + 70% idle.
        await using var context = CreateContext();
        context.Applications.Add(new Application { Id = 613, Name = "IdleExclusionApp", MinSupportFte = 1.30, RecommendedSupportFte = 0.1 });
        context.Activities.Add(new Activity { Id = 613, Name = "IdleExclusionApp - Support", ActivityType = "Support", ApplicationId = 613, Division = "Platform Engineering" });
        await context.SaveChangesAsync();

        var supporter    = MakePeResource("I3A00001", "Target Support Idle");
        var projectDonor = MakePeResource("I3A00002", "Regular Donor");
        context.Resources.AddRange(supporter, projectDonor);
        await context.SaveChangesAsync();
        // Supporter at 30% on custom app Support → 70% idle, existing contributor
        // Regular Donor at 40% on PE project → 60% idle
        context.Mappings.AddRange(
            new Mapping { ResourceId = supporter.Id,    ActivityId = 613, Month = "2026-05", Percent = 30 },
            new Mapping { ResourceId = projectDonor.Id, ActivityId = 7,   Month = "2026-05", Percent = 40 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(613, null, "2026-05");

        Assert.NotNull(result);
        var supporterRow = Assert.Single(result.Candidates, c => c.ResourceName == "Target Support Idle" && c.ActivityType == "Idle");
        Assert.True(supporterRow.IsExistingContributor);
        Assert.Contains(result.Candidates, c => c.ResourceName == "Regular Donor" && c.ActivityType == "Idle");
    }

    // Cycle 4 — Cross-divisional idle row has RequiresCrossDivisionalAlignment = true
    [Fact]
    public async Task GetDonorCandidates_CrossDivisionalIdleResource_HasAlignmentFlagTrue()
    {
        // Analytics resource at 60% → 40% idle; target is a PE app → cross-divisional.
        // Custom PE target (min=0.40) so the cross-div idle row closes the gap and lands in combo.
        await using var context = CreateContext();
        context.Applications.Add(new Application { Id = 602, Name = "CrossDivIdleApp", MinSupportFte = 0.40, RecommendedSupportFte = 0.1 });
        context.Activities.Add(new Activity { Id = 602, Name = "CrossDivIdleApp - Support", ActivityType = "Support", ApplicationId = 602, Division = "Platform Engineering" });
        await context.SaveChangesAsync();

        var analyticsResource = new Resource
        {
            ResourceId = "I4A00001", Name = "Analytics Idle Cross",
            ResourceType = "Employee", Company = "Test Co", Seniority = "Analyst", RoleType = "Developer",
            Division = "Analytics", Country = "UK", MonthlyRate = 8000m,
            Jan=0,Feb=0,Mar=0,Apr=0,May=1,Jun=0,Jul=0,Aug=0,Sep=0,Oct=0,Nov=0,Dec=0
        };
        context.Resources.Add(analyticsResource);
        await context.SaveChangesAsync();
        // Activity 9 = CRM - Support (Analytics division); 60% allocated → 40% idle
        context.Mappings.Add(new Mapping { ResourceId = analyticsResource.Id, ActivityId = 9, Month = "2026-05", Percent = 60 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(602, null, "2026-05");

        Assert.NotNull(result);
        var idleRow = Assert.Single(result.Candidates, c => c.ResourceName == "Analytics Idle Cross" && c.ActivityType == "Idle");
        Assert.Equal(0.40, idleRow.AllocationFte, precision: 5);
        Assert.True(idleRow.RequiresCrossDivisionalAlignment);
    }

    // Cycle 2 — Resource with zero mappings has 100% idle and surfaces in tier 1
    [Fact]
    public async Task GetDonorCandidates_ZeroMappingResource_SurfacesAsFullyIdleTier1()
    {
        // A PE resource with NO mappings has 100% idle FTE.
        // Custom PE target (min=1.0) so the idle row closes the gap and lands in combo.
        await using var context = CreateContext();
        context.Applications.Add(new Application { Id = 603, Name = "ZeroMappingApp", MinSupportFte = 1.0, RecommendedSupportFte = 0.1 });
        context.Activities.Add(new Activity { Id = 603, Name = "ZeroMappingApp - Support", ActivityType = "Support", ApplicationId = 603, Division = "Platform Engineering" });
        await context.SaveChangesAsync();

        var unmapped = MakePeResource("I2A00001", "Unmapped PE");
        context.Resources.Add(unmapped);
        await context.SaveChangesAsync();
        // Intentionally add NO mappings for this resource

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(603, null, "2026-05");

        Assert.NotNull(result);
        var row = Assert.Single(result.Candidates, c => c.ResourceName == "Unmapped PE");
        Assert.Equal("Idle", row.ActivityType);
        Assert.Equal(1.0, row.AllocationFte, precision: 5);
        Assert.Equal(0, row.Percent); // total allocation = 0%
        Assert.False(row.RequiresCrossDivisionalAlignment);
    }

    // Cycle 6 — idle tier (Tier 1) appears before surplus-Support tier (Tier 2)
    [Fact]
    public async Task GetDonorCandidates_IdleTier_AppearsBefore_SurplusSupportTier()
    {
        // Idle PE (40% idle, T1) + Surplus PE (100% surplus, T2) = 1.40 FTE.
        // Custom PE target (min=1.40) so both rows are needed and land in combo.
        await using var context = CreateContext();
        context.Applications.Add(new Application { Id = 100, Name = "SurplusApp", MinSupportFte = 0.0, RecommendedSupportFte = 0.0 });
        context.Activities.Add(new Activity { Id = 200, Name = "SurplusApp - Support", ActivityType = "Support", ApplicationId = 100, Division = "Platform Engineering" });
        context.Applications.Add(new Application { Id = 604, Name = "IdleBeforeSurplusApp", MinSupportFte = 1.40, RecommendedSupportFte = 0.1 });
        context.Activities.Add(new Activity { Id = 604, Name = "IdleBeforeSurplusApp - Support", ActivityType = "Support", ApplicationId = 604, Division = "Platform Engineering" });
        await context.SaveChangesAsync();

        var idleResource    = MakePeResource("I6A00001", "Idle PE Resource");
        var surplusResource = MakePeResource("I6A00002", "Surplus PE Resource");
        context.Resources.AddRange(idleResource, surplusResource);
        await context.SaveChangesAsync();
        // idleResource: 60% on PE project → 40% idle (T1)
        // surplusResource: 100% on SurplusApp (recommended=0) → 100% surplus Support (T2)
        context.Mappings.AddRange(
            new Mapping { ResourceId = idleResource.Id,    ActivityId = 7,   Month = "2026-05", Percent = 60 },
            new Mapping { ResourceId = surplusResource.Id, ActivityId = 200, Month = "2026-05", Percent = 100 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(604, null, "2026-05");

        Assert.NotNull(result);
        var idleIdx    = result.Candidates.FindIndex(c => c.ResourceName == "Idle PE Resource"    && c.ActivityType == "Idle");
        var surplusIdx = result.Candidates.FindIndex(c => c.ResourceName == "Surplus PE Resource" && c.ActivityType == "Support");
        Assert.True(idleIdx >= 0 && surplusIdx >= 0, "Both candidates must appear");
        Assert.True(idleIdx < surplusIdx, "Idle (Tier 1) must appear before surplus Support (Tier 2)");
    }

    // Cycle 5 — mapping_changes text for idle row: "add X% to AppName Support (use idle capacity — currently Y% allocated)"
    [Fact]
    public async Task GetDonorCandidates_IdleRow_MappingChangeTextUsesIdleCapacityFormat()
    {
        // ObsStack (app 5) has min=1.0, recommended=2.0. No seeded mappings in in-memory DB → gap = 1.0 FTE.
        // Supporter at 75% on ObsStack Support → gap becomes 0.25 FTE.
        // Idle PE resource at 57% allocation has 43% idle → covers 0.25 FTE gap → enters combo.
        // DonablePercent capped at gap (25%). Combo: single idle row.
        await using var context = CreateContext();
        var supporter    = MakePeResource("I5A00001", "Existing Support");
        var idleResource = MakePeResource("I5A00002", "Emily Clarke");
        context.Resources.AddRange(supporter, idleResource);
        await context.SaveChangesAsync();
        // Activity 6 = ObsStack Support (PE); Activity 35 = SurveyEngine Minor Changes (PE, pad to 100%)
        context.Mappings.AddRange(
            new Mapping { ResourceId = supporter.Id,    ActivityId = 6,  Month = "2026-05", Percent = 75 },
            new Mapping { ResourceId = supporter.Id,    ActivityId = 35, Month = "2026-05", Percent = 25 },
            new Mapping { ResourceId = idleResource.Id, ActivityId = 35, Month = "2026-05", Percent = 57 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(5, null, "2026-05");

        Assert.NotNull(result);
        var mappingChange = result.FewestChangesCombination?.MappingChanges
            .FirstOrDefault(c => c.Contains("Emily Clarke"));
        Assert.NotNull(mappingChange);
        Assert.Contains("use idle capacity", mappingChange);
        Assert.Contains("currently 57% allocated", mappingChange);
        Assert.Contains("Emily Clarke: add", mappingChange);
        Assert.Contains("ObservabilityStack Support", mappingChange);
    }

    // Cycle 5b — cross-divisional idle row mapping_changes appends cross-divisional sign-off suffix
    [Fact]
    public async Task GetDonorCandidates_CrossDivIdleRow_MappingChangeAppendsCrossDivSuffix()
    {
        // ObsStack (app 5, PE). Supporter (PE) at 75% → gap = 0.25 FTE.
        // No same-div idle resource. Analytics idle at 40% → tier 5 → closes gap → enters combo.
        await using var context = CreateContext();
        var supporter    = MakePeResource("I5B00001", "PE Supporter");
        var crossDivIdle = new Resource
        {
            ResourceId = "I5B00002", Name = "Analytics Idle Xdiv",
            ResourceType = "Employee", Company = "Test Co", Seniority = "Analyst", RoleType = "Developer",
            Division = "Analytics", Country = "UK", MonthlyRate = 8000m,
            Jan=0,Feb=0,Mar=0,Apr=0,May=1,Jun=0,Jul=0,Aug=0,Sep=0,Oct=0,Nov=0,Dec=0
        };
        context.Resources.AddRange(supporter, crossDivIdle);
        await context.SaveChangesAsync();
        // Activity 6 = ObsStack Support (PE); Activity 35 = SurveyEngine Minor Changes (PE pad)
        // Activity 9 = CRM - Support (Analytics)
        context.Mappings.AddRange(
            new Mapping { ResourceId = supporter.Id,    ActivityId = 6,  Month = "2026-05", Percent = 75 },
            new Mapping { ResourceId = supporter.Id,    ActivityId = 35, Month = "2026-05", Percent = 25 },
            new Mapping { ResourceId = crossDivIdle.Id, ActivityId = 9,  Month = "2026-05", Percent = 60 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(5, null, "2026-05");

        Assert.NotNull(result);
        var mappingChange = result.FewestChangesCombination?.MappingChanges
            .FirstOrDefault(c => c.Contains("Analytics Idle Xdiv"));
        Assert.NotNull(mappingChange);
        Assert.Contains("use idle capacity", mappingChange);
        Assert.Contains("requires cross-divisional sign-off", mappingChange);
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

    // ── get_application_risk ─────────────────────────────────────────────────

    // Inactive-resource Support mappings must not count toward coverage_fte;
    // the snapshot active-flag gate owns this rule (ADR 9).
    [Fact]
    public async Task GetApplications_InactiveResourceSupportMapping_ExcludedFromCoverage()
    {
        await using var context = CreateContext();
        var active = new Resource
        {
            ResourceId = "AR00001", Name = "Active Resource", ResourceType = "Employee",
            Company = "Test Co", Seniority = "Analyst", RoleType = "Developer",
            Division = "Platform Engineering", Country = "UK", MonthlyRate = 10000m,
            Jan=0,Feb=0,Mar=0,Apr=0,May=1,Jun=0,Jul=0,Aug=0,Sep=0,Oct=0,Nov=0,Dec=0
        };
        var inactive = new Resource
        {
            ResourceId = "AR00002", Name = "Inactive Resource", ResourceType = "Employee",
            Company = "Test Co", Seniority = "Analyst", RoleType = "Developer",
            Division = "Platform Engineering", Country = "UK", MonthlyRate = 10000m,
            Jan=0,Feb=0,Mar=0,Apr=0,May=0,Jun=0,Jul=0,Aug=0,Sep=0,Oct=0,Nov=0,Dec=0
        };
        context.Resources.AddRange(active, inactive);
        await context.SaveChangesAsync();

        // Activity 5 = AuthService - Support (ApplicationId = 4, MinSupportFte = 4.0)
        context.Mappings.AddRange(
            new Mapping { ResourceId = active.Id,   ActivityId = 5, Month = "2026-05", Percent = 50 },
            new Mapping { ResourceId = inactive.Id, ActivityId = 5, Month = "2026-05", Percent = 100 }
        );
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var results = await svc.GetApplicationsAsync("2026-05", applicationId: 4);

        var authService = Assert.Single(results);
        Assert.Equal(0.50, authService.current_coverage_fte, precision: 10);
        Assert.Equal("high", authService.risk); // 0.50 < MinSupportFte (4.0)
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

    // ── Issue #31 / Issue #43: trim-last DonablePercent ──────────────────────

    // Cycle 3 — non-stopping candidates keep full tier-builder DonablePercent; only stopping is trimmed
    [Fact]
    public async Task GetDonorCandidates_TrimLast_NonStoppingCandidatesRetainFullDonablePercent()
    {
        // gap = 1.5 FTE; 3 project donors at 80%, 50%, 30% (tier 4, PE)
        // Sorted by AllocationFte desc: d80 (0.80), d50 (0.50), d30 (0.30)
        // Cumulative: d80=0.80 (53%), d50=1.30 (87%), d30=1.60 (107% → stopping at idx 2)
        // Trim-last: d80=80 (full), d50=50 (full), d30=max(1,round((1.5-1.30)*100))=20
        await using var context = CreateContext();
        context.Applications.Add(new Application { Id = 103, Name = "TieBreakApp", MinSupportFte = 1.5, RecommendedSupportFte = 0.5 });
        context.Activities.Add(new Activity { Id = 203, Name = "TieBreakApp - Support", ActivityType = "Support", ApplicationId = 103, Division = "Platform Engineering" });
        await context.SaveChangesAsync();

        var d80 = MakePeResource("LR00020", "TB Donor 80");
        var d50 = MakePeResource("LR00021", "TB Donor 50");
        var d30 = MakePeResource("LR00022", "TB Donor 30");
        context.Resources.AddRange(d80, d50, d30);
        await context.SaveChangesAsync();
        // Activity 7 = PE project; Activity 6 = ObsStack Support pads each to 100% → no idle FTE
        // ObsStack total coverage = 0.20 + 0.50 + 0.70 = 1.40 FTE < 2.0 recommended → no surplus row
        context.Mappings.AddRange(
            new Mapping { ResourceId = d80.Id, ActivityId = 7, Month = "2026-05", Percent = 80 },
            new Mapping { ResourceId = d80.Id, ActivityId = 6, Month = "2026-05", Percent = 20 },
            new Mapping { ResourceId = d50.Id, ActivityId = 7, Month = "2026-05", Percent = 50 },
            new Mapping { ResourceId = d50.Id, ActivityId = 6, Month = "2026-05", Percent = 50 },
            new Mapping { ResourceId = d30.Id, ActivityId = 7, Month = "2026-05", Percent = 30 },
            new Mapping { ResourceId = d30.Id, ActivityId = 6, Month = "2026-05", Percent = 70 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(103, null, "2026-05");

        Assert.NotNull(result);
        Assert.NotNull(result.FewestChangesCombination);
        var changes = result.FewestChangesCombination.MappingChanges;
        Assert.Equal(3, changes.Count);
        static int Donable(string s) => int.Parse(s.Split("add ")[1].Split('%')[0]);
        Assert.Equal(80, Donable(changes.Single(s => s.Contains("TB Donor 80"))));
        Assert.Equal(50, Donable(changes.Single(s => s.Contains("TB Donor 50"))));
        Assert.Equal(20, Donable(changes.Single(s => s.Contains("TB Donor 30"))));
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
        // Activity 7 = PE project (30%); Activity 6 pads to 100% → no idle FTE
        context.Mappings.AddRange(
            new Mapping { ResourceId = donor.Id, ActivityId = 7, Month = "2026-05", Percent = 30 },
            new Mapping { ResourceId = donor.Id, ActivityId = 6, Month = "2026-05", Percent = 70 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(4, null, "2026-05"); // AuthService, gap = 4.0

        Assert.NotNull(result);
        Assert.Null(result.FewestChangesCombination);
        Assert.Empty(result.Candidates);
        Assert.Equal(1, result.TotalAvailableCandidatesCount);
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
        // Activity 35 = Minor Changes (60%); Activity 6 = ObsStack Support (40%) pads to 100% → no idle FTE
        context.Mappings.AddRange(
            new Mapping { ResourceId = donor.Id, ActivityId = 35, Month = "2026-05", Percent = 60 },
            new Mapping { ResourceId = donor.Id, ActivityId = 6,  Month = "2026-05", Percent = 40 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(102, null, "2026-05");

        Assert.NotNull(result);
        Assert.NotNull(result.FewestChangesCombination);
        var changes = result.FewestChangesCombination.MappingChanges;
        Assert.Single(changes);
        static int Donable(string s) => int.Parse(s.Split("add ")[1].Split('%')[0]);
        static int SourcePct(string s) => int.Parse(s.Split("currently ")[1].Split('%')[0]);
        Assert.True(Donable(changes[0]) <= SourcePct(changes[0]),
            $"DonablePercent exceeds source Percent in: {changes[0]}");
    }

    // Cycle 1 (tracer) — stopping candidate is trimmed to remaining gap; non-stopping keeps full value
    [Fact]
    public async Task GetDonorCandidates_TrimLast_StoppingCandidateTrimmedToRemainingGap()
    {
        // gap = 1.5 FTE; 2 donors at 100% Minor Changes → cumulative = 133.3%
        // First donor (1.0 FTE) is non-stopping → DonablePercent = 100 (full).
        // Second donor is stopping: remainingGap = 1.5 - 1.0 = 0.50 FTE → DonablePercent = 50.
        await using var context = CreateContext();
        context.Applications.Add(new Application { Id = 101, Name = "LRTestApp", MinSupportFte = 1.5, RecommendedSupportFte = 0.5 });
        context.Activities.Add(new Activity { Id = 201, Name = "LRTestApp - Support", ActivityType = "Support", ApplicationId = 101, Division = "Platform Engineering" });
        await context.SaveChangesAsync();

        var donorA = MakePeResource("LR00001", "LR Donor A");
        var donorB = MakePeResource("LR00002", "LR Donor B");
        context.Resources.AddRange(donorA, donorB);
        await context.SaveChangesAsync();
        // Activity 35 = SurveyEngine - Minor Changes (PE, tier 3)
        context.Mappings.AddRange(
            new Mapping { ResourceId = donorA.Id, ActivityId = 35, Month = "2026-05", Percent = 100 },
            new Mapping { ResourceId = donorB.Id, ActivityId = 35, Month = "2026-05", Percent = 100 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(101, null, "2026-05");

        Assert.NotNull(result);
        Assert.NotNull(result.FewestChangesCombination);
        var changes = result.FewestChangesCombination.MappingChanges;
        Assert.Equal(2, changes.Count);
        static int Donable(string s) => int.Parse(s.Split("add ")[1].Split('%')[0]);
        Assert.Contains(changes, s => Donable(s) == 100);
        Assert.Contains(changes, s => Donable(s) == 50);
    }

    // ── Issue #32: trim get_donor_candidates response ────────────────────────

    // Cycle 2 — TotalAvailableCandidatesCount reflects pre-trim count
    [Fact]
    public async Task GetDonorCandidates_TotalAvailableCandidatesCount_ReflectsPreTrimCount()
    {
        // App with gap 1.0 FTE; 2 donors close the gap (combo = 2 rows); 6 extra donors are pipeline.
        // Total = 8 candidates. Combo rows go to FewestChangesCombination; Candidates = 5 pipeline rows.
        // TotalAvailableCandidatesCount must == 8 (the pre-trim count).
        await using var context = CreateContext();
        context.Applications.Add(new Application { Id = 110, Name = "TrimApp", MinSupportFte = 1.0, RecommendedSupportFte = 0.5 });
        // Custom PaddingApp (recommended=9999) pads each resource to 100% → no idle rows
        context.Applications.Add(new Application { Id = 998, Name = "_PadPE", MinSupportFte = 0, RecommendedSupportFte = 9999 });
        context.Activities.Add(new Activity { Id = 997, Name = "_PadPE - Support", ActivityType = "Support", ApplicationId = 998, Division = "Platform Engineering" });
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

        // Activity 7 = PE project; Activity 997 = PaddingApp Support pads each to 100%
        context.Mappings.Add(new Mapping { ResourceId = combo1.Id, ActivityId = 7,   Month = "2026-05", Percent = 60 });
        context.Mappings.Add(new Mapping { ResourceId = combo1.Id, ActivityId = 997, Month = "2026-05", Percent = 40 });
        context.Mappings.Add(new Mapping { ResourceId = combo2.Id, ActivityId = 7,   Month = "2026-05", Percent = 60 });
        context.Mappings.Add(new Mapping { ResourceId = combo2.Id, ActivityId = 997, Month = "2026-05", Percent = 40 });
        foreach (var e in extras)
        {
            context.Mappings.Add(new Mapping { ResourceId = e.Id, ActivityId = 7,   Month = "2026-05", Percent = 10 });
            context.Mappings.Add(new Mapping { ResourceId = e.Id, ActivityId = 997, Month = "2026-05", Percent = 90 });
        }
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(110, null, "2026-05");

        Assert.NotNull(result);
        Assert.NotNull(result.FewestChangesCombination);
        Assert.Equal(8, result.TotalAvailableCandidatesCount);
        Assert.True(result.Candidates.Count < result.TotalAvailableCandidatesCount,
            "Trimmed list should be shorter than total available");
    }

    // Cycle 3 — trimmed list is exactly DonorCandidateBufferRows (5) pipeline rows
    [Fact]
    public async Task GetDonorCandidates_Trimming_CandidatesCount_EqualsBuffer()
    {
        // Same seed as cycle 2: 2 combo rows + 6 extras = 8 total.
        // After trim: combo rows go to FewestChangesCombination; candidates = 5 pipeline rows.
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
        Assert.Equal(result.FewestChangesCombination!.RowCount, result.Candidates.Count);
        Assert.Equal(2, result.Candidates.Count);
    }

    // Cycle 4 — combo rows appear in FewestChangesCombination.MappingChanges, not in Candidates
    [Fact]
    public async Task GetDonorCandidates_Trimming_ComboRowsInMappingChangesNotInCandidates()
    {
        // 2 combo rows close the gap; 6 extras are the pipeline buffer.
        // Combo rows must appear in MappingChanges; Candidates must not contain them.
        await using var context = CreateContext();
        context.Applications.Add(new Application { Id = 112, Name = "TrimPreserveApp", MinSupportFte = 1.0, RecommendedSupportFte = 0.5 });
        // Custom PaddingApp (recommended=9999) pads each resource to 100% → no idle rows
        context.Applications.Add(new Application { Id = 998, Name = "_PadPE", MinSupportFte = 0, RecommendedSupportFte = 9999 });
        context.Activities.Add(new Activity { Id = 997, Name = "_PadPE - Support", ActivityType = "Support", ApplicationId = 998, Division = "Platform Engineering" });
        await context.SaveChangesAsync();

        var combo1 = MakePeResource("TR32D001", "Preserve Combo1");
        var combo2 = MakePeResource("TR32D002", "Preserve Combo2");
        var extras = Enumerable.Range(3, 6).Select(i => MakePeResource($"TR32D{i:000}", $"Preserve Extra{i}")).ToList();
        context.Resources.Add(combo1);
        context.Resources.Add(combo2);
        context.Resources.AddRange(extras);
        await context.SaveChangesAsync();

        context.Mappings.Add(new Mapping { ResourceId = combo1.Id, ActivityId = 7,   Month = "2026-05", Percent = 60 });
        context.Mappings.Add(new Mapping { ResourceId = combo1.Id, ActivityId = 997, Month = "2026-05", Percent = 40 });
        context.Mappings.Add(new Mapping { ResourceId = combo2.Id, ActivityId = 7,   Month = "2026-05", Percent = 60 });
        context.Mappings.Add(new Mapping { ResourceId = combo2.Id, ActivityId = 997, Month = "2026-05", Percent = 40 });
        foreach (var e in extras)
        {
            context.Mappings.Add(new Mapping { ResourceId = e.Id, ActivityId = 7,   Month = "2026-05", Percent = 10 });
            context.Mappings.Add(new Mapping { ResourceId = e.Id, ActivityId = 997, Month = "2026-05", Percent = 90 });
        }
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(112, null, "2026-05");

        Assert.NotNull(result);
        Assert.NotNull(result.FewestChangesCombination);
        var mappingChanges = result.FewestChangesCombination.MappingChanges;
        Assert.Equal(result.FewestChangesCombination.RowCount, mappingChanges.Count);
        Assert.Contains(mappingChanges, s => s.Contains("Preserve Combo1"));
        Assert.Contains(mappingChanges, s => s.Contains("Preserve Combo2"));
        // After item 5: combo rows appear in Candidates; extras do not.
        Assert.Contains(result.Candidates, c => c.ResourceName == "Preserve Combo1");
        Assert.Contains(result.Candidates, c => c.ResourceName == "Preserve Combo2");
        Assert.DoesNotContain(result.Candidates, c => c.ResourceName is "Preserve Extra3" or "Preserve Extra4");
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
        // Custom PaddingApp (recommended=9999) pads each donor to 100% → no idle rows
        context.Applications.Add(new Application { Id = 998, Name = "_PadPE", MinSupportFte = 0, RecommendedSupportFte = 9999 });
        context.Activities.Add(new Activity { Id = 997, Name = "_PadPE - Support", ActivityType = "Support", ApplicationId = 998, Division = "Platform Engineering" });
        await context.SaveChangesAsync();
        var donors = Enumerable.Range(1, 12).Select(i => MakePeResource($"TR32E{i:000}", $"NFC Donor {i}")).ToList();
        context.Resources.AddRange(donors);
        await context.SaveChangesAsync();
        foreach (var d in donors)
        {
            context.Mappings.Add(new Mapping { ResourceId = d.Id, ActivityId = 7,   Month = "2026-05", Percent = 10 });
            context.Mappings.Add(new Mapping { ResourceId = d.Id, ActivityId = 997, Month = "2026-05", Percent = 90 });
        }
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(4, null, "2026-05"); // AuthService

        Assert.NotNull(result);
        Assert.Null(result.FewestChangesCombination);
        Assert.Empty(result.Candidates);
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
        // Custom PaddingApp (recommended=9999) pads each donor to 100% → no idle rows
        context.Applications.Add(new Application { Id = 998, Name = "_PadPE", MinSupportFte = 0, RecommendedSupportFte = 9999 });
        context.Activities.Add(new Activity { Id = 997, Name = "_PadPE - Support", ActivityType = "Support", ApplicationId = 998, Division = "Platform Engineering" });
        await context.SaveChangesAsync();
        var donors = Enumerable.Range(1, 3).Select(i => MakePeResource($"TR32A{i:000}", $"Small Donor {i}")).ToList();
        context.Resources.AddRange(donors);
        await context.SaveChangesAsync();
        foreach (var d in donors)
        {
            context.Mappings.Add(new Mapping { ResourceId = d.Id, ActivityId = 7,   Month = "2026-05", Percent = 10 });
            context.Mappings.Add(new Mapping { ResourceId = d.Id, ActivityId = 997, Month = "2026-05", Percent = 90 });
        }
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(4, null, "2026-05");

        Assert.NotNull(result);
        Assert.Empty(result.Candidates);
        Assert.Equal(3, result.TotalAvailableCandidatesCount);
    }

    // ── Issue #34: pre-rendered SummaryParagraph ─────────────────────────────

    // Cycle 1 (tracer) — paragraph contains RowCount and DistinctResourceCount as substrings
    [Fact]
    public async Task GetDonorCandidates_SummaryParagraph_ContainsHeadlineCounts()
    {
        // 2 donors at 100% Minor Changes (Activity 35) close a 1.5 FTE gap on LRTestApp
        // RowCount=2, DistinctResourceCount=2 → both must appear in the paragraph
        await using var context = CreateContext();
        context.Applications.Add(new Application { Id = 101, Name = "LRTestApp", MinSupportFte = 1.5, RecommendedSupportFte = 0.5 });
        context.Activities.Add(new Activity { Id = 201, Name = "LRTestApp - Support", ActivityType = "Support", ApplicationId = 101, Division = "Platform Engineering" });
        await context.SaveChangesAsync();

        var donorA = MakePeResource("SP34A001", "SP Donor A");
        var donorB = MakePeResource("SP34A002", "SP Donor B");
        context.Resources.AddRange(donorA, donorB);
        await context.SaveChangesAsync();
        context.Mappings.AddRange(
            new Mapping { ResourceId = donorA.Id, ActivityId = 35, Month = "2026-05", Percent = 100 },
            new Mapping { ResourceId = donorB.Id, ActivityId = 35, Month = "2026-05", Percent = 100 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(101, null, "2026-05");

        Assert.NotNull(result.FewestChangesCombination);
        var para = result.FewestChangesCombination.SummaryParagraph;
        Assert.NotNull(para);
        Assert.Contains(result.FewestChangesCombination.RowCount.ToString(), para);
        Assert.Contains(result.FewestChangesCombination.DistinctResourceCount.ToString(), para);
    }

    // Cycle 2 — FTE formatted to one decimal place
    [Fact]
    public async Task GetDonorCandidates_SummaryParagraph_FtePrecisionOneDecimal()
    {
        // 2 donors at 100% Minor Changes close a 1.5 FTE gap → TotalFte = 1.5 → "1.5 FTE"
        await using var context = CreateContext();
        context.Applications.Add(new Application { Id = 101, Name = "LRTestApp", MinSupportFte = 1.5, RecommendedSupportFte = 0.5 });
        context.Activities.Add(new Activity { Id = 201, Name = "LRTestApp - Support", ActivityType = "Support", ApplicationId = 101, Division = "Platform Engineering" });
        await context.SaveChangesAsync();

        var donorA = MakePeResource("SP34B001", "FTE Donor A");
        var donorB = MakePeResource("SP34B002", "FTE Donor B");
        context.Resources.AddRange(donorA, donorB);
        await context.SaveChangesAsync();
        context.Mappings.AddRange(
            new Mapping { ResourceId = donorA.Id, ActivityId = 35, Month = "2026-05", Percent = 100 },
            new Mapping { ResourceId = donorB.Id, ActivityId = 35, Month = "2026-05", Percent = 50 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(101, null, "2026-05");

        Assert.NotNull(result.FewestChangesCombination);
        var para = result.FewestChangesCombination.SummaryParagraph;
        Assert.NotNull(para);
        var expectedFte = result.FewestChangesCombination.TotalFte.ToString("F1");
        Assert.Contains(expectedFte + " FTE", para);
    }

    // Cycle 3 — monthly cost rounded to nearest $1k with k suffix
    [Fact]
    public async Task GetDonorCandidates_SummaryParagraph_CostRoundedToNearestK()
    {
        // MakePeResource uses MonthlyRate = 9000. 1 donor at 100% Minor Changes closes a 0.9 FTE gap.
        // ImpliedMonthlyCost = 9000 * 100% = 9000 → rounds to $9k/month
        await using var context = CreateContext();
        // MinSupportFte = 0.9 → gap = 0.9 FTE → 1 donor at 100% (1.0 FTE) closes it
        context.Applications.Add(new Application { Id = 101, Name = "LRTestApp", MinSupportFte = 0.9, RecommendedSupportFte = 0.0 });
        context.Activities.Add(new Activity { Id = 201, Name = "LRTestApp - Support", ActivityType = "Support", ApplicationId = 101, Division = "Platform Engineering" });
        await context.SaveChangesAsync();

        var donor = MakePeResource("SP34C001", "Cost Donor");
        context.Resources.Add(donor);
        await context.SaveChangesAsync();
        context.Mappings.Add(new Mapping { ResourceId = donor.Id, ActivityId = 35, Month = "2026-05", Percent = 100 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(101, null, "2026-05");

        Assert.NotNull(result.FewestChangesCombination);
        var para = result.FewestChangesCombination.SummaryParagraph;
        Assert.NotNull(para);
        var costK = (int)Math.Round(result.FewestChangesCombination.TotalMonthlyCost / 1000m);
        Assert.Contains($"${costK}k/month", para);
    }

    // Cycle 4 — single-tier solution names tier, does not use "Tiers" plural
    [Fact]
    public async Task GetDonorCandidates_SummaryParagraph_SingleTierLabel()
    {
        // 2 donors at 100% Minor Changes (Activity 35, Tier 3, same div)
        // → all combo rows in Tier 3 → "all Tier 3" should appear, "Tiers 3–" should not
        await using var context = CreateContext();
        context.Applications.Add(new Application { Id = 101, Name = "LRTestApp", MinSupportFte = 1.5, RecommendedSupportFte = 0.5 });
        context.Activities.Add(new Activity { Id = 201, Name = "LRTestApp - Support", ActivityType = "Support", ApplicationId = 101, Division = "Platform Engineering" });
        await context.SaveChangesAsync();

        var donorA = MakePeResource("SP34D001", "T2 Single A");
        var donorB = MakePeResource("SP34D002", "T2 Single B");
        context.Resources.AddRange(donorA, donorB);
        await context.SaveChangesAsync();
        context.Mappings.AddRange(
            new Mapping { ResourceId = donorA.Id, ActivityId = 35, Month = "2026-05", Percent = 100 },
            new Mapping { ResourceId = donorB.Id, ActivityId = 35, Month = "2026-05", Percent = 100 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(101, null, "2026-05");

        Assert.NotNull(result.FewestChangesCombination);
        var para = result.FewestChangesCombination.SummaryParagraph;
        Assert.NotNull(para);
        Assert.Contains("all Tier 3", para);
        Assert.DoesNotContain("Tiers 3", para);
    }

    // Cycle 5 — multi-tier solution names the range
    [Fact]
    public async Task GetDonorCandidates_SummaryParagraph_MultiTierRange()
    {
        // 2 PE donors at 100% on BigSurplusApp (recommended=0) → 2.0 FTE Tier-2 surplus Support
        // 2 PE donors at 100% Minor Changes (Activity 35) → 2.0 FTE Tier-3
        // AuthService gap = 4.0 FTE → all 4 rows needed; combo spans Tiers 2–3
        await using var context = CreateContext();
        context.Applications.Add(new Application { Id = 100, Name = "BigSurplusApp", MinSupportFte = 0.0, RecommendedSupportFte = 0.0 });
        context.Activities.Add(new Activity { Id = 200, Name = "BigSurplusApp - Support", ActivityType = "Support", ApplicationId = 100, Division = "Platform Engineering" });
        await context.SaveChangesAsync();

        var t1a = MakePeResource("SP34E001", "MT Tier1 A");
        var t1b = MakePeResource("SP34E002", "MT Tier1 B");
        var t2a = MakePeResource("SP34E003", "MT Tier2 A");
        var t2b = MakePeResource("SP34E004", "MT Tier2 B");
        context.Resources.AddRange(t1a, t1b, t2a, t2b);
        await context.SaveChangesAsync();
        context.Mappings.AddRange(
            new Mapping { ResourceId = t1a.Id, ActivityId = 200, Month = "2026-05", Percent = 100 },
            new Mapping { ResourceId = t1b.Id, ActivityId = 200, Month = "2026-05", Percent = 100 },
            new Mapping { ResourceId = t2a.Id, ActivityId = 35,  Month = "2026-05", Percent = 100 },
            new Mapping { ResourceId = t2b.Id, ActivityId = 35,  Month = "2026-05", Percent = 100 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(4, null, "2026-05");

        Assert.NotNull(result.FewestChangesCombination);
        var para = result.FewestChangesCombination.SummaryParagraph;
        Assert.NotNull(para);
        Assert.Contains("Tiers 2", para);
        Assert.Contains("3", para);
    }

    // Cycle 6 — surplus exhaustion clause appears when tier-1 FTE == SurplusSupportFteAvailable
    [Fact]
    public async Task GetDonorCandidates_SummaryParagraph_SurplusExhaustionClause()
    {
        // 4 PE donors at 100% on BigSurplusApp (recommended=0) → surplus = 4.0 FTE
        // AuthService gap = 4.0 FTE → fewest changes consumes all surplus
        // SurplusSupportFteAvailable = 4.0 and Tier-2 FTE = 4.0 → "exhausts" must appear
        await using var context = CreateContext();
        context.Applications.Add(new Application { Id = 100, Name = "BigSurplusApp", MinSupportFte = 0.0, RecommendedSupportFte = 0.0 });
        context.Activities.Add(new Activity { Id = 200, Name = "BigSurplusApp - Support", ActivityType = "Support", ApplicationId = 100, Division = "Platform Engineering" });
        await context.SaveChangesAsync();

        var donors = Enumerable.Range(1, 4).Select(i => MakePeResource($"SP34F{i:000}", $"Exhaust Donor {i}")).ToList();
        context.Resources.AddRange(donors);
        await context.SaveChangesAsync();
        foreach (var d in donors)
            context.Mappings.Add(new Mapping { ResourceId = d.Id, ActivityId = 200, Month = "2026-05", Percent = 100 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(4, null, "2026-05");

        Assert.NotNull(result.FewestChangesCombination);
        var para = result.FewestChangesCombination.SummaryParagraph;
        Assert.NotNull(para);
        Assert.Contains("exhausts", para);
    }

    // Cycle 7 — no exhaustion clause when surplus is not fully consumed
    [Fact]
    public async Task GetDonorCandidates_SummaryParagraph_NoExhaustionClause_WhenSurplusPartial()
    {
        // Tier-3-only solution (Minor Changes): no Tier-2 surplus rows → exhaustion clause must not appear
        await using var context = CreateContext();
        context.Applications.Add(new Application { Id = 101, Name = "LRTestApp", MinSupportFte = 1.5, RecommendedSupportFte = 0.5 });
        context.Activities.Add(new Activity { Id = 201, Name = "LRTestApp - Support", ActivityType = "Support", ApplicationId = 101, Division = "Platform Engineering" });
        await context.SaveChangesAsync();

        var donorA = MakePeResource("SP34G001", "NoExhaust A");
        var donorB = MakePeResource("SP34G002", "NoExhaust B");
        context.Resources.AddRange(donorA, donorB);
        await context.SaveChangesAsync();
        context.Mappings.AddRange(
            new Mapping { ResourceId = donorA.Id, ActivityId = 35, Month = "2026-05", Percent = 100 },
            new Mapping { ResourceId = donorB.Id, ActivityId = 35, Month = "2026-05", Percent = 100 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(101, null, "2026-05");

        Assert.NotNull(result.FewestChangesCombination);
        var para = result.FewestChangesCombination.SummaryParagraph;
        Assert.NotNull(para);
        Assert.DoesNotContain("exhausts", para);
    }

    // Cycle 8 — tier explanation is always present and names the target application
    [Fact]
    public async Task GetDonorCandidates_SummaryParagraph_TierExplanationAlwaysPresent()
    {
        // Any fewest-changes solution must include the tier-system explanation naming the target app
        await using var context = CreateContext();
        // MinSupportFte = 0.9 → gap = 0.9 FTE → 1 donor at 100% (1.0 FTE) closes it
        context.Applications.Add(new Application { Id = 101, Name = "LRTestApp", MinSupportFte = 0.9, RecommendedSupportFte = 0.0 });
        context.Activities.Add(new Activity { Id = 201, Name = "LRTestApp - Support", ActivityType = "Support", ApplicationId = 101, Division = "Platform Engineering" });
        await context.SaveChangesAsync();

        var donor = MakePeResource("SP34H001", "TierExp Donor");
        context.Resources.Add(donor);
        await context.SaveChangesAsync();
        context.Mappings.Add(new Mapping { ResourceId = donor.Id, ActivityId = 35, Month = "2026-05", Percent = 100 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(101, null, "2026-05");

        Assert.NotNull(result.FewestChangesCombination);
        var para = result.FewestChangesCombination.SummaryParagraph;
        Assert.NotNull(para);
        Assert.Contains("Tier system:", para);
        Assert.Contains("LRTestApp", para);
        Assert.Contains("tiers 5", para);
        Assert.Contains("cross-divisional alignment", para);
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
        // Activity 35 = SurveyEngine - Minor Changes (PE); donorA at 100% sorts first (larger FTE)
        // Activity 6 = ObsStack Support pads donorB to 100% → no idle FTE from donorB
        context.Mappings.AddRange(
            new Mapping { ResourceId = donorA.Id, ActivityId = 35, Month = "2026-05", Percent = 100 },
            new Mapping { ResourceId = donorB.Id, ActivityId = 35, Month = "2026-05", Percent = 80 },
            new Mapping { ResourceId = donorB.Id, ActivityId = 6,  Month = "2026-05", Percent = 20 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(101, null, "2026-05");

        Assert.NotNull(result);
        Assert.NotNull(result.FewestChangesCombination);
        var changes = result.FewestChangesCombination.MappingChanges;
        Assert.Equal(2, changes.Count);
        // Order Donor A (100% FTE) sorts first, Order Donor B (80% FTE) second
        Assert.Contains("Order Donor A", changes[0]);
        Assert.Contains("Order Donor B", changes[1]);
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
        // Trim-last: all 4 non-stopping donors keep DonablePercent = 100 (full donatable);
        // stopping donor also gets 100 (remainingGap = 4.0-3.0 = 1.0 FTE = 100%).
        // Reduce clause: 100 - 100 = 0 → "from 100% to 0%".
        Assert.All(result.FewestChangesCombination.MappingChanges, s =>
        {
            Assert.Contains("reduce", s);
            Assert.Contains("100%", s);
            Assert.Contains("0%", s); // 100 - 100 = 0
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

    // ── Item 5: drop buffer rows ─────────────────────────────────────────────

    [Fact]
    public async Task GetDonorCandidates_WithCombo_CandidatesCount_EqualsComboRowCount()
    {
        // Combo of 2 rows. After item 5: Candidates contains exactly those 2 rows.
        await using var context = CreateContext();
        context.Applications.Add(new Application { Id = 201, Name = "NoBufApp", MinSupportFte = 1.0, RecommendedSupportFte = 0.5 });
        context.Applications.Add(new Application { Id = 999, Name = "_NoBufPad", MinSupportFte = 0, RecommendedSupportFte = 9999 });
        context.Activities.Add(new Activity { Id = 996, Name = "_NoBufPad - Support", ActivityType = "Support", ApplicationId = 999, Division = "Platform Engineering" });
        await context.SaveChangesAsync();

        var c1 = MakePeResource("NB001", "NoBuf C1");
        var c2 = MakePeResource("NB002", "NoBuf C2");
        var extra = MakePeResource("NB003", "NoBuf Extra");
        context.Resources.AddRange(c1, c2, extra);
        await context.SaveChangesAsync();

        context.Mappings.AddRange(
            new Mapping { ResourceId = c1.Id,    ActivityId = 7,   Month = "2026-05", Percent = 60 },
            new Mapping { ResourceId = c1.Id,    ActivityId = 996, Month = "2026-05", Percent = 40 },
            new Mapping { ResourceId = c2.Id,    ActivityId = 7,   Month = "2026-05", Percent = 60 },
            new Mapping { ResourceId = c2.Id,    ActivityId = 996, Month = "2026-05", Percent = 40 },
            new Mapping { ResourceId = extra.Id, ActivityId = 7,   Month = "2026-05", Percent = 10 },
            new Mapping { ResourceId = extra.Id, ActivityId = 996, Month = "2026-05", Percent = 90 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(201, null, "2026-05");

        Assert.NotNull(result);
        Assert.NotNull(result.FewestChangesCombination);
        Assert.Equal(result.FewestChangesCombination!.RowCount, result.Candidates.Count);
        Assert.Equal(2, result.Candidates.Count);
    }

    [Fact]
    public async Task GetDonorCandidates_NoCombo_CandidatesCount_IsZero()
    {
        // Gap too large to close: no fewest-changes combination. After item 5: Candidates = empty.
        await using var context = CreateContext();
        context.Applications.Add(new Application { Id = 999, Name = "_NoBufPad", MinSupportFte = 0, RecommendedSupportFte = 9999 });
        context.Activities.Add(new Activity { Id = 996, Name = "_NoBufPad - Support", ActivityType = "Support", ApplicationId = 999, Division = "Platform Engineering" });
        await context.SaveChangesAsync();
        var donor = MakePeResource("NB_NC001", "NoCombo Donor");
        context.Resources.Add(donor);
        await context.SaveChangesAsync();
        context.Mappings.AddRange(
            new Mapping { ResourceId = donor.Id, ActivityId = 7,   Month = "2026-05", Percent = 10 },
            new Mapping { ResourceId = donor.Id, ActivityId = 996, Month = "2026-05", Percent = 90 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);
        var result = await svc.GetDonorCandidatesAsync(4, null, "2026-05"); // AuthService — huge gap

        Assert.NotNull(result);
        Assert.Null(result.FewestChangesCombination);
        Assert.Empty(result.Candidates);
    }

    // ── GetAtRiskApplicationFixAsync (Item 3) ───────────────────────────────

    [Fact]
    public async Task GetAtRiskApplicationFix_Application_MatchesGetApplicationsAsync()
    {
        await using var context = CreateContext();
        var svc = new DataService(context);

        var apps = await svc.GetApplicationsAsync("2026-05", applicationId: 4);
        var fix  = await svc.GetAtRiskApplicationFixAsync(4, null, "2026-05");

        Assert.NotNull(fix);
        Assert.Equivalent(apps.First(), fix.application);
    }

    [Fact]
    public async Task GetAtRiskApplicationFix_FewestChangesCombination_MatchesGetDonorCandidatesAsync()
    {
        await using var context = CreateContext();

        // Create a small app whose gap one donor can close
        context.Applications.Add(new Application { Id = 620, Name = "FixCombo App", MinSupportFte = 0.5, RecommendedSupportFte = 1.0 });
        context.Activities.Add(new Activity { Id = 6200, Name = "FixCombo App - Support", ActivityType = "Support", ApplicationId = 620, Division = "Platform Engineering" });
        await context.SaveChangesAsync();

        var donor = MakePeResource("FC001", "FixCombo Donor");
        context.Resources.Add(donor);
        await context.SaveChangesAsync();
        context.Mappings.AddRange(
            new Mapping { ResourceId = donor.Id, ActivityId = 6200, Month = "2026-05", Percent = 50 },
            new Mapping { ResourceId = donor.Id, ActivityId = 6,    Month = "2026-05", Percent = 50 });
        await context.SaveChangesAsync();

        var svc = new DataService(context);

        var donor_result = await svc.GetDonorCandidatesAsync(620, null, "2026-05");
        var fix          = await svc.GetAtRiskApplicationFixAsync(620, null, "2026-05");

        Assert.NotNull(fix);
        Assert.Equivalent(donor_result?.FewestChangesCombination, fix.fewest_changes_combination);
    }

    [Fact]
    public async Task GetAtRiskApplicationFix_ReturnsNull_ForUnknownApplication()
    {
        await using var context = CreateContext();
        var svc = new DataService(context);

        var result = await svc.GetAtRiskApplicationFixAsync(99999, null, "2026-05");

        Assert.Null(result);
    }
}
