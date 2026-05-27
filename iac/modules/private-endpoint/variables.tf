variable "name" {
  description = "Private endpoint resource name. Convention: `pe-<short-target-name>`."
  type        = string
}

variable "resource_group_name" {
  description = "Resource group hosting the private endpoint (typically the env RG, same RG as the target service)."
  type        = string
}

variable "location" {
  description = "Azure region for the private endpoint."
  type        = string
}

variable "subnet_id" {
  description = "Subnet ID for the private endpoint (typically the env's `snet-private-endpoints`)."
  type        = string
}

variable "target_resource_id" {
  description = "Azure resource ID of the target service (Key Vault, Cosmos account, Search service, Service Bus namespace, ACR, storage account, etc.)."
  type        = string
}

variable "subresource_name" {
  description = "Per-service PE subresource name. One of: vault, Sql, searchService, namespace, registry, blob. See README for the full reference table."
  type        = string

  validation {
    condition     = contains(["vault", "Sql", "searchService", "namespace", "registry", "blob"], var.subresource_name)
    error_message = "subresource_name must be one of: vault, Sql, searchService, namespace, registry, blob."
  }
}

variable "private_dns_zone_id" {
  description = "Private DNS zone ID for A-record registration. Must match the subresource_name (e.g., vault → privatelink.vaultcore.azure.net)."
  type        = string
}

variable "tags" {
  description = "Tags merged onto the private endpoint resource."
  type        = map(string)
  default     = {}
}
