#!/usr/bin/env bash
# Starts the BusTerminal backend (`dotnet watch`) and frontend (`pnpm dev`)
# concurrently. Interleaved structured logs are written to the current terminal.
# Ctrl-C stops both processes cleanly.
set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
API_DIR="$REPO_ROOT/api/BusTerminal.Api"
WEB_DIR="$REPO_ROOT/web"

# BusTerminal dev tenant id. Local backends that reach real Azure services
# (anything with AZURE_KEY_VAULT_URI set, etc.) authenticate via az-login in
# this tenant — see docs/local-development.md.
DEV_TENANT_ID="596c1564-6e95-4c35-a80b-2dbe45a162f3"

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

# Verify Azure CLI sign-in. Hard-fail when the backend will reach a real Key
# Vault (AZURE_KEY_VAULT_URI set) and az is not signed in to the dev tenant.
# Otherwise advisory — local mock-tenant dev does not need az.
require_az_login=false
if [ -n "${AZURE_KEY_VAULT_URI:-}" ]; then
    require_az_login=true
fi

if command -v az >/dev/null 2>&1; then
    current_tenant="$(az account show --query tenantId -o tsv 2>/dev/null || true)"
    if [ -z "$current_tenant" ]; then
        echo "[start-local] WARN: 'az account show' failed — you are not signed in." >&2
        echo "[start-local]       Run: az login --tenant $DEV_TENANT_ID" >&2
        if [ "$require_az_login" = true ]; then
            echo "[start-local] ERROR: AZURE_KEY_VAULT_URI is set; aborting." >&2
            exit 1
        fi
        echo "[start-local]       Continuing — no Azure dependencies are configured for this run."
    elif [ "$current_tenant" != "$DEV_TENANT_ID" ]; then
        echo "[start-local] WARN: Azure CLI is signed in to tenant '$current_tenant'" >&2
        echo "[start-local]       (expected '$DEV_TENANT_ID' for BusTerminal dev)." >&2
        echo "[start-local]       Run: az login --tenant $DEV_TENANT_ID" >&2
        if [ "$require_az_login" = true ]; then
            echo "[start-local] ERROR: AZURE_KEY_VAULT_URI is set; aborting." >&2
            exit 1
        fi
    else
        echo "[start-local] Azure CLI signed in to the BusTerminal dev tenant ($DEV_TENANT_ID)."
    fi
else
    if [ "$require_az_login" = true ]; then
        echo "[start-local] ERROR: 'az' CLI not found, but AZURE_KEY_VAULT_URI is set." >&2
        echo "[start-local]        Install: https://learn.microsoft.com/cli/azure/install-azure-cli" >&2
        exit 1
    fi
    echo "[start-local] 'az' CLI not found — skipping sign-in check."
fi

export ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Development}"
export ASPNETCORE_URLS="${ASPNETCORE_URLS:-http://localhost:5000}"
export NEXT_PUBLIC_API_BASE_URL="${NEXT_PUBLIC_API_BASE_URL:-http://localhost:5000}"

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
