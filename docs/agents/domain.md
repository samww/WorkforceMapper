# Domain Docs

Read `CONTEXT.md` at the repo root first. It covers the glossary, data model, API contracts, all five NL query tool signatures, key constraints, intentional demo scenarios, and the AgentService system prompt.

Only read deeper docs when directly relevant to the issue:
- `docs/build/build-plan.md` — phase-by-phase implementation spec and test names
- `docs/build/db-schema.md` — cost attribution chain, key constraints, reference counts
- `docs/build/synthetic-data-spec.md` — seed data details (only needed when verifying/regenerating data files)
- `docs/adr/decisions.md` — ADR 1 (SQLite), ADR 3 (tool-use), ADR 4 (C#/.NET), ADR 5 (advisory constraint)

Use the glossary vocabulary from `CONTEXT.md` when referring to domain concepts. If a planned implementation conflicts with an ADR, flag it before proceeding.
