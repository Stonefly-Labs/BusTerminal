# `naming` module

Single source of truth for BusTerminal Azure resource names. Pure-HCL module — no providers, no resources. Computes every derived name deterministically from `environment_name`, `naming_prefix`, and `unique_suffix`.

The names this module emits are consumed by every env composition (`iac/environments/<env>/main.tf`) and propagated downstream to every resource module that needs a name. Centralizing the pattern here means a future rename only changes one file.

## Inputs

| Name | Type | Required | Description |
|---|---|---|---|
| `environment_name` | string | yes | One of `dev`, `test`, `prod`. |
| `naming_prefix` | string | yes | Short hyphenated prefix matching `^bt-[a-z0-9]{2,8}$` (e.g., `bt-dev`). |
| `unique_suffix` | string | yes | 4–12 lowercase alphanumeric chars. Used for globally-unique resource names (KV, ACR, Cosmos, Search, Service Bus). |

## Outputs

| Name | Pattern | Notes |
|---|---|---|
| `resource_group_name` | `rg-<naming_prefix>` | |
| `log_analytics_workspace_name` | `log-<naming_prefix>` | |
| `application_insights_name` | `appi-<naming_prefix>` | |
| `key_vault_name` | `kv-<naming_prefix>-<unique_suffix>` | Globally unique. |
| `container_registry_name` | `acr<naming_prefix><unique_suffix>` | Hyphens stripped (ACR name disallows them). Globally unique. |
| `container_apps_env_name` | `cae-<naming_prefix>` | |
| `cosmos_account_name` | `cosmos-<naming_prefix>-<unique_suffix>` | Globally unique. |
| `ai_search_name` | `srch-<naming_prefix>-<unique_suffix>` | Globally unique. |
| `service_bus_name` | `sbns-<naming_prefix>-<unique_suffix>` | Globally unique. |
| `vnet_name` | `vnet-<naming_prefix>` | |
| `workload_uami_name` | `mi-<naming_prefix>-workload` | |

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
