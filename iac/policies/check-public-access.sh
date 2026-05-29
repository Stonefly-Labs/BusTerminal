#!/usr/bin/env bash
# BT-IAC-002 — No public-by-default data services in production
#
# Env-conditional: only fires when --env starts with "prod". Asserts that
# every resource of the listed types has public_network_access_enabled = false
# (or sku-conditional equivalent for ACR).
#
# Exit codes: 0 pass · 1 fail · 2 setup error.

set -euo pipefail

PLAN=""
ENV=""
ALLOWLIST=""
RULE_ID="BT-IAC-002"

usage() {
  cat <<USAGE
Usage: $0 --plan <tfplan.json> --env <env-name> [--allowlist <path>]
USAGE
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --plan) PLAN="${2:-}"; shift 2 ;;
    --env) ENV="${2:-}"; shift 2 ;;
    --allowlist) ALLOWLIST="${2:-}"; shift 2 ;;
    -h|--help) usage; exit 0 ;;
    *) echo "$RULE_ID setup: unknown arg '$1'" >&2; usage >&2; exit 2 ;;
  esac
done

if [[ -z "$PLAN" || ! -f "$PLAN" ]]; then
  echo "$RULE_ID setup: --plan <path> is required and must exist" >&2
  exit 2
fi
if [[ -z "$ENV" ]]; then
  echo "$RULE_ID setup: --env <env-name> is required" >&2
  exit 2
fi
if ! command -v jq >/dev/null 2>&1; then
  echo "$RULE_ID setup: jq is required" >&2
  exit 2
fi

# Per Q2c — dev environment may have public access enabled by design.
if [[ "$ENV" != prod* ]]; then
  echo "$RULE_ID: SKIP (env '$ENV' is non-prod; rule is prod-only per Q2c)"
  exit 0
fi

ALLOWED_JSON='[]'
if [[ -n "$ALLOWLIST" && -f "$ALLOWLIST" ]]; then
  ALLOWED_JSON=$(jq -c \
    --arg env "$ENV" \
    '."'"$RULE_ID"'" // [] | map(select((.env // "*") == $env or (.env // "*") == "*") | .resource_address // empty)' \
    "$ALLOWLIST")
fi

# Per contracts/policy-rules.md §BT-IAC-002.
TYPES_JSON='[
  "azurerm_cosmosdb_account",
  "azurerm_key_vault",
  "azurerm_search_service",
  "azurerm_servicebus_namespace",
  "azurerm_container_registry",
  "azurerm_storage_account"
]'

# Bash 3.2 compatible (macOS default) — no mapfile / readarray.
FAILURES=()
while IFS= read -r line; do
  [[ -z "$line" ]] && continue
  IFS=$'\t' read -r ADDR TYPE PNAE SKU <<<"$line"

  # ACR exposes public-network-access only on Premium. Non-Premium ACR can't
  # have private endpoints anyway — the rule's intent is satisfied trivially.
  if [[ "$TYPE" == "azurerm_container_registry" ]]; then
    SKU_STR=$(printf '%s' "$SKU" | jq -r 'if type=="string" then ascii_downcase else (.[0]?.name // "" | ascii_downcase) end')
    if [[ "$SKU_STR" != "premium" ]]; then
      continue
    fi
  fi

  case "$PNAE" in
    "true")
      FAILURES+=("$RULE_ID FAIL: $ADDR in environment $ENV has public_network_access_enabled = true")
      ;;
    "null")
      # Some provider versions omit the attribute when default-true. Treat
      # absence as a failure to force an explicit setting in prod.
      FAILURES+=("$RULE_ID FAIL: $ADDR in environment $ENV does not explicitly set public_network_access_enabled = false")
      ;;
  esac
done < <(
  jq -r --argjson types "$TYPES_JSON" --argjson allowed "$ALLOWED_JSON" '
    .resource_changes // []
    | map(select((.type | IN($types[]))))
    | map(select(((.change.actions // []) | (contains(["create"]) or contains(["update"]) or contains(["create","delete"]) or contains(["delete","create"])))))
    | map(select((.address | IN($allowed[])) | not))
    | .[]
    | [.address, .type, ((.change.after.public_network_access_enabled // .change.after.public_network_access // null) | tojson), ((.change.after.sku // .change.after.sku_name // null) | tojson)]
    | @tsv
  ' "$PLAN"
)

if [[ ${#FAILURES[@]} -gt 0 ]]; then
  printf '%s\n' "${FAILURES[@]}" >&2
  echo "$RULE_ID: ${#FAILURES[@]} failure(s)"
  exit 1
fi

echo "$RULE_ID: PASS"
exit 0
