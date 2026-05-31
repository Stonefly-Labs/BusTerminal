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

<!-- BEGIN_TF_DOCS -->
## Resources

| Name | Type |
| ---- | ---- |
| [azurerm_private_endpoint.this](https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs/resources/private_endpoint) | resource |
| [azurerm_network_interface.pe_nic](https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs/data-sources/network_interface) | data source |

## Inputs

| Name | Description | Type | Default | Required |
| ---- | ----------- | ---- | ------- | :------: |
| <a name="input_location"></a> [location](#input\_location) | Azure region for the private endpoint. | `string` | n/a | yes |
| <a name="input_name"></a> [name](#input\_name) | Private endpoint resource name. Convention: `pe-<short-target-name>`. | `string` | n/a | yes |
| <a name="input_private_dns_zone_id"></a> [private\_dns\_zone\_id](#input\_private\_dns\_zone\_id) | Private DNS zone ID for A-record registration. Must match the subresource\_name (e.g., vault → privatelink.vaultcore.azure.net). | `string` | n/a | yes |
| <a name="input_resource_group_name"></a> [resource\_group\_name](#input\_resource\_group\_name) | Resource group hosting the private endpoint (typically the env RG, same RG as the target service). | `string` | n/a | yes |
| <a name="input_subnet_id"></a> [subnet\_id](#input\_subnet\_id) | Subnet ID for the private endpoint (typically the env's `snet-private-endpoints`). | `string` | n/a | yes |
| <a name="input_subresource_name"></a> [subresource\_name](#input\_subresource\_name) | Per-service PE subresource name. One of: vault, Sql, searchService, namespace, registry, blob. See README for the full reference table. | `string` | n/a | yes |
| <a name="input_target_resource_id"></a> [target\_resource\_id](#input\_target\_resource\_id) | Azure resource ID of the target service (Key Vault, Cosmos account, Search service, Service Bus namespace, ACR, storage account, etc.). | `string` | n/a | yes |
| <a name="input_tags"></a> [tags](#input\_tags) | Tags merged onto the private endpoint resource. | `map(string)` | `{}` | no |

## Outputs

| Name | Description |
| ---- | ----------- |
| <a name="output_fqdn"></a> [fqdn](#output\_fqdn) | Derived private FQDN of the target service (`<target-name>.<private-dns-zone-name>`). Resolvable from any VNet linked to the private DNS zone. |
| <a name="output_id"></a> [id](#output\_id) | Resource ID of the private endpoint. |
| <a name="output_private_ip_address"></a> [private\_ip\_address](#output\_private\_ip\_address) | Private IP address allocated to the private endpoint's NIC. |
<!-- END_TF_DOCS -->

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
