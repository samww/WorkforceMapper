# User-Test Feedback Round 2 — Build Spec

> Source: chat session 2026-05-17, captured in `docs/temp/agent-response.md` + `docs/temp/agent-response-feedback.md`. Domain glossary in `CONTEXT.md`. Tool-output-shape decision: ADR 11. Tool-use decision: ADR 3.

Six items scoped from a chat-session review of the response to `"Why is AuthService at risk this month and where could we find the capacity to fix it?"`. The agent's response combined `get_application_risk` + `get_donor_candidates` and contained one misleading numeric headline (1.3 FTE / 4.3 FTE / "small buffer") that came from a bug in `FewestChangesCombinationSummary`. Beyond that bug, the response showed the LLM still has room to invent prose because parts of the tool output remain raw numbers it has to assemble.

This spec fixes the bug and pushes more deterministic prose into the tools per ADR 11.

1. Fix pre-trim `TotalFte` / `TotalMonthlyCost` in `FewestChangesCombinationSummary` (root cause)
2. Pre-render post-fix outcome sentence in `summary_paragraph`
3. Combined `get_at_risk_application_fix` tool
4. Pre-render `risk_reason` and `dependent_products_summary` on `get_application_risk`
5. Drop (or gate) the pipeline buffer rows on `get_donor_candidates`
6. Pre-compute `cross_divisional_signoffs_required` on the donor result

Items are independent and can ship in any order. Item 3 consumes items 2 and 4; reviewer may choose to split item 3 into a follow-up issue.

### Related work

The pool-budget Tier 2 rewrite of `BuildSurplusSupportTier` (from the 2026-05-17 diagnose session) is **not** in scope here — it lands as a separate change. All test scenarios below assume the pool-budget algorithm is in place; if you are working before that lands, expect different specific numbers but the same structural assertions.

---

## 1. Fix pre-trim totals in `FewestChangesCombinationSummary` ⟵ root cause

### Symptom

Agent response (verbatim, May 2026):

> Total: 1.3 FTE, $14k/month. This solution exceeds the 1.0 FTE gap and brings AuthService to 4.3 FTE, meeting the minimum requirement with a small buffer.

Mapping changes in the same response sum to 55 + 37 + 8 = 100% = 1.0 FTE exactly, bringing AuthService to 4.0 FTE. The "1.3" / "4.3" / "small buffer" framing is wrong.

### Root cause

`WorkforceMapper.Server/Services/DonorCandidateAlgorithm.cs` lines 77–85 trim the stopping row's `DonablePercent` to the remaining-gap percent but leave `AllocationFte` and `ImpliedMonthlyCost` un-trimmed. The summary record (lines 94–101) and `ComposeSummaryParagraph` (lines 242–243) then sum the un-trimmed `AllocationFte` / `ImpliedMonthlyCost` across `comboRows`, overstating the actual fix.

### Change

In the trim block, on the stopping row, also assign:

```csharp
candidates = candidates
    .Select((c, i) => i == stoppingIdx
        ? c with {
            DonablePercent     = trimmedPct,
            AllocationFte      = trimmedPct / 100.0,
            ImpliedMonthlyCost = c.CostPerFte * (decimal)(trimmedPct / 100.0)
          }
        : c)
    .ToList();
```

No change needed in the summary-record or `ComposeSummaryParagraph` — they already sum `AllocationFte` and `ImpliedMonthlyCost`; they will become correct once the trim covers all three fields.

### Tests

In `WorkforceMapper.Tests/DonorCandidateAlgorithmTests.cs`, against the existing Scenario A (`BuildScenario`, gap = 2.0 FTE, last row C trimmed from 60% to 50%):

- `FewestChangesCombination.TotalFte == 2.0` (was 2.10 under the bug).
- `FewestChangesCombination.TotalMonthlyCost == 0.80 * 10_000 + 0.70 * 10_000 + 0.50 * 10_000` (note 0.50, not 0.60).
- `summary_paragraph` contains `"Total: 2.0 FTE"` and does **not** contain `"Total: 2.1 FTE"`.

**New invariant test** — add as an xUnit theory across every existing fewest-changes scenario:

```csharp
Assert.Equal(
    Math.Round(comboRows.Sum(c => c.AllocationFte) * 100),
    comboRows.Sum(c => c.DonablePercent)
);
```

### Out of scope

- Do not change the trim formula itself (it remains `Math.Max(1, (int)Math.Round(remainingGap * 100.0))`).
- Do not change `CumulativeFte` / `CumulativeGapCoveragePct` — those are computed before the trim and reflect the pre-trim selection logic correctly.

---

## 2. Pre-render post-fix outcome sentence in `summary_paragraph`

### Why

After item 1, `TotalFte` is trustworthy. The agent invented a "brings AuthService to 4.3 FTE / small buffer" sentence because that is the information a user wants — and nothing in the tool output supplies it directly. Per ADR 11, anything the agent feels compelled to compose deterministically should be owned by the tool.

### Change

In `DonorCandidateAlgorithm.cs:ComposeSummaryParagraph`, append a sentence between `totals` and `exhaustionNote`. Form:

```
"{appName} coverage rises from {coverageBefore:F1} to {coverageAfter:F1} FTE ({tail})."
```

Where `coverageAfter = coverageBefore + totalFte` and `tail` is selected by post-fix coverage:

| Post-fix coverage | `tail` |
|---|---|
| `>= recommendedSupportFte` | `at the recommended level` |
| `== minSupportFte` (within ±0.005) | `the minimum` |
| `> minSupportFte and < recommendedSupportFte` | `above the minimum` |
| `< minSupportFte` | `still below the minimum` |

The "still below minimum" branch is rare because the cumulative selector trims to exactly the gap, but handle it for the case where the fewest-changes solution genuinely cannot close the gap (e.g. tier-exhaustion fallback).

### Signature change

`ComposeSummaryParagraph` currently takes `(List<DonorCandidateRow>, string, double)`. Extend to:

```csharp
private static string ComposeSummaryParagraph(
    List<DonorCandidateRow> comboRows,
    string applicationName,
    double surplusSupportFteAvailable,
    double coverageBefore,
    double minSupportFte,
    double recommendedSupportFte)
```

The caller (`FindAsync`) already has `currentCoverage`, `application.MinSupportFte`, `application.RecommendedSupportFte` in scope.

### Tests

- New: `summary_paragraph` for Scenario A (gap 2.0, AuthService min 4.0, current coverage 2.0) contains `"coverage rises from 2.0 to 4.0 FTE (the minimum)"`.
- New: build a scenario where the fewest-changes solution overshoots into the recommended-or-above range; assert the paragraph reads `"... (at the recommended level)"`.
- New: build a scenario where the gap exceeds total available capacity (no full coverage); assert the paragraph reads `"... (still below the minimum)"`.

### Out of scope

- Do not duplicate this sentence into a separate field. It belongs inside `summary_paragraph` so the existing "render summary_paragraph verbatim" instruction keeps working.
- Do not change the existing tier-system or exhaustion clauses.

---

## 3. Combined `get_at_risk_application_fix` tool

### Why

The agent currently orchestrates `get_application_risk` + `get_donor_candidates` for the canonical "why is X at risk and how do we fix it" question shape. Two consequences:

- **Drift risk:** the 1.0 FTE gap appears in both outputs, computed independently. If one tool's coverage logic changes and the other doesn't, the agent will render contradictory numbers.
- **Orchestration burden:** the LLM has to know that "at risk + fix" → two tool calls in sequence + a stitch step. That's exactly the kind of multi-step reasoning we keep getting bitten by.

### New tool

**Name:** `get_at_risk_application_fix`

**Args:**

| Arg | Type | Required | Notes |
|---|---|---|---|
| `application_name` | string | yes (or `application_id`) | Case-insensitive exact match, same as `get_application_risk`. |
| `application_id` | int | no | Preferred when known. |
| `month` | string | no | YYYY-MM, defaults to selected month. |

**Returns** (snake_case JSON):

```jsonc
{
  "application": { /* full ApplicationRiskDto, same shape as get_application_risk returns */ },
  "risk_reason": "Coverage 3.0 FTE is 1.0 FTE below the minimum 4.0 FTE (recommended 5.0); risk classification: high.",
  "dependent_products_summary": "Exposes $28.9M ARR across 7 products: BrandTracking Platform, ConsumerPanel, CustomerSatisfaction Suite, DataLicensing, FieldworkServices, MediaMeasurement, RetailInsights.",
  "fewest_changes_combination": { /* existing FewestChangesCombinationSummary, post item 1 + 2 + 6 */ },
  "candidates_pipeline_count": 124,
  "cross_divisional_signoffs_required": [ /* see item 6 */ ],
  "cross_divisional_signoffs_summary": "No cross-divisional sign-off required."
}
```

### Implementation

Thin assembly in `DataService`:

```csharp
public async Task<AtRiskApplicationFixDto?> GetAtRiskApplicationFixAsync(
    int? applicationId, string? applicationName, string month)
{
    var apps = await GetApplicationsAsync(month, applicationId, applicationName);
    var app  = apps.FirstOrDefault();
    if (app is null) return null;
    var donor = await GetDonorCandidatesAsync(applicationId, applicationName, month);
    return AtRiskApplicationFixDto.Assemble(app, donor);
}
```

`AtRiskApplicationFixDto.Assemble` is a static factory in `Models/ServiceResults.cs` that constructs the combined response and shares no logic with the individual reporters — it only stitches their outputs.

### System prompt

Replace the existing two long bullets that describe the at-risk-and-fix workflow (in `BuildSystemPrompt` — search for `get_donor_candidates` and `get_application_risk`) with a single bullet:

> For questions of the form "why is X at risk and how do we fix it", call `get_at_risk_application_fix` with the application name. Render `risk_reason` verbatim as the risk headline, then `dependent_products_summary` verbatim, then `fewest_changes_combination.summary_paragraph` verbatim, then `fewest_changes_combination.mapping_changes` as a bulleted list. If `cross_divisional_signoffs_required` is non-empty, render one bullet per entry; otherwise render `cross_divisional_signoffs_summary` as a single line. Do not recompute counts, totals, post-fix coverage, the tier-system description, ARR sums, or product names — all are baked into the strings.

Keep `get_application_risk` and `get_donor_candidates` registered as separate tools. They still serve portfolio-wide and pipeline-exploration questions.

### Tests

- New `AgentServiceTests`: the canonical AuthService at-risk-and-fix query produces exactly **one** `tool_use` with name `get_at_risk_application_fix`. Existing two-tool orchestration tests are removed (the prompt no longer instructs that workflow).
- New `DataServiceTests`: `GetAtRiskApplicationFixAsync` returns the same `application` shape that `GetApplicationsAsync` returns for the same args, and the same `fewest_changes_combination` shape that `GetDonorCandidatesAsync` returns.

### Out of scope

- Do not remove `get_application_risk` or `get_donor_candidates`.
- Do not introduce a separate `at_risk_application_summary` or `application_fix_summary` field — the combined response already contains all narrative text via items 2, 4, 6.

---

## 4. Pre-render `risk_reason` and `dependent_products_summary`

### Change in `ApplicationRiskDto`

Add two new fields to `Models/ServiceResults.cs:ApplicationRiskDto`:

```csharp
string risk_reason,
string dependent_products_summary
```

### Form

In `ApplicationRiskReporter.ReportAsync`, compose these per app:

**`risk_reason` template** (post-trim, fixed decimal):

| Risk | Template |
|---|---|
| high | `"Coverage {cov:F1} FTE is {gap:F1} FTE below the minimum {min:F1} FTE (recommended {rec:F1}); risk classification: high."` |
| medium | `"Coverage {cov:F1} FTE meets the minimum {min:F1} FTE but is {belowRec:F1} FTE below the recommended {rec:F1} FTE; risk classification: medium."` |
| low | `"Coverage {cov:F1} FTE is at or above the recommended {rec:F1} FTE; risk classification: low."` |

Where `gap = min - cov` and `belowRec = rec - cov`. Use `F1` formatting (one decimal place).

**`dependent_products_summary` template:**

```
"Exposes ${totalDependentArrUsd / 1_000_000:F1}M ARR across {n} product(s): {names joined ', '}."
```

- Product names are already sorted alphabetically in `products` (line 95 of `ApplicationRiskReporter.cs` sorts by name).
- Singular/plural: `{n} product` when `n == 1`, `{n} products` otherwise.
- When `n == 0`, omit the colon and product list: `"Exposes $0.0M ARR across 0 products."`

### System prompt updates

In the `get_application_risk` bullet:

> Render `risk_reason` verbatim — do not reconstruct the coverage/min/recommended sentence from raw fields. Render `dependent_products_summary` verbatim — do not reformat the product list, recompute the ARR sum, or change the alphabetical order.

Keep the existing instructions about `total_dependent_arr_usd` and `dependent_app_count` — they remain useful when the agent wants a number alone, not a sentence.

### Tests

In `WorkforceMapper.Tests/ApplicationsApiTests.cs`, add cases for:

- AuthService (high-risk in seeded May): `risk_reason` matches the high template; `dependent_products_summary` lists 7 products in alphabetical order and reads `"$28.9M ARR across 7 products"`.
- A medium-risk app (seed a custom scenario): `risk_reason` matches the medium template.
- A low-risk app: `risk_reason` matches the low template.

### Out of scope

- Do not change `total_dependent_arr_usd`, `dependent_app_count`, or `transitive_dependent_apps` — they remain raw for other consumers.
- Do not change product ordering. Alphabetical is the existing contract.

---

## 5. Drop (or gate) the pipeline buffer rows

### Why

`DonorCandidateAlgorithm.BufferRows = 5` adds five non-fewest-changes rows to every donor-candidates response. The system prompt instructs the agent to use only `fewest_changes_combination` and the pre-computed `total_available_candidates_count`. The five extra rows are pure token cost in the agent path — there is no current consumer.

### Decision

**Default: Option A — remove entirely.** Search the codebase for any consumer of `candidates` rows where `InFewestChangesCombination == false`; there should be none in the agent path. If a frontend / dashboard consumer exists, escalate before proceeding.

If a consumer does exist, fall back to Option B: add a `verbose` (bool, default false) arg to `get_donor_candidates`. When false, return only fewest-changes rows. When true, retain the existing fewest-plus-buffer behaviour.

### Change

`DonorCandidateAlgorithm.cs:106-109`:

```csharp
// before
var trimmed = stoppingIdx >= 0
    ? candidates.Where(c => !c.InFewestChangesCombination).Take(BufferRows).ToList()
    : candidates.Take(BufferRows * 2).ToList();

// after (Option A)
var trimmed = stoppingIdx >= 0
    ? candidates.Where(c => c.InFewestChangesCombination).ToList()
    : new List<DonorCandidateRow>();
```

Delete the `BufferRows` constant. `total_available_candidates_count` is computed from the pre-trim `candidates.Count` and is preserved — it already pre-computes the pool size for the agent.

### Tests

Update `DataServiceTests` cases that assert `candidates.Count == fewest_changes_combination.row_count + 5` to assert `== fewest_changes_combination.row_count` (Option A) or to pass `verbose: true` (Option B).

In `WorkforceMapper.Tests/DonorCandidateAlgorithmTests.cs:FindAsync_FewestChangesCombination_HasCorrectRowCount`, also assert `result.Candidates.Count == 3` (all three rows in the fewest-changes set, none beyond) under Option A.

### Out of scope

- Do not change the tier-walk or cumulative logic.
- Do not change `total_available_candidates_count` — it remains the canonical pool-size number.

---

## 6. Pre-compute `cross_divisional_signoffs_required`

### Why

The system prompt currently instructs the agent to scan every row's `requires_cross_divisional_alignment` flag and surface warnings. That is exactly the kind of row-scanning ADR 11 says belongs in the tool.

### Change in `FewestChangesCombinationSummary`

Add to `Models/ServiceResults.cs`:

```csharp
public record CrossDivisionalSignoff(
    string ResourceName, string FromDivision, string ToDivision, double Fte
);

public record FewestChangesCombinationSummary(
    // ... existing fields ...
    List<CrossDivisionalSignoff> CrossDivisionalSignoffsRequired,
    string CrossDivisionalSignoffsSummary
);
```

In `DonorCandidateAlgorithm.cs:FindAsync` after the trim step, build:

```csharp
var signoffs = comboRows
    .Where(c => c.RequiresCrossDivisionalAlignment)
    .Select(c => new CrossDivisionalSignoff(c.ResourceName, c.ResourceDivision, division, c.AllocationFte))
    .ToList();

var signoffSummary = signoffs.Count == 0
    ? "No cross-divisional sign-off required."
    : $"Cross-divisional sign-off required for {signoffs.Count} transfer(s).";
```

### System prompt

Replace the existing per-row scan instruction with:

> If `cross_divisional_signoffs_required` is non-empty, render one bullet per entry: `"{resource_name}: {from_division} → {to_division} ({fte} FTE) — requires cross-divisional sign-off"`. Otherwise render `cross_divisional_signoffs_summary` as a single line.

### Tests

Add to `DonorCandidateAlgorithmTests.cs`:

- Same-division-only fewest-changes scenario: `CrossDivisionalSignoffsRequired.Count == 0`, summary reads `"No cross-divisional sign-off required."`.
- Mixed scenario where the fewest-changes set includes at least one tier-5/6/7/8 row: `CrossDivisionalSignoffsRequired.Count` matches the number of cross-div fewest-changes rows; each entry's `FromDivision` and `ToDivision` are correct.

### Out of scope

- Do not remove `requires_cross_divisional_alignment` from `DonorCandidateRow` — it remains the per-row source of truth that the summary is computed from.
- Do not change the tier definitions.

---

## File inventory

**Modified files:**

- `WorkforceMapper.Server/Services/DonorCandidateAlgorithm.cs` — items 1, 2, 5, 6.
- `WorkforceMapper.Server/Services/ApplicationRiskReporter.cs` — item 4.
- `WorkforceMapper.Server/Services/DataService.cs` — item 3 (new method).
- `WorkforceMapper.Server/Services/AgentService.cs` — items 3 (tool registration, `DispatchAsync`), system prompt updates for items 3, 4, 6.
- `WorkforceMapper.Server/Models/ServiceResults.cs` — items 3 (new DTO), 4 (extend `ApplicationRiskDto`), 6 (extend `FewestChangesCombinationSummary`, new `CrossDivisionalSignoff`).
- `docs/build/agent-system-prompt.md` — verbatim copy of the new `BuildSystemPrompt` content (it is reference-only for the actual prompt).
- `docs/build/risk-model.md` — note the post-fix-coverage sentence is part of `summary_paragraph` (item 2); note `cross_divisional_signoffs_required` is the canonical source for cross-div warnings (item 6).
- `CONTEXT.md` — extend the `MonthlyAllocationSnapshot` / glossary entries only if a new term is introduced; otherwise no change.

**New tests:**

- `WorkforceMapper.Tests/DonorCandidateAlgorithmTests.cs` — items 1, 2, 5, 6.
- `WorkforceMapper.Tests/ApplicationsApiTests.cs` — item 4.
- `WorkforceMapper.Tests/AgentServiceTests.cs` — item 3.
- `WorkforceMapper.Tests/DataServiceTests.cs` — item 3 (combined-tool assembly), updates for item 5 (buffer rows).

## Implementation order

1. Item 1 — unblocks item 2 (depends on `TotalFte` being trustworthy).
2. Item 2 — depends on item 1.
3. Item 4 — independent.
4. Item 5 — independent, mechanical.
5. Item 6 — independent.
6. Item 3 — last, consumes items 2, 4, and 6. May be split into a follow-up issue.

## Acceptance criteria

- [ ] Re-running the canonical AuthService query (`"Why is AuthService at risk this month and where could we find the capacity to fix it?"`) against May 2026 produces a response whose post-fix FTE total and post-fix coverage match the sum of percentages in the `mapping_changes` bullets exactly.
- [ ] No "small buffer" prose unless post-fix coverage genuinely exceeds the minimum.
- [ ] The invariant `round(sum(AllocationFte) * 100) == sum(DonablePercent)` holds across every fewest-changes scenario.
- [ ] All existing tests pass; new tests for each item pass.
- [ ] System prompt is measurably shorter in proportion to prose pushed into tools (items 3, 4, 6 each remove agent-side instructions; net effect: fewer tokens per turn).
- [ ] `docs/build/agent-system-prompt.md` is updated to match the new `BuildSystemPrompt`.

## Human verification required

A human must run the chat against May 2026 with the original question, confirm both sections (why-at-risk + capacity-solution) and verify:

1. Post-fix FTE total matches the sum of mapping_change percentages.
2. No invented prose about coverage exceeding the minimum unless it actually does.
3. Cross-divisional signoff bullets appear only when needed.
4. The response is shorter overall (no redundant per-tool boilerplate).

Agent: do NOT close this issue. Stop at "all six items implemented, tests pass, ready for human review."
