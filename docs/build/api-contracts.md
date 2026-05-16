# API Surface + NL Query Tools

> **Authoritative API surface is in `CONTEXT.md`.** This file is a historical spec (target state after #10); subsequent issues (#11–#44) extended it. Treat CONTEXT.md as the source of truth for the API surface. The sections below are accurate where noted but may be incomplete for later additions.

## API Surface

| Method | Endpoint | Notes |
|---|---|---|
| GET | `/api/health` | `{ status, resource_count, mapping_count }` |
| GET | `/api/resources?month=` | Array with nested `mappings`; `active` reflects month flag |
| GET | `/api/activities` | Full list |
| GET | `/api/applications?month=` | `ApplicationRiskDto[]` — added in #11 |
| GET | `/api/products?month=` | `ProductRiskDto[]` — added in #12 |
| PUT | `/api/mappings` | Body: `{ resource_id: int, activity_id: int, month: string, percent: float }` → 200 or 400 `{ error: "would_exceed_cap" \| "resource_inactive_in_month" }` (#13) |
| DELETE | `/api/mappings?resource_id=&activity_id=&month=` | 204 / 404 / 400 `{ error: "resource_inactive_in_month" }` (#13) |
| POST | `/api/chat` | Body: `{ query: string, month: string }` → SSE stream |

## NL Query Tools

| Tool | Key outputs | Status |
|---|---|---|
| `get_capacity_by_division(division?, month?)` | `resource_count`, `active_resource_count`, `resource_type_split`, `monthly_cost`, `allocated_monthly_cost`, `unallocated_monthly_cost`, `allocation_pct` | Unchanged |
| `get_activity_allocations(activity_id?, division?, activity_type?, month?)` | `activity_id`, `activity_name`, `activity_type`, `name`, `company`, `resource_type`, `percent`, `implied_monthly_cost` | Unchanged |
| `get_unallocated_resources(division?, threshold_pct?, month?)` | `name`, `company`, `resource_type`, `division`, `total_allocation_pct`, `idle_monthly_cost` | Unchanged |
| `get_cost_by_product(month?)` | `product_name`, `arr_usd`, `total_monthly_cost`; plus explicit `"Unattributed"` line | Unchanged |
| `get_application_risk(application_id?, month?)` | `id`, `name`, `division`, `min_support_fte`, `recommended_support_fte`, `current_coverage_fte`, `risk` | Added in #11/#14 |
| `get_product_risk(product_id?, month?)` | `id`, `name`, `arr`, `risk`, `contributing_apps[]` | Added in #12/#14 |
| `get_donor_candidates(application_id?, application_name?, month?)` | `application_id`, `application_name`, `gap_fte`, `candidates[]`, `fewest_changes_combination` | Added in #29 |
| ~~`get_overallocated_resources`~~ | — | Removed in #14 (unreachable under hard cap) |

## Response shapes (new in #10)

### `ApplicationRiskDto`

```json
{
  "id": 1,
  "name": "AuthService",
  "division": "Platform Engineering",
  "min_support_fte": 4.0,
  "recommended_support_fte": 5.0,
  "current_coverage_fte": 3.0,
  "risk": "high",
  "dependent_products": [
    { "id": 1, "name": "DataLicensing", "arr_usd": 6200000, "risk": "high" }
  ]
}
```

`risk` values: `"high"` | `"medium"` | `"low"`

`dependent_products` — every product whose transitive dependency closure includes this application (both direct and indirect links). Foundational apps such as AuthService appear in all dependent products' lists because all products transitively depend on them. Each entry includes a `risk` field (product max-wins risk). `total_dependent_arr_usd` (pre-computed sum) and `dependent_app_count` are also on the DTO (added in later issues).

### `ProductRiskDto`

```json
{
  "id": 1,
  "name": "CoreInsights",
  "arr": 8500000,
  "risk": "high",
  "contributing_apps": [
    { "id": 1, "name": "AuthService", "risk": "high" }
  ],
  "direct_apps": [
    {
      "id": 5, "name": "InsightHub", "risk": "low",
      "dependencies": [
        { "id": 1, "name": "CoreDataWarehouse", "risk": "low" }
      ]
    }
  ]
}
```

`contributing_apps` — flat list of all apps in the transitive closure, capped at 10 entries, ordered by risk desc then name asc.

`direct_apps` — tree-structured list for UI rendering: each entry is a direct app (from `product_applications`) with its 1-hop `dependencies` (from `application_dependencies`). Depth capped at 2 levels below the product root. Both lists ordered by risk desc then name asc.

### Mapping rejection body

```json
{ "error": "would_exceed_cap" }
{ "error": "resource_inactive_in_month" }
```
