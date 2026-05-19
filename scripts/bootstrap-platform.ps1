#!/usr/bin/env pwsh
<#
.SYNOPSIS
Bootstrap the BusTerminal OpenTofu state backend + per-environment pipeline
identities by running the `iac/platform-bootstrap/` module.

.DESCRIPTION
One-time per-subscription operation. Re-run with additional environments in
-Environments to add more pipeline identities later.

Requires: OpenTofu ≥ 1.10, Azure CLI ≥ 2.60.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $SubscriptionId,
    [Parameter(Mandatory)] [string] $GithubOrgRepo,
    [Parameter(Mandatory)] [string] $TfstateStorageAccountName,
    [string[]] $Environments = @('dev'),
    [string] $Location = 'eastus2'
)

$ErrorActionPreference = 'Stop'

function Test-Command {
    param([string] $Name)
    $null -ne (Get-Command $Name -ErrorAction SilentlyContinue)
}

if (-not (Test-Command 'tofu')) {
    Write-Error "tofu not found on PATH. Install OpenTofu >= 1.10 first."
}
if (-not (Test-Command 'az')) {
    Write-Error "az not found on PATH. Install Azure CLI >= 2.60 first."
}

Write-Host "Verifying Azure CLI session..."
$acct = az account show --subscription $SubscriptionId 2>$null | ConvertFrom-Json
if (-not $acct) {
    Write-Host "Not logged in to subscription $SubscriptionId. Running 'az login'..."
    az login | Out-Null
    az account set --subscription $SubscriptionId
} else {
    Write-Host "Using existing Azure CLI session: $($acct.user.name)"
}

$envsJson = ($Environments | ConvertTo-Json -Compress)
if ($Environments.Count -eq 1) {
    # ConvertTo-Json renders single-element arrays as a scalar; force the array shape.
    $envsJson = "[`"$($Environments[0])`"]"
}

$bootstrapDir = Join-Path $PSScriptRoot "..\iac\platform-bootstrap"
Push-Location $bootstrapDir
try {
    Write-Host "Initializing platform-bootstrap module (local state)..."
    tofu init -backend=false

    $commonArgs = @(
        "-var", "subscription_id=$SubscriptionId",
        "-var", "github_org_repo=$GithubOrgRepo",
        "-var", "tfstate_storage_account_name=$TfstateStorageAccountName",
        "-var", "environments=$envsJson",
        "-var", "location=$Location"
    )

    Write-Host "Planning..."
    tofu plan @commonArgs

    $confirm = Read-Host "Apply this plan? [y/N]"
    if ($confirm -notmatch '^(y|Y|yes|YES)$') {
        Write-Host "Aborted."
        exit 1
    }

    tofu apply -auto-approve @commonArgs

    Write-Host ""
    Write-Host "Bootstrap complete. Map the outputs below to GitHub repository / environment variables:"
    Write-Host ""
    tofu output -json | ConvertFrom-Json | ForEach-Object {
        $_.PSObject.Properties | ForEach-Object {
            "  $($_.Name) = $($_.Value.value | ConvertTo-Json -Compress)"
        }
    }
    Write-Host ""
    Write-Host "See iac/platform-bootstrap/README.md for the GitHub variable mapping table."
}
finally {
    Pop-Location
}
