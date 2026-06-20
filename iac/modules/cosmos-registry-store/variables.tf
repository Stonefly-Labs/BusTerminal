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

# Spec 009 / T005 — new containers for discovery runs and the per-namespace
# discovery lock. PK `/namespaceId` on both; append-only writes on
# `discovery-runs` after terminal status; single-doc-per-namespace on
# `discovery-locks` (id is the literal string "lock"). Indexing policy lives
# inline in `main.tf` per data-model.md §5.

variable "discovery_runs_container_name" {
  description = "Name of the discovery-runs container (spec 009). Partition key /namespaceId. Append-only history of discovery runs; indefinite retention in v1."
  type        = string
  default     = "discovery-runs"
}

variable "discovery_locks_container_name" {
  description = "Name of the discovery-locks container (spec 009). Partition key /namespaceId. One document per registered namespace (id='lock'); used for FR-003 coalescing via Cosmos ETag-based atomic acquisition."
  type        = string
  default     = "discovery-locks"
}

# Spec 008 / T007 — new container for namespace validation run history.
# Partition key `/namespaceId`; append-only writes; indefinite retention in
# v1 (no TTL) per `specs/008-namespace-onboarding/contracts/outputs-contract.md
# §1.3` and `research.md §6`. Throughput is account-controlled (the canonical
# Cosmos account is serverless — see comment block in main.tf — so the
# contract's `autoscale_min_ru = 1000 / max_ru = 4000` is informational only;
# serverless RU is billed per-request).
variable "validation_runs_container_name" {
  description = "Name of the namespace-validation-runs container (spec 008). Partition key /namespaceId. Append-only writes; indefinite retention in v1."
  type        = string
  default     = "namespace-validation-runs"
}
