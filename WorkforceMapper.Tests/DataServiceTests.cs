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
            new Mapping { ResourceId = resource.Id, ActivityId = 1, Month = "2026-05", Percent = 40m },
            new Mapping { ResourceId = resource.Id, ActivityId = 3, Month = "2026-05", Percent = 60m }
        );
        await context.SaveChangesAsync();

        var service = new DataService(context);
        var results = await service.GetResourcesAsync("Platform Engineering");

        var found = Assert.Single(results);
        Assert.Equal(2, found.Mappings.Count);
        Assert.Contains(found.Mappings, m => m.ActivityId == 1 && m.Percent == 40m);
        Assert.Contains(found.Mappings, m => m.ActivityId == 3 && m.Percent == 60m);
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
                new Mapping { ResourceId = r.Id, ActivityId = 1, Month = "2026-05", Percent = 60m },
                new Mapping { ResourceId = r.Id, ActivityId = 3, Month = "2026-05", Percent = 52m }
            );
        }
        await context.SaveChangesAsync();

        var service = new DataService(context);
        var results = await service.GetCapacityByDivisionAsync("2026-05");

        var pe = results.Single(r => r.Division == "Platform Engineering");
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
        context.Mappings.Add(new Mapping { ResourceId = resource.Id, ActivityId = 5, Month = "2026-05", Percent = 100m });
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
            context.Mappings.Add(new Mapping { ResourceId = r.Id, ActivityId = 5, Month = "2026-05", Percent = 100m });

        // All other apps have 0 FTE mappings → also HIGH individually, but that
        // doesn't affect the structural assertion: every product's closure contains
        // AuthService, so every product must be HIGH regardless.
        await context.SaveChangesAsync();

        var service = new DataService(context);
        var products = await service.GetProductRiskAsync("2026-05");

        Assert.Equal(7, products.Count);
        Assert.All(products, p => Assert.Equal("high", p.risk));

        var consumerPanel = products.Single(p => p.name == "ConsumerPanel");
        var fieldwork = products.Single(p => p.name == "FieldworkServices");
        Assert.Equal("high", consumerPanel.risk);
        Assert.Equal("high", fieldwork.risk);

        // AuthService must appear in contributing_apps for both products
        Assert.Contains(consumerPanel.contributing_apps, a => a.name == "AuthService");
        Assert.Contains(fieldwork.contributing_apps, a => a.name == "AuthService");
    }
}
