# `diagnostic-settings` module

Thin wrapper around `azurerm_monitor_diagnostic_setting` that enforces the BusTerminal observability convention introduced by spec 005:

- **`category_group = "allLogs"`** on a single `enabled_log` block — every resource forwards all its supported log categories to the env Log Analytics Workspace.
- **No `enabled_metric` block** — metrics remain in Azure Monitor's native metric store. Forwarding `AllMetrics` to LAW is explicitly forbidden by the Q5c clarification (spec 005, session 2026-05-25); the BT-IAC-003 policy gate enforces this at plan time.

Using this module everywhere instead of inline `azurerm_monitor_diagnostic_setting` blocks makes the convention impossible to violate by accident.

<!-- BEGIN_TF_DOCS -->
## Resources

| Name | Type |
| ---- | ---- |
| [azurerm_monitor_diagnostic_setting.this](https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs/resources/monitor_diagnostic_setting) | resource |

## Inputs

| Name | Description | Type | Default | Required |
| ---- | ----------- | ---- | ------- | :------: |
| <a name="input_log_analytics_workspace_id"></a> [log\_analytics\_workspace\_id](#input\_log\_analytics\_workspace\_id) | Destination Log Analytics Workspace ID for all forwarded logs. | `string` | n/a | yes |
| <a name="input_name"></a> [name](#input\_name) | Diagnostic setting resource name. Convention: `diag-<short-resource-name>`. | `string` | n/a | yes |
| <a name="input_target_resource_id"></a> [target\_resource\_id](#input\_target\_resource\_id) | Azure resource ID of the resource whose diagnostics should be forwarded. | `string` | n/a | yes |
| <a name="input_disable_metric_categories"></a> [disable\_metric\_categories](#input\_disable\_metric\_categories) | Metric categories to explicitly DISABLE via the deprecated `metric` block<br/>with `enabled = false`. Required for `moved` resources whose prior state<br/>had a metric category enabled — without an explicit disable, the v4<br/>provider's Optional+Computed behavior preserves the existing block. The<br/>default `["AllMetrics"]` works for every Azure Monitor target this module<br/>fronts in spec 005. Set to `[]` if a future target doesn't accept the<br/>AllMetrics meta-category. Per Q5c, no metric category should be forwarded<br/>to Log Analytics, so the only sensible values are subsets of categories<br/>the target supports. | `list(string)` | <pre>[<br/>  "AllMetrics"<br/>]</pre> | no |

## Outputs

| Name | Description |
| ---- | ----------- |
| <a name="output_id"></a> [id](#output\_id) | Resource ID of the diagnostic setting. |
<!-- END_TF_DOCS -->

## FR-047 compliance — no PII leakage via `allLogs`

`category_group = "allLogs"` on Azure PaaS resource diagnostics covers only resource-level operational, audit, and platform logs (e.g., Key Vault `AuditEvent`, Cosmos `DataPlaneRequests`, Service Bus `OperationalLogs`). **Application payloads are NOT included** in any `allLogs` category for the resource types BusTerminal provisions. Application telemetry (request bodies, custom dimensions, headers) is the responsibility of the OpenTelemetry exporters in the backend Container App and the App Insights browser SDK; those pipelines have their own PII-suppression rules separate from this module.

Per-service log-category reference for reviewers: <https://learn.microsoft.com/azure/azure-monitor/essentials/resource-logs-categories>.

## Usage

```hcl
module "kv_diagnostics" {
  source = "../../modules/diagnostic-settings"

  name                       = "diag-${module.naming.key_vault_name}"
  target_resource_id         = module.keyvault.id
  log_analytics_workspace_id = module.monitoring.log_analytics_workspace_id
}
```
