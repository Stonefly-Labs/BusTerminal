variable "name" {
  description = "Search service name (from naming module). Convention: `srch-<naming_prefix>-<unique_suffix>`. 2-60 lowercase alphanumeric / hyphen."
  type        = string
}

variable "resource_group_name" {
  description = "Resource group hosting the search service (env RG)."
  type        = string
}

variable "location" {
  description = "Azure region for the search service."
  type        = string
}

variable "sku" {
  description = "AI Search SKU. One of: free, basic, standard, standard2, standard3. Per research §4 dev defaults basic, test/prod default standard. `free` is rejected when public access is disabled or a PE is requested (free SKU supports neither)."
  type        = string

  validation {
    condition     = contains(["free", "basic", "standard", "standard2", "standard3"], var.sku)
    error_message = "sku must be one of: free, basic, standard, standard2, standard3."
  }
}

variable "public_network_access_enabled" {
  description = "Per-env public-network access toggle (FR-031). Dev defaults true (Q2c warm PE), test/prod default false."
  type        = bool
}

variable "log_analytics_workspace_id" {
  description = "Log Analytics Workspace ID for diagnostic settings (allLogs only, no metrics — Q5c)."
  type        = string
}

variable "workload_principal_id" {
  description = "Workload UAMI principal (object) ID. Receives `Search Index Data Contributor` scoped to this search service (FR-033)."
  type        = string
}

variable "private_endpoint_subnet_id" {
  description = "Subnet ID for the search service private endpoint. Required when private_endpoints_enabled at the env level."
  type        = string
  default     = null
}

variable "private_dns_zone_id" {
  description = "Private DNS zone ID for `privatelink.search.windows.net`. Required when private_endpoint_subnet_id is set."
  type        = string
  default     = null
}

variable "tags" {
  description = "Tags merged onto the search service and its PE (when provisioned)."
  type        = map(string)
  default     = {}
}
