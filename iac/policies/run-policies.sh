#!/usr/bin/env bash
# Orchestrator for the BusTerminal custom IaC policy gate.
#
# Runs all rules from contracts/policy-rules.md in order, accumulates
# failures, emits a Markdown summary to stdout, and writes a JSON detail
# file to --report (default policies-report.json in CWD).
#
# Exit codes (per contracts/policy-rules.md §Rule execution):
#   0 — all-pass
#   1 — one or more rule failures
#   2 — setup error (no tfplan, jq/tofu missing, bad args, etc.)
#
# When BT-IAC-007 fails, an additional "REQUIRES MANUAL APPROVAL" banner
# is printed and a sentinel file `requires-manual-approval.flag` is written
# next to the report so the CI workflow can pause on an approval gate.

set -uo pipefail

POLICIES_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PLAN=""
STATE=""
ENV=""
ALLOWLIST="$POLICIES_DIR/allowlist.json"
COMPOSITION_DIR=""
REPORT="policies-report.json"
APPROVAL_FLAG=""

usage() {
  cat <<USAGE
Usage: $0 --plan <tfplan.json> --env <env-name> \\
          [--state <state.json>] [--allowlist <path>] \\
          [--composition-dir <iac/environments/<env>>] \\
          [--report <out.json>]

Runs every rule in iac/policies/check-*.sh and emits a Markdown summary
to stdout plus a JSON detail file to --report.
USAGE
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --plan) PLAN="${2:-}"; shift 2 ;;
    --state) STATE="${2:-}"; shift 2 ;;
    --env) ENV="${2:-}"; shift 2 ;;
    --allowlist) ALLOWLIST="${2:-}"; shift 2 ;;
    --composition-dir) COMPOSITION_DIR="${2:-}"; shift 2 ;;
    --report) REPORT="${2:-}"; shift 2 ;;
    -h|--help) usage; exit 0 ;;
    *) echo "run-policies setup: unknown arg '$1'" >&2; usage >&2; exit 2 ;;
  esac
done

if [[ -z "$PLAN" || ! -f "$PLAN" ]]; then
  echo "run-policies setup: --plan <tfplan.json> is required and must exist" >&2
  exit 2
fi
if [[ -z "$ENV" ]]; then
  echo "run-policies setup: --env <env-name> is required" >&2
  exit 2
fi
if ! command -v jq >/dev/null 2>&1; then
  echo "run-policies setup: jq is required on PATH" >&2
  exit 2
fi

APPROVAL_FLAG="$(dirname "$REPORT")/requires-manual-approval.flag"
rm -f "$APPROVAL_FLAG"

# Rule registry — order matters for deterministic output.
#   <rule-id>|<script>|<requires-composition-dir>
RULES=(
  "BT-IAC-001|check-tags.sh|0"
  "BT-IAC-002|check-public-access.sh|0"
  "BT-IAC-003|check-diagnostics.sh|0"
  "BT-IAC-004|check-rbac-scope.sh|0"
  "BT-IAC-005|check-outputs-no-secrets.sh|0"
  "BT-IAC-006|check-lockfile.sh|1"
  "BT-IAC-007|check-stateful-destroys.sh|0"
)

PASS_COUNT=0
FAIL_COUNT=0
SETUP_ERR_COUNT=0
ROWS_JSON='[]'
DESTRUCTIVE=0

for entry in "${RULES[@]}"; do
  RULE_ID="${entry%%|*}"
  rest="${entry#*|}"
  SCRIPT_NAME="${rest%%|*}"
  NEEDS_COMP="${rest##*|}"
  SCRIPT_PATH="$POLICIES_DIR/$SCRIPT_NAME"

  if [[ ! -x "$SCRIPT_PATH" ]]; then
    # Tolerate missing executable bit on freshly-checked-out scripts.
    if [[ -f "$SCRIPT_PATH" ]]; then
      chmod +x "$SCRIPT_PATH" 2>/dev/null || true
    fi
  fi
  if [[ ! -f "$SCRIPT_PATH" ]]; then
    echo "run-policies setup: $SCRIPT_PATH is missing" >&2
    SETUP_ERR_COUNT=$((SETUP_ERR_COUNT + 1))
    continue
  fi

  CMD=("$SCRIPT_PATH" --plan "$PLAN" --env "$ENV" --allowlist "$ALLOWLIST")
  if [[ "$NEEDS_COMP" == "1" ]]; then
    if [[ -z "$COMPOSITION_DIR" ]]; then
      # Skip with a warning rather than fail-the-orchestrator — the
      # lockfile rule is only meaningful when run from a composition dir.
      ROWS_JSON=$(jq -c \
        --arg rule "$RULE_ID" \
        --arg status "SKIP" \
        --arg detail "no --composition-dir provided; lockfile drift check requires the env directory" \
        '. + [{rule:$rule, status:$status, detail:$detail}]' <<<"$ROWS_JSON")
      continue
    fi
    CMD+=(--composition-dir "$COMPOSITION_DIR")
  fi

  OUT_FILE=$(mktemp)
  ERR_FILE=$(mktemp)
  set +e
  "${CMD[@]}" >"$OUT_FILE" 2>"$ERR_FILE"
  RC=$?
  set -e

  STDOUT=$(<"$OUT_FILE")
  STDERR=$(<"$ERR_FILE")
  rm -f "$OUT_FILE" "$ERR_FILE"

  case "$RC" in
    0)
      PASS_COUNT=$((PASS_COUNT + 1))
      STATUS=$(printf '%s' "$STDOUT" | grep -E "^$RULE_ID: (PASS|SKIP)" | head -1)
      if [[ -z "$STATUS" ]]; then STATUS="$RULE_ID: PASS"; fi
      DETAIL="$STATUS"
      STATUS_LABEL="${STATUS##*: }"
      ;;
    1)
      FAIL_COUNT=$((FAIL_COUNT + 1))
      STATUS_LABEL="FAIL"
      DETAIL="$STDERR"
      if [[ "$RULE_ID" == "BT-IAC-007" ]]; then
        DESTRUCTIVE=1
      fi
      ;;
    *)
      SETUP_ERR_COUNT=$((SETUP_ERR_COUNT + 1))
      STATUS_LABEL="ERROR"
      DETAIL="$STDERR"
      ;;
  esac

  ROWS_JSON=$(jq -c \
    --arg rule "$RULE_ID" \
    --arg status "$STATUS_LABEL" \
    --arg detail "$DETAIL" \
    --arg stdout "$STDOUT" \
    --argjson rc "$RC" \
    '. + [{rule:$rule, status:$status, exit_code:$rc, stdout:$stdout, detail:$detail}]' \
    <<<"$ROWS_JSON")
done

# --- Markdown summary to stdout ---------------------------------------------
echo "## BusTerminal IaC policy gate — env \`$ENV\`"
echo ""
echo "| Rule | Status | Detail |"
echo "|---|---|---|"
echo "$ROWS_JSON" | jq -r '
  .[]
  | "| \(.rule) | \(.status) | \((.detail // "") | gsub("\n"; "<br>") | .[0:300]) |"
'
echo ""
echo "**Totals**: ${PASS_COUNT} pass · ${FAIL_COUNT} fail · ${SETUP_ERR_COUNT} setup error(s)"

if [[ "$DESTRUCTIVE" == "1" ]]; then
  echo ""
  echo "> ⚠️ **REQUIRES MANUAL APPROVAL** — BT-IAC-007 detected a stateful destroy. CI must pause for reviewer sign-off before apply."
  : > "$APPROVAL_FLAG"
fi

# --- JSON detail file -------------------------------------------------------
mkdir -p "$(dirname "$REPORT")"
jq -n \
  --arg env "$ENV" \
  --argjson rows "$ROWS_JSON" \
  --argjson pass "$PASS_COUNT" \
  --argjson fail "$FAIL_COUNT" \
  --argjson setup_err "$SETUP_ERR_COUNT" \
  --argjson destructive "$DESTRUCTIVE" \
  '{
    env: $env,
    totals: {pass: $pass, fail: $fail, setup_error: $setup_err},
    requires_manual_approval: ($destructive == 1),
    rules: $rows
  }' > "$REPORT"

# --- Exit code --------------------------------------------------------------
if [[ "$SETUP_ERR_COUNT" -gt 0 ]]; then
  exit 2
fi
if [[ "$FAIL_COUNT" -gt 0 ]]; then
  exit 1
fi
exit 0
