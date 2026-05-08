# Issue #14 — Chat agent: replace get_overallocated_resources with risk tools

> Domain glossary, tool signatures, API contracts in `CONTEXT.md`.
> Risk classification rule and product aggregation in `docs/build/risk-model.md`.
> Existing AgentService pattern in `docs/build/issue-6-chat-api.md`.
> System prompt template in `docs/build/agent-system-prompt.md` (rewritten by this slice).

Replace the now-unreachable `get_overallocated_resources` tool with two risk tools and align the agent system prompt with the capacity-driven risk premise.

## Phase A — Tool registry diff

### Remove

- `get_overallocated_resources(division?, threshold_pct?, month?)`. Cannot return a non-empty result under the hard cap (issue #13).

### Add

- `get_application_risk(application_id?: int, month?: string)` — returns the same shape as `DataService.GetApplicationsAsync` (one row per app or one specific app); thin wrapper.
- `get_product_risk(product_id?: int, month?: string)` — returns the same shape as `DataService.GetProductRiskAsync`.

### Keep

- `get_capacity_by_division`, `get_activity_allocations`, `get_unallocated_resources`, `get_cost_by_product` — unchanged.

Donor candidate identification stays as a composed flow over `get_unallocated_resources` + `get_activity_allocations`. No dedicated donor tool.

## Phase B — AgentService wiring

In `WorkforceMapper.Server/Services/AgentService.cs`:
- Remove the `get_overallocated_resources` tool definition and its dispatch arm.
- Add two new tool definitions in the same Anthropic SDK format used for the existing tools.
- Tool dispatch hands off to `DataService.GetApplicationsAsync` and `DataService.GetProductRiskAsync`.

`POST /api/chat` request/response shape is unchanged.

## Phase C — System prompt rewrite

Rewrite `docs/build/agent-system-prompt.md` verbatim. Keep the format and tone of the existing prompt; change the substantive bullets:

- Drop the bullet about `allocation_pct > 100% indicating over-allocation`. Replace with: `the 100% allocation rule is hard — the API rejects any mapping change that would push a resource above 100% or target a month in which the resource is inactive.`
- Add bullets describing application risk (high/medium/low against min and recommended Support FTE) and product risk (max-wins over the transitive application dependency closure).
- Keep the `get_cost_by_product` Unattributed bullet as-is.
- Keep the active flag bullet, the month-default bullet, and the answer-style guidance.

The `AgentService` reads the prompt verbatim from this file or an embedded constant — keep the existing mechanism, just update the text.

## Phase D — Tests

### `AgentServiceTests`

- `RunAsync_ToolList_ContainsRiskTools_AndDoesNotContainOverallocatedResources` — startup smoke test.
- `RunAsync_CallsApplicationRiskTool_WhenAskedAboutHighRiskApps`
- `RunAsync_CallsProductRiskTool_WhenAskedAboutAffectedProducts`
- `RunAsync_ComposesDonorCandidates_FromUnallocatedAndActivityAllocations`

Existing `RunAsync_*` tests for the other four tools should stay green without modification.

## Phase E — Inline doc updates

- `docs/build/agent-system-prompt.md` — full rewrite per Phase C.
- `docs/build/api-contracts.md` — update the NL Query Tools table: remove `get_overallocated_resources`, add `get_application_risk` and `get_product_risk` with their key outputs.
- `CONTEXT.md` mentions of the five tools and ADR 3 (`get_overallocated_resources` named) belong to the doc-updates slice (#15).

## Out of scope

- Streaming protocol changes. SSE format unchanged.
- Anthropic SDK version bumps.
- Adding new tools beyond the two named.

**Output:** AgentService tool registry change + system prompt rewrite + tool tests.
