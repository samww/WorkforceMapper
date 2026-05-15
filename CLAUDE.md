# WorkforceMapper

## Domain docs

Read `CONTEXT.md` at the repo root before writing any code. It covers the domain model, glossary, API contracts, key constraints, and tech stack.

All issues (#2–#36) are complete. Read `docs/build/issue-N-*.md` only when maintaining existing code.

Additional docs (only when needed):
- `docs/build/risk-model.md` — capacity coverage, application risk, product risk, donor candidate tier model
- `docs/build/db-schema.md` — cost attribution chain, key constraints
- `docs/build/synthetic-data-spec.md` — seed data rate tables (only when regenerating `data/*.json` from scratch)
- `docs/build/frontend-components.md` — detail screen props, navigation state, MermaidDiagram, DependencyGraph (only when touching frontend)
- `docs/build/agent-system-prompt.md` — verbatim AgentService system prompt (only when maintaining chat agent)
- `docs/adr/decisions.md` — ADR 1 (SQLite), ADR 3 (tool-use), ADR 4 (C#/.NET), ADR 6 (hard cap), ADR 7 (risk model), ADR 8 (integer-only percent)

Use the glossary vocabulary from `CONTEXT.md` when referring to domain concepts. If a planned implementation conflicts with an ADR, flag it before proceeding.

## Issue management

Only read these when performing triage or issue-management tasks (not when writing code):
- `docs/agents/issue-tracker.md` — GitHub CLI conventions
- `docs/agents/triage-labels.md` — label vocabulary
