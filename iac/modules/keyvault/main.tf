terraform {
  required_version = ">= 1.10.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.0"
    }
  }
}

data "azurerm_client_config" "current" {}

module "keyvault" {
  source  = "Azure/avm-res-keyvault-vault/azurerm"
  version = "0.10.0"

  name                          = var.name
  resource_group_name           = var.resource_group_name
  location                      = var.location
  tenant_id                     = data.azurerm_client_config.current.tenant_id
  sku_name                      = "standard"
  purge_protection_enabled      = true
  soft_delete_retention_days    = 90
  public_network_access_enabled = var.public_network_access_enabled

  network_acls = {
    bypass         = "AzureServices"
    default_action = var.public_network_access_enabled ? "Allow" : "Deny"
  }

  diagnostic_settings = var.log_analytics_workspace_id != null ? {
    audit = {
      name                  = "kv-audit"
      workspace_resource_id = var.log_analytics_workspace_id
      log_groups            = ["allLogs"]
      metric_categories     = ["AllMetrics"]
    }
  } : {}

  tags = var.tags
}
