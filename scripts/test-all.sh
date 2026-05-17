#!/usr/bin/env bash
# Runs the full local test suite — frontend (Vitest), accessibility
# (Playwright a11y project), backend (dotnet test), and IaC (tofu validate).
# Exits non-zero if any step fails.
set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

status=0

bold() { printf '\033[1m%s\033[0m\n' "$*"; }
red()  { printf '\033[31m%s\033[0m\n' "$*"; }
green() { printf '\033[32m%s\033[0m\n' "$*"; }

run_step() {
    local label="$1"; shift
    bold "▶ $label"
    if "$@"; then
        green "✓ $label"
    else
        red "✗ $label"
        status=1
    fi
    echo
}

run_step "web — vitest" pnpm --filter web test
run_step "web — typecheck" pnpm --filter web typecheck

# Accessibility — only when explicitly requested; requires a running dev server
# and Playwright browsers installed. Skipped by default in test-all.
if [ "${RUN_A11Y:-}" = "1" ]; then
    run_step "web — a11y" pnpm --filter web test:a11y
fi

run_step "api — dotnet test" dotnet test api/BusTerminal.slnx --nologo

# IaC — validate the dev environment composition when it exists.
if [ -f "iac/environments/dev/main.tf" ]; then
    run_step "iac/environments/dev — tofu validate" bash -c "cd iac/environments/dev && tofu init -backend=false -input=false >/dev/null && tofu validate"
else
    bold "▶ iac/environments/dev — skipping (composition not present yet)"
    echo
fi

if [ $status -eq 0 ]; then
    bold "All steps passed."
else
    red "One or more steps failed."
fi

exit $status
