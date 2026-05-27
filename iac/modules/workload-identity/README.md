# workload-identity

Generalized BusTerminal workload identity: a user-assigned managed identity
plus its downstream grant surface (Azure RBAC + BusTerminal API app-role
assignments). Spec 003, FR-013 / FR-014 / FR-022. Replaces the per-env inline
pattern that the slice-002 `identity` module exposes.

## What you get

- `azurerm_user_assigned_identity` (via the Azure Verified Module
  `Azure/avm-res-managedidentity-userassignedidentity/azurerm`)
- `azurerm_role_assignment` per entry in `assigned_azure_rbac` — downstream
  data-plane RBAC (ACR, Key Vault, future Cosmos / AI Search / Storage /
  Service Bus / OpenAI / App Configuration)
- `azuread_app_role_assignment` per entry in `assigned_api_app_roles` —
  authorizes the MI to call the BusTerminal API with the named role in the
  `roles` claim (no internal-trust bypass; same authorization path as humans)

Federated credentials are NOT in scope for this module — they live in
`../federated-credential/` and are composed alongside this module per
workload.

## Internal addresses preserved from `../identity/`

Resources inside this module are at the same Tofu addresses as the slice-002
`identity` module:

- `module.identity.azurerm_user_assigned_identity.this`
- `azurerm_role_assignment.this["<rbac-nickname>"]`

So callers migrating from `../identity/` only need to:

1. Change `source = "../../modules/identity"` →
   `source = "../../modules/workload-identity"`
2. Rename `role_assignments = { ... }` → `assigned_azure_rbac = { ... }`
3. Add `environment`, `workload` (now required for traceability).
4. Optionally add `api_service_principal_object_id` + `assigned_api_app_roles`
   if the workload needs to call the API.

No `moved` blocks are required for the existing UAMI / RBAC state — only the
new `azuread_app_role_assignment.api_roles["*"]` resources are added.

## Role-assignment split convention (spec 005 / FR-033)

The full FR-033 forward-looking role set for the BusTerminal workload UAMI:

| Nickname                          | Built-in role                     | Scope                          | Where it is emitted                                  |
|-----------------------------------|-----------------------------------|--------------------------------|------------------------------------------------------|
| `acr-pull`                        | `AcrPull`                         | ACR resource                   | **This module**, via `assigned_azure_rbac`           |
| `kv-secrets-user`                 | `Key Vault Secrets User`          | Key Vault resource             | **This module**, via `assigned_azure_rbac`           |
| `monitoring-metrics-publisher`    | `Monitoring Metrics Publisher`    | App Insights resource          | **This module**, via `assigned_azure_rbac`           |
| `search-index-data-contributor`   | `Search Index Data Contributor`   | AI Search service              | `iac/modules/ai-search/` (module owns the grant)     |
| `sb-data-sender`                  | `Azure Service Bus Data Sender`   | Service Bus namespace          | `iac/modules/service-bus/` (module owns the grant)   |
| `sb-data-receiver`                | `Azure Service Bus Data Receiver` | Service Bus namespace          | `iac/modules/service-bus/` (module owns the grant)   |
| `cosmos-data-contributor`         | `Cosmos DB Built-in Data Contributor` (Cosmos-native, NOT Azure RBAC) | Per-database data-plane scope | `azurerm_cosmosdb_sql_role_assignment` at env composition |

> ⚠️ **WARNING — DO NOT DOUBLE-GRANT.** Each data-service role in the lower
> rows above is emitted by its owning module (or, for Cosmos, by the env
> composition). Adding the same role via `assigned_azure_rbac` here would
> produce a **second** `azurerm_role_assignment` resource targeting the same
> `(principal, role_definition, scope)` tuple, and `tofu apply` would fail
> with `RoleAssignmentExists`. Pass ONLY the upper-block (non-data-service)
> nicknames here.

The split exists because data-service modules are self-contained: their
own readers, writers, and reviewers should be able to audit "who has what
on this resource" without traversing back to the env composition.

## Usage

```hcl
data "azuread_service_principal" "api" {
  client_id = var.entra_api_client_id
}

module "workload_identity" {
  source = "../../modules/workload-identity"

  name                = "mi-bt-dev-workload"
  resource_group_name = azurerm_resource_group.this.name
  location            = azurerm_resource_group.this.location
  environment         = "dev"
  workload            = "workload"

  # Non-data-service roles only. See § Role-assignment split convention.
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

# Data-service roles are emitted by the data-service modules — NOT here.
module "ai_search" {
  # ... workload_principal_id = module.workload_identity.principal_id
  # The ai-search module emits the Search Index Data Contributor assignment.
}

module "service_bus" {
  # ... workload_principal_id = module.workload_identity.principal_id
  # The service-bus module emits both SB Sender + Receiver assignments.
}
```

## Validation rules (encoded in `variables.tf`)

- `name` must match `^mi-bt-(dev|test|prod)-[a-z0-9-]+$`
- `kind` must be `UserAssigned` (system-assigned requires an ADR exception)
- `environment` must be one of `dev`, `test`, `prod`
- `workload` must be `[a-z0-9-]+`
- `api_service_principal_object_id` must be a UUID when provided
- Every `assigned_api_app_roles` value must be a UUID role_id
