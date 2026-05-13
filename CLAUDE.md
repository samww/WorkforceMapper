# WorkforceMapper

## Domain docs

Read `CONTEXT.md` at the repo root before writing any code. It covers the domain model, glossary, API contracts, key constraints, and tech stack.

Active iteration: **#30–#34**. Issues #11–#23, #26, #27, #29, #31 done; `CONTEXT.md` reflects current state. Issues #32–#34 are the remaining agent tool-quality follow-ups from the AuthService donor-candidate response review (see `docs/temp/agent-response-feedback.md`).

Only read deeper docs when directly relevant to the issue being worked on:

| Issue | Spec | Blocked by |
|---|---|---|
| #2–#19, #21–#23 (done) | `docs/build/issue-N-*.md` — read only when maintaining existing code | — |
| #20 (done) Demo-aligned query chips | `docs/build/issue-20-query-chips.md` — read only when maintaining existing code | — |
| #29 (done) Six-tier donor candidate pipeline | `docs/build/issue-29-tiered-donor-candidates.md` — read only when maintaining existing code | — |
| #30 Agent surface: six-tier donor prompt | `docs/build/issue-30-agent-donor-prompt.md` | — |
| #31 (done) Largest-remainders rounding for `donable_percent` | `docs/build/issue-31-rounding.md` — read only when maintaining existing code | — |
| #32 Trim `get_donor_candidates` response | `docs/build/issue-32-trim-candidates.md` | — |
| #33 Pre-rendered `mapping_changes` list | `docs/build/issue-33-mapping-changes.md` | — |
| #34 Pre-rendered `summary_paragraph` | `docs/build/issue-34-summary-paragraph.md` | — |

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
