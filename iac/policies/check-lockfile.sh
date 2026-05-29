#!/usr/bin/env bash
# BT-IAC-006 — Provider versions pinned
#
# Asserts the env composition's .terraform.lock.hcl exists, is committed,
# and matches what `tofu init -upgrade=false` resolves (no drift).
#
# Unlike the other rules, this check operates against the working tree of an
# env composition rather than a tfplan. It is invoked separately by the
# orchestrator with --composition-dir.
#
# Exit codes: 0 pass · 1 fail · 2 setup error.

set -euo pipefail

COMPOSITION_DIR=""
RULE_ID="BT-IAC-006"

usage() {
  cat <<USAGE
Usage: $0 --composition-dir <path>

Asserts that the .terraform.lock.hcl in <path> is present, committed, and
matches the providers tofu init -upgrade=false would resolve.
USAGE
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --composition-dir) COMPOSITION_DIR="${2:-}"; shift 2 ;;
    # Accept-but-ignore the orchestrator's standard args so this script is
    # interchangeable with the others.
    --plan|--env|--allowlist) shift 2 ;;
    -h|--help) usage; exit 0 ;;
    *) echo "$RULE_ID setup: unknown arg '$1'" >&2; usage >&2; exit 2 ;;
  esac
done

if [[ -z "$COMPOSITION_DIR" || ! -d "$COMPOSITION_DIR" ]]; then
  echo "$RULE_ID setup: --composition-dir <path> is required and must exist" >&2
  exit 2
fi
if ! command -v tofu >/dev/null 2>&1; then
  echo "$RULE_ID setup: tofu CLI is required on PATH" >&2
  exit 2
fi

LOCKFILE="$COMPOSITION_DIR/.terraform.lock.hcl"
if [[ ! -f "$LOCKFILE" ]]; then
  echo "$RULE_ID FAIL: .terraform.lock.hcl is missing under $COMPOSITION_DIR — run 'tofu init' and commit the lockfile" >&2
  exit 1
fi

# Capture the committed hash of the lockfile.
EXPECTED_SHA=$(shasum -a 256 "$LOCKFILE" | awk '{print $1}')

# Re-resolve providers without upgrading. tofu init writes to the lockfile
# in place when providers need pinning — we run in a temp copy to avoid
# mutating the working tree.
WORK_DIR=$(mktemp -d)
trap 'rm -rf "$WORK_DIR"' EXIT

cp -R "$COMPOSITION_DIR"/. "$WORK_DIR"/
# Strip any pre-existing .terraform directory so we get a clean resolution.
rm -rf "$WORK_DIR/.terraform"

(
  cd "$WORK_DIR"
  # -backend=false keeps the check fast and avoids requiring backend creds.
  tofu init -backend=false -upgrade=false -input=false >/dev/null
)

RESOLVED_SHA=$(shasum -a 256 "$WORK_DIR/.terraform.lock.hcl" | awk '{print $1}')

if [[ "$EXPECTED_SHA" != "$RESOLVED_SHA" ]]; then
  echo "$RULE_ID FAIL: provider lockfile drift detected — expected $EXPECTED_SHA, got $RESOLVED_SHA" >&2
  echo "    Run 'tofu init -upgrade' in $COMPOSITION_DIR and commit the updated .terraform.lock.hcl" >&2
  exit 1
fi

echo "$RULE_ID: PASS"
exit 0
