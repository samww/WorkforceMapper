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

## Donor candidate (for chat narrative only)

Active resources currently mapped to Project or Minor Changes activities are donor candidates for shifting capacity to a Support activity. Donor identification is composed by the agent from `get_unallocated_resources` and `get_activity_allocations`; there is no dedicated tool.
