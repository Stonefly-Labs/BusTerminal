#!/usr/bin/env bash
# Spec 008 / T155 + SC-007. Hard-fail CI on any Azure Service Bus credential
# pattern in production code. BusTerminal is OAuth2 / Managed-Identity only —
# connection strings, shared-access keys, and shared-access signatures must
# never appear in shipped code.
#
# Patterns covered:
#   - Endpoint=sb://               (Service Bus connection-string prefix)
#   - SharedAccessKey=             (SAS key fragment)
#   - SharedAccessKeyName=         (SAS key-name fragment)
#   - SharedAccessSignature=       (SAS token fragment)
#
# Scanned file types:  .cs .ts .tsx .tf .json .yaml .yml
#   - Markdown is deliberately omitted: design specs and policy docs describe
#     the patterns themselves (e.g., spec-005's policy-rules.md, this spec's
#     tasks.md). Markdown lives outside the runtime surface.
# Excluded directories: node_modules, obj, bin, .git, .terraform, specs
#   - `specs/` is excluded for the same reason: spec artifacts may name the
#     patterns descriptively (in backticks or schema enums).
# Excluded files: *.stories.tsx
#   - Storybook fixtures show example connection strings for visual
#     documentation; they ship to the storybook bundle only, never to prod.
#
# Exit codes: 0 = clean, 1 = matches found (fails the workflow step).

set -euo pipefail

PATTERN='Endpoint=sb://|SharedAccessKey=|SharedAccessKeyName=|SharedAccessSignature='

# git ls-files keeps the scan honest with the actual repo contents — local
# build/plan artifacts (e.g., iac/environments/*/tfplan.json) are gitignored
# and stay out of CI, so they stay out of this scan too.
FILES=$(git ls-files -- \
    '*.cs' '*.ts' '*.tsx' '*.tf' '*.json' '*.yaml' '*.yml' \
    ':!:specs/**' \
    ':!:scripts/check-no-servicebus-credentials.sh' \
    ':!:**/*.stories.tsx')

if [[ -z "${FILES}" ]]; then
    echo "OK: no in-scope files to scan."
    exit 0
fi

MATCHES=$(printf '%s\n' "${FILES}" | xargs grep -InE "${PATTERN}" 2>/dev/null || true)

if [[ -n "${MATCHES}" ]]; then
    echo "ERROR: Service Bus credential pattern found in source tree (SC-007 / FR-033):" >&2
    echo >&2
    echo "${MATCHES}" >&2
    echo >&2
    echo "BusTerminal must NEVER ship Service Bus connection strings, SAS keys, or SAS tokens." >&2
    echo "Use OAuth2 / Managed Identity instead. See specs/008-namespace-onboarding/spec.md FR-017, FR-033." >&2
    exit 1
fi

echo "OK: no Service Bus credentials found in source tree."
