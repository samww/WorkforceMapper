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

## Dependency graph

In-memory BFS over a snapshot of `application_dependencies`. Built once per request from a snapshot, not cached across requests.

Methods:
- `TransitiveDependenciesOf(appId) -> set<appId>` — every app reachable via depends-on edges; excludes the seed.
- `ProductsAffectedByApp(appId) -> set<productId>` — every product whose closure includes `appId`.

Cycle handling: visited-set termination. Cycles must not infinite-loop.

## Cost attribution boundary

Cost attribution is unchanged by the risk model. Risk uses transitive `application_dependencies`; cost uses direct `product_applications` only. The Unattributed line in `get_cost_by_product` continues to capture support cost for foundational apps that no product directly lists.

## Donor candidate tiers (for chat narrative — returned by `get_donor_candidates`)

Active resources who could be shifted onto an under-resourced application's Support activity. Returned in six-tier priority order. Within each tier, sorted by `cost_per_fte` ascending. Cumulative FTE and `cumulative_gap_coverage_pct` accumulate across all tiers.

| Tier | Source | Division | `requires_cross_divisional_alignment` |
|---|---|---|---|
| 1 | Support mappings where `coverage > recommended_support_fte` (surplus FTE only) | Same as target app | false |
| 2 | All Minor Changes mappings | Same as target app | false |
| 3 | All Project mappings | Same as target app | false |
| 4 | Support surplus as tier 1 | Other divisions | true |
| 5 | All Minor Changes mappings | Other divisions | true |
| 6 | All Project mappings | Other divisions | true |

Exclusion rule: a resource already mapped to the target application's Support activity must not appear in any tier.

Surplus support FTE for a donor application = `max(0, current_coverage_fte - recommended_support_fte)`. An application at exactly `recommended_support_fte` contributes zero surplus and yields no tier-1/4 candidates.
