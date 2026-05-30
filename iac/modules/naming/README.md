# `naming` module

Single source of truth for BusTerminal Azure resource names. Pure-HCL module — no providers, no resources. Computes every derived name deterministically from `environment_name`, `naming_prefix`, and `unique_suffix`.

The names this module emits are consumed by every env composition (`iac/environments/<env>/main.tf`) and propagated downstream to every resource module that needs a name. Centralizing the pattern here means a future rename only changes one file.

<!-- BEGIN_TF_DOCS -->


## Inputs

| Name | Description | Type | Default | Required |
| ---- | ----------- | ---- | ------- | :------: |
| <a name="input_environment_name"></a> [environment\_name](#input\_environment\_name) | Logical environment name. One of dev, test, prod. | `string` | n/a | yes |
| <a name="input_naming_prefix"></a> [naming\_prefix](#input\_naming\_prefix) | Short hyphenated prefix applied to every derived resource name (e.g., bt-dev, bt-test, bt-prod). | `string` | n/a | yes |
| <a name="input_unique_suffix"></a> [unique\_suffix](#input\_unique\_suffix) | Globally-unique suffix appended to names that require uniqueness across Azure (Key Vault, ACR, Cosmos, Search, Service Bus). 4-12 lowercase alphanumeric characters. | `string` | n/a | yes |

## Outputs

| Name | Description |
| ---- | ----------- |
| <a name="output_ai_search_name"></a> [ai\_search\_name](#output\_ai\_search\_name) | Azure AI Search service name (srch-<naming\_prefix>-<unique\_suffix>). Globally unique. |
| <a name="output_application_insights_name"></a> [application\_insights\_name](#output\_application\_insights\_name) | Application Insights resource name (appi-<naming\_prefix>). |
| <a name="output_container_apps_env_name"></a> [container\_apps\_env\_name](#output\_container\_apps\_env\_name) | Container Apps Environment name (cae-<naming\_prefix>). |
| <a name="output_container_registry_name"></a> [container\_registry\_name](#output\_container\_registry\_name) | Azure Container Registry name (acr<naming\_prefix><unique\_suffix>, hyphens stripped). Globally unique. |
| <a name="output_cosmos_account_name"></a> [cosmos\_account\_name](#output\_cosmos\_account\_name) | Cosmos DB account name (cosmos-<naming\_prefix>-<unique\_suffix>). Globally unique. |
| <a name="output_key_vault_name"></a> [key\_vault\_name](#output\_key\_vault\_name) | Key Vault name (kv-<naming\_prefix>-<unique\_suffix>). Globally unique. |
| <a name="output_log_analytics_workspace_name"></a> [log\_analytics\_workspace\_name](#output\_log\_analytics\_workspace\_name) | Log Analytics Workspace name (log-<naming\_prefix>). |
| <a name="output_mandatory_tags"></a> [mandatory\_tags](#output\_mandatory\_tags) | Mandatory tag set per data-model.md §1.2 (application/environment/managed-by/cost-center/owner). Env compositions merge operator-supplied tags on top of this set. |
| <a name="output_resource_group_name"></a> [resource\_group\_name](#output\_resource\_group\_name) | Env resource group name (rg-<naming\_prefix>). |
| <a name="output_service_bus_name"></a> [service\_bus\_name](#output\_service\_bus\_name) | Service Bus namespace name (sbns-<naming\_prefix>-<unique\_suffix>). Globally unique. |
| <a name="output_vnet_name"></a> [vnet\_name](#output\_vnet\_name) | Virtual network name (vnet-<naming\_prefix>). |
| <a name="output_workload_uami_name"></a> [workload\_uami\_name](#output\_workload\_uami\_name) | Workload user-assigned managed identity name (mi-<naming\_prefix>-workload). |
<!-- END_TF_DOCS -->

## Usage

```hcl
module "naming" {
  source = "../../modules/naming"

  environment_name = var.environment_name
  naming_prefix    = var.naming_prefix
  unique_suffix    = var.unique_suffix
}

module "keyvault" {
  source = "../../modules/keyvault"

  name                = module.naming.key_vault_name
  resource_group_name = module.naming.resource_group_name
  # ...
}
```
