# Architecture Decision Records

---

## ADR 1: SQLite + EF Core

**Decision:** SQLite (file-based, zero Azure cost, runnable with docker compose up) with EF Core (idiomatic .NET ORM, code-first migrations, InMemory provider for tests).

**Key constraints:**
- No concurrent writes — single-user demo; acceptable.
- WAL journal mode enabled at startup (`PRAGMA journal_mode=WAL` in `Program.cs` after `MigrateAsync`). Required because ASP.NET Core handles requests concurrently; without WAL, simultaneous reads and a write cause `SQLITE_BUSY` → 500. WAL is a persistent file setting, idempotent to set on every boot.
- Azure Container Apps is pinned to exactly 1 replica (`--min-replicas 1 --max-replicas 1` in `scripts/deploy.ps1`). SQLite cannot be shared across processes; multiple replicas each get an isolated copy of the database, causing split-brain state.
- SQLite file persisted via named Docker volume (local); ephemeral per container restart in Azure (reseeds from `data/*.json` on every boot — by design for a demo).
- Switching to Azure SQL = replace connection string + provider package only; entities, migrations, and LINQ queries are database-agnostic.

---

## ADR 3: Structured tool-use over NL-to-SQL

**Decision:** Eight typed tools (`get_capacity_by_division`, `get_activity_allocations`, `get_unallocated_resources`, `get_cost_by_product`, `get_application_risk`, `get_product_risk`, `get_donor_candidates`, `get_project_allocation`) implemented as EF Core queries in `DataService.cs`. Claude calls tools; never sees schema or executes SQL.

**Why not NL-to-SQL:**
- Security: tool-use exposes only eight read operations; NL-to-SQL exposes the full schema.
- Predictability: tool response shapes are typed and fixed; NL-to-SQL output varies per query.
- Testability: each tool is a C# method with a defined signature; tool call selection is assertable in unit tests.

**Key constraints:**
- Fixed surface area — questions outside the eight tools cannot be answered (data limitation, not a tool limitation).
- `get_cost_by_product` includes an explicit `"Unattributed"` line for cost not traceable to any product — this is intentional, not a data quality issue.
- `get_application_risk` and `get_product_risk` expose capacity coverage, risk classification, and dependent product ARR; they replace the former `get_overallocated_resources` tool (removed in issue #14).
- `get_donor_candidates` returns active resources whose capacity could shift onto an under-resourced application's Support activity, in eight-tier priority order (same-division tiers 1–4, cross-divisional tiers 5–8); added in issue #29.
- `get_project_allocation` returns FTE allocated to each project with pre-computed `total_project_allocation_fte` and `projects_ranked_by_allocation`; added in issue #47. Projects carry no risk classification (ADR 10).
- All eight tools accept optional `month` parameter defaulting to the selected month.

---

## ADR 4: ASP.NET Core / C# backend

**Decision:** ASP.NET Core (.NET 10), C#. Controllers under `WorkforceMapper.Server/Controllers/`; `AddControllers()` / `MapControllers()` in `Program.cs`. No minimal-API inline handlers.

**Key constraints:**
- All API endpoints must be `[ApiController]` classes — do not inline handlers in `Program.cs`.
- Use `Anthropic.SDK` for all AI calls — no other AI SDK.

---

## ADR 6: Hard 100% allocation cap, API-enforced

**Decision:** `PUT /api/mappings` and `DELETE /api/mappings` enforce a hard cap. Writes that push a resource above 100% in a given month are rejected with 400 + `would_exceed_cap`. Writes against a month in which the resource is inactive are rejected with 400 + `resource_inactive_in_month`.

**Why:** Over-allocation is a symptom rather than a tracked operational state once the tool diagnoses risk. Surfacing impossible states weakens trust in the diagnostic frame. Recording a resource at 112% in the tool implies the tool permits operational over-commitment; the risk model renders that framing obsolete.

**Alternatives considered and rejected:**
- SQLite cross-row CHECK constraints — would require triggers; the seed pipeline is the only other writer, so API enforcement is sufficient and keeps the constraint logic in one testable place.
- Soft warning UI — keeps the door open for symptom-tracking and contradicts the new premise that the tool answers "is this application safe?" rather than "how over-committed is this person?".

**Key constraints:**
- The cap rule and the inactive-month rule are owned by `AllocationValidator` (single source of truth).
- Seed data is held to the same invariant: every active resource's monthly total is `<= 100%`.

---

## ADR 7: Capacity-driven application risk model

**Decision:** Each application carries `min_support_fte` and `recommended_support_fte` thresholds. Coverage is the sum of `(mapping.percent / 100)` across active resources mapped to the application's Support activity for the month. Risk classification: `coverage < min` → high; `min <= coverage < recommended` → medium; `coverage >= recommended` → low. Product risk = max risk over the transitive closure of (direct apps + their `application_dependencies`). Aggregation rule is max-wins.

**Why:** Over-allocation as a primary insight is a symptom. The diagnostic question is whether each application has enough support to keep running safely. Thresholds + coverage make that question answerable and directly connect to the business impact (dependent product ARR) exposed in the same view.

**Alternatives considered and rejected:**
- Weighted or count-based product aggregation. Max-wins is unambiguous and matches business framing: if any critical application is at risk, the product is at risk.
- Cost pro-rating across the application dependency graph. Cost stays attributed to direct `product_applications` only; the Unattributed line absorbs foundational infrastructure spend. Mixing cost attribution with risk propagation would conflate two distinct questions.
- Skill modelling on resources. Out of scope; the demo narrative references skill match informally without encoding it as a data constraint.

**Key constraints:**
- Risk classification is owned by `RiskCalculator` (single source of truth).
- Dependency graph traversal is owned by `DependencyGraph`; BFS with visited-set termination handles cycles.
- Cost attribution is unchanged. The Unattributed line behaviour is preserved.

---

## ADR 8: Integer-only mapping percent

**Decision:** `mapping.Percent` is a positive integer in the range 1–100. The API rejects fractional values and zero. The frontend enforces integers at the input layer (`step="1"`, `parseInt`). Computed aggregates (`allocation_pct`, `current_coverage_fte`) remain fractional decimals derived from integer inputs.

**Why:** Fractional percents (e.g. 13.5%) imply a precision that does not exist in this tool. Workforce allocation is estimated, not measured; the appearance of decimal precision misleads users. Integer inputs simplify the UI, remove a class of floating-point edge cases from the cap validator, and make the seed data generator straightforward.

**Alternatives considered and rejected:**
- One-decimal precision (e.g. 13.5%) — the precision is cosmetic; the underlying data is still estimation. Adds complexity (float equality checks, generator rounding) for no meaningful gain.
- Rounding fractional inputs silently on write — hides the constraint from callers; a clear rejection with `would_exceed_cap` is more honest than silent mutation.

**Key constraints:**
- `AllocationValidator` is the single enforcement point: rejects `percent < 1`. `Mapping.Percent` is typed as `int`, so non-integer values are rejected at the model-binding layer before reaching the validator.
- The frontend enforces integers at the input layer: `type="text"` with `inputMode="numeric"`, `onChange` strips non-digit characters, `parseInt` on submit.
- Seed data must be regenerated from scratch using a largest-remainder integer distribution; do not patch existing fractional rows.
- The `data/mappings.json` invariant is unchanged: every active resource's monthly total is exactly 100%.

---

## ADR 9: MonthlyAllocationSnapshot is the canonical mapping-state primitive

**Decision:** A `MonthlyAllocationSnapshot` module owns per-month-state arithmetic for the seven tool methods. Each tool calls `await MonthlyAllocationSnapshot.LoadAsync(context, month)` at the top of its body, then projects from the snapshot's `ActiveResources`, `ActiveMappings`, and `SupportCoverageOf(applicationId)`. New tool methods consume the snapshot rather than re-querying.

The snapshot owns: the active-flag rule (resources contribute only when their month flag = 1), the cost rule (`monthly_rate × percent / 100`), per-resource allocation percent and idle FTE, per-mapping allocated cost, and capacity coverage per application. The snapshot does **not** own risk classification (`RiskCalculator`), dependency closures (`DependencyGraph`), max-wins risk aggregation (`RiskCalculator.AggregateProductRisk`), donor-tier orchestration, or product/project cost attribution.

**Why:** `DataService` re-derives the same arithmetic in seven tool methods today. The deletion test on the snapshot passes — removing it scatters the same arithmetic back across seven methods. A shared primitive prevents drift (e.g. the integer-division bug fixed in #36 was possible because the per-division and overall paths each had their own cast of `m.Percent`).

**Alternatives considered and rejected:**
- **Snapshot exposes raw mappings only; coverage lives in `RiskCalculator`.** Fails the locality argument that motivated the proposal — the same Support-filter sum gets re-implemented in donor target-gap and donor surplus-Support calculations. Capacity coverage is a load-bearing primitive needed by three independent consumers; one definition or zero.
- **Snapshot owns risk classification too.** Conflates per-month state (mappings + active flags) with per-application policy (`MinSupportFte`, `RecommendedSupportFte`). Risk thresholds change with ADR amendments; mapping state changes with seed regeneration. Different change vectors, different modules.
- **Per-request scoped `ISnapshotProvider` with caching.** Adds DI plumbing whose only value is amortising duplicate loads inside a single chat turn. With ~3,144 mapping rows on local SQLite, duplicate loads are sub-millisecond. The wrapper today fails its own deletion test. Add only if a profile shows hot-spot pain.
- **Bare entity references on the snapshot (`IReadOnlyList<Resource>`, `IReadOnlyList<Mapping>`).** Forces every consumer to recompute `AllocationPercent`, `AllocatedCost`, `IdleFte` from raw fields. The whole point was to stop scattering that arithmetic.
- **Bundle the Group D split with this extraction.** The split-DataService-into-domain-reporters question (`ProductCostAttribution`, `ApplicationRiskReporter`, `ProductRiskReporter`, `DonorCandidateAlgorithm`) was real but separable. Splitting before extraction meant guessing at boundaries; splitting after (done in #42) meant the boundaries were visible because each method had shrunk. `DataService` now delegates all four Group D methods to those named classes.

**Key constraints:**
- The snapshot is the single source of truth for `capacity coverage`, `allocation_pct`, idle FTE, and per-resource / per-mapping cost. New tool methods that re-implement these must instead consume the snapshot.
- `Resource.IsActiveIn(month)` is the single source of truth for the active-flag rule. `DataService.GetMonthFlag` was a duplicate and was removed in #41.
- The public constructor `MonthlyAllocationSnapshot(month, activeResources, activeMappings)` is the test seam — snapshot tests build snapshots without a `DbContext`.
- Migration proceeds in five slices: `GetCapacityByDivisionAsync` first (validation), then the small projection trio, then `GetApplicationsAsync` (validates `SupportCoverageOf`), then `GetCostByProductAsync`, then `GetDonorCandidatesAsync`. See `docs/build/monthly-allocation-snapshot.md`.
- The `WebApplicationFactory` integration tests remain the regression net. Existing `DataServiceTests` continue to pass; their consolidation onto snapshot tests is a cleanup, not a slice gate.

---

## ADR 10: Projects are not risk-classified

**Decision:** The Projects tab and the `get_project_allocation` tool report `project_allocation` (sum of `mapping.percent / 100` across active mappings to the project's activities, in FTE) and nothing else. Projects carry no `min_*_fte` / `recommended_*_fte` thresholds, no risk classification, and no badge. The AgentService system prompt explicitly forbids the LLM from describing a project as "under-resourced", "adequately staffed", or "at risk".

**Why:** Application Support models steady-state operations (SLO/uptime-driven, ongoing) — capacity below `min_support_fte` is operationally dangerous in a way that's knowable from headcount alone. Projects model discretionary delivery work (deadline-driven, finite-duration) — whether a project is "adequately staffed" depends on its scope, target delivery date, and remaining work, none of which are in the data model. Importing the Application risk framing into Projects would force the tool to either invent thresholds or fabricate adequacy judgments the data cannot support.

**Alternatives considered and rejected:**
- **Per-project `target_fte` threshold + risk classification.** Would require schedule and scope data the model does not carry. The thresholds would have to be guessed or hand-curated and would rot as projects re-plan; that drift is worse than absence.
- **Aggregate delivery-risk signal (e.g. "behind plan").** Same data gap. Without baseline and burndown, a derived signal would be cosmetic.
- **Show projects in the Applications tab with a "Project" badge instead of a separate tab.** Conflates two distinct mental models (steady-state vs. delivery) under one risk vocabulary, which is exactly the failure mode this ADR exists to prevent.

**Key constraints:**
- `MonthlyAllocationSnapshot.ProjectAllocationOf(projectId)` is the single source of truth for project allocation; parallels `SupportCoverageOf` but spans all activity types of the project.
- The Projects tab renders a fill-only allocation bar — no min/recommended markers, no risk badge, no risk-derived sort.
- `get_project_allocation` system-prompt guidance must forbid adequacy/risk language explicitly. Every other tool has thresholds to lean on; without that explicit forbid, the LLM will pattern-match to the Application playbook.
