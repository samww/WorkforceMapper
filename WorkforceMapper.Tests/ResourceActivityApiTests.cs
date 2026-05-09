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
            MakeResource("PI00005", "Eve", "Analytics"),
            MakeResource("PI00006", "Frank", "Platform Engineering", jun: 0)
        );
        db.SaveChanges();

        var all = db.Resources.ToList();
        foreach (var r in all.Where(r => r.Division == "Platform Engineering"))
            db.Mappings.Add(new Mapping { ResourceId = r.Id, ActivityId = 1, Month = "2026-05", Percent = 60m });
        foreach (var r in all.Where(r => r.Division == "Analytics"))
            db.Mappings.Add(new Mapping { ResourceId = r.Id, ActivityId = 11, Month = "2026-05", Percent = 80m });
        db.SaveChanges();
    }

    private static Resource MakeResource(string rid, string name, string division, int jun = 1) => new()
    {
        ResourceId = rid, Name = name, ResourceType = "Employee",
        Company = "Pragmenta", Seniority = "Analyst", RoleType = "Developer",
        Division = division, Country = "UK", MonthlyRate = 9000m,
        Jan = 1, Feb = 1, Mar = 1, Apr = 1, May = 1,
        Jun = jun, Jul = 1, Aug = 1, Sep = 1, Oct = 1, Nov = 1, Dec = 1
    };

    // Support activity IDs from HasData seeding:
    //  1=CDW-Sup, 3=APIGateway-Sup, 4=SurveyEngine-Sup, 5=AuthService-Sup, 6=ObsStack-Sup
    // 11=InsightHub-Sup, 13=DataMapper-Sup, 14=QuotaManager-Sup, 15=SegmentBuilder-Sup
    // 16=PanelIQ-Sup, 20=FieldOps-Sup, 21=WebCollect-Sup, 22=SampleRouter-Sup
    // 23=CallCentre-Sup, 24=DiaryTracker-Sup, 28=ClientPortal-Sup, 29=ReportFactory-Sup, 31=DeliveryTracker-Sup
    //
    // Calibration for 2026-05:
    //   AuthService   (app 4, act 5):  3 @ 100% = 3.0 FTE  [min=4.0 → HIGH]
    //   SurveyEngine  (app 3, act 4):  2 @ 100% = 2.0 FTE  [min=2.0, rec=3.0 → MEDIUM]
    //   PanelIQ       (app 18, act 16): 2 @ 100% = 2.0 FTE [min=2.0, rec=3.0 → MEDIUM]
    //   DataMapper    (app 7, act 13): 2 @ 100% = 2.0 FTE  [min=1.5, rec=2.5 → MEDIUM]
    //   Others: each mapped at >= rec → LOW
    private static readonly (int activityId, int count, decimal pct)[] _applicationSeedMappings =
    [
        (5,  3, 100m), // AuthService: 3.0 FTE  → HIGH
        (4,  2, 100m), // SurveyEngine: 2.0 FTE → MEDIUM
        (16, 2, 100m), // PanelIQ: 2.0 FTE      → MEDIUM
        (13, 2, 100m), // DataMapper: 2.0 FTE    → MEDIUM
        (1,  3, 100m), // CDW: 3.0 FTE           → LOW (rec=3.0)
        (3,  3, 100m), // APIGateway: 3.0 FTE    → LOW (rec=2.5)
        (6,  2, 100m), // ObsStack: 2.0 FTE      → LOW (rec=2.0)
        (11, 3, 100m), // InsightHub: 3.0 FTE    → LOW (rec=2.5)
        (14, 2, 100m), // QuotaManager: 2.0 FTE  → LOW (rec=1.5)
        (15, 2, 100m), // SegmentBuilder: 2.0 FTE → LOW (rec=2.0)
        (20, 3, 100m), // FieldOps: 3.0 FTE      → LOW (rec=3.0)
        (21, 3, 100m), // WebCollect: 3.0 FTE    → LOW (rec=2.5)
        (22, 2, 100m), // SampleRouter: 2.0 FTE  → LOW (rec=1.5)
        (23, 2, 100m), // CallCentre: 2.0 FTE    → LOW (rec=1.5)
        (24, 2, 100m), // DiaryTracker: 2.0 FTE  → LOW (rec=2.0)
        (28, 2, 100m), // ClientPortal: 2.0 FTE  → LOW (rec=2.0)
        (29, 2, 100m), // ReportFactory: 2.0 FTE → LOW (rec=1.5)
        (31, 2, 100m), // DeliveryTracker: 2.0 FTE → LOW (rec=1.5)
    ];

    public void SeedApplicationsTestData()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorkforceDbContext>();
        if (db.Resources.Any()) return;

        var counter = 0;
        foreach (var (activityId, count, pct) in _applicationSeedMappings)
        {
            for (var i = 0; i < count; i++)
            {
                counter++;
                var rid = $"AP{counter:00000}";
                var resource = new Resource
                {
                    ResourceId = rid, Name = $"Resource {rid}", ResourceType = "Employee",
                    Company = "Pragmenta", Seniority = "Analyst", RoleType = "Developer",
                    Division = "Platform Engineering", Country = "UK", MonthlyRate = 9000m,
                    Jan = 1, Feb = 1, Mar = 1, Apr = 1, May = 1,
                    Jun = 1, Jul = 1, Aug = 1, Sep = 1, Oct = 1, Nov = 1, Dec = 1
                };
                db.Resources.Add(resource);
                db.SaveChanges();
                db.Mappings.Add(new Mapping { ResourceId = resource.Id, ActivityId = activityId, Month = "2026-05", Percent = pct });
            }
        }
        db.SaveChanges();
    }

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
        Assert.Equal(6, items.Length);
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
        Assert.Equal(4, items.Length);
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
        Assert.Equal(48, items.Length);
        var first = items[0];
        Assert.True(first.TryGetProperty("id", out _));
        Assert.True(first.TryGetProperty("activity_type", out _));
        Assert.True(first.TryGetProperty("parent_name", out _));
    }

    // --- Cycle 5: Activities filtered by activity type ---

    [Fact]
    public async Task GetActivities_FiltersByActivityType()
    {
        var response = await _client.GetAsync("/api/activities?activity_type=Project");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(items);
        Assert.True(items.Length > 0);
        Assert.All(items, a =>
            Assert.Equal("Project", a.GetProperty("activity_type").GetString()));
    }
}
