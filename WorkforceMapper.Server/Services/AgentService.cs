using System.Text.Json;
using System.Text.Json.Nodes;
using Anthropic.SDK.Common;
using Anthropic.SDK.Constants;
using Anthropic.SDK.Messaging;

namespace WorkforceMapper.Server.Services;

public class AgentService(IClaudeClient claude, DataService data)
{
    private static string BuildSystemPrompt(string month) => $"""
        You are a workforce capacity analyst for Pragmenta Insights, a mid-size data analytics company.
        You have access to tools that query the company's workforce allocation data. Use them to answer
        questions about capacity utilisation, monthly cost, product attribution, risk, and run/change spend.

        Key facts about the data:
        - All costs are in USD; rates are monthly.
        - The currently selected month is {month}. Use this month for all tool calls unless the user
          explicitly asks about a different month. Do not guess or invent other months.
        - The 100% allocation rule is hard — the API rejects any mapping change that would push a
          resource above 100% or target a month in which the resource is inactive.
        - Application risk (get_application_risk) is classified as high, medium, or low based on
          current_coverage_fte against min_support_fte and recommended_support_fte: below min is high,
          below recommended is medium, at or above recommended is low.
        - The dependent_products field in get_application_risk results lists only direct
          product-application links. Foundational infrastructure apps (e.g. AuthService) have no direct
          product links and will show dependent_products: [] — yet their risk propagates to every
          product that depends on them transitively. Do not use dependent_products to assess ARR
          exposure; use get_product_risk instead and look for the app in contributing_apps.
        - Product risk (get_product_risk) is the maximum application risk across the transitive closure
          of all applications the product depends on (max-wins aggregation). Always call get_product_risk
          when asked about product revenue at risk or portfolio-wide exposure.
        - get_cost_by_product returns an "Unattributed" line for mapped cost that cannot be traced to
          any product (e.g. AuthService support, ObservabilityStack support, DevPortal Rebuild — shared
          infrastructure overhead). This is deliberate, not a data quality issue.
        - active_resource_count reflects resources billing in the queried month (active flag = 1);
          resource_count is total headcount. These differ when resources join or leave mid-year.

        Answer only from tool results — do not estimate or infer allocation figures. Always call at
        least one tool before answering. Cite specific division names, activity names, monthly cost
        figures, and allocation percentages from tool results. Flag clearly when the answer requires
        data not available in the tools.

        Respond concisely and precisely. When surfacing cost figures, round to the nearest $1,000.
        When a question cannot be answered with the available tools, say so clearly.
        When the answer involves resource allocations, always include a per-resource breakdown table
        showing resource name, allocation percentage, and implied monthly cost — never summarise these
        into a range.
        """;

    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public async Task RunAsync(string query, string month, Func<string, Task> onToken,
        CancellationToken ct = default)
    {
        var messages = new List<Message> { new(RoleType.User, query) };
        var parameters = new MessageParameters
        {
            Model = AnthropicModels.Claude45Haiku,
            MaxTokens = 4096,
            System = [new SystemMessage(BuildSystemPrompt(month), null)],
            Tools = BuildTools(),
            Messages = messages,
            Stream = false
        };

        var toolsDispatched = false;

        while (true)
        {
            var response = await claude.GetMessageAsync(parameters);

            if (response.StopReason != "tool_use")
            {
                if (!toolsDispatched)
                {
                    var directText = string.Concat(
                        (response.Content ?? []).OfType<TextContent>().Select(t => t.Text));
                    if (directText.Length > 0)
                        await onToken(directText);
                    return;
                }
                break;
            }

            toolsDispatched = true;
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
        var rawMonth = args?["month"]?.GetValue<string>();
        var month = IsValidMonth(rawMonth) ? rawMonth! : defaultMonth;
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
                        args?["activity_name"]?.GetValue<string>(),
                        month),
                    JsonOpts),

            "get_unallocated_resources" =>
                JsonSerializer.Serialize(
                    await data.GetUnallocatedResourcesAsync(
                        (int)(args?["threshold_pct"]?.GetValue<double>() ?? 100),
                        division,
                        month),
                    JsonOpts),

            "get_application_risk" =>
                JsonSerializer.Serialize(
                    await data.GetApplicationsAsync(
                        month,
                        args?["application_id"]?.GetValue<int>()),
                    JsonOpts),

            "get_product_risk" =>
                JsonSerializer.Serialize(
                    await data.GetProductRiskAsync(
                        month,
                        args?["product_id"]?.GetValue<int>()),
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
                ["activity_name"] = Prop("string",  "Case-insensitive substring match on activity name (e.g. \"AuthService Replacement\")."),
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
        MakeTool("get_application_risk",
            "Get application risk classification (high/medium/low) based on support coverage against min and recommended FTE thresholds.",
            new JsonObject
            {
                ["application_id"] = Prop("integer", "Filter to a specific application ID."),
                ["month"]          = Prop("string",  "Month in YYYY-MM format.")
            }),
        MakeTool("get_product_risk",
            "Get product risk (max-wins over transitive application dependency closure) with contributing app breakdown.",
            new JsonObject
            {
                ["product_id"] = Prop("integer", "Filter to a specific product ID."),
                ["month"]      = Prop("string",  "Month in YYYY-MM format.")
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

    private static bool IsValidMonth(string? month) =>
        month is { Length: 7 } && month[4] == '-' &&
        int.TryParse(month.AsSpan(0, 4), out _) &&
        int.TryParse(month.AsSpan(5, 2), out var m) && m is >= 1 and <= 12;
}
