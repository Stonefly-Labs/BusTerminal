variable "name" {
  description = "Globally unique ACR name. 5-50 alphanumeric characters."
  type        = string
}

variable "resource_group_name" {
  description = "Resource group hosting the registry."
  type        = string
}

variable "location" {
  description = "Azure region for the registry."
  type        = string
}

variable "sku" {
  description = "ACR SKU. Defaults to Premium so geo-replication / private endpoints can be enabled in later slices."
  type        = string
  default     = "Premium"
}

variable "log_analytics_workspace_id" {
  description = "Log Analytics Workspace ID for diagnostic settings. Required — every ACR we provision routes diagnostics to the solution LAW per constitutional policy."
  type        = string
}

variable "public_network_access_enabled" {
  description = "Allow public network access. Defaults to true; flip to false when private endpoints land."
  type        = bool
  default     = true
}

variable "tags" {
  description = "Tags applied to the registry."
  type        = map(string)
  default     = {}
}
