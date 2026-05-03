# Issue #3 — Health + Resources + Activities API

> Domain context, glossary, data model, API contracts, and tool signatures are in `CONTEXT.md`.
> DB schema and EF Core entity details are in `docs/build/db-schema.md`.
> DataService is implemented in Phase 2 (`docs/build/issue-2-data-layer.md`).

## Phase 3a — Read-only API endpoints

**Goal:** Health check plus read endpoints for resources and activities, with integration tests.

### Tests (`WorkforceMapper.Tests/ResourceActivityApiTests.cs`)

Using `WebApplicationFactory<Program>` with test SQLite database:
- `GetHealth_Returns200`
- `GetResources_Returns200_WithSeededData`
- `GetResources_FiltersByDivision`
- `GetActivities_Returns200_WithSeededData`
- `GetActivities_FiltersByActivityType`

### Controllers (`WorkforceMapper.Server/Controllers/`)

`HealthController.cs`:
```
GET /api/health → 200 OK
```

`ResourcesController.cs`:
```
GET /api/resources?division=&month=
Returns resources with embedded mappings (activity id, activity name, percent, month)
```

`ActivitiesController.cs`:
```
GET /api/activities?division=&activity_type=
Returns activities with application/project name
```

All controllers use `DataService` for queries. Default month: `"2026-05"`.

Run `dotnet test` — all tests green before proceeding.

**Output:** Health, Resources, and Activities endpoints with passing tests
