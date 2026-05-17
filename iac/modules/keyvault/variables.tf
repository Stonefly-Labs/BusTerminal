variable "name" {
  description = "Globally unique Key Vault name. 3-24 alphanumeric/hyphen characters."
  type        = string
}

variable "resource_group_name" {
  description = "Resource group hosting the Key Vault."
  type        = string
}

variable "location" {
  description = "Azure region for the Key Vault."
  type        = string
}

variable "log_analytics_workspace_id" {
  description = "Log Analytics Workspace ID for diagnostic settings. Set to null to skip diagnostic-settings creation."
  type        = string
  default     = null
}

variable "public_network_access_enabled" {
  description = "When true, the Key Vault accepts traffic from the internet (gated by RBAC). When false, requires private endpoints. Defaults to true for the foundation slice."
  type        = bool
  default     = true
}

variable "tags" {
  description = "Tags applied to the Key Vault."
  type        = map(string)
  default     = {}
}
