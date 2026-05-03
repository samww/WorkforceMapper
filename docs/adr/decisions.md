# Architecture Decision Records

---

## ADR 1: SQLite + EF Core

**Decision:** SQLite (file-based, zero Azure cost, runnable with docker compose up) with EF Core (idiomatic .NET ORM, code-first migrations, InMemory provider for tests).

**Key constraints:**
- No concurrent writes — single-user demo; acceptable.
- SQLite file persisted via named Docker volume; lost if volume deleted.
- Switching to Azure SQL = replace connection string + provider package only; entities, migrations, and LINQ queries are database-agnostic.

---

## ADR 3: Structured tool-use over NL-to-SQL

**Decision:** Five typed tools (`get_capacity_by_division`, `get_activity_allocations`, `get_unallocated_resources`, `get_overallocated_resources`, `get_cost_by_product`) implemented as EF Core queries in `DataService.cs`. Claude calls tools; never sees schema or executes SQL.

**Why not NL-to-SQL:**
- Security: tool-use exposes only five read operations; NL-to-SQL exposes the full schema.
- Predictability: tool response shapes are typed and fixed; NL-to-SQL output varies per query.
- Testability: each tool is a C# method with a defined signature; tool call selection is assertable in unit tests.

**Key constraints:**
- Fixed surface area — questions outside the five tools cannot be answered (data limitation, not a tool limitation).
- `get_cost_by_product` includes an explicit `"Unattributed"` line for cost not traceable to any product — this is intentional, not a data quality issue.
- All five tools accept optional `month` parameter defaulting to the selected month.

---

## ADR 4: ASP.NET Core / C# backend

**Decision:** ASP.NET Core (.NET 10), C#. Controllers under `WorkforceMapper.Server/Controllers/`; `AddControllers()` / `MapControllers()` in `Program.cs`. No minimal-API inline handlers.

**Key constraints:**
- All API endpoints must be `[ApiController]` classes — do not inline handlers in `Program.cs`.
- Use `Anthropic.SDK` for all AI calls — no other AI SDK.

---

## ADR 5: Advisory-only allocation constraint

**Decision:** 100% allocation rule is advisory only. `PUT /api/mappings` accepts any `percent` value. Violations surface in the UI (red over-allocation indicator, unallocated filter) and via NL query tools. No DB CHECK constraint, no API rejection.

**Why:** Over-allocation is a valid operational state (Platform Engineering runs at ~112%). Enforcing a cap at API or DB level would prevent users from recording real-world conditions.

**Key constraint:** The seeded `data/mappings.json` is the authoritative example: every active resource in `2026-05` sums to exactly 100%, with deliberate under-allocation for two India Analytics Associates (new hires, not yet mapped).
