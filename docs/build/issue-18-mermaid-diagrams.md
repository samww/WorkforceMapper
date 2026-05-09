# Issue #18 — Mermaid dependency diagrams

> Dependency graph contract and risk propagation rules in `docs/build/risk-model.md`.
> `DependencyGraph` service in `WorkforceMapper.Server/Services/DependencyGraph.cs`.
> `ApplicationRiskDto` and related DTOs in `WorkforceMapper.Server/Models/Dtos.cs`.

Replace the expandable dependency tree in `ProductDetail` and add a dependency diagram to `ApplicationDetail`, both rendered with Mermaid.js.

---

## Phase A — Backend: DependencyGraph reverse traversal

Add a `_reverseEdges` dictionary to `DependencyGraph` — built in the constructor from the same edge list, but with source and target swapped:
```csharp
foreach (var (appId, dep) in edges)
{
    if (!_reverseEdges.TryGetValue(dep, out var set))
        _reverseEdges[dep] = set = [];
    set.Add(appId);
}
```

Add a new public method:
```csharp
public IReadOnlySet<int> TransitiveDependentsOf(int appId)
```
This traverses `_reverseEdges` from `appId` upward (BFS), identical structure to `TransitiveDependenciesOf` but using `_reverseEdges`. Returns all app IDs that directly or transitively depend on `appId`, excluding `appId` itself.

---

## Phase B — Backend: ApplicationRiskDto extension

Add `List<ContributingAppDto> dependents` as the final parameter of `ApplicationRiskDto` in `Dtos.cs`. `ContributingAppDto` already has `{ id, name, risk }` — no changes needed (if issue #16 added `division`, include it here too).

In `DataService.GetApplicationsAsync`: build the full `appRiskById` and name lookup in a first pass over all applications, then in a second pass populate `dependents` by calling `graph.TransitiveDependentsOf(app.Id)`, filtering to known app IDs, projecting to `ContributingAppDto`, and ordering by name. The two-pass structure is necessary so each app's dependents can resolve names from the sibling list.

---

## Phase C — Frontend: Mermaid component

Install: `npm install mermaid` in `workforcemapper.client`.

Create `src/MermaidDiagram.tsx`:
```tsx
import { useEffect, useRef } from 'react';
import mermaid from 'mermaid';

mermaid.initialize({ startOnLoad: false, theme: 'dark' });

let idCounter = 0;

export default function MermaidDiagram({ definition }: { definition: string }) {
  const ref = useRef<HTMLDivElement>(null);
  const id = useRef(`mermaid-${++idCounter}`);

  useEffect(() => {
    if (!ref.current) return;
    mermaid.render(id.current, definition).then(({ svg }) => {
      ref.current!.innerHTML = svg;
    });
  }, [definition]);

  return <div ref={ref} className="overflow-x-auto" />;
}
```

---

## Phase D — ProductDetail: replace AppNode tree with Mermaid

Convert `direct_apps` + their `dependencies` into a Mermaid `graph TD` definition:

```ts
function buildProductMermaid(directApps: DirectAppNode[]): string {
  const lines: string[] = ['graph TD'];
  const cls = (risk: string) => risk === 'high' ? 'high' : risk === 'medium' ? 'medium' : 'low';
  lines.push('classDef high fill:#dc2626,color:#fff,stroke:#991b1b');
  lines.push('classDef medium fill:#d97706,color:#fff,stroke:#92400e');
  lines.push('classDef low fill:#16a34a,color:#fff,stroke:#166534');
  for (const app of directApps) {
    lines.push(`  app${app.id}["${app.name}"]:::${cls(app.risk)}`);
    for (const dep of app.dependencies)
      lines.push(`  app${app.id} --> app${dep.id}["${dep.name}"]:::${cls(dep.risk)}`);
  }
  return lines.join('\n');
}
```

Replace the `<ul>` / `AppNode` tree in `ProductDetail` with:
```tsx
<MermaidDiagram definition={buildProductMermaid(product.direct_apps)} />
```

Remove the `AppNode` component and `RiskBadge` import if no longer used.

---

## Phase E — ApplicationDetail: add dependents diagram

Update `ApplicationRisk` TypeScript interface in `ApplicationsView.tsx` to add:
```ts
dependents: { id: number; name: string; risk: string }[];
```

In `ApplicationDetail`, after the "Dependent Products" table, add a "Downstream Dependents" section:

```tsx
function buildAppMermaid(dependents: ContributingApp[], selfName: string): string {
  const lines = ['graph TD'];
  lines.push('classDef high fill:#dc2626,color:#fff,stroke:#991b1b');
  lines.push('classDef medium fill:#d97706,color:#fff,stroke:#92400e');
  lines.push('classDef low fill:#16a34a,color:#fff,stroke:#166534');
  // Self node (unstyled or neutral)
  lines.push(`  self["${selfName}"]`);
  for (const dep of dependents) {
    const cls = dep.risk === 'high' ? 'high' : dep.risk === 'medium' ? 'medium' : 'low';
    lines.push(`  app${dep.id}["${dep.name}"]:::${cls} --> self`);
  }
  return lines.join('\n');
}
```

Only render the section if `app.dependents.length > 0`. Title: "Apps that depend on this application".

---

## Phase F — Tests

Add to `DependencyGraphTests`: direct-only case, transitive case, and leaf node returns empty. Existing `TransitiveDependenciesOf` tests must stay green.

## Out of scope

No new API endpoints. Diagram is read-only (no zoom/edit). No changes to `ProductsView` list.

**Output:** Mermaid diagrams on ApplicationDetail and ProductDetail; `DependencyGraph.TransitiveDependentsOf`; `ApplicationRiskDto` extended with `dependents`.
