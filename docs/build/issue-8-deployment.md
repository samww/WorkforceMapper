# Issue #8 — Docker + Azure Container Apps deployment

> Domain context is in `CONTEXT.md`.

## Phase 6 — Docker and Azure deployment

### Dockerfile (multi-stage)

- Stage 1 `node:22-slim AS frontend-build`: copy `workforcemapper.client/`, `npm ci` + `npm run build`
- Stage 2 `mcr.microsoft.com/dotnet/sdk:10.0 AS build`: `dotnet restore`, `dotnet publish -c Release`
- Stage 3 `mcr.microsoft.com/dotnet/aspnet:10.0`: copy published output + built frontend; expose port 8080

**Note:** MCR mirror only carries Node up to 20 — use `node:22-slim` from Docker Hub.

### docker-compose.yml

- Mounts `.env` for `ANTHROPIC_API_KEY`
- Exposes port 8080
- Named volume `sqlite_data` → `/app/data` for SQLite persistence
- Mounts `./data` → `/app/data` read-only for seeding

### `.env.example`

```
ANTHROPIC_API_KEY=sk-ant-...
# AZURE_AD_TENANT_ID=...
# AZURE_AD_CLIENT_ID=...
```

### Azure deployment scripts (PowerShell, idempotent)

`scripts/deploy.ps1` — creates resource group, ACR (Basic SKU), builds image via `az acr build`, creates/updates Container App. Re-running updates the live app.

`scripts/setup_auth.ps1` — enables Easy Auth: creates/reuses Entra app registration, wires to Container App.

Defaults: app `workforce-mapper`, resource group `rg-workforce-mapper`, region UK South. Override with `$env:APP_NAME`, `$env:RESOURCE_GROUP`, `$env:LOCATION`.

### Verification

`docker compose up` → seed completes → ask "Which divisions have unallocated monthly cost this month?" → verify Data Collection (~$18k/month idle) in response.

**Output:** Working Docker container; deployed Azure Container App behind Microsoft login
