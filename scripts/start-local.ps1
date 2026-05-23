#!/usr/bin/env pwsh
# Starts the BusTerminal backend (`dotnet watch`) and frontend (`pnpm dev`)
# concurrently. Ctrl-C stops both cleanly.
#requires -Version 7.4
[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$apiDir = Join-Path $repoRoot "api/BusTerminal.Api"
$webDir = Join-Path $repoRoot "web"

# BusTerminal dev tenant id. Local backends that reach real Azure services
# (anything with AZURE_KEY_VAULT_URI set, etc.) authenticate via az-login in
# this tenant — see docs/local-development.md.
$devTenantId = "596c1564-6e95-4c35-a80b-2dbe45a162f3"

if (-not (Test-Path $apiDir)) {
    Write-Error "API project not found at $apiDir"
}
if (-not (Test-Path $webDir)) {
    Write-Error "Web project not found at $webDir"
}

# Seed dev appsettings on first run.
$devSettings = Join-Path $apiDir "appsettings.Development.json"
$devExample = Join-Path $apiDir "appsettings.Development.json.example"
if (-not (Test-Path $devSettings) -and (Test-Path $devExample)) {
    Copy-Item $devExample $devSettings
    Write-Host "[start-local] Copied appsettings.Development.json from example."
}

# Verify Azure CLI sign-in. Hard-fail when the backend will reach a real Key
# Vault (AZURE_KEY_VAULT_URI set) and az is not signed in to the dev tenant.
# Otherwise advisory — local mock-tenant dev does not need az.
$requireAzLogin = -not [string]::IsNullOrWhiteSpace($env:AZURE_KEY_VAULT_URI)

if (Get-Command az -ErrorAction SilentlyContinue) {
    $currentTenant = (az account show --query tenantId -o tsv 2>$null)
    if ([string]::IsNullOrWhiteSpace($currentTenant)) {
        Write-Warning "[start-local] 'az account show' failed — you are not signed in."
        Write-Warning "[start-local]  Run: az login --tenant $devTenantId"
        if ($requireAzLogin) {
            Write-Error "[start-local] AZURE_KEY_VAULT_URI is set; aborting."
        }
        Write-Host "[start-local]  Continuing — no Azure dependencies are configured for this run."
    } elseif ($currentTenant -ne $devTenantId) {
        Write-Warning "[start-local] Azure CLI is signed in to tenant '$currentTenant'"
        Write-Warning "[start-local]  (expected '$devTenantId' for BusTerminal dev)."
        Write-Warning "[start-local]  Run: az login --tenant $devTenantId"
        if ($requireAzLogin) {
            Write-Error "[start-local] AZURE_KEY_VAULT_URI is set; aborting."
        }
    } else {
        Write-Host "[start-local] Azure CLI signed in to the BusTerminal dev tenant ($devTenantId)."
    }
} else {
    if ($requireAzLogin) {
        Write-Error "[start-local] 'az' CLI not found, but AZURE_KEY_VAULT_URI is set. Install: https://learn.microsoft.com/cli/azure/install-azure-cli"
    }
    Write-Host "[start-local] 'az' CLI not found — skipping sign-in check."
}

$env:ASPNETCORE_ENVIRONMENT = $env:ASPNETCORE_ENVIRONMENT ?? "Development"
$env:ASPNETCORE_URLS = $env:ASPNETCORE_URLS ?? "http://localhost:5000"
$env:NEXT_PUBLIC_API_BASE_URL = $env:NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5000"

Write-Host "[start-local] Backend  -> $($env:ASPNETCORE_URLS)"
Write-Host "[start-local] Frontend -> http://localhost:3000"

$apiJob = Start-Job -Name busterminal-api -ScriptBlock {
    Set-Location $using:apiDir
    dotnet watch run --no-launch-profile 2>&1 | ForEach-Object { "[api] $_" }
}
$webJob = Start-Job -Name busterminal-web -ScriptBlock {
    Set-Location $using:webDir
    pnpm dev 2>&1 | ForEach-Object { "[web] $_" }
}

try {
    while ($apiJob.State -eq "Running" -or $webJob.State -eq "Running") {
        Receive-Job -Job $apiJob -Keep:$false
        Receive-Job -Job $webJob -Keep:$false
        Start-Sleep -Milliseconds 500
    }
}
finally {
    foreach ($job in @($apiJob, $webJob)) {
        if ($job.State -eq "Running") {
            Stop-Job -Job $job -PassThru | Out-Null
        }
        Receive-Job -Job $job -Keep:$false
        Remove-Job -Job $job -Force | Out-Null
    }
}
