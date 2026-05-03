using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WorkforceMapper.Server.Data;
using WorkforceMapper.Server.Models;

namespace WorkforceMapper.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath =
        Path.Combine(Path.GetTempPath(), $"wm_test_{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            var d = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<WorkforceDbContext>));
            if (d != null) services.Remove(d);
            services.AddDbContext<WorkforceDbContext>(o => o.UseSqlite($"Data Source={_dbPath}"));
        });
    }

    public void SeedTestData()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorkforceDbContext>();
        if (db.Resources.Any()) return;

        db.Resources.AddRange(
            MakeResource("PI00001", "Alice", "Platform Engineering"),
            MakeResource("PI00002", "Bob", "Platform Engineering"),
            MakeResource("PI00003", "Carol", "Platform Engineering"),
            MakeResource("PI00004", "Dave", "Analytics"),
            MakeResource("PI00005", "Eve", "Analytics")
        );
        db.SaveChanges();

        var all = db.Resources.ToList();
        foreach (var r in all.Where(r => r.Division == "Platform Engineering"))
            db.Mappings.Add(new Mapping { ResourceId = r.Id, ActivityId = 1, Month = "2026-05", Percent = 60m });
        foreach (var r in all.Where(r => r.Division == "Analytics"))
            db.Mappings.Add(new Mapping { ResourceId = r.Id, ActivityId = 11, Month = "2026-05", Percent = 80m });
        db.SaveChanges();
    }

    private static Resource MakeResource(string rid, string name, string division) => new()
    {
        ResourceId = rid, Name = name, ResourceType = "Employee",
        Company = "Pragmenta", Seniority = "Analyst", RoleType = "Developer",
        Division = division, Country = "UK", MonthlyRate = 9000m,
        Jan = 1, Feb = 1, Mar = 1, Apr = 1, May = 1,
        Jun = 1, Jul = 1, Aug = 1, Sep = 1, Oct = 1, Nov = 1, Dec = 1
    };

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && File.Exists(_dbPath))
            try { File.Delete(_dbPath); } catch { /* best-effort cleanup */ }
    }
}

public class ResourceActivityApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ResourceActivityApiTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        factory.SeedTestData();
    }

    // --- Cycle 1: Health ---

    [Fact]
    public async Task GetHealth_Returns200()
    {
        var response = await _client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ok", body.GetProperty("status").GetString());
        Assert.True(body.GetProperty("resource_count").GetInt32() >= 0);
        Assert.True(body.GetProperty("mapping_count").GetInt32() >= 0);
    }

    // --- Cycle 2: Resources with seeded data ---

    [Fact]
    public async Task GetResources_Returns200_WithSeededData()
    {
        var response = await _client.GetAsync("/api/resources");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(items);
        Assert.Equal(5, items.Length);
        var first = items[0];
        Assert.True(first.TryGetProperty("resource_id", out _));
        Assert.True(first.TryGetProperty("active", out _));
        Assert.True(first.TryGetProperty("mappings", out _));
    }

    // --- Cycle 3: Resources filtered by division ---

    [Fact]
    public async Task GetResources_FiltersByDivision()
    {
        var response = await _client.GetAsync("/api/resources?division=Platform+Engineering");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(items);
        Assert.Equal(3, items.Length);
        Assert.All(items, r =>
            Assert.Equal("Platform Engineering", r.GetProperty("division").GetString()));
    }

    // --- Cycle 4: Activities with seeded data ---

    [Fact]
    public async Task GetActivities_Returns200_WithSeededData()
    {
        var response = await _client.GetAsync("/api/activities");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(items);
        Assert.Equal(33, items.Length);
        var first = items[0];
        Assert.True(first.TryGetProperty("id", out _));
        Assert.True(first.TryGetProperty("activity_type", out _));
        Assert.True(first.TryGetProperty("parent_name", out _));
    }

    // --- Cycle 5: Activities filtered by activity type ---

    [Fact]
    public async Task GetActivities_FiltersByActivityType()
    {
        var response = await _client.GetAsync("/api/activities?activity_type=Project+Changes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(items);
        Assert.True(items.Length > 0);
        Assert.All(items, a =>
            Assert.Equal("Project Changes", a.GetProperty("activity_type").GetString()));
    }
}
