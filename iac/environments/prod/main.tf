# Spec 005 — `prod` environment composition template.
#
# Template only — NOT applied by spec 005 (Q1c env scope = dev only).
# When operators stand this env up, follow `specs/005-infrastructure-baseline/quickstart.md` §B.
# The module call graph mirrors `iac/environments/dev/main.tf` and the test
# template. Per-env behavior is driven entirely by the variable defaults in
# `variables.tf` and the operator-supplied `terraform.tfvars`. No dev-specific
# `import {}` adoptions or `moved {}` refactors are carried over because prod
# has no pre-existing state to adopt.

# Spec 005 — central naming convention. The naming module computes the names
# the modules below consume.
module "naming" {
  source = "../../modules/naming"

  environment_name = var.environment_name
  naming_prefix    = var.naming_prefix
  unique_suffix    = var.unique_suffix
}

locals {
  resource_group_name = module.naming.resource_group_name

  log_analytics_workspace_name = module.naming.log_analytics_workspace_name
  application_insights_name    = module.naming.application_insights_name
  key_vault_name               = module.naming.key_vault_name
  container_registry_name      = module.naming.container_registry_name
  container_apps_env_name      = module.naming.container_apps_env_name
  workload_identity_name       = module.naming.workload_uami_name
  cosmos_account_name          = module.naming.cosmos_account_name

  frontend_app_name = "ca-${var.naming_prefix}-web"
  backend_app_name  = "ca-${var.naming_prefix}-api"

  frontend_target_port = 3000
  backend_target_port  = 8080

  # Spec 005 — private DNS zones provisioned in prod. Storage + Azure Monitor
  # zones are deferred per research §14.
  private_dns_zone_names = [
    "privatelink.vaultcore.azure.net",
    "privatelink.documents.azure.com",
    "privatelink.search.windows.net",
    "privatelink.servicebus.windows.net",
    "privatelink.azurecr.io",
  ]

  shared_tags = merge(
    module.naming.mandatory_tags,
    var.tags,
  )
}

resource "azurerm_resource_group" "this" {
  name     = local.resource_group_name
  location = var.location
  tags     = local.shared_tags
}

# Spec 005 — VNet + subnets + private DNS zones. Prod ships with
# data_services_public_access_enabled = false AND private_endpoints_enabled = true
# (private-by-default per spec 005 / FR-031).
module "networking" {
  source = "../../modules/networking"

  vnet_name           = module.naming.vnet_name
  resource_group_name = azurerm_resource_group.this.name
  location            = azurerm_resource_group.this.location

  address_space                 = var.network_address_space
  subnet_integration_cidr       = var.subnet_integration_cidr
  subnet_private_endpoints_cidr = var.subnet_private_endpoints_cidr
  private_dns_zones             = local.private_dns_zone_names

  tags = local.shared_tags
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
  retention_in_days = var.log_analytics_retention_days

  tags = local.shared_tags
}

module "keyvault" {
  source = "../../modules/keyvault"

  name                       = local.key_vault_name
  resource_group_name        = azurerm_resource_group.this.name
  location                   = azurerm_resource_group.this.location
  log_analytics_workspace_id = module.monitoring.log_analytics_workspace_id

  public_network_access_enabled = var.data_services_public_access_enabled
  private_endpoint_enabled      = var.private_endpoints_enabled
  private_endpoint_subnet_id    = module.networking.subnet_private_endpoints_id
  private_dns_zone_id           = module.networking.private_dns_zone_ids["privatelink.vaultcore.azure.net"]

  tags = local.shared_tags
}

# Pipeline managed identity data-plane access to the env KV.
data "azurerm_client_config" "current" {}

resource "azurerm_role_assignment" "pipeline_kv_secrets_officer" {
  scope                = azurerm_resource_group.this.id
  role_definition_name = "Key Vault Secrets Officer"
  principal_id         = data.azurerm_client_config.current.object_id
  description          = "Pipeline MI manages `azurerm_key_vault_secret` resources in this env via AAD (shared keys are disabled on KV)."
}

# Operator standing access — populated via `kv_operator_object_ids`.
resource "azurerm_role_assignment" "operator_kv_secrets_officer" {
  for_each             = toset(var.kv_operator_object_ids)
  scope                = module.keyvault.id
  role_definition_name = "Key Vault Secrets Officer"
  principal_id         = each.value
  description          = "Standing operator access for bootstrap-secret seeding. Listed in `kv_operator_object_ids`."
}

resource "time_sleep" "wait_for_kv_rbac_propagation" {
  depends_on = [
    azurerm_role_assignment.pipeline_kv_secrets_officer,
    azurerm_role_assignment.operator_kv_secrets_officer,
  ]
  create_duration = "60s"
}

resource "azurerm_key_vault_secret" "app_insights_connection_string" {
  name         = "ApplicationInsightsConnectionString"
  value        = module.monitoring.application_insights_connection_string
  key_vault_id = module.keyvault.id
  content_type = "text/plain"

  # The App Insights connection string is a static identifier that does not
  # rotate. A far-future expiration date satisfies CKV_AZURE_41 without
  # imposing operational rotation overhead.
  expiration_date = "2099-12-31T23:59:59Z"

  tags = local.shared_tags

  depends_on = [time_sleep.wait_for_kv_rbac_propagation]
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

data "azuread_service_principal" "api" {
  client_id = var.entra_api_client_id
}

module "workload_identity" {
  source = "../../modules/workload-identity"

  name                = local.workload_identity_name
  resource_group_name = azurerm_resource_group.this.name
  location            = azurerm_resource_group.this.location
  environment         = var.environment_name
  workload            = "workload"

  # Spec 005 / FR-033 — non-data-service roles only. Data-service roles
  # (Search Index Data Contributor, SB Data Sender/Receiver, Cosmos Data
  # Contributor) are emitted by their owning modules / by the Cosmos SQL
  # role assignment below.
  assigned_azure_rbac = {
    acr-pull = {
      role_definition_name = "AcrPull"
      scope                = module.container_registry.id
    }
    kv-secrets-user = {
      role_definition_name = "Key Vault Secrets User"
      scope                = module.keyvault.id
    }
    monitoring-metrics-publisher = {
      role_definition_name = "Monitoring Metrics Publisher"
      scope                = module.monitoring.application_insights_id
    }
  }

  api_service_principal_object_id = data.azuread_service_principal.api.object_id

  assigned_api_app_roles = {
    reader = module.app_registration_roles.role_ids.reader
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
  # Spec 005 / T134 / FR-010 — prod default is `false` (internal-only ingress).
  # The backend is not exposed to the public internet by default. Operators
  # may override via tfvars if a use case requires external ingress.
  ingress_external = var.backend_external_ingress
  min_replicas     = var.backend_min_replicas
  max_replicas     = var.backend_max_replicas
  cpu              = 0.5
  memory           = "1Gi"

  env_vars = {
    ASPNETCORE_ENVIRONMENT = var.environment_name == "dev" ? "Development" : title(var.environment_name)
    ASPNETCORE_URLS        = "http://+:${local.backend_target_port}"
    AzureAd__Instance      = "https://login.microsoftonline.com/"
    AzureAd__TenantId      = var.entra_tenant_id
    AzureAd__ClientId      = var.entra_api_client_id
    AzureAd__Audience      = "api://${var.entra_api_client_id}"
    AZURE_KEY_VAULT_URI    = module.keyvault.uri
    AZURE_CLIENT_ID        = module.workload_identity.client_id

    # Spec 005 / Q1c (research §6) — backend .NET OpenTelemetry exporter
    # authenticates to App Insights ingestion via AAD using the workload UAMI.
    APPLICATIONINSIGHTS_AUTHENTICATION_STRING = "Authorization=AAD;ClientId=${module.workload_identity.client_id}"
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
    NODE_ENV                       = "production"
    PORT                           = tostring(local.frontend_target_port)
    NEXT_PUBLIC_API_BASE_URL       = "https://${module.backend_app.fqdn_url}"
    NEXT_PUBLIC_AZURE_AD_TENANT_ID = var.entra_tenant_id
    NEXT_PUBLIC_AZURE_AD_CLIENT_ID = var.entra_web_client_id
    NEXT_PUBLIC_API_SCOPE          = "api://${var.entra_api_client_id}/.default"
    AZURE_KEY_VAULT_URI            = module.keyvault.uri
    AZURE_CLIENT_ID                = module.workload_identity.client_id
  }

  secret_env_vars = {
    NEXT_PUBLIC_APPINSIGHTS_CONNECTION_STRING = "appinsights-connection-string"
  }

  key_vault_secrets = {
    appinsights-connection-string = azurerm_key_vault_secret.app_insights_connection_string.versionless_id
  }

  tags = local.shared_tags
}

# Spec 005 / T084 — Container App diagnostic settings routed through the
# central `diagnostic-settings` wrapper (Q5c: `allLogs` only, no metrics).
module "backend_app_diagnostics" {
  source = "../../modules/diagnostic-settings"

  name                       = "ca-backend-diagnostics"
  target_resource_id         = module.backend_app.id
  log_analytics_workspace_id = module.monitoring.log_analytics_workspace_id
}

module "frontend_app_diagnostics" {
  source = "../../modules/diagnostic-settings"

  name                       = "ca-frontend-diagnostics"
  target_resource_id         = module.frontend_app.id
  log_analytics_workspace_id = module.monitoring.log_analytics_workspace_id
}

# Spec 005 / T086 — Application Insights diagnostic setting forwarding `allLogs`
# to the env LAW.
module "application_insights_diagnostics" {
  source = "../../modules/diagnostic-settings"

  name                       = "appi-diagnostics"
  target_resource_id         = module.monitoring.application_insights_id
  log_analytics_workspace_id = module.monitoring.log_analytics_workspace_id
}

module "workload_federation_environment" {
  source = "../../modules/federated-credential"

  name                = "github-environment-${var.environment_name}-workload"
  resource_group_name = azurerm_resource_group.this.name
  parent_id           = module.workload_identity.id
  subject             = "repo:${var.github_org_repo}:environment:${var.environment_name}"
}

# Spec 003 — platform app roles on the API app registration.
data "azuread_application" "api" {
  client_id = var.entra_api_client_id
}

# Spec 003 / US3 / SC-003 — opt-in internal-caller probe job.
module "probe_job_internal_caller" {
  count  = var.probe_job_enabled ? 1 : 0
  source = "../../modules/probe-job-internal-caller"

  name                          = "caj-${var.naming_prefix}-probe-internal-caller"
  resource_group_name           = azurerm_resource_group.this.name
  location                      = azurerm_resource_group.this.location
  container_apps_environment_id = module.container_apps_env.id
  managed_identity_id           = module.workload_identity.id
  workload_identity_client_id   = module.workload_identity.client_id
  api_url                       = "https://${module.backend_app.fqdn_url}"
  api_scope                     = "api://${var.entra_api_client_id}/.default"

  tags = local.shared_tags
}

# Spec 003 / US6 / FR-024 — Microsoft Graph application-permission grant on
# the API app registration.
module "graph_permissions" {
  source = "../../modules/graph-permissions"

  api_application_id = data.azuread_application.api.id

  granted_application_permission_ids = [
    "df021288-bdef-4463-88db-98f22de89214", # User.Read.All (Application)
  ]
}

# Spec 004 — Cosmos DB account hosting the canonical metadata store.
module "cosmos_account" {
  source = "../../modules/cosmos-account"

  name                       = local.cosmos_account_name
  resource_group_name        = azurerm_resource_group.this.name
  location                   = azurerm_resource_group.this.location
  log_analytics_workspace_id = module.monitoring.log_analytics_workspace_id

  public_network_access_enabled = var.data_services_public_access_enabled
  private_endpoint_enabled      = var.private_endpoints_enabled
  private_endpoint_subnet_id    = module.networking.subnet_private_endpoints_id
  private_dns_zone_id           = module.networking.private_dns_zone_ids["privatelink.documents.azure.com"]

  tags = local.shared_tags
}

module "cosmos_canonical_store" {
  source = "../../modules/cosmos-canonical-store"

  cosmos_account_name = module.cosmos_account.account_name
  cosmos_account_id   = module.cosmos_account.account_id
  resource_group_name = azurerm_resource_group.this.name
  database_name       = var.canonical_db_name
}

resource "azurerm_cosmosdb_sql_role_assignment" "workload_data_contributor" {
  resource_group_name = azurerm_resource_group.this.name
  account_name        = module.cosmos_account.account_name
  role_definition_id  = "${module.cosmos_account.account_id}/sqlRoleDefinitions/00000000-0000-0000-0000-000000000002"
  scope               = module.cosmos_canonical_store.canonical_database_role_scope
  principal_id        = module.workload_identity.principal_id
}

resource "azurerm_cosmosdb_sql_role_assignment" "developer_data_contributor" {
  resource_group_name = azurerm_resource_group.this.name
  account_name        = module.cosmos_account.account_name
  role_definition_id  = "${module.cosmos_account.account_id}/sqlRoleDefinitions/00000000-0000-0000-0000-000000000002"
  scope               = module.cosmos_canonical_store.canonical_database_role_scope
  principal_id        = data.azurerm_client_config.current.object_id
}

# Spec 005 — Azure AI Search service.
module "ai_search" {
  source = "../../modules/ai-search"

  name                = module.naming.ai_search_name
  resource_group_name = azurerm_resource_group.this.name
  location            = azurerm_resource_group.this.location

  sku                           = var.ai_search_sku
  public_network_access_enabled = var.data_services_public_access_enabled

  log_analytics_workspace_id = module.monitoring.log_analytics_workspace_id
  workload_principal_id      = module.workload_identity.principal_id

  private_endpoint_enabled   = var.private_endpoints_enabled
  private_endpoint_subnet_id = module.networking.subnet_private_endpoints_id
  private_dns_zone_id        = module.networking.private_dns_zone_ids["privatelink.search.windows.net"]

  tags = local.shared_tags
}

# Spec 005 — Service Bus namespace. Prod default is Premium SKU — PE is
# attached when private_endpoints_enabled = true.
module "service_bus" {
  source = "../../modules/service-bus"

  name                = module.naming.service_bus_name
  resource_group_name = azurerm_resource_group.this.name
  location            = azurerm_resource_group.this.location

  sku                           = var.service_bus_sku
  capacity                      = var.service_bus_capacity
  public_network_access_enabled = var.data_services_public_access_enabled

  log_analytics_workspace_id = module.monitoring.log_analytics_workspace_id
  workload_principal_id      = module.workload_identity.principal_id

  # SKU-conditional PE wiring: only attach a PE when SKU=Premium AND PEs
  # are enabled. The service-bus module's precondition fires if Standard SKU
  # receives non-null PE inputs.
  private_endpoint_subnet_id = (var.service_bus_sku == "Premium" && var.private_endpoints_enabled) ? module.networking.subnet_private_endpoints_id : null
  private_dns_zone_id        = (var.service_bus_sku == "Premium" && var.private_endpoints_enabled) ? module.networking.private_dns_zone_ids["privatelink.servicebus.windows.net"] : null

  tags = local.shared_tags
}

module "app_registration_roles" {
  source = "../../modules/app-registration-roles"

  api_application_id = data.azuread_application.api.id

  role_definitions = {
    admin = {
      role_id              = var.platform_role_ids.admin
      value                = "BusTerminal.Admin"
      display_name         = "BusTerminal Administrator"
      description          = "Full administrative access. Authorizes every operation class: Read, MutateDomain, OperatePlatform, Administer, DeveloperTooling."
      allowed_member_types = ["User", "Application"]
    }
    operator = {
      role_id              = var.platform_role_ids.operator
      value                = "BusTerminal.Operator"
      display_name         = "BusTerminal Operator"
      description          = "Operational management access. Authorizes Read, MutateDomain, OperatePlatform."
      allowed_member_types = ["User", "Application"]
    }
    reader = {
      role_id              = var.platform_role_ids.reader
      value                = "BusTerminal.Reader"
      display_name         = "BusTerminal Reader"
      description          = "Read-only access to platform and domain state."
      allowed_member_types = ["User", "Application"]
    }
    developer = {
      role_id              = var.platform_role_ids.developer
      value                = "BusTerminal.Developer"
      display_name         = "BusTerminal Developer"
      description          = "API/spec/developer-tooling access. Authorizes Read and DeveloperTooling."
      allowed_member_types = ["User", "Application"]
    }
  }
}
