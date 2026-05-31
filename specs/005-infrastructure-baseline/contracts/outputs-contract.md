# Env-Composition Outputs Contract

**Feature**: `005-infrastructure-baseline` | **Date**: 2026-05-25

This is the **binding output surface** every env composition (`iac/environments/<env>/outputs.tf`) MUST emit. Downstream specs and CI workflows consume these outputs by name. Output names are stable across env compositions (dev/test/prod produce the same key set with env-appropriate values).

**Rule (FR-036)**: no output here may contain a secret value. The App Insights connection string is materialized in Key Vault (via `azurerm_key_vault_secret.app_insights_connection_string`) and surfaced to workloads via Container Apps secret references, NEVER as a plaintext Tofu output.

---

## Resource identifiers

| Output | Type | Marked sensitive | Notes |
|---|---|---|---|
| `resource_group_id` | string | no | `azurerm_resource_group.this.id` |
| `resource_group_name` | string | no | |
| `location` | string | no | echo of `var.location` |
| `environment_name` | string | no | echo of `var.environment_name` |

## Networking

| Output | Type | Notes |
|---|---|---|
| `vnet_id` | string | |
| `vnet_name` | string | |
| `subnet_integration_id` | string | For the future CAE retrofit |
| `subnet_private_endpoints_id` | string | |
| `private_dns_zone_ids` | map(string) | Keyed by zone name |

## Compute

| Output | Type | Notes |
|---|---|---|
| `container_apps_environment_id` | string | |
| `container_apps_environment_default_domain` | string | e.g., `purplemoss-xxxx.eastus2.azurecontainerapps.io` |
| `frontend_app_id` | string | |
| `frontend_app_fqdn` | string | The public hostname |
| `backend_app_id` | string | |
| `backend_app_fqdn` | string | |

## Data services

| Output | Type | Notes |
|---|---|---|
| `cosmos_account_id` | string | |
| `cosmos_account_name` | string | |
| `cosmos_account_endpoint` | string | `https://<name>.documents.azure.com:443/` |
| `cosmos_canonical_database_name` | string | echo of `var.canonical_db_name` |
| `cosmos_canonical_database_role_scope` | string | The exact scope path consumers need for adding new `azurerm_cosmosdb_sql_role_assignment` resources (avoids the ARM-vs-data-plane-path trap documented in research §15) |
| `ai_search_id` | string | |
| `ai_search_name` | string | |
| `ai_search_endpoint` | string | `https://<name>.search.windows.net` |
| `service_bus_namespace_id` | string | |
| `service_bus_namespace_name` | string | |
| `service_bus_namespace_fqdn` | string | `<name>.servicebus.windows.net` |

## Secrets

| Output | Type | Notes |
|---|---|---|
| `key_vault_id` | string | |
| `key_vault_name` | string | |
| `key_vault_uri` | string | `https://<name>.vault.azure.net/` |
| `app_insights_connection_string_secret_uri` | string | Versionless KV secret URI; consumed by Container Apps secret references (not the secret value itself) |

## Container Registry

| Output | Type | Notes |
|---|---|---|
| `container_registry_id` | string | |
| `container_registry_login_server` | string | `<name>.azurecr.io` |

## Observability

| Output | Type | Notes |
|---|---|---|
| `log_analytics_workspace_id` | string | |
| `log_analytics_workspace_customer_id` | string | The workspace ID (GUID) — needed by some agents |
| `application_insights_id` | string | |
| `application_insights_app_id` | string | |
| `application_insights_name` | string | For backend env-var assembly: `Authorization=AAD;ClientId=<workload UAMI client_id>` references this resource via the Monitoring Metrics Publisher role |
| `application_insights_connection_string` | string, sensitive | Marked sensitive; consumed only by the in-state KV secret materialization — NOT exposed via `tofu output` plaintext |

## Identity

| Output | Type | Notes |
|---|---|---|
| `workload_uami_id` | string | |
| `workload_uami_client_id` | string | The client ID consumed by `AZURE_CLIENT_ID` env vars and the `APPLICATIONINSIGHTS_AUTHENTICATION_STRING` AAD config |
| `workload_uami_principal_id` | string | For granting additional role assignments in future slices |

## Cross-stack (informational, not redeclared)

| Output | Type | Notes |
|---|---|---|
| (none in env composition) | | The pipeline UAMI ID, federated-credential subject, tfstate storage account ID — all live in `iac/platform-bootstrap/outputs.tf`. Cross-references via Azure resource IDs only. |

---

## Validation

CI policy gate (`iac/policies/check-outputs-no-secrets.sh`) parses `tofu show -json` against the apply state and:
- Asserts every output declared above is present
- Asserts no plaintext output contains values matching common secret patterns (`AccountKey=`, `SharedAccessSignature`, JWT, base64-encoded blobs matching cert/key headers, etc.)
- Asserts `application_insights_connection_string` is marked sensitive (`"sensitive": true` in the JSON state)
