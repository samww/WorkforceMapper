# Workforce Mapper — Synthetic Data Specification

> Reference doc for the Pragmenta Insights fictional dataset. Read this only when verifying or regenerating seed files (`data/resources.json`, `data/taxonomy.json`, `data/mappings.json`). Not needed for API, UI, or agent implementation tasks.

**Fictional company. Any resemblance to real organisations is coincidental.**

---

## Intentional Demo Scenarios (2026-05)

| Scenario | Expected value |
|---|---|
| Platform Engineering allocation | ~112% (over-allocated) |
| Data Collection idle cost | ~$18k/month |
| AuthService Replacement allocated | ~$28k/month vs ~$42k/month needed |
| ARR transitively exposed via AuthService | $28.9m (all 7 products) |
| Unattributed monthly cost | ~$70k/month |

---
- `docs/decisions/`
  - `001-sqlite-vs-azure-sql.md` — why SQLite is sufficient for this project
  - `002-tool-use-vs-freeform-sql.md` — why structured tools over freeform SQL generation
- `docs/DEMO_SCRIPT.md` — 3 NL query demos with expected output

---

## Synthetic Data Specification

Both synthetic datasets use the same fictional company: **Pragmenta Insights** — the mid-size data analytics company from `product-portfolio-rag`. The workforce data here is the operational source behind the application cost figures in that project.

> **Pragmenta Insights is a fictional company. Any resemblance to real organisations is coincidental.**

### Resource schema (`data/resources.json`)

Each record in `data/resources.json` maps to one `resources` row:

| Field | Type | Notes |
|---|---|---|
| resource_id | Text | `PI#####` format — stable identifier |
| name | Text | Full name |
| resource_type | Text | `Employee`, `Contractor`, `Vendor` |
| company | Text | `Pragmenta Insights` for Employee/Contractor; supplier name for Vendor |
| seniority | Text | `Associate`, `Analyst`, `Senior Analyst`, `Manager`, `Senior Manager` |
| role_type | Text | Standardised role name, e.g. `Java Developer`, `Data Scientist` |
| division | Text | `Platform Engineering`, `Analytics`, `Data Collection`, `Client Services` |
| country | Text | `United Kingdom`, `Poland`, `India` |
| monthly_rate | Number | USD monthly cost — basis for all cost calculations |
| jan–dec | Integer ×12 | `1` if active (billing) that month, `0` if not |

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
