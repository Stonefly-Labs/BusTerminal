#!/usr/bin/env bash
# BT-IAC-005 — No secret values in non-sensitive OpenTofu outputs
#
# For every output in the tfplan:
#   - If sensitive=false, the resolved value MUST NOT match documented
#     secret patterns (account keys, SAS, SB connection strings, JWTs,
#     PEM key headers, raw InstrumentationKey).
#   - The output named like application_insights_connection_string MUST be
#     marked sensitive (per Q1c documented exception).
#
# Allowlist: NONE permitted; secret-content patterns are absolute.
#
# Exit codes: 0 pass · 1 fail · 2 setup error.

set -euo pipefail

PLAN=""
ENV=""
RULE_ID="BT-IAC-005"

usage() {
  cat <<USAGE
Usage: $0 --plan <tfplan.json> [--env <env-name>]
USAGE
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --plan) PLAN="${2:-}"; shift 2 ;;
    --env) ENV="${2:-}"; shift 2 ;;
    --allowlist) shift 2 ;;
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

# Output_changes in a tfplan look like:
#   "output_changes": {
#     "name": {
#       "actions": [...],
#       "after": <value or null>,
#       "after_sensitive": bool,
#       "after_unknown": bool
#     }
#   }

FAILURES=()

# Iterate non-sensitive, knowable outputs and check their string value.
while IFS= read -r line; do
  [[ -z "$line" ]] && continue
  IFS=$'\t' read -r NAME VALUE <<<"$line"

  # Patterns from contracts/policy-rules.md §BT-IAC-005.
  if [[ "$VALUE" == *"AccountKey="* ]]; then
    FAILURES+=("$RULE_ID FAIL: output $NAME contains a secret-like value (AccountKey=) without being marked sensitive")
  fi
  if [[ "$VALUE" == *"SharedAccessSignature="* ]]; then
    FAILURES+=("$RULE_ID FAIL: output $NAME contains a secret-like value (SharedAccessSignature=) without being marked sensitive")
  fi
  if [[ "$VALUE" =~ Endpoint=sb:// && "$VALUE" == *"SharedAccessKey="* ]]; then
    FAILURES+=("$RULE_ID FAIL: output $NAME contains a secret-like value (Endpoint=sb://...SharedAccessKey=) without being marked sensitive")
  fi
  if [[ "$VALUE" == *"-----BEGIN"*"PRIVATE KEY-----"* ]]; then
    FAILURES+=("$RULE_ID FAIL: output $NAME contains a secret-like value (PEM private key) without being marked sensitive")
  fi
  if [[ "$VALUE" == eyJ* ]]; then
    FAILURES+=("$RULE_ID FAIL: output $NAME contains a secret-like value (likely JWT) without being marked sensitive")
  fi
  if [[ "$VALUE" == *"InstrumentationKey="* && "$NAME" != *app_insights_connection_string* && "$NAME" != *appinsights_connection_string* ]]; then
    FAILURES+=("$RULE_ID FAIL: output $NAME contains a secret-like value (InstrumentationKey=) without being marked sensitive")
  fi
done < <(
  jq -r '
    .output_changes // {}
    | to_entries
    | map(select((.value.after_sensitive // false) == false))
    | map(select((.value.after_unknown // false) == false))
    | map(select(.value.after != null))
    | map(select((.value.after | type) == "string"))
    | .[]
    | [.key, .value.after]
    | @tsv
  ' "$PLAN"
)

# Affirmative check: app_insights_connection_string output IS sensitive.
NON_SENSITIVE_CS=$(
  jq -r '
    .output_changes // {}
    | to_entries
    | map(select(.key | test("app(_)?insights_connection_string"; "i")))
    | map(select((.value.after_sensitive // false) == false))
    | .[]
    | .key
  ' "$PLAN"
)
while IFS= read -r name; do
  [[ -z "$name" ]] && continue
  FAILURES+=("$RULE_ID FAIL: output $name must be marked sensitive (App Insights connection string is the documented sensitive exception per Q1c)")
done <<<"$NON_SENSITIVE_CS"

if [[ ${#FAILURES[@]} -gt 0 ]]; then
  printf '%s\n' "${FAILURES[@]}" >&2
  echo "$RULE_ID: ${#FAILURES[@]} failure(s)"
  exit 1
fi

echo "$RULE_ID: PASS"
exit 0
