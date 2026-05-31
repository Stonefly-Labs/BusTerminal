resource "azurerm_monitor_diagnostic_setting" "this" {
  name                       = var.name
  target_resource_id         = var.target_resource_id
  log_analytics_workspace_id = var.log_analytics_workspace_id

  enabled_log {
    category_group = "allLogs"
  }

  # Spec 005 / Q5c — metrics stay in Azure Monitor's native metric store and
  # are NOT forwarded to Log Analytics.
  #
  # Both `enabled_metric` (new in azurerm v4) and `metric` (deprecated but
  # still in the v4 schema) are Optional+Computed, which means omitting them
  # in config preserves whatever the existing state had. For resources that
  # carried `metric { category = "AllMetrics", enabled = true }` in pre-spec-
  # 005 state (the inline backend/frontend container-app diagnostic settings),
  # omitting both blocks here leaves them in place — observed in CI as a
  # BT-IAC-003 failure where the plan's `after.enabled_metric` still showed
  # `[{category: "AllMetrics"}]` even though the module declared no metric
  # blocks.
  #
  # The fix: explicitly disable each metric category via the deprecated
  # `metric` block (the only one in the v4 schema that exposes an `enabled`
  # attribute — `enabled_metric` has only `category`, where presence implies
  # enabled). After apply, the Azure Monitor API reflects the category as
  # disabled and the provider clears `enabled_metric` from state, making
  # the BT-IAC-003 check green.
  #
  # `disable_metric_categories` defaults to `["AllMetrics"]` because that's
  # the universal meta-category supported by every Azure Monitor target
  # this module fronts (Cosmos / KV / ACR / Container App / ACE / AI Search
  # / Service Bus / App Insights). Consumers can override to `[]` if a
  # target ever doesn't accept AllMetrics.
  dynamic "metric" {
    for_each = var.disable_metric_categories
    content {
      category = metric.value
      enabled  = false
    }
  }
}
