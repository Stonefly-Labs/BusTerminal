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

$env:ASPNETCORE_ENVIRONMENT = $env:ASPNETCORE_ENVIRONMENT ?? "Development"
$env:ASPNETCORE_URLS = $env:ASPNETCORE_URLS ?? "http://localhost:5000"
$env:NEXT_PUBLIC_API_BASE_URL = $env:NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5000"
$env:AZURE_AD_TENANT_ID = $env:AZURE_AD_TENANT_ID ?? "development"
$env:AZURE_AD_CLIENT_ID = $env:AZURE_AD_CLIENT_ID ?? "mock-client"
$env:AZURE_AD_CLIENT_SECRET = $env:AZURE_AD_CLIENT_SECRET ?? "mock-secret"
$env:NEXTAUTH_SECRET = $env:NEXTAUTH_SECRET ?? "dev-secret-do-not-use-in-prod"
$env:AUTH_SECRET = $env:AUTH_SECRET ?? $env:NEXTAUTH_SECRET

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
