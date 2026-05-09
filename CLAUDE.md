# WorkforceMapper

## Domain docs

Read `CONTEXT.md` at the repo root before writing any code. It covers the domain model, glossary, API contracts, key constraints, and tech stack.

> **Issue #10 in progress.** `CONTEXT.md` reflects the pre-#10 state. The hard 100% cap (#13) has landed — the advisory rule is now enforced at the API layer. Remaining open slice: #15 (ADR + CONTEXT.md doc updates). The target API surface and tool list are in `docs/build/api-contracts.md`.

Only read deeper docs when directly relevant to the issue being worked on:

| Issue | Status | Implementation spec | Also read |
|---|---|---|---|
| #2–#8 (done) | Done | Specs in `docs/build/issue-N-*.md` — only load when maintaining existing code | — |
| #10 Risk-driven support model (PRD parent) | Open | — read GitHub issue body for full PRD | — only when planning slices |
| #11 Application risk model + UI | Done | `docs/build/issue-11-application-risk.md` — only load when maintaining existing code | — |
| #12 Product risk propagation | Done | `docs/build/issue-12-product-risk.md` — only load when maintaining existing code | — |
| #13 Hard 100% allocation cap | Done | `docs/build/issue-13-allocation-cap.md` — only load when maintaining existing code | — |
| #14 Chat agent risk tools | Done | `docs/build/issue-14-chat-tools.md` — only load when maintaining existing code | — |
| #15 ADR + CONTEXT.md doc updates | Open | `docs/build/issue-15-doc-updates.md` | `docs/adr/decisions.md`, `docs/build/risk-model.md` |

Additional docs (only when needed):
- `docs/build/risk-model.md` — capacity coverage formula, application risk classification, product max-wins aggregation, dependency graph contract (#11, #12, #14)
- `docs/build/db-schema.md` — cost attribution chain, key constraints (#11)
- `docs/build/synthetic-data-spec.md` — seed data rate tables (only when regenerating `data/*.json` from scratch)
- `docs/build/agent-system-prompt.md` — verbatim AgentService system prompt (only when maintaining chat agent)
- `docs/adr/decisions.md` — ADR 1 (SQLite), ADR 3 (tool-use), ADR 4 (C#/.NET), ADR 5 (advisory — superseded by ADR 6 in #15)

Use the glossary vocabulary from `CONTEXT.md` when referring to domain concepts. If a planned implementation conflicts with an ADR, flag it before proceeding.

## Issue management

Only read these when performing triage or issue-management tasks (not when writing code):
- `docs/agents/issue-tracker.md` — GitHub CLI conventions
- `docs/agents/triage-labels.md` — label vocabulary
