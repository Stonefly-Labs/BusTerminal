# Networking module

Wraps the Azure Verified Modules
[`Azure/avm-res-network-virtualnetwork/azurerm` v0.16.0](https://registry.terraform.io/modules/Azure/avm-res-network-virtualnetwork/azurerm/0.16.0)
and
[`Azure/avm-res-network-privatednszone/azurerm` v0.4.2](https://registry.terraform.io/modules/Azure/avm-res-network-privatednszone/azurerm/0.4.2)
to provision a single env's VNet + two subnets + N private DNS zones (each linked
to the new VNet).

Spec 005 / Phase 3 / US1 — implements the topology in
`specs/005-infrastructure-baseline/research.md` §10 and §11.

## Topology

| Subnet | Purpose | Delegation / network policy |
|---|---|---|
| `snet-cae-integration` | Container Apps Environment VNet integration (deferred retrofit — subnet provisioned warm) | delegated to `Microsoft.App/environments` |
| `snet-private-endpoints` | All private endpoints for data services (KV, Cosmos, AI Search, SB Premium, ACR) | `private_endpoint_network_policies = "Disabled"` |

<!-- BEGIN_TF_DOCS -->
## Resources

| Name | Type |
| ---- | ---- |
| [terraform_data.subnet_validation](https://registry.terraform.io/providers/hashicorp/terraform/latest/docs/resources/data) | resource |
| [azurerm_resource_group.this](https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs/data-sources/resource_group) | data source |

## Inputs

| Name | Description | Type | Default | Required |
| ---- | ----------- | ---- | ------- | :------: |
| <a name="input_address_space"></a> [address\_space](#input\_address\_space) | VNet address space (e.g., ["10.50.0.0/16"]). Per env per research §10. | `list(string)` | n/a | yes |
| <a name="input_location"></a> [location](#input\_location) | Azure region for the VNet. | `string` | n/a | yes |
| <a name="input_private_dns_zones"></a> [private\_dns\_zones](#input\_private\_dns\_zones) | List of private DNS zone names to provision and link to this VNet (e.g.,<br/>`privatelink.vaultcore.azure.net`, `privatelink.documents.azure.com`,<br/>`privatelink.search.windows.net`, `privatelink.servicebus.windows.net`,<br/>`privatelink.azurecr.io`). Per research §11 — the env composition decides<br/>which zones to provision based on `private_endpoints_enabled`. | `list(string)` | n/a | yes |
| <a name="input_resource_group_name"></a> [resource\_group\_name](#input\_resource\_group\_name) | Resource group hosting the VNet and private DNS zones (typically the env RG). | `string` | n/a | yes |
| <a name="input_subnet_integration_cidr"></a> [subnet\_integration\_cidr](#input\_subnet\_integration\_cidr) | CIDR for the Container Apps Environment integration subnet. /23 minimum (Azure CAE requirement). Must be inside address\_space. | `string` | n/a | yes |
| <a name="input_subnet_private_endpoints_cidr"></a> [subnet\_private\_endpoints\_cidr](#input\_subnet\_private\_endpoints\_cidr) | CIDR for the private-endpoints subnet. /24 recommended. Must be inside address\_space and non-overlapping with subnet\_integration\_cidr. | `string` | n/a | yes |
| <a name="input_vnet_name"></a> [vnet\_name](#input\_vnet\_name) | Virtual network name (from naming module). Convention: `vnet-<naming_prefix>`. | `string` | n/a | yes |
| <a name="input_tags"></a> [tags](#input\_tags) | Tags merged onto the VNet, subnets, and every private DNS zone. | `map(string)` | `{}` | no |

## Outputs

| Name | Description |
| ---- | ----------- |
| <a name="output_private_dns_zone_ids"></a> [private\_dns\_zone\_ids](#output\_private\_dns\_zone\_ids) | Map of private DNS zone resource IDs keyed by zone name (e.g., `privatelink.vaultcore.azure.net` → /subscriptions/.../privateDnsZones/privatelink.vaultcore.azure.net). Consumed by per-service PE wrapper module calls in the env composition. |
| <a name="output_subnet_integration_id"></a> [subnet\_integration\_id](#output\_subnet\_integration\_id) | Resource ID of the CAE integration subnet (snet-cae-integration). Delegated to Microsoft.App/environments; consumed by the future Container Apps Environment VNet-integration retrofit. |
| <a name="output_subnet_private_endpoints_id"></a> [subnet\_private\_endpoints\_id](#output\_subnet\_private\_endpoints\_id) | Resource ID of the private-endpoints subnet (snet-private-endpoints). PE attachment-ready (private\_endpoint\_network\_policies = Disabled). |
| <a name="output_vnet_id"></a> [vnet\_id](#output\_vnet\_id) | Resource ID of the VNet. |
| <a name="output_vnet_name"></a> [vnet\_name](#output\_vnet\_name) | VNet name (echo of var.vnet\_name). |
<!-- END_TF_DOCS -->

## Validations / preconditions

1. `subnet_integration_cidr` is `/23` or larger (Azure Container Apps Environment minimum).
2. `subnet_integration_cidr` and `subnet_private_endpoints_cidr` must not overlap.
3. Each subnet CIDR must be a valid CIDR block.

## CIDR allocation reference (per `research.md` §10)

| Env | VNet | Integration subnet | PE subnet | Reserved (jumpbox) |
|---|---|---|---|---|
| dev | `10.50.0.0/16` | `10.50.0.0/23` | `10.50.2.0/24` | `10.50.3.0/27` |
| test | `10.51.0.0/16` | `10.51.0.0/23` | `10.51.2.0/24` | `10.51.3.0/27` |
| prod | `10.52.0.0/16` | `10.52.0.0/23` | `10.52.2.0/24` | `10.52.3.0/27` |

The `10.5x.4.0/22` and beyond ranges are reserved for future subnets without
re-IP'ing existing resources. `10.5x.*` was chosen to avoid colliding with
common on-prem default ranges (`10.0.*`, `10.1.*`) if BusTerminal ever peers
with corporate networks.

## Private DNS zone reference

Per `research.md` §11 and
[the Azure private-endpoint DNS reference](https://learn.microsoft.com/azure/private-link/private-endpoint-dns):

| Service | Zone name |
|---|---|
| Key Vault | `privatelink.vaultcore.azure.net` |
| Cosmos DB SQL | `privatelink.documents.azure.com` |
| AI Search | `privatelink.search.windows.net` |
| Service Bus (Premium only) | `privatelink.servicebus.windows.net` |
| Container Registry | `privatelink.azurecr.io` |
| Storage (blob, future) | `privatelink.blob.core.windows.net` |

## Usage

```hcl
module "networking" {
  source = "../../modules/networking"

  vnet_name           = module.naming.vnet_name
  resource_group_name = azurerm_resource_group.this.name
  location            = azurerm_resource_group.this.location

  address_space                 = var.network_address_space
  subnet_integration_cidr       = var.subnet_integration_cidr
  subnet_private_endpoints_cidr = var.subnet_private_endpoints_cidr

  private_dns_zones = [
    "privatelink.vaultcore.azure.net",
    "privatelink.documents.azure.com",
    "privatelink.search.windows.net",
    "privatelink.servicebus.windows.net",
    "privatelink.azurecr.io",
  ]

  tags = local.shared_tags
}
```
