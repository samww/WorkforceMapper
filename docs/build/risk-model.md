# Risk Model — Reference

> Shared reference for issues #11, #12, #14. Domain glossary in `CONTEXT.md`.

## Capacity coverage (per application, per month)

```
coverage(app, month) = sum( mapping.percent / 100 ) for each mapping where
    mapping.activity = app's Support activity
    mapping.month   = month
    mapping.resource is active in month (active flag = 1)
```

Only Support activities count. Minor Changes and Project mappings do not contribute to coverage. Inactive resources contribute zero regardless of mappings.

## Application risk classification

| Coverage vs threshold | Risk |
|---|---|
| `coverage < min_support_fte` | high |
| `min_support_fte <= coverage < recommended_support_fte` | medium |
| `coverage >= recommended_support_fte` | low |

Boundary rule: exactly at min -> medium; exactly at recommended -> low.

Inputs: `min_support_fte` and `recommended_support_fte` (REAL columns on `applications`), `coverage` (number).

## Product risk aggregation

Product risk = max risk over the transitive closure of:
- direct apps (via `product_applications`), plus
- their app dependencies (via `application_dependencies`, transitive).

Aggregation rule: max-wins. Any high-risk app in the closure makes the product high risk. Order: high > medium > low. No alternative aggregation rules in scope.

## Cost attribution boundary

Cost attribution is unchanged by the risk model. Risk uses transitive `application_dependencies`; cost uses direct `product_applications` only. The Unattributed line in `get_cost_by_product` continues to capture support cost for foundational apps that no product directly lists.

## Donor candidate tiers (for chat narrative — returned by `get_donor_candidates`)

Active resources whose capacity could be shifted onto an under-resourced application's Support activity. Returned in eight-tier priority order. Within each tier, sorted by `AllocationFte` descending. Cumulative FTE and `cumulative_gap_coverage_pct` accumulate across all tiers.

| Tier | Source | Division | `requires_cross_divisional_alignment` |
|---|---|---|---|
| 1 | Idle FTE: active resources where `100 - sum(percent) > 0` for the month | Same as target app | false |
| 2 | Support mappings where `coverage > recommended_support_fte` (surplus FTE only) | Same as target app | false |
| 3 | All Minor Changes mappings | Same as target app | false |
| 4 | All Project mappings | Same as target app | false |
| 5 | Idle FTE: active resources where `100 - sum(percent) > 0` for the month | Other divisions | true |
| 6 | Support mappings where `coverage > recommended_support_fte` (surplus FTE only) | Other divisions | true |
| 7 | All Minor Changes mappings | Other divisions | true |
| 8 | All Project mappings | Other divisions | true |

Exclusion rule: a resource already mapped to the target application's Support activity is excluded from tiers 2–8. It is **included** in tiers 1 and 5 if it has spare capacity (`idle_fte > 0`), because increasing an existing Support allocation displaces nothing. `DonorCandidateRow.IsExistingContributor` is `true` for these rows; `CurrentTargetSupportPct` carries their current percent on the target Support activity. `ComposeMappingChange` renders `"increase {app} Support from {current}% to {current + donable}%"` rather than `"add {donable}%"`, and the clause reads `"(use idle capacity - currently {total}% total allocated)"`.

Idle FTE for a resource in a month = `100 - sum(percent for the resource in the month)`, percentage points. Resources with zero mappings in the month still appear if the month flag is 1. `AllocationFte` on the row is `idle_pp / 100`; `ImpliedMonthlyCost` is `MonthlyRate * AllocationFte`; the row's `Percent` field carries the resource's current total allocation (used to render "currently X% allocated" in the mapping-change string), not a source-mapping percent. `ActivityType` is `"Idle"` and `ActivityName` is `"Idle capacity"`.

Surplus support FTE for a donor application = `max(0, current_coverage_fte - recommended_support_fte)`. An application at exactly `recommended_support_fte` contributes zero surplus and yields no tier-2/6 candidates.
