# WorkforceMapper — Domain Context

Pragmenta Insights workforce capacity tool. Resources (employees, contractors, vendors) are mapped to activities at a percentage allocation per month. The AI layer lets users query capacity, cost, and over/under-allocation in natural language.

**Fictional company. Any resemblance to real organisations is coincidental.**

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
| **advisory constraint** | The 100% allocation rule. The API accepts any percent value. Violations surface in the UI (red bar, unallocated filter) and via NL query tools. Never enforced at API or database level. |

---

## Data Model (8 entities)

```
resources ──< mappings >── activities ──> applications ──< product_applications >── products
                                     └──> projects     ──< project_products     >── products
```

- **resources** — 80 seeded from `data/resources.json`; 4 divisions: Platform Engineering (23), Analytics (24), Data Collection (20), Client Services (13)
- **activities** — 48 seeded via EF `HasData()` from `data/taxonomy.json`; each belongs to one application OR one project; all 18 applications have both Support and Minor Changes; projects prefixed P01–P12
- **mappings** — seeded from `data/mappings.json`; every active resource in `2026-05` sums to exactly 100%
- **products** — 7; $28.9m total ARR; all 7 transitively depend on AuthService
- **applications** — 18; **projects** — 12; join tables: `product_applications` (26), `project_products` (26)

Default month: **`2026-05`**. Hardcode as a constant throughout.

---

## Key Constraints

- Active flag gates all cost — `active flag = 0` → cost = 0 for that month regardless of mappings
- Advisory-only 100% rule — API returns 200 for any percent value (ADR 5)
- No freeform SQL — all Claude queries go through the five typed tool methods (ADR 3)
- No Excel upload, no UploadService, no SuggestionService (out of scope)

---

## Tech Stack

- **Backend:** ASP.NET Core (.NET 10), C#, EF Core + SQLite, `Anthropic.SDK` (`claude-haiku-4-5-20251001`)
- **Frontend:** React 19, TypeScript, Vite, Tailwind CSS v4, Vitest + RTL
- **Hosting:** Azure Container Apps, Azure AD Easy Auth, Docker multi-stage (Node 22 → .NET 10)

