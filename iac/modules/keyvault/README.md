# Key Vault module

Wraps the Azure Verified Module
[`Azure/avm-res-keyvault-vault/azurerm` v0.10.0](https://registry.terraform.io/modules/Azure/avm-res-keyvault-vault/azurerm/0.10.0)
to provision an Azure Key Vault with:

- RBAC authorization (no access policies) — FR-015
- Per-env purge protection + soft-delete retention (FR-019; US7 / T122).
  Dev defaults `true` / `90`; test/prod default `true` / `90`. **Azure
  does not permit disabling purge protection once enabled** — flipping
  `purge_protection_enabled` from true to false on an in-state vault
  will be rejected at apply time.
- `allLogs`-only diagnostic forwarding (Q5c / BT-IAC-003)
- Optional private endpoint via the project's `private-endpoint` wrapper

Spec 002 / Spec 005 / US7 — the foundation slice's secrets surface;
holds the App Insights connection string (consumed by the frontend
runtime) and any future workload secrets.

<!-- BEGIN_TF_DOCS -->
## Resources

| Name | Type |
| ---- | ---- |
| [terraform_data.pe_validation](https://registry.terraform.io/providers/hashicorp/terraform/latest/docs/resources/data) | resource |
| [azurerm_client_config.current](https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs/data-sources/client_config) | data source |

## Inputs

| Name | Description | Type | Default | Required |
| ---- | ----------- | ---- | ------- | :------: |
| <a name="input_location"></a> [location](#input\_location) | Azure region for the Key Vault. | `string` | n/a | yes |
| <a name="input_log_analytics_workspace_id"></a> [log\_analytics\_workspace\_id](#input\_log\_analytics\_workspace\_id) | Log Analytics Workspace ID for diagnostic settings. Required — every Key Vault we provision routes diagnostics to the solution LAW per constitutional policy. | `string` | n/a | yes |
| <a name="input_name"></a> [name](#input\_name) | Globally unique Key Vault name. 3-24 alphanumeric/hyphen characters. | `string` | n/a | yes |
| <a name="input_resource_group_name"></a> [resource\_group\_name](#input\_resource\_group\_name) | Resource group hosting the Key Vault. | `string` | n/a | yes |
| <a name="input_private_dns_zone_id"></a> [private\_dns\_zone\_id](#input\_private\_dns\_zone\_id) | Private DNS zone ID for `privatelink.vaultcore.azure.net`. Required when private\_endpoint\_enabled = true. | `string` | `null` | no |
| <a name="input_private_endpoint_enabled"></a> [private\_endpoint\_enabled](#input\_private\_endpoint\_enabled) | Plan-time bool toggling the conditional private-endpoint child module.<br/>Required as a separate variable from `private_endpoint_subnet_id`<br/>because the subnet ID is sourced from the networking module's output,<br/>which is "known after apply" — using a nullable string in the `count`<br/>expression breaks plan with "Invalid count argument: count value<br/>depends on resource attributes that cannot be determined until apply".<br/>The env composition passes a literal bool here (`var.private_endpoints_enabled`)<br/>so plan can statically resolve the count. | `bool` | `false` | no |
| <a name="input_private_endpoint_subnet_id"></a> [private\_endpoint\_subnet\_id](#input\_private\_endpoint\_subnet\_id) | Subnet ID for the Key Vault private endpoint. Required when private\_endpoint\_enabled = true. Bound to the `vault` subresource via the project's private-endpoint module. | `string` | `null` | no |
| <a name="input_public_network_access_enabled"></a> [public\_network\_access\_enabled](#input\_public\_network\_access\_enabled) | When true, the Key Vault accepts traffic from the internet (gated by RBAC). When false, requires private endpoints. Defaults to true for the foundation slice. | `bool` | `true` | no |
| <a name="input_purge_protection_enabled"></a> [purge\_protection\_enabled](#input\_purge\_protection\_enabled) | Enable Azure Key Vault purge protection. Once enabled, Azure does not allow disabling. Dev composition may set false on a fresh vault; test/prod default true per FR-019. | `bool` | `true` | no |
| <a name="input_soft_delete_retention_days"></a> [soft\_delete\_retention\_days](#input\_soft\_delete\_retention\_days) | Soft-delete retention window in days. Azure range 7-90. Dev composition may set 7; test/prod default 90 per FR-019. | `number` | `90` | no |
| <a name="input_tags"></a> [tags](#input\_tags) | Tags applied to the Key Vault. | `map(string)` | `{}` | no |

## Outputs

| Name | Description |
| ---- | ----------- |
| <a name="output_id"></a> [id](#output\_id) | Resource ID of the Key Vault. |
| <a name="output_name"></a> [name](#output\_name) | Key Vault name. |
| <a name="output_private_endpoint_id"></a> [private\_endpoint\_id](#output\_private\_endpoint\_id) | Resource ID of the Key Vault PE. Null when no PE is provisioned. |
| <a name="output_uri"></a> [uri](#output\_uri) | Key Vault DNS endpoint (vault URI) — set this as `AZURE_KEY_VAULT_URI` on workload containers. |
<!-- END_TF_DOCS -->
