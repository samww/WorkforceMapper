# WorkforceMapper

## Domain Docs

Read `CONTEXT.md` before writing code - domain model, glossary, API contracts, constraints, tech stack.

Closed-issue specs in `docs/build/issue-N-*.md` and `docs/build/user-test-feedback-*.md` are reference-only. Open work: each GitHub issue links to a `docs/build/` design doc.

Read on demand:

- `docs/adr/decisions.md` - ADRs 1, 3, 4, 6-11 (SQLite, tool-use, .NET, hard cap, risk model, integer percent, snapshot, projects-not-risk, pre-rendered prose). Flag any conflict before proceeding.
- `docs/build/risk-model.md` - coverage, application + product risk, risk reach, donor tiers.
- `docs/build/monthly-allocation-snapshot.md` - when touching `MonthlyAllocationSnapshot`.
- `docs/build/agent-system-prompt.md` - when touching `AgentService` or `WorkforceToolCatalog`.
- `docs/build/frontend-components.md` - frontend only.
- `docs/build/iphone-16-pro-max-mobile-nav-applications-table.md` - implemented responsive shell nav and Applications table behavior.
- `docs/build/db-schema.md` - cost-attribution + DB constraints.
- `docs/build/synthetic-data-spec.md` - when regenerating `data/*.json`.

Use `CONTEXT.md` glossary terms.

Risk reach guidance: `RiskReach` wraps `DependencyGraph` and owns product closure, products reached by an application, transitive dependent applications, and stable exposure ordering. Keep application risk classification in `RiskCalculator`; reporters should own DTO/prose assembly only.

## Issue Management

For triage / issue work only:

- `docs/agents/issue-tracker.md` - `gh` CLI conventions.
- `docs/agents/triage-labels.md` - label vocabulary.

## Agent Skills

### Issue Tracker

Issues are tracked in this repo's GitHub Issues via the `gh` CLI. See `docs/agents/issue-tracker.md`.

### Triage Labels

The triage label vocabulary uses the canonical labels `needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, and `wontfix`. See `docs/agents/triage-labels.md`.

### Domain Docs

This repo uses a single-context domain-doc layout with root `CONTEXT.md` and `docs/adr/`. See `docs/agents/domain.md`.
