locals {
  target_name = element(split("/", var.target_resource_id), length(split("/", var.target_resource_id)) - 1)
  zone_name   = element(split("/", var.private_dns_zone_id), length(split("/", var.private_dns_zone_id)) - 1)
}

resource "azurerm_private_endpoint" "this" {
  name                = var.name
  resource_group_name = var.resource_group_name
  location            = var.location
  subnet_id           = var.subnet_id
  tags                = var.tags

  private_service_connection {
    name                           = "${var.name}-psc"
    private_connection_resource_id = var.target_resource_id
    subresource_names              = [var.subresource_name]
    is_manual_connection           = false
  }

  private_dns_zone_group {
    name                 = "default"
    private_dns_zone_ids = [var.private_dns_zone_id]
  }
}

data "azurerm_network_interface" "pe_nic" {
  name                = azurerm_private_endpoint.this.network_interface[0].name
  resource_group_name = var.resource_group_name
}
