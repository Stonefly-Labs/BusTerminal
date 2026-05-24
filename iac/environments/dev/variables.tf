variable "subscription_id" {
  description = "Azure subscription ID for the dev environment."
  type        = string
}

variable "environment_name" {
  description = "Logical environment name. Drives tagging, the App Insights `environment` claim echoed by the backend, and the `ASPNETCORE_ENVIRONMENT` value on the backend container."
  type        = string
  default     = "dev"
}

variable "location" {
  description = "Azure region hosting the environment's resources."
  type        = string
  default     = "eastus2"
}

variable "naming_prefix" {
  description = "Short prefix applied to every resource name (e.g., `bt-dev` produces `rg-bt-dev`, `kv-bt-dev-...`)."
  type        = string
  default     = "bt-dev"
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

variable "kv_operator_object_ids" {
  description = <<-EOT
    Entra ID object IDs of humans (or break-glass service principals) who need
    standing `Key Vault Secrets Officer` access on the environment Key Vault.

    Pipeline managed identities do NOT belong here — they receive the role
    automatically via `azurerm_role_assignment.pipeline_kv_secrets_officer`.

    Each ID listed here gets a separate role assignment scoped to the env KV
    so on-call operators can set any future workload secrets via
    `az keyvault secret set` without an out-of-band manual grant. (Spec 003
    removed the original NextAuth/web-client secrets; this access is now
    reserved for future workload secrets only.)

    Wire from CI via `TF_VAR_kv_operator_object_ids` (JSON-encoded list).
  EOT
  type        = list(string)
  default     = []
}
