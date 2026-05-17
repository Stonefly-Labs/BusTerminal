#!/usr/bin/env pwsh
# Runs the full local test suite — frontend (Vitest), accessibility
# (Playwright a11y project — opt-in), backend (dotnet test), and IaC.
#requires -Version 7.4
[CmdletBinding()]
param()

$ErrorActionPreference = "Continue"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
Set-Location $repoRoot

$failures = @()

function Invoke-Step {
    param(
        [string]$Label,
        [scriptblock]$Action
    )
    Write-Host "`u{25B6} $Label" -ForegroundColor White
    & $Action
    if ($LASTEXITCODE -ne 0 -and $null -ne $LASTEXITCODE) {
        Write-Host "`u{2717} $Label" -ForegroundColor Red
        $script:failures += $Label
    }
    else {
        Write-Host "`u{2713} $Label" -ForegroundColor Green
    }
    Write-Host ""
}

Invoke-Step "web - vitest" { pnpm --filter web test }
Invoke-Step "web - typecheck" { pnpm --filter web typecheck }

if ($env:RUN_A11Y -eq "1") {
    Invoke-Step "web - a11y" { pnpm --filter web test:a11y }
}

Invoke-Step "api - dotnet test" { dotnet test api/BusTerminal.slnx --nologo }

$envDev = Join-Path $repoRoot "iac/environments/dev/main.tf"
if (Test-Path $envDev) {
    Invoke-Step "iac/environments/dev - tofu validate" {
        Push-Location (Join-Path $repoRoot "iac/environments/dev")
        try {
            tofu init -backend=false -input=false | Out-Null
            tofu validate
        }
        finally {
            Pop-Location
        }
    }
}
else {
    Write-Host "`u{25B6} iac/environments/dev - skipping (composition not present yet)" -ForegroundColor White
    Write-Host ""
}

if ($failures.Count -gt 0) {
    Write-Host "One or more steps failed:" -ForegroundColor Red
    foreach ($f in $failures) { Write-Host "  - $f" -ForegroundColor Red }
    exit 1
}

Write-Host "All steps passed." -ForegroundColor Green
exit 0
