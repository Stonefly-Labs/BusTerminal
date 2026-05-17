#!/usr/bin/env bash
# Builds and runs the BusTerminal stack via docker compose.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

exec docker compose up --build "$@"
