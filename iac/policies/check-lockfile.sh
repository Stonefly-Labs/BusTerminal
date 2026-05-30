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
#
# Compositions reference shared modules via `source = "../../modules/<name>"`,
# so a flat copy of the composition dir alone leaves those sources dangling
# (`Unable to evaluate directory symlink: lstat ../../modules`). Mirror the
# repo-relative parent layout: the composition lives at
# `iac/environments/<env>/` so we recreate `$WORK_DIR/environments/<env>/`
# alongside `$WORK_DIR/modules/` (symlinked back to the real tree — read-only
# access by `tofu init` is sufficient).
WORK_DIR=$(mktemp -d)
trap 'rm -rf "$WORK_DIR"' EXIT

# Normalize to an absolute path before computing basename / parent so the
# `--composition-dir .` form (CI passes this with CWD == the env dir)
# resolves the same way as the absolute form. Otherwise basename "." is "."
# and `cd ./../..` walks up from CWD, breaking the parent-layout mirror.
COMPOSITION_DIR_ABS=$(cd "$COMPOSITION_DIR" && pwd)
COMPOSITION_NAME=$(basename "$COMPOSITION_DIR_ABS")
COMPOSITION_PARENT=$(dirname "$(dirname "$COMPOSITION_DIR_ABS")")
COMPOSITION_WORK="$WORK_DIR/environments/$COMPOSITION_NAME"

mkdir -p "$COMPOSITION_WORK"
cp -R "$COMPOSITION_DIR"/. "$COMPOSITION_WORK"/
# Strip any pre-existing .terraform directory so we get a clean resolution.
rm -rf "$COMPOSITION_WORK/.terraform"

ln -s "$COMPOSITION_PARENT/modules" "$WORK_DIR/modules"

(
  cd "$COMPOSITION_WORK"
  # -backend=false keeps the check fast and avoids requiring backend creds.
  tofu init -backend=false -upgrade=false -input=false >/dev/null
)

RESOLVED_SHA=$(shasum -a 256 "$COMPOSITION_WORK/.terraform.lock.hcl" | awk '{print $1}')

if [[ "$EXPECTED_SHA" != "$RESOLVED_SHA" ]]; then
  echo "$RULE_ID FAIL: provider lockfile drift detected — expected $EXPECTED_SHA, got $RESOLVED_SHA" >&2
  echo "    Run 'tofu init -upgrade' in $COMPOSITION_DIR and commit the updated .terraform.lock.hcl" >&2
  exit 1
fi

echo "$RULE_ID: PASS"
exit 0
