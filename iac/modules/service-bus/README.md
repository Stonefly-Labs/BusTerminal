# Service Bus module

Wraps the Azure Verified Module
[`Azure/avm-res-servicebus-namespace/azurerm` v0.4.0](https://registry.terraform.io/modules/Azure/avm-res-servicebus-namespace/azurerm/0.4.0)
to provision a Service Bus **namespace** only (no topics, queues, or
subscriptions per FR-022 + spec Q3) with:

- AAD-only data plane (`local_auth_enabled = false`) per FR-016
- `allLogs`-only diagnostic forwarding via the project's `diagnostic-settings`
  module (Q5c)
- Workload UAMI granted `Azure Service Bus Data Sender` AND
  `Azure Service Bus Data Receiver` (FR-033)
- Optional private endpoint via the project's `private-endpoint` wrapper —
  Premium-only

Spec 005 / Phase 3 / US1 — implements research §3 (SKU choice), §11
(private-endpoint DNS zone), and §12 (RBAC GUIDs).

## SKU rationale (per `research.md` §3)

| Env | SKU | Approx cost/mo | Rationale |
|---|---|---|---|
| dev | `Standard` | ~$10 | Cheapest SKU that supports topics/subscriptions/AAD. No PEs (Standard tier limitation — covered by FR-024's "where the chosen SKU supports it" caveat). |
| test | `Premium` | ~$667+ | Required for PEs (Q2c private-by-default for test/prod). Capacity = 1 messaging unit. |
| prod | `Premium` | ~$667+ | Same as test. Capacity = 1 messaging unit; operators can scale via tfvars. |

`Basic` SKU is rejected — it doesn't support topics/subscriptions, which any
near-term spec may need.

## Preconditions

1. `sku = "Basic"` rejected (variable validation).
2. `sku = "Standard"` AND `private_endpoint_subnet_id != null` → ERROR. The
   env composition MUST null PE inputs when SKU is Standard (dev does this
   automatically via a SKU-conditional in `iac/environments/dev/main.tf`).
3. `sku = "Premium"` AND `capacity == null` → ERROR.

## Inputs

| Name | Type | Required | Default | Description |
|---|---|---|---|---|
| `name` | string | yes | n/a | Namespace name (globally unique) |
| `resource_group_name` | string | yes | n/a | Env RG |
| `location` | string | yes | n/a | Azure region |
| `sku` | string | yes | n/a | `Standard` or `Premium` |
| `capacity` | number | conditional | `null` | Required when `sku = "Premium"`: one of 1, 2, 4, 8, 16 |
| `public_network_access_enabled` | bool | yes | n/a | Per-env toggle |
| `log_analytics_workspace_id` | string | yes | n/a | For diagnostics |
| `workload_principal_id` | string | yes | n/a | UAMI principal_id; receives Sender + Receiver |
| `private_endpoint_subnet_id` | string | no | `null` | Premium-only |
| `private_dns_zone_id` | string | no | `null` | Required when PE subnet is set |
| `tags` | map(string) | no | `{}` | Merged on every child |

## Outputs

| Name | Type | Description |
|---|---|---|
| `id` | string | Namespace resource ID |
| `name` | string | Echo of `var.name` |
| `fqdn` | string | `<name>.servicebus.windows.net` |
| `private_endpoint_id` | string | PE resource ID, null when no PE |

## Usage

```hcl
module "service_bus" {
  source = "../../modules/service-bus"

  name                = module.naming.service_bus_name
  resource_group_name = azurerm_resource_group.this.name
  location            = azurerm_resource_group.this.location

  sku                           = var.service_bus_sku
  capacity                      = var.service_bus_capacity
  public_network_access_enabled = var.data_services_public_access_enabled

  log_analytics_workspace_id = module.monitoring.log_analytics_workspace_id
  workload_principal_id      = module.workload_identity.principal_id

  # SKU-conditional nulling: PE inputs only when Premium AND PEs enabled.
  private_endpoint_subnet_id = (var.service_bus_sku == "Premium" && var.private_endpoints_enabled) ? module.networking.subnet_private_endpoints_id : null
  private_dns_zone_id        = (var.service_bus_sku == "Premium" && var.private_endpoints_enabled) ? module.networking.private_dns_zone_ids["privatelink.servicebus.windows.net"] : null

  tags = local.shared_tags
}
```
