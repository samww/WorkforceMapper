# WorkforceMapper

## Domain docs

Read `CONTEXT.md` before writing code — domain model, glossary, API contracts, constraints, tech stack.

Closed-issue specs in `docs/build/issue-N-*.md` and `docs/build/user-test-feedback-*.md` are reference-only. Open work: each GitHub issue links to a `docs/build/` design doc.

Read on demand:

- `docs/adr/decisions.md` — ADRs 1, 3, 4, 6–11 (SQLite, tool-use, .NET, hard cap, risk model, integer percent, snapshot, projects-not-risk, pre-rendered prose). Flag any conflict before proceeding.
- `docs/build/risk-model.md` — coverage, application + product risk, donor tiers
- `docs/build/monthly-allocation-snapshot.md` — when touching `MonthlyAllocationSnapshot`
- `docs/build/agent-system-prompt.md` — when touching `AgentService`
- `docs/build/frontend-components.md` — frontend only
- `docs/build/db-schema.md` — cost-attribution + DB constraints
- `docs/build/synthetic-data-spec.md` — when regenerating `data/*.json`

Use `CONTEXT.md` glossary terms.

## Issue management

For triage / issue work only:
- `docs/agents/issue-tracker.md` — `gh` CLI conventions
- `docs/agents/triage-labels.md` — label vocabulary
