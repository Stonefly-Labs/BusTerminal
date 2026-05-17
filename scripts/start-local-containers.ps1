#!/usr/bin/env pwsh
# Builds and runs the BusTerminal stack via docker compose.
#requires -Version 7.4
[CmdletBinding()]
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$Forwarded
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
Set-Location $repoRoot

docker compose up --build @Forwarded
