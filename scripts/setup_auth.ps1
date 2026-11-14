[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$AppName       = if ($env:APP_NAME)       { $env:APP_NAME }       else { 'workforce-mapper' }
$ResourceGroup = if ($env:RESOURCE_GROUP) { $env:RESOURCE_GROUP } else { 'rg-workforce-mapper' }

# -- 1. Resolve tenant and app FQDN ------------------------------------------
$TenantId = az account show --query tenantId -o tsv
$Fqdn     = az containerapp show --name $AppName --resource-group $ResourceGroup --query "properties.configuration.ingress.fqdn" -o tsv
$AppUrl   = "https://$Fqdn"

Write-Host "Tenant   : $TenantId"
Write-Host "App URL  : $AppUrl"

# -- 2. Create or reuse Entra app registration --------------------------------
$RegName  = "$AppName-auth"
$ReplyUri = "$AppUrl/.auth/login/aad/callback"

$Existing = az ad app list --display-name $RegName | ConvertFrom-Json
if ($Existing.Count -gt 0) {
    $ClientId = $Existing[0].appId
    Write-Host "Reusing existing app registration '$RegName' (appId: $ClientId)"

    # Add the current ReplyUri, keeping every redirect URI already registered
    # (e.g. a custom-domain callback): --web-redirect-uris replaces the whole list.
    # Runs before the secret reset, so a failed read stops the script with nothing changed.
    $RedirectUris = @(az ad app show --id $ClientId --query "web.redirectUris" -o tsv)
    if ($LASTEXITCODE -ne 0) { throw "Could not read redirect URIs for $ClientId" }
    if ($RedirectUris -cnotcontains $ReplyUri) {
        az ad app update --id $ClientId --web-redirect-uris ($RedirectUris + $ReplyUri)
        if ($LASTEXITCODE -ne 0) { throw "Could not update redirect URIs for $ClientId" }
    }

    # Always reset and capture a fresh secret
    $SecretObj    = az ad app credential reset --id $ClientId | ConvertFrom-Json
    $ClientSecret = $SecretObj.password
} else {
    Write-Host "Creating app registration '$RegName' ..."
    $AppReg   = az ad app create --display-name $RegName --sign-in-audience AzureADMyOrg --web-redirect-uris $ReplyUri | ConvertFrom-Json
    $ClientId = $AppReg.appId

    # Enable ID tokens - required for Easy Auth browser redirect flow
    az ad app update --id $ClientId --enable-id-token-issuance true

    # -- 3. Create a client secret (new registration only) --------------------
    $SecretObj    = az ad app credential reset --id $ClientId | ConvertFrom-Json
    $ClientSecret = $SecretObj.password
}

Write-Host "App registration client ID: $ClientId"

if (-not $ClientSecret) {
    Write-Host "WARNING: Reusing existing registration - client secret not rotated."
    Write-Host "To rotate, run: az ad app credential reset --id $ClientId"
    exit 0
}

# -- 4. Wire the Microsoft provider into Container Apps Easy Auth -------------
az containerapp auth microsoft update `
    --name $AppName `
    --resource-group $ResourceGroup `
    --client-id $ClientId `
    --client-secret $ClientSecret `
    --issuer "https://login.microsoftonline.com/$TenantId/v2.0" `
    --yes

# -- 5. Enable authentication, redirect unauthenticated requests --------------
az containerapp auth update `
    --name $AppName `
    --resource-group $ResourceGroup `
    --enabled true `
    --unauthenticated-client-action RedirectToLoginPage

Write-Host ""
Write-Host "Easy Auth enabled. Login URL: $AppUrl"
