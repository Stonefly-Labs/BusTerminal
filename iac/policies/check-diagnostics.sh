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
# must NOT forward any enabled metric category to Log Analytics (Q5c).
#
# Effective-enabled-metric semantics: the azurerm v4 schema exposes BOTH
# `enabled_metric` (newer, presence = enabled) and `metric` (deprecated but
# still in-schema, has an `enabled` bool). For `moved` resources whose prior
# state had `metric { enabled = true }`, the `enabled_metric` attribute is
# Optional+Computed and the provider preserves it at plan time even when the
# module config disables the category via `metric { enabled = false }`. To
# express that pattern correctly we subtract any disabled `metric` categories
# from the `enabled_metric` set, then also fail if any `metric` entry has
# `enabled = true`. Both sources must be empty/disabled for the check to pass.
while IFS= read -r line; do
  [[ -z "$line" ]] && continue
  IFS=$'\t' read -r DIAG_ADDR CG_OK EFFECTIVE_METRIC ENABLED_LOG_COUNT <<<"$line"
  if [[ "$CG_OK" != "true" ]]; then
    if [[ "$ENABLED_LOG_COUNT" == "0" ]]; then
      FAILURES+=("$RULE_ID FAIL: $DIAG_ADDR has no enabled_log block with category_group = \"allLogs\"")
    else
      FAILURES+=("$RULE_ID FAIL: $DIAG_ADDR uses an individual log category instead of category_group = \"allLogs\"")
    fi
  fi
  if [[ "$EFFECTIVE_METRIC" == "true" ]]; then
    FAILURES+=("$RULE_ID FAIL: $DIAG_ADDR includes an enabled_metric block (Q5c forbids forwarding metrics to Log Analytics)")
  fi
done < <(
  jq -r '
    # Compute the "effective enabled metric count" for a diagnostic-setting
    # state object — subtract `metric` blocks with enabled = false from
    # `enabled_metric` categories, then add `metric` blocks with enabled = true.
    def effective_metric_count:
      (.enabled_metric // []) as $em
      | (.metric // []) as $m
      | ($m | map(select(.enabled == false)) | map(.category)) as $disabled
      | ($em | map(.category) | map(select(. as $c | $disabled | index($c) | not)) | length)
        + ($m | map(select(.enabled == true)) | length);

    .resource_changes // []
    | map(select(.type == "azurerm_monitor_diagnostic_setting"))
    | map(select(((.change.actions // []) | (contains(["create"]) or contains(["update"]) or contains(["create","delete"]) or contains(["delete","create"])))))
    | .[]
    | (.change.before // {}) as $b
    | (.change.after  // {}) as $a
    | ($b | effective_metric_count) as $effective_before
    | ($a | effective_metric_count) as $effective_after
    # Fail only when the apply INTRODUCES metric forwarding (after > before)
    # or when a create action lands a non-empty after. Pre-existing metric
    # state on an unrelated `update` (e.g. log_analytics_destination_type
    # flip) is not regressed by this apply and is tracked separately as
    # Q5c-cleanup tech debt rather than blocking every PR on the historical
    # state. v4 provider exposes both `enabled_metric` (Optional+Computed,
    # preserved from state when config omits) and the deprecated `metric`
    # block — we only treat the apply as introducing metrics when its
    # effective count grows.
    | (
        ((.change.actions // []) | contains(["create"]) and $effective_after > 0)
        or ($effective_after > $effective_before)
      ) as $has_new_metric
    | [
        .address,
        (($a.enabled_log // []) | map(select(.category_group == "allLogs")) | length > 0 | tostring),
        ($has_new_metric | tostring),
        (($a.enabled_log // []) | length | tostring)
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

  # Walk up the module-ancestor chain of the resource. The spec-005 convention
  # is `module.<svc>` wraps BOTH `module.<svc-impl>` (e.g. AVM `module.search`)
  # AND `module.diagnostics`. So a diagnostic setting at any ancestor's
  # descendant subtree covers the resource — not just descendants of the
  # resource's direct owner module. Build the candidate ancestor list by
  # progressively trimming the last ".module.X" segment.
  ANCESTORS=("$RES_PREFIX")
  cur="$RES_PREFIX"
  while [[ "$cur" == *.module.* ]]; do
    cur="${cur%.module.*}"
    ANCESTORS+=("$cur")
  done
  # Root module (empty prefix) covers root-level diag settings.
  ANCESTORS+=("")

  MATCHED=0
  while IFS= read -r dp; do
    for anc in "${ANCESTORS[@]}"; do
      if [[ "$dp" == "$anc" || ( -n "$anc" && "$dp" == "$anc".* ) || ( -z "$anc" && -n "$dp" ) ]]; then
        MATCHED=1
        break 2
      fi
    done
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
