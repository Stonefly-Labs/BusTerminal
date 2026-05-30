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

variable "disable_metric_categories" {
  description = <<-EOT
    Metric categories to explicitly DISABLE via the deprecated `metric` block
    with `enabled = false`. Required for `moved` resources whose prior state
    had a metric category enabled — without an explicit disable, the v4
    provider's Optional+Computed behavior preserves the existing block. The
    default `["AllMetrics"]` works for every Azure Monitor target this module
    fronts in spec 005. Set to `[]` if a future target doesn't accept the
    AllMetrics meta-category. Per Q5c, no metric category should be forwarded
    to Log Analytics, so the only sensible values are subsets of categories
    the target supports.
  EOT
  type        = list(string)
  default     = ["AllMetrics"]
}
