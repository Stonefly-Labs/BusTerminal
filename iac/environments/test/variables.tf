variable "subscription_id" {
  description = "Azure subscription ID for the test environment."
  type        = string
}

variable "environment_name" {
  description = "Logical environment name. Drives tagging, the App Insights `environment` claim echoed by the backend, and the `ASPNETCORE_ENVIRONMENT` value on the backend container."
  type        = string
  default     = "test"
}

variable "location" {
  description = "Azure region hosting the environment's resources."
  type        = string
  default     = "eastus2"
}

variable "naming_prefix" {
  description = "Short prefix applied to every resource name (e.g., `bt-test` produces `rg-bt-test`, `kv-bt-test-...`)."
  type        = string
  default     = "bt-test"
}

variable "unique_suffix" {
  description = "Globally-unique suffix appended to names that require uniqueness across Azure (Key Vault, ACR). 4-12 lowercase alphanumeric characters."
  type        = string

  validation {
    condition     = can(regex("^[a-z0-9]{4,12}$", var.unique_suffix))
    error_message = "unique_suffix must be 4-12 lowercase alphanumeric characters."
  }
}

variable "github_org_repo" {
  description = "GitHub repository identifier in `<org>/<repo>` form. Used as the federated-credential subject prefix on the workload identity."
  type        = string
}

variable "frontend_image" {
  description = "Fully qualified frontend container image (e.g., `<acr>.azurecr.io/busterminal/web:<sha>`). Supplied by the CD pipeline per deploy."
  type        = string
}

variable "backend_image" {
  description = "Fully qualified backend container image (e.g., `<acr>.azurecr.io/busterminal/api:<sha>`). Supplied by the CD pipeline per deploy."
  type        = string
}

variable "frontend_min_replicas" {
  description = "Minimum replica count for the frontend Container App."
  type        = number
  default     = 0
}

variable "frontend_max_replicas" {
  description = "Maximum replica count for the frontend Container App."
  type        = number
  default     = 3
}

variable "backend_min_replicas" {
  description = "Minimum replica count for the backend Container App."
  type        = number
  default     = 0
}

variable "backend_max_replicas" {
  description = "Maximum replica count for the backend Container App."
  type        = number
  default     = 3
}

variable "entra_tenant_id" {
  description = "Microsoft Entra ID tenant ID enforced for user sign-in and backend JWT validation."
  type        = string
}

variable "entra_api_client_id" {
  description = "Entra ID application (client) ID of the backend API registration. Becomes the JWT audience the backend validates."
  type        = string
}

variable "entra_web_client_id" {
  description = "Entra ID application (client) ID of the frontend (web) SPA registration. Consumed by MSAL (`@azure/msal-browser`) as the `auth.clientId` for Authorization Code + PKCE sign-in."
  type        = string
}

variable "tags" {
  description = "Additional tags merged onto every resource provisioned for this environment."
  type        = map(string)
  default     = {}
}

variable "platform_role_ids" {
  description = <<-EOT
    Stable role_id GUIDs for the four BusTerminal platform app roles declared
    on the API app registration. Entra ID treats `role_id` as the identity of
    the role; once a value is assigned, it must never change (assignments
    reference it). Generate once with `uuidgen` and commit; wire from CI via
    `TF_VAR_platform_role_ids` (JSON object).
  EOT
  type = object({
    admin     = string
    operator  = string
    reader    = string
    developer = string
  })

  validation {
    condition = alltrue([
      for v in [var.platform_role_ids.admin, var.platform_role_ids.operator, var.platform_role_ids.reader, var.platform_role_ids.developer] :
      can(regex("^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$", v))
    ])
    error_message = "Every platform_role_ids.* value must be a UUID."
  }
}

variable "probe_job_enabled" {
  description = <<-EOT
    Toggle for the internal-caller probe Container Apps Job
    (`iac/modules/probe-job-internal-caller`). Off by default — set to true
    only when you want to deploy the re-runnable SC-003 smoke that proves
    the workload MI can authenticate to the BusTerminal API. See
    `docs/internal-workload-callers.md` § Worked example.
  EOT
  type        = bool
  default     = false
}

variable "canonical_db_name" {
  description = "Logical name of the Cosmos SQL database holding the canonical resource store and the change-event log."
  type        = string
  default     = "busterminal-canonical"
}

variable "kv_operator_object_ids" {
  description = <<-EOT
    Entra ID object IDs of humans (or break-glass service principals) who need
    standing `Key Vault Secrets Officer` access on the environment Key Vault.

    Pipeline managed identities do NOT belong here — they receive the role
    automatically via `azurerm_role_assignment.pipeline_kv_secrets_officer`.

    Each ID listed here gets a separate role assignment scoped to the env KV
    so on-call operators can set any future workload secrets via
    `az keyvault secret set` without an out-of-band manual grant.

    Wire from CI via `TF_VAR_kv_operator_object_ids` (JSON-encoded list).
  EOT
  type        = list(string)
  default     = []
}

# -----------------------------------------------------------------------------
# Spec 005 — Infrastructure Baseline
# Per `specs/005-infrastructure-baseline/contracts/config-profile-schema.md`.
# Test defaults: private-by-default data services, Premium SB, Standard Search,
# 90-day KV soft-delete + purge protection ON. Test region mirrors dev
# (`eastus2`) per research §17.
# -----------------------------------------------------------------------------

variable "network_address_space" {
  description = "VNet address space for the env. Test default is 10.51.0.0/16 (dev=10.50.0.0/16, prod=10.52.0.0/16) per research §10."
  type        = list(string)
  default     = ["10.51.0.0/16"]

  validation {
    condition     = length(var.network_address_space) > 0 && alltrue([for c in var.network_address_space : can(cidrnetmask(c))])
    error_message = "network_address_space must contain one or more valid CIDR blocks."
  }
}

variable "subnet_integration_cidr" {
  description = "CIDR for the Container Apps Environment integration subnet. /23 minimum (Azure Container Apps requirement). Must be inside network_address_space."
  type        = string
  default     = "10.51.0.0/23"

  validation {
    condition     = can(cidrnetmask(var.subnet_integration_cidr)) && tonumber(split("/", var.subnet_integration_cidr)[1]) <= 23
    error_message = "subnet_integration_cidr must be a valid CIDR with prefix /23 or larger (Container Apps Environment minimum)."
  }
}

variable "subnet_private_endpoints_cidr" {
  description = "CIDR for the private-endpoints subnet. /24 recommended. Must be inside network_address_space and non-overlapping with subnet_integration_cidr."
  type        = string
  default     = "10.51.2.0/24"

  validation {
    condition     = can(cidrnetmask(var.subnet_private_endpoints_cidr))
    error_message = "subnet_private_endpoints_cidr must be a valid CIDR block."
  }
}

variable "data_services_public_access_enabled" {
  description = "Per-env toggle for public-network access on data services (KV, Cosmos, AI Search, Service Bus). Test defaults to false (private-by-default) per spec 005 §FR-031."
  type        = bool
  default     = false
}

variable "private_endpoints_enabled" {
  description = "When true, the env provisions private endpoints for data services. Test defaults true so workloads continue to reach data services after public access is disabled."
  type        = bool
  default     = true
}

variable "ai_search_sku" {
  description = "Azure AI Search SKU. Test/prod default to standard (S1). Per research §4."
  type        = string
  default     = "standard"

  validation {
    condition     = contains(["free", "basic", "standard", "standard2", "standard3"], var.ai_search_sku)
    error_message = "ai_search_sku must be one of: free, basic, standard, standard2, standard3."
  }
}

variable "service_bus_sku" {
  description = "Service Bus namespace SKU. Test/prod default Premium (required for private endpoints). Basic is rejected at module level."
  type        = string
  default     = "Premium"

  validation {
    condition     = contains(["Standard", "Premium"], var.service_bus_sku)
    error_message = "service_bus_sku must be one of: Standard, Premium. Basic is rejected (no topics/subscriptions support)."
  }
}

variable "service_bus_capacity" {
  description = "Service Bus Premium messaging units. Required (and only used) when service_bus_sku = Premium. One of 1, 2, 4, 8, 16."
  type        = number
  default     = 1

  validation {
    condition     = var.service_bus_capacity == null || contains([1, 2, 4, 8, 16], var.service_bus_capacity)
    error_message = "service_bus_capacity must be null (Standard SKU) or one of: 1, 2, 4, 8, 16 (Premium SKU)."
  }
}

# Wired by US7 / T122 — see specs/005-infrastructure-baseline/tasks.md. Test
# defaults to true (FR-019 hardening); the keyvault module will read it once
# T122 threads the value through `module.keyvault`.
# tflint-ignore: terraform_unused_declarations
variable "key_vault_purge_protection_enabled" {
  description = "Enable Key Vault purge protection. Test/prod default true per FR-019."
  type        = bool
  default     = true
}

# Wired by US7 / T122 — see specs/005-infrastructure-baseline/tasks.md. Test
# defaults to 90 (FR-019 hardening); the keyvault module will read it once
# T122 threads the value through `module.keyvault`.
# tflint-ignore: terraform_unused_declarations
variable "key_vault_soft_delete_retention_days" {
  description = "Key Vault soft-delete retention window in days. Test/prod default 90. Azure range: 7-90."
  type        = number
  default     = 90

  validation {
    condition     = var.key_vault_soft_delete_retention_days >= 7 && var.key_vault_soft_delete_retention_days <= 90
    error_message = "key_vault_soft_delete_retention_days must be between 7 and 90."
  }
}

variable "log_analytics_retention_days" {
  description = "Log Analytics Workspace retention in days. All envs default to 30 per Q5c. Azure range: 30-730 (interactive)."
  type        = number
  default     = 30

  validation {
    condition     = var.log_analytics_retention_days >= 30 && var.log_analytics_retention_days <= 730
    error_message = "log_analytics_retention_days must be between 30 and 730."
  }
}

# Spec 005 / T134 — backend Container App ingress posture (FR-010).
# Test mirrors dev (`true`) so the backend remains reachable from outside the
# CAE during stand-up and parity-of-debugging; flip to `false` via tfvars once
# the test workloads are stable and external ingress is no longer needed.
# Prod defaults to `false`.
variable "backend_external_ingress" {
  description = "Controls the backend Container App's ingress.external_enabled. Test defaults true (mirrors dev posture for parity-of-debugging); operators flip to false via tfvars once stable. Prod template defaults false (FR-010)."
  type        = bool
  default     = true
}
