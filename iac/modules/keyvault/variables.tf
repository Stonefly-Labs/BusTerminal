variable "name" {
  description = "Globally unique Key Vault name. 3-24 alphanumeric/hyphen characters."
  type        = string
}

variable "resource_group_name" {
  description = "Resource group hosting the Key Vault."
  type        = string
}

variable "location" {
  description = "Azure region for the Key Vault."
  type        = string
}

variable "log_analytics_workspace_id" {
  description = "Log Analytics Workspace ID for diagnostic settings. Required — every Key Vault we provision routes diagnostics to the solution LAW per constitutional policy."
  type        = string
}

variable "public_network_access_enabled" {
  description = "When true, the Key Vault accepts traffic from the internet (gated by RBAC). When false, requires private endpoints. Defaults to true for the foundation slice."
  type        = bool
  default     = true
}

# Spec 005 — private-endpoint extension (T054–T056). Defaults preserve existing
# (pre-spec-005) behavior: no PE provisioned. Env composition opts in via Q2c
# per-env toggles.

variable "private_endpoint_enabled" {
  description = <<-EOT
    Plan-time bool toggling the conditional private-endpoint child module.
    Required as a separate variable from `private_endpoint_subnet_id`
    because the subnet ID is sourced from the networking module's output,
    which is "known after apply" — using a nullable string in the `count`
    expression breaks plan with "Invalid count argument: count value
    depends on resource attributes that cannot be determined until apply".
    The env composition passes a literal bool here (`var.private_endpoints_enabled`)
    so plan can statically resolve the count.
  EOT
  type        = bool
  default     = false
}

variable "private_endpoint_subnet_id" {
  description = "Subnet ID for the Key Vault private endpoint. Required when private_endpoint_enabled = true. Bound to the `vault` subresource via the project's private-endpoint module."
  type        = string
  default     = null
}

variable "private_dns_zone_id" {
  description = "Private DNS zone ID for `privatelink.vaultcore.azure.net`. Required when private_endpoint_enabled = true."
  type        = string
  default     = null
}

# Spec 005 / US7 / T122 — per-env purge-protection + soft-delete tuning per
# FR-019. Defaults match the prior hardcoded values so existing deployments
# (which baked these in as `true` / `90`) replan as no-ops. Dev composition
# may override to false / 7 on a NEW vault; **Azure does not permit disabling
# purge protection once enabled**, so flipping `purge_protection_enabled`
# from true to false on an in-state vault will be rejected at apply time.

variable "purge_protection_enabled" {
  description = "Enable Azure Key Vault purge protection. Once enabled, Azure does not allow disabling. Dev composition may set false on a fresh vault; test/prod default true per FR-019."
  type        = bool
  default     = true
}

variable "soft_delete_retention_days" {
  description = "Soft-delete retention window in days. Azure range 7-90. Dev composition may set 7; test/prod default 90 per FR-019."
  type        = number
  default     = 90

  validation {
    condition     = var.soft_delete_retention_days >= 7 && var.soft_delete_retention_days <= 90
    error_message = "soft_delete_retention_days must be between 7 and 90."
  }
}

variable "tags" {
  description = "Tags applied to the Key Vault."
  type        = map(string)
  default     = {}
}
