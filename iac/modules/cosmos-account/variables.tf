variable "name" {
  description = "Cosmos DB account name. Must be globally unique, 3-44 lowercase alphanumeric / hyphen chars."
  type        = string

  validation {
    condition     = can(regex("^[a-z0-9-]{3,44}$", var.name))
    error_message = "Cosmos DB account name must be 3-44 lowercase alphanumeric or hyphen characters."
  }
}

variable "resource_group_name" {
  description = "Resource group hosting the Cosmos DB account."
  type        = string
}

variable "location" {
  description = "Azure region for the Cosmos DB account."
  type        = string
}

variable "tags" {
  description = "Tags applied to the Cosmos DB account."
  type        = map(string)
  default     = {}
}

variable "log_analytics_workspace_id" {
  description = <<-EOT
    Optional Log Analytics Workspace resource id for diagnostic settings. When
    set, a diagnostic-setting routes allLogs + AllMetrics to the workspace
    (Constitution §Operational Excellence — every Azure resource routes
    diagnostic logs + AllMetrics to the LAW). Pass null to skip.
  EOT
  type        = string
  default     = null
}
