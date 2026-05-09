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
}
