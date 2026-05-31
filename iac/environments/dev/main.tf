# Spec 005 — central naming convention (T059). The naming module computes the
# same names the inline locals below produced for spec 002 resources, plus the
# new spec 005 names (ai_search_name, service_bus_name, vnet_name,
# workload_uami_name). Names match the previous spec-002 computation exactly,
# so introducing the module is a non-destructive refactor — zero state churn.
module "naming" {
  source = "../../modules/naming"

  environment_name = var.environment_name
  naming_prefix    = var.naming_prefix
  unique_suffix    = var.unique_suffix
}

locals {
  # Existing spec-002 names retained as locals so the existing module-call
  # graph below doesn't churn. Each value is identical to the corresponding
  # `module.naming.*` output by construction.
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

  # Spec 005 — private DNS zones provisioned in dev. SB zone is included even
  # though the dev SB SKU is Standard (no PE) so the Premium upgrade is a
  # single attribute flip. ACR zone is included for the same reason (PE
  # deferred per research §2 but the zone is warm). Storage + Azure Monitor
  # zones are deferred to the test/prod template (research §14).
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

  # Spec 005 / US7 / T121 — every env-scoped resource lives in this RG.
  # Destroying it would cascade-delete every stateful child (Cosmos, KV,
  # ACR, LAW, App Insights, ACE, workload UAMI). BT-IAC-007 is primary;
  # this is the secondary block. To intentionally tear down the env,
  # remove this block in a dedicated PR (and expect manual approval).
  lifecycle {
    prevent_destroy = true
  }
}

# Spec 005 — VNet + subnets + private DNS zones (T060). Provisioned WARM in
# dev per Q2c: PE subnet exists, zones exist, links exist — public access on
# the data services stays on via `data_services_public_access_enabled = true`
# until a future destructive-retrofit slice flips it. Allocating the CAE
# integration subnet now means the Container Apps Environment VNet-integration
# retrofit is a single attribute flip later (no destructive subnet replacement).
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

  # Spec 005 (T063) — KV public access tied to per-env toggle; PE inputs
  # warm-on-by-default in dev per Q2c (both flags default true in dev tfvars
  # so both wires are active simultaneously — public access stays on AND a PE
  # exists, until a future destructive-retrofit slice flips public access off).
  #
  # `private_endpoint_enabled` is the plan-time bool that drives the
  # conditional PE child module; subnet_id + dns_zone_id are passed
  # unconditionally (known-after-apply outputs) and the module's precondition
  # validates them when enabled.
  public_network_access_enabled = var.data_services_public_access_enabled
  private_endpoint_enabled      = var.private_endpoints_enabled
  private_endpoint_subnet_id    = module.networking.subnet_private_endpoints_id
  private_dns_zone_id           = module.networking.private_dns_zone_ids["privatelink.vaultcore.azure.net"]

  # Spec 005 / US7 / T122 — per-env purge-protection + soft-delete tuning
  # per FR-019. Dev's deployed KV has purge protection ON (Azure forbids
  # disabling once set); tfvars sets the var to true so plan is a no-op.
  purge_protection_enabled   = var.key_vault_purge_protection_enabled
  soft_delete_retention_days = var.key_vault_soft_delete_retention_days

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

  # Spec 005 / FR-033 — non-data-service roles only. Data-service roles
  # (Search Index Data Contributor, SB Data Sender/Receiver, Cosmos Data
  # Contributor) are emitted by their owning modules / by the Cosmos SQL
  # role assignment below. See iac/modules/workload-identity/README.md
  # § Role-assignment split convention.
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
  # Spec 005 / T134 / FR-010 — backend ingress externality is per-env;
  # dev default is `true` (preserves the existing developer + smoke-test
  # workflow that hits the backend over the public internet).
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
    # authenticates to App Insights ingestion via AAD using the workload
    # UAMI. The ClientId discriminates between MIs when multiple are
    # attached. `APPLICATIONINSIGHTS_CONNECTION_STRING` still flows from
    # KV below (the exporter needs both: connection string to identify
    # the target component, auth string to authenticate to it). The
    # `Monitoring Metrics Publisher` role is granted on the App Insights
    # resource via `module.workload_identity.assigned_azure_rbac`.
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

# Spec 005 / T084 (revised post-merge defect fix) — per-Container-App
# diagnostic settings are intentionally NOT provisioned. Azure rejects them:
#
#   - The Container App resource type accepts diagnostic settings with ONLY
#     `--metrics AllMetrics`; the `--logs` parameter is not supported at the
#     per-app level (verified via Microsoft Learn `log-options#configure-
#     logging-options`).
#   - Spec 005 / Q5c forbids forwarding `AllMetrics` to Log Analytics. With
#     logs disallowed by Azure and metrics disallowed by Q5c, there is no
#     valid shape for a per-app diagnostic setting.
#
# Container App logs (`ContainerAppConsoleLogs`, `ContainerAppSystemLogs`)
# still reach the env Log Analytics Workspace via the Container Apps
# Environment's `allLogs` diagnostic setting (wired inside the
# container-apps-env module's AVM call). Per-app diagnostics would be a
# duplicate sink even if Azure accepted them.
#
# Removed in main: pre-spec-005 had `azurerm_monitor_diagnostic_setting.
# backend_app` + `.frontend_app` carrying `enabled_metric AllMetrics` only.
# Spec 005 routed them through the `diagnostic-settings` wrapper, which
# Azure then rejected (`CategoryGroup: 'allLogs' is not supported,
# supported ones are: ''`). Both blocks are now deleted; `tofu apply`
# will destroy the in-state resources.

# Spec 005 / T086 — Application Insights diagnostic setting forwarding `allLogs`
# to the env LAW. AI Search and Service Bus diagnostics are emitted inside their
# own modules (T037, T045). Key Vault, ACR, Container Apps Environment, and
# Cosmos diagnostics are emitted inside those modules. This call handles
# App Insights, which has no module wrapper of its own — the AVM `insights-component`
# module does not expose a `diagnostic_settings` input, so we wire the wrapper
# at the composition level.
module "application_insights_diagnostics" {
  source = "../../modules/diagnostic-settings"

  name                       = "appi-diagnostics"
  target_resource_id         = module.monitoring.application_insights_id
  log_analytics_workspace_id = module.monitoring.log_analytics_workspace_id
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

# Spec 003 / US6 / FR-024 — Microsoft Graph application-permission grant on
# the API app registration. Declares `User.Read.All` (the sole permission this
# slice grants — `contracts/graph-permissions-inventory.md` is the binding
# inventory). Admin consent is NOT performed by Tofu (FR-024 + research § 9):
# after `tofu apply`, a tenant admin must grant consent in the Entra portal
# (or `az ad app permission admin-consent --id <bt-dev-api-app-id>`) before
# any `IGraphClient` call succeeds. See quickstart.md § A.2.3.
module "graph_permissions" {
  source = "../../modules/graph-permissions"

  api_application_id = data.azuread_application.api.id

  granted_application_permission_ids = [
    "df021288-bdef-4463-88db-98f22de89214", # User.Read.All (Application)
  ]
}

# Spec 004 — Cosmos DB account hosting the canonical metadata store.
#
# Spec 005 (T063) extends this call with a per-env public-access toggle and a
# warm private endpoint. Dev defaults to BOTH public-access ON AND a PE
# attached (Q2c) — the destructive retrofit (flipping public access off)
# is a future spec.
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

# Workload UAMI gets data-contributor on the canonical database scope. The
# built-in "Cosmos DB Built-in Data Contributor" role GUID is well-known:
# 00000000-0000-0000-0000-000000000002. `azurerm_cosmosdb_sql_role_assignment`
# expects the FULL resource id of the role definition, not the bare GUID.
#
# Spec 005 / T073 — the data-plane scope path is computed inside the
# `cosmos-canonical-store` module and surfaced as
# `canonical_database_role_scope` so this composition (and any future spec)
# can't re-rediscover the ARM-vs-data-plane-path trap (research §15).
resource "azurerm_cosmosdb_sql_role_assignment" "workload_data_contributor" {
  resource_group_name = azurerm_resource_group.this.name
  account_name        = module.cosmos_account.account_name
  role_definition_id  = "${module.cosmos_account.account_id}/sqlRoleDefinitions/00000000-0000-0000-0000-000000000002"
  scope               = module.cosmos_canonical_store.canonical_database_role_scope
  principal_id        = module.workload_identity.principal_id
}

# Developer (and pipeline) standing data-contributor on the canonical database so
# `dotnet run --project tools/load-fixtures --auth aad` works from developer
# machines per quickstart.md Path B. Uses the same caller identity that already
# manages KV secrets (the pipeline MI in CI, the developer's az-login principal
# locally).
resource "azurerm_cosmosdb_sql_role_assignment" "developer_data_contributor" {
  resource_group_name = azurerm_resource_group.this.name
  account_name        = module.cosmos_account.account_name
  role_definition_id  = "${module.cosmos_account.account_id}/sqlRoleDefinitions/00000000-0000-0000-0000-000000000002"
  scope               = module.cosmos_canonical_store.canonical_database_role_scope
  principal_id        = data.azurerm_client_config.current.object_id
}

# Spec 005 — Azure AI Search service (T061). Warm PE in dev per Q2c.
# Workload UAMI is granted `Search Index Data Contributor` inside the module
# (per the FR-033 forward-looking workload RBAC enumeration).
module "ai_search" {
  source = "../../modules/ai-search"

  name                = module.naming.ai_search_name
  resource_group_name = azurerm_resource_group.this.name
  # Allow overriding the search service's region independently of the env
  # (var.ai_search_location). Defaults to the env location when null.
  location = coalesce(var.ai_search_location, azurerm_resource_group.this.location)

  sku                           = var.ai_search_sku
  public_network_access_enabled = var.data_services_public_access_enabled

  log_analytics_workspace_id = module.monitoring.log_analytics_workspace_id
  workload_principal_id      = module.workload_identity.principal_id

  private_endpoint_enabled   = var.private_endpoints_enabled
  private_endpoint_subnet_id = module.networking.subnet_private_endpoints_id
  private_dns_zone_id        = module.networking.private_dns_zone_ids["privatelink.search.windows.net"]

  tags = local.shared_tags
}

# Spec 005 — Service Bus namespace (T062). Dev defaults to Standard SKU — no
# PE on Standard (per research §3 + service-bus module precondition). The PE
# inputs are SKU-conditionally nulled here so the same composition file works
# unchanged when an operator overrides `service_bus_sku = "Premium"` via
# tfvars. Workload UAMI is granted both `Azure Service Bus Data Sender` AND
# `Azure Service Bus Data Receiver` inside the module (FR-033).
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
