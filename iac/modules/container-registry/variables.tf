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

# Spec 005 — private-endpoint extension (T057–T058). Defaults preserve existing
# (pre-spec-005) behavior: no PE provisioned. PE requires Premium SKU
# (var.sku defaults to Premium so no SKU validation needed here).

variable "private_endpoint_subnet_id" {
  description = "Subnet ID for the container-registry private endpoint. When set, provisions a PE bound to the `registry` subresource. Requires Premium SKU (default)."
  type        = string
  default     = null
}

variable "private_dns_zone_id" {
  description = "Private DNS zone ID for `privatelink.azurecr.io`. Required when private_endpoint_subnet_id is set."
  type        = string
  default     = null
}
