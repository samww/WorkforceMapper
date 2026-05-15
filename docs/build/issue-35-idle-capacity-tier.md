# Issue #35 — Idle-capacity tier in `get_donor_candidates`

> Spec for `DataService.GetDonorCandidatesAsync` and the agent system prompt.
> Read `docs/build/risk-model.md` (donor candidate tiers section) and `docs/build/issue-29-tiered-donor-candidates.md` first.
> Sub-issue of #34.

## Background

`get_donor_candidates` sources every candidate row from an existing mapping in `mappings`. A resource whose total allocation is below 100% has idle FTE that the tool cannot see, even though that idle FTE is the cheapest possible source of capacity (no displacement of other work, no cross-divisional sign-off, no opportunity cost).

The runtime constraint on resource allocation is `0 <= sum(percent) <= 100`. The exactly-100 property is a seed-data construction artefact, not an invariant — once any UI edit removes or reduces a mapping, idle FTE exists and the donor model becomes blind to it.

Concretely: with AuthService 0.34 FTE below `min_support_fte` and Emily Clarke (Platform Engineering) at 57% allocated, the agent should recommend topping her up by 34% to close the gap in one change. Today it recommends four changes against CoreDataWarehouse surplus support instead, because Emily has no mapping to source a candidate row from.

## What to build

Add idle FTE as the top tier (same-division) and as the first cross-divisional tier. Renumber the existing six tiers to land at 8 total. Idle FTE is the cheapest source — promote it above tier-1 surplus support, which displaces above-recommended Support effort on another app.

### Final tier table

| Tier | Source                                                  | Division          | `requires_cross_divisional_alignment` |
|------|---------------------------------------------------------|-------------------|---------------------------------------|
| 1    | Idle FTE (`100 - sum(percent)` for the resource/month)  | Same as target    | false                                 |
| 2    | Surplus Support (was tier 1)                            | Same as target    | false                                 |
| 3    | Minor Changes (was tier 2)                              | Same as target    | false                                 |
| 4    | Project (was tier 3)                                    | Same as target    | false                                 |
| 5    | Idle FTE                                                | Other divisions   | true                                  |
| 6    | Surplus Support (was tier 4)                            | Other divisions   | true                                  |
| 7    | Minor Changes (was tier 5)                              | Other divisions   | true                                  |
| 8    | Project (was tier 6)                                    | Other divisions   | true                                  |

Within each tier, sort by `AllocationFte` descending — same convention as existing tiers.

### Idle tier semantics

- **Source set**: every active resource (month flag = 1) whose total `percent` for the month is `< 100`, excluding any resource in `excludedResourceIds` (i.e. anyone already on the target application's Support activity).
- **`AllocationFte`** (donatable): `(100 - sum(percent)) / 100`. Capped at the gap via the existing largest-remainders pass — no special handling needed.
- **`Percent`** on the row: the resource's current total allocation (used in the rendered mapping change so the user can see "currently 57% allocated, has 43% idle"). This is a re-purposing of the field; the agent should never interpret idle-tier `Percent` as a source-mapping percent.
- **`ActivityType`**: `"Idle"` (new value). `ActivityName`: `"Idle capacity"`.
- **`CostPerFte`**: `m.Resource.MonthlyRate` (same as tier 2/3 — full cost per unit of FTE shifted).
- **`ImpliedMonthlyCost`**: `MonthlyRate * AllocationFte`.

### Exclusion rule

Unchanged: a resource already mapped to the target application's Support activity does not appear in any tier (including idle). They're already a known donor candidate via the existing mapping; the right action there is to raise their percent, not to create a parallel idle row.

### Mapping-change rendering

`ComposeMappingChange` needs a third branch for `ActivityType == "Idle"`:

```
{ResourceName}: add {DonablePercent}% to {targetAppName} Support (use idle capacity — currently {Percent}% allocated)
```

If `RequiresCrossDivisionalAlignment` is true, append the same cross-divisional sign-off suffix as the existing branches.

### Summary paragraph

`ComposeSummaryParagraph` derives a tier number from `ActivityType` and `RequiresCrossDivisionalAlignment`. Extend the switch:

| `ActivityType`     | cross-div = false | cross-div = true |
|--------------------|-------------------|------------------|
| `"Idle"`           | 1                 | 5                |
| `"Support"`        | 2                 | 6                |
| `"Minor Changes"`  | 3                 | 7                |
| `"Project"` (default) | 4              | 8                |

Tier labels:
- 1: `"idle capacity in the same division"`
- 2: `"surplus Support capacity in the same division"`
- 3: `"Minor Changes capacity in the same division"`
- 4: `"Project capacity in the same division"`
- 5: `"idle capacity from other divisions"`
- 6: `"surplus Support capacity from other divisions"`
- 7: `"Minor Changes capacity from other divisions"`
- 8: `"Project capacity from other divisions"`

Update the baked tier-system explanation in the same method:

> "Tier system: tiers 1-4 draw from the same division as {applicationName} (Tier 1 idle capacity, Tier 2 surplus Support, Tier 3 Minor Changes, Tier 4 Project); tiers 5-8 are the same source types from other divisions and require cross-divisional alignment."

The existing `exhaustionNote` (same-division surplus exhausted) is keyed off `TierOf(r) == 1` and `surplusSupportFteAvailable`. Update the condition to `TierOf(r) == 2` (surplus Support is now tier 2). Idle FTE does not have an analogous exhaustion note for this iteration.

### Largest-remainders rounding

`ApplyLargestRemainders` already handles per-row caps generically (`caps[i] = (int)Math.Round(candidates[i].AllocationFte * 100.0)`). Idle tier rows have `AllocationFte = (100 - sum_percent) / 100`, so the cap will be `100 - sum_percent` — exactly the maximum donatable. No code change needed in this method.

### `BuildIdleTier` skeleton

Mirror the existing tier builders' signature and add to the tier list before tier 1:

```csharp
var tier1 = BuildIdleTier(activeMappings, allResources, excludedResourceIds, division, gap, crossDiv: false);
var tier2 = BuildSurplusSupportTier(...);   // was tier1
var tier3 = BuildNonSupportTier(..., "Minor Changes", ..., crossDiv: false);  // was tier2
var tier4 = BuildNonSupportTier(..., "Project",       ..., crossDiv: false);  // was tier3
var tier5 = BuildIdleTier(activeMappings, allResources, excludedResourceIds, division, gap, crossDiv: true);
var tier6 = BuildSurplusSupportTier(..., crossDiv: true);   // was tier4
var tier7 = BuildNonSupportTier(..., "Minor Changes", ..., crossDiv: true);   // was tier5
var tier8 = BuildNonSupportTier(..., "Project",       ..., crossDiv: true);   // was tier6
```

`BuildIdleTier` needs the full active-resource list (not just the active-mappings list) because resources with zero mappings still have idle FTE and must be included. Build a map `resourceId -> total_percent_in_month` from `activeMappings`, then iterate active resources, computing idle = `100 - total` and emitting a row when idle > 0.

## Agent system prompt changes

### `AgentService.BuildSystemPrompt` and `docs/build/agent-system-prompt.md`

The donor-candidate paragraph currently says:

> "The tool returns candidates in six-tier priority order: same-division tiers 1-3 (surplus Support, Minor Changes, Project) come first; cross-divisional tiers 4-6 (surplus Support, Minor Changes, Project from other divisions) follow. Walk tiers in order - exhaust same-division options (tiers 1-3) before presenting cross-divisional options (tiers 4-6)."

Replace with:

> "The tool returns candidates in eight-tier priority order: same-division tiers 1-4 (idle capacity, surplus Support, Minor Changes, Project) come first; cross-divisional tiers 5-8 (the same source types from other divisions) follow. Walk tiers in order - exhaust same-division options (tiers 1-4) before presenting cross-divisional options (tiers 5-8). Idle capacity (tiers 1 and 5) is the cheapest source because it does not displace any other work."

Update `docs/build/agent-system-prompt.md` to mirror the new prompt verbatim.

### `AgentService.BuildTools` — `get_donor_candidates` description

Replace the existing "six-tier" wording with "eight-tier" and the tier source list with: "idle capacity, surplus Support, Minor Changes, Project."

## Documentation updates

- `CONTEXT.md` glossary entries: `donor candidate` (mention idle FTE as a new source, eight-tier total), `surplus support capacity` (now tier 2 / tier 6, not tier 1 / tier 4), `requires_cross_divisional_alignment` (true on tiers 5-8, false on tiers 1-4). Add a new entry `idle FTE` if helpful, but it's already covered by the existing data-model description.
- `docs/build/risk-model.md`: rewrite the "Donor candidate tiers" section with the eight-tier table. Note the idle FTE source formula (`100 - sum(percent)`). Note that a resource with zero mappings still appears in the idle tier.

## Acceptance criteria

- [ ] `BuildIdleTier` exists and produces rows for every active same-division resource where `sum(percent) < 100`, excluding `excludedResourceIds`.
- [ ] Cross-divisional variant produces rows for every active other-division resource with idle FTE, with `RequiresCrossDivisionalAlignment = true`.
- [ ] Resources with zero mappings in the month appear in the idle tier (this is the explicit test that fails today's assumption).
- [ ] Renumbered tiers: the `summary_paragraph` reports same-division surplus support as Tier 2 (not Tier 1), Minor Changes as Tier 3, Project as Tier 4, and cross-divisional equivalents as 6/7/8.
- [ ] The `mapping_changes` string for an idle-tier row reads: `"{Resource}: add {donable}% to {target} Support (use idle capacity - currently {sum}% allocated)"`.
- [ ] Cross-divisional idle rows append the existing cross-divisional sign-off suffix.
- [ ] Largest-remainders pass caps idle-tier donations at `100 - sum(percent)`.
- [ ] All existing tier-numbered tests in `DataServiceTests.cs` (search for `Tier`, `Tier1`, `Tier2`, `Tier3`, `Tier456`, `Tiers123`, `six-tier`) are renumbered to match the new scheme.
- [ ] New tests cover: (a) idle resource surfaces as tier 1; (b) idle resource with zero mappings surfaces; (c) idle tier ordered before surplus-Support tier; (d) idle row excluded when resource is on target Support; (e) cross-divisional idle row has alignment flag true; (f) `mapping_changes` text for idle row.
- [ ] HITL verification: with Emily Clarke at 57% in May 2026 (idle 43%) and AuthService below `min_support_fte` by 0.34 FTE, the chat agent's response to *"Why is AuthService at risk this month and where could we find the capacity to fix it?"* names Emily Clarke as the recommended donor with a single mapping change of "+34% to AuthService Support" (or the largest-remainders rounded equivalent), and identifies the source as Tier 1 idle capacity in the same division.

## Frontend visibility

Indirect - chat agent responses for capacity queries now recommend idle FTE before displacing other work. Verify by setting up the Emily Clarke + AuthService scenario above and observing that the response leads with Emily as a single-change idle-capacity donor.

## Not in scope

- A new `exhaustionNote` for idle FTE (analogous to the surplus-Support exhaustion note). Can be added later if needed.
- Surfacing the idle-tier total in a new pre-computed field on `DonorCandidatesResult` (e.g. `idle_fte_available`). Useful but not required for the agent's behaviour change.
- Backfilling missing mappings in seed data - the seed continues to sum to 100% by construction.
