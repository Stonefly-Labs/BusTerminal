#!/usr/bin/env bash
# Spec 008 / T155 + SC-007. Hard-fail CI on any Azure Service Bus credential
# pattern in the source tree. BusTerminal is OAuth2 / Managed-Identity only —
# connection strings, shared-access keys, and shared-access signatures must
# never appear in source.
#
# Patterns covered:
#   - Endpoint=sb://               (Service Bus connection-string prefix)
#   - SharedAccessKey=             (SAS key fragment)
#   - SharedAccessKeyName=         (SAS key-name fragment)
#   - SharedAccessSignature=       (SAS token fragment)
#
# Scanned file types:  .cs .ts .tsx .tf .json .yaml .yml .md
# Excluded directories: node_modules, obj, bin, .git, .terraform
#
# Exit codes: 0 = clean, 1 = matches found (fails the workflow step).

set -euo pipefail

PATTERN='Endpoint=sb://|SharedAccessKey=|SharedAccessKeyName=|SharedAccessSignature='

MATCHES=$(grep -RInE "${PATTERN}" \
    --include='*.cs' \
    --include='*.ts' \
    --include='*.tsx' \
    --include='*.tf' \
    --include='*.json' \
    --include='*.yaml' \
    --include='*.yml' \
    --include='*.md' \
    --exclude-dir=node_modules \
    --exclude-dir=obj \
    --exclude-dir=bin \
    --exclude-dir=.git \
    --exclude-dir=.terraform \
    --exclude='check-no-servicebus-credentials.sh' \
    . || true)

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
