variable "log_analytics_workspace_name" {
  description = "Name of the Log Analytics Workspace."
  type        = string
}

variable "application_insights_name" {
  description = "Name of the Application Insights component."
  type        = string
}

variable "resource_group_name" {
  description = "Resource group hosting both monitoring resources."
  type        = string
}

variable "location" {
  description = "Azure region for the monitoring resources."
  type        = string
}

variable "retention_in_days" {
  description = "Log Analytics workspace data-retention period in days."
  type        = number
  default     = 30
}

variable "key_vault_id" {
  description = "Optional Key Vault ID where the Application Insights connection string is exposed as a secret for workload consumption. Set to null to skip secret creation."
  type        = string
  default     = null
}

variable "tags" {
  description = "Tags applied to both monitoring resources."
  type        = map(string)
  default     = {}
}

variable "local_authentication_disabled" {
  description = <<-EOT
    Forwarded to `azurerm_application_insights.local_authentication_disabled`
    (via the AVM `local_authentication_disabled` input). Spec 005 / Q1c /
    research §6: MUST remain `false`. The Application Insights JavaScript SDK
    does NOT support Microsoft Entra ingestion authentication
    (https://learn.microsoft.com/azure/azure-monitor/app/azure-ad-authentication#unsupported-scenarios),
    so disabling local auth would break all browser telemetry. The backend
    .NET OpenTelemetry exporter authenticates via AAD using
    `APPLICATIONINSIGHTS_AUTHENTICATION_STRING = "Authorization=AAD;ClientId=..."`,
    which works ALONGSIDE local auth — not as a replacement for it. See
    README.md § Local authentication.
  EOT
  type        = bool
  default     = false
}
