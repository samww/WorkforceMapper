# API Surface + NL Query Tools

> Domain context, glossary, data model, and constraints are in `CONTEXT.md`.

## API Surface

| Method | Endpoint | Notes |
|---|---|---|
| GET | `/api/health` | `{ status, resource_count, mapping_count }` |
| GET | `/api/resources?month=` | Array with nested `mappings`; `active` reflects month flag |
| GET | `/api/activities` | Full list |
| PUT | `/api/mappings` | Body: `{ resource_id: int, activity_id: int, month: string, percent: float }` |
| DELETE | `/api/mappings?resource_id=&activity_id=&month=` | 204 / 404 |
| POST | `/api/chat` | Body: `{ query: string, month: string }` → SSE stream |

## NL Query Tools (all 5 are required)

| Tool | Key outputs |
|---|---|
| `get_capacity_by_division(division?, month?)` | `resource_count`, `active_resource_count`, `resource_type_split`, `monthly_cost`, `allocated_monthly_cost`, `unallocated_monthly_cost`, `allocation_pct` |
| `get_activity_allocations(activity_id?, division?, activity_type?, month?)` | `activity_id`, `activity_name`, `activity_type`, `name`, `company`, `resource_type`, `percent`, `implied_monthly_cost` |
| `get_unallocated_resources(division?, threshold_pct?, month?)` | `name`, `company`, `resource_type`, `division`, `total_allocation_pct`, `idle_monthly_cost` |
| `get_overallocated_resources(division?, threshold_pct?, month?)` | `name`, `company`, `resource_type`, `division`, `total_allocation_pct`, `overallocation_pct` |
| `get_cost_by_product(month?)` | `product_name`, `arr_usd`, `total_monthly_cost`; plus explicit `"Unattributed"` line |
