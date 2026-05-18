# Demo Auth Toggle — GitHub Actions

> Pairs with ADR 12. Linked from the open GitHub issue.

## Goal

Let the maintainer flip the Azure Container App's Easy Auth on and off from a phone during a live demo, then re-lock it afterward. The "off" state must allow anonymous attendees to load the app without an Entra sign-in. No application-code changes; no in-app admin UI.

## Architecture

Two `workflow_dispatch` GitHub Actions workflows, each one tap away in the GitHub mobile app, each calling a single `az containerapp auth update` against the deployed Container App. Workflows authenticate to Azure via OIDC (no long-lived secret in GitHub) using a service principal that holds a custom role with the minimum permissions to read/write the Container App's auth config — nothing else.

The "off" state uses `--unauthenticated-client-action AllowAnonymous`, not `--enabled false`: Easy Auth and the Entra app registration stay wired up; only the unauthenticated-client behaviour changes. Re-enabling is one CLI call, and signed-in users continue to get `X-MS-CLIENT-PRINCIPAL` headers throughout.

Rationale and rejected alternatives (in-app React UI, Cloud Shell, Azure mobile app): see ADR 12.

## Deliverables

1. `scripts/setup_demo_auth_toggle.ps1` — idempotent one-time setup, run from maintainer's laptop.
2. `.github/workflows/demo-auth-off.yml` — flips Easy Auth to AllowAnonymous.
3. `.github/workflows/demo-auth-on.yml` — flips Easy Auth back to RedirectToLoginPage.
4. README addition (optional, see "Documentation" below) — short "Demo auth toggle" section pointing at the workflows.

No changes to:
- `WorkforceMapper.Server/` (no .NET code touches Easy Auth)
- `workforcemapper.client/` (no React UI changes)
- `scripts/setup_auth.ps1` (initial Easy Auth bootstrap remains unchanged)
- `scripts/deploy.ps1`

## Setup script: `scripts/setup_demo_auth_toggle.ps1`

Idempotent, ASCII-only PowerShell (see memory: scripts must not contain non-ASCII characters — Windows-1252 misreads UTF-8 quotes and silently breaks parsing). Mirrors the style of `scripts/setup_auth.ps1` and `scripts/deploy.ps1`: reads `.env` at repo root, defaults `APP_NAME` / `RESOURCE_GROUP` the same way, fails fast on non-zero `az` exits via the same `Assert-AzSuccess` helper.

Resolves the following at runtime (do not hardcode):
- `$TenantId` and `$SubscriptionId` from `az account show`
- `$AppResourceId` from `az containerapp show -n $AppName -g $ResourceGroup --query id -o tsv`
- `$RepoSlug` from `git config --get remote.origin.url` (parse out `owner/repo`); fall back to env var `GITHUB_REPO` if parsing fails

### What it does, in order

1. **Create or reuse Entra app registration** named `${AppName}-gha-auth-toggle` (parallel to the `${AppName}-auth` pattern in `setup_auth.ps1`).
   - Use `az ad app list --display-name` to check for existing.
   - If new: `az ad app create --display-name "$RegName" --sign-in-audience AzureADMyOrg`.
   - Capture `$ClientId`.

2. **Create or reuse the service principal** for that app registration.
   - `az ad sp list --filter "appId eq '$ClientId'"`; create with `az ad sp create --id $ClientId` if missing.
   - Capture `$SpObjectId` (the SP's `id`, not the app's `appId`).

3. **Create or update the custom role** `Container App Auth Toggle - $AppName`.
   - Definition (write to a temp JSON file):
     ```json
     {
       "Name": "Container App Auth Toggle - workforce-mapper",
       "IsCustom": true,
       "Description": "Read and write Easy Auth config for one Container App.",
       "Actions": [
         "Microsoft.App/containerApps/read",
         "Microsoft.App/containerApps/authConfigs/read",
         "Microsoft.App/containerApps/authConfigs/write"
       ],
       "NotActions": [],
       "AssignableScopes": [
         "<AppResourceId from step above>"
       ]
     }
     ```
   - Check existence: `az role definition list --name "$RoleName" --scope $AppResourceId`.
   - Create with `az role definition create --role-definition <file>` or update with `az role definition update`.
   - **Verify the operation names before committing the script** — run `az provider operation show --namespace Microsoft.App --query "resourceTypes[?name=='containerApps/authConfigs'].operations[].name"` and adjust if the operation strings differ. The set above matches Microsoft.App as of the spec date but ARM operation names occasionally change.

4. **Assign the custom role to the service principal**, scoped to the Container App only.
   - `az role assignment create --assignee-object-id $SpObjectId --assignee-principal-type ServicePrincipal --role "$RoleName" --scope $AppResourceId`
   - Tolerate "role assignment already exists" exit code by re-checking before failing.

5. **Create or update a federated credential** on the app registration for GitHub Actions OIDC.
   - Subject: `repo:${RepoSlug}:ref:refs/heads/main` (workflows live on main; do not federate against pull-request or environment subjects unless a clear reason emerges).
   - Issuer: `https://token.actions.githubusercontent.com`
   - Audiences: `["api://AzureADTokenExchange"]`
   - Name: `gha-main-branch`
   - Use `az ad app federated-credential create` (or `list` + `update` if it already exists — currently `update` requires the credential `id`; list first to capture).

6. **Print the three values** the maintainer must add to GitHub Secrets:
   ```
   AZURE_CLIENT_ID       = <ClientId>
   AZURE_TENANT_ID       = <TenantId>
   AZURE_SUBSCRIPTION_ID = <SubscriptionId>
   ```
   Plus copy-paste-ready `gh secret set` commands so the maintainer can paste them directly:
   ```
   gh secret set AZURE_CLIENT_ID       --body <ClientId>
   gh secret set AZURE_TENANT_ID       --body <TenantId>
   gh secret set AZURE_SUBSCRIPTION_ID --body <SubscriptionId>
   ```

Script must be safely re-runnable: a second run should be a no-op (or print "reused existing X" messages) and must not duplicate role assignments or federated credentials.

## Workflows

Both workflows go in `.github/workflows/`. Keep them as two separate files (not one workflow with an input) — single-tap mobile UX, no risk of misclicking a dropdown under demo stress. Workflow names use a `Demo - ` prefix so they group together in the Actions tab.

### `demo-auth-off.yml`

```yaml
name: Demo - disable auth (allow anonymous)
on:
  workflow_dispatch:

permissions:
  id-token: write   # required for OIDC token issuance
  contents: read

concurrency:
  group: demo-auth-toggle
  cancel-in-progress: false

jobs:
  toggle:
    runs-on: ubuntu-latest
    steps:
      - name: Sign in to Azure (OIDC)
        uses: azure/login@v2
        with:
          client-id: ${{ secrets.AZURE_CLIENT_ID }}
          tenant-id: ${{ secrets.AZURE_TENANT_ID }}
          subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}

      - name: Set unauthenticated-client-action to AllowAnonymous
        run: |
          az containerapp auth update \
            --name workforce-mapper \
            --resource-group rg-workforce-mapper \
            --unauthenticated-client-action AllowAnonymous

      - name: Verify
        run: |
          state=$(az containerapp auth show \
            --name workforce-mapper \
            --resource-group rg-workforce-mapper \
            --query "globalValidation.unauthenticatedClientAction" -o tsv)
          echo "unauthenticatedClientAction = $state"
          test "$state" = "AllowAnonymous"
```

### `demo-auth-on.yml`

Identical structure. Differences:
- `name:` -> `Demo - enable auth (require sign-in)`
- The toggle step uses `--unauthenticated-client-action RedirectToLoginPage`
- The verify step asserts `state = RedirectToLoginPage`

The shared `concurrency.group: demo-auth-toggle` means a stray double-tap (or running "on" before "off" finished) serialises instead of racing.

If the resource names (`workforce-mapper`, `rg-workforce-mapper`) ever change, both workflow files need updating. The names match the defaults in `scripts/setup_auth.ps1` and `scripts/deploy.ps1`; if those scripts ever start reading non-default `APP_NAME` / `RESOURCE_GROUP` from `.env` for a different environment, these workflows would need parameterising too. Out of scope today — single-environment deploy.

## Documentation

Add a short "Demo auth toggle" section to README.md (or create one if it does not exist) with:
- One sentence on what the toggle is for.
- The two workflow names and where to find them (Actions tab).
- A reminder that "off" is `AllowAnonymous`, not full disable — Easy Auth and the Entra app registration remain configured.

Do not add this to `CONTEXT.md` (operational mechanism, not a domain concept) or `CLAUDE.md` (not a code convention agents need to know).

## Acceptance criteria

- [ ] `scripts/setup_demo_auth_toggle.ps1` exists, is idempotent (second run = no-op messages, no duplicate role assignments / federated credentials), ASCII-only.
- [ ] Running the setup script with a clean Azure state creates: one Entra app registration, one service principal, one custom role definition scoped to the Container App, one role assignment, one federated credential.
- [ ] Setup script prints the three GitHub Secret values plus copy-paste `gh secret set` commands at the end.
- [ ] `.github/workflows/demo-auth-off.yml` and `.github/workflows/demo-auth-on.yml` exist, each as a single-job workflow_dispatch with the OIDC sign-in + az call + verify pattern.
- [ ] Both workflows share `concurrency.group: demo-auth-toggle`.
- [ ] After setup, manually triggering "Demo - disable auth" from GitHub Actions UI succeeds, the verify step passes, and the deployed app loads in an incognito browser without an Entra prompt.
- [ ] Manually triggering "Demo - enable auth" succeeds, the verify step passes, and the deployed app redirects unauthenticated requests to Microsoft sign-in.
- [ ] README updated (or created) with the brief "Demo auth toggle" section described above.
- [ ] Branch committed and merged to main (workflows must be on main for OIDC subject `repo:OWNER/REPO:ref:refs/heads/main` to match).

## Verification / smoke test

After setup script runs and secrets are set:
1. Open the deployed app in incognito browser — confirm currently requires Entra sign-in.
2. Trigger `Demo - disable auth` workflow from the Actions tab. Wait for green check.
3. Open the deployed app in a fresh incognito browser — confirm loads without sign-in prompt.
4. Trigger `Demo - enable auth` workflow. Wait for green check.
5. Open the deployed app in another fresh incognito browser — confirm requires sign-in again.

## Cleanup (if reversing this work later)

- `gh workflow delete demo-auth-off.yml` and `demo-auth-on.yml`
- `az ad app delete --id <ClientId>` (removes app registration, SP, federated credential together)
- `az role definition delete --name "Container App Auth Toggle - workforce-mapper" --scope <AppResourceId>`
- `gh secret delete AZURE_CLIENT_ID` / `AZURE_TENANT_ID` / `AZURE_SUBSCRIPTION_ID`

## Close-out

When the implementing agent closes the issue, rename this file from `demo-auth-toggle.md` to `issue-N-demo-auth-toggle.md` to match the closed-spec convention referenced in `CLAUDE.md` ("Closed-issue specs in `docs/build/issue-N-*.md`").
