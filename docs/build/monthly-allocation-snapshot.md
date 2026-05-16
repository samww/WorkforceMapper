# MonthlyAllocationSnapshot — Reference

> Shared reference for the snapshot-extraction issues. Domain glossary in `CONTEXT.md`. Decision record in ADR 9.

## Why

`DataService` currently re-derives the same per-month state inside seven tool methods: filter active resources via `GetMonthFlag(r, month) == 1`, sum mapping percent per resource, multiply `monthly_rate × percent / 100` for cost, sum Support `percent / 100` per application for capacity coverage. Each tool re-implements this arithmetic; new tools would too. Consumers cannot rely on a canonical primitive — every method is its own source of truth.

The snapshot concentrates the per-month-state arithmetic in one module so every tool consumes the same primitives. The deletion test passes: removing the snapshot scatters the same arithmetic back across seven methods.

## What it owns

- The **active-flag rule** — a resource contributes only when its month flag = 1. Bake-in means callers cannot forget.
- The **cost rule** — `monthly_rate × percent / 100`, exposed per-mapping (`ActiveMapping.AllocatedCost`) and per-resource (`ActiveResource.AllocatedCost`, `IdleCost`).
- **Capacity coverage** — `SupportCoverageOf(applicationId)` returns the sum of Support `percent / 100` across active resources, for a single application.
- **Per-resource allocation percent** — `ActiveResource.AllocationPercent` = sum of mapping percent for the month.
- **Per-resource idle FTE** — `ActiveResource.IdleFte` = `(100 - AllocationPercent) / 100.0`.

## What it does not own

- **Risk classification** — `RiskCalculator.ClassifyApplication(coverage, min, recommended)` stays a pure function; thresholds are application policy, not month-state.
- **Dependency closures** — `DependencyGraph.TransitiveDependenciesOf` / `TransitiveDependentsOf` are graph operations over `application_dependencies`, independent of month.
- **Max-wins risk aggregation** — `RiskCalculator.AggregateProductRisk` is already factored.
- **Donor-tier orchestration** — the 8-tier algorithm consumes the snapshot but lives in its own method (and may move to its own module per the follow-on candidate).
- **Cost attribution graph** — product/project navigation in `GetCostByProductAsync` consumes `ActiveMapping.AllocatedCost` but the graph traversal itself is separate.

## Interface

```csharp
namespace WorkforceMapper.Server.Services;

public sealed record ActiveResource(
    Resource Resource,           // includes Division, MonthlyRate, ResourceType, Country
    int      AllocationPercent,  // sum of mapping.Percent for the month
    decimal  AllocatedCost,      // sum of per-mapping AllocatedCost
    decimal  IdleCost,           // MonthlyRate * (100 - AllocationPercent) / 100
    double   IdleFte);           // (100 - AllocationPercent) / 100.0

public sealed record ActiveMapping(
    Resource Resource,
    Activity Activity,           // eagerly loaded; carries ApplicationId, ProjectId, ActivityType
    int      Percent,
    decimal  AllocatedCost);     // Resource.MonthlyRate * Percent / 100

public sealed class MonthlyAllocationSnapshot
{
    public string Month { get; }

    public IReadOnlyList<ActiveResource> ActiveResources { get; }
    public IReadOnlyList<ActiveMapping>  ActiveMappings  { get; }

    public ActiveResource?              Resource(int resourceId);
    public IReadOnlyList<ActiveMapping> MappingsOf(int resourceId);
    public IReadOnlyList<ActiveMapping> MappingsOn(int activityId);

    public double SupportCoverageOf(int applicationId);

    // Test seam — no DbContext, no DI.
    public MonthlyAllocationSnapshot(
        string month,
        IReadOnlyList<Resource> activeResources,
        IReadOnlyList<Mapping>  activeMappings);

    // Production loader.
    public static Task<MonthlyAllocationSnapshot> LoadAsync(
        WorkforceDbContext context, string month);
}
```

`Resource` and `Activity` references are the EF entities, eagerly loaded. Existing call sites that read `m.Resource.Division`, `m.Activity.ActivityType`, `m.Activity.ApplicationId` continue to work — they swap `m.` for `am.` and the rest is identical.

## Construction

`LoadAsync(context, month)` is the production entry point. It runs one read pass:

1. `context.Resources.ToListAsync()` — filter to active via `r.IsActiveIn(month)`.
2. `context.Mappings.Where(m => m.Month == month).Include(m => m.Activity).Include(m => m.Resource).ToListAsync()` — filter to active resources.
3. Construct via the public ctor.

The public ctor takes pre-loaded lists. Tests build a snapshot directly with hand-built `Resource` and `Mapping` objects — no `DbContext`, no DI, no seeded-ID knowledge.

## Lifecycle

Per call: each tool method calls `await MonthlyAllocationSnapshot.LoadAsync(context, month)` at the top, then projects. No DI provider, no scoped caching.

If chat-turn duplication (same month across multiple tool calls in one SSE response) ever shows up in a profile, layer a scoped `ISnapshotProvider` over `LoadAsync`. It's a 30-line addition with no architectural rework needed; do not add it speculatively.

## Migration order

Work proceeds in five slices. Slice 1 validates the interface; slices 2–5 are mechanical conversions.

| Slice | Tool method | What it validates | Status |
|---|---|---|---|
| 1 | `GetCapacityByDivisionAsync` | The full `ActiveResource` view (`AllocationPercent`, `AllocatedCost`, `IdleCost`, `IdleFte`), the projection pattern, snapshot construction. | **Done (#37)** |
| 2 | `GetActivityAllocationsAsync`, `GetUnallocatedResourcesAsync`, `GetOverallocatedResourcesAsync` | `ActiveMappings` per-mapping access; reuse of the projection pattern across three small methods. | **Done (#38)** |
| 3 | `GetApplicationsAsync` | First real exercise of `SupportCoverageOf`. Bigger because it also touches the dependency graph (which stays in DataService for now). If the snapshot interface has gaps, this slice surfaces them. | **Done (#39)** |
| 4 | `GetCostByProductAsync` | Per-mapping `AllocatedCost` across product/project graph navigation. | **Done (#40)** |
| 5 | `GetDonorCandidatesAsync` | The 8-tier orchestration consumes the snapshot. Largest method, most moving parts; left for last so the snapshot is battle-tested when this lands. | **Done (#41)** |

Each slice is a single PR. Acceptance criteria forbid regressions in existing tests and require new snapshot-level tests where the slice introduces a new primitive.

## Resource.IsActiveIn(month)

`Resource.IsActiveIn(month)` (`Resource.cs:29-44`, returns `bool`) is the single source of truth for the active-flag rule. `DataService.GetMonthFlag` was a duplicate and has been deleted (removed in slice 5, #41).

## Test strategy

**Snapshot tests** — direct unit tests via the public ctor. Build 3-5 hand-written `Resource` and `Mapping` objects, construct a snapshot, assert primitives. Zero DbContext, zero seeded-ID knowledge. Slice 1 introduces this test file (`MonthlyAllocationSnapshotTests.cs`).

**Tool-method tests** — existing `DataServiceTests` continue to use the in-memory DbContext and continue to pass unchanged. After all five slices, the `DataServiceTests` cases that exercise the same primitive twice (e.g. capacity arithmetic + activity-allocation arithmetic) can collapse onto the snapshot tests, but that cleanup is not required for any slice to land.

**Integration tests** — `ApplicationsApiTests`, `ProductsApiTests`, `ResourceActivityApiTests`, `MappingApiTests` continue to use `WebApplicationFactory` end-to-end and continue to pass unchanged.

## Out of scope

- **Group D split.** Done in #42. `DataService` now delegates its four Group D methods to `ProductCostAttribution`, `ApplicationRiskReporter`, `ProductRiskReporter`, and `DonorCandidateAlgorithm`. The duplicated transitive-closure walk is unified in `ProductRiskReporter.BuildProductClosure` (internal static), called by both `ProductRiskReporter` and `ApplicationRiskReporter`.
- **Move product-risk closure into `RiskCalculator`.** Resolved as part of #42 — the walk lives in `ProductRiskReporter.BuildProductClosure` rather than `RiskCalculator`, which is consistent with ADR 9 (closure is graph state, not risk policy).
- **Per-request scoped `ISnapshotProvider`.** Premature without measurement. Add only if a profile shows duplicate loads as a hot spot.
- **Replacing the `WebApplicationFactory` integration tests.** The seam at the HTTP layer remains; the snapshot is below it.
