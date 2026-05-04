using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace WorkforceMapper.Tests;

public class MappingApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public MappingApiTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        factory.SeedTestData();
    }

    // --- Cycle 1: PUT happy path ---

    [Fact]
    public async Task PutMapping_Returns200_WithValidAllocation()
    {
        var response = await _client.PutAsJsonAsync("/api/mappings", new
        {
            resource_id = 1,
            activity_id = 2,
            month = "2026-06",
            percent = 50f
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // --- Cycle 2: Advisory constraint — over-allocation accepted (ADR 5) ---

    [Fact]
    public async Task PutMapping_Returns200_WhenAllocationExceeds100Pct()
    {
        var response = await _client.PutAsJsonAsync("/api/mappings", new
        {
            resource_id = 1,
            activity_id = 3,
            month = "2026-06",
            percent = 150f
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // --- Cycle 3: Upsert — second PUT on same key updates percent ---

    [Fact]
    public async Task PutMapping_Returns200_WhenUpdatingExistingMapping()
    {
        await _client.PutAsJsonAsync("/api/mappings", new
        {
            resource_id = 1,
            activity_id = 4,
            month = "2026-06",
            percent = 50f
        });

        var response = await _client.PutAsJsonAsync("/api/mappings", new
        {
            resource_id = 1,
            activity_id = 4,
            month = "2026-06",
            percent = 75f
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // --- Cycle 4: DELETE happy path ---

    [Fact]
    public async Task DeleteMapping_Returns204_WhenMappingExists()
    {
        await _client.PutAsJsonAsync("/api/mappings", new
        {
            resource_id = 1,
            activity_id = 5,
            month = "2026-06",
            percent = 30f
        });

        var response = await _client.DeleteAsync(
            "/api/mappings?resource_id=1&activity_id=5&month=2026-06");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    // --- Cycle 5: DELETE miss ---

    [Fact]
    public async Task DeleteMapping_Returns404_WhenMappingNotFound()
    {
        var response = await _client.DeleteAsync(
            "/api/mappings?resource_id=1&activity_id=6&month=2026-07");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- Cycle 6: Mappings embedded in GET /api/resources after a PUT ---

    [Fact]
    public async Task GetResources_ReturnsMappings_WhenMappingsExist()
    {
        await _client.PutAsJsonAsync("/api/mappings", new
        {
            resource_id = 1,
            activity_id = 6,
            month = "2026-06",
            percent = 40f
        });

        var response = await _client.GetAsync("/api/resources?month=2026-06");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(items);
        var resource1 = items.First(r => r.GetProperty("resource_id").GetString() == "PI00001");
        var mappings = resource1.GetProperty("mappings").EnumerateArray().ToList();
        Assert.Contains(mappings, m => m.GetProperty("activity_id").GetInt32() == 6);
    }
}
