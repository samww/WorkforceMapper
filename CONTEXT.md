# WorkforceMapper — Domain Context

Pragmenta Insights workforce capacity tool. Resources (employees, contractors, vendors) are mapped to activities at a percentage allocation per month. The AI layer lets users query capacity, cost, and risk in natural language.

---

## Glossary

| Term | Definition |
|---|---|
| **resource** | A person or vendor slot with a `monthly_rate` and per-month active flags (`jan`–`dec`). Active flag `0` means zero cost regardless of mappings. |
| **activity** | A unit of work with `activity_type` of `Support`, `Minor Changes`, or `Project`. Belongs to either an application or a project (not both). Named `"X - Y"` where X is the application name or `"P{nn} ProjectName"` and Y is the type. |
| **mapping** | A resource allocated to an activity at `percent` for a given `month`. Composite PK: `(resource_id, activity_id, month)`. `percent` is a positive integer (1–100); fractional values are rejected by the API. |
| **allocation_pct** | Sum of mapping percents for a division's **active** resources ÷ (active_resource_count × 100). Denominator is active headcount only — inactive resources do not dilute it. Values > 100% are a valid operational state (over-allocation). |
| **idle_monthly_cost** | `monthly_rate × ((100 − total_allocation_pct) / 100)` — the cost of unmapped time, not the full rate. |
| **active_resource_count** | Resources whose month flag = 1 for the queried month. Differs from `resource_count` (total headcount) when resources join or leave mid-year. |
| **Unattributed** | Mapped cost that cannot be traced to any product (e.g. AuthService support, ObservabilityStack, DevPortal Rebuild). Surfaced as an explicit line by `get_cost_by_product`. |
| **support level** | Per-application thresholds `min_support_fte` and `recommended_support_fte` describing the demand on the engineering team to keep the application running. |
| **capacity coverage** | Sum of `(mapping.percent / 100)` across active resources whose mapping targets an application's Support activity for a month. |
| **project allocation** | Sum of `(mapping.percent / 100)` across active resources whose mapping targets any activity belonging to a project, for a month. Reported in FTE. Parallels [[capacity coverage]] but spans all of a project's activities (projects only have `activity_type = Project`). No minimum or recommended thresholds — projects are not risk-classified. |
| **application risk** | High / medium / low classification of an application based on capacity coverage relative to its `min_support_fte` and `recommended_support_fte`. |
| **product risk** | Max-wins aggregation of application risk over the transitive closure of (direct apps + their `application_dependencies`). |
| **application dependency** | Directed `(application_id, depends_on_application_id)` edge in `application_dependencies`. Used for risk reach; not used for cost attribution. |
| **donor candidate** | An active resource whose capacity could be shifted onto an under-resourced application's Support activity. Sources: idle FTE (`100 - sum(percent)` for the month), surplus Support, Minor Changes, or Project mappings. Returned by `get_donor_candidates` in eight-tier priority order (same-division tiers 1–4 before cross-divisional tiers 5–8). |
| **idle FTE** | A resource's unallocated capacity for a month, computed as `100 - sum(percent)`. The cheapest donor source — no displacement of other work, no opportunity cost. Source for tier-1 (same division) and tier-5 (other division) donor candidates. |
| **surplus support capacity** | Support FTE mapped to an application in excess of its `recommended_support_fte`. Safe to donate without degrading risk classification. Source for tier-2 (same division) and tier-6 (other division). Drained as a shared pool, biggest mapping first — see `docs/build/risk-model.md`. |
| **requires_cross_divisional_alignment** | Flag on a donor candidate row indicating the resource sits in a different division from the target application. A transfer requires explicit cross-divisional agreement before acting. True for all tier-5, 6, 7, 8 candidates; false for tiers 1–4. |
| **MonthlyAllocationSnapshot** | Read-only view of mapping state for a single month. Owns the active-flag rule, the cost rule (`monthly_rate × percent / 100`), and the capacity-coverage primitive. Built once per request via `LoadAsync(context, month)` and consumed by the seven tool methods. New tool methods consume the snapshot instead of re-querying. Does **not** own risk classification (`RiskCalculator`), dependency closures (`DependencyGraph`), or donor-tier orchestration. See `docs/build/monthly-allocation-snapshot.md` and ADR 9. |
| **ActiveResource** | Per-month projection of a `Resource` constructed by `MonthlyAllocationSnapshot`. Bundles `Resource`, `AllocationPercent` (sum of mapping percent), `AllocatedCost`, `IdleCost`, `IdleFte`. Field-baked — callers read derived values, never recompute. |
| **ActiveMapping** | Per-month projection of a `Mapping` constructed by `MonthlyAllocationSnapshot`. Bundles `Resource`, `Activity` (both eagerly loaded), `Percent`, `AllocatedCost`. Carries the per-row cost so consumers do not multiply rate × percent themselves. |
| **SupportCoverageOf(applicationId)** | `MonthlyAllocationSnapshot` method returning an application's `capacity coverage`. The single definition; consumed by `RiskCalculator.ClassifyApplication`, donor target-gap calculation (`min - coverage`), and donor surplus-Support calculation (`coverage - recommended`). |
| **Resource.IsActiveIn(month)** | Instance method on `Resource` that resolves the per-month active flag. The single source of truth for "active flag gates cost". |

---

## Data Model (9 entities)

```
resources ──< mappings >── activities ──> applications ──< product_applications >── products
                                     └──> projects     ──< project_products     >── products
                                          applications ──< application_dependencies >── applications
```

- **resources** — 80 seeded from `data/resources.json`; 4 divisions: Platform Engineering (23), Analytics (24), Data Collection (20), Client Services (13)
- **activities** — 48 seeded via EF `HasData()` from `data/taxonomy.json`; each belongs to one application OR one project; all 18 applications have both Support and Minor Changes; projects prefixed P01–P12
- **mappings** — 3,144 entries seeded from `data/mappings.json` covering all 12 months of 2026; valid total allocation per resource per month is 0-100% (hard cap at 100% enforced by the API; under-allocation is a legitimate state, e.g. idle / unmapped time); all mapping `percent` values are positive integers (1-100). Seed properties (true at load time, not enforced at runtime): each active resource sums to exactly 100% by construction (see `scripts/generator.ts`); at least one application is high-risk in every non-May month. Generated by `scripts/generator.ts` (run via `scripts/generate-monthly-data.ts`); May 2026 is the verbatim baseline.
- **products** — 7; $28.9m total ARR; all 7 transitively depend on AuthService
- **applications** — 18 (`min_support_fte` and `recommended_support_fte` thresholds per application); **projects** — 12; join tables: `product_applications` (26), `project_products` (26), `application_dependencies` (directed dependency edges used for risk propagation, not cost attribution)

Default month: **`2026-05`** (`DEFAULT_MONTH` constant — UI has a month dropdown; do not hardcode elsewhere). Month and division filter state lives in `App.tsx`, passed as `{ month, division }` props to all three tab views (`MappingView`, `ApplicationsView`, `ProductsView`) — do not re-introduce local state. The Ask AI button lives in the `<nav>` in `App.tsx` (always visible, `onClick={() => setChatOpen(o => !o)}`); tab views do not own or render it. Do not render Ask AI inside detail screens.

Frontend component details (props, navigation state, MermaidDiagram, DependencyGraph): `docs/build/frontend-components.md`.

---

## Key Constraints

- Active flag gates all cost — `active flag = 0` → cost = 0 for that month regardless of mappings
- Hard 100% allocation cap — `PUT /api/mappings` and `DELETE /api/mappings` reject writes that exceed the cap or target a month in which the resource is inactive (ADR 6)
- Integer-only percent — `PUT /api/mappings` rejects non-integer or zero `percent` values; valid range is 1–100 per mapping
- No duplicate activity per resource+month — the add and edit forms in `MappingView` block saving a mapping whose `activity_id` already exists for that resource in that month; enforced client-side only (the API uses upsert intentionally for the edit path)
- No freeform SQL — all Claude queries go through the six typed tool methods (ADR 3)
- No Excel upload, no UploadService, no SuggestionService (out of scope)

---

## API Surface

| Method | Endpoint | Notes |
|---|---|---|
| GET | `/api/health` | `{ status, resource_count, mapping_count }` |
| GET | `/api/resources?month=` | Array with nested `mappings`; `active` reflects month flag |
| GET | `/api/activities` | Full list |
| GET | `/api/applications?month=` | Array with risk classification and coverage; each entry includes `dependent_products` and `dependents` |
| GET | `/api/products?month=` | Array with aggregated product risk and dependent ARR; `contributing_apps` includes `division`; `direct_apps` for diagram rendering |
| PUT | `/api/mappings` | Body: `{ resource_id, activity_id, month, percent }`; 400 + `would_exceed_cap` or `resource_inactive_in_month` on violation |
| DELETE | `/api/mappings?resource_id=&activity_id=&month=` | 204 / 404; 400 + `resource_inactive_in_month` if month is inactive |
| POST | `/api/chat` | Body: `{ query, month }` → SSE stream of text tokens |

---

## Tech Stack

- **Backend:** ASP.NET Core (.NET 10), C#, EF Core + SQLite, `Anthropic.SDK` (`claude-haiku-4-5-20251001`)
- **Frontend:** React 19, TypeScript, Vite, Tailwind CSS v4, Vitest + RTL, `mermaid` v11 (dependency diagrams)
