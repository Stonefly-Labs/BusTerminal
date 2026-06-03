# Spec 006 / Phase 1 T009 — variable surface (scaffold). Full schema lands in
# Phase 2 T014.

variable "ai_search_id" {
  description = "Resource id of the AI Search service from spec 005."
  type        = string
}

variable "index_definition_path" {
  description = "Path to the JSON file containing the search index definition. Defaults to the spec-006 contract."
  type        = string
  default     = "../../../specs/006-service-bus-registry-core/contracts/search-index.json"
}
