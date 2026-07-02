#!/usr/bin/env bash
# Clear the indexer's Cosmos change-feed lease checkpoints.
#
# Deletes every document in the `registry-entities-leases` container so the
# indexer's CosmosDBTrigger (StartFromBeginning = true, see
# api/BusTerminal.Indexer/Functions/RegistryEntityIndexer.cs) replays the
# registry-entities change feed from offset zero on its next start — fully
# re-projecting every registry entity into the AI Search index.
#
# Used by the dev park/unpark flow (env-park-dev.yml): the park apply destroys
# the search service + index and scales the indexer to zero, then this script
# clears the leases. Any later unpark apply recreates the empty index and the
# indexer backfills it automatically. It is also the lease-reset half of the
# spec 009 T114 canonical-rebuild concern (rebuild-search-index.sh).
#
# MUST run while the indexer Container App is scaled to zero (or stopped) —
# deleting leases underneath a live change-feed processor causes lease-ownership
# errors and can double-checkpoint.
#
# Auth: Cosmos data plane via AAD (DefaultAzureCredential — az CLI locally,
# federated OIDC in Actions). The caller needs `Cosmos DB Built-in Data
# Contributor` on the canonical database (the pipeline MI holds it via
# `azurerm_cosmosdb_sql_role_assignment.developer_data_contributor`; local
# principals may need the same grant).
#
# Deleting lease *documents* is safe — they are checkpoint state, not data.
# The container itself stays (prevent_destroy in cosmos-registry-store).

set -euo pipefail

ENV="dev"
RESOURCE_GROUP=""
ACCOUNT=""
DATABASE="busterminal-canonical"
CONTAINER="registry-entities-leases"

usage() {
  cat <<USAGE
Usage: $0 [--env <dev|test|prod>] [--resource-group <rg>] [--account <cosmos-account>] [--database <db>] [--container <leases-container>]

Defaults: --env dev → resource group rg-bt-<env>; the Cosmos account is
auto-discovered as the single account in that resource group. Database
defaults to busterminal-canonical, container to registry-entities-leases.
USAGE
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --env) ENV="${2:-}"; shift 2 ;;
    --resource-group) RESOURCE_GROUP="${2:-}"; shift 2 ;;
    --account) ACCOUNT="${2:-}"; shift 2 ;;
    --database) DATABASE="${2:-}"; shift 2 ;;
    --container) CONTAINER="${2:-}"; shift 2 ;;
    -h|--help) usage; exit 0 ;;
    *) echo "clear-indexer-leases: unknown arg '$1'" >&2; usage >&2; exit 2 ;;
  esac
done

RESOURCE_GROUP="${RESOURCE_GROUP:-rg-bt-$ENV}"

for cmd in az python3; do
  if ! command -v "$cmd" >/dev/null 2>&1; then
    echo "clear-indexer-leases: '$cmd' is required on PATH" >&2
    exit 2
  fi
done

if [[ -z "$ACCOUNT" ]]; then
  echo "==> Discovering Cosmos account in $RESOURCE_GROUP"
  ACCOUNT=$(az cosmosdb list --resource-group "$RESOURCE_GROUP" --query "[0].name" -o tsv)
  if [[ -z "$ACCOUNT" ]]; then
    echo "clear-indexer-leases: no Cosmos account found in $RESOURCE_GROUP" >&2
    exit 1
  fi
fi

ENDPOINT="https://${ACCOUNT}.documents.azure.com:443/"
echo "==> Clearing leases: $ENDPOINT $DATABASE/$CONTAINER"

# azure-cosmos + azure-identity: use the system python if the imports resolve,
# otherwise a throwaway venv (CI runners have neither preinstalled).
PYTHON=python3
if ! "$PYTHON" -c "import azure.cosmos, azure.identity" >/dev/null 2>&1; then
  VENV="${TMPDIR:-/tmp}/bt-clear-leases-venv"
  echo "==> Installing azure-cosmos + azure-identity into $VENV"
  python3 -m venv "$VENV"
  "$VENV/bin/pip" install --quiet --disable-pip-version-check azure-cosmos azure-identity
  PYTHON="$VENV/bin/python"
fi

COSMOS_ENDPOINT="$ENDPOINT" COSMOS_DATABASE="$DATABASE" COSMOS_CONTAINER="$CONTAINER" "$PYTHON" - <<'PY'
import os
from azure.identity import DefaultAzureCredential
from azure.cosmos import CosmosClient

client = CosmosClient(os.environ["COSMOS_ENDPOINT"], credential=DefaultAzureCredential())
container = client.get_database_client(os.environ["COSMOS_DATABASE"]).get_container_client(os.environ["COSMOS_CONTAINER"])

# Lease container partition key is /id, so the document id doubles as its
# partition key value.
ids = [item["id"] for item in container.query_items("SELECT c.id FROM c", enable_cross_partition_query=True)]
for doc_id in ids:
    container.delete_item(item=doc_id, partition_key=doc_id)
print(f"Deleted {len(ids)} lease document(s).")
PY

echo "==> Done. The indexer will replay the change feed from the beginning on next start."
