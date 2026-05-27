output "log_analytics_workspace_id" {
  description = "Resource ID of the Log Analytics Workspace. Pass to other modules for diagnostic-settings sinks."
  value       = module.log_analytics.resource_id
}

output "log_analytics_workspace_customer_id" {
  description = "LAW customer (workspace) GUID — needed by some agents that authenticate against the workspace by GUID."
  value       = module.log_analytics.resource.workspace_id
}

output "application_insights_id" {
  description = "Resource ID of the Application Insights component."
  value       = module.application_insights.resource_id
}

output "application_insights_app_id" {
  description = "Application Insights `app_id` — a stable GUID identifying the AI component in the REST query API."
  value       = module.application_insights.resource.app_id
}

output "application_insights_name" {
  description = "Application Insights resource name (echo of var.application_insights_name)."
  value       = var.application_insights_name
}

output "application_insights_connection_string" {
  description = "Application Insights connection string. Sensitive — prefer consuming via the Key Vault reference exposed by `app_insights_connection_string_secret_uri`."
  value       = module.application_insights.connection_string
  sensitive   = true
}

output "app_insights_connection_string_secret_uri" {
  description = "Key Vault secret URI exposing the connection string (null when no Key Vault was passed)."
  value = (
    var.key_vault_id != null
    ? azurerm_key_vault_secret.app_insights_connection_string[0].versionless_id
    : null
  )
}
