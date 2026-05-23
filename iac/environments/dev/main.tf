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

# Pipeline managed identity data-plane access to the env KV.
#
# The composition's principal in CI IS the pipeline managed identity (federated
# from GitHub OIDC, see iac/platform-bootstrap). `azurerm_key_vault_secret`
# resources require Key Vault data-plane RBAC; this self-grant supplies it.
# The bootstrap RBAC-Admin condition explicitly allows assigning this role
# (b86a8fe4-...) so this `azurerm_role_assignment` write succeeds with the
# pipeline's existing permissions.
#
# Scope is the RG (not the KV) so any future env-scoped KV in this RG inherits
# the grant — operationally simpler than per-KV assignments.
data "azurerm_client_config" "current" {}

resource "azurerm_role_assignment" "pipeline_kv_secrets_officer" {
  scope                = azurerm_resource_group.this.id
  role_definition_name = "Key Vault Secrets Officer"
  principal_id         = data.azurerm_client_config.current.object_id
  description          = "Pipeline MI manages `azurerm_key_vault_secret` resources in this env via AAD (shared keys are disabled on KV)."
}

# Operator standing access — populated via `kv_operator_object_ids`. Each
# entry gets `Key Vault Secrets Officer` scoped to the env KV so on-call
# humans can set any future bootstrap secrets via `az keyvault secret set`
# without an out-of-band grant. (Spec 003 removed the NextAuth/web-client
# secrets; this access is now reserved for future workload secrets only.)
resource "azurerm_role_assignment" "operator_kv_secrets_officer" {
  for_each             = toset(var.kv_operator_object_ids)
  scope                = module.keyvault.id
  role_definition_name = "Key Vault Secrets Officer"
  principal_id         = each.value
  description          = "Standing operator access for bootstrap-secret seeding. Listed in `kv_operator_object_ids`."
}

# Azure AD role assignments have eventual-consistency propagation. Without
# this sleep, the first `azurerm_key_vault_secret` create races propagation
# of the role assignment and 403s.
resource "time_sleep" "wait_for_kv_rbac_propagation" {
  depends_on = [
    azurerm_role_assignment.pipeline_kv_secrets_officer,
    azurerm_role_assignment.operator_kv_secrets_officer,
  ]
  create_duration = "60s"
}

# One-time imports for role assignments that were created manually via `az`
# during the initial dev bootstrap (2026-05-19). Tofu adopts them into state
# instead of failing on "resource already exists". Import blocks are
# idempotent — they're a no-op once the resource is in state — and can stay
# in tree until the team forgets why they exist (or be removed in a later
# cleanup commit).
import {
  to = azurerm_role_assignment.pipeline_kv_secrets_officer
  id = "/subscriptions/08b37dc0-0011-4841-84c0-0349a5c65883/resourceGroups/rg-bt-dev/providers/Microsoft.Authorization/roleAssignments/d78aec1f-2553-464e-9be2-bd382da9818c"
}

import {
  to = azurerm_role_assignment.operator_kv_secrets_officer["62936c0c-a840-43e8-a24e-22304b7d7c89"]
  id = "/subscriptions/08b37dc0-0011-4841-84c0-0349a5c65883/resourceGroups/rg-bt-dev/providers/Microsoft.KeyVault/vaults/kv-bt-dev-chdev01/providers/Microsoft.Authorization/roleAssignments/12d618e9-a9ca-4e42-82a7-dc077b5c5a78"
}

# App Insights connection-string secret in Key Vault, exposed to workloads as a
# Container Apps secret backed by this Key Vault URI. Placed here (not in the
# monitoring module) to avoid the KV ↔ monitoring cycle.
resource "azurerm_key_vault_secret" "app_insights_connection_string" {
  name         = "ApplicationInsightsConnectionString"
  value        = module.monitoring.application_insights_connection_string
  key_vault_id = module.keyvault.id
  content_type = "text/plain"

  # The App Insights connection string is a static identifier that does not
  # rotate. A far-future expiration date satisfies CKV_AZURE_41 (which
  # requires every secret have an expiration) without imposing operational
  # rotation overhead that would not actually rotate anything.
  expiration_date = "2099-12-31T23:59:59Z"

  tags = local.shared_tags

  # Wait for the KV data-plane RBAC to propagate before tofu attempts the
  # data-plane secret write. Only matters on first-apply against a fresh
  # state (subsequent applies already have the role).
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

# Spec 003 — workload MI promoted to the generalized `workload-identity` module
# so it can carry API app-role assignments (FR-022) alongside its existing
# Azure RBAC. Internal addresses are preserved against the old `identity`
# module — no `moved` blocks needed for the UAMI / RBAC state.
#
# The `BusTerminal.Reader` app-role assignment grants the workload MI standing
# read access to the API: this is what an internal Container Apps Job /
# Functions caller exercises via `DefaultAzureCredential` → `az get-access-token`
# → `Authorization: Bearer <token>` → API role policy (FR-022 / SC-003).
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

  assigned_azure_rbac = {
    acr-pull = {
      role_definition_name = "AcrPull"
      scope                = module.container_registry.id
    }
    kv-secrets-user = {
      role_definition_name = "Key Vault Secrets User"
      scope                = module.keyvault.id
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
#
# Spec 003 / US5 / FR-029 — composed via the generalized
# `federated-credential` module. Issuer + audience use the module's GitHub
# Actions / Entra-workload-identity defaults. The `moved` block keeps Tofu
# state intact across the refactor.
module "workload_federation_environment" {
  source = "../../modules/federated-credential"

  name                = "github-environment-${var.environment_name}-workload"
  resource_group_name = azurerm_resource_group.this.name
  parent_id           = module.workload_identity.id
  subject             = "repo:${var.github_org_repo}:environment:${var.environment_name}"
}

moved {
  from = azurerm_federated_identity_credential.workload_environment
  to   = module.workload_federation_environment.azurerm_federated_identity_credential.this
}

# Spec 003 — platform app roles on the API app registration.
#
# The `bt-dev-api` app registration was created out-of-band in 002 and is not
# a tofu-managed `azuread_application` resource. Reference it via a data source
# and pass its id into the new `app-registration-roles` module. Because the
# parent app is not a managed resource, the module's "ignore_changes = [app_role]"
# lifecycle requirement (which applies when the parent IS managed) does not
# apply here. See iac/modules/app-registration-roles/README.md.
data "azuread_application" "api" {
  client_id = var.entra_api_client_id
}

# Spec 003 / US3 / SC-003 — opt-in internal-caller probe job. Disabled by
# default; flip `probe_job_enabled` to true (via `-var` or tfvars) to
# provision the smoke. The job exits 0 on `GET /probe/read` returning 200
# using a token acquired by the workload MI. See
# `iac/modules/probe-job-internal-caller/README.md` and
# `docs/internal-workload-callers.md` § Worked example.
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
