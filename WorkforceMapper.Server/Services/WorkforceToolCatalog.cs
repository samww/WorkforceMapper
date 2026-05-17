using System.Text.Json;
using System.Text.Json.Nodes;
using Anthropic.SDK.Common;
using Anthropic.SDK.Messaging;

namespace WorkforceMapper.Server.Services;

public class WorkforceToolCatalog(DataService data)
{
    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public string BuildSystemPrompt(string month) => $"""
        You are a workforce capacity analyst for Pragmenta Insights, a mid-size data analytics company.
        You have access to tools that query the company's workforce allocation data. Use them to answer
        questions about capacity utilisation, monthly cost, product attribution, risk, and run/change spend.

        Key facts about the data:
        - All costs are in USD; rates are monthly.
        - The currently selected month is {month}. Use this month for all tool calls unless the user
          explicitly asks about a different month. Do not guess or invent other months.
        - The 100% allocation rule is hard â€” the API rejects any mapping change that would push a
          resource above 100% or target a month in which the resource is inactive.
        - Application risk (get_application_risk) returns a pre-computed risk field: "high", "medium",
          or "low". Always read this field directly â€” do not re-derive it from FTE numbers.
          Render risk_reason verbatim â€” do not reconstruct the coverage/min/recommended sentence
          from raw fields. Render dependent_products_summary verbatim â€” do not reformat the product
          list, recompute the ARR sum, or change the alphabetical order.
        - The dependent_products field in get_application_risk results lists every product whose
          transitive dependency closure includes this application â€” both direct and indirect links.
          Use total_dependent_arr_usd (pre-computed) for total ARR exposure; do not sum arr_usd
          values yourself. Do not call get_product_risk to count ARR exposure when
          get_application_risk already returns total_dependent_arr_usd and the full product list.
        - The transitive_dependent_apps field lists every application that transitively depends on
          this application (direct dependents plus all apps that depend on those dependents, etc.).
          Use dependent_app_count (pre-computed) for the total count â€” do not count the list yourself.
        - Product risk (get_product_risk) is the maximum application risk across the transitive closure
          of all applications the product depends on (max-wins aggregation). Always call get_product_risk
          when asked about product revenue at risk or portfolio-wide exposure.
        - get_cost_by_product returns an "Unattributed" line for mapped cost that cannot be traced to
          any product (e.g. AuthService support, ObservabilityStack support, DevPortal Rebuild â€” shared
          infrastructure overhead). This is deliberate, not a data quality issue.
          Use total_arr_usd (pre-computed) for the company-wide ARR total â€” do not sum arr_usd
          values from the products list yourself.
          unmapped_payroll is payroll for active resources with zero mappings in the month. It is the
          gap between total_payroll and total_mapped_cost, and is entirely distinct from the
          Unattributed line: Unattributed is mapped cost already inside total_mapped_cost; unmapped_payroll
          is outside total_mapped_cost and represents resources whose time was not allocated anywhere.
          Do not conflate these two figures. Do not compute or infer unmapped_payroll â€” read it directly.
          Cost and ARR are independent dimensions. A product's allocated cost comes from Support activity
          mappings (monthly_rate Ã— allocation_percent / 100) â€” it is not driven by or correlated with
          ARR. Do not imply a causal relationship between a product's ARR and its cost.
        - active_resource_count reflects resources billing in the queried month (active flag = 1);
          resource_count is total headcount. These differ when resources join or leave mid-year.

        - For questions of the form "why is X at risk and how do we fix it", call
          get_at_risk_application_fix with the application name. Render risk_reason verbatim as the
          risk headline, then dependent_products_summary verbatim, then
          fewest_changes_combination.summary_paragraph verbatim, then
          fewest_changes_combination.mapping_changes as a bulleted list. If
          cross_divisional_signoffs_required is non-empty, render one bullet per entry in the form
          "RESOURCE: FROM_DIV â†’ TO_DIV (N FTE) â€” requires cross-divisional sign-off" using the
          resource_name, from_division, to_division, and fte fields. Otherwise render
          cross_divisional_signoffs_summary as a single line.
          Do not recompute counts, totals, post-fix coverage, the tier-system description, ARR sums,
          or product names â€” all are baked into the strings.

        - When asked where capacity could be found to address an under-resourced application (without
          also asking why it is at risk), call get_donor_candidates. Pass application_name â€” do not
          guess or invent application_id values. The tool returns candidates in eight-tier priority order:
          same-division tiers 1â€“4 (idle capacity, surplus Support, Minor Changes, Project) come first;
          cross-divisional tiers 5â€“8 (the same source types from other divisions) follow.
          Render fewest_changes_combination.summary_paragraph verbatim as the response headline,
          followed by the fewest_changes_combination.mapping_changes bulleted list. Do not recompute
          counts, totals, or the tier-system description â€” they are baked into the paragraph.
          If cross_divisional_signoffs_required is non-empty, render one bullet per entry in the form
          "RESOURCE: FROM_DIV â†’ TO_DIV (N FTE) â€” requires cross-divisional sign-off" using the
          resource_name, from_division, to_division, and fte fields; otherwise render
          cross_divisional_signoffs_summary as a single line.
          Use total_available_candidates_count (pre-computed) when stating how many candidates were
          considered â€” do not count the candidates list to determine pool size.
          Do not compose donor candidates manually from get_activity_allocations calls.

        - Project allocation (get_project_allocation) returns the total FTE allocated to each project
          for the month. Projects have no risk classification or capacity thresholds â€” do not describe
          a project as "under-resourced", "adequately staffed", or "at risk", and do not invent
          benchmarks. Report allocation figures only. Use total_project_allocation_fte for the
          company-wide total and projects_ranked_by_allocation for "which projects consume the most"
          questions â€” do not sort or sum the projects list yourself.

        - When asked which division is most constrained on capacity, use get_capacity_by_division.
          The result includes a pre-computed fully_constrained_divisions list (all divisions at 100%
          allocation). If this list contains more than one division, they are equally constrained â€”
          state that clearly rather than picking one as "most" constrained. Only if exactly one
          division appears in fully_constrained_divisions, or if you must break a tie, explain the
          tiebreaker (e.g. largest headcount or cost) and label it as such.

        - For questions about unallocated or idle resource cost â€” for a specific division or
          company-wide â€” call get_unallocated_resources. Pass division = "<name>" to scope to
          one division; omit it for a company-wide breakdown. Use threshold_pct = 100 to include
          all resources below 100%. The result includes total_idle_monthly_cost (pre-computed sum)
          and a per-resource breakdown. Always show the per-resource breakdown table when resources
          are returned. If total_resource_count is zero, state that the division has no idle
          resources.

        Answer only from tool results â€” do not estimate or infer allocation figures. Always call at
          least one tool before answering. Cite specific division names, activity names, monthly cost
          figures, and allocation percentages from tool results. Flag clearly when the answer requires
          data not available in the tools.

          Respond concisely and precisely. When surfacing cost figures, round to the nearest $1,000.
        When a question cannot be answered with the available tools, say so clearly.
        When the answer involves resource allocations, always include a per-resource breakdown table
        showing resource name, allocation percentage, and implied monthly cost â€” never summarise these
        into a range.
        """;

    public async Task<string> DispatchAsync(ToolUseContent toolUse, string defaultMonth)
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

            "get_at_risk_application_fix" =>
                JsonSerializer.Serialize(
                    await data.GetAtRiskApplicationFixAsync(
                        args?["application_id"]?.GetValue<int>(),
                        args?["application_name"]?.GetValue<string>(),
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

            "get_project_allocation" =>
                JsonSerializer.Serialize(
                    await BuildProjectAllocationResponse(args, month), JsonOpts),

            _ => JsonSerializer.Serialize(new { error = $"Unknown tool: {toolUse.Name}" })
        };
    }

    private async Task<object> BuildProjectAllocationResponse(JsonNode? args, string month)
    {
        var projects = await data.GetProjectsAsync(
            month,
            args?["project_id"]?.GetValue<int>(),
            args?["project_name"]?.GetValue<string>());
        return new
        {
            projects,
            total_project_allocation_fte = Math.Round(projects.Sum(p => p.allocation_fte), 2),
            projects_ranked_by_allocation = projects.OrderByDescending(p => p.allocation_fte).ToList()
        };
    }

    public IList<Anthropic.SDK.Common.Tool> BuildTools() =>
    [
        MakeTool("get_at_risk_application_fix",
            "Combined tool for the canonical 'why is X at risk and how do we fix it' question. " +
            "Returns application risk (same shape as get_application_risk) plus the pre-computed " +
            "fewest-changes donor solution (same shape as get_donor_candidates). " +
            "risk_reason and dependent_products_summary are pre-rendered strings â€” render verbatim. " +
            "fewest_changes_combination.summary_paragraph and mapping_changes are pre-rendered â€” render verbatim. " +
            "cross_divisional_signoffs_required lists cross-div transfers needed; " +
            "cross_divisional_signoffs_summary is the pre-rendered single-line alternative when the list is empty. " +
            "candidates_pipeline_count is the pre-computed total donor pool size.",
            new JsonObject
            {
                ["application_name"] = Prop("string",  "Application name (e.g. \"AuthService\"). Use when you know the name but not the ID."),
                ["application_id"]   = Prop("integer", "Application ID â€” use only when already known."),
                ["month"]            = Prop("string",  "Month in YYYY-MM format.")
            }),
        MakeTool("get_capacity_by_division",
            "Get resource count, cost breakdown, and allocation percentage by division. " +
            "Returns all divisions in a single call â€” no division filter. " +
            "Returns pre-computed company-wide totals: total_resource_count, total_monthly_cost, " +
            "total_allocated_monthly_cost, total_unallocated_monthly_cost, and overall_allocation_pct " +
            "(headcount-weighted across all divisions). Use these for company-wide questions â€” " +
            "do not sum the divisions list yourself. " +
            "Also returns fully_constrained_divisions: pre-computed list of division names whose " +
            "allocation_pct is at 100%. Use this list directly when answering capacity-constraint " +
            "questions â€” do not filter the divisions list yourself. " +
            "For per-resource idle cost detail, use get_unallocated_resources instead.",
            new JsonObject
            {
                ["month"] = Prop("string", "Month in YYYY-MM format.")
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
            "Use this tool for questions about unallocated or idle resource cost â€” for a specific " +
            "division or company-wide. Pass division to scope results; omit for all divisions. " +
            "Use threshold_pct = 100 to include all resources below 100% allocation. " +
            "Returns total_idle_monthly_cost (pre-computed) and a per-resource breakdown " +
            "(resource name, division, allocation %, idle monthly cost). " +
            "Do not count or sum the resources list â€” use total_resource_count and total_idle_monthly_cost.",
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
                ["application_id"]   = Prop("integer", "Application ID â€” use only when already known from a prior call."),
                ["month"]            = Prop("string",  "Month in YYYY-MM format.")
            }),
        MakeTool("get_product_risk",
            "Get product risk (max-wins over transitive application dependency closure) with contributing app breakdown. " +
            "Returns pre-computed risk-level aggregates: high_risk_product_count, medium_risk_product_count, " +
            "low_risk_product_count, total_high_risk_arr_usd, total_medium_risk_arr_usd. " +
            "Use these for portfolio-wide questions â€” do not filter or sum the products list yourself.",
            new JsonObject
            {
                ["product_id"] = Prop("integer", "Filter to a specific product ID."),
                ["month"]      = Prop("string",  "Month in YYYY-MM format.")
            }),
        MakeTool("get_cost_by_product",
            "Get total monthly cost attributed to each product (rounded to nearest $1,000), plus an Unattributed line. " +
            "Returns pre-computed totals: total_payroll, total_mapped_cost, unmapped_payroll, and total_arr_usd. " +
            "unmapped_payroll = payroll for resources with zero mappings; it is the gap between total_payroll and " +
            "total_mapped_cost, and is distinct from the Unattributed product line (which is already inside total_mapped_cost). " +
            "Use total_arr_usd for the company-wide ARR figure â€” do not sum arr_usd values yourself. " +
            "Cost and ARR are independent â€” do not infer a causal relationship between them.",
            new JsonObject
            {
                ["month"] = Prop("string", "Month in YYYY-MM format.")
            }),
        MakeTool("get_project_allocation",
            "Get the FTE allocated to each project for the month. " +
            "Returns pre-computed aggregates: total_project_allocation_fte (sum across all returned projects) " +
            "and projects_ranked_by_allocation (list sorted by allocation_fte descending). " +
            "Use total_project_allocation_fte for the company-wide total â€” do not sum the projects list. " +
            "Use projects_ranked_by_allocation for 'which projects consume the most' questions â€” " +
            "do not sort the projects list yourself. " +
            "Projects carry no risk classification or capacity thresholds â€” do not describe allocation as adequate or insufficient.",
            new JsonObject
            {
                ["project_id"]   = Prop("integer", "Filter to a specific project ID."),
                ["project_name"] = Prop("string",  "Filter by project name (substring match)."),
                ["month"]        = Prop("string",  "Month in YYYY-MM format.")
            }),
        MakeTool("get_donor_candidates",
            "Get donor candidates for an under-resourced application in eight-tier priority order: " +
            "same-division tiers 1â€“4 (idle capacity, surplus Support, Minor Changes, Project) then " +
            "cross-divisional tiers 5â€“8 (idle capacity, surplus Support, Minor Changes, Project from other divisions). " +
            "fewest_changes_combination is the complete pre-computed solution: render its summary_paragraph " +
            "verbatim as the response opening, then its mapping_changes list verbatim as bullets. " +
            "summary_paragraph contains counts, totals, and the tier-system description â€” do not recompute these. " +
            "candidates contains pipeline rows beyond the solution; each row has allocation_fte, " +
            "gap_coverage_pct, cumulative_fte, cumulative_gap_coverage_pct, and " +
            "in_fewest_changes_combination (always false on pipeline rows). " +
            "requires_cross_divisional_alignment is true on any tier-5/6/7/8 row and signals that " +
            "a cross-divisional conversation is needed before acting. " +
            "total_available_candidates_count is the full pool size; use it when stating how many " +
            "candidates were considered â€” do not count the candidates list. " +
            "Prefer application_name when you know the name but not the numeric ID.",
            new JsonObject
            {
                ["application_name"] = Prop("string",  "Application name (e.g. \"AuthService\"). Use this when you know the name but not the ID."),
                ["application_id"]   = Prop("integer", "Application ID â€” use only when already known from a prior get_application_risk call."),
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
