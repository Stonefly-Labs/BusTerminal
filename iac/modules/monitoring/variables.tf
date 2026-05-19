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
