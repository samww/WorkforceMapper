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

**Decision:** Seven typed tools (`get_capacity_by_division`, `get_activity_allocations`, `get_unallocated_resources`, `get_cost_by_product`, `get_application_risk`, `get_product_risk`, `get_donor_candidates`) implemented as EF Core queries in `DataService.cs`. Claude calls tools; never sees schema or executes SQL.

**Why not NL-to-SQL:**
- Security: tool-use exposes only seven read operations; NL-to-SQL exposes the full schema.
- Predictability: tool response shapes are typed and fixed; NL-to-SQL output varies per query.
- Testability: each tool is a C# method with a defined signature; tool call selection is assertable in unit tests.

**Key constraints:**
- Fixed surface area — questions outside the seven tools cannot be answered (data limitation, not a tool limitation).
- `get_cost_by_product` includes an explicit `"Unattributed"` line for cost not traceable to any product — this is intentional, not a data quality issue.
- `get_application_risk` and `get_product_risk` expose capacity coverage, risk classification, and dependent product ARR; they replace the former `get_overallocated_resources` tool (removed in issue #14).
- `get_donor_candidates` returns active resources whose capacity could shift onto an under-resourced application's Support activity, in eight-tier priority order (same-division tiers 1–4, cross-divisional tiers 5–8); added in issue #29.
- All seven tools accept optional `month` parameter defaulting to the selected month.

---

## ADR 4: ASP.NET Core / C# backend

**Decision:** ASP.NET Core (.NET 10), C#. Controllers under `WorkforceMapper.Server/Controllers/`; `AddControllers()` / `MapControllers()` in `Program.cs`. No minimal-API inline handlers.

**Key constraints:**
- All API endpoints must be `[ApiController]` classes — do not inline handlers in `Program.cs`.
- Use `Anthropic.SDK` for all AI calls — no other AI SDK.

---

## ADR 5: Advisory-only allocation constraint

**Status:** Superseded by ADR 6 (issue #13). The 100% allocation rule is now hard, API-enforced, with structured rejection codes for cap and inactive-month violations.

**Decision:** 100% allocation rule is advisory only. `PUT /api/mappings` accepts any `percent` value. Violations surface in the UI (red over-allocation indicator, unallocated filter) and via NL query tools. No DB CHECK constraint, no API rejection.

**Why:** Over-allocation is a valid operational state (Platform Engineering runs at ~112%). Enforcing a cap at API or DB level would prevent users from recording real-world conditions.

**Key constraint:** The seeded `data/mappings.json` is the authoritative example: every active resource in `2026-05` sums to exactly 100%, with deliberate under-allocation for two India Analytics Associates (new hires, not yet mapped).

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
