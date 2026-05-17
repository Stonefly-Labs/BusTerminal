terraform {
  required_version = ">= 1.11.0"
}

module "registry" {
  source  = "Azure/avm-res-containerregistry-registry/azurerm"
  version = "0.4.0"

  name                          = var.name
  resource_group_name           = var.resource_group_name
  location                      = var.location
  sku                           = var.sku
  admin_enabled                 = false
  public_network_access_enabled = var.public_network_access_enabled

  diagnostic_settings = var.log_analytics_workspace_id != null ? {
    audit = {
      name                  = "acr-diagnostics"
      workspace_resource_id = var.log_analytics_workspace_id
      log_groups            = ["allLogs"]
      metric_categories     = ["AllMetrics"]
    }
  } : {}

  tags = var.tags
}
