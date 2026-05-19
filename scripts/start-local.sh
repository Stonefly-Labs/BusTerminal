#!/usr/bin/env bash
# Starts the BusTerminal backend (`dotnet watch`) and frontend (`pnpm dev`)
# concurrently. Interleaved structured logs are written to the current terminal.
# Ctrl-C stops both processes cleanly.
set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
API_DIR="$REPO_ROOT/api/BusTerminal.Api"
WEB_DIR="$REPO_ROOT/web"

if [ ! -d "$API_DIR" ]; then
    echo "ERROR: $API_DIR not found." >&2
    exit 1
fi
if [ ! -d "$WEB_DIR" ]; then
    echo "ERROR: $WEB_DIR not found." >&2
    exit 1
fi

cleanup() {
    if [ -n "${API_PID:-}" ] && kill -0 "$API_PID" 2>/dev/null; then
        kill -TERM "$API_PID" 2>/dev/null || true
    fi
    if [ -n "${WEB_PID:-}" ] && kill -0 "$WEB_PID" 2>/dev/null; then
        kill -TERM "$WEB_PID" 2>/dev/null || true
    fi
    wait 2>/dev/null || true
}
trap cleanup INT TERM EXIT

# Ensure dev appsettings exists; copy from the example when missing.
if [ ! -f "$API_DIR/appsettings.Development.json" ]; then
    if [ -f "$API_DIR/appsettings.Development.json.example" ]; then
        cp "$API_DIR/appsettings.Development.json.example" "$API_DIR/appsettings.Development.json"
        echo "[start-local] Copied appsettings.Development.json from example."
    fi
fi

export ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Development}"
export ASPNETCORE_URLS="${ASPNETCORE_URLS:-http://localhost:5000}"
export NEXT_PUBLIC_API_BASE_URL="${NEXT_PUBLIC_API_BASE_URL:-http://localhost:5000}"
export AZURE_AD_TENANT_ID="${AZURE_AD_TENANT_ID:-development}"
export AZURE_AD_CLIENT_ID="${AZURE_AD_CLIENT_ID:-mock-client}"
export AZURE_AD_CLIENT_SECRET="${AZURE_AD_CLIENT_SECRET:-mock-secret}"
export NEXTAUTH_SECRET="${NEXTAUTH_SECRET:-dev-secret-do-not-use-in-prod}"
export AUTH_SECRET="${AUTH_SECRET:-$NEXTAUTH_SECRET}"

echo "[start-local] Backend → $ASPNETCORE_URLS"
echo "[start-local] Frontend → http://localhost:3000"

(
    cd "$API_DIR"
    dotnet watch run --no-launch-profile 2>&1 | sed -u 's/^/[api] /'
) &
API_PID=$!

(
    cd "$WEB_DIR"
    pnpm dev 2>&1 | sed -u 's/^/[web] /'
) &
WEB_PID=$!

wait "$API_PID" "$WEB_PID"
