#!/usr/bin/env bash
# Lint check: IaC HCL under `iac/` MUST NOT contain inline Azure-service
# static credentials. The "no static credentials" stance (FR-015 / slice 003)
# applies platform-wide; this guard future-proofs it as later slices introduce
# Cosmos DB, AI Search, Storage, OpenAI, Service Bus, and App Configuration
# resources where account keys, SAS tokens, and connection strings would
# otherwise be tempting to embed.
#
# Spec 003 § Polish / T101.
#
# The check is intentionally lexical: it scans for assignment-form occurrences
# of the documented credential-bearing attribute names. Real provisioning
# should expose these as outputs from Azure-Verified-Module-style modules and
# leave the keys in Key Vault / Managed Identity grants — never inline in HCL.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

# Attribute names forbidden from appearing on the LHS of an assignment inside
# `iac/`. Patterns are matched against `<name>\s*=` so they trigger on actual
# resource arguments, not on free-text mentions in comments.
DENYLIST_PATTERN='(AccountKey|SharedAccessKey|connection_string|primary_access_key|secondary_access_key|primary_connection_string|secondary_connection_string|sas_token|shared_access_policy_key)[[:space:]]*='

# Fully-qualified file:line entries that may remain despite the denylist.
# Keep small; each entry must capture *why* the exception is safe (e.g., the
# value is itself sourced from Key Vault / Managed Identity, not from a
# literal).
ALLOWLIST=(
  # (empty — no current exceptions in slice 003)
)

# Scan every .tf and .tf.json file under iac/.
FILES=()
while IFS= read -r path; do
  FILES+=("$path")
done < <(find iac -type f \( -name '*.tf' -o -name '*.tf.json' \) ! -path '*/.terraform/*' | sort)

if [[ ${#FILES[@]} -eq 0 ]]; then
  echo "lint-iac-inline-credentials: no .tf files found under iac/" >&2
  exit 0
fi

FAIL=0

for f in "${FILES[@]}"; do
  # Skip comment-only matches by stripping `#`-line comments before matching.
  # HCL's other comment style (`//`) is rare in this repo but handled too.
  matches=$(grep -nE "$DENYLIST_PATTERN" "$f" \
    | grep -vE '^[[:space:]]*[0-9]+:[[:space:]]*(#|//)' \
    || true)

  [[ -z "$matches" ]] && continue

  while IFS= read -r match; do
    [[ -z "$match" ]] && continue
    lineno="${match%%:*}"
    addr="$f:$lineno"

    allowed=0
    for a in "${ALLOWLIST[@]}"; do
      [[ "$addr" == "$a" ]] && allowed=1 && break
    done

    if [[ $allowed -eq 1 ]]; then
      echo "lint-iac-inline-credentials: ALLOWED $addr ($(printf '%s' "$match" | sed -E 's/^[0-9]+://'))"
      continue
    fi

    echo "::error file=$f,line=$lineno::Inline Azure-service credential detected. Static credentials are prohibited in IaC (FR-015). Move the value behind Key Vault + Managed Identity. Match: $(printf '%s' "$match" | sed -E 's/^[0-9]+://' | tr -d '\r')"
    FAIL=1
  done <<<"$matches"
done

if [[ $FAIL -ne 0 ]]; then
  echo "lint-iac-inline-credentials: FAIL — at least one inline credential pattern detected."
  exit 1
fi

echo "lint-iac-inline-credentials: PASS — no inline Azure-service credentials in iac/."
