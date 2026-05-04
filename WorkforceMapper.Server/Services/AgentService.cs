using System.Text.Json;
using System.Text.Json.Nodes;
using Anthropic.SDK.Common;
using Anthropic.SDK.Constants;
using Anthropic.SDK.Messaging;

namespace WorkforceMapper.Server.Services;

public class AgentService(IClaudeClient claude, DataService data)
{
    private const string SystemPrompt = """
        You are a workforce capacity analyst for Pragmenta Insights, a mid-size data analytics company.
        You have access to tools that query the company's workforce allocation data. Use them to answer
        questions about capacity utilisation, monthly cost, product attribution, and run/change spend.

        Key facts about the data:
        - All costs are in USD; rates are monthly.
        - allocation_pct values above 100% indicate over-allocation (a resource's mapped percent totals
          exceed 100%) — this is a real operational condition, not a data error.
        - get_cost_by_product returns an "Unattributed" line for mapped cost that cannot be traced to
          any product (e.g. AuthService support, ObservabilityStack support, DevPortal Rebuild — shared
          infrastructure overhead). This is deliberate, not a data quality issue.
        - active_resource_count reflects resources billing in the queried month (active flag = 1);
          resource_count is total headcount. These differ when resources join or leave mid-year.
        - The currently selected month is passed as the default for all tool calls unless the user
          specifies a different month.

        Answer only from tool results — do not estimate or infer allocation figures. Always call at
        least one tool before answering. Cite specific division names, activity names, monthly cost
        figures, and allocation percentages from tool results. Flag clearly when the answer requires
        data not available in the tools.

        Respond concisely and precisely. When surfacing cost figures, round to the nearest $1,000.
        When a question cannot be answered with the available tools, say so clearly.
        """;

    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public async Task RunAsync(string query, string month, Func<string, Task> onToken,
        CancellationToken ct = default)
    {
        var messages = new List<Message> { new(RoleType.User, query) };
        var parameters = new MessageParameters
        {
            Model = AnthropicModels.Claude46Sonnet,
            MaxTokens = 4096,
            System = [new SystemMessage(SystemPrompt, null)],
            Tools = BuildTools(),
            Messages = messages,
            Stream = false
        };

        while (true)
        {
            var response = await claude.GetMessageAsync(parameters);

            if (response.StopReason != "tool_use")
                break;

            messages.Add(new Message { Role = RoleType.Assistant, Content = response.Content ?? [] });

            var toolResults = new List<ContentBase>();
            foreach (var toolUse in (response.Content ?? []).OfType<ToolUseContent>())
            {
                var result = await DispatchAsync(toolUse, month);
                toolResults.Add(new ToolResultContent
                {
                    ToolUseId = toolUse.Id,
                    Content = [new TextContent { Text = result }]
                });
            }

            messages.Add(new Message { Role = RoleType.User, Content = toolResults });
            parameters.Messages = messages;
        }

        parameters.Stream = true;
        parameters.Tools = null;

        await foreach (var chunk in claude.StreamMessageAsync(parameters).WithCancellation(ct))
        {
            if (chunk.Delta?.Text is { Length: > 0 } text)
                await onToken(text);
        }
    }

    private async Task<string> DispatchAsync(ToolUseContent toolUse, string defaultMonth)
    {
        var args = toolUse.Input;
        var month = args?["month"]?.GetValue<string>() ?? defaultMonth;
        var division = args?["division"]?.GetValue<string>();

        return toolUse.Name switch
        {
            "get_capacity_by_division" =>
                JsonSerializer.Serialize(
                    await data.GetCapacityByDivisionAsync(month), JsonOpts),

            "get_activity_allocations" =>
                JsonSerializer.Serialize(
                    await data.GetActivityAllocationsAsync(
                        args?["activity_id"]?.GetValue<int>(),
                        division,
                        args?["activity_type"]?.GetValue<string>(),
                        month),
                    JsonOpts),

            "get_unallocated_resources" =>
                JsonSerializer.Serialize(
                    await data.GetUnallocatedResourcesAsync(
                        (int)(args?["threshold_pct"]?.GetValue<double>() ?? 100),
                        division,
                        month),
                    JsonOpts),

            "get_overallocated_resources" =>
                JsonSerializer.Serialize(
                    await data.GetOverallocatedResourcesAsync(
                        division,
                        (int)(args?["threshold_pct"]?.GetValue<double>() ?? 100),
                        month),
                    JsonOpts),

            "get_cost_by_product" =>
                JsonSerializer.Serialize(
                    await data.GetCostByProductAsync(month), JsonOpts),

            _ => JsonSerializer.Serialize(new { error = $"Unknown tool: {toolUse.Name}" })
        };
    }

    private static IList<Anthropic.SDK.Common.Tool> BuildTools() =>
    [
        MakeTool("get_capacity_by_division",
            "Get resource count, cost breakdown, and allocation percentage by division.",
            new JsonObject
            {
                ["division"] = Prop("string",  "Filter to a specific division name."),
                ["month"]    = Prop("string",  "Month in YYYY-MM format.")
            }),
        MakeTool("get_activity_allocations",
            "List resource-to-activity allocations with implied monthly cost.",
            new JsonObject
            {
                ["activity_id"]   = Prop("integer", "Filter by activity ID."),
                ["division"]      = Prop("string",  "Filter by division name."),
                ["activity_type"] = Prop("string",  "Support, Minor Changes, or Project."),
                ["month"]         = Prop("string",  "Month in YYYY-MM format.")
            }),
        MakeTool("get_unallocated_resources",
            "List active resources whose total allocation is below a threshold.",
            new JsonObject
            {
                ["division"]      = Prop("string", "Filter by division name."),
                ["threshold_pct"] = Prop("number", "Allocation % ceiling (default 100)."),
                ["month"]         = Prop("string", "Month in YYYY-MM format.")
            }),
        MakeTool("get_overallocated_resources",
            "List active resources whose total allocation exceeds a threshold.",
            new JsonObject
            {
                ["division"]      = Prop("string", "Filter by division name."),
                ["threshold_pct"] = Prop("number", "Allocation % floor (default 100)."),
                ["month"]         = Prop("string", "Month in YYYY-MM format.")
            }),
        MakeTool("get_cost_by_product",
            "Get total monthly cost attributed to each product, plus an Unattributed line.",
            new JsonObject
            {
                ["month"] = Prop("string", "Month in YYYY-MM format.")
            })
    ];

    private static Anthropic.SDK.Common.Tool MakeTool(string name, string description, JsonObject properties)
    {
        var schema = new JsonObject { ["type"] = "object", ["properties"] = properties };
        return new Anthropic.SDK.Common.Tool(new Function(name, description, schema));
    }

    private static JsonObject Prop(string type, string description) =>
        new() { ["type"] = type, ["description"] = description };
}
