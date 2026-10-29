# WorkforceMapper

## Domain Docs

Read `CONTEXT.md` before writing code - domain model, glossary, API contracts, constraints, tech stack.

Closed-issue specs in `docs/build/issue-N-*.md` and `docs/build/user-test-feedback-*.md` are reference-only. Open work: each GitHub issue links to a `docs/build/` design doc.

Read on demand:

- `docs/adr/decisions.md` - ADRs 1, 3, 4, 6-12 (SQLite, tool-use, .NET, hard cap, risk model, integer percent, snapshot, projects-not-risk, pre-rendered prose, demo auth toggle via GH Actions). Flag any conflict before proceeding.
- `docs/build/risk-model.md` - coverage, application + product risk, risk reach, donor tiers.
- `docs/build/monthly-allocation-snapshot.md` - when touching `MonthlyAllocationSnapshot`.
- `docs/build/agent-system-prompt.md` - when touching `AgentService` or `WorkforceToolCatalog`.
- `docs/build/frontend-components.md` - frontend only.
- `docs/build/iphone-16-pro-max-mobile-nav-applications-table.md` - implemented responsive shell nav and Applications table behavior.
- `docs/build/db-schema.md` - cost-attribution + DB constraints.
- `docs/build/synthetic-data-spec.md` - when regenerating `data/*.json`.

Use `CONTEXT.md` glossary terms.

Risk reach: `RiskReach` wraps `DependencyGraph` — owns product closure, products reached by an application, transitive dependent applications, and stable exposure ordering. Keep application risk classification in `RiskCalculator`; reporters own DTO/prose assembly only.

## CI and Dependencies

- CI (`.github/workflows/ci.yml`) runs on every PR and push to `main`: client `npm run lint`, `npm test`, `npm run build`; root `npm test` (generator); `dotnet test`. Keep client lint at zero errors - it fails the build. Tests need no secrets.
- Dependabot (`.github/dependabot.yml`) groups minor/patch and security updates into one weekly PR per ecosystem (npm across `/` and `/workforcemapper.client`, NuGet, GitHub Actions); major bumps arrive individually. CI's `auto-merge` job rebase-merges Dependabot patch/minor PRs (npm, NuGet) once all CI jobs pass; majors and GitHub Actions bumps need a manual merge.

## Issue Management

For triage / issue work only:

- `docs/agents/issue-tracker.md` - `gh` CLI conventions.
- `docs/agents/triage-labels.md` - label vocabulary (`needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`).
