[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$AppName       = if ($env:APP_NAME)       { $env:APP_NAME }       else { 'workforce-mapper' }
$ResourceGroup = if ($env:RESOURCE_GROUP) { $env:RESOURCE_GROUP } else { 'rg-workforce-mapper' }

function Assert-AzSuccess {
    param([string]$Step)
    if ($LASTEXITCODE -ne 0) {
        Write-Error "az command failed at step: $Step (exit $LASTEXITCODE)"
        exit 1
    }
}

# -- 1. Resolve tenant, subscription, and Container App resource ID -----------
$AccountJson     = az account show | ConvertFrom-Json
Assert-AzSuccess '1. account show'
$TenantId        = $AccountJson.tenantId
$SubscriptionId  = $AccountJson.id

$AppResourceId   = az containerapp show --name $AppName --resource-group $ResourceGroup --query id -o tsv
Assert-AzSuccess '1. containerapp show'

Write-Host "Tenant         : $TenantId"
Write-Host "Subscription   : $SubscriptionId"
Write-Host "Container App  : $AppResourceId"

# -- 2. Resolve repo slug for federated credential subject --------------------
$RemoteUrl = git config --get remote.origin.url 2>$null
if ($RemoteUrl -match '[:/]([^/]+/[^/]+?)(\.git)?$') {
    $RepoSlug = $Matches[1]
} elseif ($env:GITHUB_REPO) {
    $RepoSlug = $env:GITHUB_REPO
} else {
    Write-Error 'Cannot determine repo slug. Set GITHUB_REPO env var (e.g. samww/WorkforceMapper).'
    exit 1
}
Write-Host "Repo slug      : $RepoSlug"

# -- 3. Create or reuse Entra app registration --------------------------------
$RegName  = "$AppName-gha-auth-toggle"
$Existing = az ad app list --display-name $RegName | ConvertFrom-Json
Assert-AzSuccess '3. app list'
if ($Existing.Count -gt 0) {
    $ClientId = $Existing[0].appId
    Write-Host "Reusing existing app registration '$RegName' (appId: $ClientId)"
} else {
    Write-Host "Creating app registration '$RegName' ..."
    $AppReg   = az ad app create --display-name $RegName --sign-in-audience AzureADMyOrg | ConvertFrom-Json
    Assert-AzSuccess '3. app create'
    $ClientId = $AppReg.appId
    Write-Host "Created app registration '$RegName' (appId: $ClientId)"
}

# -- 4. Create or reuse the service principal ---------------------------------
$SpList = az ad sp list --filter "appId eq '$ClientId'" | ConvertFrom-Json
Assert-AzSuccess '4. sp list'
if ($SpList.Count -gt 0) {
    $SpObjectId = $SpList[0].id
    Write-Host "Reusing existing service principal (id: $SpObjectId)"
} else {
    Write-Host "Creating service principal for appId $ClientId ..."
    $Sp         = az ad sp create --id $ClientId | ConvertFrom-Json
    Assert-AzSuccess '4. sp create'
    $SpObjectId = $Sp.id
    Write-Host "Created service principal (id: $SpObjectId)"
}

# -- 5. Create or update the custom role definition ---------------------------
$RoleName = "Container App Auth Toggle - $AppName"

$RoleJson = @"
{
  "Name": "$RoleName",
  "IsCustom": true,
  "Description": "Read and write Easy Auth config for one Container App.",
  "Actions": [
    "Microsoft.App/containerApps/read",
    "Microsoft.App/containerApps/authConfigs/read",
    "Microsoft.App/containerApps/authConfigs/write"
  ],
  "NotActions": [],
  "AssignableScopes": [
    "$AppResourceId"
  ]
}
"@

$TempFile = [System.IO.Path]::GetTempFileName() + '.json'
$RoleJson | Out-File -FilePath $TempFile -Encoding utf8

$ExistingRole = az role definition list --name $RoleName --scope $AppResourceId | ConvertFrom-Json
Assert-AzSuccess '5. role definition list'
if ($ExistingRole.Count -gt 0) {
    Write-Host "Updating existing custom role '$RoleName' ..."
    az role definition update --role-definition $TempFile | Out-Null
    Assert-AzSuccess '5. role definition update'
} else {
    Write-Host "Creating custom role '$RoleName' ..."
    az role definition create --role-definition $TempFile | Out-Null
    Assert-AzSuccess '5. role definition create'
}
Remove-Item $TempFile -ErrorAction SilentlyContinue

# -- 6. Assign the custom role to the service principal -----------------------
$AssignmentList = az role assignment list --assignee $SpObjectId --role $RoleName --scope $AppResourceId | ConvertFrom-Json
Assert-AzSuccess '6. role assignment list'
if ($AssignmentList.Count -gt 0) {
    Write-Host "Role assignment already exists for '$RoleName' on this Container App."
} else {
    Write-Host "Assigning role '$RoleName' to service principal $SpObjectId ..."
    az role assignment create `
        --assignee-object-id $SpObjectId `
        --assignee-principal-type ServicePrincipal `
        --role $RoleName `
        --scope $AppResourceId | Out-Null
    Assert-AzSuccess '6. role assignment create'
}

# -- 7. Create or update the federated credential for GitHub Actions OIDC -----
$FedCredName    = 'gha-main-branch'
$FedCredSubject = "repo:${RepoSlug}:ref:refs/heads/main"

$ExistingCreds = az ad app federated-credential list --id $ClientId | ConvertFrom-Json
Assert-AzSuccess '7. federated-credential list'
$ExistingCred  = $ExistingCreds | Where-Object { $_.name -eq $FedCredName }

if ($ExistingCred) {
    Write-Host "Updating existing federated credential '$FedCredName' ..."
    az ad app federated-credential update `
        --id $ClientId `
        --federated-credential-id $ExistingCred.id `
        --parameters "{""subject"":""$FedCredSubject"",""issuer"":""https://token.actions.githubusercontent.com"",""audiences"":[""api://AzureADTokenExchange""],""name"":""$FedCredName""}" | Out-Null
    Assert-AzSuccess '7. federated-credential update'
} else {
    Write-Host "Creating federated credential '$FedCredName' ..."
    az ad app federated-credential create `
        --id $ClientId `
        --parameters "{""subject"":""$FedCredSubject"",""issuer"":""https://token.actions.githubusercontent.com"",""audiences"":[""api://AzureADTokenExchange""],""name"":""$FedCredName""}" | Out-Null
    Assert-AzSuccess '7. federated-credential create'
}

# -- 8. Print GitHub Secrets values and copy-paste commands -------------------
Write-Host ""
Write-Host "====================================================================="
Write-Host " GitHub Secrets required for the demo-auth-toggle workflows"
Write-Host "====================================================================="
Write-Host ""
Write-Host "AZURE_CLIENT_ID       = $ClientId"
Write-Host "AZURE_TENANT_ID       = $TenantId"
Write-Host "AZURE_SUBSCRIPTION_ID = $SubscriptionId"
Write-Host ""
Write-Host "Copy-paste commands:"
Write-Host "  gh secret set AZURE_CLIENT_ID       --body $ClientId"
Write-Host "  gh secret set AZURE_TENANT_ID       --body $TenantId"
Write-Host "  gh secret set AZURE_SUBSCRIPTION_ID --body $SubscriptionId"
Write-Host ""
Write-Host "Setup complete."
