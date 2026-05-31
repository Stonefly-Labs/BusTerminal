#!/usr/bin/env bash
# BT-IAC-004 — RBAC scope is not subscription-wide for workloads
#
# For every azurerm_role_assignment / azurerm_cosmosdb_sql_role_assignment in
# the plan:
#   - If the assignment's address indicates a workload UAMI grant (heuristic:
#     address contains "workload" or principal references a workload UAMI),
#     the scope MUST NOT be subscription-level or management-group-level.
#   - If the role_definition is a management-plane role (Owner, Contributor,
#     User Access Administrator, Cosmos DB Account Contributor, Service Bus
#     Namespace Owner, Search Service Contributor, Key Vault Administrator)
#     AND the principal is a workload UAMI, FAIL.
#
# The pipeline-MI's subscription-Contributor + condition-scoped RBAC-Admin
# is the documented Complexity Tracking exception and is recorded in the
# allowlist file (see iac/policies/allowlist.json).
#
# Exit codes: 0 pass · 1 fail · 2 setup error.

set -euo pipefail

PLAN=""
ENV=""
ALLOWLIST=""
RULE_ID="BT-IAC-004"

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

# Allowlist entries match by (principal_match, role, scope) tuple. The
# allowlist file's BT-IAC-004 section lists the pipeline-MI exceptions.
ALLOWED_JSON='[]'
if [[ -n "$ALLOWLIST" && -f "$ALLOWLIST" ]]; then
  ALLOWED_JSON=$(jq -c '."'"$RULE_ID"'" // []' "$ALLOWLIST")
fi

# Management-plane roles that must NEVER land on a workload UAMI regardless
# of scope.
MGMT_PLANE_ROLES_JSON='[
  "Owner",
  "Contributor",
  "User Access Administrator",
  "Cosmos DB Account Contributor",
  "Service Bus Namespace Owner",
  "Search Service Contributor",
  "Key Vault Administrator",
  "Role Based Access Control Administrator"
]'

FAILURES=()

while IFS= read -r line; do
  [[ -z "$line" ]] && continue
  IFS=$'\t' read -r ADDR ROLE_NAME SCOPE PRINCIPAL <<<"$line"

  # Identify workload-targeted assignments. Heuristic: address contains
  # "workload" (the project convention is `module.workload_identity.*` or
  # similar). This matches the spirit of "principal_type=ServicePrincipal
  # AND name matches mi-${prefix}-workload" without needing to resolve
  # post-apply UUIDs at plan time.
  IS_WORKLOAD=0
  if [[ "$ADDR" == *workload* ]]; then
    IS_WORKLOAD=1
  fi

  # Pipeline-MI assignments are intentionally subscription-scoped per
  # Complexity Tracking; allowlist matching handles them.
  IS_PIPELINE=0
  if [[ "$ADDR" == *pipeline* ]]; then
    IS_PIPELINE=1
  fi

  # Allowlist consultation (only meaningful for pipeline-MI per the file).
  if [[ "$IS_PIPELINE" == "1" ]]; then
    if echo "$ALLOWED_JSON" | jq -e \
        --arg role "$ROLE_NAME" \
        --arg scope "$SCOPE" \
        --arg addr "$ADDR" '
          map(select(
            (.role == $role or .role == "*")
            and (.scope == $scope or .scope == "*" or ($scope | startswith(.scope | rtrimstr("*"))))
            and ((.principal_match // "*") == "*" or ($addr | test(.principal_match)))
          )) | length > 0
        ' >/dev/null; then
      continue
    fi
  fi

  # Subscription-scope and management-group-scope detection.
  IS_SUB_SCOPE=0
  if [[ "$SCOPE" =~ ^/subscriptions/[0-9a-fA-F-]+$ ]]; then
    IS_SUB_SCOPE=1
  fi
  IS_MG_SCOPE=0
  if [[ "$SCOPE" == /providers/Microsoft.Management/managementGroups/* ]]; then
    IS_MG_SCOPE=1
  fi

  if [[ "$IS_WORKLOAD" == "1" && ("$IS_SUB_SCOPE" == "1" || "$IS_MG_SCOPE" == "1") ]]; then
    FAILURES+=("$RULE_ID FAIL: role assignment $ADDR grants $ROLE_NAME to $PRINCIPAL at $SCOPE — workload identities must not receive subscription-wide or management-plane grants")
    continue
  fi

  # Management-plane role on a workload UAMI at any scope.
  if [[ "$IS_WORKLOAD" == "1" ]]; then
    if echo "$MGMT_PLANE_ROLES_JSON" | jq -e --arg r "$ROLE_NAME" '. | index($r)' >/dev/null; then
      FAILURES+=("$RULE_ID FAIL: role assignment $ADDR grants $ROLE_NAME to $PRINCIPAL at $SCOPE — workload identities must not receive subscription-wide or management-plane grants")
    fi
  fi
done < <(
  jq -r '
    .resource_changes // []
    | map(select(.type == "azurerm_role_assignment" or .type == "azurerm_cosmosdb_sql_role_assignment"))
    | map(select(((.change.actions // []) | (contains(["create"]) or contains(["update"]) or contains(["create","delete"]) or contains(["delete","create"])))))
    | .[]
    | [
        .address,
        (.change.after.role_definition_name // .change.after.role_definition_id // "<unknown-role>"),
        (.change.after.scope // "<unknown-scope>"),
        (.change.after.principal_id // "<unknown-principal>")
      ]
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
