# Issue #18 — Mermaid dependency diagrams (done)

> Dependency graph contract and risk propagation rules in `docs/build/risk-model.md`.
> `DependencyGraph` service in `WorkforceMapper.Server/Services/DependencyGraph.cs`.
> `ApplicationRiskDto` and related DTOs in `WorkforceMapper.Server/Models/Dtos.cs`.

## What was built

### Backend

**`DependencyGraph.TransitiveDependentsOf(int appId)`** — BFS over `_reverseEdges` (swapped edge list built in constructor). Returns all apps that directly or transitively depend on `appId`, excluding `appId` itself.

**`ApplicationRiskDto`** extended with two new fields:
- `dependent_products: List<ProductRefDto>` — products whose transitive dependency closure includes this application (not just direct `product_applications` links). Computed by building per-product closures, then reversing to an app→products index.
- `dependents: List<ContributingAppDto>` — apps that transitively depend on this application, via `TransitiveDependentsOf`.

**`DataService.GetApplicationsAsync`** uses a two-pass approach: first pass builds `nameById`/`divisionById` and `appRiskById` for all apps; second pass projects DTOs with `dependent_products` (transitive) and `dependents` (reverse BFS).

### Frontend

**`MermaidDiagram.tsx`** — wrapper around `mermaid.render`. Uses a unique `renderId = mermaid-{ts}-{rand}` per effect invocation to prevent StrictMode double-invoke races. Accepts `onNodeClick?(nodeId: string)` — after render, attaches click listeners to `g.node` SVG elements and extracts node IDs via `/flowchart-(.+)-\d+$/` (no `^` — Mermaid v11 prefixes render IDs onto element IDs).

**`diagramBuilders.ts`** — pure functions, testable without DOM:
- `buildProductMermaid(directApps, productName)` — renders product as a circle `(("name"))` at top connected by arrows to app rectangles `["name"]:::riskClass`.

**`ProductDetail`** — replaced expandable AppNode tree with Mermaid diagram (clickable nodes navigate to ApplicationDetail) and a Contributing Applications table (also clickable).

**`ApplicationDetail`** — replaced planned dependents Mermaid diagram with a Dependent Products table (clickable product names navigate to ProductDetail). The `dependents` field remains on the DTO and interface for future use.

**Bidirectional navigation** (App.tsx):
- `productBackApp: ApplicationRisk | null` — saved when navigating AppDetail → ProductDetail; back restores AppDetail.
- `appBackProduct: ProductRisk | null` — saved when navigating ProductDetail → AppDetail; back restores ProductDetail.
- Back button labels update to the source screen name (`"Back to AuthService"`, `"Back to DataLicensing"`).
- Both states cleared on tab switch.

### Deviations from original spec

| Spec (Phase E) | What was built |
|---|---|
| ApplicationDetail: Mermaid dependents diagram showing apps that depend on this app | Replaced with a clickable Dependent Products table (products whose closure includes this app) |
| ProductDetail: diagram showing direct_apps tree only | Diagram shows product as circle → app rectangles; nodes are clickable |
| No inter-screen navigation specified | Full bidirectional navigation with context-aware back labels added |

## Tests added

- `DependencyGraphTests`: 3 tests for `TransitiveDependentsOf` (empty, direct, transitive chain)
- `MermaidDiagram.test.tsx`: sets innerHTML on resolve; StrictMode safety; `onNodeClick` fires with correct node ID
- `diagramBuilders.test.ts`: header + classDefs + product circle; product→app edge; app→dep edge
- `ApplicationDetail.test.tsx`: renders, coverage values, product names/ARR, empty state, click callback
- `ProductDetail.test.tsx`: heading present, container rendered
