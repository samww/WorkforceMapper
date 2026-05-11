# Issue #30 — Agent surface: six-tier donor prompt and tool description

> Spec for `AgentService.BuildTools` and `AgentService.BuildSystemPrompt`.
> Read `docs/build/issue-29-tiered-donor-candidates.md` and `docs/build/risk-model.md` first.
> Blocked by #29 — complete that issue before touching AgentService.

## `BuildTools` — `get_donor_candidates` description

Replace the current description string. The new description must:

1. State that results are returned in six-tier priority order (same-division tiers 1–3 first, cross-divisional tiers 4–6 second).
2. Name the three source types per tier group: surplus Support, Minor Changes, Project.
3. Document `requires_cross_divisional_alignment` — the field is `true` on any tier-4/5/6 candidate and signals that a cross-divisional conversation is needed before acting.
4. Document `surplus_support_fte_available` — the pre-computed total surplus support FTE in the same division.
5. Preserve all existing field descriptions (`allocation_fte`, `gap_coverage_pct`, `cumulative_fte`, `cumulative_gap_coverage_pct`, `unique_resource_count`, `total_allocation_count`).

## `BuildSystemPrompt` — donor-candidate paragraph

Replace the existing donor-candidate bullet. The replacement must instruct Claude to:

1. Walk tiers in order: same-division surplus Support first, then same-division Minor Changes, then same-division Project; only proceed to cross-divisional tiers (4–6) if same-division tiers do not close the gap.
2. Surface a clear cross-divisional alignment warning for every candidate row where `requires_cross_divisional_alignment` is `true`. The warning should note that a cross-divisional transfer requires explicit sign-off before acting.
3. Use `cumulative_gap_coverage_pct >= 100` as the stopping condition when identifying the minimum-cost combination — do not sum `gap_coverage_pct` values manually.
4. Use `surplus_support_fte_available` (pre-computed) when stating how much surplus support capacity exists in the same division — do not derive it from the candidates list.
5. Preserve the existing instructions: use `unique_resource_count` and `total_allocation_count` for counts; do not compose donor candidates manually from `get_activity_allocations` calls.

## `docs/build/agent-system-prompt.md`

After updating `BuildSystemPrompt`, copy the full updated prompt verbatim into the code block in this file, replacing the existing content. The file is the canonical readable reference for the live prompt.

## Verification (HITL)

Run the backend and issue the query:
> "Why is AuthService at risk this month and where could we find the capacity to fix it? I want to use excess support capacity if possible."

Expected behaviour:
- Agent checks surplus support in Platform Engineering first.
- If no surplus, moves to Minor Changes in Platform Engineering.
- If still not enough, moves to Project in Platform Engineering.
- If same-division options do not close the gap, presents cross-divisional options with an explicit alignment warning on each cross-divisional row.
- Does not mix tiers in the presentation without explaining the order.
