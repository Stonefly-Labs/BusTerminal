# `private-endpoint` module

Reusable wrapper around `azurerm_private_endpoint` that always provisions a single private service connection bound to a single private DNS zone via a `private_dns_zone_group`. Used by every BusTerminal data service whose AVM module doesn't bake in PE support directly.

## Per-service `subresource_name` reference

When provisioning a PE to an Azure PaaS service, the `subresource_name` (group ID) is service-specific. Using the wrong value fails at apply time. Reference:

| Target service | `subresource_name` | Private DNS zone |
|---|---|---|
| Azure Key Vault | `vault` | `privatelink.vaultcore.azure.net` |
| Azure Cosmos DB (SQL API) | `Sql` | `privatelink.documents.azure.com` |
| Azure AI Search | `searchService` | `privatelink.search.windows.net` |
| Azure Service Bus (Premium only) | `namespace` | `privatelink.servicebus.windows.net` |
| Azure Container Registry | `registry` | `privatelink.azurecr.io` |
| Azure Storage (Blob) | `blob` | `privatelink.blob.core.windows.net` |

Source: <https://learn.microsoft.com/azure/private-link/private-endpoint-dns> (canonical mapping).

## Inputs

| Name | Type | Required | Description |
|---|---|---|---|
| `name` | string | yes | PE resource name. Convention: `pe-<short-target-name>`. |
| `resource_group_name` | string | yes | Resource group hosting the PE (typically the env RG). |
| `location` | string | yes | Azure region. |
| `subnet_id` | string | yes | PE subnet ID (typically the env's `snet-private-endpoints`). |
| `target_resource_id` | string | yes | Azure resource ID of the target service. |
| `subresource_name` | string | yes | One of the values in the reference table above. |
| `private_dns_zone_id` | string | yes | Private DNS zone for A-record registration. Must match the `subresource_name` mapping. |
| `tags` | map(string) | no (default `{}`) | Tags merged onto the PE. |

## Outputs

| Name | Description |
|---|---|
| `id` | PE resource ID. |
| `private_ip_address` | Private IP allocated to the PE's NIC (read via a `data "azurerm_network_interface"` lookup). |
| `fqdn` | Derived private FQDN (`<target-name>.<private-dns-zone-name>`). |

## Usage

```hcl
module "kv_pe" {
  source = "../../modules/private-endpoint"

  name                = "pe-${module.naming.key_vault_name}"
  resource_group_name = module.naming.resource_group_name
  location            = var.location
  subnet_id           = module.networking.subnet_private_endpoints_id
  target_resource_id  = module.keyvault.id
  subresource_name    = "vault"
  private_dns_zone_id = module.networking.private_dns_zone_ids["privatelink.vaultcore.azure.net"]
  tags                = local.mandatory_tags
}
```
