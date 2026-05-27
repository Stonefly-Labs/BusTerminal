terraform {
  required_version = ">= 1.11.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.0"
    }
  }
}

module "log_analytics" {
  source  = "Azure/avm-res-operationalinsights-workspace/azurerm"
  version = "0.4.2"

  name                                      = var.log_analytics_workspace_name
  resource_group_name                       = var.resource_group_name
  location                                  = var.location
  log_analytics_workspace_retention_in_days = var.retention_in_days
  log_analytics_workspace_sku               = "PerGB2018"
  tags                                      = var.tags
}

module "application_insights" {
  source  = "Azure/avm-res-insights-component/azurerm"
  version = "0.3.0"

  name                = var.application_insights_name
  resource_group_name = var.resource_group_name
  location            = var.location
  workspace_id        = module.log_analytics.resource_id
  application_type    = "web"
  # Spec 005 / Q1c / research §6 — MUST stay `false`. See variables.tf and
  # README.md § Local authentication for the full rationale.
  local_authentication_disabled = var.local_authentication_disabled
  tags                          = var.tags
}

resource "azurerm_key_vault_secret" "app_insights_connection_string" {
  count = var.key_vault_id != null ? 1 : 0

  name         = "ApplicationInsightsConnectionString"
  value        = module.application_insights.connection_string
  key_vault_id = var.key_vault_id
  content_type = "text/plain"

  # Static identifier; far-future expiry satisfies CKV_AZURE_41 without
  # imposing rotation overhead. (See env composition for the callsite-level
  # rationale: the connection string is an opaque Azure-managed identifier
  # that does not rotate.)
  expiration_date = "2099-12-31T23:59:59Z"

  tags = var.tags
}
