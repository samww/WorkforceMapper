# Architecture Decision Records

---

## ADR 1: SQLite + EF Core

**Decision:** SQLite (file-based, zero Azure cost, runnable with `docker compose up`) with EF Core (idiomatic .NET ORM, code-first migrations, InMemory provider for tests).

**Key constraints:**
- WAL journal mode enabled at startup (`PRAGMA journal_mode=WAL` after `MigrateAsync`) — required because ASP.NET Core handles requests concurrently; without WAL, simultaneous reads and a write cause `SQLITE_BUSY` → 500. Idempotent to set on every boot.
- Azure Container Apps pinned to exactly 1 replica (`--min-replicas 1 --max-replicas 1` in `scripts/deploy.ps1`) — SQLite cannot be shared across processes.
- SQLite file persisted via named Docker volume locally; ephemeral per container restart in Azure (reseeds from `data/*.json` on every boot — by design).
- Switching to Azure SQL = replace connection string + provider package only; entities, migrations, and LINQ queries are database-agnostic.

---

## ADR 3: Structured tool-use over NL-to-SQL

**Decision:** Eight typed tools (`get_capacity_by_division`, `get_activity_allocations`, `get_unallocated_resources`, `get_cost_by_product`, `get_application_risk`, `get_product_risk`, `get_donor_candidates`, `get_project_allocation`) implemented as EF Core queries in `DataService.cs`. Claude calls tools; never sees schema or executes SQL.

**Key constraints:**
- Fixed surface area — questions outside the eight tools cannot be answered (data limitation, not a tool limitation).
- `get_cost_by_product` includes an explicit `"Unattributed"` line for cost not traceable to any product — intentional, not a data quality issue.
- `get_application_risk` and `get_product_risk` expose coverage, risk classification, and dependent product ARR; replaced the former `get_overallocated_resources` tool (removed in issue #14).
- `get_donor_candidates` returns candidates in eight-tier priority order (same-division tiers 1–4, cross-divisional tiers 5–8); added in issue #29.
- `get_project_allocation` returns FTE allocated to each project with `total_project_allocation_fte` and `projects_ranked_by_allocation`; added in issue #47. Projects carry no risk classification (ADR 10).
- All eight tools accept optional `month` parameter defaulting to the selected month.

---

## ADR 4: ASP.NET Core / C# backend

**Decision:** ASP.NET Core (.NET 10), C#. Controllers under `WorkforceMapper.Server/Controllers/`; `AddControllers()` / `MapControllers()` in `Program.cs`. No minimal-API inline handlers.

**Key constraints:**
- All API endpoints must be `[ApiController]` classes — do not inline handlers in `Program.cs`.
- Use `Anthropic.SDK` (`claude-haiku-4-5-20251001`) for all AI calls — no other AI SDK.

---

## ADR 6: Hard 100% allocation cap, API-enforced

**Decision:** `PUT /api/mappings` and `DELETE /api/mappings` enforce a hard cap. Writes that push a resource above 100% in a given month → 400 + `would_exceed_cap`. Writes against an inactive month → 400 + `resource_inactive_in_month`.

**Key constraints:**
- The cap rule and inactive-month rule are owned by `AllocationValidator` (single source of truth).
- Seed data is held to the same invariant: every active resource's monthly total is `<= 100%`.

---

## ADR 7: Capacity-driven application risk model

**Decision:** Each application carries `min_support_fte` and `recommended_support_fte` thresholds. Coverage = sum of `(mapping.percent / 100)` across active resources on the application's Support activity. Risk: `coverage < min` → high; `min <= coverage < recommended` → medium; `coverage >= recommended` → low. Product risk = max-wins over the transitive closure of direct apps + their `application_dependencies`.

**Key constraints:**
- Risk classification is owned by `RiskCalculator` (single source of truth).
- Dependency graph traversal is owned by `DependencyGraph`; BFS with visited-set termination handles cycles.
- Cost attribution is unchanged — Unattributed line behaviour is preserved.

---

## ADR 8: Integer-only mapping percent

**Decision:** `mapping.Percent` is a positive integer (1–100). The API rejects fractional values and zero. The frontend enforces integers at the input layer (`step="1"`, `parseInt`). Computed aggregates remain fractional decimals derived from integer inputs.

**Key constraints:**
- `AllocationValidator` is the single enforcement point: rejects `percent < 1`. `Mapping.Percent` is typed as `int`, so non-integer values are rejected at model-binding before reaching the validator.
- The frontend enforces integers: `type="text"` with `inputMode="numeric"`, `onChange` strips non-digit characters, `parseInt` on submit.
- Seed data must be regenerated from scratch using a largest-remainder integer distribution; do not patch existing fractional rows.
- `data/mappings.json` invariant: every active resource's monthly total is exactly 100%.

---

## ADR 9: MonthlyAllocationSnapshot is the canonical mapping-state primitive

**Decision:** `MonthlyAllocationSnapshot` owns per-month-state arithmetic for all tool methods. Each tool calls `await MonthlyAllocationSnapshot.LoadAsync(context, month)`, then projects from `ActiveResources`, `ActiveMappings`, and `SupportCoverageOf(applicationId)`. New tool methods consume the snapshot rather than re-querying.

The snapshot owns: active-flag rule, cost rule (`monthly_rate × percent / 100`), per-resource allocation percent and idle FTE, per-mapping allocated cost, and capacity coverage per application. It does **not** own risk classification (`RiskCalculator`), dependency closures (`DependencyGraph`), max-wins risk aggregation, donor-tier orchestration, or product/project cost attribution.

**Key constraints:**
- Snapshot is the single source of truth for `capacity_coverage`, `allocation_pct`, idle FTE, and per-resource / per-mapping cost. New tool methods must consume the snapshot, not re-query.
- `Resource.IsActiveIn(month)` is the single source of truth for the active-flag rule. `DataService.GetMonthFlag` was a duplicate and was removed in #41.
- Public constructor `MonthlyAllocationSnapshot(month, activeResources, activeMappings)` is the test seam — snapshot tests build snapshots without a `DbContext`.
- `DataService` delegates all Group D methods (`ProductCostAttribution`, `ApplicationRiskReporter`, `ProductRiskReporter`, `DonorCandidateAlgorithm`) to those named classes.
- See `docs/build/monthly-allocation-snapshot.md` for migration slices and test guidance.

---

## ADR 10: Projects are not risk-classified

**Decision:** The Projects tab and `get_project_allocation` report `project_allocation` FTE only. Projects carry no `min_*_fte` / `recommended_*_fte` thresholds, no risk classification, and no badge. The AgentService system prompt explicitly forbids the LLM from describing a project as "under-resourced", "adequately staffed", or "at risk".

**Key constraints:**
- `MonthlyAllocationSnapshot.ProjectAllocationOf(projectId)` is the single source of truth for project allocation; parallels `SupportCoverageOf` but spans all activity types of the project.
- The Projects tab renders a fill-only allocation bar — no min/recommended markers, no risk badge, no risk-derived sort.
- `get_project_allocation` system-prompt guidance must forbid adequacy/risk language explicitly.

---

## ADR 11: Pre-rendered deterministic prose in tool outputs

**Decision:** When a well-formed sentence can be composed deterministically from a tool's data, the tool owns the sentence — LLM renders verbatim, never reconstructs from raw fields. Covers risk explanations, FTE/coverage headlines, donor-fix summaries, mapping-change bullets, dependent-product summaries, cross-divisional sign-off warnings, and future per-row narrative.

**Key constraints:**
- Every pre-rendered string has a snapshot test for at least one representative scenario.
- Numeric-bearing prose has an invariant test asserting embedded numbers match source data.
- Raw fields are retained when pre-rendered fields are added — pre-rendered strings are additive for non-LLM consumers.
- System prompt instructs verbatim rendering and explicitly forbids reconstruction. Without the forbid the LLM pattern-matches to training-prior recomposition.
- New tools are designed pre-render-first: ask "what sentences will the LLM render?" before defining the data shape.
- Precedent: issue #33 (`mapping_changes`), #34 (`summary_paragraph`), `docs/build/user-test-feedback-2.md`.

---

## ADR 12: Demo auth toggle via GitHub Actions

**Decision:** Toggling Easy Auth on/off for live demos is done by two `workflow_dispatch` workflows (`demo-auth-off.yml`, `demo-auth-on.yml`). They authenticate via OIDC using a service principal with a custom role scoped to the single Container App (`Microsoft.App/containerApps/authConfigs/read+write` only). The "off" state uses `--unauthenticated-client-action AllowAnonymous` — Easy Auth and Entra registration stay configured; only anonymous behaviour changes.

**Key constraints:**
- Custom role `AssignableScopes` is the single Container App resource ID — not the resource group or subscription.
- Federated credential subject is `repo:samww/WorkforceMapper:ref:refs/heads/main`. Additional branches require additional federated credentials; do not broaden the subject.
- Both workflows share `concurrency.group: demo-auth-toggle` to serialise double-taps.
- Resource names (`workforce-mapper`, `rg-workforce-mapper`) are hardcoded in workflows to match deploy defaults.
- Setup is in `scripts/setup_demo_auth_toggle.ps1`; must remain idempotent on re-run.
- See `docs/build/issue-58-demo-auth-toggle.md` for the full implementation spec.
