terraform {
  required_version = ">= 1.11.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.0"
    }
    azuread = {
      source  = "hashicorp/azuread"
      version = "~> 3.0"
    }
    time = {
      source  = "hashicorp/time"
      version = "~> 0.12"
    }
  }
}

provider "azurerm" {
  subscription_id = var.subscription_id
  # Use AAD (not shared keys) for storage data-plane operations, including
  # the provider's post-create data-plane availability wait. This is what
  # lets us keep `shared_access_key_enabled = false` on the tfstate SA.
  storage_use_azuread = true
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

# Grant the operator running this bootstrap (the current az-CLI principal)
# data-plane access on the tfstate resource group. With shared keys disabled
# on the storage account, the azurerm provider's post-create data-plane
# availability wait must authenticate via AAD; this role assignment is what
# makes that path work. Scope is the RG so it's in place before the SA is
# (re)created.
resource "azurerm_role_assignment" "bootstrap_operator_blob_data_owner" {
  scope                = azurerm_resource_group.tfstate.id
  role_definition_name = "Storage Blob Data Owner"
  principal_id         = data.azurerm_client_config.current.object_id
}

# Azure AD role assignments have eventual-consistency propagation delays.
# Wait a beat after granting the data-plane role before the storage module
# tries its data-plane availability poll, otherwise the first attempt
# races propagation and 403s.
resource "time_sleep" "wait_for_blob_rbac_propagation" {
  depends_on      = [azurerm_role_assignment.bootstrap_operator_blob_data_owner]
  create_duration = "60s"
}

module "tfstate_storage" {
  source  = "Azure/avm-res-storage-storageaccount/azurerm"
  version = "0.6.3"

  depends_on = [time_sleep.wait_for_blob_rbac_propagation]

  enable_telemetry              = false
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

  # AVM defaults this to `default_action = "Deny"` with only the
  # `AzureServices` bypass, which blocks the GitHub-hosted CI runners
  # that need to read/write tfstate via OIDC. Allow public access at
  # the network layer; RBAC (Storage Blob Data Contributor on the
  # pipeline managed identity) remains the actual access control.
  # CKV_AZURE_35 is already skipped in `iac/.checkov.yaml` with this
  # exact rationale; private endpoints + service-endpoint allowlists
  # arrive with the VNet slice.
  network_rules = {
    bypass         = ["AzureServices"]
    default_action = "Allow"
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

  enable_telemetry    = false
  name                = "mi-busterminal-pipeline-${each.key}"
  resource_group_name = azurerm_resource_group.tfstate.name
  location            = azurerm_resource_group.tfstate.location
  tags                = merge(local.shared_tags, { environment = each.key })
}

# Spec 003 / US5 / FR-029 — pipeline-MI federated credentials composed via
# the generalized `federated-credential` module so future workload additions
# follow the same pattern (no inline FIC resource blocks at env-level main.tf).
# The `moved` block below preserves Tofu state for the existing per-env
# credentials so the refactor is a zero-destructive-change plan.
module "pipeline_federation" {
  for_each = toset(var.environments)

  source = "../modules/federated-credential"

  name                = "github-environment-${each.key}"
  resource_group_name = azurerm_resource_group.tfstate.name
  parent_id           = module.pipeline_identity[each.key].resource_id
  subject             = "repo:${var.github_org_repo}:environment:${each.key}"
  # `issuer` (GitHub Actions) and `audience` (`api://AzureADTokenExchange`)
  # use the module defaults — same values the inline resource encoded.
}

# `moved` blocks must be enumerated per for_each instance (Tofu does not
# support for_each on moved blocks). Today only the `dev` instance is in
# state; add additional blocks here when `var.environments` is extended.
moved {
  from = azurerm_federated_identity_credential.pipeline_environment["dev"]
  to   = module.pipeline_federation["dev"].azurerm_federated_identity_credential.this
}

resource "azurerm_role_assignment" "pipeline_storage_data" {
  for_each = toset(var.environments)

  principal_id         = module.pipeline_identity[each.key].principal_id
  role_definition_name = "Storage Blob Data Contributor"
  scope                = module.tfstate_storage.resource_id
}

# Subscription-level Contributor so the pipeline can provision environment
# resources via `tofu apply` against `iac/environments/<env>/`. The pipeline
# identity is scoped to a single environment via the federated credential
# subject, providing the per-environment isolation FR-100 requires while
# keeping the role assignment broad enough to actually create resources.
resource "azurerm_role_assignment" "pipeline_subscription_contributor" {
  for_each = toset(var.environments)

  principal_id         = module.pipeline_identity[each.key].principal_id
  role_definition_name = "Contributor"
  scope                = "/subscriptions/${var.subscription_id}"
}

# RBAC Administrator at the subscription scope lets the pipeline grant the
# workload managed identity its own narrowly-scoped roles (AcrPull, Key Vault
# Secrets User) during `tofu apply`. Without this, the workload identity's
# role assignments inside the env composition fail.
resource "azurerm_role_assignment" "pipeline_role_admin" {
  for_each = toset(var.environments)

  principal_id         = module.pipeline_identity[each.key].principal_id
  role_definition_name = "Role Based Access Control Administrator"
  scope                = "/subscriptions/${var.subscription_id}"

  condition_version = "2.0"
  # Scope the RBAC-admin grant: pipeline can only assign roles the workload
  # (and the pipeline itself, where needed) actually use. Prevents privilege
  # escalation per FR-040–FR-052 spirit.
  #
  # Allowed role-definition GUIDs (built-in Azure roles):
  #   7f951dda-4ed3-4680-a7ca-43fe172d538d  AcrPull
  #     → granted to the workload identity so Container Apps can pull images.
  #   4633458b-17de-408a-b874-0445c86b69e6  Key Vault Secrets User
  #     → granted to the workload identity so Container Apps can resolve KV
  #       secret references at runtime.
  #   b86a8fe4-44ce-4948-aee5-eccb2c155cd7  Key Vault Secrets Officer
  #     → granted to the pipeline managed identity itself by the env
  #       composition so `tofu apply` can manage `azurerm_key_vault_secret`
  #       resources via AAD (no shared keys on KV).
  #
  # Spec 005 / FR-033 / research §12 — forward-looking workload role set:
  #   69a216fc-b8fb-44d8-bc22-1f3c2cd27a39  Azure Service Bus Data Sender
  #     → granted to the workload identity so backend services can publish
  #       to Service Bus topics/queues using DefaultAzureCredential.
  #   4f6d3b9b-027b-4f4c-9142-0e5a2a2247e0  Azure Service Bus Data Receiver
  #     → granted to the workload identity so backend services can consume
  #       from Service Bus subscriptions using DefaultAzureCredential.
  #   8ebe5a00-799e-43f5-93ac-243d3dce84a7  Search Index Data Contributor
  #     → granted to the workload identity so backend services can read /
  #       write the AI Search index data plane.
  #   3913510d-42f4-4e42-8a64-420c390055eb  Monitoring Metrics Publisher
  #     → granted to the workload identity so the backend .NET OpenTelemetry
  #       exporter can authenticate to Application Insights ingestion via
  #       AAD (Q1c / research §6).
  #
  # NOT in this list: Cosmos DB Built-in Data Contributor — Cosmos uses its
  # OWN native RBAC surface (`azurerm_cosmosdb_sql_role_assignment`), not
  # Azure RBAC (`azurerm_role_assignment`), so this condition does not govern
  # those grants. The pipeline's subscription-Contributor permits them.
  condition = <<-CONDITION
    (
      !(ActionMatches{'Microsoft.Authorization/roleAssignments/write'})
    )
    OR
    (
      @Request[Microsoft.Authorization/roleAssignments:RoleDefinitionId] ForAnyOfAnyValues:GuidEquals {
        7f951dda-4ed3-4680-a7ca-43fe172d538d,
        4633458b-17de-408a-b874-0445c86b69e6,
        b86a8fe4-44ce-4948-aee5-eccb2c155cd7,
        69a216fc-b8fb-44d8-bc22-1f3c2cd27a39,
        4f6d3b9b-027b-4f4c-9142-0e5a2a2247e0,
        8ebe5a00-799e-43f5-93ac-243d3dce84a7,
        3913510d-42f4-4e42-8a64-420c390055eb
      }
    )
  CONDITION
}
