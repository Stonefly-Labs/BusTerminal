output "database_name" {
  description = "Logical database name. Bound to CosmosOptions.Database."
  value       = azurerm_cosmosdb_sql_database.canonical.name
}

output "database_id" {
  description = "Resource id of the SQL database. Used as the role-assignment scope for the data-contributor RBAC grant."
  value       = azurerm_cosmosdb_sql_database.canonical.id
}

output "resources_container_name" {
  description = "Container holding canonical resource + relationship documents. Bound to CosmosOptions.Containers.Resources."
  value       = azurerm_cosmosdb_sql_container.resources.name
}

output "change_events_container_name" {
  description = "Append-only change-event log container. Bound to CosmosOptions.Containers.ChangeEvents."
  value       = azurerm_cosmosdb_sql_container.change_events.name
}
