[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot

$DotEnvPath = Join-Path $RepoRoot '.env'
if (Test-Path $DotEnvPath) {
    Get-Content $DotEnvPath | Where-Object { $_ -match '^\s*([^#][^=]+)=(.*)$' } | ForEach-Object {
        $name, $value = ($_ -split '=', 2).Trim()
        if (-not [System.Environment]::GetEnvironmentVariable($name)) {
            [System.Environment]::SetEnvironmentVariable($name, $value)
        }
    }
}

function Assert-AzSuccess([string]$Op) {
    if ($LASTEXITCODE -ne 0) { throw "az $Op failed (exit $LASTEXITCODE)" }
}

$AppName          = if ($env:APP_NAME)       { $env:APP_NAME }       else { 'workforce-mapper' }
$ResourceGroup    = if ($env:RESOURCE_GROUP) { $env:RESOURCE_GROUP } else { 'rg-workforce-mapper' }
$Location         = if ($env:LOCATION)       { $env:LOCATION }       else { 'uksouth' }
$AnthropicApiKey  = if ($env:ANTHROPIC_API_KEY) { $env:ANTHROPIC_API_KEY } else { throw 'ANTHROPIC_API_KEY env var is required' }
$EnvName          = "$AppName-env"

# Deterministic ACR name: alphanumeric only, max 50 chars
$AcrName   = ($AppName -replace '[^a-zA-Z0-9]', '') + 'acr'
$AcrServer = "$AcrName.azurecr.io"

Write-Host "Resource group : $ResourceGroup"
Write-Host "ACR            : $AcrServer"
Write-Host "ContainerApp   : $AppName"

# -- 1. Resource group + ACR --------------------------------------------------
az group create --name $ResourceGroup --location $Location --output none
Assert-AzSuccess 'group create'

az acr create `
    --name $AcrName `
    --resource-group $ResourceGroup `
    --sku Basic `
    --admin-enabled true `
    --output none
Assert-AzSuccess 'acr create'

# -- 2. Build image in ACR (cloud build — no local Docker needed) --------------
$Tag   = (Get-Date -Format 'yyyyMMddHHmmss')
$Image = "$AcrServer/${AppName}:$Tag"

# Stage context into a temp directory so az acr build doesn't traverse locked
# VS/IDE files (.vs, .idea) before it can apply .dockerignore exclusions.
$BuildContext = Join-Path $env:TEMP "wm-acr-$(Get-Date -Format 'yyyyMMddHHmmss')"
Write-Host "Staging build context -> $BuildContext"
robocopy $RepoRoot $BuildContext /E /XD .vs .git .idea /NFL /NDL /NJH /NJS | Out-Null

try {
    Write-Host "Building image $Image ..."
    az acr build `
        --registry $AcrName `
        --resource-group $ResourceGroup `
        --image "${AppName}:$Tag" `
        $BuildContext
    Assert-AzSuccess 'acr build'
} finally {
    Remove-Item -Recurse -Force $BuildContext -ErrorAction SilentlyContinue
}

# -- 3. Get ACR admin credentials ---------------------------------------------
$AcrCreds = az acr credential show --name $AcrName | ConvertFrom-Json
$AcrUser  = $AcrCreds.username
$AcrPass  = $AcrCreds.passwords[0].value

# -- 4. Ensure Container App Environment exists -------------------------------
$EnvCount = az containerapp env list --resource-group $ResourceGroup `
    --query "[?name=='$EnvName'] | length(@)" -o tsv
if ($EnvCount -eq 0) {
    Write-Host "Creating environment $EnvName ..."
    az containerapp env create `
        --name $EnvName `
        --resource-group $ResourceGroup `
        --location $Location `
        --output none
}

# -- 5. Create or update ContainerApp -----------------------------------------
$AppCount = az containerapp list --resource-group $ResourceGroup `
    --query "[?name=='$AppName'] | length(@)" -o tsv
if ($AppCount -gt 0) {
    Write-Host "Updating ContainerApp $AppName ..."
    az containerapp update `
        --name $AppName `
        --resource-group $ResourceGroup `
        --image $Image `
        --min-replicas 1 `
        --max-replicas 1 `
        --output none
} else {
    Write-Host "Creating ContainerApp $AppName ..."
    az containerapp create `
        --name $AppName `
        --resource-group $ResourceGroup `
        --environment $EnvName `
        --image $Image `
        --registry-server $AcrServer `
        --registry-username $AcrUser `
        --registry-password $AcrPass `
        --target-port 8080 `
        --ingress external `
        --min-replicas 1 `
        --max-replicas 1 `
        --output none
}

# -- 6. Secrets + env vars ----------------------------------------------------
az containerapp secret set `
    --name $AppName `
    --resource-group $ResourceGroup `
    --secrets "anthropic-api-key=$AnthropicApiKey"

az containerapp update `
    --name $AppName `
    --resource-group $ResourceGroup `
    --set-env-vars "ANTHROPIC_API_KEY=secretref:anthropic-api-key" `
    --output none

Write-Host ""
$Fqdn = az containerapp show --name $AppName --resource-group $ResourceGroup --query "properties.configuration.ingress.fqdn" -o tsv
Write-Host "Deployed: https://$Fqdn"
