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

## Inputs

| Name | Type | Required | Default | Description |
|---|---|---|---|---|
| `name` | string | yes | n/a | Search service name |
| `resource_group_name` | string | yes | n/a | Env RG |
| `location` | string | yes | n/a | Azure region |
| `sku` | string | yes | n/a | One of `free`, `basic`, `standard`, `standard2`, `standard3` |
| `public_network_access_enabled` | bool | yes | n/a | Per-env toggle (FR-031) |
| `log_analytics_workspace_id` | string | yes | n/a | For diagnostics |
| `workload_principal_id` | string | yes | n/a | UAMI principal_id; receives `Search Index Data Contributor` |
| `private_endpoint_subnet_id` | string | no | `null` | When set, provision a PE |
| `private_dns_zone_id` | string | no | `null` | Required when PE subnet is set |
| `tags` | map(string) | no | `{}` | Merged on every child |

## Outputs

| Name | Type | Description |
|---|---|---|
| `id` | string | Search service resource ID |
| `name` | string | Echo of `var.name` |
| `endpoint` | string | `https://<name>.search.windows.net` |
| `private_endpoint_id` | string | PE resource ID, null when PE disabled |

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
