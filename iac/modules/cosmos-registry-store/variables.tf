# Spec 006 / T013. Variables for the cosmos-registry-store module.

variable "cosmos_account_name" {
  description = "Name of the existing Cosmos DB account (output by the cosmos-account module). Required because `azurerm_cosmosdb_sql_container` references the account by name."
  type        = string
}

variable "resource_group_name" {
  description = "Resource group hosting the Cosmos DB account."
  type        = string
}

variable "cosmos_canonical_database_name" {
  description = "Name of the existing 'canonical' database (output by cosmos-canonical-store)."
  type        = string
  default     = "canonical"
}

variable "entities_container_name" {
  description = "Name of the registry-entities container. Partition key /environment."
  type        = string
  default     = "registry-entities"
}

variable "audit_container_name" {
  description = "Name of the registry-audit container. Partition key /entityId. Append-only writes (FR-034)."
  type        = string
  default     = "registry-audit"
}

variable "leases_container_name" {
  description = "Name of the Cosmos change-feed lease container for the indexer (research §17). Partition key /id, required by the change-feed trigger."
  type        = string
  default     = "registry-entities-leases"
}

variable "entity_default_ttl_seconds" {
  description = "Default TTL on the registry-entities container. `-1` enables per-item TTL so the tombstone-then-delete pattern (research §10) can self-expire markers. Set to `0` (off) to disable per-item TTL — only do this in a future ops spec that replaces the tombstone approach."
  type        = number
  default     = -1
}
