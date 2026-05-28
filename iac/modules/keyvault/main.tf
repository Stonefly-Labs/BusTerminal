terraform {
  required_version = ">= 1.11.0"

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

  # Static key (`audit`) so `for_each` inside the AVM diagnostic-setting
  # resource can enumerate at plan time even when `workspace_resource_id`
  # is only known after apply (first plan against empty state).
  diagnostic_settings = {
    audit = {
      name                  = "kv-audit"
      workspace_resource_id = var.log_analytics_workspace_id
      log_groups            = ["allLogs"]
      metric_categories     = ["AllMetrics"]
    }
  }

  tags = var.tags
}

# Spec 005 — conditional private endpoint via the project's PE wrapper (T055).
# Subresource `vault` per the Azure private-endpoint DNS reference (research §11).
# `count` keyed off the plan-time-known `private_endpoint_enabled` bool — see
# variables.tf for why we can't key off `subnet_id != null` (subnet_id is
# known-after-apply from the networking module's output).
resource "terraform_data" "pe_validation" {
  count = var.private_endpoint_enabled ? 1 : 0

  input = {
    private_endpoint_subnet_id = var.private_endpoint_subnet_id
    private_dns_zone_id        = var.private_dns_zone_id
  }

  lifecycle {
    precondition {
      condition     = var.private_endpoint_subnet_id != null && var.private_dns_zone_id != null
      error_message = "keyvault: private_endpoint_subnet_id and private_dns_zone_id are required when private_endpoint_enabled = true."
    }
  }
}

module "private_endpoint" {
  count = var.private_endpoint_enabled ? 1 : 0

  source = "../private-endpoint"

  name                = "pe-${var.name}"
  resource_group_name = var.resource_group_name
  location            = var.location
  subnet_id           = var.private_endpoint_subnet_id
  target_resource_id  = module.keyvault.resource_id
  subresource_name    = "vault"
  private_dns_zone_id = var.private_dns_zone_id
  tags                = var.tags
}
