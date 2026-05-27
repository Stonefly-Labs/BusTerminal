variable "name" {
  description = "Diagnostic setting resource name. Convention: `diag-<short-resource-name>`."
  type        = string
}

variable "target_resource_id" {
  description = "Azure resource ID of the resource whose diagnostics should be forwarded."
  type        = string
}

variable "log_analytics_workspace_id" {
  description = "Destination Log Analytics Workspace ID for all forwarded logs."
  type        = string
}
