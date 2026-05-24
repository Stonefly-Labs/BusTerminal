terraform {
  required_version = ">= 1.11.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.0"
    }
    azuread = {
      source  = "hashicorp/azuread"
      version = "~> 3.1"
    }
  }
}

# Generalized BusTerminal workload identity (spec 003, FR-013 / FR-014 / FR-022).
#
# Produces a single user-assigned managed identity plus its downstream grant
# surface:
#
#   - `assigned_azure_rbac` — `azurerm_role_assignment` per entry. Used for
#     data-plane access on downstream Azure resources (ACR, Key Vault, future
#     Cosmos / AI Search / Storage / Service Bus / OpenAI / App Configuration).
#
#   - `assigned_api_app_roles` — `azuread_app_role_assignment` per entry.
#     Authorizes the workload MI to call the BusTerminal API with the named
#     `BusTerminal.*` app role on the API service principal. The MI's
#     access token carries the role in the `roles` claim, which the backend's
#     `RolePolicies` consume identically to human tokens — there is no
#     internal-trust bypass (FR-012).
#
# Internal resource addresses are kept identical to `iac/modules/identity/`
# (`module.identity.azurerm_user_assigned_identity.this` + the
# `azurerm_role_assignment.this[*]` keys) so callers can migrate from the old
# module by changing only the `source` and renaming the role-assignments
# variable — no `moved` blocks required for the existing UAMI / RBAC state.

# Workload + environment labels are baked into the MI's tags so operators
# can filter in the portal by `workload=` (which is otherwise unrecoverable
# from the resource name alone — there is no convention that maps name suffix
# back to a structured workload label). `environment` is typically already
# present in `var.tags` from the caller's `shared_tags`; the merge is a
# no-op for that key when supplied. Caller-provided tags win on conflict.
locals {
  module_tags = merge(
    {
      workload    = var.workload
      environment = var.environment
      mi-kind     = var.kind
    },
    var.tags,
  )
}

module "identity" {
  source  = "Azure/avm-res-managedidentity-userassignedidentity/azurerm"
  version = "0.3.3"

  name                = var.name
  resource_group_name = var.resource_group_name
  location            = var.location
  tags                = local.module_tags
}

resource "azurerm_role_assignment" "this" {
  for_each = var.assigned_azure_rbac

  principal_id         = module.identity.principal_id
  role_definition_name = each.value.role_definition_name
  scope                = each.value.scope
}

# App-role assignments against the BusTerminal API service principal. Each
# entry's value is the `role_id` UUID emitted by the `app-registration-roles`
# module (`module.app_registration_roles.role_ids.<nickname>`). Assignment
# requires the API SP's *object id*, not its client id — pass the
# `data "azuread_service_principal" "api".object_id` from the caller.
resource "azuread_app_role_assignment" "api_roles" {
  for_each = var.assigned_api_app_roles

  app_role_id         = each.value
  principal_object_id = module.identity.principal_id
  resource_object_id  = var.api_service_principal_object_id
}
