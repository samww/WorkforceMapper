# Issue #9 — README + CLAUDE.md

> Domain context and the intentional demo scenario values are in `CONTEXT.md`.
> Synthetic data details (rate tables, ARR figures) are in `docs/build/synthetic-data-spec.md`.

## Phase 7 — Documentation

### README.md sections

1. One-line description + what the system does
2. Why this use case — workforce capacity management problem; link to `product-portfolio-rag`
3. Centrepiece demo: AuthService Replacement shortfall query and answer (~$28k/month allocated vs ~$42k/month needed; vendor EOL deadline; Platform Engineering 112% over-allocation; all 7 products exposed including DataLicensing $6.2m ARR; $28.9m total ARR at risk)
4. Architecture diagram (Mermaid): React → ASP.NET Core → SQLite → Claude (tool-use) → SSE stream
5. AI layer explanation — NL query via tool-use loop; reference `docs/adr/decisions.md`
6. Example queries + answers (at least 3: AuthService shortfall, division bench capacity, run/change split)
7. Local dev setup (`docker compose up`)
8. Azure deployment (`scripts/deploy.ps1`, `scripts/setup_auth.ps1`)

### CLAUDE.md

Agent context covering: solution structure, test commands (`dotnet test`, `npx vitest run`), conventions (EF Core for all data access, InMemory provider for tests), intentional data scenarios.

**Output:** README.md, CLAUDE.md
