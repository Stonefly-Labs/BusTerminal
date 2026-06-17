#!/usr/bin/env pwsh
# Spec 009 / T010 — local-dev seeding helper for the entity discovery slice.
# PowerShell sibling of `scripts/seed-discovery-emulator.sh`. See the bash
# version for full design notes — this script mirrors its behavior on
# Windows-first dev workflows (PowerShell is the primary dev shell per
# CLAUDE.md).
#
# Usage:
#   ./scripts/seed-discovery-emulator.ps1
#
# Exit codes match the bash version: 0 success, 1 emulator unreachable,
# 2 pre-flight error.

#requires -Version 7.4
[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

$EmulatorReadinessUrl = "http://localhost:8080/ready"

Write-Host "[seed-discovery] Verifying Cosmos emulator readiness at $EmulatorReadinessUrl"

# 30s tolerant window — the emulator's first warm-up can take 60s+, callers
# should `docker compose up cosmos-emulator -d` ahead of this script.
$deadline = (Get-Date).AddSeconds(30)
$reachable = $false
while ((Get-Date) -lt $deadline) {
    try {
        $response = Invoke-WebRequest -Uri $EmulatorReadinessUrl -Method Get -TimeoutSec 3 -ErrorAction Stop
        if ($response.StatusCode -eq 200) {
            $reachable = $true
            break
        }
    } catch {
        Start-Sleep -Seconds 2
    }
}

if (-not $reachable) {
    Write-Error "[seed-discovery] Cosmos emulator did not become ready in 30s. Start it first: docker compose up cosmos-emulator -d"
    exit 1
}

Write-Host "[seed-discovery] Cosmos emulator is ready."
Write-Host ""
Write-Host "[seed-discovery] Containers required by spec 009 (created lazily by"
Write-Host "                 the API/Indexer on first startup once Phase 2 T025"
Write-Host "                 wires it):"
Write-Host "                   - discovery-runs   (PK /namespaceId)"
Write-Host "                   - discovery-locks  (PK /namespaceId)"
Write-Host ""
Write-Host "[seed-discovery] Limitations:"
Write-Host "  * The docker-compose stack does NOT run a Service Bus emulator."
Write-Host "    Phase 3 (US1) integration tests use a recorded ARM HTTP fixture"
Write-Host "    for ARM traffic and unit-test the queue publish path. End-to-end"
Write-Host "    US1 verification (queue <-> worker) requires the dev Azure"
Write-Host "    environment."
Write-Host "  * The Cosmos emulator's RBAC surface is not GA; the dev fixture"
Write-Host "    therefore uses the emulator's well-known master key (not a"
Write-Host "    secret -- published in MS Learn). Production code paths always"
Write-Host "    use Managed Identity / DefaultAzureCredential."

exit 0
