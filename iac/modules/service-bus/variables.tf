variable "name" {
  description = "Service Bus namespace name (from naming module). Convention: `sbns-<naming_prefix>-<unique_suffix>`. 6-50 alphanumeric / hyphen; must start with a letter and end with a letter or digit. Globally unique."
  type        = string
}

variable "resource_group_name" {
  description = "Resource group hosting the namespace (env RG)."
  type        = string
}

variable "location" {
  description = "Azure region for the namespace."
  type        = string
}

variable "sku" {
  description = "Namespace SKU. One of: Standard, Premium. Per research §3 dev defaults Standard (~$10/mo), test/prod default Premium (PE-capable, ~$667+/mo). Basic is rejected — no topics/subscriptions support."
  type        = string

  validation {
    condition     = contains(["Standard", "Premium"], var.sku)
    error_message = "sku must be one of: Standard, Premium. Basic is rejected (no topics/subscriptions support)."
  }
}

variable "capacity" {
  description = "Premium messaging units. Required when sku=\"Premium\"; ignored otherwise. One of 1, 2, 4, 8, 16."
  type        = number
  default     = null

  validation {
    condition     = var.capacity == null || contains([1, 2, 4, 8, 16], var.capacity)
    error_message = "capacity must be null (Standard SKU) or one of: 1, 2, 4, 8, 16 (Premium SKU)."
  }
}

variable "public_network_access_enabled" {
  description = "Per-env public-network access toggle (FR-031). Dev defaults true (Q2c warm), test/prod default false."
  type        = bool
}

variable "log_analytics_workspace_id" {
  description = "Log Analytics Workspace ID for diagnostic settings (allLogs only, no metrics — Q5c)."
  type        = string
}

variable "workload_principal_id" {
  description = "Workload UAMI principal (object) ID. Receives `Azure Service Bus Data Sender` AND `Azure Service Bus Data Receiver` scoped to this namespace (FR-033)."
  type        = string
}

variable "private_endpoint_subnet_id" {
  description = "Subnet ID for the namespace private endpoint. Required when private endpoint is desired AND sku=Premium. The env composition is responsible for nulling this for Standard SKU (the module precondition rejects non-null PE inputs with Standard)."
  type        = string
  default     = null
}

variable "private_dns_zone_id" {
  description = "Private DNS zone ID for `privatelink.servicebus.windows.net`. Required when private_endpoint_subnet_id is set."
  type        = string
  default     = null
}

variable "tags" {
  description = "Tags merged onto the namespace and its PE (when provisioned)."
  type        = map(string)
  default     = {}
}

# Spec 009 / T004 — internal `discovery-requested` queue gating + tuning.
# The platform's own Service Bus namespace carries a single internal queue
# used by the discovery worker (BusTerminal.Indexer) to drain
# discovery-run requests from the API. Disabled by default so the module
# stays composable for future tenants that do not need the discovery
# slice; the dev env opt-ins via `enable_discovery_queue = true`.

variable "enable_discovery_queue" {
  description = "Spec 009 — create the internal `discovery-requested` queue on this namespace. The API enqueues discovery requests here and the BusTerminal.Indexer Functions worker drains them. Disabled by default; the env composition opts in."
  type        = bool
  default     = false
}

variable "discovery_queue_name" {
  description = "Spec 009 — name of the internal discovery queue. Bound to `ServiceBusOptions.DiscoveryQueueName` at the app layer."
  type        = string
  default     = "discovery-requested"
}

variable "discovery_queue_lock_duration" {
  description = "Spec 009 / data-model.md §1.3 — ISO-8601 lock duration on the discovery queue. ~5 min matches the SC-005 ceiling so a single discovery message's lock cannot expire while the worker is still draining the namespace."
  type        = string
  default     = "PT5M"
}

variable "discovery_queue_max_delivery_count" {
  description = "Spec 009 / FR-021a — max delivery count before Service Bus dead-letters a discovery message. 3 matches the bounded exponential-backoff retry policy enforced inside the worker (retry inside the message; surface to DLQ only after the worker exhausts its retries)."
  type        = number
  default     = 3
}
