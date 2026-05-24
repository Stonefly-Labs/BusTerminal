#!/usr/bin/env bash
# Lint check: env-level `iac/environments/*/main.tf` files MUST NOT declare
# inline IAM resources. Identity / federation / RBAC grants are composed
# exclusively from the modules under `iac/modules/`:
#
#   - `azurerm_user_assigned_identity`          → `modules/workload-identity`
#   - `azurerm_role_assignment`                 → `modules/workload-identity`
#                                                 (`assigned_azure_rbac` input)
#   - `azurerm_federated_identity_credential`   → `modules/federated-credential`
#   - `azuread_application_federated_identity_credential`  (none yet — sibling
#                                                 module follows when needed)
#
# Spec 003 § US5 / T078. Future-proofs the "no inline IAM in env composition"
# stance for every workload added after this slice.
#
# Allowlisted entries below are documented exceptions inherited from spec 002.
# Add NEW entries only with a one-line rationale in the comment alongside.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

# Resource types forbidden at env level (anywhere outside a module block).
DENYLIST=(
  "azurerm_role_assignment"
  "azurerm_user_assigned_identity"
  "azurerm_federated_identity_credential"
  "azuread_application_federated_identity_credential"
)

# Fully-qualified addresses (`<type>.<name>`) that may remain inline despite
# the denylist. Keep small; each entry should be obviously env-specific and
# unmodulable, with the reason captured here.
ALLOWLIST=(
  # The pipeline MI's data-plane KV grant. Scope is the env RG, which only
  # exists in the env composition — the workload-identity module is parented
  # on a different identity (the workload MI), so this can't be folded in.
  "azurerm_role_assignment.pipeline_kv_secrets_officer"

  # Standing operator access for `kv_operator_object_ids`. Per-env operator
  # set — not a workload grant, not a fit for the workload-identity module.
  "azurerm_role_assignment.operator_kv_secrets_officer"
)

# `iac/environments/<env>/main.tf` only — submodules + helper files are
# unaffected. -maxdepth 2 keeps the scan to direct env children. Written for
# bash 3.2 compatibility (macOS system bash) — no mapfile, no readarray.
FILES=()
while IFS= read -r path; do
  FILES+=("$path")
done < <(find iac/environments -mindepth 2 -maxdepth 2 -name 'main.tf' -type f | sort)

if [[ ${#FILES[@]} -eq 0 ]]; then
  echo "lint-iac-inline-iam: no env main.tf files found under iac/environments/*/" >&2
  exit 0
fi

FAIL=0

for f in "${FILES[@]}"; do
  # Match top-level `resource "TYPE" "NAME" {` declarations (allows leading
  # whitespace; HCL has no syntactic enforcement of left-margin start, and
  # `tofu fmt` produces zero-indent top-level blocks anyway, but we're
  # tolerant).
  while IFS= read -r line; do
    lineno="${line%%:*}"
    rest="${line#*:}"
    # Extract <type>.<name>
    addr=$(printf '%s' "$rest" | sed -E 's/^[[:space:]]*resource[[:space:]]+"([^"]+)"[[:space:]]+"([^"]+)".*/\1.\2/')

    # Is the type even in the denylist?
    type="${addr%%.*}"
    deny=0
    for d in "${DENYLIST[@]}"; do
      [[ "$type" == "$d" ]] && deny=1 && break
    done
    [[ $deny -eq 0 ]] && continue

    # Allowlisted?
    allowed=0
    for a in "${ALLOWLIST[@]}"; do
      [[ "$addr" == "$a" ]] && allowed=1 && break
    done
    if [[ $allowed -eq 1 ]]; then
      continue
    fi

    echo "::error file=${f},line=${lineno}::Inline IAM resource '${addr}' is forbidden at the env-composition layer. Use the matching module under iac/modules/ (see scripts/lint-iac-inline-iam.sh header). To add a documented exception, append to ALLOWLIST in that script with a rationale."
    FAIL=1
  done < <(grep -nE '^[[:space:]]*resource[[:space:]]+"[^"]+"[[:space:]]+"[^"]+"' "$f" || true)
done

if [[ $FAIL -ne 0 ]]; then
  echo "lint-iac-inline-iam: found forbidden inline IAM resource(s). See ::error lines above." >&2
  exit 1
fi

echo "lint-iac-inline-iam: OK — no forbidden inline IAM resources in iac/environments/*/main.tf"
