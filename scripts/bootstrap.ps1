#!/usr/bin/env pwsh
# BusTerminal prerequisite verification (no installs).
# Exits 0 when every prerequisite is present at or above the minimum;
# exits 1 with an actionable summary when one or more are missing.

#Requires -Version 7.4

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$req = @{
    DotNetMajor = 10
    NodeMajor   = 22
    PnpmMajor   = 10
    TofuMajor   = 1
    TofuMinor   = 10
    AzMajor     = 2
    AzMinor     = 60
    PwshMajor   = 7
    PwshMinor   = 4
}

$gaps = New-Object System.Collections.Generic.List[string]
$ok   = New-Object System.Collections.Generic.List[string]

function Test-CommandExists {
    param([string]$Name)
    return [bool](Get-Command -Name $Name -ErrorAction SilentlyContinue)
}

function Compare-VersionAtLeast {
    param([string]$Version, [int]$ReqMajor, [int]$ReqMinor = 0)
    if ([string]::IsNullOrWhiteSpace($Version)) { return $false }
    $parts = $Version.Trim().TrimStart('v') -split '[.\-+]'
    $major = 0; $minor = 0
    [int]::TryParse($parts[0], [ref]$major) | Out-Null
    if ($parts.Length -gt 1) { [int]::TryParse($parts[1], [ref]$minor) | Out-Null }
    if ($major -gt $ReqMajor) { return $true }
    if ($major -eq $ReqMajor -and $minor -ge $ReqMinor) { return $true }
    return $false
}

Write-Host "BusTerminal prerequisite check" -ForegroundColor White
Write-Host ""

# .NET 10
if (Test-CommandExists 'dotnet') {
    $v = (dotnet --version 2>$null)
    if (Compare-VersionAtLeast -Version $v -ReqMajor $req.DotNetMajor) {
        $ok.Add(".NET SDK $v")
    } else {
        $gaps.Add(".NET SDK $($req.DotNetMajor)+ required (found '$v'). Install via https://dotnet.microsoft.com/download")
    }
} else {
    $gaps.Add(".NET SDK $($req.DotNetMajor)+ not found. Install via https://dotnet.microsoft.com/download")
}

# Node.js 22
if (Test-CommandExists 'node') {
    $v = (node --version 2>$null)
    if (Compare-VersionAtLeast -Version $v -ReqMajor $req.NodeMajor) {
        $ok.Add("Node.js $v")
    } else {
        $gaps.Add("Node.js $($req.NodeMajor) LTS required (found '$v'). Install via https://nodejs.org or 'nvm install $($req.NodeMajor)'")
    }
} else {
    $gaps.Add("Node.js $($req.NodeMajor) LTS not found. Install via https://nodejs.org or 'nvm install $($req.NodeMajor)'")
}

# pnpm
if (Test-CommandExists 'pnpm') {
    $v = (pnpm --version 2>$null)
    if (Compare-VersionAtLeast -Version $v -ReqMajor $req.PnpmMajor) {
        $ok.Add("pnpm $v")
    } else {
        $gaps.Add("pnpm $($req.PnpmMajor)+ recommended (found '$v'). Install: 'corepack enable pnpm' or 'npm i -g pnpm'")
    }
} else {
    $gaps.Add("pnpm not found. Install: 'corepack enable pnpm' or 'npm i -g pnpm'")
}

# OpenTofu
if (Test-CommandExists 'tofu') {
    $first = (tofu version 2>$null | Select-Object -First 1)
    $token = if ($first) { ($first -split '\s+')[1] } else { '' }
    if (Compare-VersionAtLeast -Version $token -ReqMajor $req.TofuMajor -ReqMinor $req.TofuMinor) {
        $ok.Add("OpenTofu $token")
    } else {
        $gaps.Add("OpenTofu $($req.TofuMajor).$($req.TofuMinor)+ required (found '$token'). Install via https://opentofu.org/docs/intro/install/")
    }
} else {
    $gaps.Add("OpenTofu $($req.TofuMajor).$($req.TofuMinor)+ not found. Install via https://opentofu.org/docs/intro/install/")
}

# Azure CLI
if (Test-CommandExists 'az') {
    $azver = (az version --output tsv --query '\"azure-cli\"' 2>$null)
    if (Compare-VersionAtLeast -Version $azver -ReqMajor $req.AzMajor -ReqMinor $req.AzMinor) {
        $ok.Add("Azure CLI $azver")
    } else {
        $gaps.Add("Azure CLI $($req.AzMajor).$($req.AzMinor)+ required (found '$azver'). Install via https://aka.ms/InstallAzureCLI")
    }
} else {
    $gaps.Add("Azure CLI $($req.AzMajor).$($req.AzMinor)+ not found. Install via https://aka.ms/InstallAzureCLI")
}

# Docker
if (Test-CommandExists 'docker') {
    $null = & docker info 2>&1
    if ($LASTEXITCODE -eq 0) {
        $v = (docker version --format '{{.Client.Version}}' 2>$null)
        $ok.Add("Docker $v (daemon reachable)")
    } else {
        $gaps.Add("Docker is installed but daemon is not reachable. Start Docker Desktop or the docker service.")
    }
} else {
    $gaps.Add("Docker not found. Install Docker Desktop (https://docs.docker.com/get-docker/) or Docker Engine.")
}

# PowerShell 7 (running this script confirms it; report version)
$pwshVer = $PSVersionTable.PSVersion
if ($pwshVer.Major -gt $req.PwshMajor -or ($pwshVer.Major -eq $req.PwshMajor -and $pwshVer.Minor -ge $req.PwshMinor)) {
    $ok.Add("PowerShell $pwshVer")
} else {
    $gaps.Add("PowerShell $($req.PwshMajor).$($req.PwshMinor)+ required (found '$pwshVer'). Install via https://aka.ms/PowerShell")
}

foreach ($entry in $ok) {
    Write-Host "  ok  $entry" -ForegroundColor Green
}

if ($gaps.Count -gt 0) {
    Write-Host ""
    Write-Host "Missing prerequisites:" -ForegroundColor Red
    foreach ($entry in $gaps) {
        Write-Host "  - $entry" -ForegroundColor Yellow
    }
    Write-Host ""
    Write-Host "Resolve the gaps above, then re-run scripts/bootstrap.ps1." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "All prerequisites satisfied." -ForegroundColor Green
exit 0
