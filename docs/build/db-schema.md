# Workforce Mapper — Database Schema

SQLite database managed by Entity Framework Core (code-first). Schema created via `dotnet ef migrations add InitialCreate`; applied at startup via `context.Database.Migrate()`. Taxonomy tables (applications, products, projects, activities, and their joins) are seeded from `data/taxonomy.json` via EF Core `HasData()`.

---

## Entity Relationship Diagram

```mermaid
%%{init: {'theme': 'base', 'themeVariables': {'lineColor': '#888888', 'edgeLabelBackground': '#1e1e1e'}}}%%
erDiagram
    resources {
        INTEGER id PK
        TEXT resource_id UK "PI##### format"
        TEXT name
        TEXT resource_type "Employee | Contractor | Vendor"
        TEXT company "Pragmenta Insights or supplier name"
        TEXT seniority "Associate | Analyst | Senior Analyst | Manager | Senior Manager"
        TEXT role_type "e.g. Java Developer, Data Scientist"
        TEXT division "Platform Engineering | Analytics | Data Collection | Client Services"
        TEXT country "United Kingdom | Poland | India"
        REAL monthly_rate "USD; basis for all cost calculations"
        INTEGER jan
        INTEGER feb
        INTEGER mar
        INTEGER apr
        INTEGER may
        INTEGER jun
        INTEGER jul
        INTEGER aug
        INTEGER sep
        INTEGER oct
        INTEGER nov
        INTEGER dec
    }

    mappings {
        INTEGER resource_id PK,FK
        INTEGER activity_id PK,FK
        TEXT month PK "e.g. 2026-05"
        REAL percent "0-100; must sum to 100% per resource per month"
    }

    activities {
        INTEGER id PK
        TEXT name "e.g. CoreDataWarehouse - Application Support"
        TEXT activity_type "Application Support | Application Minor Changes | Project Changes"
        INTEGER application_id FK "null for project activities"
        INTEGER project_id FK "null for application activities"
        TEXT division
    }

    applications {
        INTEGER id PK
        TEXT name
    }

    projects {
        INTEGER id PK
        TEXT name
    }

    product_applications {
        INTEGER product_id PK,FK
        INTEGER application_id PK,FK
    }

    project_products {
        INTEGER project_id PK,FK
        INTEGER product_id PK,FK
    }

    products {
        INTEGER id PK
        TEXT name
        REAL arr_usd "annual recurring revenue in USD"
    }

    resources    ||--o{ mappings             : "has"
    activities   ||--o{ mappings             : "has"
    applications ||--o{ activities           : "has"
    projects     ||--o{ activities           : "has"
    applications ||--o{ product_applications : "in"
    projects     ||--o{ project_products     : "has"
    products     ||--o{ product_applications : "has"
    products     ||--o{ project_products     : "in"
```

---

## Cost Attribution Chain

```mermaid
%%{init: {'theme': 'base', 'themeVariables': {'lineColor': '#888888', 'edgeLabelBackground': '#1e1e1e'}}}%%
flowchart LR
    R["resources\nmonthly_rate × (percent/100)\n× active month flag\n(jan–dec = 1)"]
    M["mappings\npercent + month"]
    A["activities\nactivity_type"]
    AP["applications"]
    PR["projects"]
    PD["products\n(via product_applications)"]
    PDP["products\n(via project_products)"]

    R -->|"allocated cost"| M
    M --> A
    A -->|"application_id"| AP
    A -->|"project_id"| PR
    AP -->|"product_applications"| PD
    PR -->|"project_products"| PDP
```

**Cost model:** `allocated_monthly_cost = monthly_rate × (percent / 100)` **only when the resource's active flag for that month is `1`**. If the active flag is `0` the resource is not billing — all cost figures are `0` for that month regardless of any mappings. Summed across all mappings for a resource, `percent` values must total 100% per month for a fully-allocated resource. The `product_applications` and `project_products` joins enable attribution up to product level, surfaced by the `get_cost_by_product` NL query tool.

---

## Key Constraints

| Table | Constraint |
|---|---|
| `resources` | `resource_id` UNIQUE — stable identifier preserved across re-uploads |
| `mappings` | Composite PK `(resource_id, activity_id, month)` — one mapping per resource/activity/month |
| `mappings.percent` | Advisory 100% rule — no database CHECK constraint; enforced by UI (red over-allocation indicator, unallocated filter). API accepts any value. |
| `product_applications` | Composite PK `(product_id, application_id)` |
| `project_products` | Composite PK `(project_id, product_id)` |
| `activities.application_id` | Nullable — null for project activities |
| `activities.project_id` | Nullable — null for application activities |

---

## Reference Counts (seeded from `taxonomy.json`)

| Table | Count | Notes |
|---|---|---|
| applications | 18 | Across 4 divisions |
| products | 7 | Subset of 14-product portfolio; $28.9m ARR |
| product_applications | 26 | Each product has 3–5 dependent applications |
| projects | 12 | One per project change activity |
| project_products | 26 | Several projects benefit multiple products |
| activities | 33 | 10 PE / 9 Analytics / 8 Data Collection / 6 Client Services |
| resources | 80 | Loaded from `sample-headcount.xlsx` |
| mappings | ~160 | Created via UI or accepted from AI suggestions |
