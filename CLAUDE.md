# WorkforceMapper

## Agent skills

### Issue tracker

Issues live in GitHub Issues (`samww/WorkforceMapper`). See `docs/agents/issue-tracker.md`.

### Triage labels

Uses the default five-label vocabulary (needs-triage, needs-info, ready-for-agent, ready-for-human, wontfix). See `docs/agents/triage-labels.md`.

### Domain docs

Single-context repo: one `CONTEXT.md` + `docs/adr/` at the repo root. See `docs/agents/domain.md`.

## Architecture notes

- Resources and mappings are seeded from `data/resources.json` and `data/mappings.json` at startup (if tables are empty). Taxonomy (applications, products, projects, activities) is seeded via EF Core `HasData()` migrations.
- All backend endpoints are implemented as Controller classes under `WorkforceMapper.Server/Controllers/` using `[ApiController]` / `[Route]` attributes. Do not add inline route handlers in `Program.cs`.
- There is no Excel upload feature, no ClosedXML dependency, no UploadService, and no SuggestionService.
- The frontend (`workforcemapper.client`) is a two-panel layout: `MappingView` (left) + `ChatPanel` (right). No UploadPanel, no SuggestionDrawer.

## Build reference

Before writing any code, read:
- `docs/build/build-plan.md` — full feature spec: data model, API contracts, tool signatures, system prompt, frontend architecture
- `docs/build/db-schema.md` — ERD, cost model rules, key constraints
- `docs/adr/decisions.md` — architecture decisions (SQLite, advisory allocation constraint, tool-use over NL-to-SQL, etc.)
