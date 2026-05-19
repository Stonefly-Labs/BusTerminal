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

  # Static key (`audit`) so `for_each` inside the AVM diagnostic-setting
  # resource can enumerate at plan time even when `workspace_resource_id`
  # is only known after apply (first plan against empty state).
  diagnostic_settings = {
    audit = {
      name                  = "acr-diagnostics"
      workspace_resource_id = var.log_analytics_workspace_id
      log_groups            = ["allLogs"]
      metric_categories     = ["AllMetrics"]
    }
  }

  tags = var.tags
}
