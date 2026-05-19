output "resource_group_name" {
  description = "Resource group hosting the dev environment."
  value       = azurerm_resource_group.this.name
}

output "frontend_fqdn" {
  description = "HTTPS URL of the frontend Container App."
  value       = module.frontend_app.fqdn_url
}

output "backend_fqdn" {
  description = "HTTPS URL of the backend Container App."
  value       = module.backend_app.fqdn_url
}

output "container_registry_login_server" {
  description = "ACR login server hostname — used by the pipeline as the image prefix."
  value       = module.container_registry.login_server
}

output "container_registry_name" {
  description = "ACR name (no domain suffix) — used by the pipeline for `az acr login`."
  value       = module.container_registry.name
}

output "container_apps_environment_default_domain" {
  description = "Default DNS suffix assigned by the Container Apps Environment."
  value       = module.container_apps_env.default_domain
}

output "key_vault_uri" {
  description = "Key Vault URI — workloads consume secrets from this vault."
  value       = module.keyvault.uri
}

output "log_analytics_workspace_id" {
  description = "Resource ID of the Log Analytics Workspace receiving all diagnostic logs."
  value       = module.monitoring.log_analytics_workspace_id
}

output "application_insights_connection_string_secret_uri" {
  description = "Versionless Key Vault secret URI exposing the Application Insights connection string for workload consumption."
  value       = azurerm_key_vault_secret.app_insights_connection_string.versionless_id
}

output "workload_identity_client_id" {
  description = "Client ID of the workload user-assigned managed identity. Workloads use this to acquire tokens for Key Vault, ACR, etc."
  value       = module.workload_identity.client_id
}

output "backend_image_in_use" {
  description = "Backend container image currently applied to state. The CD pipeline reads this on subsequent runs so the infra-only apply phase keeps the running revision intact while ACR is being re-pushed."
  value       = var.backend_image
}

output "frontend_image_in_use" {
  description = "Frontend container image currently applied to state. Counterpart to backend_image_in_use."
  value       = var.frontend_image
}
