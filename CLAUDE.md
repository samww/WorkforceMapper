# WorkforceMapper

## Domain docs

Read `CONTEXT.md` at the repo root before writing any code. It covers the domain model, glossary, API contracts, all five tool signatures, key constraints, and tech stack.

Only read deeper docs when directly relevant to the issue being worked on:

| Issue | Status | Implementation spec | Also read |
|---|---|---|---|
| #2 DB schema + seed data | Done | `docs/build/issue-2-data-layer.md` | — |
| #3 Resources + Activities API | Done | `docs/build/issue-3-resources-api.md` | `docs/build/api-contracts.md` |
| #4 Mappings API | Done | — | — |
| #5 Mapping UI | Done | `docs/build/issue-5-mapping-ui.md` | `docs/build/api-contracts.md` |
| #6 Chat API | Done | `docs/build/issue-6-chat-api.md` | `docs/build/api-contracts.md` |
| #7 Chat UI | Done | `docs/build/issue-7-chat-ui.md` | `docs/build/api-contracts.md` |
| #8 Docker + Azure deployment | Done | `docs/build/issue-8-deployment.md` | — |

Additional docs (only when needed):
- `docs/build/db-schema.md` — cost attribution chain, key constraints (issues #2–#4)
- `docs/build/synthetic-data-spec.md` — seed data rate tables and scenario values (only when regenerating `data/*.json` files from scratch)
- `docs/build/agent-system-prompt.md` — verbatim AgentService system prompt (issue #6 only)
- `docs/adr/decisions.md` — ADR 1 (SQLite), ADR 3 (tool-use), ADR 4 (C#/.NET), ADR 5 (advisory constraint)

Use the glossary vocabulary from `CONTEXT.md` when referring to domain concepts. If a planned implementation conflicts with an ADR, flag it before proceeding.

## Issue management

Only read these when performing triage or issue-management tasks (not when writing code):
- `docs/agents/issue-tracker.md` — GitHub CLI conventions
- `docs/agents/triage-labels.md` — label vocabulary
