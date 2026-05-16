# WorkforceMapper

## Domain docs

Read `CONTEXT.md` at the repo root before writing any code. It covers the domain model, glossary, API contracts, key constraints, and tech stack.

Issues #2–#44 are complete; their per-issue specs in `docs/build/issue-N-*.md` are reference-only when maintaining that code. Open work is tracked on GitHub — each open issue links to a feature-level design doc in `docs/build/` rather than a per-issue spec.

Additional docs (only when needed):
- `docs/build/risk-model.md` — capacity coverage, application risk, product risk, donor candidate tier model
- `docs/build/monthly-allocation-snapshot.md` — snapshot interface, what it owns, construction pattern (only when adding new tool methods or modifying MonthlyAllocationSnapshot)
- `docs/build/db-schema.md` — cost attribution chain, key constraints
- `docs/build/synthetic-data-spec.md` — seed data rate tables (only when regenerating `data/*.json` from scratch)
- `docs/build/frontend-components.md` — detail screen props, navigation state, MermaidDiagram, DependencyGraph (only when touching frontend)
- `docs/build/agent-system-prompt.md` — verbatim AgentService system prompt (only when maintaining chat agent)
- `docs/adr/decisions.md` — ADR 1 (SQLite), ADR 3 (tool-use), ADR 4 (C#/.NET), ADR 6 (hard cap), ADR 7 (risk model), ADR 8 (integer-only percent), ADR 9 (snapshot primitive)

Use the glossary vocabulary from `CONTEXT.md` when referring to domain concepts. If a planned implementation conflicts with an ADR, flag it before proceeding.

## Issue management

Only read these when performing triage or issue-management tasks (not when writing code):
- `docs/agents/issue-tracker.md` — GitHub CLI conventions
- `docs/agents/triage-labels.md` — label vocabulary
