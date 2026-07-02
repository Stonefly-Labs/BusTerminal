#!/usr/bin/env bash
# Operator wrapper: fmt → validate → plan → policy gate → apply
#
# Runs the full local-validation chain against a supplied env composition
# directory with the right backend key, then (if the policy gate passes)
# applies the captured plan.
#
# Per specs/005-infrastructure-baseline/plan.md §Project Structure /
# iac/scripts/apply-env.sh. Intended for local operator use; the CI
# workflow (.github/workflows/iac-apply-dev.yml) wires the same steps with
# OIDC federated login and a manual-approval gate for BT-IAC-007.

set -euo pipefail

ENV=""
SKIP_POLICIES=0
SKIP_APPLY=0
AUTO_APPROVE=0
VAR_FILE="terraform.tfvars"
PARK_MODE=""

usage() {
  cat <<USAGE
Usage: $0 --env <dev|test|prod> [--var-file <name>] [--park|--unpark] [--skip-policies] [--skip-apply] [--auto-approve]

Runs against iac/environments/<env>/:
  1. tofu fmt -check -recursive (whole iac/ tree)
  2. tofu init (with azurerm backend)
  3. tofu validate
  4. tofu plan -var-file=<var-file> -var parked=<resolved> -out=tfplan
  5. tofu show -json tfplan > tfplan.json
  6. bash iac/policies/run-policies.sh --plan tfplan.json --env <env>
  7. tofu apply tfplan  (skipped with --skip-apply; prompts unless --auto-approve)

Parking (dev cost control — docs/dev-environment-parking.md):
  --park     set parked=true (destroys AI Search + Service Bus, indexer to 0)
  --unpark   set parked=false (recreates them)
  neither    reuse the live 'parked' state output, so a routine apply never
             silently unparks (or parks) the environment.
  After a --park apply, run iac/scripts/clear-indexer-leases.sh --env <env> so
  the next unpark fully re-populates the search index from the change feed.

Exits non-zero on any failure. The policy gate must pass before apply.
USAGE
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --env) ENV="${2:-}"; shift 2 ;;
    --var-file) VAR_FILE="${2:-}"; shift 2 ;;
    --park) PARK_MODE="true"; shift ;;
    --unpark) PARK_MODE="false"; shift ;;
    --skip-policies) SKIP_POLICIES=1; shift ;;
    --skip-apply) SKIP_APPLY=1; shift ;;
    --auto-approve) AUTO_APPROVE=1; shift ;;
    -h|--help) usage; exit 0 ;;
    *) echo "apply-env: unknown arg '$1'" >&2; usage >&2; exit 2 ;;
  esac
done

if [[ -z "$ENV" ]]; then
  echo "apply-env: --env <dev|test|prod> is required" >&2
  exit 2
fi

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
COMPOSITION_DIR="$REPO_ROOT/iac/environments/$ENV"
POLICIES_DIR="$REPO_ROOT/iac/policies"

if [[ ! -d "$COMPOSITION_DIR" ]]; then
  echo "apply-env: composition directory $COMPOSITION_DIR does not exist" >&2
  exit 2
fi

for cmd in tofu jq; do
  if ! command -v "$cmd" >/dev/null 2>&1; then
    echo "apply-env: '$cmd' is required on PATH" >&2
    exit 2
  fi
done

echo "==> tofu fmt -check -recursive ($REPO_ROOT/iac)"
( cd "$REPO_ROOT/iac" && tofu fmt -check -recursive -diff )

echo "==> tofu init ($COMPOSITION_DIR)"
( cd "$COMPOSITION_DIR" && tofu init -input=false )

echo "==> tofu validate ($COMPOSITION_DIR)"
( cd "$COMPOSITION_DIR" && tofu validate )

# Resolve the parked flag: explicit --park/--unpark wins; otherwise reuse the
# live state output so a routine apply preserves the current parked posture.
# Only compositions that declare `variable "parked"` (dev today) get the -var;
# passing an undeclared -var is a hard error in tofu.
PARK_VAR_ARGS=()
if grep -qE 'variable\s+"parked"' "$COMPOSITION_DIR"/*.tf 2>/dev/null; then
  if [[ -z "$PARK_MODE" ]]; then
    PARK_MODE=$( (cd "$COMPOSITION_DIR" && tofu output -raw parked 2>/dev/null) || true )
    case "$PARK_MODE" in
      true|false) ;;
      *) PARK_MODE="false" ;;
    esac
  fi
  echo "==> parked=$PARK_MODE"
  PARK_VAR_ARGS=(-var "parked=$PARK_MODE")
elif [[ -n "$PARK_MODE" ]]; then
  echo "apply-env: --park/--unpark requested but $ENV does not declare variable \"parked\"" >&2
  exit 2
fi

echo "==> tofu plan ($COMPOSITION_DIR)"
( cd "$COMPOSITION_DIR" && tofu plan -input=false -lock-timeout=300s -var-file="$VAR_FILE" ${PARK_VAR_ARGS[@]+"${PARK_VAR_ARGS[@]}"} -out=tfplan )

echo "==> tofu show -json tfplan"
( cd "$COMPOSITION_DIR" && tofu show -json tfplan > tfplan.json )

if [[ "$SKIP_POLICIES" == "0" ]]; then
  echo "==> Custom policy gate"
  bash "$POLICIES_DIR/run-policies.sh" \
    --plan "$COMPOSITION_DIR/tfplan.json" \
    --env "$ENV" \
    --allowlist "$POLICIES_DIR/allowlist.json" \
    --composition-dir "$COMPOSITION_DIR" \
    --report "$COMPOSITION_DIR/policies-report.json"
else
  echo "==> Custom policy gate (SKIPPED via --skip-policies)"
fi

if [[ "$SKIP_APPLY" == "1" ]]; then
  echo "==> Apply skipped via --skip-apply. Plan + report are in $COMPOSITION_DIR/."
  exit 0
fi

echo "==> tofu apply tfplan ($COMPOSITION_DIR)"
if [[ "$AUTO_APPROVE" == "1" ]]; then
  ( cd "$COMPOSITION_DIR" && tofu apply -input=false -auto-approve tfplan )
else
  ( cd "$COMPOSITION_DIR" && tofu apply -input=false tfplan )
fi

echo "==> Done."
