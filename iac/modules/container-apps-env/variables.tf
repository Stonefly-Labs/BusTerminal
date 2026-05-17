variable "name" {
  description = "Container Apps Environment name."
  type        = string
}

variable "resource_group_name" {
  description = "Resource group hosting the environment."
  type        = string
}

variable "location" {
  description = "Azure region for the environment."
  type        = string
}

variable "log_analytics_workspace_name" {
  description = "Log Analytics Workspace name receiving environment logs."
  type        = string
}

variable "log_analytics_workspace_resource_group" {
  description = "Resource group of the Log Analytics Workspace."
  type        = string
}

variable "zone_redundancy_enabled" {
  description = "Enable zone redundancy. Defaults to false for the dev environment; flip on in prod."
  type        = bool
  default     = false
}

variable "tags" {
  description = "Tags applied to the environment."
  type        = map(string)
  default     = {}
}
