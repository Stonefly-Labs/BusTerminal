#!/usr/bin/env bash
# Bootstrap the BusTerminal OpenTofu state backend + per-environment pipeline
# identities by running the `iac/platform-bootstrap/` module.
#
# This is a one-time per-Azure-subscription operation. Re-run with additional
# environments listed in `-e` to add more pipeline identities later.
#
# Requires: tofu, az (logged in to the target subscription).
set -euo pipefail

usage() {
  cat <<EOF
Usage: scripts/bootstrap-platform.sh -s <subscription-id> -r <github-org/repo> -a <tfstate-storage-account-name> [-e <env>[,<env>...]] [-l <location>]

  -s  Azure subscription ID hosting the state account and pipeline identities.
  -r  GitHub repo identifier (e.g., chouse/BusTerminal).
  -a  Globally-unique storage account name for tfstate (3-24 lowercase alphanumeric).
  -e  Comma-separated environment names (default: dev).
  -l  Azure region (default: eastus2).

Example:
  scripts/bootstrap-platform.sh -s 11111111-2222-... -r yourorg/BusTerminal -a btstate0001 -e dev,test
EOF
}

ENVIRONMENTS="dev"
LOCATION="eastus2"

while getopts ":s:r:a:e:l:h" opt; do
  case "$opt" in
    s) SUBSCRIPTION_ID="$OPTARG" ;;
    r) GITHUB_ORG_REPO="$OPTARG" ;;
    a) TFSTATE_STORAGE_ACCOUNT_NAME="$OPTARG" ;;
    e) ENVIRONMENTS="$OPTARG" ;;
    l) LOCATION="$OPTARG" ;;
    h) usage; exit 0 ;;
    *) usage; exit 1 ;;
  esac
done

: "${SUBSCRIPTION_ID:?Missing -s subscription id}"
: "${GITHUB_ORG_REPO:?Missing -r github org/repo}"
: "${TFSTATE_STORAGE_ACCOUNT_NAME:?Missing -a storage account name}"

if ! command -v tofu >/dev/null 2>&1; then
  echo "ERROR: tofu not found on PATH. Install OpenTofu ≥ 1.10 first." >&2
  exit 1
fi
if ! command -v az >/dev/null 2>&1; then
  echo "ERROR: az not found on PATH. Install Azure CLI ≥ 2.60 first." >&2
  exit 1
fi

echo "Verifying Azure CLI session..."
ACCOUNT_JSON=$(az account show --subscription "$SUBSCRIPTION_ID" 2>/dev/null || true)
if [ -z "$ACCOUNT_JSON" ]; then
  echo "Not logged in to subscription $SUBSCRIPTION_ID. Running 'az login'..."
  az login --tenant "$(az account list --query '[0].tenantId' -o tsv 2>/dev/null || true)" >/dev/null
  az account set --subscription "$SUBSCRIPTION_ID"
else
  echo "Using existing Azure CLI session: $(echo "$ACCOUNT_JSON" | sed -n 's/.*"user".*"name": *"\([^"]*\)".*/\1/p' || true)"
fi

# Convert "dev,test" → ["dev","test"] JSON for the tofu var.
ENVS_JSON=$(printf '%s\n' "$ENVIRONMENTS" | awk -F, '{
  printf "["; for(i=1;i<=NF;i++){ if(i>1) printf ","; printf "\"%s\"", $i } printf "]"
}')

cd "$(dirname "$0")/../iac/platform-bootstrap"

echo "Initializing platform-bootstrap module (local state)..."
tofu init -backend=false

echo "Planning..."
tofu plan \
  -var "subscription_id=$SUBSCRIPTION_ID" \
  -var "github_org_repo=$GITHUB_ORG_REPO" \
  -var "tfstate_storage_account_name=$TFSTATE_STORAGE_ACCOUNT_NAME" \
  -var "environments=$ENVS_JSON" \
  -var "location=$LOCATION"

read -rp "Apply this plan? [y/N] " CONFIRM
case "$CONFIRM" in
  y|Y|yes|YES) ;;
  *) echo "Aborted."; exit 1 ;;
esac

tofu apply -auto-approve \
  -var "subscription_id=$SUBSCRIPTION_ID" \
  -var "github_org_repo=$GITHUB_ORG_REPO" \
  -var "tfstate_storage_account_name=$TFSTATE_STORAGE_ACCOUNT_NAME" \
  -var "environments=$ENVS_JSON" \
  -var "location=$LOCATION"

echo
echo "Bootstrap complete. Map the outputs below to GitHub repository / environment variables:"
echo
tofu output -json | python3 -c "
import json,sys
o = json.load(sys.stdin)
for k,v in o.items():
    print(f'  {k} = {json.dumps(v[\"value\"])}')
"
echo
echo "See iac/platform-bootstrap/README.md for the GitHub variable mapping table."
