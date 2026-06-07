# Spec 006 / T014 — variable surface.

variable "search_service_name" {
  description = "Name of the AI Search service from spec 005. Used to construct the data-plane hostname (`{name}.search.windows.net`) that azapi_data_plane_resource targets."
  type        = string
}

variable "index_definition_path" {
  description = "Path to the JSON file containing the search index definition. Defaults to the spec-006 contract."
  type        = string
  default     = "../../../specs/006-service-bus-registry-core/contracts/search-index.json"
}
