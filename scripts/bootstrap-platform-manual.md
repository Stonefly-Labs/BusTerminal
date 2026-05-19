# Manual platform-bootstrap walkthrough (FR-082b)

This walkthrough produces the same end state as
[`iac/platform-bootstrap/`](../iac/platform-bootstrap/README.md) using only the
Azure CLI. Use it when running OpenTofu against your own subscription is not an
option (e.g., subscription-level guardrails block local apply) but you still
need the BusTerminal CD pipeline to function.

Both paths converge on the same artifacts:

- A resource group hosting the OpenTofu state account
- A versioned, HTTPS-only storage account with a `tfstate` blob container
- One user-assigned managed identity per pipeline environment (`dev`, ...) with:
  - A federated identity credential bound to `repo:<org>/BusTerminal:environment:<env>`
  - `Storage Blob Data Contributor` on the state account
  - `Contributor` at the subscription scope (so the pipeline can provision the env)
- GitHub repository / environment variables wired to the above

You can stop after each section to verify and resume later — every step is
idempotent (re-running it produces the same result).

---

## Prerequisites

- Azure CLI ≥ 2.60 (`az version`)
- An Azure subscription where you have `Owner` rights (needed to assign roles)
- The GitHub repository identifier (e.g., `yourorg/BusTerminal`)
- A globally-unique storage account name (3–24 lowercase alphanumeric chars)

Set the values you'll reuse below:

```sh
export SUBSCRIPTION_ID="00000000-0000-0000-0000-000000000000"
export GITHUB_ORG_REPO="yourorg/BusTerminal"
export TFSTATE_RG="rg-busterminal-tfstate"
export TFSTATE_SA="btstate$RANDOM"        # must be globally unique
export TFSTATE_CONTAINER="tfstate"
export LOCATION="eastus2"
export ENVIRONMENTS=("dev")               # add "test", "prod" as needed

az login
az account set --subscription "$SUBSCRIPTION_ID"
```

---

## 1. Resource group

```sh
az group create \
  --name "$TFSTATE_RG" \
  --location "$LOCATION" \
  --tags application=BusTerminal component=platform-bootstrap managed-by=manual cost-center=platform
```

---

## 2. Storage account + container

```sh
az storage account create \
  --name "$TFSTATE_SA" \
  --resource-group "$TFSTATE_RG" \
  --location "$LOCATION" \
  --sku Standard_ZRS \
  --kind StorageV2 \
  --min-tls-version TLS1_2 \
  --allow-blob-public-access false \
  --allow-shared-key-access false \
  --https-only true

az storage account blob-service-properties update \
  --account-name "$TFSTATE_SA" \
  --enable-versioning true \
  --enable-delete-retention true \
  --delete-retention-days 30 \
  --enable-container-delete-retention true \
  --container-delete-retention-days 30

# Containers must be created with Azure AD auth since shared-key is disabled.
az storage container create \
  --name "$TFSTATE_CONTAINER" \
  --account-name "$TFSTATE_SA" \
  --auth-mode login
```

---

## 3. Per-environment pipeline managed identities

Repeat for every environment in `${ENVIRONMENTS[@]}`:

```sh
for ENV in "${ENVIRONMENTS[@]}"; do
  MI_NAME="mi-busterminal-pipeline-$ENV"

  az identity create \
    --name "$MI_NAME" \
    --resource-group "$TFSTATE_RG" \
    --location "$LOCATION" \
    --tags application=BusTerminal component=platform-bootstrap managed-by=manual environment="$ENV"

  CLIENT_ID=$(az identity show --name "$MI_NAME" --resource-group "$TFSTATE_RG" --query clientId -o tsv)
  PRINCIPAL_ID=$(az identity show --name "$MI_NAME" --resource-group "$TFSTATE_RG" --query principalId -o tsv)

  # Federated credential — pipeline jobs that target this GitHub environment
  # exchange an OIDC token for an Azure access token via this binding.
  az identity federated-credential create \
    --identity-name "$MI_NAME" \
    --resource-group "$TFSTATE_RG" \
    --name "github-environment-$ENV" \
    --issuer "https://token.actions.githubusercontent.com" \
    --subject "repo:${GITHUB_ORG_REPO}:environment:${ENV}" \
    --audiences "api://AzureADTokenExchange"

  # State-account access — read/write blobs (the state file) via Entra-issued tokens.
  STORAGE_SCOPE=$(az storage account show --name "$TFSTATE_SA" --resource-group "$TFSTATE_RG" --query id -o tsv)
  az role assignment create \
    --assignee-object-id "$PRINCIPAL_ID" \
    --assignee-principal-type ServicePrincipal \
    --role "Storage Blob Data Contributor" \
    --scope "$STORAGE_SCOPE"

  # Subscription-level Contributor — required so the pipeline can provision the
  # environment's resources. (Equivalent to the role assignment the platform-
  # bootstrap OpenTofu module makes at the subscription scope.)
  az role assignment create \
    --assignee-object-id "$PRINCIPAL_ID" \
    --assignee-principal-type ServicePrincipal \
    --role "Contributor" \
    --scope "/subscriptions/$SUBSCRIPTION_ID"

  echo "Environment '$ENV': CLIENT_ID=$CLIENT_ID"
done
```

---

## 4. Map outputs to GitHub variables

Configure the values below in the GitHub repository. Repository-scoped variables
apply to every workflow; environment-scoped variables apply only to that
deployment environment.

### Repository → Settings → Secrets and variables → Actions → Variables

| Variable | Value |
|----------|-------|
| `AZURE_TENANT_ID` | `$(az account show --query tenantId -o tsv)` |
| `AZURE_SUBSCRIPTION_ID` | `$SUBSCRIPTION_ID` |
| `TFSTATE_RESOURCE_GROUP` | `$TFSTATE_RG` |
| `TFSTATE_STORAGE_ACCOUNT_NAME` | `$TFSTATE_SA` |
| `TFSTATE_CONTAINER_NAME` | `$TFSTATE_CONTAINER` |

### Repository → Settings → Environments → `dev` (and each environment) → Variables

| Variable | Value |
|----------|-------|
| `AZURE_CLIENT_ID` | client ID of `mi-busterminal-pipeline-<env>` (printed above) |
| `UNIQUE_SUFFIX_DEV` | a 4–12-char alphanumeric suffix used in globally-unique resource names |
| `ENTRA_TENANT_ID_DEV` | Microsoft Entra ID tenant ID enforced for sign-in |
| `ENTRA_API_CLIENT_ID_DEV` | application (client) ID of the backend API registration |
| `ENTRA_WEB_CLIENT_ID_DEV` | application (client) ID of the frontend (web) registration |

(For non-`dev` environments, replace the `_DEV` suffix accordingly and create
matching variables in each environment.)

---

## 5. Verify

```sh
# Storage account is reachable, ZRS, versioning on:
az storage account show --name "$TFSTATE_SA" --resource-group "$TFSTATE_RG" \
  --query "{sku:sku.name, https:enableHttpsTrafficOnly, sharedKey:allowSharedKeyAccess}"

# Federated cred is in place for each env:
for ENV in "${ENVIRONMENTS[@]}"; do
  az identity federated-credential list \
    --identity-name "mi-busterminal-pipeline-$ENV" \
    --resource-group "$TFSTATE_RG"
done
```

You can now run the CD pipeline (`.github/workflows/cd-dev.yml`) and the
`tofu init` step will authenticate via OIDC to the storage account you just
provisioned.

---

## Teardown (only if abandoning the bootstrap)

```sh
az group delete --name "$TFSTATE_RG" --yes --no-wait
```

Note: this destroys all environment state. Only run if you intend to start over.
