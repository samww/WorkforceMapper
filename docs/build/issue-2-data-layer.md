# Issue #2 — DB Schema + Seed Data

> Domain context, glossary, data model, API contracts, and tool signatures are in `CONTEXT.md`.
> Synthetic data details (rate tables, per-division breakdowns) are in `docs/build/synthetic-data-spec.md`.

## Phase 1 — Seed data verification

Confirm `data/` files match the intentional scenarios before writing any code:
- `data/taxonomy.json` — 18 applications, 7 products, 12 projects, 33 activities with correct `activity_type`, `application_id`/`project_id`, and `division`
- `data/resources.json` — 80 resources (PE 23, Analytics 24, Data Collection 20, Client Services 13); IDs PI00001–PI00080; plausible monthly rates and month flags; 2 Analytics Associates with Jan–Feb = 0
- `data/mappings.json` — every active resource in 2026-05 sums to exactly 100%; produces Platform Engineering ~112%, Data Collection ~86% (~$18k/month idle), AuthService Replacement ~$28k/month allocated

**Output:** Confirmed seed data files — no code changes required if data is correct.

---

## Phase 2 — Project setup and data layer

**Goal:** Extend `WorkforceMapper.Server` with SQLite/EF Core, entity classes, DbContext, initial migration, JSON seeding at startup, DataService, and tests.

### Solution structure

```
WorkforceMapper.slnx
WorkforceMapper.Server/    ← existing; extend this
WorkforceMapper.Tests/     ← xUnit test project; add to solution
```

### NuGet packages

`WorkforceMapper.Server.csproj`:
- `Microsoft.EntityFrameworkCore.Sqlite`
- `Microsoft.EntityFrameworkCore.Design`
- `Anthropic.SDK`

`WorkforceMapper.Tests.csproj`:
- `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`
- `Microsoft.EntityFrameworkCore.InMemory`
- `Moq`
- `Microsoft.AspNetCore.Mvc.Testing`

### Entity classes (`WorkforceMapper.Server/Models/`)

- `Resource.cs` — properties matching resources table (including jan–dec int properties); `ICollection<Mapping> Mappings`
- `Application.cs` — id, name; `ICollection<ProductApplication>`, `ICollection<Activity>`
- `Product.cs` — id, name; `ICollection<ProductApplication>`, `ICollection<ProjectProduct>`
- `ProductApplication.cs` — join entity; composite key (ProductId, ApplicationId)
- `Project.cs` — id, name; `ICollection<ProjectProduct>`, `ICollection<Activity>`
- `ProjectProduct.cs` — join entity; composite key (ProjectId, ProductId)
- `Activity.cs` — activities schema; nullable `ApplicationId` and `ProjectId` FKs; `ICollection<Mapping>`
- `Mapping.cs` — composite key `(ResourceId, ActivityId, Month)`; nav properties to `Resource` and `Activity`

### DbContext (`WorkforceMapper.Server/Data/WorkforceDbContext.cs`)

- Inherits `DbContext`; `DbSet` for all eight entities
- `OnModelCreating`: composite PKs for `Mapping`, `ProductApplication`, `ProjectProduct`; nullable FKs on `Activity`; unique index on `Resource.ResourceId`
- Taxonomy seeding via `HasData()`: embed all taxonomy entities from `data/taxonomy.json` directly in model configuration — applied through EF Core migrations at startup

### Migration

```bash
dotnet ef migrations add InitialCreate --project WorkforceMapper.Server
dotnet ef database update --project WorkforceMapper.Server
```

### Startup seeding (`Program.cs`)

Register DbContext:
```csharp
builder.Services.AddDbContext<WorkforceDbContext>(o => o.UseSqlite("Data Source=workforce.db"));
```

After `context.Database.Migrate()`:
```csharp
// Taxonomy seeded via HasData() migrations above

if (!context.Resources.Any())
{
    var json = await File.ReadAllTextAsync("data/resources.json");
    var records = JsonSerializer.Deserialize<List<ResourceSeed>>(json,
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    context.Resources.AddRange(records!.Select(r => new Resource { ... }));
    await context.SaveChangesAsync();
}

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

Define `ResourceSeed` and `MappingSeed` as record types in `WorkforceMapper.Server/Data/SeedTypes.cs`. `MappingSeed.ResourceId` is the text identifier `PI#####` — resolved to integer PK via lookup.

### DataService (`WorkforceMapper.Server/Services/DataService.cs`)

- `GetResourcesAsync(division?, month?)` — `.Include(r => r.Mappings).ThenInclude(m => m.Activity)`
- `GetActivitiesAsync(division?, activityType?)` — with application/project references, optionally filtered
- `GetCapacityByDivisionAsync(month)` — group by division; return per-division: `resource_count`, `resource_type_split`, `monthly_cost`, `allocated_monthly_cost`, `unallocated_monthly_cost`, `allocation_pct`
- `GetActivityAllocationsAsync(activityId?, division?, activityType?, month)` — resources mapped to activities with percent and implied monthly cost
- `GetUnallocatedResourcesAsync(thresholdPct, division?, month)` — resources below threshold with idle monthly cost
- `GetCostByProductAsync(month)` — sum costs through activity → application → product and activity → project → product chains

### Tests (`WorkforceMapper.Tests/DataServiceTests.cs`)

Using EF Core InMemory provider via shared test DbContext fixture:
- `GetResources_Returns80Resources_WhenSeeded`
- `GetActivities_ReturnsAllActivities_WhenSeeded`
- `GetMappings_ReturnsCorrectMappings_ForResource`
- `GetCapacityByDivision_ReturnsCorrectAllocationPct` — seed known data, verify 112% for over-allocated division

Run `dotnet test` — all tests green before proceeding.

### Notes

- All API endpoints use `[ApiController]` / `[Route]` Controller classes under `WorkforceMapper.Server/Controllers/` — do not inline route handlers in `Program.cs`
- No separate DatabaseService class
- Default month constant: `"2026-05"`

**Output:** DbContext, initial migration, DataService, JSON seeding at startup, passing xUnit tests
