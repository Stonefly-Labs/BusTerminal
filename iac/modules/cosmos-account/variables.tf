variable "name" {
  description = "Cosmos DB account name. Must be globally unique, 3-44 lowercase alphanumeric / hyphen chars."
  type        = string

  validation {
    condition     = can(regex("^[a-z0-9-]{3,44}$", var.name))
    error_message = "Cosmos DB account name must be 3-44 lowercase alphanumeric or hyphen characters."
  }
}

variable "resource_group_name" {
  description = "Resource group hosting the Cosmos DB account."
  type        = string
}

variable "location" {
  description = "Azure region for the Cosmos DB account."
  type        = string
}

variable "tags" {
  description = "Tags applied to the Cosmos DB account."
  type        = map(string)
  default     = {}
}

variable "log_analytics_workspace_id" {
  description = <<-EOT
    Optional Log Analytics Workspace resource id for diagnostic settings. When
    set, a diagnostic-setting routes allLogs + AllMetrics to the workspace
    (Constitution §Operational Excellence — every Azure resource routes
    diagnostic logs + AllMetrics to the LAW). Pass null to skip.
  EOT
  type        = string
  default     = null
}

# Spec 005 — private-endpoint extension (T051–T053). Defaults preserve existing
# (pre-spec-005) behavior: no PE provisioned, public access on. Env composition
# opts in via Q2c per-env toggles.

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
  description = "Subnet ID for the Cosmos account private endpoint. Required when private_endpoint_enabled = true. Bound to the `Sql` subresource via the project's private-endpoint module."
  type        = string
  default     = null
}

variable "private_dns_zone_id" {
  description = "Private DNS zone ID for `privatelink.documents.azure.com`. Required when private_endpoint_enabled = true."
  type        = string
  default     = null
}

variable "public_network_access_enabled" {
  description = "When true, the Cosmos account accepts traffic from the internet (gated by AAD/RBAC). When false, requires private endpoints. Defaults to true to preserve pre-spec-005 behavior."
  type        = bool
  default     = true
}

variable "ip_range_filter" {
  description = <<-EOT
    Cosmos `ipRules` set. When a private endpoint is configured AND
    `public_network_access_enabled = true`, Cosmos enters a default
    "restricted public" mode where public traffic is dropped unless
    explicitly allowed via this set. The special magic value `0.0.0.0`
    permits traffic originating from any Azure datacenter — used in
    dev so the Container Apps Environment's egress NAT (a public
    Azure-allocated IP) can reach Cosmos for the indexer's change-feed
    listener. Empty set keeps the default-restrictive posture (PE-only
    + named IP allowlist). Bare IP literals or CIDRs are also accepted.
  EOT
  type        = set(string)
  default     = []
}
