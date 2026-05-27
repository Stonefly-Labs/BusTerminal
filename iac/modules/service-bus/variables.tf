variable "name" {
  description = "Service Bus namespace name (from naming module). Convention: `sbns-<naming_prefix>-<unique_suffix>`. 6-50 alphanumeric / hyphen; must start with a letter and end with a letter or digit. Globally unique."
  type        = string
}

variable "resource_group_name" {
  description = "Resource group hosting the namespace (env RG)."
  type        = string
}

variable "location" {
  description = "Azure region for the namespace."
  type        = string
}

variable "sku" {
  description = "Namespace SKU. One of: Standard, Premium. Per research §3 dev defaults Standard (~$10/mo), test/prod default Premium (PE-capable, ~$667+/mo). Basic is rejected — no topics/subscriptions support."
  type        = string

  validation {
    condition     = contains(["Standard", "Premium"], var.sku)
    error_message = "sku must be one of: Standard, Premium. Basic is rejected (no topics/subscriptions support)."
  }
}

variable "capacity" {
  description = "Premium messaging units. Required when sku=\"Premium\"; ignored otherwise. One of 1, 2, 4, 8, 16."
  type        = number
  default     = null

  validation {
    condition     = var.capacity == null || contains([1, 2, 4, 8, 16], var.capacity)
    error_message = "capacity must be null (Standard SKU) or one of: 1, 2, 4, 8, 16 (Premium SKU)."
  }
}

variable "public_network_access_enabled" {
  description = "Per-env public-network access toggle (FR-031). Dev defaults true (Q2c warm), test/prod default false."
  type        = bool
}

variable "log_analytics_workspace_id" {
  description = "Log Analytics Workspace ID for diagnostic settings (allLogs only, no metrics — Q5c)."
  type        = string
}

variable "workload_principal_id" {
  description = "Workload UAMI principal (object) ID. Receives `Azure Service Bus Data Sender` AND `Azure Service Bus Data Receiver` scoped to this namespace (FR-033)."
  type        = string
}

variable "private_endpoint_subnet_id" {
  description = "Subnet ID for the namespace private endpoint. Required when private endpoint is desired AND sku=Premium. The env composition is responsible for nulling this for Standard SKU (the module precondition rejects non-null PE inputs with Standard)."
  type        = string
  default     = null
}

variable "private_dns_zone_id" {
  description = "Private DNS zone ID for `privatelink.servicebus.windows.net`. Required when private_endpoint_subnet_id is set."
  type        = string
  default     = null
}

variable "tags" {
  description = "Tags merged onto the namespace and its PE (when provisioned)."
  type        = map(string)
  default     = {}
}
