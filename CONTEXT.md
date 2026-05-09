# WorkforceMapper — Domain Context

Pragmenta Insights workforce capacity tool. Resources (employees, contractors, vendors) are mapped to activities at a percentage allocation per month. The AI layer lets users query capacity, cost, and risk in natural language.

---

## Glossary

| Term | Definition |
|---|---|
| **resource** | A person or vendor slot with a `monthly_rate` and per-month active flags (`jan`–`dec`). Active flag `0` means zero cost regardless of mappings. |
| **activity** | A unit of work with `activity_type` of `Support`, `Minor Changes`, or `Project`. Belongs to either an application or a project (not both). Named `"X - Y"` where X is the application name or `"P{nn} ProjectName"` and Y is the type. |
| **mapping** | A resource allocated to an activity at `percent` for a given `month`. Composite PK: `(resource_id, activity_id, month)`. |
| **allocation_pct** | Sum of mapping percents for a division's **active** resources ÷ (active_resource_count × 100). Denominator is active headcount only — inactive resources do not dilute it. Values > 100% are a valid operational state (over-allocation). |
| **idle_monthly_cost** | `monthly_rate × ((100 − total_allocation_pct) / 100)` — the cost of unmapped time, not the full rate. |
| **active_resource_count** | Resources whose month flag = 1 for the queried month. Differs from `resource_count` (total headcount) when resources join or leave mid-year. |
| **Unattributed** | Mapped cost that cannot be traced to any product (e.g. AuthService support, ObservabilityStack, DevPortal Rebuild). Surfaced as an explicit line by `get_cost_by_product`. |
| **support level** | Per-application thresholds `min_support_fte` and `recommended_support_fte` describing the demand on the engineering team to keep the application running. |
| **capacity coverage** | Sum of `(mapping.percent / 100)` across active resources whose mapping targets an application's Support activity for a month. |
| **application risk** | High / medium / low classification of an application based on capacity coverage relative to its `min_support_fte` and `recommended_support_fte`. |
| **product risk** | Max-wins aggregation of application risk over the transitive closure of (direct apps + their `application_dependencies`). |
| **application dependency** | Directed `(application_id, depends_on_application_id)` edge in `application_dependencies`. Used for risk reach; not used for cost attribution. |
| **donor candidate** | An active resource currently mapped to a Project or Minor Changes activity, surfaced as potential capacity to shift onto a Support activity. Composed by the agent from `get_unallocated_resources` and `get_activity_allocations`. |

---

## Data Model (9 entities)

```
resources ──< mappings >── activities ──> applications ──< product_applications >── products
                                     └──> projects     ──< project_products     >── products
                                          applications ──< application_dependencies >── applications
```

- **resources** — 80 seeded from `data/resources.json`; 4 divisions: Platform Engineering (23), Analytics (24), Data Collection (20), Client Services (13)
- **activities** — 48 seeded via EF `HasData()` from `data/taxonomy.json`; each belongs to one application OR one project; all 18 applications have both Support and Minor Changes; projects prefixed P01–P12
- **mappings** — seeded from `data/mappings.json`; every active resource in `2026-05` sums to exactly 100%
- **products** — 7; $28.9m total ARR; all 7 transitively depend on AuthService
- **applications** — 18 (`min_support_fte` and `recommended_support_fte` thresholds per application); **projects** — 12; join tables: `product_applications` (26), `project_products` (26), `application_dependencies` (directed dependency edges used for risk propagation, not cost attribution)

Default month: **`2026-05`**. This is the initial selected value only — the UI exposes a month dropdown so the user can switch months. Do not hardcode `2026-05` beyond the `DEFAULT_MONTH` constant that sets the initial state.

---

## Key Constraints

- Active flag gates all cost — `active flag = 0` → cost = 0 for that month regardless of mappings
- Hard 100% allocation cap — `PUT /api/mappings` and `DELETE /api/mappings` reject writes that exceed the cap or target a month in which the resource is inactive (ADR 6)
- No freeform SQL — all Claude queries go through the six typed tool methods (ADR 3)
- No Excel upload, no UploadService, no SuggestionService (out of scope)

---

## API Surface

| Method | Endpoint | Notes |
|---|---|---|
| GET | `/api/health` | `{ status, resource_count, mapping_count }` |
| GET | `/api/resources?month=` | Array with nested `mappings`; `active` reflects month flag |
| GET | `/api/activities` | Full list |
| GET | `/api/applications?month=` | Array with risk classification and coverage per application |
| GET | `/api/products?month=` | Array with aggregated product risk and dependent ARR |
| PUT | `/api/mappings` | Body: `{ resource_id, activity_id, month, percent }`; 400 + `would_exceed_cap` or `resource_inactive_in_month` on violation |
| DELETE | `/api/mappings?resource_id=&activity_id=&month=` | 204 / 404; 400 + `resource_inactive_in_month` if month is inactive for the resource |
| POST | `/api/chat` | Body: `{ query, month }` → SSE stream of text tokens |

---

## Tech Stack

- **Backend:** ASP.NET Core (.NET 10), C#, EF Core + SQLite, `Anthropic.SDK` (`claude-haiku-4-5-20251001`)
- **Frontend:** React 19, TypeScript, Vite, Tailwind CSS v4, Vitest + RTL

