# Issue #5 — Mapping UI

> Domain context, glossary, and data model are in `CONTEXT.md`.
> API contracts (Resources, Activities, Mappings endpoints) are in `CONTEXT.md`.

## Phase 5a — Frontend: mapping panel

**Stack:** React 19, TypeScript, Vite, Tailwind CSS v4 (`@tailwindcss/vite`), Vitest, React Testing Library. No component library. Dark mode.

**Vite config:** Set `build.outDir` to `../../WorkforceMapper.Server/wwwroot` so `npm run build` writes directly to the ASP.NET Core static files directory.

### Components

`MappingView.tsx`:
- Resource card grid with division filter dropdown and "show unallocated only" toggle
- Each card: name, role type, seniority, resource type, country, total allocation % (colour-coded: green ≤ 100%, amber 90–100%, red > 100%)
- Add mapping: inline form with activity dropdown and allocation % input
- Edit/remove: pencil/trash icons inline per mapping
- Month selector (defaults to May 2026)

`AllocationBar.tsx`:
- Visual progress bar showing allocation %
- Red fill > 100%, amber 80–99%, green < 80%
- Tooltip: `"CoreDataWarehouse — Application Support: 50% | AuthService Replacement: 60%"`

### Tests

`MappingView.test.tsx`:
- Division filter changes visible cards
- Unallocated filter hides fully-allocated people

Run `npx vitest run` — all tests green.

**Output:** Working mapping panel served by ASP.NET Core
