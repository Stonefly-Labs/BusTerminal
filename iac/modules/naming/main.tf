locals {
  # Azure Container Registry name forbids hyphens, so strip them from prefix and suffix.
  acr_prefix = replace(var.naming_prefix, "-", "")
  acr_suffix = var.unique_suffix

  names = {
    resource_group_name          = "rg-${var.naming_prefix}"
    log_analytics_workspace_name = "log-${var.naming_prefix}"
    application_insights_name    = "appi-${var.naming_prefix}"
    key_vault_name               = "kv-${var.naming_prefix}-${var.unique_suffix}"
    container_registry_name      = "acr${local.acr_prefix}${local.acr_suffix}"
    container_apps_env_name      = "cae-${var.naming_prefix}"
    cosmos_account_name          = "cosmos-${var.naming_prefix}-${var.unique_suffix}"
    ai_search_name               = "srch-${var.naming_prefix}-${var.unique_suffix}"
    service_bus_name             = "sbns-${var.naming_prefix}-${var.unique_suffix}"
    vnet_name                    = "vnet-${var.naming_prefix}"
    workload_uami_name           = "mi-${var.naming_prefix}-workload"
  }
}
