# User-Test Feedback Round 1 — Build Spec

> Source: `docs/temp/feedback.md`. Domain glossary in `CONTEXT.md`. Risk-asymmetry decision: ADR 10.

Four feature items scoped from a user-test session. Independent slices — can ship in any order.

1. Persistent **Ask AI** button in the tab nav
2. Shared-scale **coverage bar** on the Applications tab
3. New **Projects tab** + `get_project_allocation` agent tool + 5th query chip
4. Click-to-sort **column headers** on Applications and Projects tabs

---

## 1. Ask AI button — persistent placement

Move the button out of each tab view's header into the persistent tab nav row.

### Change

In `App.tsx`:
- Inside the `<nav>` at line 34, right-align an Ask AI button next to the tab buttons.
- The button toggles the chat panel: `onClick={() => setChatOpen(o => !o)}`. Always visible — does not hide when the chat is open.
- Label stays `"Ask AI"`. Style consistent with the existing buttons in `MappingView.tsx:82-89` (`bg-blue-600 hover:bg-blue-500 text-white rounded-lg px-4 py-2 text-sm font-medium`).

In `MappingView.tsx`, `ApplicationsView.tsx`, `ProductsView.tsx`:
- Delete the per-view Ask AI button blocks (`MappingView.tsx:82-89`, `ApplicationsView.tsx:98-105`, `ProductsView.tsx:75-83`).
- Drop the `onOpenChat` prop from each component's props interface.
- Remove the `onOpenChat` prop passed at `App.tsx:80,83,110`.

### Out of scope

- No changes to the chat panel internals or streaming logic.
- No keyboard shortcut for the button.

---

## 2. Applications tab — shared-scale coverage bars

Replace per-row bar scaling with a shared scale across visible apps so bar lengths are comparable.

### Change

In `ApplicationsView.tsx`:
- Lift `barMax` out of the `CoverageBar` component (currently computed per-row at line 37).
- Compute `barMax` once over the **visible** apps (after the division filter at line 86):

```ts
const barMax = Math.max(
  ...visible.flatMap(a => [a.current_coverage_fte, a.min_support_fte, a.recommended_support_fte])
);
```

- Pass `barMax` into `CoverageBar` as a prop. Inside, all three ratios (`coveragePct`, `minPct`, `recPct`) divide by `barMax` instead of recomputing per-row.
- No headroom past the max — the longest bar fills the full width.
- Markers and the light-blue fill are otherwise unchanged.

### Out of scope

- No changes to the marker semantics (min/recommended still rendered).
- No changes to the displayed FTE labels under the bar.

---

## 3. Projects tab

New tab listing all 12 projects with their per-month allocation. No risk classification (see ADR 10).

### New glossary term

Already added to `CONTEXT.md`: **`project allocation`** = sum of `(mapping.percent / 100)` across active resources whose mapping targets any activity of the project, for a month. Parallels `capacity coverage` but spans all of a project's activities. Inherits the snapshot's active-resource gate.

### Backend

**New snapshot primitive** in `MonthlyAllocationSnapshot.cs`, alongside `SupportCoverageOf` at line 39:

```csharp
public double ProjectAllocationOf(int projectId) =>
    _mappingsByActivityId.Values
        .SelectMany(x => x)
        .Where(am => am.Activity.ProjectId == projectId)
        .Sum(am => am.Percent / 100.0);
```

**New endpoint** `GET /api/projects?month=` returning:

```json
[
  { "id": 1, "name": "P01 ProjectAlpha", "divisions": ["Analytics", "Platform Engineering"], "allocation_fte": 2.3 }
]
```

- New `ProjectsController` — one-line GET, parallel to `ApplicationsController.cs:11-15`.
- New `DataService.GetProjectsAsync(month)` — **inline** in `DataService`, no dedicated reporter class (logic is small: project list × snapshot lookup + division derivation).
- `divisions` is **taxonomy-driven**: `distinct(activity.Division for activity in project.Activities)`. Stable across months. Sorted alphabetically before serialising.
- Include all 12 projects every month — zero-allocation projects are returned with `allocation_fte: 0` (do not filter them out).

### Frontend

**New `ProjectsView.tsx`** in `workforcemapper.client/src/`:
- Columns: **Name | Divisions | Allocation bar**.
- Initial sort: project code ascending (column 1, Name).
- Allocation bar shares scale across visible projects per the same rules as item 2 above, **minus the min/recommended markers** — solid light-blue fill from 0 to `current_fte / barMax`. No risk badges, no thresholds.
- Division filter: include-if-any — show the project if any of its activities is in the selected division. The displayed `allocation_fte` stays global (not sliced by division).
- Rows are **not clickable**. No hover state, no detail screen.

**Tab wiring** in `App.tsx`:
- Add `'projects'` to the `Tab` union at line 11.
- Add to the tab list at line 35: `(['mappings', 'applications', 'products', 'projects'] as Tab[])`.
- Add the route at the appropriate place in the conditional block (~line 79):
  ```tsx
  {tab === 'projects' && (
    <ProjectsView month={month} division={division} />
  )}
  ```

### AI agent

**New tool method `get_project_allocation`** in `AgentService.cs`:
- Args: optional `project_id`, optional `project_name`, optional `month`. Same shape as `get_application_risk` (`AgentService.cs:247-255`).
- Response per-row: `{ id, name, divisions, allocation_fte }`.
- Response aggregates (pre-computed to prevent the LLM doing arithmetic):
  - `total_project_allocation_fte` — sum of `allocation_fte` across all projects in the response.
  - `projects_ranked_by_allocation` — same list pre-sorted by `allocation_fte` descending.
- Backed by the same `DataService.GetProjectsAsync` call as the REST endpoint.

**New system-prompt paragraph** in `AgentService.cs:BuildSystemPrompt`, alongside the existing per-tool guidance:

> Project allocation (get_project_allocation) returns the total FTE allocated to each project for the month. Projects have no risk classification or capacity thresholds — do not describe a project as "under-resourced", "adequately staffed", or "at risk", and do not invent benchmarks. Report allocation figures only. Use `total_project_allocation_fte` for the company-wide total and `projects_ranked_by_allocation` for "which projects consume the most" questions — do not sort or sum the projects list yourself.

The "do not invent benchmarks" sentence is non-negotiable — every other tool has thresholds to lean on; without that explicit forbid the LLM will pattern-match to the Application playbook (see ADR 10).

### 5th query chip

Append to `ChatPanel.tsx:4-9`:

```ts
'Which projects are consuming the most capacity this month?',
```

Do not replace existing chips.

### Out of scope

- No `ProjectDetail` screen. Rows are not clickable.
- No dependent-products column or any join to `project_products`.
- No risk thresholds, badges, or any visual signal beyond the allocation bar.
- No `get_project_allocation` variants for project-by-product or project-by-division lookups — single tool, optional filters only.

---

## 4. Sortable column headers — Applications + Projects

Click any column header to sort by that column. Click again to toggle direction. Show an arrow on the active sorted column.

### Scope

- Applies to **list views only**: `ApplicationsView` and `ProjectsView`.
- **Excluded:** `ApplicationDetail`, `ProductDetail`, `MappingView`, and any tables inside detail screens.

### Sort state

- **View-local:** sort state lives in each view's component state. Switching tabs resets to the default sort.
- **Survives Month and Division filter changes** — only a tab switch (or detail drill-in) resets.
- **Not persisted** to localStorage.

### Per-column behavior

**Applications tab:**

| Column | First-click direction | Sort key |
|---|---|---|
| Name | asc | `name` |
| Division | asc | `division` |
| Risk | desc | risk ordinal: `high=2, medium=1, low=0` |
| Coverage | asc | raw `current_coverage_fte` (not ratio) |

**Projects tab:**

| Column | First-click direction | Sort key |
|---|---|---|
| Name | asc | `name` (project code is the prefix, so this is naturally P01→P12) |
| Divisions | asc | first division alphabetically: `sort(divisions)[0]` |
| Allocation | desc | `allocation_fte` |

**Tiebreaker on every column:** name asc.

This drops the existing risk-then-gap secondary sort at `ApplicationsView.tsx:77-79`. The current default sort (risk desc, gap desc) becomes "risk desc, name asc" as the initial state.

### Visual

- Active sorted column header: text + ` ▲` or ` ▼` (Unicode `U+25B2` / `U+25BC`). Header colour `text-gray-100`.
- Inactive sortable headers: no arrow placeholder. Hover style matches the tab nav (`App.tsx:40`): `text-gray-400 hover:text-gray-200`, `cursor: pointer`.
- Header element: `<button>` inside the `<th>` for keyboard accessibility, or `<th>` with `role="columnheader"` and `aria-sort` reflecting current state (`ascending` | `descending` | `none`).

### Out of scope

- No multi-column sort (shift-click to add secondary).
- No "remove sort" / "reset to default" affordance — re-clicking the active column toggles direction; switching columns picks a new sort.
- No sort indicator on hover for non-active columns.

---

## File inventory

**New files:**
- `workforcemapper.client/src/ProjectsView.tsx`
- `WorkforceMapper.Server/Controllers/ProjectsController.cs`

**Modified files:**
- `workforcemapper.client/src/App.tsx` — Ask AI button in nav, Projects tab wiring.
- `workforcemapper.client/src/MappingView.tsx` — remove Ask AI button + prop.
- `workforcemapper.client/src/ApplicationsView.tsx` — remove Ask AI button + prop, shared-scale bars, click-to-sort headers.
- `workforcemapper.client/src/ProductsView.tsx` — remove Ask AI button + prop.
- `workforcemapper.client/src/ChatPanel.tsx` — append 5th chip.
- `WorkforceMapper.Server/Services/MonthlyAllocationSnapshot.cs` — add `ProjectAllocationOf`.
- `WorkforceMapper.Server/Services/DataService.cs` — add `GetProjectsAsync`.
- `WorkforceMapper.Server/Services/AgentService.cs` — add `get_project_allocation` tool, system-prompt paragraph, dispatch case.

**Already updated:**
- `CONTEXT.md` — `project allocation` glossary entry.
- `docs/adr/decisions.md` — ADR 10 (Projects are not risk-classified).
