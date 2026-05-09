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
    // Resource 1, "2026-06": +50% on activity 2 (cumulative 50%)

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

    // --- Cycle 2: Hard cap — over-allocation rejected ---
    // Resource 2, "2026-07": PUT 100% on activity 3 (sticks), then reject 50% on activity 2

    [Fact]
    public async Task PutMapping_Returns400_WithReason_WhenCapExceeded()
    {
        await _client.PutAsJsonAsync("/api/mappings", new
        {
            resource_id = 2,
            activity_id = 3,
            month = "2026-07",
            percent = 100f
        });

        var response = await _client.PutAsJsonAsync("/api/mappings", new
        {
            resource_id = 2,
            activity_id = 2,
            month = "2026-07",
            percent = 50f
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("would_exceed_cap", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task PutMapping_Returns400_WithReason_WhenResourceInactiveInMonth()
    {
        var response = await _client.PutAsJsonAsync("/api/mappings", new
        {
            resource_id = 6,
            activity_id = 2,
            month = "2026-06",
            percent = 50f
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("resource_inactive_in_month", body.GetProperty("error").GetString());
    }

    // Resource 3, "2026-07": PUT 70% on activity 3, then 30% on activity 2 (total exactly 100%)

    [Fact]
    public async Task PutMapping_Returns200_WhenBringsResourceTo_Exactly_100Pct()
    {
        await _client.PutAsJsonAsync("/api/mappings", new
        {
            resource_id = 3,
            activity_id = 3,
            month = "2026-07",
            percent = 70m
        });

        var response = await _client.PutAsJsonAsync("/api/mappings", new
        {
            resource_id = 3,
            activity_id = 2,
            month = "2026-07",
            percent = 30m
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // Resource 4, "2026-07": PUT 90% on activity 3, update to 50% (reduction)

    [Fact]
    public async Task PutMapping_Returns200_WhenReducingExistingMapping_BelowCap()
    {
        await _client.PutAsJsonAsync("/api/mappings", new
        {
            resource_id = 4,
            activity_id = 3,
            month = "2026-07",
            percent = 90m
        });

        var response = await _client.PutAsJsonAsync("/api/mappings", new
        {
            resource_id = 4,
            activity_id = 3,
            month = "2026-07",
            percent = 50m
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // --- Cycle 3: Upsert — second PUT on same key updates percent ---
    // Resource 1, "2026-07": update activity 4 from 50% to 75%

    [Fact]
    public async Task PutMapping_Returns200_WhenUpdatingExistingMapping()
    {
        await _client.PutAsJsonAsync("/api/mappings", new
        {
            resource_id = 1,
            activity_id = 4,
            month = "2026-07",
            percent = 50f
        });

        var response = await _client.PutAsJsonAsync("/api/mappings", new
        {
            resource_id = 1,
            activity_id = 4,
            month = "2026-07",
            percent = 75f
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // --- Cycle 4: DELETE happy path ---
    // Resource 1, "2026-08": PUT 30% on activity 5, then DELETE

    [Fact]
    public async Task DeleteMapping_Returns204_WhenMappingExists()
    {
        await _client.PutAsJsonAsync("/api/mappings", new
        {
            resource_id = 1,
            activity_id = 5,
            month = "2026-08",
            percent = 30f
        });

        var response = await _client.DeleteAsync(
            "/api/mappings?resource_id=1&activity_id=5&month=2026-08");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    // --- Cycle 5: DELETE miss ---

    [Fact]
    public async Task DeleteMapping_Returns404_WhenMappingNotFound()
    {
        var response = await _client.DeleteAsync(
            "/api/mappings?resource_id=1&activity_id=6&month=2026-09");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteMapping_Returns400_WithReason_WhenResourceInactiveInMonth()
    {
        var response = await _client.DeleteAsync(
            "/api/mappings?resource_id=6&activity_id=2&month=2026-06");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("resource_inactive_in_month", body.GetProperty("error").GetString());
    }

    // Resource 5, "2026-07": PUT then DELETE on activity 7

    [Fact]
    public async Task DeleteMapping_Returns204_WhenResourceActive_AndMappingExists()
    {
        await _client.PutAsJsonAsync("/api/mappings", new
        {
            resource_id = 5,
            activity_id = 7,
            month = "2026-07",
            percent = 20m
        });

        var response = await _client.DeleteAsync(
            "/api/mappings?resource_id=5&activity_id=7&month=2026-07");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    // --- Cycle 6: Mappings embedded in GET /api/resources after a PUT ---
    // Resource 1, "2026-06": +40% on activity 6 (cumulative with test 1: 90%)

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
