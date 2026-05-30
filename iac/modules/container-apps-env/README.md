# Container Apps Environment module

Wraps the Azure Verified Module
[`Azure/avm-res-app-managedenvironment/azurerm` v0.4.0](https://registry.terraform.io/modules/Azure/avm-res-app-managedenvironment/azurerm/0.4.0)
to provision a Container Apps Environment with:

- Log Analytics Workspace wiring via the AVM's
  `log_analytics_workspace_customer_id` / `primary_shared_key` inputs
- `allLogs`-only diagnostic forwarding (Q5c / BT-IAC-003) — the AVM's
  default `AllMetrics` block is dropped via `metric_categories = []`
- Optional zone redundancy (test/prod) per FR-029
- Optional VNet-bound infrastructure subnet (private ACE) per FR-031

Spec 002 / Spec 005 — the runtime environment for BusTerminal's
Container Apps + containerized Azure Functions.

<!-- BEGIN_TF_DOCS -->
## Resources

| Name | Type |
| ---- | ---- |
| [azurerm_log_analytics_workspace.this](https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs/data-sources/log_analytics_workspace) | data source |

## Inputs

| Name | Description | Type | Default | Required |
| ---- | ----------- | ---- | ------- | :------: |
| <a name="input_location"></a> [location](#input\_location) | Azure region for the environment. | `string` | n/a | yes |
| <a name="input_log_analytics_workspace_name"></a> [log\_analytics\_workspace\_name](#input\_log\_analytics\_workspace\_name) | Log Analytics Workspace name receiving environment logs. | `string` | n/a | yes |
| <a name="input_log_analytics_workspace_resource_group"></a> [log\_analytics\_workspace\_resource\_group](#input\_log\_analytics\_workspace\_resource\_group) | Resource group of the Log Analytics Workspace. | `string` | n/a | yes |
| <a name="input_name"></a> [name](#input\_name) | Container Apps Environment name. | `string` | n/a | yes |
| <a name="input_resource_group_name"></a> [resource\_group\_name](#input\_resource\_group\_name) | Resource group hosting the environment. | `string` | n/a | yes |
| <a name="input_tags"></a> [tags](#input\_tags) | Tags applied to the environment. | `map(string)` | `{}` | no |
| <a name="input_zone_redundancy_enabled"></a> [zone\_redundancy\_enabled](#input\_zone\_redundancy\_enabled) | Enable zone redundancy. Defaults to false for the dev environment; flip on in prod. | `bool` | `false` | no |

## Outputs

| Name | Description |
| ---- | ----------- |
| <a name="output_default_domain"></a> [default\_domain](#output\_default\_domain) | Default DNS suffix assigned by Azure — workloads land at `<app-name>.<default_domain>`. |
| <a name="output_id"></a> [id](#output\_id) | Resource ID of the Container Apps Environment. |
| <a name="output_name"></a> [name](#output\_name) | Environment name. |
<!-- END_TF_DOCS -->
