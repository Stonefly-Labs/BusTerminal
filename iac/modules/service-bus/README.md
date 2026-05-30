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

<!-- BEGIN_TF_DOCS -->
## Resources

| Name | Type |
| ---- | ---- |
| [azurerm_role_assignment.workload_sb_data_receiver](https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs/resources/role_assignment) | resource |
| [azurerm_role_assignment.workload_sb_data_sender](https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs/resources/role_assignment) | resource |
| [terraform_data.sku_validation](https://registry.terraform.io/providers/hashicorp/terraform/latest/docs/resources/data) | resource |

## Inputs

| Name | Description | Type | Default | Required |
| ---- | ----------- | ---- | ------- | :------: |
| <a name="input_location"></a> [location](#input\_location) | Azure region for the namespace. | `string` | n/a | yes |
| <a name="input_log_analytics_workspace_id"></a> [log\_analytics\_workspace\_id](#input\_log\_analytics\_workspace\_id) | Log Analytics Workspace ID for diagnostic settings (allLogs only, no metrics — Q5c). | `string` | n/a | yes |
| <a name="input_name"></a> [name](#input\_name) | Service Bus namespace name (from naming module). Convention: `sbns-<naming_prefix>-<unique_suffix>`. 6-50 alphanumeric / hyphen; must start with a letter and end with a letter or digit. Globally unique. | `string` | n/a | yes |
| <a name="input_public_network_access_enabled"></a> [public\_network\_access\_enabled](#input\_public\_network\_access\_enabled) | Per-env public-network access toggle (FR-031). Dev defaults true (Q2c warm), test/prod default false. | `bool` | n/a | yes |
| <a name="input_resource_group_name"></a> [resource\_group\_name](#input\_resource\_group\_name) | Resource group hosting the namespace (env RG). | `string` | n/a | yes |
| <a name="input_sku"></a> [sku](#input\_sku) | Namespace SKU. One of: Standard, Premium. Per research §3 dev defaults Standard (~$10/mo), test/prod default Premium (PE-capable, ~$667+/mo). Basic is rejected — no topics/subscriptions support. | `string` | n/a | yes |
| <a name="input_workload_principal_id"></a> [workload\_principal\_id](#input\_workload\_principal\_id) | Workload UAMI principal (object) ID. Receives `Azure Service Bus Data Sender` AND `Azure Service Bus Data Receiver` scoped to this namespace (FR-033). | `string` | n/a | yes |
| <a name="input_capacity"></a> [capacity](#input\_capacity) | Premium messaging units. Required when sku="Premium"; ignored otherwise. One of 1, 2, 4, 8, 16. | `number` | `null` | no |
| <a name="input_private_dns_zone_id"></a> [private\_dns\_zone\_id](#input\_private\_dns\_zone\_id) | Private DNS zone ID for `privatelink.servicebus.windows.net`. Required when private\_endpoint\_subnet\_id is set. | `string` | `null` | no |
| <a name="input_private_endpoint_subnet_id"></a> [private\_endpoint\_subnet\_id](#input\_private\_endpoint\_subnet\_id) | Subnet ID for the namespace private endpoint. Required when private endpoint is desired AND sku=Premium. The env composition is responsible for nulling this for Standard SKU (the module precondition rejects non-null PE inputs with Standard). | `string` | `null` | no |
| <a name="input_tags"></a> [tags](#input\_tags) | Tags merged onto the namespace and its PE (when provisioned). | `map(string)` | `{}` | no |

## Outputs

| Name | Description |
| ---- | ----------- |
| <a name="output_fqdn"></a> [fqdn](#output\_fqdn) | Namespace FQDN (`<name>.servicebus.windows.net`). Bound to `ServiceBusOptions.FullyQualifiedNamespace` at the app layer. |
| <a name="output_id"></a> [id](#output\_id) | Resource ID of the Service Bus namespace. |
| <a name="output_name"></a> [name](#output\_name) | Namespace name (echo of var.name). |
| <a name="output_private_endpoint_id"></a> [private\_endpoint\_id](#output\_private\_endpoint\_id) | Resource ID of the namespace PE. Null when no PE is provisioned (Standard SKU or PE inputs nulled). |
<!-- END_TF_DOCS -->

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
