# Issue #12 — Product risk propagation

> Domain glossary, data model, API surface in `CONTEXT.md`.
> Risk classification rule, dependency graph contract, max-wins aggregation in `docs/build/risk-model.md`.
> Schema and seed for `application_dependencies` are already in place from issue #11 — see `docs/build/issue-11-application-risk.md` Phase A/B.

Propagate application risk to products via the transitive `application_dependencies` closure and surface the result in two new product views, plus update `ApplicationDetail` to render product-risk badges on its dependent-products list.

## Phase A — DependencyGraph (pure module)

`WorkforceMapper.Server/Services/DependencyGraph.cs`. Stateless; constructed per request from a snapshot.

```csharp
public sealed class DependencyGraph
{
    public DependencyGraph(IEnumerable<(int appId, int dependsOnAppId)> edges,
                          IEnumerable<(int productId, int appId)> productApps);
    public IReadOnlySet<int> TransitiveDependenciesOf(int appId);
    public IReadOnlySet<int> ProductsAffectedByApp(int appId);
}
```

BFS over the directed edge set. Cycle handling: visited-set termination — graph must not infinite-loop on cycles. Either reject cycles at construction or terminate via visited-set.

### Unit tests (`WorkforceMapper.Tests/DependencyGraphTests.cs`)

- `TransitiveDependenciesOf_ReturnsEmpty_WhenNoEdges`
- `TransitiveDependenciesOf_ReturnsDirectAndIndirect`
- `TransitiveDependenciesOf_TerminatesOnCycle`
- `TransitiveDependenciesOf_HandlesDiamond`
- `ProductsAffectedByApp_DirectAndTransitive`
- `ProductsAffectedByApp_ReachableThroughMultiplePaths`
- depth-1 vs depth-N traversal sanity

## Phase B — DataService.GetProductRisk

```csharp
Task<List<ProductRiskDto>> GetProductRiskAsync(string month, int? productId = null);
```

Returns one row per product:

```
{
  id, name, arr,
  risk,            // max-wins over closure
  contributing_apps: [
    { id, name, risk }
  ]
}
```

Algorithm per product:
1. Build `DependencyGraph` once for the request from a snapshot of `application_dependencies` and `product_applications`.
2. Compute application risks for `month` via `DataService.GetApplicationsAsync(month)` (existing from issue #11).
3. For each product, take its direct apps + their transitive dependencies; map to risks; aggregate via `RiskCalculator.AggregateProductRisk`.

`contributing_apps` lists the apps in the closure ordered by risk desc, then name asc. Cap to 10 entries to keep payload small.

### Cost attribution invariant

`get_cost_by_product` behaviour and the Unattributed line are unchanged. Add a regression test that confirms.

## Phase C — `GET /api/products` extension

If a `ProductsController` already exists, add a query path for risk; otherwise create:

```
GET /api/products?month=2026-05
Returns 200 with ProductRiskDto[]
Default month: "2026-05"
```

Update `docs/build/api-contracts.md` API Surface table with the new row.

### Integration tests (extends `DataServiceTests` + new `ProductsApiTests`)

- `GetProductRisk_AllSevenProducts_AreHighRisk_When_AuthService_Is_HighRisk` — every product transitively depends on AuthService per CONTEXT.md.
- `GetProductRisk_TransitiveReach_DiscoversIndirectDependency`
- `GetProductRisk_ContributingApps_OrderedByRiskThenName`
- `GetCostByProduct_Unattributed_LineIsUnchanged_PostRiskFeature`

## Phase D — Frontend views

### `ProductsView.tsx`

- Fetches `GET /api/products?month=<selected>` on mount and on month change.
- Renders a sortable list (default sort: risk desc, then ARR desc).
- Per row: name, ARR (USD, rounded to nearest $1k), max-risk badge.

### `ProductDetail.tsx`

- Indented dependency tree, depth cap 2, collapsible subtrees.
- Risk badge per node. Root node is the product; children are its direct apps; grandchildren are app dependencies.
- Header: product name, ARR, max-risk badge.

### `ApplicationDetail` update (from issue #11)

- Render product-risk badges on the dependent-products list. Fetch via `GET /api/products?month=<selected>` and join client-side, or extend `ApplicationRiskDto` to include `dependent_products: [{ id, name, arr, risk }]` server-side. Prefer the server-side join — single round-trip, smaller payload.

### Component tests

Render-smoke only.

## Phase E — Inline doc updates

- Add `GET /api/products?month=` row to `docs/build/api-contracts.md` if not already present.
- Do not touch `CONTEXT.md` or ADRs — those are the doc-updates slice (#15).

## Out of scope

- App-to-app dependency editing in the UI. Graph is immutable.
- Cost-pro-rating across the application dependency graph.
- Alternative aggregation rules. Max-wins is the only rule.

**Output:** DependencyGraph + GetProductRisk + ProductsView + ProductDetail + ApplicationDetail product-risk badges.
