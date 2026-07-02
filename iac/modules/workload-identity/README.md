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

<!-- BEGIN_TF_DOCS -->
## Resources

| Name | Type |
| ---- | ---- |
| [azuread_app_role_assignment.api_roles](https://registry.terraform.io/providers/hashicorp/azuread/latest/docs/resources/app_role_assignment) | resource |
| [azuread_app_role_assignment.graph_roles](https://registry.terraform.io/providers/hashicorp/azuread/latest/docs/resources/app_role_assignment) | resource |
| [azurerm_role_assignment.this](https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs/resources/role_assignment) | resource |

## Inputs

| Name | Description | Type | Default | Required |
| ---- | ----------- | ---- | ------- | :------: |
| <a name="input_environment"></a> [environment](#input\_environment) | Logical environment label (`dev` / `test` / `prod`). Used for documentation and downstream tagging conventions only — not enforced beyond the `name` regex. | `string` | n/a | yes |
| <a name="input_location"></a> [location](#input\_location) | Azure region for the identity. | `string` | n/a | yes |
| <a name="input_name"></a> [name](#input\_name) | Name of the user-assigned managed identity. Convention: `mi-bt-<env>-<workload>`. | `string` | n/a | yes |
| <a name="input_resource_group_name"></a> [resource\_group\_name](#input\_resource\_group\_name) | Resource group holding the identity. | `string` | n/a | yes |
| <a name="input_workload"></a> [workload](#input\_workload) | Short workload label (`api`, `pipeline`, `discovery-job`, `event-fn-x`, …). Echoed in the `name` regex and documentation. | `string` | n/a | yes |
| <a name="input_api_service_principal_object_id"></a> [api\_service\_principal\_object\_id](#input\_api\_service\_principal\_object\_id) | Object ID (NOT client/application id) of the BusTerminal API service<br/>principal that owns the `BusTerminal.*` app roles. Required when<br/>`assigned_api_app_roles` is non-empty. Typically supplied by the caller<br/>as `data.azuread_service_principal.api.object_id`. | `string` | `null` | no |
| <a name="input_assigned_api_app_roles"></a> [assigned\_api\_app\_roles](#input\_assigned\_api\_app\_roles) | Map of API app-role nickname (e.g. `reader`, `operator`) → role\_id UUID,<br/>typically wired from `module.app_registration_roles.role_ids.<nickname>`.<br/>Each entry produces one `azuread_app_role_assignment` granting the<br/>workload MI permission to call the BusTerminal API with the named role<br/>in the `roles` claim (FR-022). | `map(string)` | `{}` | no |
| <a name="input_assigned_azure_rbac"></a> [assigned\_azure\_rbac](#input\_assigned\_azure\_rbac) | Downstream Azure RBAC assignments for this workload's data-plane access.<br/>Key is a stable nickname; value is a `(scope, role_definition_name)` tuple.<br/>Each entry produces one `azurerm_role_assignment`.<br/><br/>Pass ONLY non-data-service roles via this map. Spec 005 (FR-033)<br/>forward-looking set for the BusTerminal workload UAMI:<br/><br/>  - `acr-pull`                    AcrPull on the ACR<br/>  - `kv-secrets-user`             Key Vault Secrets User on the KV<br/>  - `monitoring-metrics-publisher` Monitoring Metrics Publisher on the<br/>                                   App Insights resource (enables AAD<br/>                                   ingestion per research §6 / Q1c)<br/><br/>Data-service roles are intentionally emitted by their owning modules so<br/>each module remains self-contained (and so consumers don't accidentally<br/>double-grant). DO NOT include these here:<br/><br/>  - `search-index-data-contributor` (emitted by `iac/modules/ai-search/`)<br/>  - `sb-data-sender` + `sb-data-receiver` (emitted by `iac/modules/service-bus/`)<br/>  - `cosmos-data-contributor` (granted via `azurerm_cosmosdb_sql_role_assignment`<br/>    at the env composition; Cosmos uses native RBAC, not Azure RBAC)<br/><br/>See README.md § Role-assignment split convention. | <pre>map(object({<br/>    role_definition_name = string<br/>    scope                = string<br/>  }))</pre> | `{}` | no |
| <a name="input_assigned_graph_app_roles"></a> [assigned\_graph\_app\_roles](#input\_assigned\_graph\_app\_roles) | Map of Microsoft Graph app-role nickname (e.g. `user-read-all`) → app role<br/>id UUID. Each entry produces one `azuread_app_role_assignment` granting the<br/>workload MI app-only Graph access; the role surfaces in the `roles` claim<br/>of the MI's Graph token. Requires `graph_service_principal_object_id`.<br/><br/>Role ids come from the Graph SP's `appRoles` (stable across tenants), e.g.<br/>`User.Read.All` = df021288-bdef-4463-88db-98f22de89214,<br/>`Group.Read.All` = 5b567255-7703-4780-807c-7be8301ae99b. Keep this set in<br/>lockstep with the inventory at<br/>`specs/003-auth-and-identity/contracts/graph-permissions-inventory.md`. | `map(string)` | `{}` | no |
| <a name="input_graph_service_principal_object_id"></a> [graph\_service\_principal\_object\_id](#input\_graph\_service\_principal\_object\_id) | Object ID (NOT app/client id) of the Microsoft Graph service principal in<br/>this tenant — the `resource_object_id` for the workload MI's app-only Graph<br/>app-role assignments. Required when `assigned_graph_app_roles` is non-empty.<br/>Resolve it from the well-known Graph app id rather than hardcoding the<br/>tenant-specific object id, e.g.<br/><br/>  data "azuread\_service\_principal" "msgraph" {<br/>    client\_id = "00000003-0000-0000-c000-000000000000"<br/>  }<br/><br/>Why this lives on the MI and not the API app registration: app-only Graph<br/>permissions are effective for the principal that actually authenticates.<br/>The BusTerminal API runs as this user-assigned MANAGED IDENTITY, so its SP<br/>must hold the Graph app roles directly. Admin-consent on the API app<br/>registration (see `iac/modules/graph-permissions`) only authorizes that<br/>app registration's SP under client-credentials — a DIFFERENT principal —<br/>so it does nothing for the MI. Without the assignments below, Graph returns<br/>403 (spec 008 owner-picker regression, 2026-06-24). | `string` | `null` | no |
| <a name="input_kind"></a> [kind](#input\_kind) | Managed-identity kind. `UserAssigned` is the only value supported by this<br/>module — system-assigned MIs are deferred per FR-014 ("user-assigned<br/>managed identities by default; system-assigned permitted only when<br/>user-assigned is infeasible"). Surfaced as a variable so callers can<br/>declare intent in HCL and so a future slice can extend the module without<br/>changing every consumer. | `string` | `"UserAssigned"` | no |
| <a name="input_tags"></a> [tags](#input\_tags) | Tags applied to the managed identity. | `map(string)` | `{}` | no |

## Outputs

| Name | Description |
| ---- | ----------- |
| <a name="output_assigned_api_app_role_ids"></a> [assigned\_api\_app\_role\_ids](#output\_assigned\_api\_app\_role\_ids) | Map of API app role nickname → role\_id assigned to this workload MI. Empty when the workload does not call the API. |
| <a name="output_assigned_graph_app_role_ids"></a> [assigned\_graph\_app\_role\_ids](#output\_assigned\_graph\_app\_role\_ids) | Map of Microsoft Graph app-role nickname → role\_id assigned directly to this workload MI's service principal. Empty when the workload makes no app-only Graph calls. |
| <a name="output_client_id"></a> [client\_id](#output\_client\_id) | Client ID (applicationId) — set on the workload's `AZURE_CLIENT_ID` env var so `DefaultAzureCredential` picks the right MI when multiple are attached. |
| <a name="output_id"></a> [id](#output\_id) | Resource ID of the managed identity. |
| <a name="output_name"></a> [name](#output\_name) | Identity name (echoed for downstream references). |
| <a name="output_principal_id"></a> [principal\_id](#output\_principal\_id) | Object ID (principal ID) for RBAC / app-role assignments and for `Platform Principal.ObjectId` on workload-issued tokens. |
<!-- END_TF_DOCS -->

