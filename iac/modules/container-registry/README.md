# Container Registry module

Wraps the Azure Verified Module
[`Azure/avm-res-containerregistry-registry/azurerm` v0.4.0](https://registry.terraform.io/modules/Azure/avm-res-containerregistry-registry/azurerm/0.4.0)
to provision an Azure Container Registry with:

- `admin_enabled = false` (FR-015 — no inline credentials)
- AAD-only pulls via the workload UAMI's `AcrPull` role assignment, wired
  by the env composition
- `allLogs`-only diagnostic forwarding (Q5c / BT-IAC-003) — the AVM's
  default `AllMetrics` block is dropped via `metric_categories = []`
- Optional private endpoint via the project's `private-endpoint` wrapper

Spec 002 / Spec 005 / US1 — image registry for BusTerminal's container
artifacts; PE-warm in dev per Q2c, locked-down in test/prod.

<!-- BEGIN_TF_DOCS -->
## Resources

| Name | Type |
| ---- | ---- |
| [terraform_data.pe_validation](https://registry.terraform.io/providers/hashicorp/terraform/latest/docs/resources/data) | resource |

## Inputs

| Name | Description | Type | Default | Required |
| ---- | ----------- | ---- | ------- | :------: |
| <a name="input_location"></a> [location](#input\_location) | Azure region for the registry. | `string` | n/a | yes |
| <a name="input_log_analytics_workspace_id"></a> [log\_analytics\_workspace\_id](#input\_log\_analytics\_workspace\_id) | Log Analytics Workspace ID for diagnostic settings. Required — every ACR we provision routes diagnostics to the solution LAW per constitutional policy. | `string` | n/a | yes |
| <a name="input_name"></a> [name](#input\_name) | Globally unique ACR name. 5-50 alphanumeric characters. | `string` | n/a | yes |
| <a name="input_resource_group_name"></a> [resource\_group\_name](#input\_resource\_group\_name) | Resource group hosting the registry. | `string` | n/a | yes |
| <a name="input_private_dns_zone_id"></a> [private\_dns\_zone\_id](#input\_private\_dns\_zone\_id) | Private DNS zone ID for `privatelink.azurecr.io`. Required when private\_endpoint\_subnet\_id is set. | `string` | `null` | no |
| <a name="input_private_endpoint_subnet_id"></a> [private\_endpoint\_subnet\_id](#input\_private\_endpoint\_subnet\_id) | Subnet ID for the container-registry private endpoint. When set, provisions a PE bound to the `registry` subresource. Requires Premium SKU (default). | `string` | `null` | no |
| <a name="input_public_network_access_enabled"></a> [public\_network\_access\_enabled](#input\_public\_network\_access\_enabled) | Allow public network access. Defaults to true; flip to false when private endpoints land. | `bool` | `true` | no |
| <a name="input_sku"></a> [sku](#input\_sku) | ACR SKU. Defaults to Premium so geo-replication / private endpoints can be enabled in later slices. | `string` | `"Premium"` | no |
| <a name="input_tags"></a> [tags](#input\_tags) | Tags applied to the registry. | `map(string)` | `{}` | no |

## Outputs

| Name | Description |
| ---- | ----------- |
| <a name="output_id"></a> [id](#output\_id) | Resource ID of the registry. |
| <a name="output_login_server"></a> [login\_server](#output\_login\_server) | Login server hostname — used as the image prefix (e.g., `<login_server>/busterminal/api:<sha>`). |
| <a name="output_name"></a> [name](#output\_name) | Registry name. |
| <a name="output_private_endpoint_id"></a> [private\_endpoint\_id](#output\_private\_endpoint\_id) | Resource ID of the ACR PE. Null when no PE is provisioned. |
<!-- END_TF_DOCS -->
