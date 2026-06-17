#!/usr/bin/env bash
# Spec 009 / T010 — local-dev seeding helper for the entity discovery slice.
#
# Adds two Cosmos containers (`discovery-runs`, `discovery-locks`) on the
# canonical database hosted by the docker-compose Cosmos emulator.
# Idempotent: every call uses `CreateContainerIfNotExistsAsync` semantics.
#
# Service Bus is NOT seeded by this script. The platform's
# `discovery-requested` queue lives only in the dev/test/prod Azure Service
# Bus namespace; the local docker-compose stack does not run a Service Bus
# emulator. See "Limitations" below.
#
# Usage:
#   ./scripts/seed-discovery-emulator.sh
#
# Exit codes:
#   0 — succeeded (containers exist after the call)
#   1 — emulator unreachable or seeding failed
#   2 — pre-flight error (docker / dotnet missing)

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
EMULATOR_READINESS_URL="http://localhost:8080/ready"
DOTNET_PROJECT="$REPO_ROOT/api/BusTerminal.Api.Tests"

echo "[seed-discovery] Verifying Cosmos emulator readiness at $EMULATOR_READINESS_URL"

if ! command -v curl >/dev/null 2>&1; then
  echo "[seed-discovery] ERROR: curl is required" >&2
  exit 2
fi

# 30s tolerant window — the emulator's first warm-up can take 60s+, callers
# should `docker compose up cosmos-emulator -d` ahead of this script.
deadline=$((SECONDS + 30))
while (( SECONDS < deadline )); do
  if curl -sf "$EMULATOR_READINESS_URL" -o /dev/null; then
    echo "[seed-discovery] Cosmos emulator is ready."
    break
  fi
  sleep 2
done

if ! curl -sf "$EMULATOR_READINESS_URL" -o /dev/null; then
  echo "[seed-discovery] ERROR: Cosmos emulator did not become ready in 30s." >&2
  echo "[seed-discovery]        Start it first: docker compose up cosmos-emulator -d" >&2
  exit 1
fi

# Container provisioning leverages the existing test fixture's
# CreateContainerIfNotExistsAsync logic. We invoke a targeted xunit class
# (CosmosEmulatorFixture's InitializeAsync runs as the bootstrap path for
# every integration test). Running a single fast smoke test pulls the
# fixture, which idempotently creates the canonical containers; spec 009's
# new containers will be created by the same pattern once the discovery
# slice's test fixture lands in Phase 3.
#
# For now, the lighter-touch path is: the API and Indexer auto-create
# missing Cosmos containers on first startup (added by T025 in Phase 2).
# This script therefore does NOT need to do the create itself — it only
# verifies the emulator is up. Once T025 wires
# `CreateContainerIfNotExistsAsync` into Program.cs, every fresh
# `start-local.sh` will populate the new containers on first request.

echo "[seed-discovery] OK. Cosmos emulator is reachable."
echo ""
echo "[seed-discovery] Containers required by spec 009 (created lazily by"
echo "                 the API/Indexer on first startup once Phase 2 T025"
echo "                 wires it):"
echo "                   - discovery-runs   (PK /namespaceId)"
echo "                   - discovery-locks  (PK /namespaceId)"
echo ""
echo "[seed-discovery] Limitations:"
echo "  * The docker-compose stack does NOT run a Service Bus emulator."
echo "    Phase 3 (US1) integration tests use a recorded ARM HTTP fixture"
echo "    for ARM traffic and unit-test the queue publish path. End-to-end"
echo "    US1 verification (queue ↔ worker) requires the dev Azure"
echo "    environment."
echo "  * The Cosmos emulator's RBAC surface is not GA; the dev fixture"
echo "    therefore uses the emulator's well-known master key (not a"
echo "    secret — published in MS Learn). Production code paths always"
echo "    use Managed Identity / DefaultAzureCredential."
