# Spec 006 / Phase 1 T008 — variable surface for the cosmos-registry-store module.
# Variable bodies are skeletons; full schema lands in Phase 2 T013 per
# specs/006-service-bus-registry-core/contracts/outputs-contract.md.

variable "cosmos_account_id" {
  description = "Resource id of the existing Cosmos DB account from spec 005."
  type        = string
}

variable "cosmos_canonical_database_name" {
  description = "Name of the existing 'canonical' database from spec 004."
  type        = string
  default     = "canonical"
}
