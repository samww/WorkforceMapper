using Anthropic.SDK.Messaging;
using Microsoft.EntityFrameworkCore;
using WorkforceMapper.Server.Data;
using WorkforceMapper.Server.Services;

namespace WorkforceMapper.Tests;

public class WorkforceToolCatalogTests
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

    private static WorkforceToolCatalog CreateCatalog(WorkforceDbContext context) =>
        new(new DataService(context));

    [Fact]
    public void BuildTools_ContainsRiskTools_AndDoesNotContainOverallocatedResources()
    {
        using var context = CreateContext();
        var tools = CreateCatalog(context).BuildTools();

        Assert.Contains(tools, t => t.Function?.Name == "get_application_risk");
        Assert.Contains(tools, t => t.Function?.Name == "get_product_risk");
        Assert.DoesNotContain(tools, t => t.Function?.Name == "get_overallocated_resources");
    }

    [Fact]
    public void BuildTools_CapacityByDivision_HasNoDivisionParameter()
    {
        using var context = CreateContext();
        var tool = CreateCatalog(context)
            .BuildTools()
            .Single(t => t.Function?.Name == "get_capacity_by_division");

        var props = tool.Function!.Parameters?["properties"];

        Assert.Null(props?["division"]);
    }

    [Fact]
    public void BuildSystemPrompt_ProjectParagraph_ForbidsRiskAndAdequacyLanguage()
    {
        using var context = CreateContext();
        var systemText = CreateCatalog(context).BuildSystemPrompt("2026-05");

        Assert.Contains("get_project_allocation", systemText);
        Assert.Contains("under-resourced", systemText);
        Assert.Contains("do not invent", systemText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DispatchAsync_InvalidTool_ReturnsSerializedError()
    {
        await using var context = CreateContext();
        var toolUse = new ToolUseContent
        {
            Id = "tc_unknown",
            Name = "does_not_exist",
            Input = System.Text.Json.Nodes.JsonNode.Parse("{}")
        };

        var result = await CreateCatalog(context).DispatchAsync(toolUse, "2026-05");

        Assert.Contains("Unknown tool: does_not_exist", result);
    }
}
