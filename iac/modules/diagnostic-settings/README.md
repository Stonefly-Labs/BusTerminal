# `diagnostic-settings` module

Thin wrapper around `azurerm_monitor_diagnostic_setting` that enforces the BusTerminal observability convention introduced by spec 005:

- **`category_group = "allLogs"`** on a single `enabled_log` block — every resource forwards all its supported log categories to the env Log Analytics Workspace.
- **No `enabled_metric` block** — metrics remain in Azure Monitor's native metric store. Forwarding `AllMetrics` to LAW is explicitly forbidden by the Q5c clarification (spec 005, session 2026-05-25); the BT-IAC-003 policy gate enforces this at plan time.

Using this module everywhere instead of inline `azurerm_monitor_diagnostic_setting` blocks makes the convention impossible to violate by accident.

## Inputs

| Name | Type | Required | Description |
|---|---|---|---|
| `name` | string | yes | Diagnostic setting resource name. Convention: `diag-<short-resource-name>`. |
| `target_resource_id` | string | yes | Azure resource ID of the resource whose diagnostics should be forwarded. |
| `log_analytics_workspace_id` | string | yes | Destination Log Analytics Workspace ID. |

## Outputs

| Name | Description |
|---|---|
| `id` | Resource ID of the diagnostic setting. |

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
