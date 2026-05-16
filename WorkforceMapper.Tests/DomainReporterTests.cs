using Microsoft.EntityFrameworkCore;
using WorkforceMapper.Server.Data;
using WorkforceMapper.Server.Models;
using WorkforceMapper.Server.Services;
using Xunit;

namespace WorkforceMapper.Tests;

public class DomainReporterTests
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

    private static Resource MakePeResource(string id, string name, decimal rate = 10_000m) => new()
    {
        ResourceId = id, Name = name, ResourceType = "Employee",
        Company = "Test Co", Seniority = "Analyst", RoleType = "Developer",
        Division = "Platform Engineering", Country = "UK", MonthlyRate = rate,
        Jan=0,Feb=0,Mar=0,Apr=0,May=1,Jun=0,Jul=0,Aug=0,Sep=0,Oct=0,Nov=0,Dec=0
    };

    // ── ProductCostAttribution ─────────────────────────────────────────────────

    [Fact]
    public async Task ProductCostAttribution_MappingToUnlinkedApp_GoesToUnattributed()
    {
        // Activity 5 = AuthService Support (app 4); app 4 has no ProductApplications
        await using var context = CreateContext();
        var r = MakePeResource("PC00001", "Auth Resource");
        context.Resources.Add(r);
        await context.SaveChangesAsync();
        context.Mappings.Add(new Mapping { ResourceId = r.Id, ActivityId = 5, Month = "2026-05", Percent = 100 });
        await context.SaveChangesAsync();

        var result = await new ProductCostAttribution(context).ComputeAsync("2026-05");

        var unattributed = result.Products.Single(p => p.ProductName == "Unattributed");
        Assert.Equal(10_000m, unattributed.TotalAllocatedCost);
    }

    [Fact]
    public async Task ProductCostAttribution_MappingToLinkedApp_SplitsEvenlyAcrossProducts()
    {
        // Activity 1 = CoreDataWarehouse Support (app 1); app 1 is in products 1,2,3,4,5 → 5-way split
        await using var context = CreateContext();
        var r = MakePeResource("PC00002", "CDW Resource");
        context.Resources.Add(r);
        await context.SaveChangesAsync();
        context.Mappings.Add(new Mapping { ResourceId = r.Id, ActivityId = 1, Month = "2026-05", Percent = 100 });
        await context.SaveChangesAsync();

        var result = await new ProductCostAttribution(context).ComputeAsync("2026-05");

        // 10_000 / 5 products = 2_000 each; no Unattributed line
        Assert.DoesNotContain(result.Products, p => p.ProductName == "Unattributed");
        var attributed = result.Products.Where(p => p.ProductId.HasValue).ToList();
        Assert.Equal(5, attributed.Count);
        Assert.All(attributed, p => Assert.Equal(2_000m, p.TotalAllocatedCost));
    }

    // ── ApplicationRiskReporter ────────────────────────────────────────────────

    [Fact]
    public async Task ApplicationRiskReporter_AppBelowMinCoverage_ReturnsHighRisk()
    {
        // App 1 CoreDataWarehouse: min=2.0; 1 resource at 50% on Activity 1 → coverage=0.5 < 2.0 → high
        await using var context = CreateContext();
        var r = MakePeResource("AR00001", "CDW Resource");
        context.Resources.Add(r);
        await context.SaveChangesAsync();
        context.Mappings.Add(new Mapping { ResourceId = r.Id, ActivityId = 1, Month = "2026-05", Percent = 50 });
        await context.SaveChangesAsync();

        var results = await new ApplicationRiskReporter(context).ReportAsync("2026-05");

        var app1 = results.Single(a => a.id == 1);
        Assert.Equal("high", app1.risk);
    }

    [Fact]
    public async Task ApplicationRiskReporter_FilterByApplicationId_ReturnsSingleApp()
    {
        await using var context = CreateContext();

        var results = await new ApplicationRiskReporter(context).ReportAsync("2026-05", applicationId: 1);

        Assert.Single(results);
        Assert.Equal(1, results[0].id);
    }

    // ── ProductRiskReporter ────────────────────────────────────────────────────

    [Fact]
    public async Task ProductRiskReporter_DirectHighRiskApp_MakesProductHigh()
    {
        // Product 7 (FieldworkServices) includes app 8 (QuotaManager, min=1.0).
        // No support coverage → app 8 is high → product 7 is high.
        await using var context = CreateContext();

        var result = await new ProductRiskReporter(context).ReportAsync("2026-05", productId: 7);

        var product7 = Assert.Single(result.Products);
        Assert.Equal("high", product7.risk);
    }

    [Fact]
    public async Task ProductRiskReporter_AllDirectAppsLow_ButTransitiveDepHigh_ProductIsHigh()
    {
        // Product 7 direct apps: 10 (FieldOps), 8 (QuotaManager), 12 (SampleRouter), 13 (CallCentre Suite).
        // All four depend on AuthService (app 4) transitively.
        // Give every direct app recommended coverage → low; leave AuthService uncovered → high.
        await using var context = CreateContext();

        // FieldOps (app 10, Activity 20, min=2.0, rec=3.0): 3 × 100% = 3.0 FTE ≥ rec
        var fieldOps = Enumerable.Range(1, 3).Select(i => MakePeResource($"PR00{i:00}", $"FieldOps {i}")).ToList();
        // QuotaManager (app 8, Activity 14, min=1.0, rec=1.5): 2 × 100% = 2.0 FTE ≥ rec
        var quotaMgr = Enumerable.Range(1, 2).Select(i => MakePeResource($"PR01{i:00}", $"QuotaMgr {i}")).ToList();
        // SampleRouter (app 12, Activity 22, min=1.0, rec=1.5): 2 × 100% = 2.0 FTE ≥ rec
        var sampleRtr = Enumerable.Range(1, 2).Select(i => MakePeResource($"PR02{i:00}", $"SampleRtr {i}")).ToList();
        // CallCentre Suite (app 13, Activity 23, min=1.0, rec=1.5): 2 × 100% = 2.0 FTE ≥ rec
        var callCentre = Enumerable.Range(1, 2).Select(i => MakePeResource($"PR03{i:00}", $"CallCentre {i}")).ToList();

        context.Resources.AddRange(fieldOps.Concat(quotaMgr).Concat(sampleRtr).Concat(callCentre));
        await context.SaveChangesAsync();

        foreach (var r in fieldOps)
            context.Mappings.Add(new Mapping { ResourceId = r.Id, ActivityId = 20, Month = "2026-05", Percent = 100 });
        foreach (var r in quotaMgr)
            context.Mappings.Add(new Mapping { ResourceId = r.Id, ActivityId = 14, Month = "2026-05", Percent = 100 });
        foreach (var r in sampleRtr)
            context.Mappings.Add(new Mapping { ResourceId = r.Id, ActivityId = 22, Month = "2026-05", Percent = 100 });
        foreach (var r in callCentre)
            context.Mappings.Add(new Mapping { ResourceId = r.Id, ActivityId = 23, Month = "2026-05", Percent = 100 });
        await context.SaveChangesAsync();
        // AuthService (app 4, Activity 5): no support coverage → high risk

        var result = await new ProductRiskReporter(context).ReportAsync("2026-05", productId: 7);

        var product7 = Assert.Single(result.Products);
        Assert.Equal("high", product7.risk);
    }

    // ── DonorCandidateAlgorithm ────────────────────────────────────────────────

    [Fact]
    public async Task DonorCandidateAlgorithm_UnknownApplicationName_ReturnsNull()
    {
        await using var context = CreateContext();

        var result = await new DonorCandidateAlgorithm(context).FindAsync(null, "DoesNotExist", "2026-05");

        Assert.Null(result);
    }
}
