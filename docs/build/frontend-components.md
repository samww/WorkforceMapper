# Frontend Component Reference

## Detail screen components

`ApplicationDetail` — shows support coverage bar, clickable dependent-products table, and back navigation. Props: `app`, `onBack`, `onNavigateToProduct(id)`, `backLabel?` (defaults `"Back to Applications"`; set to product name when navigating back from a product).

`ProductDetail` — shows Mermaid dependency diagram (product circle → app rectangles, clickable nodes) and a Contributing Applications table with clickable app names. Props: `product`, `onBack`, `backLabel?` (defaults `"Back to Products"`), `onNavigateToApp?(id)`.

`MermaidDiagram` — thin wrapper around `mermaid.render`. Uses a unique render ID per invocation (StrictMode-safe). After render, attaches click listeners to `g.node` elements; extracts node IDs via `/flowchart-(.+)-\d+$/` (no leading `^` — Mermaid v11 prefixes the render ID onto element IDs). Props: `definition`, `onNodeClick?(nodeId: string)`.

`diagramBuilders.ts` — pure functions for building Mermaid graph definitions. `buildProductMermaid(directApps, productName)` renders the product as a circle at the top connected to app rectangles coloured by risk.

## Navigation between detail screens

`App.tsx` holds `productBackApp` (ApplicationRisk | null) and `appBackProduct` (ProductRisk | null). When navigating ApplicationDetail → ProductDetail, `productBackApp` is saved; back restores the app. When navigating ProductDetail → ApplicationDetail, `appBackProduct` is saved; back restores the product. Both states are cleared on tab switch. Back button labels update to reflect the destination (`"Back to AuthService"`, `"Back to DataLicensing"`, etc.).

## DependencyGraph

`TransitiveDependentsOf(appId)` — BFS over `_reverseEdges` (built in the constructor from the same edge list with source/target swapped). Returns all apps that directly or transitively depend on `appId`, excluding `appId` itself.

`dependent_products` on `ApplicationRiskDto` uses the transitive closure of every product's dependency graph — a product appears in an application's list if that application is anywhere in the product's transitive dependency set, not only if it is a direct `product_application` entry.
