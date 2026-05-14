# WorkforceMapper

## Domain docs

Read `CONTEXT.md` at the repo root before writing any code. It covers the domain model, glossary, API contracts, key constraints, and tech stack.

Active iteration: **#30–#34**. Done: #2–#23, #26, #27, #29, #31, #32, #33. Open: #30, #34 (agent tool-quality follow-ups; see `docs/temp/agent-response-feedback.md`).

Only read deeper docs when directly relevant to the issue being worked on:

| Issue | Spec | Blocked by |
|---|---|---|
| All done issues | `docs/build/issue-N-*.md` — read only when maintaining existing code | — |
| #30 Agent surface: six-tier donor prompt | `docs/build/issue-30-agent-donor-prompt.md` | — |
| #34 Pre-rendered `summary_paragraph` | `docs/build/issue-34-summary-paragraph.md` | — |

Additional docs (only when needed):
- `docs/build/risk-model.md` — capacity coverage formula, application risk classification, product max-wins aggregation, dependency graph contract, donor candidate tier model
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
