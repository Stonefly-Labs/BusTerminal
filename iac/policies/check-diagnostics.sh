#!/usr/bin/env bash
# BT-IAC-003 — Diagnostic-settings coverage and shape
#
# For each resource of the supported types, asserts that some
# azurerm_monitor_diagnostic_setting exists in the same module scope (a
# sibling/descendant of the same parent module address — the convention
# established by iac/modules/diagnostic-settings) AND that the diagnostic
# setting uses category_group = "allLogs" with NO enabled_metric block.
#
# Allowlist entries bypass coverage check for resources Azure does not
# support diagnostic settings on (identified at task-implementation time).
#
# Exit codes: 0 pass · 1 fail · 2 setup error.

set -euo pipefail

PLAN=""
ENV=""
ALLOWLIST=""
RULE_ID="BT-IAC-003"

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

# Per contracts/policy-rules.md §BT-IAC-003.
TYPES_JSON='[
  "azurerm_cosmosdb_account",
  "azurerm_key_vault",
  "azurerm_search_service",
  "azurerm_servicebus_namespace",
  "azurerm_container_registry",
  "azurerm_container_app_environment",
  "azurerm_container_app",
  "azurerm_log_analytics_workspace",
  "azurerm_application_insights"
]'

FAILURES=()

# Shape check: every diagnostic_setting must use category_group="allLogs" and
# must NOT include enabled_metric blocks (Q5c).
while IFS= read -r line; do
  [[ -z "$line" ]] && continue
  IFS=$'\t' read -r DIAG_ADDR CG_OK HAS_METRIC ENABLED_LOG_COUNT <<<"$line"
  if [[ "$CG_OK" != "true" ]]; then
    if [[ "$ENABLED_LOG_COUNT" == "0" ]]; then
      FAILURES+=("$RULE_ID FAIL: $DIAG_ADDR has no enabled_log block with category_group = \"allLogs\"")
    else
      FAILURES+=("$RULE_ID FAIL: $DIAG_ADDR uses an individual log category instead of category_group = \"allLogs\"")
    fi
  fi
  if [[ "$HAS_METRIC" == "true" ]]; then
    FAILURES+=("$RULE_ID FAIL: $DIAG_ADDR includes an enabled_metric block (Q5c forbids forwarding metrics to Log Analytics)")
  fi
done < <(
  jq -r '
    .resource_changes // []
    | map(select(.type == "azurerm_monitor_diagnostic_setting"))
    | map(select(((.change.actions // []) | (contains(["create"]) or contains(["update"]) or contains(["create","delete"]) or contains(["delete","create"])))))
    | .[]
    | [
        .address,
        ((.change.after.enabled_log // []) | map(select(.category_group == "allLogs")) | length > 0 | tostring),
        (((.change.after.enabled_metric // []) | length > 0) | tostring),
        ((.change.after.enabled_log // []) | length | tostring)
      ]
    | @tsv
  ' "$PLAN"
)

# Coverage check: for each supported-type resource being created/updated,
# require at least one diagnostic_setting whose module-path is a sibling or
# descendant of the resource's module-path. The convention is:
#   module.<svc>.azurerm_<svc>.this
#   module.<svc>.module.diagnostics.azurerm_monitor_diagnostic_setting.this
# so the diagnostic-setting address starts with "module.<svc>".
#
# This is a convention check, not a literal target_resource_id match — at
# plan time the target id is typically unknown for net-new resources.

# Build module-prefix list of diagnostic_settings.
DIAG_PREFIXES=$(
  jq -r '
    .resource_changes // []
    | map(select(.type == "azurerm_monitor_diagnostic_setting"))
    | map(.address)
    | map(
        # Drop trailing ".azurerm_monitor_diagnostic_setting.<name>" plus any
        # indexer suffix. Anything before is the owning module path; for
        # root-level diag settings this becomes "" (root module).
        capture("^(?<prefix>.*?)(?:^|\\.)azurerm_monitor_diagnostic_setting\\..*$"; "x") // {prefix:.}
        | .prefix
      )
    | unique
    | .[]
  ' "$PLAN"
)

# For each candidate resource, check coverage.
while IFS= read -r line; do
  [[ -z "$line" ]] && continue
  IFS=$'\t' read -r ADDR TYPE <<<"$line"

  # Skip if explicitly allowlisted.
  if jq -e --arg a "$ADDR" --argjson allowed "$ALLOWED_JSON" -n '$allowed | index($a)' >/dev/null; then
    continue
  fi

  # Owning module prefix of this resource (everything before `.<type>.<name>`).
  RES_PREFIX=$(printf '%s' "$ADDR" | sed -E "s/(^|\\.)${TYPE}\\.[^.]+(\\[.*\\])?\$//")

  MATCHED=0
  while IFS= read -r dp; do
    # A diagnostic setting under the same module owns this resource if the
    # diagnostic-setting prefix equals the resource's prefix OR is a deeper
    # descendant of it.
    if [[ "$dp" == "$RES_PREFIX" || "$dp" == "$RES_PREFIX".* ]]; then
      MATCHED=1
      break
    fi
  done <<<"$DIAG_PREFIXES"

  if [[ "$MATCHED" == "0" ]]; then
    FAILURES+=("$RULE_ID FAIL: $ADDR has no diagnostic setting forwarding allLogs to a Log Analytics workspace")
  fi
done < <(
  jq -r --argjson types "$TYPES_JSON" '
    .resource_changes // []
    | map(select(.type | IN($types[])))
    | map(select(((.change.actions // []) | (contains(["create"]) or contains(["update"]) or contains(["create","delete"]) or contains(["delete","create"])))))
    | .[]
    | [.address, .type]
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
