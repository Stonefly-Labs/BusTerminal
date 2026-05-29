#!/usr/bin/env bash
# BT-IAC-007 — Stateful-resource destroys require explicit approval
#
# Asserts the tfplan contains NO delete or destroy-replace action targeting
# any of the documented stateful resource types. When it fires, emits a
# "REQUIRES MANUAL APPROVAL" banner so the CI workflow can pause on a
# manual-approval gate.
#
# Allowlist key: BT-IAC-007:<resource_address> plus a justification in the
# PR description (reviewers enforce — gate does not parse PR text).
#
# Exit codes: 0 pass · 1 fail · 2 setup error.

set -euo pipefail

PLAN=""
ENV=""
ALLOWLIST=""
RULE_ID="BT-IAC-007"

usage() {
  cat <<USAGE
Usage: $0 --plan <tfplan.json> [--env <env-name>] [--allowlist <path>]
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
if ! command -v jq >/dev/null 2>&1; then
  echo "$RULE_ID setup: jq is required" >&2
  exit 2
fi

ALLOWED_JSON='[]'
if [[ -n "$ALLOWLIST" && -f "$ALLOWLIST" ]]; then
  ALLOWED_JSON=$(jq -c '."'"$RULE_ID"'" // [] | map(.resource_address // empty)' "$ALLOWLIST")
fi

# Per contracts/policy-rules.md §BT-IAC-007.
STATEFUL_TYPES_JSON='[
  "azurerm_resource_group",
  "azurerm_log_analytics_workspace",
  "azurerm_application_insights",
  "azurerm_key_vault",
  "azurerm_key_vault_secret",
  "azurerm_container_registry",
  "azurerm_cosmosdb_account",
  "azurerm_cosmosdb_sql_database",
  "azurerm_user_assigned_identity",
  "azurerm_container_app_environment",
  "azurerm_storage_account"
]'

FAILURES=()
while IFS= read -r line; do
  [[ -z "$line" ]] && continue
  IFS=$'\t' read -r ADDR TYPE ACTIONS <<<"$line"
  FAILURES+=("$RULE_ID FAIL: plan would $ACTIONS stateful resource $ADDR (state would be lost). Manual reviewer approval required.")
done < <(
  jq -r --argjson types "$STATEFUL_TYPES_JSON" --argjson allowed "$ALLOWED_JSON" '
    .resource_changes // []
    | map(select(.type | IN($types[])))
    | map(select((.address | IN($allowed[])) | not))
    | map(select(
        (.change.actions // []) as $a
        | ($a == ["delete"]) or ($a == ["delete","create"]) or ($a == ["create","delete"])
      ))
    | .[]
    | [.address, .type, (.change.actions | join("+"))]
    | @tsv
  ' "$PLAN"
)

if [[ ${#FAILURES[@]} -gt 0 ]]; then
  printf '%s\n' "${FAILURES[@]}" >&2
  echo ""
  echo "============================================================"
  echo "  $RULE_ID — REQUIRES MANUAL APPROVAL"
  echo "  ${#FAILURES[@]} stateful destroy/replace action(s) detected."
  echo "  CI must pause for reviewer sign-off before apply."
  echo "============================================================"
  exit 1
fi

echo "$RULE_ID: PASS"
exit 0
