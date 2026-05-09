using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace WorkforceMapper.Tests;

public class ProductsApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ProductsApiTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        factory.SeedApplicationsTestData();
    }

    private async Task<JsonElement[]> GetProductsAsync(string month = "2026-05")
    {
        var response = await _client.GetAsync($"/api/products?month={month}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement[]>())!;
    }

    // --- Cycle 7: all products HIGH when AuthService is HIGH ---

    [Fact]
    public async Task GetProductRisk_AllSevenProducts_AreHighRisk_When_AuthService_Is_HighRisk()
    {
        var products = await GetProductsAsync();

        Assert.Equal(7, products.Length);
        Assert.All(products, p =>
            Assert.Equal("high", p.GetProperty("risk").GetString()));
    }

    // --- Cycle 8: transitive reach ---

    [Fact]
    public async Task GetProductRisk_TransitiveReach_DiscoversIndirectDependency()
    {
        // FieldworkServices (id=7) has direct apps: FieldOps(10), QuotaManager(8), SampleRouter(12), CallCentreSuite(13).
        // None of those directly depend on AuthService in product_applications, but all have
        // application_dependencies -> AuthService (id=4). AuthService is HIGH.
        // So FieldworkServices must be HIGH and AuthService must appear in contributing_apps.
        var products = await GetProductsAsync();
        var fieldwork = products.Single(p => p.GetProperty("name").GetString() == "FieldworkServices");

        Assert.Equal("high", fieldwork.GetProperty("risk").GetString());

        var contrib = fieldwork.GetProperty("contributing_apps").EnumerateArray().ToList();
        Assert.Contains(contrib, a => a.GetProperty("name").GetString() == "AuthService");
    }

    // --- Cycle 8b: direct_apps tree structure ---

    [Fact]
    public async Task GetProductRisk_DirectApps_ContainsNested_AppDependencies()
    {
        // DataLicensing (id=1) has direct apps: CDW(1), APIGateway(2), ClientPortal(15).
        // CDW(1) depends on AuthService(4); APIGateway(2) depends on AuthService(4).
        // So direct_apps for DataLicensing should include CDW and APIGateway,
        // and their dependencies arrays should each contain AuthService.
        var products = await GetProductsAsync();
        var dataLicensing = products.Single(p => p.GetProperty("name").GetString() == "DataLicensing");

        var directApps = dataLicensing.GetProperty("direct_apps").EnumerateArray().ToList();
        Assert.NotEmpty(directApps);

        var cdw = directApps.SingleOrDefault(a => a.GetProperty("name").GetString() == "CoreDataWarehouse");
        Assert.True(cdw.ValueKind != System.Text.Json.JsonValueKind.Undefined, "CoreDataWarehouse should be a direct app of DataLicensing");

        var deps = cdw.GetProperty("dependencies").EnumerateArray().ToList();
        Assert.Contains(deps, d => d.GetProperty("name").GetString() == "AuthService");
    }

    // --- Cycle 9: contributing_apps ordering ---

    [Fact]
    public async Task GetProductRisk_ContributingApps_OrderedByRiskThenName()
    {
        var products = await GetProductsAsync();
        // All products are HIGH risk in this seed, so high apps come first.
        // Within the same risk tier the names should be alphabetical.
        foreach (var product in products)
        {
            var contrib = product.GetProperty("contributing_apps").EnumerateArray().ToList();
            if (contrib.Count < 2) continue;

            var riskOrder = new Dictionary<string, int> { ["high"] = 0, ["medium"] = 1, ["low"] = 2 };
            for (var i = 0; i < contrib.Count - 1; i++)
            {
                var a = contrib[i];
                var b = contrib[i + 1];
                var rA = riskOrder[a.GetProperty("risk").GetString()!];
                var rB = riskOrder[b.GetProperty("risk").GetString()!];
                Assert.True(rA <= rB, $"Risk order violated: {a.GetProperty("name")} ({rA}) before {b.GetProperty("name")} ({rB})");
                if (rA == rB)
                    Assert.True(
                        string.Compare(a.GetProperty("name").GetString(), b.GetProperty("name").GetString(), StringComparison.Ordinal) <= 0,
                        $"Name order violated within same risk tier");
            }
        }
    }

}
