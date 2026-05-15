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
        - Application risk (get_application_risk) returns a pre-computed risk field: "high", "medium",
          or "low". Always read this field directly — do not re-derive it from FTE numbers.
        - The dependent_products field in get_application_risk results lists every product whose
          transitive dependency closure includes this application — both direct and indirect links.
          Use total_dependent_arr_usd (pre-computed) for total ARR exposure; do not sum arr_usd
          values yourself. Do not call get_product_risk to count ARR exposure when
          get_application_risk already returns total_dependent_arr_usd and the full product list.
        - The transitive_dependent_apps field lists every application that transitively depends on
          this application (direct dependents plus all apps that depend on those dependents, etc.).
          Use dependent_app_count (pre-computed) for the total count — do not count the list yourself.
        - Product risk (get_product_risk) is the maximum application risk across the transitive closure
          of all applications the product depends on (max-wins aggregation). Always call get_product_risk
          when asked about product revenue at risk or portfolio-wide exposure.
        - get_cost_by_product returns an "Unattributed" line for mapped cost that cannot be traced to
          any product (e.g. AuthService support, ObservabilityStack support, DevPortal Rebuild — shared
          infrastructure overhead). This is deliberate, not a data quality issue.
          Use total_arr_usd (pre-computed) for the company-wide ARR total — do not sum arr_usd
          values from the products list yourself.
        - active_resource_count reflects resources billing in the queried month (active flag = 1);
          resource_count is total headcount. These differ when resources join or leave mid-year.

        - When asked where capacity could be found to address an under-resourced application, call
          get_donor_candidates. Pass application_name (e.g. "AuthService") — do not guess or invent
          application_id values. The tool returns candidates in eight-tier priority order: same-division
          tiers 1–4 (idle capacity, surplus Support, Minor Changes, Project) come first;
          cross-divisional tiers 5–8 (the same source types from other divisions) follow. Walk tiers
          in order — exhaust same-division options (tiers 1–4) before presenting cross-divisional
          options (tiers 5–8). Idle capacity (tiers 1 and 5) is the cheapest source because it does
          not displace any other work.
          Each row includes allocation_fte, gap_coverage_pct, cumulative_fte, cumulative_gap_coverage_pct,
          and in_fewest_changes_combination.
          The fewest-changes combination is the complete pre-computed solution — do not present the
          final row of the table in isolation as the recommended action. Render
          fewest_changes_combination.summary_paragraph verbatim as the response headline,
          followed by the fewest_changes_combination.mapping_changes bulleted list. Do not recompute
          counts, totals, or the tier-system description — they are baked into the paragraph.
          For every candidate row where requires_cross_divisional_alignment is true, surface an explicit
          warning that the transfer requires cross-divisional sign-off before acting.
          The candidates list contains pipeline rows beyond the fewest-changes solution; use
          total_available_candidates_count (pre-computed) when stating how many candidates were
          considered — do not count the candidates list to determine pool size.
          Do not compose donor candidates manually from get_activity_allocations calls.

        - When asked which division is most constrained on capacity, use get_capacity_by_division.
          The result includes a pre-computed fully_constrained_divisions list (all divisions at 100%
          allocation). If this list contains more than one division, they are equally constrained —
          state that clearly rather than picking one as "most" constrained. Only if exactly one
          division appears in fully_constrained_divisions, or if you must break a tie, explain the
          tiebreaker (e.g. largest headcount or cost) and label it as such.

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
            Temperature = 0,
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
                        args?["application_id"]?.GetValue<int>(),
                        args?["application_name"]?.GetValue<string>()),
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

            "get_donor_candidates" =>
                JsonSerializer.Serialize(
                    await data.GetDonorCandidatesAsync(
                        args?["application_id"]?.GetValue<int>(),
                        args?["application_name"]?.GetValue<string>(),
                        month),
                    JsonOpts),

            _ => JsonSerializer.Serialize(new { error = $"Unknown tool: {toolUse.Name}" })
        };
    }

    private static IList<Anthropic.SDK.Common.Tool> BuildTools() =>
    [
        MakeTool("get_capacity_by_division",
            "Get resource count, cost breakdown, and allocation percentage by division. " +
            "Returns pre-computed company-wide totals: total_resource_count, total_monthly_cost, " +
            "total_allocated_monthly_cost, total_unallocated_monthly_cost, and overall_allocation_pct " +
            "(headcount-weighted across all divisions). Use these for company-wide questions — " +
            "do not sum the divisions list yourself. " +
            "Also returns fully_constrained_divisions: pre-computed list of division names whose " +
            "allocation_pct is at 100%. Use this list directly when answering capacity-constraint " +
            "questions — do not filter the divisions list yourself.",
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
            "List active resources whose total allocation is below a threshold. " +
            "Returns total_resource_count (pre-computed sum across all divisions) and " +
            "total_idle_monthly_cost — do not count or sum the resources list yourself.",
            new JsonObject
            {
                ["division"]      = Prop("string", "Filter by division name."),
                ["threshold_pct"] = Prop("number", "Allocation % ceiling (default 100)."),
                ["month"]         = Prop("string", "Month in YYYY-MM format.")
            }),
        MakeTool("get_application_risk",
            "Get application risk classification (high/medium/low) based on support coverage against min and recommended FTE thresholds. " +
            "Prefer application_name when you know the name but not the numeric ID.",
            new JsonObject
            {
                ["application_name"] = Prop("string",  "Application name (e.g. \"AuthService\"). Use this when you know the name but not the ID."),
                ["application_id"]   = Prop("integer", "Application ID — use only when already known from a prior call."),
                ["month"]            = Prop("string",  "Month in YYYY-MM format.")
            }),
        MakeTool("get_product_risk",
            "Get product risk (max-wins over transitive application dependency closure) with contributing app breakdown. " +
            "Returns pre-computed risk-level aggregates: high_risk_product_count, medium_risk_product_count, " +
            "low_risk_product_count, total_high_risk_arr_usd, total_medium_risk_arr_usd. " +
            "Use these for portfolio-wide questions — do not filter or sum the products list yourself.",
            new JsonObject
            {
                ["product_id"] = Prop("integer", "Filter to a specific product ID."),
                ["month"]      = Prop("string",  "Month in YYYY-MM format.")
            }),
        MakeTool("get_cost_by_product",
            "Get total monthly cost attributed to each product, plus an Unattributed line. " +
            "Returns pre-computed totals: total_payroll (active headcount), total_mapped_cost, " +
            "and total_arr_usd (sum of ARR across all products). " +
            "Use total_arr_usd for the company-wide ARR figure — do not sum arr_usd values yourself.",
            new JsonObject
            {
                ["month"] = Prop("string", "Month in YYYY-MM format.")
            }),
        MakeTool("get_donor_candidates",
            "Get donor candidates for an under-resourced application in eight-tier priority order: " +
            "same-division tiers 1–4 (idle capacity, surplus Support, Minor Changes, Project) then " +
            "cross-divisional tiers 5–8 (idle capacity, surplus Support, Minor Changes, Project from other divisions). " +
            "fewest_changes_combination is the complete pre-computed solution: render its summary_paragraph " +
            "verbatim as the response opening, then its mapping_changes list verbatim as bullets. " +
            "summary_paragraph contains counts, totals, and the tier-system description — do not recompute these. " +
            "candidates contains pipeline rows beyond the solution; each row has allocation_fte, " +
            "gap_coverage_pct, cumulative_fte, cumulative_gap_coverage_pct, and " +
            "in_fewest_changes_combination (always false on pipeline rows). " +
            "requires_cross_divisional_alignment is true on any tier-5/6/7/8 row and signals that " +
            "a cross-divisional conversation is needed before acting. " +
            "total_available_candidates_count is the full pool size; use it when stating how many " +
            "candidates were considered — do not count the candidates list. " +
            "Prefer application_name when you know the name but not the numeric ID.",
            new JsonObject
            {
                ["application_name"] = Prop("string",  "Application name (e.g. \"AuthService\"). Use this when you know the name but not the ID."),
                ["application_id"]   = Prop("integer", "Application ID — use only when already known from a prior get_application_risk call."),
                ["month"]            = Prop("string",  "Month in YYYY-MM format.")
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
