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
}
