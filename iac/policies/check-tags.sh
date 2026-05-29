#!/usr/bin/env bash
# BT-IAC-001 — Mandatory tag coverage
#
# Asserts that every taggable resource in a tofu plan has the 5 mandatory
# tags with non-empty values:
#   application = "BusTerminal"
#   environment = <env_name>
#   managed-by  = "opentofu"
#   cost-center = <non-empty>
#   one of: owner OR team (non-empty)
#
# Resource types listed in SKIP_TF_TYPES below are exempt because they do not
# accept Azure tags. Allowlist entries (per-resource bypass) live in the JSON
# file passed via --allowlist (default iac/policies/allowlist.json).
#
# Exit codes: 0 pass · 1 fail · 2 setup error.

set -euo pipefail

PLAN=""
ENV=""
ALLOWLIST=""
RULE_ID="BT-IAC-001"

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
  echo "$RULE_ID setup: jq is required but not on PATH" >&2
  exit 2
fi

# Terraform resource types whose underlying Azure resource does not accept tags.
SKIP_TF_TYPES_JSON='[
  "azurerm_role_assignment",
  "azurerm_role_definition",
  "azurerm_cosmosdb_sql_role_assignment",
  "azurerm_cosmosdb_sql_role_definition",
  "azurerm_private_dns_zone_virtual_network_link",
  "azurerm_key_vault_secret",
  "azurerm_key_vault_access_policy",
  "azurerm_federated_identity_credential",
  "azurerm_monitor_diagnostic_setting",
  "azurerm_private_endpoint_application_security_group_association",
  "azuread_application",
  "azuread_application_federated_identity_credential",
  "azuread_app_role_assignment",
  "azuread_service_principal",
  "azuread_directory_role_assignment"
]'

# azapi_resource is a generic wrapper that can target any Azure ARM resource
# type. Some of those Azure types don't accept tags (notably subnets, which
# are children of virtual networks and inherit their parent's tag context).
# This list matches against the azapi resource's `type` attribute by PREFIX —
# the trailing `@<api-version>` is ignored so AVM bumps don't break the skip.
SKIP_AZAPI_TYPE_PREFIXES_JSON='[
  "Microsoft.Network/virtualNetworks/subnets"
]'

ALLOWED_JSON='[]'
if [[ -n "$ALLOWLIST" && -f "$ALLOWLIST" ]]; then
  ALLOWED_JSON=$(jq -c '."'"$RULE_ID"'" // [] | map(.resource_address // empty)' "$ALLOWLIST")
fi

# Emit one line per taggable resource needing audit:
#   <address>\t<tagsJSON>
# Bash 3.2 compatible (macOS default) — no mapfile / readarray.
FAILURES=()
while IFS= read -r line; do
  [[ -z "$line" ]] && continue
  ADDR="${line%%	*}"
  TAGS_JSON="${line#*	}"

  # Compute missing-tag list with jq for clean semantics on null vs empty.
  MISSING=$(printf '%s' "$TAGS_JSON" | jq -r --arg env "$ENV" '
    . as $t
    | (
        (if ($t.application // "") == "BusTerminal" then [] else ["application(must=\"BusTerminal\")"] end)
        + (if ($t.environment // "") == $env then [] else ["environment(must=\""+$env+"\")"] end)
        + (if (($t."managed-by" // "") == "opentofu") then [] else ["managed-by(must=\"opentofu\")"] end)
        + (if (($t."cost-center" // "") | length) > 0 then [] else ["cost-center"] end)
        + (if ((($t.owner // "") | length) > 0) or ((($t.team // "") | length) > 0) then [] else ["owner-or-team"] end)
      )
    | join(", ")
  ')

  if [[ -n "$MISSING" ]]; then
    FAILURES+=("$RULE_ID FAIL: $ADDR is missing tag(s): $MISSING")
  fi
done < <(
  jq -r \
    --argjson skip "$SKIP_TF_TYPES_JSON" \
    --argjson skipAzapi "$SKIP_AZAPI_TYPE_PREFIXES_JSON" \
    --argjson allowed "$ALLOWED_JSON" '
    .resource_changes // []
    | map(select(
        (.change.actions // [])
          | (contains(["create"]) or contains(["update"]) or contains(["create","delete"]) or contains(["delete","create"]))
      ))
    | map(select((.type | IN($skip[])) | not))
    # azapi_resource skip: if .type == "azapi_resource" AND .change.after.type
    # (the Azure ARM resource type, e.g.
    # "Microsoft.Network/virtualNetworks/subnets@2024-07-01") begins with any
    # of the documented untaggable prefixes, drop it.
    | map(select(
        .type != "azapi_resource"
        or (
          (.change.after // {}).type as $azt
          | $skipAzapi | map(. as $p | $azt | startswith($p)) | any | not
        )
      ))
    | map(select((.address | IN($allowed[])) | not))
    | map(select(.change.after != null and (.change.after | type) == "object"))
    | map(select(.change.after | has("tags")))
    | .[]
    | [.address, (.change.after.tags // {} | tojson)]
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
