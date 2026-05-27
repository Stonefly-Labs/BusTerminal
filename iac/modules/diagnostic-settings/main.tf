resource "azurerm_monitor_diagnostic_setting" "this" {
  name                       = var.name
  target_resource_id         = var.target_resource_id
  log_analytics_workspace_id = var.log_analytics_workspace_id

  enabled_log {
    category_group = "allLogs"
  }

  # Intentionally no `enabled_metric` block — per Q5c (spec 005 clarification),
  # metrics stay in Azure Monitor's native metric store and are NOT forwarded
  # to Log Analytics. Adding `enabled_metric` here would violate the convention
  # and trigger the BT-IAC-003 policy gate.
}
