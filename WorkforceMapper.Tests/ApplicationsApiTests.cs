using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using WorkforceMapper.Server.Data;
using WorkforceMapper.Server.Models;

namespace WorkforceMapper.Tests;

public class ApplicationsApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ApplicationsApiTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        factory.SeedApplicationsTestData();
    }

    private async Task<JsonElement[]> GetAppsAsync()
    {
        var response = await _client.GetAsync("/api/applications?month=2026-05");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement[]>())!;
    }

    [Fact]
    public async Task GetApplications_Returns200_WithSeededData()
    {
        var apps = await GetAppsAsync();
        Assert.Equal(18, apps.Length);
    }

    [Fact]
    public async Task GetApplications_AuthService_Is_HighRisk_For_2026_05()
    {
        var apps = await GetAppsAsync();
        var authService = apps.Single(a => a.GetProperty("name").GetString() == "AuthService");
        Assert.Equal("high", authService.GetProperty("risk").GetString());
    }

    [Fact]
    public async Task GetApplications_ThreeAppsAreMediumRisk_For_2026_05()
    {
        var apps = await GetAppsAsync();
        var mediumApps = apps.Where(a => a.GetProperty("risk").GetString() == "medium").ToList();
        Assert.Equal(3, mediumApps.Count);
        var mediumNames = mediumApps.Select(a => a.GetProperty("name").GetString()).ToHashSet();
        Assert.Contains("SurveyEngine", mediumNames);
        Assert.Contains("PanelIQ", mediumNames);
        Assert.Contains("DataMapper", mediumNames);
    }

    [Fact]
    public async Task GetApplications_AllOthersAreLowRisk_For_2026_05()
    {
        var apps = await GetAppsAsync();
        var notLow = apps.Where(a =>
            a.GetProperty("risk").GetString() != "low" &&
            a.GetProperty("name").GetString() != "AuthService" &&
            a.GetProperty("name").GetString() != "SurveyEngine" &&
            a.GetProperty("name").GetString() != "PanelIQ" &&
            a.GetProperty("name").GetString() != "DataMapper"
        ).ToList();
        Assert.Empty(notLow);
    }

    [Fact]
    public async Task GetApplications_CoverageMatchesSumOfActiveSupportMappings()
    {
        var apps = await GetAppsAsync();
        // AuthService: 3 resources at 100% each = 3.0 FTE (seed defined in factory)
        var authService = apps.Single(a => a.GetProperty("name").GetString() == "AuthService");
        var coverage = authService.GetProperty("current_coverage_fte").GetDouble();
        Assert.Equal(3.0, coverage, precision: 5);
    }

    [Fact]
    public async Task GetApplications_IncludesDependentProducts()
    {
        var apps = await GetAppsAsync();
        // SurveyEngine (app 3) is in product_applications for products 3 and 4
        var survey = apps.Single(a => a.GetProperty("name").GetString() == "SurveyEngine");
        var products = survey.GetProperty("dependent_products").EnumerateArray().ToList();
        Assert.NotEmpty(products);
        Assert.All(products, p =>
        {
            Assert.True(p.TryGetProperty("name", out _));
            Assert.True(p.TryGetProperty("arr_usd", out _));
        });
    }

    // ── Item 4: pre-rendered risk_reason and dependent_products_summary ───────

    [Fact]
    public async Task GetApplications_AuthService_RiskReason_MatchesHighTemplate()
    {
        // coverage=3.0, min=4.0, rec=5.0, gap=1.0
        var apps = await GetAppsAsync();
        var auth = apps.Single(a => a.GetProperty("name").GetString() == "AuthService");
        var reason = auth.GetProperty("risk_reason").GetString();
        Assert.Equal(
            "Coverage 3.0 FTE is 1.0 FTE below the minimum 4.0 FTE (recommended 5.0); risk classification: high.",
            reason);
    }

    [Fact]
    public async Task GetApplications_AuthService_DependentProductsSummary_ListsSevenProducts()
    {
        // All 7 products ($28.9M ARR) transitively depend on AuthService
        var apps = await GetAppsAsync();
        var auth = apps.Single(a => a.GetProperty("name").GetString() == "AuthService");
        var summary = auth.GetProperty("dependent_products_summary").GetString();
        Assert.Contains("$28.9M ARR across 7 products", summary);
        // Alphabetically first and last
        Assert.Contains("BrandTracking Platform", summary);
        Assert.Contains("RetailInsights", summary);
    }

    [Fact]
    public async Task GetApplications_DataMapper_RiskReason_MatchesMediumTemplate()
    {
        // coverage=2.0, min=1.5, rec=2.5, belowRec=0.5
        var apps = await GetAppsAsync();
        var app = apps.Single(a => a.GetProperty("name").GetString() == "DataMapper");
        var reason = app.GetProperty("risk_reason").GetString();
        Assert.Equal(
            "Coverage 2.0 FTE meets the minimum 1.5 FTE but is 0.5 FTE below the recommended 2.5 FTE; risk classification: medium.",
            reason);
    }

    [Fact]
    public async Task GetApplications_CoreDataWarehouse_RiskReason_MatchesLowTemplate()
    {
        // coverage=3.0, rec=3.0 → low
        var apps = await GetAppsAsync();
        var app = apps.Single(a => a.GetProperty("name").GetString() == "CoreDataWarehouse");
        var reason = app.GetProperty("risk_reason").GetString();
        Assert.Equal(
            "Coverage 3.0 FTE is at or above the recommended 3.0 FTE; risk classification: low.",
            reason);
    }
}
