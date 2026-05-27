# Env-composition outputs — see
# specs/005-infrastructure-baseline/contracts/outputs-contract.md for the
# binding key set. Output names are stable across dev/test/prod compositions;
# downstream specs and CI workflows consume these outputs by name.
#
# Rule (FR-036): no output may contain a secret value. The App Insights
# connection string is marked sensitive AND materialized into Key Vault via
# azurerm_key_vault_secret.app_insights_connection_string; workloads consume
# it via Container Apps secret references, never as a plaintext tofu output.

# -----------------------------------------------------------------------------
# Resource identifiers
# -----------------------------------------------------------------------------

output "resource_group_id" {
  description = "Resource ID of the env resource group."
  value       = azurerm_resource_group.this.id
}

output "resource_group_name" {
  description = "Resource group hosting the dev environment."
  value       = azurerm_resource_group.this.name
}

output "location" {
  description = "Azure region (echo of var.location)."
  value       = var.location
}

output "environment_name" {
  description = "Logical environment name (echo of var.environment_name)."
  value       = var.environment_name
}

# -----------------------------------------------------------------------------
# Networking (spec 005)
# -----------------------------------------------------------------------------

output "vnet_id" {
  description = "Resource ID of the env VNet."
  value       = module.networking.vnet_id
}

output "vnet_name" {
  description = "VNet name."
  value       = module.networking.vnet_name
}

output "subnet_integration_id" {
  description = "Resource ID of `snet-cae-integration` — consumed by the future Container Apps Environment VNet-integration retrofit."
  value       = module.networking.subnet_integration_id
}

output "subnet_private_endpoints_id" {
  description = "Resource ID of `snet-private-endpoints` — hosts every data-service PE."
  value       = module.networking.subnet_private_endpoints_id
}

output "private_dns_zone_ids" {
  description = "Private DNS zone resource IDs keyed by zone name."
  value       = module.networking.private_dns_zone_ids
}

# -----------------------------------------------------------------------------
# Compute (Container Apps)
# -----------------------------------------------------------------------------

output "container_apps_environment_id" {
  description = "Resource ID of the Container Apps Environment."
  value       = module.container_apps_env.id
}

output "container_apps_environment_default_domain" {
  description = "Default DNS suffix assigned by the Container Apps Environment."
  value       = module.container_apps_env.default_domain
}

output "frontend_app_id" {
  description = "Resource ID of the frontend Container App."
  value       = module.frontend_app.id
}

output "frontend_app_fqdn" {
  description = "Frontend Container App public hostname."
  value       = module.frontend_app.fqdn_url
}

output "frontend_fqdn" {
  description = "HTTPS URL of the frontend Container App (alias of frontend_app_fqdn, kept for spec-002 compatibility)."
  value       = module.frontend_app.fqdn_url
}

output "backend_app_id" {
  description = "Resource ID of the backend Container App."
  value       = module.backend_app.id
}

output "backend_app_fqdn" {
  description = "Backend Container App hostname."
  value       = module.backend_app.fqdn_url
}

output "backend_fqdn" {
  description = "HTTPS URL of the backend Container App (alias of backend_app_fqdn, kept for spec-002 compatibility)."
  value       = module.backend_app.fqdn_url
}

# -----------------------------------------------------------------------------
# Data services
# -----------------------------------------------------------------------------

output "cosmos_account_id" {
  description = "Cosmos DB account resource ID."
  value       = module.cosmos_account.account_id
}

output "cosmos_account_name" {
  description = "Cosmos DB account name. Bound to `CosmosOptions.AccountName`."
  value       = module.cosmos_account.account_name
}

output "cosmos_account_endpoint" {
  description = "Cosmos DB account endpoint URL. Bound to `CosmosOptions.Endpoint`."
  value       = module.cosmos_account.account_endpoint
}

output "cosmos_canonical_database_name" {
  description = "Logical SQL database holding canonical resources + change-event log. Bound to `CosmosOptions.Database`."
  value       = module.cosmos_canonical_store.database_name
}

output "cosmos_canonical_database_role_scope" {
  description = "Cosmos data-plane scope path for new `azurerm_cosmosdb_sql_role_assignment` resources targeting the canonical database. Pre-built to avoid the ARM-vs-data-plane-path trap (research §15)."
  value       = module.cosmos_canonical_store.canonical_database_role_scope
}

output "cosmos_canonical_resources_container_name" {
  description = "Container holding resource + relationship documents."
  value       = module.cosmos_canonical_store.resources_container_name
}

output "cosmos_canonical_change_events_container_name" {
  description = "Append-only change-event log container."
  value       = module.cosmos_canonical_store.change_events_container_name
}

output "ai_search_id" {
  description = "AI Search service resource ID."
  value       = module.ai_search.id
}

output "ai_search_name" {
  description = "AI Search service name."
  value       = module.ai_search.name
}

output "ai_search_endpoint" {
  description = "AI Search service endpoint (`https://<name>.search.windows.net`)."
  value       = module.ai_search.endpoint
}

output "service_bus_namespace_id" {
  description = "Service Bus namespace resource ID."
  value       = module.service_bus.id
}

output "service_bus_namespace_name" {
  description = "Service Bus namespace name."
  value       = module.service_bus.name
}

output "service_bus_namespace_fqdn" {
  description = "Service Bus namespace FQDN (`<name>.servicebus.windows.net`)."
  value       = module.service_bus.fqdn
}

# -----------------------------------------------------------------------------
# Secrets
# -----------------------------------------------------------------------------

output "key_vault_id" {
  description = "Key Vault resource ID."
  value       = module.keyvault.id
}

output "key_vault_name" {
  description = "Key Vault name."
  value       = module.keyvault.name
}

output "key_vault_uri" {
  description = "Key Vault URI — workloads consume secrets from this vault."
  value       = module.keyvault.uri
}

output "app_insights_connection_string_secret_uri" {
  description = "Versionless Key Vault secret URI exposing the Application Insights connection string for workload consumption (Container Apps secret reference target)."
  value       = azurerm_key_vault_secret.app_insights_connection_string.versionless_id
}

# Spec-002-era alias retained for callers reading the older name.
output "application_insights_connection_string_secret_uri" {
  description = "Alias for app_insights_connection_string_secret_uri (kept for spec-002 caller compatibility)."
  value       = azurerm_key_vault_secret.app_insights_connection_string.versionless_id
}

# -----------------------------------------------------------------------------
# Container Registry
# -----------------------------------------------------------------------------

output "container_registry_id" {
  description = "ACR resource ID."
  value       = module.container_registry.id
}

output "container_registry_login_server" {
  description = "ACR login server hostname — used by the pipeline as the image prefix."
  value       = module.container_registry.login_server
}

output "container_registry_name" {
  description = "ACR name (no domain suffix) — used by the pipeline for `az acr login`."
  value       = module.container_registry.name
}

# -----------------------------------------------------------------------------
# Observability
# -----------------------------------------------------------------------------

output "log_analytics_workspace_id" {
  description = "Resource ID of the Log Analytics Workspace receiving all diagnostic logs."
  value       = module.monitoring.log_analytics_workspace_id
}

output "log_analytics_workspace_customer_id" {
  description = "LAW customer (workspace) GUID — needed by some agents that authenticate against the workspace by GUID."
  value       = module.monitoring.log_analytics_workspace_customer_id
}

output "application_insights_id" {
  description = "Application Insights resource ID."
  value       = module.monitoring.application_insights_id
}

output "application_insights_app_id" {
  description = "Application Insights `app_id` (a stable GUID identifying the AI component in REST queries)."
  value       = module.monitoring.application_insights_app_id
}

output "application_insights_name" {
  description = "Application Insights resource name — consumed by backend env-var assembly: `APPLICATIONINSIGHTS_AUTHENTICATION_STRING = Authorization=AAD;ClientId=<workload-uami-client-id>` references this resource via the Monitoring Metrics Publisher role."
  value       = module.monitoring.application_insights_name
}

output "application_insights_connection_string" {
  description = "App Insights connection string. Marked sensitive AND consumed only by the in-state KV secret materialization — NOT exposed via `tofu output` plaintext."
  value       = module.monitoring.application_insights_connection_string
  sensitive   = true
}

# -----------------------------------------------------------------------------
# Identity
# -----------------------------------------------------------------------------

output "workload_uami_id" {
  description = "Resource ID of the workload user-assigned managed identity."
  value       = module.workload_identity.id
}

output "workload_uami_client_id" {
  description = "Client ID of the workload UAMI. Consumed by `AZURE_CLIENT_ID` env vars and by the backend's `APPLICATIONINSIGHTS_AUTHENTICATION_STRING` AAD config."
  value       = module.workload_identity.client_id
}

output "workload_uami_principal_id" {
  description = "Principal (object) ID of the workload UAMI. Used by future specs adding role assignments scoped to this identity."
  value       = module.workload_identity.principal_id
}

# Spec-002-era alias retained for callers reading the older name.
output "workload_identity_client_id" {
  description = "Alias for workload_uami_client_id (kept for spec-002 caller compatibility)."
  value       = module.workload_identity.client_id
}

# -----------------------------------------------------------------------------
# Image references (operational — informational only)
# -----------------------------------------------------------------------------

output "backend_image_in_use" {
  description = "Backend container image currently applied to state. The CD pipeline reads this so the infra-only apply phase keeps the running revision intact while ACR is being re-pushed."
  value       = var.backend_image
}

output "frontend_image_in_use" {
  description = "Frontend container image currently applied to state. Counterpart to backend_image_in_use."
  value       = var.frontend_image
}
