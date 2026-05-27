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

## Inputs

| Name | Type | Required | Description |
|---|---|---|---|
| `vnet_name` | string | yes | Convention: `vnet-<naming_prefix>` |
| `resource_group_name` | string | yes | Env RG (module derives the RG resource ID via a data source) |
| `location` | string | yes | Azure region |
| `address_space` | list(string) | yes | e.g., `["10.50.0.0/16"]` |
| `subnet_integration_cidr` | string | yes | `/23` minimum (CAE requirement) |
| `subnet_private_endpoints_cidr` | string | yes | `/24` recommended |
| `private_dns_zones` | list(string) | yes | Zone names to provision (e.g., `privatelink.vaultcore.azure.net`) |
| `tags` | map(string) | no (default `{}`) | Tags merged onto VNet, subnets, and every zone |

## Outputs

| Name | Type | Description |
|---|---|---|
| `vnet_id` | string | Resource ID of the VNet |
| `vnet_name` | string | VNet name echo |
| `subnet_integration_id` | string | Resource ID of `snet-cae-integration` |
| `subnet_private_endpoints_id` | string | Resource ID of `snet-private-endpoints` |
| `private_dns_zone_ids` | map(string) | Keyed by zone name → zone resource ID |

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
