terraform {
  required_version = ">= 1.11.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.0"
    }
  }
}

data "azurerm_log_analytics_workspace" "this" {
  name                = var.log_analytics_workspace_name
  resource_group_name = var.log_analytics_workspace_resource_group
}

module "environment" {
  source  = "Azure/avm-res-app-managedenvironment/azurerm"
  version = "0.4.0"

  name                = var.name
  resource_group_name = var.resource_group_name
  location            = var.location

  log_analytics_workspace_customer_id        = data.azurerm_log_analytics_workspace.this.workspace_id
  log_analytics_workspace_primary_shared_key = data.azurerm_log_analytics_workspace.this.primary_shared_key

  zone_redundancy_enabled = var.zone_redundancy_enabled

  diagnostic_settings = {
    audit = {
      name                  = "cae-diagnostics"
      workspace_resource_id = data.azurerm_log_analytics_workspace.this.id
      log_groups            = ["allLogs"]
      metric_categories     = ["AllMetrics"]
    }
  }

  tags = var.tags
}
