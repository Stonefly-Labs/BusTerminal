output "vnet_id" {
  description = "Resource ID of the VNet."
  value       = module.vnet.resource_id
}

output "vnet_name" {
  description = "VNet name (echo of var.vnet_name)."
  value       = var.vnet_name
}

output "subnet_integration_id" {
  description = "Resource ID of the CAE integration subnet (snet-cae-integration). Delegated to Microsoft.App/environments; consumed by the future Container Apps Environment VNet-integration retrofit."
  value       = module.vnet.subnets["integration"].resource_id
}

output "subnet_private_endpoints_id" {
  description = "Resource ID of the private-endpoints subnet (snet-private-endpoints). PE attachment-ready (private_endpoint_network_policies = Disabled)."
  value       = module.vnet.subnets["private_endpoints"].resource_id
}

output "private_dns_zone_ids" {
  description = "Map of private DNS zone resource IDs keyed by zone name (e.g., `privatelink.vaultcore.azure.net` → /subscriptions/.../privateDnsZones/privatelink.vaultcore.azure.net). Consumed by per-service PE wrapper module calls in the env composition."
  value       = { for k, v in module.private_dns_zones : k => v.resource_id }
}
