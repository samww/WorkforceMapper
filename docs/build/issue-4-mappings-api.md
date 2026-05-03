# Issue #4 — Mappings API (CRUD)

> Domain context, glossary, data model, API contracts, and tool signatures are in `CONTEXT.md`.
> DB schema and EF Core entity details are in `docs/build/db-schema.md`.
> DataService is implemented in Phase 2 (`docs/build/issue-2-data-layer.md`).

## Phase 3b — Mapping CRUD endpoints

**Goal:** PUT and DELETE endpoints for mappings; advisory allocation constraint; integration tests.

### Tests (`WorkforceMapper.Tests/MappingApiTests.cs`)

Using `WebApplicationFactory<Program>` with test SQLite database:
- `PutMapping_Returns200_WithValidAllocation`
- `PutMapping_Returns200_WhenAllocationExceeds100Pct` — over-allocation is advisory; API accepts any percent value (ADR 5)
- `PutMapping_Returns200_WhenUpdatingExistingMapping`
- `DeleteMapping_Returns204_WhenMappingExists`
- `DeleteMapping_Returns404_WhenMappingNotFound`
- `GetResources_ReturnsMappings_WhenMappingsExist` — verify mappings embedded in resource response

### Controller (`WorkforceMapper.Server/Controllers/MappingController.cs`)

```
PUT /api/mappings
Body: { resource_id: int, activity_id: int, month: string, percent: float }
No allocation cap — advisory only per ADR 5; accepts any percent value
Returns 200

DELETE /api/mappings?resource_id=&activity_id=&month=
Returns 204 on success, 404 if not found
```

### DataService additions

Add to `DataService.cs`:
- `UpsertMappingAsync` — `context.Mappings.FindAsync(...)` → update or add, then `SaveChangesAsync`
- `DeleteMappingAsync` — find by composite key, remove, `SaveChangesAsync`; return false if not found

Run `dotnet test` — all tests green before proceeding.

**Output:** Mapping PUT/DELETE endpoints, passing tests
