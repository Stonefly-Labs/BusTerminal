output "log_analytics_workspace_id" {
  description = "Resource ID of the Log Analytics Workspace. Pass to other modules for diagnostic-settings sinks."
  value       = module.log_analytics.resource_id
}

output "application_insights_id" {
  description = "Resource ID of the Application Insights component."
  value       = module.application_insights.resource_id
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
