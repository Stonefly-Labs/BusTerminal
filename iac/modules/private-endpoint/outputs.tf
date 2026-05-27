output "id" {
  description = "Resource ID of the private endpoint."
  value       = azurerm_private_endpoint.this.id
}

output "private_ip_address" {
  description = "Private IP address allocated to the private endpoint's NIC."
  value       = data.azurerm_network_interface.pe_nic.ip_configuration[0].private_ip_address
}

output "fqdn" {
  description = "Derived private FQDN of the target service (`<target-name>.<private-dns-zone-name>`). Resolvable from any VNet linked to the private DNS zone."
  value       = "${local.target_name}.${local.zone_name}"
}
