# Issue #16 — Global month/division controls + loading spinners

> Domain glossary and API contracts in `CONTEXT.md`.

Lift `month` and `division` state into `App.tsx`, add a shared nav-bar dropdown pair, wire both into all three tab views, and add consistent loading spinners.

---

## Phase A — App.tsx state + nav bar

Add to `App.tsx`:
- `const [month, setMonth] = useState(DEFAULT_MONTH)`
- `const [division, setDivision] = useState('All')`
- A `MONTH_OPTIONS` and `DIVISIONS` constant (copy from `MappingView.tsx`, which then imports them from a shared location or just duplicates — either is fine).

Render a filter bar **between the tab buttons and the view content** containing:
- Month `<select>` (same style as the existing one in `MappingView`)
- Division `<select>`

Pass `month` and `division` as props into each tab view.

Remove the internal `month` and `division` state from `MappingView.tsx`; accept them as props instead.

---

## Phase B — Per-view wiring

### MappingView

Change signature to `MappingView({ month, division, onOpenChat })`. Remove internal `month`/`division` `useState`. The `unallocatedOnly` filter and `zeroMonths` logic stay in `MappingView` as before.

### ApplicationsView

Add `division` prop. Filter `apps` client-side after fetch — show all when `'All'`, otherwise filter by `a.division === division`.

### ProductsView

Division filtering requires knowing each product's contributing app divisions. Extend the backend: add `division: string` to `ContributingAppDto` (in `Dtos.cs`), populate it in `DataService.GetProductRiskAsync` using the app's division (derived from its activities, same pattern as `GetApplicationsAsync`). Update the `ContributingApp` TypeScript interface in `ProductsView.tsx`.

Client-side filter: show a product if `division === 'All'` or any of its `contributing_apps` has the matching division.

---

## Phase C — Loading spinners

Add `const [loading, setLoading] = useState(true)` to `ApplicationsView`, `ProductsView`, and `MappingView`. Set to `false` in the `.then()` of the primary fetch. While loading, render a centred spinner — a small circle with `animate-spin` and a single coloured arc (`border-t-blue-400`). Use identical markup across all three views.

---

## Out of scope

- `ContributingAppDto` gains `division`; no other API surface changes.
- `ChatPanel` manages its own month state — no changes.
- `unallocatedOnly` checkbox stays in `MappingView`.

**Output:** Global month/division controls in nav bar, per-view division filtering, consistent loading spinners in all three views.
