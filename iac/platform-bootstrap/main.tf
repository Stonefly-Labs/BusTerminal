terraform {
  required_version = ">= 1.10.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.0"
    }
    azuread = {
      source  = "hashicorp/azuread"
      version = "~> 3.0"
    }
  }
}

provider "azurerm" {
  subscription_id = var.subscription_id
  features {}
}

provider "azuread" {}

locals {
  tfstate_resource_group_name = "rg-busterminal-tfstate"
  tfstate_container_name      = "tfstate"
  shared_tags = {
    application = "BusTerminal"
    component   = "platform-bootstrap"
    managed-by  = "opentofu"
    cost-center = "platform"
  }
}

resource "azurerm_resource_group" "tfstate" {
  name     = local.tfstate_resource_group_name
  location = var.location
  tags     = local.shared_tags
}

module "tfstate_storage" {
  source  = "Azure/avm-res-storage-storageaccount/azurerm"
  version = "0.6.3"

  name                          = var.tfstate_storage_account_name
  resource_group_name           = azurerm_resource_group.tfstate.name
  location                      = azurerm_resource_group.tfstate.location
  account_tier                  = "Standard"
  account_replication_type      = "ZRS"
  account_kind                  = "StorageV2"
  min_tls_version               = "TLS1_2"
  public_network_access_enabled = true
  shared_access_key_enabled     = false
  https_traffic_only_enabled    = true

  blob_properties = {
    versioning_enabled = true
    delete_retention_policy = {
      days = 30
    }
    container_delete_retention_policy = {
      days = 30
    }
  }

  containers = {
    tfstate = {
      name = local.tfstate_container_name
    }
  }

  tags = local.shared_tags
}

module "pipeline_identity" {
  for_each = toset(var.environments)

  source  = "Azure/avm-res-managedidentity-userassignedidentity/azurerm"
  version = "0.3.3"

  name                = "mi-busterminal-pipeline-${each.key}"
  resource_group_name = azurerm_resource_group.tfstate.name
  location            = azurerm_resource_group.tfstate.location
  tags                = merge(local.shared_tags, { environment = each.key })
}

resource "azurerm_federated_identity_credential" "pipeline_environment" {
  for_each = toset(var.environments)

  name                = "github-environment-${each.key}"
  resource_group_name = azurerm_resource_group.tfstate.name
  parent_id           = module.pipeline_identity[each.key].resource_id
  audience            = ["api://AzureADTokenExchange"]
  issuer              = "https://token.actions.githubusercontent.com"
  subject             = "repo:${var.github_org_repo}:environment:${each.key}"
}

resource "azurerm_role_assignment" "pipeline_storage_data" {
  for_each = toset(var.environments)

  principal_id         = module.pipeline_identity[each.key].principal_id
  role_definition_name = "Storage Blob Data Contributor"
  scope                = module.tfstate_storage.resource_id
}
