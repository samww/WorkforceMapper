# Issue #11 — Application risk model + UI (foundation slice)

> Domain glossary, data model, API surface in `CONTEXT.md`.
> Risk classification rule and capacity coverage formula in `docs/build/risk-model.md`.
> Existing schema and EF Core patterns in `docs/build/db-schema.md`.

Foundation slice for the issue #10 risk-driven support model. Establishes schema, seed data, the `RiskCalculator` deep module, the applications API, and two new React views. End state: AuthService visibly red on the Applications list, three apps medium, all others low.

## Phase A — Schema migration

### New columns on `applications`

- `min_support_fte REAL NOT NULL`
- `recommended_support_fte REAL NOT NULL`

### New table `application_dependencies`

| Column | Type | Notes |
|---|---|---|
| `application_id` | INT | FK to applications.id |
| `depends_on_application_id` | INT | FK to applications.id |

Composite PK `(application_id, depends_on_application_id)`. No nav properties needed beyond what EF Core generates from the keys; keep it as a plain join entity.

### EF Core wiring

- New `ApplicationDependency` entity in `WorkforceMapper.Server/Models/`.
- `WorkforceDbContext.OnModelCreating`: composite PK on `ApplicationDependency`; FKs to `Application` on both sides; `DeleteBehavior.Restrict` to keep the graph immutable.
- HasData seeding for both columns and the join table from the new JSON files (see Phase B).
- Migration: `dotnet ef migrations add AddApplicationRiskModel --project WorkforceMapper.Server`.

## Phase B — Seed data

### `data/applications.json` (new)

One row per application:

```
{ "id": 1, "name": "AuthService", "min_support_fte": 4.0, "recommended_support_fte": 5.0 }
```

Calibration target for `2026-05`:

| App | Min FTE | Rec FTE | Seed coverage | Risk |
|---|---|---|---|---|
| AuthService | 4.0 | 5.0 | 3.0 | high |
| SurveyEngine | 2.0 | 3.0 | 2.5 | medium |
| PanelIQ | 2.0 | 3.0 | 2.5 | medium |
| DataMapper | 1.5 | 2.5 | 2.0 | medium |
| Other 14 apps | 1.0–3.0 | 1.5–4.5 | at-or-above-rec | low |

`CallCentre Suite` is intentionally low capacity-risk despite its intrinsic risk rating (winding down). See PRD "Further Notes".

### `data/application_dependencies.json` (new)

Lifted from the portfolio-RAG application dependency arrays, restricted to the 18 in-scope applications. One row per directed edge:

```
{ "application_id": <dependent>, "depends_on_application_id": <dependency> }
```

All 7 products must transitively depend on AuthService (already true via the existing `product_applications` plus the new edges).

### `data/mappings.json` (rewrite)

Constraints for the rewrite:
- Every active resource's monthly total in `2026-05` is `<= 100%` (Platform Engineering off 112%).
- Support coverage produces the calibration table above for `2026-05`.
- Two senior Platform Engineering resources sit at 100% on `P05 ML Forecasting` (the demo donor pool).
- Other months unchanged unless they violate the cap.

Total Support FTE in seed should land near 40–45 FTE out of ~80 available.

### Seed loader

Extend the existing JSON seeding pattern in `Program.cs`:
- Load `data/applications.json` once on startup; merge `min_support_fte` / `recommended_support_fte` into existing `applications` rows by `id` if HasData seeding is not used for the columns.
- Load `data/application_dependencies.json` if `application_dependencies` is empty.

## Phase C — RiskCalculator (pure module)

`WorkforceMapper.Server/Services/RiskCalculator.cs`. Stateless; no DI dependencies; no I/O.

Public methods (illustrative shape):

```csharp
public enum RiskLevel { Low, Medium, High }

public static class RiskCalculator
{
    public static RiskLevel ClassifyApplication(double coverage, double min, double recommended);
    public static RiskLevel AggregateProductRisk(IEnumerable<RiskLevel> appRisks);
}
```

Boundary rule: exactly at min -> medium; exactly at recommended -> low.

Validation guards: throw `ArgumentException` if `min < 0`, `recommended < min`, or `coverage < 0`.

### Unit tests (`WorkforceMapper.Tests/RiskCalculatorTests.cs`)

- coverage = 0 -> high
- coverage just below min -> high
- coverage exactly at min -> medium
- coverage between min and recommended -> medium
- coverage exactly at recommended -> low
- coverage above recommended -> low
- argument validation (negative, recommended < min)
- product aggregation: empty -> low; mixed (high, low, medium) -> high; all medium -> medium; all low -> low

## Phase D — DataService.GetApplications

```csharp
Task<List<ApplicationRiskDto>> GetApplicationsAsync(string month);
```

Returns one row per application:

```
{
  id, name, division,
  min_support_fte, recommended_support_fte,
  current_coverage_fte, risk
}
```

Coverage computed via the formula in `docs/build/risk-model.md`. Risk computed via `RiskCalculator.ClassifyApplication`. Single round-trip query: pull active mappings for the month with their resource active flag and their activity's `application_id` and `activity_type`, group, sum, then classify in memory.

## Phase E — `GET /api/applications`

`WorkforceMapper.Server/Controllers/ApplicationsController.cs`:

```
GET /api/applications?month=2026-05
Returns 200 with ApplicationRiskDto[]
Default month: "2026-05"
```

Update `docs/build/api-contracts.md` API Surface table with the new row.

### Integration tests (`WorkforceMapper.Tests/ApplicationsApiTests.cs`)

- `GetApplications_Returns200_WithSeededData`
- `GetApplications_AuthService_Is_HighRisk_For_2026_05`
- `GetApplications_ThreeAppsAreMediumRisk_For_2026_05`
- `GetApplications_AllOthersAreLowRisk_For_2026_05`
- `GetApplications_CoverageMatchesSumOfActiveSupportMappings`

## Phase F — Frontend views

Add a top-level Applications tab to existing navigation.

### `ApplicationsView.tsx`

- Fetches `GET /api/applications?month=<selected>` on mount and on month change.
- Renders a sortable list (default sort: risk desc, then coverage gap desc).
- Per row: name, division, risk badge (red/amber/green), coverage `X.X / Y.Y FTE`.
- Click row -> navigate to `ApplicationDetail` for that app.

### `ApplicationDetail.tsx`

- Coverage bar: current FTE relative to min and recommended; markers at min and recommended thresholds.
- Dependent products list: name + ARR. **No risk badges in this slice** — those land in issue #12.
- Back link to `ApplicationsView`.

### Component tests

Render-smoke only for `ApplicationsView` and `ApplicationDetail`. The list/detail views are display-heavy; no logic worth a deep test.

## Phase G — Inline doc updates

- Add `GET /api/applications?month=` row to `docs/build/api-contracts.md`.
- Add `min_support_fte`, `recommended_support_fte`, and `application_dependencies` to `docs/build/db-schema.md` reference counts and any relevant constraint table.
- Do not touch `CONTEXT.md` or ADRs — those are the doc-updates slice (#15).

## Out of scope

- Product risk badges anywhere (issue #12).
- Hard cap enforcement on `PUT /api/mappings` (issue #13).
- Chat tool changes (issue #14).
- ADR / `CONTEXT.md` rewrites (issue #15).

**Output:** Schema + seed data + RiskCalculator + GetApplications + ApplicationsView + ApplicationDetail. AuthService visibly red end-to-end.
