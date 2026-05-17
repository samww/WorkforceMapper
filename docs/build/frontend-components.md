# Frontend Component Reference

## Detail screen components

`ApplicationDetail` — shows support coverage bar, clickable dependent-products table, and back navigation. Props: `app`, `onBack`, `onNavigateToProduct(id)`, `backLabel?` (defaults `"Back to Applications"`; set to product name when navigating back from a product).

`ProductDetail` — shows Mermaid dependency diagram (product circle → app rectangles, clickable nodes) and a Contributing Applications table with clickable app names. Props: `product`, `onBack`, `backLabel?` (defaults `"Back to Products"`), `onNavigateToApp?(id)`.

`MermaidDiagram` — thin wrapper around `mermaid.render`. Uses a unique render ID per invocation (StrictMode-safe). After render, attaches click listeners to `g.node` elements; extracts node IDs via `/flowchart-(.+)-\d+$/` (no leading `^` — Mermaid v11 prefixes the render ID onto element IDs). Props: `definition`, `onNodeClick?(nodeId: string)`.

`diagramBuilders.ts` — pure functions for building Mermaid graph definitions. `buildProductMermaid(directApps, productName)` renders the product as a circle at the top connected to app rectangles coloured by risk.

## Navigation between detail screens

`App.tsx` holds `productBackApp` (ApplicationRisk | null) and `appBackProduct` (ProductRisk | null). When navigating ApplicationDetail → ProductDetail, `productBackApp` is saved; back restores the app. When navigating ProductDetail → ApplicationDetail, `appBackProduct` is saved; back restores the product. Both states are cleared on tab switch. Back button labels update to reflect the destination (`"Back to AuthService"`, `"Back to DataLicensing"`, etc.).

## Responsive shell navigation

`App.tsx` owns the responsive top-level shell navigation. Below Tailwind's `sm` breakpoint (`640px`), the desktop section tabs are hidden and a top-left hamburger button opens a left slide-in drawer with Mappings, Applications, Products, and Projects. At `640px` and above, the desktop tab bar is visible.

`switchTopLevelSection` is the shared section-switching path for desktop tabs and mobile drawer destinations. It clears `selectedApp`, `selectedProduct`, `productBackApp`, and `appBackProduct`; drawer destination taps also close the drawer. The drawer closes via destination tap, backdrop click, Escape, and its close button. Drawer width is `min(20rem, 85vw)`. `Ask AI`, Month, and Division remain visible global controls outside the drawer.

The nav bar (`<header>`) and the filter bar (month/division dropdowns, `role="region" aria-label="Filter bar"`) share the same visual height. Neither uses `min-h-16`; both are sized by `py-2` outer padding plus their content. Nav bar buttons use `py-1` (not `py-2`) and carry no `min-h-11` constraint — the filter bar's natural select height sets the target row height for both.

For the Applications screen mobile layout, the table remains a table. The `Division` header and cells use `hidden sm:table-cell`, and mobile cells use tighter horizontal padding while preserving desktop spacing. The global Division filter is the mobile way to work by division; there is no visible mobile sort control for the hidden Division column.

## DependencyGraph

`TransitiveDependentsOf(appId)` — BFS over `_reverseEdges` (built in the constructor from the same edge list with source/target swapped). Returns all apps that directly or transitively depend on `appId`, excluding `appId` itself.

`dependent_products` on `ApplicationRiskDto` uses the transitive closure of every product's dependency graph — a product appears in an application's list if that application is anywhere in the product's transitive dependency set, not only if it is a direct `product_application` entry.
