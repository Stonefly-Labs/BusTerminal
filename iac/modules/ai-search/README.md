# AI Search module

Wraps the Azure Verified Module
[`Azure/avm-res-search-searchservice/azurerm` v0.2.0](https://registry.terraform.io/modules/Azure/avm-res-search-searchservice/azurerm/0.2.0)
to provision an Azure AI Search service with:

- AAD-only data plane (`local_authentication_enabled = false`) per FR-016
- `allLogs`-only diagnostic forwarding via the project's `diagnostic-settings`
  module (Q5c)
- Workload UAMI granted `Search Index Data Contributor` (FR-033)
- Optional private endpoint via the project's `private-endpoint` wrapper

Spec 005 / Phase 3 / US1 — implements research §4 (SKU choice) and §11
(private-endpoint DNS zone).

## SKU table (per `research.md` §4)

| Env | SKU | Notes |
|---|---|---|
| dev | `basic` | Cheapest SKU supporting AAD/RBAC + PEs |
| test | `standard` | Production-grade S1 |
| prod | `standard` | Production-grade S1 |

`free` SKU is rejected when public access is disabled OR a PE is requested
(precondition fires at plan time) — free supports neither AAD/RBAC nor PEs.

<!-- BEGIN_TF_DOCS -->
## Resources

| Name | Type |
| ---- | ---- |
| [azurerm_role_assignment.workload_search_index_data_contributor](https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs/resources/role_assignment) | resource |
| [terraform_data.pe_inputs_validation](https://registry.terraform.io/providers/hashicorp/terraform/latest/docs/resources/data) | resource |
| [terraform_data.sku_validation](https://registry.terraform.io/providers/hashicorp/terraform/latest/docs/resources/data) | resource |

## Inputs

| Name | Description | Type | Default | Required |
| ---- | ----------- | ---- | ------- | :------: |
| <a name="input_location"></a> [location](#input\_location) | Azure region for the search service. | `string` | n/a | yes |
| <a name="input_log_analytics_workspace_id"></a> [log\_analytics\_workspace\_id](#input\_log\_analytics\_workspace\_id) | Log Analytics Workspace ID for diagnostic settings (allLogs only, no metrics — Q5c). | `string` | n/a | yes |
| <a name="input_name"></a> [name](#input\_name) | Search service name (from naming module). Convention: `srch-<naming_prefix>-<unique_suffix>`. 2-60 lowercase alphanumeric / hyphen. | `string` | n/a | yes |
| <a name="input_public_network_access_enabled"></a> [public\_network\_access\_enabled](#input\_public\_network\_access\_enabled) | Per-env public-network access toggle (FR-031). Dev defaults true (Q2c warm PE), test/prod default false. | `bool` | n/a | yes |
| <a name="input_resource_group_name"></a> [resource\_group\_name](#input\_resource\_group\_name) | Resource group hosting the search service (env RG). | `string` | n/a | yes |
| <a name="input_sku"></a> [sku](#input\_sku) | AI Search SKU. One of: free, basic, standard, standard2, standard3. Per research §4 dev defaults basic, test/prod default standard. `free` is rejected when public access is disabled or a PE is requested (free SKU supports neither). | `string` | n/a | yes |
| <a name="input_workload_principal_id"></a> [workload\_principal\_id](#input\_workload\_principal\_id) | Workload UAMI principal (object) ID. Receives `Search Index Data Contributor` scoped to this search service (FR-033). | `string` | n/a | yes |
| <a name="input_private_dns_zone_id"></a> [private\_dns\_zone\_id](#input\_private\_dns\_zone\_id) | Private DNS zone ID for `privatelink.search.windows.net`. Required when private\_endpoint\_enabled = true. | `string` | `null` | no |
| <a name="input_private_endpoint_enabled"></a> [private\_endpoint\_enabled](#input\_private\_endpoint\_enabled) | Plan-time bool toggling the conditional private-endpoint child module.<br/>Required as a separate variable from `private_endpoint_subnet_id`<br/>because the subnet ID is sourced from the networking module's output,<br/>which is "known after apply" — using a nullable string in the `count`<br/>expression breaks plan with "Invalid count argument: count value<br/>depends on resource attributes that cannot be determined until apply".<br/>The env composition passes a literal bool here (`var.private_endpoints_enabled`)<br/>so plan can statically resolve the count. | `bool` | `false` | no |
| <a name="input_private_endpoint_location"></a> [private\_endpoint\_location](#input\_private\_endpoint\_location) | Azure region for the private endpoint resource (must match the subnet's region). Defaults to var.location when null. | `string` | `null` | no |
| <a name="input_private_endpoint_subnet_id"></a> [private\_endpoint\_subnet\_id](#input\_private\_endpoint\_subnet\_id) | Subnet ID for the search service private endpoint. Required when private\_endpoint\_enabled = true. | `string` | `null` | no |
| <a name="input_tags"></a> [tags](#input\_tags) | Tags merged onto the search service and its PE (when provisioned). | `map(string)` | `{}` | no |

## Outputs

| Name | Description |
| ---- | ----------- |
| <a name="output_endpoint"></a> [endpoint](#output\_endpoint) | Public search-service endpoint URL (`https://<name>.search.windows.net`). Bound to `SearchOptions.Endpoint` at the app layer. |
| <a name="output_id"></a> [id](#output\_id) | Resource ID of the AI Search service. |
| <a name="output_name"></a> [name](#output\_name) | Search service name (echo of var.name). |
| <a name="output_private_endpoint_id"></a> [private\_endpoint\_id](#output\_private\_endpoint\_id) | Resource ID of the search PE. Null when no PE is provisioned. |
<!-- END_TF_DOCS -->

## Usage

```hcl
module "ai_search" {
  source = "../../modules/ai-search"

  name                = module.naming.ai_search_name
  resource_group_name = azurerm_resource_group.this.name
  location            = azurerm_resource_group.this.location

  sku                           = var.ai_search_sku
  public_network_access_enabled = var.data_services_public_access_enabled

  log_analytics_workspace_id = module.monitoring.log_analytics_workspace_id
  workload_principal_id      = module.workload_identity.principal_id

  private_endpoint_subnet_id = var.private_endpoints_enabled ? module.networking.subnet_private_endpoints_id : null
  private_dns_zone_id        = var.private_endpoints_enabled ? module.networking.private_dns_zone_ids["privatelink.search.windows.net"] : null

  tags = local.shared_tags
}
```
