# WorkforceMapper

## Domain docs

Read `CONTEXT.md` before writing code â€” domain model, glossary, API contracts, constraints, tech stack.

Closed-issue specs in `docs/build/issue-N-*.md` and `docs/build/user-test-feedback-*.md` are reference-only. Open work: each GitHub issue links to a `docs/build/` design doc.

Read on demand:

- `docs/adr/decisions.md` â€” ADRs 1, 3, 4, 6â€“11 (SQLite, tool-use, .NET, hard cap, risk model, integer percent, snapshot, projects-not-risk, pre-rendered prose). Flag any conflict before proceeding.
- `docs/build/risk-model.md` â€” coverage, application + product risk, donor tiers
- `docs/build/monthly-allocation-snapshot.md` â€” when touching `MonthlyAllocationSnapshot`
- `docs/build/agent-system-prompt.md` â€” when touching `AgentService` or `WorkforceToolCatalog`
- `docs/build/frontend-components.md` â€” frontend only
- `docs/build/db-schema.md` â€” cost-attribution + DB constraints
- `docs/build/synthetic-data-spec.md` â€” when regenerating `data/*.json`

Use `CONTEXT.md` glossary terms.

## Issue management

For triage / issue work only:
- `docs/agents/issue-tracker.md` â€” `gh` CLI conventions
- `docs/agents/triage-labels.md` â€” label vocabulary

## Agent skills

### Issue tracker

Issues are tracked in this repo's GitHub Issues via the `gh` CLI. See `docs/agents/issue-tracker.md`.

### Triage labels

The triage label vocabulary uses the canonical labels `needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, and `wontfix`. See `docs/agents/triage-labels.md`.

### Domain docs

This repo uses a single-context domain-doc layout with root `CONTEXT.md` and `docs/adr/`. See `docs/agents/domain.md`.
