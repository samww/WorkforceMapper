# WorkforceMapper

## Domain docs

Read `CONTEXT.md` at the repo root before writing any code. It covers the domain model, glossary, API contracts, key constraints, and tech stack.

Active iteration: **#29, #30**. Issues #11–#23, #26, #27 done; `CONTEXT.md` reflects current state.

Only read deeper docs when directly relevant to the issue being worked on:

| Issue | Spec | Blocked by |
|---|---|---|
| #2–#19, #21–#23 (done) | `docs/build/issue-N-*.md` — read only when maintaining existing code | — |
| #20 (done) Demo-aligned query chips | `docs/build/issue-20-query-chips.md` — read only when maintaining existing code | — |
| #29 Six-tier donor candidate pipeline | `docs/build/issue-29-tiered-donor-candidates.md` | — |
| #30 Agent surface: six-tier donor prompt | `docs/build/issue-30-agent-donor-prompt.md` | #29 |

Additional docs (only when needed):
- `docs/build/risk-model.md` — capacity coverage formula, application risk classification, product max-wins aggregation, dependency graph contract, donor candidate tier model (#11, #12, #14, #29)
- `docs/build/db-schema.md` — cost attribution chain, key constraints (#11)
- `docs/build/synthetic-data-spec.md` — seed data rate tables (only when regenerating `data/*.json` from scratch)
- `docs/build/agent-system-prompt.md` — verbatim AgentService system prompt (only when maintaining chat agent)
- `docs/adr/decisions.md` — ADR 1 (SQLite), ADR 3 (tool-use), ADR 4 (C#/.NET), ADR 5 (advisory — superseded by ADR 6), ADR 6 (hard cap), ADR 7 (risk model), ADR 8 (integer-only percent — #21–#23)

Use the glossary vocabulary from `CONTEXT.md` when referring to domain concepts. If a planned implementation conflicts with an ADR, flag it before proceeding.

## Issue management

Only read these when performing triage or issue-management tasks (not when writing code):
- `docs/agents/issue-tracker.md` — GitHub CLI conventions
- `docs/agents/triage-labels.md` — label vocabulary
