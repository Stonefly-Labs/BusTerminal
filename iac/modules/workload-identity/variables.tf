variable "name" {
  description = "Name of the user-assigned managed identity. Convention: `mi-bt-<env>-<workload>`."
  type        = string

  validation {
    condition     = can(regex("^mi-bt-(dev|test|prod)-[a-z0-9-]+$", var.name))
    error_message = "name must match `mi-bt-(dev|test|prod)-<workload>` per data-model.md § Workload Identity."
  }
}

variable "resource_group_name" {
  description = "Resource group holding the identity."
  type        = string
}

variable "location" {
  description = "Azure region for the identity."
  type        = string
}

variable "tags" {
  description = "Tags applied to the managed identity."
  type        = map(string)
  default     = {}
}

variable "kind" {
  description = <<-EOT
    Managed-identity kind. `UserAssigned` is the only value supported by this
    module — system-assigned MIs are deferred per FR-014 ("user-assigned
    managed identities by default; system-assigned permitted only when
    user-assigned is infeasible"). Surfaced as a variable so callers can
    declare intent in HCL and so a future slice can extend the module without
    changing every consumer.
  EOT
  type        = string
  default     = "UserAssigned"

  validation {
    condition     = var.kind == "UserAssigned"
    error_message = "Only 'UserAssigned' is supported. System-assigned MIs require an explicit ADR exception per FR-014."
  }
}

variable "environment" {
  description = "Logical environment label (`dev` / `test` / `prod`). Used for documentation and downstream tagging conventions only — not enforced beyond the `name` regex."
  type        = string

  validation {
    condition     = contains(["dev", "test", "prod"], var.environment)
    error_message = "environment must be one of: dev, test, prod."
  }
}

variable "workload" {
  description = "Short workload label (`api`, `pipeline`, `discovery-job`, `event-fn-x`, …). Echoed in the `name` regex and documentation."
  type        = string

  validation {
    condition     = can(regex("^[a-z0-9-]+$", var.workload))
    error_message = "workload must be lowercase alphanumeric with hyphens."
  }
}

variable "assigned_azure_rbac" {
  description = <<-EOT
    Downstream Azure RBAC assignments for this workload's data-plane access.
    Key is a stable nickname; value is a `(scope, role_definition_name)` tuple.
    Each entry produces one `azurerm_role_assignment`.

    Pass ONLY non-data-service roles via this map. Spec 005 (FR-033)
    forward-looking set for the BusTerminal workload UAMI:

      - `acr-pull`                    AcrPull on the ACR
      - `kv-secrets-user`             Key Vault Secrets User on the KV
      - `monitoring-metrics-publisher` Monitoring Metrics Publisher on the
                                       App Insights resource (enables AAD
                                       ingestion per research §6 / Q1c)

    Data-service roles are intentionally emitted by their owning modules so
    each module remains self-contained (and so consumers don't accidentally
    double-grant). DO NOT include these here:

      - `search-index-data-contributor` (emitted by `iac/modules/ai-search/`)
      - `sb-data-sender` + `sb-data-receiver` (emitted by `iac/modules/service-bus/`)
      - `cosmos-data-contributor` (granted via `azurerm_cosmosdb_sql_role_assignment`
        at the env composition; Cosmos uses native RBAC, not Azure RBAC)

    See README.md § Role-assignment split convention.
  EOT
  type = map(object({
    role_definition_name = string
    scope                = string
  }))
  default = {}
}

variable "api_service_principal_object_id" {
  description = <<-EOT
    Object ID (NOT client/application id) of the BusTerminal API service
    principal that owns the `BusTerminal.*` app roles. Required when
    `assigned_api_app_roles` is non-empty. Typically supplied by the caller
    as `data.azuread_service_principal.api.object_id`.
  EOT
  type        = string
  default     = null

  validation {
    condition     = var.api_service_principal_object_id == null || can(regex("^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$", var.api_service_principal_object_id))
    error_message = "api_service_principal_object_id, when provided, must be a UUID."
  }
}

variable "assigned_api_app_roles" {
  description = <<-EOT
    Map of API app-role nickname (e.g. `reader`, `operator`) → role_id UUID,
    typically wired from `module.app_registration_roles.role_ids.<nickname>`.
    Each entry produces one `azuread_app_role_assignment` granting the
    workload MI permission to call the BusTerminal API with the named role
    in the `roles` claim (FR-022).
  EOT
  type        = map(string)
  default     = {}

  validation {
    condition = alltrue([
      for v in values(var.assigned_api_app_roles) :
      can(regex("^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$", v))
    ])
    error_message = "Every assigned_api_app_roles value must be a UUID role_id."
  }
}
