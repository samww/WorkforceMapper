# WorkforceMapper

## Domain docs

Read `CONTEXT.md` at the repo root before writing any code. It covers the domain model, glossary, API contracts, key constraints, and tech stack.

Active iteration: **#17–#20** (multi-month data, dependency diagrams, mobile, query chips). Issues #11–#16 done; `CONTEXT.md` reflects current state.

Only read deeper docs when directly relevant to the issue being worked on:

| Issue | Spec |
|---|---|
| #2–#16 (done) | `docs/build/issue-N-*.md` — read only when maintaining existing code |
| #17 Multi-month synthetic data | `docs/build/issue-17-multi-month-data.md` — also read `docs/build/synthetic-data-spec.md` |
| #18 Mermaid dependency diagrams | `docs/build/issue-18-mermaid-diagrams.md` — also read `docs/build/risk-model.md` |
| #19 Mobile touch targets | `docs/build/issue-19-touch-targets.md` |
| #20 Demo-aligned query chips | `docs/build/issue-20-query-chips.md` — also read `docs/background/DEMO_SCRIPT.md` |

Additional docs (only when needed):
- `docs/build/risk-model.md` — capacity coverage formula, application risk classification, product max-wins aggregation, dependency graph contract (#11, #12, #14)
- `docs/build/db-schema.md` — cost attribution chain, key constraints (#11)
- `docs/build/synthetic-data-spec.md` — seed data rate tables (only when regenerating `data/*.json` from scratch)
- `docs/build/agent-system-prompt.md` — verbatim AgentService system prompt (only when maintaining chat agent)
- `docs/adr/decisions.md` — ADR 1 (SQLite), ADR 3 (tool-use), ADR 4 (C#/.NET), ADR 5 (advisory — superseded by ADR 6), ADR 6 (hard cap), ADR 7 (risk model)

Use the glossary vocabulary from `CONTEXT.md` when referring to domain concepts. If a planned implementation conflicts with an ADR, flag it before proceeding.

## Issue management

Only read these when performing triage or issue-management tasks (not when writing code):
- `docs/agents/issue-tracker.md` — GitHub CLI conventions
- `docs/agents/triage-labels.md` — label vocabulary
