# Workforce Mapper — Build Plan

## Purpose

A React/Azure app for workforce capacity data maintenance, extended with an AI layer for natural language reporting.

Directly reflects the system built at Kantar: headcount data loaded into a database, with a UI enabling self-service mapping of people to project and application activities. Resources and mappings are pre-seeded from JSON files at startup — no upload step required. The AI layer (Claude) adds a query interface over the resulting capacity data.

Complements `product-portfolio-rag` (Python, RAG, semantic search) by showing a different pattern: a real data pipeline, a CRUD/mapping UI, and AI as an augmentation layer rather than the whole application. Both projects use the same fictional company (Pragmenta Insights) — the workforce data here is the source behind the cost figures in the portfolio RAG project.

---

## What it demonstrates

| CV claim | Demonstrated by |
|---|---|
| "React/Azure app service with RBAC enabling self-service data maintenance" | The core app: mapping UI, Azure AD auth |
| "Built traceability from workforce to revenue/cost" | Resource monthly rates × allocation % → activity → application or project → product; `get_cost_by_product` tool surfaces the full attribution chain |
| "Copilot Studio agents for CEO executive reporting" | NL query over capacity and cost data — same analytical output, different tooling |
| "Workforce capacity model across multiple PPE divisions" | Per-division resource count and monthly cost view; employees, contractors, and vendor resources all included |
| "Run vs Change" cost separation | Three activity types (Application Support / Application Minor Changes / Project Changes); NL query surfaces the spend split |

This is honest under interview questioning: the architecture matches what was actually built, the AI layer is additive. The project demonstrates C# backend and React frontend (different stack from `product-portfolio-rag`) alongside AI tool-use, evidencing breadth.

---

## Tech Stack

| Layer | Choice | Notes |
|---|---|---|
| Backend | ASP.NET Core 8, C# | Controllers under `WorkforceMapper.Server/Controllers/`; `AddControllers()` / `MapControllers()` in `Program.cs` |
| Database | SQLite | File-based, no setup, readable on GitHub |
| ORM / data access | Entity Framework Core | Code-first with SQLite provider; migrations handle schema evolution |
| AI | Anthropic C# SDK (`Anthropic.SDK`) | `claude-sonnet-4-6`, tool-use for NL query |
| Frontend | Vite + React + TypeScript | |
| Auth | Azure AD Easy Auth | Container Apps managed — zero application auth code |
| Hosting | Azure Container Apps | Same pattern as `product-portfolio-rag` |
| Containerisation | Docker multi-stage build | Node 22 → .NET 8 runtime |

---

## Data Model

Eight entities — resources, a taxonomy layer (applications, products, projects, and their many-to-many joins), activities, and mappings. The layered taxonomy enables cost attribution from resource monthly rates through activities to product level.

**resources** — seeded from `data/resources.json` at startup (if table empty)
- `id` INTEGER PRIMARY KEY AUTOINCREMENT
- `resource_id` TEXT UNIQUE NOT NULL — e.g. `PI00123`; stable identifier preserved across uploads
- `name` TEXT NOT NULL
- `resource_type` TEXT NOT NULL — `Employee | Contractor | Vendor`
- `company` TEXT NOT NULL — `Pragmenta Insights` for employees/contractors; supplier name for vendors (e.g. `Meridian Systems`)
- `seniority` TEXT NOT NULL — `Associate | Analyst | Senior Analyst | Manager | Senior Manager`
- `role_type` TEXT NOT NULL — standardised role name, e.g. `Java Developer`, `Data Scientist`, `Project Manager`
- `division` TEXT NOT NULL — `Platform Engineering | Analytics | Data Collection | Client Services`
- `country` TEXT NOT NULL — `United Kingdom`, `Poland`, `India`
- `monthly_rate` REAL NOT NULL — cost in USD; drives all cost calculations
- `jan` through `dec` INTEGER NOT NULL DEFAULT 0 — `1` if active (and therefore billing) that month, `0` if not

**applications** — seeded from `data/taxonomy.json` at startup
- `id` INTEGER PRIMARY KEY
- `name` TEXT NOT NULL

**products** — seeded from `data/taxonomy.json` at startup
- `id` INTEGER PRIMARY KEY
- `name` TEXT NOT NULL — revenue-generating product line
- `arr_usd` REAL NOT NULL — annual recurring revenue in USD

**product_applications** — many-to-many join, seeded at startup
- `product_id` INTEGER NOT NULL REFERENCES products(id)
- `application_id` INTEGER NOT NULL REFERENCES applications(id)
- PRIMARY KEY (product_id, application_id)

**projects** — seeded from `data/taxonomy.json` at startup
- `id` INTEGER PRIMARY KEY
- `name` TEXT NOT NULL

**project_products** — many-to-many join, seeded at startup
- `project_id` INTEGER NOT NULL REFERENCES projects(id)
- `product_id` INTEGER NOT NULL REFERENCES products(id)
- PRIMARY KEY (project_id, product_id)

**activities** — seeded from `data/taxonomy.json` at startup
- `id` INTEGER PRIMARY KEY
- `name` TEXT NOT NULL — e.g. `CoreDataWarehouse — Application Support`
- `activity_type` TEXT NOT NULL — `Application Support | Application Minor Changes | Project Changes`
- `application_id` INTEGER REFERENCES applications(id) — set for application activities; null for project activities
- `project_id` INTEGER REFERENCES projects(id) — set for project activities; null for application activities
- `division` TEXT NOT NULL

**mappings** — seeded from `data/mappings.json` at startup (if table empty); subsequently created/edited by users in the UI
- `resource_id` INTEGER NOT NULL REFERENCES resources(id)
- `activity_id` INTEGER NOT NULL REFERENCES activities(id)
- `percent` REAL NOT NULL — 0–100
- `month` TEXT NOT NULL — e.g. `"2026-05"`
- PRIMARY KEY (resource_id, activity_id, month)

**100% allocation constraint:** advisory only — the API accepts any `percent` value; the UI surfaces violations via the red over-allocation indicator and the unallocated filter. The seeded `data/mappings.json` is the authoritative reference: every active resource in `2026-05` sums to exactly 100%, with deliberate under-allocation for specific resources (e.g. the two India Analytics Associates who are new hires and not yet mapped).

**Cost model:** a resource's allocated cost for a given month = `monthly_rate × (percent / 100)` **only when the resource's active flag for that month is `1`** (`jan`–`dec` columns). If the active flag is `0`, the resource is not billing and all cost figures — including `implied_monthly_cost` in `get_activity_allocations` and `monthly_cost` in `get_capacity_by_division` — are treated as `0` regardless of any mappings that exist for that month. Summed across activities for a resource, allocation percentages must total 100% per month for a fully-allocated resource. The `product_applications` and `project_products` joins allow this cost to be attributed up to product level: resource monthly rate → activity (% share) → application or project → product(s).

---

## Features

### 1. Mapping UI
- Per-resource card showing current activity allocations for the selected month
- Add/edit/remove mappings with allocation % and month selector
- Division filter; unallocated filter ("show resources with < 100% total allocation")
- Inline save — no form submission pattern; PUT/DELETE on individual mappings

### 2. Natural Language Query
- Chat panel: user asks questions over the mapped capacity data
- Four tools exposed to Claude:

| Tool | Returns |
|---|---|
| `get_capacity_by_division` | `division`, `resource_count` (total headcount for the division), `active_resource_count` (resources with active flag = 1 for the queried month), `resource_type_split` (object with `Employee`, `Contractor`, `Vendor` keys — each containing `count` and `monthly_cost`), `monthly_cost`, `allocated_monthly_cost`, `unallocated_monthly_cost`, `allocation_pct` — where `allocation_pct` = sum of all mapping percents for the division's **active** resources / (active_resource_count × 100); denominator uses only resources whose active flag for the queried month is `1` so inactive resources do not dilute the utilisation figure; values > 100% indicate over-allocation. Optional `month` parameter (default: currently selected month) |
| `get_activity_allocations` | Resources mapped to activities with `activity_id`, `activity_name`, `activity_type`, `name`, `company`, `resource_type`, `percent` and `implied_monthly_cost` — filterable by `activity_id`, `division`, `activity_type`, `month` (default: currently selected month) |
| `get_unallocated_resources` | Resources with total allocation below `threshold_pct` (default 100%), with `name`, `company`, `resource_type`, `division`, `total_allocation_pct`, `idle_monthly_cost` — where `idle_monthly_cost` = `monthly_rate × ((100 - total_allocation_pct) / 100)` (the cost of unmapped time, not the full rate). Optional `month` parameter (default: currently selected month) |
| `get_overallocated_resources` | Resources with total allocation above `threshold_pct` (default 100%), with `name`, `company`, `resource_type`, `division`, `total_allocation_pct`, `overallocation_pct`. Optional `month` parameter (default: currently selected month) |
| `get_cost_by_product` | Per-product: `product_name`, `arr_usd`, `total_monthly_cost` derived through activity → application → product and activity → project → product chains; plus an explicit `"Unattributed"` line (null `arr_usd`) for mapped cost that cannot be traced to any product (e.g. DevPortal Rebuild, infrastructure activities with no `product_applications` or `project_products` entry). Optional `month` parameter (default: currently selected month) |

- Starter questions displayed on empty state:
  - *"Which divisions are over-allocated this month?"*
  - *"What is the monthly workforce cost of the DataLicensing product?"*
  - *"What's the Run vs Change spend split in Analytics?"*
  - *"How much are we spending on the AuthService Replacement project per month?"*

---

## Backend Architecture

```
Program.cs
  └── builder.Services.AddControllers() / app.MapControllers()

Controllers/
  ├── HealthController.cs     ← GET  /api/health      — status, resource_count, mapping_count
  ├── ResourceController.cs   ← GET  /api/resources?month=2026-05  — resources with current mappings for month (defaults to currently selected month); response: array of resource objects each with nested `mappings` array:
  │                              `{ id, resource_id, name, division, resource_type, monthly_rate, active, total_allocation_pct, mappings: [{ activity_id, activity_name, activity_type, percent }] }`
  ├── ActivityController.cs   ← GET  /api/activities  — full seeded list
  ├── MappingController.cs    ← PUT  /api/mappings    — upsert a single mapping; body: `{ "resource_id": int, "activity_id": int, "month": "2026-05", "percent": float }`
  │                              DELETE /api/mappings  — remove a mapping; query params: `?resource_id=1&activity_id=5&month=2026-05`
  └── ChatController.cs       ← POST /api/chat        — SSE NL query stream (tool-use loop)

Services/
  ├── DataService.cs          ← EF Core queries for resources, activities, mappings, capacity
  └── AgentService.cs         ← tool-use loop, SSE streaming

**AgentService system prompt** — sent as the `system` parameter on every Claude API call:

```
You are a workforce capacity analyst for Pragmenta Insights, a mid-size data analytics company.
You have access to tools that query the company's workforce allocation data. Use them to answer
questions about capacity utilisation, monthly cost, product attribution, and run/change spend.

Key facts about the data:
- All costs are in USD; rates are monthly.
- allocation_pct values above 100% indicate over-allocation (a resource's mapped percent totals
  exceed 100%) — this is a real operational condition, not a data error.
- get_cost_by_product returns an "Unattributed" line for mapped cost that cannot be traced to
  any product (e.g. AuthService support, ObservabilityStack support, DevPortal Rebuild — shared
  infrastructure overhead). This is deliberate, not a data quality issue.
- active_resource_count reflects resources billing in the queried month (active flag = 1);
  resource_count is total headcount. These differ when resources join or leave mid-year.
- The currently selected month is passed as the default for all tool calls unless the user
  specifies a different month.

Respond concisely and precisely. When surfacing cost figures, round to the nearest $1,000.
When a question cannot be answered with the available tools, say so clearly.
```
```

SQLite initialisation, taxonomy seeding (via EF `HasData()`), and resource/mapping seeding (from JSON files, if tables empty) are all handled at startup in `Program.cs` via `context.Database.Migrate()` — no separate DatabaseService class.

---

## Frontend Architecture

```
App.tsx                          ← two-panel layout; header: "80 resources loaded — May 2026"; single global month state passed to both panels
  ├── MappingView.tsx            ← resource cards, allocation editor, division + unallocated filters; uses global month for mapping reads/writes
  │     └── AllocationBar.tsx   ← visual allocation % bar, over-allocation highlighted in red
  └── ChatPanel.tsx             ← NL query input, SSE token stream, starter question chips; passes global month as default to all tool calls
        └── ToolCallIndicator.tsx  ← shows which tool Claude is calling (e.g. "Checking capacity by division…")
```

---

## Deployment

- `docker-compose.yml` — mounts `./data` (read-only) to `/app/data`; SQLite named volume `sqlite_data`
- `Dockerfile` — multi-stage: Node 22 builds frontend → .NET 8 runtime serves API + static files
- Azure Container Apps — `workforce-mapper-backend`, `workforce-mapper-frontend`
- `ANTHROPIC_API_KEY` as Container Apps secret
- Azure AD Easy Auth on both apps — same pattern as `product-portfolio-rag`

**Verification:** `docker compose up` → wait for seed → open app → MappingView shows 80 resources across 4 divisions → chat query *"Which divisions have unallocated monthly cost this month?"* → Data Collection ~$18k idle, Platform Engineering over-allocated at 112%.

---

## Documentation

- `README.md` — setup, local run, deploy, demo walkthrough
- `CLAUDE.md` — agent build instructions (this file, committed to repo root)
- `docs/decisions/`
  - `001-sqlite-vs-azure-sql.md` — why SQLite is sufficient for a portfolio demo
  - `002-suggestions-not-auto-apply.md` — why AI suggestions require human confirmation
  - `003-tool-use-vs-freeform-sql.md` — why structured tools over freeform SQL generation
- `docs/DEMO_SCRIPT.md` — upload walkthrough + 3 NL query demos with expected output
- `data/sample-data/headcount-template.xlsx` — blank template with column headers and validation notes

---

## Synthetic Data Specification

Both synthetic datasets use the same fictional company: **Pragmenta Insights** — the mid-size data analytics company from `product-portfolio-rag`. The workforce data here is the operational source behind the application cost figures in that project.

> **Pragmenta Insights is a fictional company. Any resemblance to real organisations is coincidental.**

### Resource schema (Excel columns)

Each row in the headcount Excel file maps to one `resources` record:

| Column | Type | Required | Valid values |
|---|---|---|---|
| Resource ID | Text | Yes | `PI#####` format — stable identifier preserved on re-upload |
| Name | Text | Yes | Full name |
| Resource Type | Text | Yes | `Employee`, `Contractor`, `Vendor` |
| Company | Text | Yes | `Pragmenta Insights` for Employee/Contractor; supplier name for Vendor |
| Seniority | Text | Yes | `Associate`, `Analyst`, `Senior Analyst`, `Manager`, `Senior Manager` |
| Role Type | Text | Yes | Standardised role name, e.g. `Java Developer`, `Data Scientist` |
| Division | Text | Yes | `Platform Engineering`, `Analytics`, `Data Collection`, `Client Services` |
| Country | Text | Yes | `United Kingdom`, `Poland`, `India` |
| Monthly Rate | Number | Yes | USD monthly cost — basis for all cost calculations |
| Jan–Dec | Integer ×12 | Yes | `1` if active (billing) that month, `0` if not |

### Taxonomy schema (`data/taxonomy.json`)

Single JSON file seeded at startup, containing all reference data:

```json
{
  "applications": [{ "id": 1, "name": "CoreDataWarehouse" }],
  "products": [{ "id": 1, "name": "DataLicensing" }],
  "product_applications": [{ "product_id": 1, "application_id": 1 }],
  "projects": [{ "id": 1, "name": "AuthService Replacement" }],
  "project_products": [{ "project_id": 1, "product_id": 1 }],
  "activities": [{
    "id": 1,
    "name": "CoreDataWarehouse — Application Support",
    "activity_type": "Application Support",
    "application_id": 1,
    "project_id": null,
    "division": "Platform Engineering"
  }]
}
```

### Target resources (80 across 4 divisions)

Resource mix: **51 employees (~64%)** (Pragmenta Insights), **8 contractors (~10%)** (Pragmenta Insights), **21 vendors (~26%)** (Meridian Systems — IT and engineering staff augmentation; Quantex Analytics — data and analytics specialists). Names are realistic and internationally diverse, reflecting UK/Poland/India locations.

Monthly rates are calibrated so that the sum of monthly resource cost allocated to each application's Application Support and Application Minor Changes activities approximates that application's `annual_cost_workforce` figure in `product-portfolio-rag` (e.g. CoreDataWarehouse `annual_cost_workforce` = $650k → ~$54k/month allocated across CoreDataWarehouse — Application Support and CoreDataWarehouse — Application Minor Changes). The two projects therefore reconcile: the workforce data here is the operational source for the cost figures in the portfolio RAG project.

Monthly rates vary by resource type, seniority, and country:

| Seniority | UK Employee | Poland Employee | India Employee | Contractor (UK) | Vendor |
|---|---|---|---|---|---|
| Senior Manager | $17,000 | — | — | $21,000 | — |
| Manager | $13,000 | $8,000 | — | $16,000 | — |
| Senior Analyst | $9,000 | $5,800 | $4,000 | $11,000 | $13,000–$14,000 |
| Analyst | $6,500 | $4,200 | $2,800 | $8,000 | $7,000–$10,000 |
| Associate | $5,000 | $3,200 | $2,200 | — | — |

**Platform Engineering (23 resources, ~$231k/month resource cost)**
- 13 employees: 2 Senior Managers (Solutions Architects, UK), 2 Managers (Technical Leads, UK), 3 Senior Analysts (Java developers, UK × 1 / Poland × 2), 3 Analysts (Java developers, Poland), 2 Senior Analysts (Python, UK), 1 Senior Analyst (Security, UK)
- 3 contractors (Pragmenta Insights): 2 Senior Analysts (DevOps, UK), 1 Analyst (QA, UK)
- 7 vendors (Meridian Systems): 3 Senior Analyst-equivalent Java developers (UK; 2 joined in February), 2 Analyst-equivalent Java developers (Poland), 2 Senior Analyst-equivalent QA engineers (UK)

**Analytics (24 resources, ~$213k/month resource cost)**
- 14 employees: 1 Senior Manager (Principal Data Scientist, UK), 1 Manager (Analytics Engineering Lead, UK), 3 Senior Analysts (Data Scientists, UK), 2 Analysts (Data Scientists, UK × 1 / Poland × 1), 2 Senior Analysts (ML Engineers, UK), 1 Senior Analyst (Data Engineer, UK), 3 Senior Analysts (BI Developers, UK), 1 Analyst (Data Analyst, UK)
- 2 contractors (Pragmenta Insights): 2 Senior Analysts (Data Engineers, UK)
- 6 vendors (Quantex Analytics): 2 Senior Analyst-equivalent Data Scientists (UK), 2 Analyst-equivalent ML Engineers (Poland), 2 Analyst-equivalent BI Developers (Poland)
- 2 Associate employees (Data Analysts, India) — recent hires joining Q2, not yet mapped (unallocated scenario; Jan–Feb flags = 0)

**Data Collection (20 resources, ~$129k/month resource cost)**
- 12 employees: 1 Senior Analyst (Data Engineer, UK), 2 Analysts (Data Engineers, Poland), 2 Associates (Data Analysts, India), 2 Senior Analysts (Survey Operations, UK), 4 Analysts (Survey Operations, Poland — 3 are the CallCentre bench), 1 Analyst (Systems Admin, Poland)
- 2 contractors (Pragmenta Insights): 2 Senior Analysts (Field Operations, UK)
- 6 vendors (Meridian Systems): 2 Analyst-equivalent Data Engineers (India), 2 Analyst-equivalent Field Operations (Poland), 2 Analyst-equivalent Survey Operations (Poland)

**Client Services (13 resources, ~$122k/month resource cost)**
- 10 employees: 1 Senior Manager (Delivery Manager, UK), 3 Managers (Client Delivery Managers, UK), 2 Analysts (Client Delivery Managers, UK), 2 Senior Analysts (Client Analysts, UK), 2 Analysts (Client Analysts, UK × 1 / Poland × 1)
- 1 contractor (Pragmenta Insights): 1 Senior Analyst (Report Developer, UK)
- 2 vendors (Meridian Systems): 2 Analyst-equivalent Report Developers (Poland)

**Total monthly resource cost: ~$695k/month**, calibrated so that per-application allocated cost reconciles with `product-portfolio-rag`'s `annual_cost_workforce` figures (annual / 12). The remainder of resource cost is allocated to project change activities.

### Target applications (18 across 4 divisions)

| Application | Division (in workforce-mapper) | `annual_cost_workforce` (portfolio-rag) | Implied monthly target |
|---|---|---|---|
| CoreDataWarehouse | Platform Engineering | $650k | $54k |
| APIGateway | Platform Engineering | $280k | $23k |
| SurveyEngine | Platform Engineering | $560k | $47k |
| AuthService | Platform Engineering | $420k | $35k |
| ObservabilityStack | Platform Engineering | $210k | $17.5k |
| InsightHub | Analytics | $420k | $35k |
| DataMapper | Analytics | $390k | $32.5k |
| QuotaManager | Analytics | $280k | $23k |
| SegmentBuilder | Analytics | $350k | $29k |
| PanelIQ | Analytics | $480k | $40k |
| FieldOps | Data Collection | $360k | $30k |
| WebCollect | Data Collection | $310k | $26k |
| SampleRouter | Data Collection | $290k | $24k |
| CallCentre Suite | Data Collection | $300k | $25k |
| DiaryTracker | Data Collection | $320k | $27k |
| ClientPortal | Client Services | $380k | $32k |
| ReportFactory | Client Services | $340k | $28k |
| DeliveryTracker | Client Services | $260k | $22k |

Note on `SurveyEngine`: in `product-portfolio-rag` the application is in the Analytics division (owned by Analytics Engineering); in `workforce-mapper` the BAU support team sits in Platform Engineering (a shared platform-services model). The data layer reflects the workforce reality, not portfolio ownership.

### Target activities (33 across 4 divisions)

**Platform Engineering (10 activities)**

| Name | Activity Type | Application | Project |
|---|---|---|---|
| CoreDataWarehouse — Application Support | Application Support | CoreDataWarehouse | — |
| CoreDataWarehouse — Application Minor Changes | Application Minor Changes | CoreDataWarehouse | — |
| APIGateway — Application Support | Application Support | APIGateway | — |
| SurveyEngine — Application Support | Application Support | SurveyEngine | — |
| AuthService — Application Support | Application Support | AuthService | — |
| ObservabilityStack — Application Support | Application Support | ObservabilityStack | — |
| CoreDataWarehouse Cloud Migration | Project Changes | — | CoreDataWarehouse Cloud Migration |
| AuthService Replacement | Project Changes | — | AuthService Replacement |
| Security & Compliance Programme | Project Changes | — | Security & Compliance Programme |
| DevPortal Rebuild | Project Changes | — | DevPortal Rebuild |

**Analytics (9 activities)**

| Name | Activity Type | Application | Project |
|---|---|---|---|
| InsightHub — Application Support | Application Support | InsightHub | — |
| InsightHub — Application Minor Changes | Application Minor Changes | InsightHub | — |
| DataMapper — Application Support | Application Support | DataMapper | — |
| QuotaManager — Application Support | Application Support | QuotaManager | — |
| SegmentBuilder — Application Support | Application Support | SegmentBuilder | — |
| PanelIQ — Application Support | Application Support | PanelIQ | — |
| ML Forecasting Platform | Project Changes | — | ML Forecasting Platform |
| Real-Time Analytics Initiative | Project Changes | — | Real-Time Analytics Initiative |
| SegmentBuilder Enhancement | Project Changes | — | SegmentBuilder Enhancement |

**Data Collection (8 activities)**

| Name | Activity Type | Application | Project |
|---|---|---|---|
| FieldOps — Application Support | Application Support | FieldOps | — |
| WebCollect — Application Support | Application Support | WebCollect | — |
| SampleRouter — Application Support | Application Support | SampleRouter | — |
| CallCentre Suite — Application Support | Application Support | CallCentre Suite | — |
| DiaryTracker — Application Support | Application Support | DiaryTracker | — |
| CallCentre Suite Replacement | Project Changes | — | CallCentre Suite Replacement |
| DiaryTracker Mobile Rewrite | Project Changes | — | DiaryTracker Mobile Rewrite |
| Panel Automation Programme | Project Changes | — | Panel Automation Programme |

**Client Services (6 activities)**

| Name | Activity Type | Application | Project |
|---|---|---|---|
| ClientPortal — Application Support | Application Support | ClientPortal | — |
| ReportFactory — Application Support | Application Support | ReportFactory | — |
| ReportFactory — Application Minor Changes | Application Minor Changes | ReportFactory | — |
| DeliveryTracker — Application Support | Application Support | DeliveryTracker | — |
| Client Onboarding Automation | Project Changes | — | Client Onboarding Automation |
| API Delivery Programme | Project Changes | — | API Delivery Programme |

### Target products (7) and their associations

The product set is a deliberate subset of the 14 products in `product-portfolio-rag` — chosen to cover all 18 applications above and to keep `DataLicensing` ($6.2m ARR — Pragmenta Insights' highest-revenue product and the centrepiece of the portfolio RAG project) in scope. Together the 7 products represent **$28.9m of in-scope ARR**, against $44.4m for the full 14-product portfolio.

**Products** (ARR figures sourced from `product-portfolio-rag/data/products.json`)

| Product | ARR | Description |
|---|---|---|
| DataLicensing | $6.2m | Syndicated data licensing platform — programmatic access to Pragmenta Insights' panel data, survey databases, and audience segments via API |
| MediaMeasurement | $5.1m | Cross-media audience measurement service tracking TV, digital, and print consumption via diary-based panels |
| RetailInsights | $4.6m | Retail performance analytics combining shopper survey data with panel tracking for FMCG and retail clients |
| BrandTracking Platform | $4.2m | Continuous brand health tracking study measuring awareness, consideration, and preference for FMCG and media clients |
| CustomerSatisfaction Suite | $3.8m | Customer satisfaction and loyalty measurement programme combining transactional surveys, NPS, and driver analysis |
| ConsumerPanel | $3.4m | Syndicated consumer panel providing continuous purchase and behaviour tracking across grocery, health, and personal care |
| FieldworkServices | $1.6m | Managed fieldwork execution service providing face-to-face and telephone interviewing capabilities at scale |

**Product → Application associations** (matches `product-portfolio-rag/data/products.json` `dependent_applications`)

| Product | Applications |
|---|---|
| DataLicensing | CoreDataWarehouse, APIGateway, ClientPortal |
| MediaMeasurement | DiaryTracker, DataMapper, InsightHub, CoreDataWarehouse |
| RetailInsights | SurveyEngine, SegmentBuilder, InsightHub, CoreDataWarehouse |
| BrandTracking Platform | SurveyEngine, PanelIQ, InsightHub, CoreDataWarehouse |
| CustomerSatisfaction Suite | SurveyEngine, WebCollect, ReportFactory, CoreDataWarehouse |
| ConsumerPanel | PanelIQ, WebCollect, DataMapper, SampleRouter |
| FieldworkServices | FieldOps, QuotaManager, SampleRouter, CallCentre Suite |

Apps with no direct product association — these activities land in the `"Unattributed"` line of `get_cost_by_product` by design:
- `AuthService` (application_id 4) — authentication infrastructure; every product transitively depends on it but it is not owned by any single product line
- `ObservabilityStack` (application_id 5) — shared platform observability; same reasoning
- `DeliveryTracker` (application_id 17) — internal project tracking, not a revenue product surface

The `DevPortal Rebuild` project (project_id 4) also lands in `"Unattributed"` — internal developer tooling with no `project_products` entries.

Together these produce approximately **$52k–$70k/month of deliberate unattributed cost** (AuthService Support ~$35k + ObservabilityStack Support ~$17.5k + DevPortal Rebuild ~$11.5k, less DeliveryTracker Support ~$22k which is Client Services overhead). Reviewers should expect this line to be non-zero.

**Project → Product associations**

| Project | Products benefited |
|---|---|
| CoreDataWarehouse Cloud Migration | DataLicensing, MediaMeasurement, RetailInsights, BrandTracking Platform, CustomerSatisfaction Suite (5 products using CoreDataWarehouse) |
| AuthService Replacement | All 7 products (authentication infrastructure — every product transitively depends on AuthService) |
| Security & Compliance Programme | All 7 products |
| DevPortal Rebuild | None directly (internal developer tooling) |
| ML Forecasting Platform | MediaMeasurement, RetailInsights, BrandTracking Platform |
| Real-Time Analytics Initiative | DataLicensing, MediaMeasurement |
| SegmentBuilder Enhancement | RetailInsights |
| CallCentre Suite Replacement | FieldworkServices |
| DiaryTracker Mobile Rewrite | MediaMeasurement |
| Panel Automation Programme | BrandTracking Platform, ConsumerPanel |
| Client Onboarding Automation | CustomerSatisfaction Suite, DataLicensing |
| API Delivery Programme | DataLicensing |

### Per-activity monthly allocation targets

The numbers below are the *intended* allocated monthly cost per activity once mapping is complete. They produce the per-division allocation percentages and project shortfall scenarios listed below. Application Support and Application Minor Changes totals per app reconcile to `product-portfolio-rag`'s `annual_cost_workforce` divided by 12.

**Platform Engineering** ($259k of work allocated against $231k resource pool → ~112%)
- CoreDataWarehouse — Application Support: ~$42k
- CoreDataWarehouse — Application Minor Changes: ~$12k
- APIGateway — Application Support: ~$23k
- SurveyEngine — Application Support: ~$47k
- AuthService — Application Support: ~$35k
- ObservabilityStack — Application Support: ~$17.5k
- CoreDataWarehouse Cloud Migration: ~$25k
- AuthService Replacement: ~$28k *(allocated; ~$42k needed → $14k shortfall)*
- Security & Compliance Programme: ~$18k
- DevPortal Rebuild: ~$11.5k

**Analytics** ($213k of work allocated against $213k resource pool → ~100%, split ~68% AS+AMC / ~32% Projects)
- InsightHub — Application Support: ~$30k
- InsightHub — Application Minor Changes: ~$5k
- DataMapper — Application Support: ~$30k
- QuotaManager — Application Support: ~$20k
- SegmentBuilder — Application Support: ~$25k
- PanelIQ — Application Support: ~$34k
- ML Forecasting Platform: ~$20k *(allocated; ~$30k needed → $10k shortfall)*
- Real-Time Analytics Initiative: ~$27k
- SegmentBuilder Enhancement: ~$22k

**Data Collection** ($111k of work allocated against $129k resource pool → ~86%, ~$18k idle)
- FieldOps — Application Support: ~$22k
- WebCollect — Application Support: ~$20k
- SampleRouter — Application Support: ~$18k
- CallCentre Suite — Application Support: ~$12k *(below normal — telephony wind-down in Q1)*
- DiaryTracker — Application Support: ~$18k
- CallCentre Suite Replacement: ~$9k *(under-resourced — replacement project has not yet ramped)*
- DiaryTracker Mobile Rewrite: ~$7k
- Panel Automation Programme: ~$5k
- *Idle (CallCentre bench, partial allocations): ~$18k*

**Client Services** ($121k of work allocated against $122k resource pool → ~99%)
- ClientPortal — Application Support: ~$32k
- ReportFactory — Application Support: ~$28k
- ReportFactory — Application Minor Changes: ~$4k
- DeliveryTracker — Application Support: ~$22k
- Client Onboarding Automation: ~$20k
- API Delivery Programme: ~$15k

### Intentional data scenarios (designed to make queries interesting)

**Over-allocation — Platform Engineering (~112%)**
AuthService carries a double load: `AuthService — Application Support` is consuming elevated capacity (incidents and manual workarounds due to upcoming vendor EOL), and `AuthService Replacement` needs substantial resource but is under-staffed. The specialist Java and security skill set spans both activities. Several Meridian Systems vendor resources are on fixed-scope contracts that cannot be extended. Total Platform Engineering allocation ~112% when `get_capacity_by_division` is called.

**Bench capacity — Data Collection (~86%, ~$18k/month idle)**
The `CallCentre Suite — Application Support` team has partially stood down as telephony processes were automated in Q1. The replacement project has not yet ramped. Three Survey Operations Specialists in Poland are fully active on the budget (monthly_rate billing) but allocated to less than 100% of activities — approximately $18,000/month of idle monthly cost. Total Data Collection allocation ~86%.

**Run-heavy Analytics (~68% Application Support + Minor Changes / ~32% Project Changes)**
Most Analytics resources are mapped to `InsightHub`, `DataMapper`, `QuotaManager`, `SegmentBuilder`, and `PanelIQ` Application Support. The `ML Forecasting Platform` project is strategically important but under-resourced — approximately $20,000/month allocated vs ~$30,000/month implied need. This creates a striking answer to "what's the Application Support vs Project Changes spend in Analytics?"

**AuthService Replacement shortfall — the centrepiece scenario**
The `AuthService Replacement` project has approximately $28,000/month of resource cost mapped to it, against an implied need of ~$42,000/month to hit the completion deadline before vendor EOL — a $14,000/month shortfall. AuthService is a transitive dependency for **DataLicensing ($6.2m ARR — Pragmenta Insights' highest-revenue product and the centrepiece of `product-portfolio-rag`)** and for the other 6 in-scope products. **Total ARR transitively exposed: $28.9m**.

This is the connective tissue between the two projects: the same AuthService that `product-portfolio-rag` flags as Critical risk (vendor EOL Q2 2026, no renewal path) is shown here to have an under-resourced replacement programme — and because no other Platform Engineering capacity exists (allocation already at 112%), there is no obvious place to find the missing $14k/month.

**5 resources with < 100% allocation**
- 3 in Data Collection (CallCentre transition bench — Poland-based Survey Operations Specialists, ~$4,200/month each)
- 2 in Analytics (recent Associate hires in India, joined in Q2, not yet mapped to activities)

These surface in the `get_unallocated_resources` tool response, including their idle monthly cost.

**Positive scenario — Client Services well-balanced**
Client Services close to 100% allocated, predominantly Application Support, with two well-resourced strategic projects (Client Onboarding Automation and API Delivery Programme — the latter directly supporting DataLicensing). Provides balance — not all stories are risk stories.

**Cost traceability to products**
The `get_cost_by_product` tool demonstrates the full attribution chain. The product totals reflect the four-app dependency lists above plus project investments. Indicative ranking:
- **DataLicensing** (highest workforce cost, despite a narrow application footprint of CoreDataWarehouse + APIGateway + ClientPortal — because all five PE platform projects benefit it, including the AuthService Replacement and the entire Security & Compliance Programme)
- **BrandTracking Platform** and **MediaMeasurement** (broad app and project footprint, plus ML Forecasting Platform investment)
- **FieldworkServices** (smallest ARR and the only product fully exposed to the CallCentre Suite wind-down — most concentrated risk)

---

## Build Plan

### Phase 1 — Synthetic data
**Goal:** Create `data/taxonomy.json` (applications, products, product-application links, projects, project-product links, activities) and `data/sample-data/sample-headcount.xlsx` (80 resources — employees, contractors, vendors), configured to produce the intentional cost and capacity scenarios above.

Steps:
1. Create `data/taxonomy.json` with all taxonomy entities per the schema above: 18 applications, 7 products, product-application associations, 12 projects, project-product associations, and all 33 activities with `activity_type`, `application_id`/`project_id`, and `division`
2. Create `data/sample-data/headcount-template.xlsx` — blank file with the 21 column headers (`Resource ID` through `Dec`) and a Notes sheet with validation rules and valid values per column
3. Create `data/sample-data/sample-headcount.xlsx` — all 80 resources pre-filled per the division/type/seniority breakdown above (PE 23, Analytics 24, Data Collection 20, Client Services 13), with resource IDs (PI00001–PI00080), monthly rates, and month flags per the rate table. Ensure 2 Analytics Associates have Jan–Feb = 0 (Q2 starters); remaining resources active all year.
4. Calibrate resource monthly rates and the allocation % values used in Phase 4 to produce: Platform Engineering ~112%, Data Collection ~86% (~$18k/month idle), Analytics ~68% Application Support / ~32% Project Changes, AuthService Replacement ~$28k/month allocated vs ~$42k/month needed, 5 resources below 100% allocation. Per-application and per-project monthly cost targets are listed in the "Per-activity monthly allocation targets" section above.

**Note on the sample Excel:** The sample file is the live demo artefact — upload it during the walkthrough to load 80 resources and trigger suggestion generation. Monthly rate values must be plausible (vary by seniority + country + resource type per the rate table above).

**Output:** `data/taxonomy.json`, `data/sample-data/headcount-template.xlsx`, `data/sample-data/sample-headcount.xlsx`

---

### Phase 2 — Project setup and data layer
**Goal:** Extend the existing `WorkforceMapper.Server` project with SQLite/EF Core, define entity classes and DbContext, run the initial migration, seed taxonomy via `HasData()` migrations, seed resources and mappings from JSON at startup, and implement the data access layer with tests.

Work within the existing `WorkforceMapper.Server` project. Do not create a new solution or project.

Steps:
1. Add a `WorkforceMapper.Tests` xUnit project to the solution if not already present:
   ```
   WorkforceMapper.slnx
   WorkforceMapper.Server/    ← existing; extend this
   WorkforceMapper.Tests/     ← xUnit test project; add to solution
   ```
2. Add NuGet packages to `WorkforceMapper.Server.csproj`:
   - `Microsoft.EntityFrameworkCore.Sqlite` — EF Core SQLite provider
   - `Microsoft.EntityFrameworkCore.Design` — for `dotnet ef` migrations tooling
   - `Anthropic.SDK` — Anthropic C# SDK
3. Add NuGet packages to `WorkforceMapper.Tests.csproj`:
   - `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`
   - `Microsoft.EntityFrameworkCore.InMemory` — in-memory provider for unit tests
   - `Moq` — for mocking the Anthropic SDK in unit tests
   - `Microsoft.AspNetCore.Mvc.Testing` — for integration tests via `WebApplicationFactory`
4. Define EF Core entity classes in `WorkforceMapper.Server/Models/`:
   - `Resource.cs` — properties matching the resources table schema (including jan–dec int properties); `ICollection<Mapping> Mappings` navigation property
   - `Application.cs` — id and name; `ICollection<ProductApplication>` and `ICollection<Activity>` navigation properties
   - `Product.cs` — id and name; `ICollection<ProductApplication>` and `ICollection<ProjectProduct>` navigation properties
   - `ProductApplication.cs` — join entity; composite key (ProductId, ApplicationId)
   - `Project.cs` — id and name; `ICollection<ProjectProduct>` and `ICollection<Activity>` navigation properties
   - `ProjectProduct.cs` — join entity; composite key (ProjectId, ProductId)
   - `Activity.cs` — properties matching activities schema; nullable `ApplicationId` and `ProjectId` FKs; `ICollection<Mapping>` navigation property
   - `Mapping.cs` — composite key `(ResourceId, ActivityId, Month)`; navigation properties to `Resource` and `Activity`
5. Implement `WorkforceMapper.Server/Data/WorkforceDbContext.cs`:
   - Inherits `DbContext`; registers `DbSet` for all eight entities
   - `OnModelCreating`: configure composite PKs for `Mapping`, `ProductApplication`, `ProjectProduct`; configure nullable FKs on `Activity`; configure unique index on `Resource.ResourceId`
   - Taxonomy seeding via `HasData()`: embed all taxonomy entities (applications, products, product-application links, projects, project-product links, activities) from `data/taxonomy.json` directly in the model configuration — these are applied through EF Core migrations at startup
6. Create and apply the initial migration:
   ```bash
   dotnet ef migrations add InitialCreate --project WorkforceMapper.Server
   dotnet ef database update --project WorkforceMapper.Server
   ```
   The migration file is committed to the repo — reviewers can see the schema definition.
7. Add startup seed logic in `Program.cs` immediately after `context.Database.Migrate()`:

   ```csharp
   // Seed taxonomy is handled by EF HasData() migrations (above)

   // Seed resources from JSON if table is empty
   if (!context.Resources.Any())
   {
       var json = await File.ReadAllTextAsync("data/resources.json");
       var records = JsonSerializer.Deserialize<List<ResourceSeed>>(json,
           new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
       context.Resources.AddRange(records!.Select(r => new Resource { ... }));
       await context.SaveChangesAsync();
   }

   // Seed mappings from JSON if table is empty
   if (!context.Mappings.Any())
   {
       var lookup = context.Resources.ToDictionary(r => r.ResourceId, r => r.Id);
       var json = await File.ReadAllTextAsync("data/mappings.json");
       var records = JsonSerializer.Deserialize<List<MappingSeed>>(json,
           new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
       context.Mappings.AddRange(records!.Select(m => new Mapping {
           ResourceId = lookup[m.ResourceId],
           ActivityId = m.ActivityId,
           Percent = m.Percent,
           Month = m.Month
       }));
       await context.SaveChangesAsync();
   }
   ```

   Define `ResourceSeed` and `MappingSeed` as simple record types in `WorkforceMapper.Server/Data/SeedTypes.cs`, matching the JSON schema (note: `MappingSeed.ResourceId` is the text identifier `PI#####`, not the integer PK — the seed code resolves it via lookup).

8. Write `WorkforceMapper.Tests/DataServiceTests.cs` with tests using the EF Core InMemory provider via a shared test DbContext fixture:
   - `GetResources_Returns80Resources_WhenSeeded`
   - `GetActivities_ReturnsAllActivities_WhenSeeded`
   - `GetMappings_ReturnsCorrectMappings_ForResource`
   - `GetCapacityByDivision_ReturnsCorrectAllocationPct` — seed known data, verify 112% for an over-allocated division
9. Implement `WorkforceMapper.Server/Services/DataService.cs` using EF Core LINQ queries:
   - `GetResourcesAsync(division?, month?)` — returns resources with current mappings via `.Include(r => r.Mappings).ThenInclude(m => m.Activity)`
   - `GetActivitiesAsync(division?, activityType?)` — returns activities with application/project references, optionally filtered
   - `GetCapacityByDivisionAsync(month)` — group by division; returns per-division: `resource_count`, `resource_type_split`, `monthly_cost` (sum of monthly_rate for resources active in that month), `allocated_monthly_cost`, `unallocated_monthly_cost`, `allocation_pct`
   - `GetActivityAllocationsAsync(activityId?, division?, activityType?, month)` — returns resources mapped to activities with percent and implied monthly cost
   - `GetUnallocatedResourcesAsync(thresholdPct, division?, month)` — resources whose total allocation is below threshold, with idle monthly cost
   - `GetCostByProductAsync(month)` — sum resource costs through activity → application → product and activity → project → product chains; returns per-product monthly cost
10. Implement until all DataService tests pass. Run `dotnet test` — all tests green.

**Note:** Register `WorkforceDbContext` in `Program.cs` with `builder.Services.AddDbContext<WorkforceDbContext>(o => o.UseSqlite("Data Source=workforce.db"))`. Call `context.Database.Migrate()` at startup to apply pending migrations and seed taxonomy — then immediately seed resources and mappings from JSON as shown in step 7. All API endpoints must be implemented as Controller classes under `WorkforceMapper.Server/Controllers/` using `[ApiController]` / `[Route]` attributes — do not inline route handlers in `Program.cs`. No separate DatabaseService class is needed.

**Output:** Extended `WorkforceMapper.Server` with EF Core DbContext, initial migration, DataService, resource and mapping seeding at startup, passing xUnit tests

---

### Phase 3 — Excel upload + ETL
**Goal:** Parse a `.xlsx` headcount file, validate it, return a preview of changes, and commit to the `people` table.

Steps:
1. Write `tests/UploadServiceTests.cs`:
   - `ParseExcel_ReturnsCorrectResourceCount_ForValidFile` — parse `sample-headcount.xlsx`, expect 80 records
   - `ParseExcel_ThrowsValidationError_WhenRequiredColumnMissing`
   - `ParseExcel_ThrowsValidationError_WhenResourceTypeInvalid`
   - `ParseExcel_ThrowsValidationError_WhenVendorCompanyIsPragmentaInsights`
   - `GeneratePreview_ReturnsCorrectNewCount_WhenNoResourcesExist`
   - `GeneratePreview_ReturnsCorrectUpdatedCount_WhenResourceAlreadyExists`
   - `CommitUpload_UpsertsCorrectly_MatchingOnResourceId`
2. Implement `Services/UploadService.cs`:
   - `ParseAsync(Stream file)` — ClosedXML reads rows, maps to `ResourceRecord` objects, validates required columns and enum values; collects validation errors (don't throw on first error — collect all)
   - `GeneratePreviewAsync(List<ResourceRecord>)` — compares against existing `resources` records; returns `{New: int, Updated: int, Unchanged: int, Records: List<ResourceRecord>}`
   - `CommitAsync(List<ResourceRecord>)` — upserts all records into `resources` table (match on `resource_id`); returns final count
3. Implement until all UploadService tests pass. Run `dotnet test`.

**API endpoints:**
```csharp
// POST /api/upload — multipart/form-data with "file" field
// Returns: { preview: { new, updated, unchanged }, records: [...] }

// POST /api/upload/commit — body: { records: [...] }
// Awaits SuggestionService synchronously (typically 5–15 seconds for 80 people)
// Returns: { committed: int, suggestions: [ { person_name, activity_suggestions: [...] } ] }
// Frontend shows loading state on "Confirming…" until response arrives — no polling needed
```

**Output:** UploadService, upload and commit endpoints, passing tests

---

### Phase 4 — Mapping API
**Goal:** CRUD endpoints for mappings; allocation validation; API integration tests.

Steps:
1. Write `tests/MappingApiTests.cs` using `WebApplicationFactory<Program>` with a test SQLite database:
   - `PutMapping_Returns200_WithValidAllocation`
   - `PutMapping_Returns400_WhenAllocationExceeds100Pct` — person total allocation > 100%
   - `PutMapping_Returns200_WhenUpdatingExistingMapping`
   - `DeleteMapping_Returns204_WhenMappingExists`
   - `DeleteMapping_Returns404_WhenMappingNotFound`
   - `GetPeople_ReturnsMappings_WhenMappingsExist` — verify mappings are embedded in person response
2. Implement mapping endpoints in `Program.cs`:
   ```csharp
   // PUT /api/mappings
   // Body: { personId, activityId, allocationPct, month }
   // Validates: allocationPct > 0; person's total allocation for the month does not exceed 100%
   // Returns 400 with error message if allocation would exceed 100%

   // DELETE /api/mappings/{personId}/{activityId}/{month}
   // Returns 204 on success, 404 if not found
   ```
3. Add `UpsertMappingAsync` and `DeleteMappingAsync` to `DataService.cs` using EF Core (`context.Mappings.FindAsync(...)` → update or add, then `SaveChangesAsync`)
4. Run `dotnet test` — all tests must pass before proceeding.

**Output:** Mapping endpoints, allocation validation, passing tests

---

### Phase 5 — AI features: suggestions and NL query
**Goal:** Implement two AI capabilities using the Anthropic C# SDK: batch mapping suggestions on upload, and a tool-use NL query agent with SSE streaming.

**Suggestions (SuggestionService.cs):**

Steps:
1. Write `tests/SuggestionServiceTests.cs`:
   - `GenerateSuggestions_ReturnsSuggestionsForEachPerson` — mock the Anthropic client; verify response maps person names to ranked suggestions
   - `GenerateSuggestions_DoesNotAutoApply` — suggestions are returned, not written to mappings table
   - `ParseSuggestionResponse_ExtractsConfidenceAndReasoning` — unit test the JSON parsing logic
2. Implement `Services/SuggestionService.cs`:
   - Single `Messages.CreateAsync` call with all people and activities in the prompt
   - Use `response_format` (structured output) to enforce the suggestion schema:
     ```json
     {
       "suggestions": [
         {
           "person_name": "string",
           "activity_suggestions": [
             { "activity_name": "string", "confidence": 0.0, "reasoning": "string" }
           ]
         }
       ]
     }
     ```
   - Return top 3 suggestions per person, ordered by confidence descending
   - Suggestions with confidence < 0.5 are excluded

**NL Query agent (AgentService.cs):**

Steps:
1. Write `tests/AgentServiceTests.cs`:
   - `RunAsync_CallsCorrectTool_WhenCapacityQueryReceived` — mock Claude; verify `get_capacity_by_division` is called
   - `RunAsync_CallsProductCostTool_WhenProductQueryReceived` — verify `get_cost_by_product` is called for product spend questions
   - `RunAsync_StreamsTokens_ToCallback` — verify the callback is invoked with non-empty tokens
   - `RunAsync_HandlesMultipleToolCalls_InSingleConversation`
2. Implement `Services/AgentService.cs`:
   - Define four tools using Anthropic C# SDK tool definition format:
     - `get_capacity_by_division(division?: string, month?: string)` — resource count and monthly cost by division
     - `get_activity_allocations(activity_id?: int, division?: string, activity_type?: string, month?: string)` — resources mapped to activities with percent and monthly cost
     - `get_unallocated_resources(division?: string, threshold_pct?: number, month?: string)` — bench resources with idle monthly cost
     - `get_cost_by_product(month?: string)` — monthly workforce cost attributed to each product
   - Tool-use loop: send message → if response contains tool calls, execute tools via `DataService`, send tool results back → repeat until no tool calls → stream final text response
   - SSE streaming: `Action<string> onToken` callback, invoked for each streamed text token
3. Implement SSE endpoint in `Program.cs`:
   ```csharp
   // POST /api/chat
   // Body: { message: string, month: string }
   // Sets Content-Type: text/event-stream
   // Streams: data: {token}\n\n for each token
   // Final event: data: [DONE]\n\n
   ```
4. Run `dotnet test` — all tests must pass before proceeding.

**Default month:** Use `"2026-05"` throughout. The system prompt should tell Claude it is operating over Pragmenta Insights' May 2026 workforce data.

**System prompt (instruct Claude to):**
- Answer only from tool results — do not estimate or infer allocation figures
- Always call at least one tool before answering
- Cite specific division names, activity names, monthly cost figures, and allocation percentages from tool results
- Flag clearly when the answer requires data not available in the tools

**Output:** SuggestionService, AgentService, `/api/chat` endpoint, passing tests

---

### Phase 6 — Frontend
**Goal:** React + TypeScript single-page application with three panels: upload, mapping, and chat. Built with Vite; served as static files by the .NET backend.

**Stack:** React 19, TypeScript, Vite, Vitest + React Testing Library, Tailwind CSS v4 (via `@tailwindcss/vite`). No component library — plain Tailwind. Dark mode.

**Vite config:** Set `build.outDir` to `../../src/WorkforceMapper.Api/wwwroot` so `npm run build` writes directly to the directory that ASP.NET Core mounts as static files.

**Components to implement:**

`UploadPanel.tsx`
- Drag-and-drop zone for `.xlsx` files; accepts file via `<input type="file">` as well
- On file select: POST to `/api/upload`, display preview table (new / updated / unchanged counts, scrollable list of records)
- Confirm button: POST to `/api/upload/commit`; show loading state ("Confirming…") while awaiting the response (~5–15 seconds)
- After response arrives: display success state, pass `suggestions` from response body to SuggestionDrawer — which opens immediately

`SuggestionDrawer.tsx`
- Rendered below the upload confirmation once the commit response arrives (suggestions are in the response body — no polling)
- Per-person list: shows top 3 activity suggestions with confidence bar and reasoning
- Accept / Reject buttons per suggestion; accepted suggestions send a PUT to `/api/mappings`
- "Accept all high-confidence" button (accepts all with confidence ≥ 0.8)

`MappingView.tsx`
- Resource card grid with division filter dropdown and "show unallocated only" toggle
- Each card shows: name, role type, seniority, resource type, country, total allocation % (colour-coded: green ≤ 100%, amber 90–100%, red > 100%)
- Add mapping: inline form with activity dropdown and allocation % input
- Edit/remove: pencil/trash icons inline per mapping
- Month selector (defaults to May 2026)

`AllocationBar.tsx`
- Visual progress bar showing allocation % for a resource
- Red fill when over 100%, amber 80–99%, green < 80%
- Tooltip showing breakdown: "CoreDataWarehouse — Application Support: 50% | AuthService Replacement: 60%"

`ChatPanel.tsx`
- Query input textarea with submit button
- Four starter question chips rendered below input on empty state
- SSE consumer: `EventSource` connected to `/api/chat` (using POST via fetch with streaming)
- Tokens appended to response display as they arrive
- `ToolCallIndicator.tsx` rendered while tools are being called: "Checking capacity by division…"

**Tests:**
- `UploadPanel.test.tsx` — renders drop zone; shows preview after upload response; confirm button calls commit endpoint
- `SuggestionDrawer.test.tsx` — renders suggestions passed as props correctly; accept button calls PUT mapping; "accept all" accepts only high-confidence items
- `MappingView.test.tsx` — division filter changes visible cards; unallocated filter hides fully-allocated people
- `ChatPanel.test.tsx` — chips call submit with correct query string; response area renders streamed text

Run `npx vitest run` after completing the frontend phase — all tests green.

**Output:** Functioning React app served by ASP.NET Core; all panels working; SSE streaming renders incrementally

---

### Phase 7 — Docker and Azure deployment
**Goal:** Single container running the .NET API, with the React app pre-built into `wwwroot`. Deployed to Azure Container Apps.

**Dockerfile (multi-stage):**
- Stage 1 (`node:22-slim AS frontend-build`): copy `src/WorkforceMapper.Api/ClientApp/`, run `npm ci` + `npm run build`
- Stage 2 (`mcr.microsoft.com/dotnet/sdk:8.0 AS build`): copy solution, `dotnet restore`, `dotnet publish -c Release`
- Stage 3 (`mcr.microsoft.com/dotnet/aspnet:8.0`): copy published output and built frontend from Stage 1, expose port 8080, set entrypoint

**docker-compose.yml:**
- Mounts `.env` for `ANTHROPIC_API_KEY`
- Exposes port 8080
- Mounts named volume `sqlite_data` to `/app/data` for SQLite persistence between restarts
- Mounts `./data` (taxonomy.json, sample-data/) to `/app/data` read-only for seeding

**`.env.example`:**
```
ANTHROPIC_API_KEY=sk-ant-...
# Optional Azure AD (only needed for deployed environment)
# AZURE_AD_TENANT_ID=...
# AZURE_AD_CLIENT_ID=...
```

**Azure deployment scripts (PowerShell, idempotent):**
- `scripts/deploy.ps1` — creates resource group, ACR (Basic SKU), builds image via `az acr build`, creates or updates Container App. Re-running updates the running app with a fresh image.
- `scripts/setup_auth.ps1` — enables Easy Auth: creates (or reuses) Entra app registration, wires to Container App. Idempotent.

Default app name: `workforce-mapper` in resource group `rg-workforce-mapper` (UK South). Override with `$env:APP_NAME`, `$env:RESOURCE_GROUP`, `$env:LOCATION`.

**Note on Node 22 in ACR build:** Same constraint as `product-portfolio-rag` — MCR mirror only carries Node up to 20. Use `node:22-slim` from Docker Hub, which works without rate-limit issues at personal deploy cadence.

Verify full local run: `docker compose up` → upload sample-headcount.xlsx → confirm → accept suggestions → ask "Which divisions have unallocated monthly cost this month?" in chat panel → verify correct divisions and monthly cost figures in response.

**Output:** Working Docker container; deployed Azure app behind Microsoft login

---

### Phase 8 — Documentation
**Goal:** README and CLAUDE.md that make the repo immediately credible to a technical reviewer.

**README.md sections:**
1. One-line description + what the system does (no assumed knowledge)
2. Why this use case — brief explanation of the workforce capacity management problem; link to `product-portfolio-rag` as the complementary project
3. **The centrepiece demo scenario:** the AuthService Replacement shortfall. Show the query *"How much are we spending on the AuthService Replacement project per month, and is that team sufficient?"* and the answer calling out ~$28k/month allocated vs ~$42k/month needed, the vendor EOL deadline, Platform Engineering's 112% over-allocation, and the company-wide product exposure — all 7 products transitively depend on AuthService, including DataLicensing ($6.2m ARR — Pragmenta Insights' highest-revenue product), with $28.9m of total ARR exposed. This is the first thing a technical reviewer should see — it demonstrates AI reasoning over live operational data with real cost figures, and it directly connects to the AuthService Critical risk story in `product-portfolio-rag`.
4. Architecture diagram (Mermaid): Excel upload → ASP.NET Core → SQLite → Claude (tool-use) → SSE stream
5. AI layer explanation — mapping suggestions (structured output) and NL query (tool-use loop). Reference `docs/decisions/` for the ADRs.
6. Example queries + answers (code blocks) — include at least 3: the AuthService shortfall query, the division bench capacity query, and the run/change split query
7. Local development setup (`docker compose up`)
8. Azure deployment instructions (two-script pattern)

**CLAUDE.md:** Agent context file covering solution structure, test commands (`dotnet test`, `npx vitest run`), conventions (EF Core for all data access, InMemory provider for tests), and the intentional data scenarios.

**Output:** README.md, CLAUDE.md; repo looks actively maintained

---

## Success Criteria

- `docker compose up` → app running locally at `http://localhost:8080`
- Upload `data/sample-data/sample-headcount.xlsx` → 80 resources loaded (employees, contractors, vendors), suggestions generated
- Mapping UI shows Platform Engineering over-allocated (red), Data Collection with bench (green below 100%)
- Chat query "Which divisions have unallocated monthly cost this month?" returns Data Collection (~$18k/month idle) — Client Services is close to fully allocated
- Chat query "How much are we spending on the AuthService Replacement project per month?" returns ~$28k/month and surfaces the shortfall vs ~$42k/month needed, plus the DataLicensing ($6.2m ARR) exposure
- Chat query "What is the monthly workforce cost of the DataLicensing product?" returns correct product attribution through activity → application → product chain
- `dotnet test` — all backend tests pass
- `npx vitest run` — all frontend tests pass
- Deployed to Azure Container Apps with Microsoft login via Easy Auth

---

## Notes for building agents

- Build phases sequentially — each phase has clear inputs and outputs
- Write tests alongside each implementation phase. Run `dotnet test` after each backend phase; run `npx vitest run` after the frontend phase. Do not proceed to the next phase until the current phase's tests pass.
- Use Entity Framework Core for all database access. Register `WorkforceDbContext` as a scoped service. Call `context.Database.Migrate()` at startup — do not use raw SQL for schema creation. No separate DatabaseService class is needed.
- Use the Anthropic C# SDK (`Anthropic.SDK`) for all AI calls. Do not use any other AI SDK or framework.
- The default month for all data and queries is `"2026-05"` — hardcode this as a constant and use it throughout
- The sample-headcount.xlsx must be generated programmatically (using ClosedXML or `generate_sample_data.py`) — do not use a placeholder. The demo depends on uploading a real file with all 21 columns including monthly rates and month flags.
- Suggestion generation is synchronous: `POST /api/upload/commit` awaits `SuggestionService.GenerateSuggestionsAsync()` and returns suggestions in the response body. No polling endpoint, no background task, no in-memory suggestions store.
- The SSE streaming in ASP.NET Core uses `Response.Body` directly; ensure `AllowSynchronousIO` is not needed by using async write patterns throughout
- Keep the frontend dependencies minimal: React 19, TypeScript, Vite, Tailwind CSS v4, Vitest, React Testing Library. No component library.
