variable "cosmos_account_name" {
  description = "Name of the Cosmos DB account (output by the cosmos-account module). Required because `azurerm_cosmosdb_sql_database` references the account by name, not by id."
  type        = string
}

variable "resource_group_name" {
  description = "Resource group hosting the Cosmos DB account. Required by `azurerm_cosmosdb_sql_database`."
  type        = string
}

variable "database_name" {
  description = "Logical database name for the canonical store."
  type        = string
  default     = "busterminal-canonical"
}

variable "resources_container_name" {
  description = "Name of the container holding canonical resource documents + the relationship peer documents (per data-model.md §3, relationships live in the same container)."
  type        = string
  default     = "resources"
}

variable "change_events_container_name" {
  description = "Name of the append-only change-event log container (FR-015 / Q5)."
  type        = string
  default     = "change-events"
}
