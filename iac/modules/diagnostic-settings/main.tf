resource "azurerm_monitor_diagnostic_setting" "this" {
  name                       = var.name
  target_resource_id         = var.target_resource_id
  log_analytics_workspace_id = var.log_analytics_workspace_id

  enabled_log {
    category_group = "allLogs"
  }

  # Spec 005 / Q5c — metrics stay in Azure Monitor's native metric store and
  # are NOT forwarded to Log Analytics. The empty dynamic block explicitly
  # emits zero `enabled_metric` blocks; omitting the block entirely is NOT
  # sufficient because `enabled_metric` is Optional+Computed in the azurerm
  # v4 provider, so the provider preserves whatever the existing state had
  # (including the historical `metric { category = "AllMetrics" }` blocks
  # that azurerm v4 migrates into `enabled_metric` on read). The empty
  # for_each is the canonical way to tell the provider "I want zero of
  # these blocks", which makes the BT-IAC-003 policy gate green at plan
  # time for `moved` resources that carried metric blocks in prior state.
  dynamic "enabled_metric" {
    for_each = []
    content {
      category = enabled_metric.value
    }
  }
}
