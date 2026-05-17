locals {
  resource_group_name = "rg-${var.naming_prefix}"

  log_analytics_workspace_name = "log-${var.naming_prefix}"
  application_insights_name    = "appi-${var.naming_prefix}"
  key_vault_name               = "kv-${var.naming_prefix}-${var.unique_suffix}"
  container_registry_name      = replace("acr${var.naming_prefix}${var.unique_suffix}", "-", "")
  container_apps_env_name      = "cae-${var.naming_prefix}"
  workload_identity_name       = "mi-${var.naming_prefix}-workload"

  frontend_app_name = "ca-${var.naming_prefix}-web"
  backend_app_name  = "ca-${var.naming_prefix}-api"

  frontend_target_port = 3000
  backend_target_port  = 8080

  shared_tags = merge(
    {
      application = "BusTerminal"
      environment = var.environment_name
      managed-by  = "opentofu"
      cost-center = "platform"
    },
    var.tags,
  )
}

resource "azurerm_resource_group" "this" {
  name     = local.resource_group_name
  location = var.location
  tags     = local.shared_tags
}

# Monitoring is split from the Key Vault secret creation so the dependency
# graph stays acyclic: LAW → KV (KV reads LAW for diagnostic settings) → KV
# secret holding the App Insights connection string (created below, outside
# the monitoring module).
module "monitoring" {
  source = "../../modules/monitoring"

  log_analytics_workspace_name = local.log_analytics_workspace_name
  application_insights_name    = local.application_insights_name
  resource_group_name          = azurerm_resource_group.this.name
  location                     = azurerm_resource_group.this.location

  key_vault_id      = null
  retention_in_days = 30

  tags = local.shared_tags
}

module "keyvault" {
  source = "../../modules/keyvault"

  name                       = local.key_vault_name
  resource_group_name        = azurerm_resource_group.this.name
  location                   = azurerm_resource_group.this.location
  log_analytics_workspace_id = module.monitoring.log_analytics_workspace_id

  tags = local.shared_tags
}

# App Insights connection-string secret in Key Vault, exposed to workloads as a
# Container Apps secret backed by this Key Vault URI. Placed here (not in the
# monitoring module) to avoid the KV ↔ monitoring cycle.
resource "azurerm_key_vault_secret" "app_insights_connection_string" {
  name         = "ApplicationInsightsConnectionString"
  value        = module.monitoring.application_insights_connection_string
  key_vault_id = module.keyvault.id
  content_type = "text/plain"

  tags = local.shared_tags
}

module "container_registry" {
  source = "../../modules/container-registry"

  name                          = local.container_registry_name
  resource_group_name           = azurerm_resource_group.this.name
  location                      = azurerm_resource_group.this.location
  sku                           = "Premium"
  public_network_access_enabled = true
  log_analytics_workspace_id    = module.monitoring.log_analytics_workspace_id

  tags = local.shared_tags
}

module "workload_identity" {
  source = "../../modules/identity"

  name                = local.workload_identity_name
  resource_group_name = azurerm_resource_group.this.name
  location            = azurerm_resource_group.this.location

  role_assignments = {
    acr-pull = {
      role_definition_name = "AcrPull"
      scope                = module.container_registry.id
    }
    kv-secrets-user = {
      role_definition_name = "Key Vault Secrets User"
      scope                = module.keyvault.id
    }
  }

  tags = local.shared_tags
}

module "container_apps_env" {
  source = "../../modules/container-apps-env"

  name                                   = local.container_apps_env_name
  resource_group_name                    = azurerm_resource_group.this.name
  location                               = azurerm_resource_group.this.location
  log_analytics_workspace_name           = local.log_analytics_workspace_name
  log_analytics_workspace_resource_group = azurerm_resource_group.this.name
  zone_redundancy_enabled                = false

  tags = local.shared_tags

  depends_on = [module.monitoring]
}

module "backend_app" {
  source = "../../modules/container-app"

  name                          = local.backend_app_name
  resource_group_name           = azurerm_resource_group.this.name
  container_apps_environment_id = module.container_apps_env.id
  managed_identity_id           = module.workload_identity.id
  image                         = var.backend_image
  registry_login_server         = module.container_registry.login_server
  target_port                   = local.backend_target_port
  ingress_external              = true
  min_replicas                  = var.backend_min_replicas
  max_replicas                  = var.backend_max_replicas
  cpu                           = 0.5
  memory                        = "1Gi"

  env_vars = {
    ASPNETCORE_ENVIRONMENT = var.environment_name == "dev" ? "Development" : title(var.environment_name)
    ASPNETCORE_URLS        = "http://+:${local.backend_target_port}"
    AzureAd__Instance      = "https://login.microsoftonline.com/"
    AzureAd__TenantId      = var.entra_tenant_id
    AzureAd__ClientId      = var.entra_api_client_id
    AzureAd__Audience      = "api://${var.entra_api_client_id}"
    AZURE_KEY_VAULT_URI    = module.keyvault.uri
    AZURE_CLIENT_ID        = module.workload_identity.client_id
  }

  secret_env_vars = {
    APPLICATIONINSIGHTS_CONNECTION_STRING = "appinsights-connection-string"
  }

  key_vault_secrets = {
    appinsights-connection-string = azurerm_key_vault_secret.app_insights_connection_string.versionless_id
  }

  tags = local.shared_tags
}

module "frontend_app" {
  source = "../../modules/container-app"

  name                          = local.frontend_app_name
  resource_group_name           = azurerm_resource_group.this.name
  container_apps_environment_id = module.container_apps_env.id
  managed_identity_id           = module.workload_identity.id
  image                         = var.frontend_image
  registry_login_server         = module.container_registry.login_server
  target_port                   = local.frontend_target_port
  ingress_external              = true
  min_replicas                  = var.frontend_min_replicas
  max_replicas                  = var.frontend_max_replicas
  cpu                           = 0.5
  memory                        = "1Gi"

  env_vars = {
    NODE_ENV                 = "production"
    PORT                     = tostring(local.frontend_target_port)
    NEXT_PUBLIC_API_BASE_URL = "https://${module.backend_app.fqdn_url}"
    NEXTAUTH_URL             = "https://${local.frontend_app_name}.${module.container_apps_env.default_domain}"
    AZURE_AD_TENANT_ID       = var.entra_tenant_id
    AZURE_AD_CLIENT_ID       = var.entra_web_client_id
    AZURE_KEY_VAULT_URI      = module.keyvault.uri
    AZURE_CLIENT_ID          = module.workload_identity.client_id
  }

  secret_env_vars = {
    AZURE_AD_CLIENT_SECRET                    = "web-client-secret"
    NEXTAUTH_SECRET                           = "nextauth-secret"
    NEXT_PUBLIC_APPINSIGHTS_CONNECTION_STRING = "appinsights-connection-string"
  }

  key_vault_secrets = {
    appinsights-connection-string = azurerm_key_vault_secret.app_insights_connection_string.versionless_id
    web-client-secret             = "${module.keyvault.uri}secrets/WebClientSecret"
    nextauth-secret               = "${module.keyvault.uri}secrets/NextAuthSecret"
  }

  tags = local.shared_tags
}

# FR-072 — every Azure resource routes diagnostic logs + AllMetrics to the LAW.
# AVM modules already wire diagnostic settings on Key Vault, ACR, and the
# Container Apps Environment. Container App resources themselves do not have a
# `diagnostic_settings` parameter on the AVM module today, so we wire AllMetrics
# explicitly here (logs flow through the Environment binding to the LAW).
resource "azurerm_monitor_diagnostic_setting" "backend_app" {
  name                       = "ca-backend-diagnostics"
  target_resource_id         = module.backend_app.id
  log_analytics_workspace_id = module.monitoring.log_analytics_workspace_id

  enabled_metric {
    category = "AllMetrics"
  }
}

resource "azurerm_monitor_diagnostic_setting" "frontend_app" {
  name                       = "ca-frontend-diagnostics"
  target_resource_id         = module.frontend_app.id
  log_analytics_workspace_id = module.monitoring.log_analytics_workspace_id

  enabled_metric {
    category = "AllMetrics"
  }
}

# Workload-identity federated credentials so the running workloads can obtain
# Entra-issued tokens via the workload's user-assigned MI when needed by future
# slices (e.g., post-deploy smoke test using the pipeline identity to call the
# deployed `/whoami`). Subject scopes the credential to this environment.
resource "azurerm_federated_identity_credential" "workload_environment" {
  name                = "github-environment-${var.environment_name}-workload"
  resource_group_name = azurerm_resource_group.this.name
  parent_id           = module.workload_identity.id
  audience            = ["api://AzureADTokenExchange"]
  issuer              = "https://token.actions.githubusercontent.com"
  subject             = "repo:${var.github_org_repo}:environment:${var.environment_name}"
}
