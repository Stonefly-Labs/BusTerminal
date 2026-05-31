# Cosmos DB account module

Provisions the Azure Cosmos DB (NoSQL / SQL API) account that backs the
canonical metadata store (FR-001 / FR-020 / FR-025).

**AVM bypass**: this module uses `azurerm_cosmosdb_account` directly rather
than `Azure/avm-res-documentdb-databaseaccount/azurerm` v0.10.0. The AVM
silently overrides `local_authentication_disabled` to `false` when its
`sql_databases` input is empty, and the project splits database + container
provisioning into the sibling `cosmos-canonical-store` module. Using the
resource directly is the simplest way to honor both the AAD-only constraint
(FR-016) and the multi-module separation the spec calls for. See
`main.tf` for the full rationale and the AVM-issue tracker link.

Spec 004 / Spec 005 / US1 — canonical resource store + change-event log.

- Serverless capacity (dev); production may switch to provisioned throughput
- `local_authentication_disabled = true` (AAD-only; FR-016)
- `lifecycle.prevent_destroy = true` on the account (US7 / T121)
- Optional private endpoint via the project's `private-endpoint` wrapper

<!-- BEGIN_TF_DOCS -->
## Resources

| Name | Type |
| ---- | ---- |
| [azurerm_cosmosdb_account.this](https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs/resources/cosmosdb_account) | resource |
| [terraform_data.pe_validation](https://registry.terraform.io/providers/hashicorp/terraform/latest/docs/resources/data) | resource |

## Inputs

| Name | Description | Type | Default | Required |
| ---- | ----------- | ---- | ------- | :------: |
| <a name="input_location"></a> [location](#input\_location) | Azure region for the Cosmos DB account. | `string` | n/a | yes |
| <a name="input_name"></a> [name](#input\_name) | Cosmos DB account name. Must be globally unique, 3-44 lowercase alphanumeric / hyphen chars. | `string` | n/a | yes |
| <a name="input_resource_group_name"></a> [resource\_group\_name](#input\_resource\_group\_name) | Resource group hosting the Cosmos DB account. | `string` | n/a | yes |
| <a name="input_log_analytics_workspace_id"></a> [log\_analytics\_workspace\_id](#input\_log\_analytics\_workspace\_id) | Optional Log Analytics Workspace resource id for diagnostic settings. When<br/>set, a diagnostic-setting routes allLogs + AllMetrics to the workspace<br/>(Constitution §Operational Excellence — every Azure resource routes<br/>diagnostic logs + AllMetrics to the LAW). Pass null to skip. | `string` | `null` | no |
| <a name="input_private_dns_zone_id"></a> [private\_dns\_zone\_id](#input\_private\_dns\_zone\_id) | Private DNS zone ID for `privatelink.documents.azure.com`. Required when private\_endpoint\_enabled = true. | `string` | `null` | no |
| <a name="input_private_endpoint_enabled"></a> [private\_endpoint\_enabled](#input\_private\_endpoint\_enabled) | Plan-time bool toggling the conditional private-endpoint child module.<br/>Required as a separate variable from `private_endpoint_subnet_id`<br/>because the subnet ID is sourced from the networking module's output,<br/>which is "known after apply" — using a nullable string in the `count`<br/>expression breaks plan with "Invalid count argument: count value<br/>depends on resource attributes that cannot be determined until apply".<br/>The env composition passes a literal bool here (`var.private_endpoints_enabled`)<br/>so plan can statically resolve the count. | `bool` | `false` | no |
| <a name="input_private_endpoint_subnet_id"></a> [private\_endpoint\_subnet\_id](#input\_private\_endpoint\_subnet\_id) | Subnet ID for the Cosmos account private endpoint. Required when private\_endpoint\_enabled = true. Bound to the `Sql` subresource via the project's private-endpoint module. | `string` | `null` | no |
| <a name="input_public_network_access_enabled"></a> [public\_network\_access\_enabled](#input\_public\_network\_access\_enabled) | When true, the Cosmos account accepts traffic from the internet (gated by AAD/RBAC). When false, requires private endpoints. Defaults to true to preserve pre-spec-005 behavior. | `bool` | `true` | no |
| <a name="input_tags"></a> [tags](#input\_tags) | Tags applied to the Cosmos DB account. | `map(string)` | `{}` | no |

## Outputs

| Name | Description |
| ---- | ----------- |
| <a name="output_account_endpoint"></a> [account\_endpoint](#output\_account\_endpoint) | Public Cosmos DB account endpoint (e.g., https://<name>.documents.azure.com:443/). Bound to CosmosOptions.Endpoint at the app layer. |
| <a name="output_account_id"></a> [account\_id](#output\_account\_id) | Resource id of the Cosmos DB account. Used by cosmos-canonical-store and by SQL role assignments. |
| <a name="output_account_name"></a> [account\_name](#output\_account\_name) | Name of the Cosmos DB account. Used by raw `azurerm_cosmosdb_sql_*` resources downstream. |
| <a name="output_private_endpoint_id"></a> [private\_endpoint\_id](#output\_private\_endpoint\_id) | Resource ID of the Cosmos account PE. Null when no PE is provisioned. |
<!-- END_TF_DOCS -->
