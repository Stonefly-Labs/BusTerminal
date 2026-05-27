output "resource_group_name" {
  description = "Env resource group name (rg-<naming_prefix>)."
  value       = local.names.resource_group_name
}

output "log_analytics_workspace_name" {
  description = "Log Analytics Workspace name (log-<naming_prefix>)."
  value       = local.names.log_analytics_workspace_name
}

output "application_insights_name" {
  description = "Application Insights resource name (appi-<naming_prefix>)."
  value       = local.names.application_insights_name
}

output "key_vault_name" {
  description = "Key Vault name (kv-<naming_prefix>-<unique_suffix>). Globally unique."
  value       = local.names.key_vault_name
}

output "container_registry_name" {
  description = "Azure Container Registry name (acr<naming_prefix><unique_suffix>, hyphens stripped). Globally unique."
  value       = local.names.container_registry_name
}

output "container_apps_env_name" {
  description = "Container Apps Environment name (cae-<naming_prefix>)."
  value       = local.names.container_apps_env_name
}

output "cosmos_account_name" {
  description = "Cosmos DB account name (cosmos-<naming_prefix>-<unique_suffix>). Globally unique."
  value       = local.names.cosmos_account_name
}

output "ai_search_name" {
  description = "Azure AI Search service name (srch-<naming_prefix>-<unique_suffix>). Globally unique."
  value       = local.names.ai_search_name
}

output "service_bus_name" {
  description = "Service Bus namespace name (sbns-<naming_prefix>-<unique_suffix>). Globally unique."
  value       = local.names.service_bus_name
}

output "vnet_name" {
  description = "Virtual network name (vnet-<naming_prefix>)."
  value       = local.names.vnet_name
}

output "workload_uami_name" {
  description = "Workload user-assigned managed identity name (mi-<naming_prefix>-workload)."
  value       = local.names.workload_uami_name
}
