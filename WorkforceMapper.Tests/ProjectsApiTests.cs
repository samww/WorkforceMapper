using System.Net.Http.Json;
using System.Text.Json;

namespace WorkforceMapper.Tests;

public class ProjectsApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ProjectsApiTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        factory.SeedProjectsTestData();
    }

    private async Task<JsonElement[]> GetProjectsAsync()
    {
        var response = await _client.GetAsync("/api/projects?month=2026-05");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement[]>())!;
    }

    // ── Cycle 4a ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetProjects_Returns200_WithAll12Projects()
    {
        var projects = await GetProjectsAsync();
        Assert.Equal(12, projects.Length);
    }

    // ── Cycle 4b ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetProjects_SeededProject_ReturnsCorrectAllocationFte()
    {
        var projects = await GetProjectsAsync();
        // Activity 7 = P01 at 50% → 0.5 FTE
        var p01 = projects.Single(p => p.GetProperty("name").GetString()!.StartsWith("P01"));
        Assert.Equal(0.50, p01.GetProperty("allocation_fte").GetDouble(), precision: 5);
    }

    // ── Cycle 4c ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetProjects_UnmappedProjects_HaveZeroAllocationFte()
    {
        var projects = await GetProjectsAsync();
        var unmapped = projects.Where(p => !p.GetProperty("name").GetString()!.StartsWith("P01")).ToList();
        Assert.All(unmapped, p => Assert.Equal(0.0, p.GetProperty("allocation_fte").GetDouble()));
    }

    // ── Cycle 4d ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetProjects_Divisions_AreAlphabeticallySorted()
    {
        var projects = await GetProjectsAsync();
        var p01 = projects.Single(p => p.GetProperty("name").GetString()!.StartsWith("P01"));
        var divisions = p01.GetProperty("divisions").EnumerateArray()
            .Select(d => d.GetString()!).ToList();
        Assert.NotEmpty(divisions);
        Assert.Equal(divisions.OrderBy(d => d).ToList(), divisions);
    }
}
