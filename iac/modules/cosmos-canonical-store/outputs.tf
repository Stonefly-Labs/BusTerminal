output "database_name" {
  description = "Logical database name. Bound to CosmosOptions.Database."
  value       = azurerm_cosmosdb_sql_database.canonical.name
}

output "database_id" {
  description = "ARM resource id of the SQL database. Useful for ARM-shaped references (e.g., management-plane role assignments). NOT the right shape for Cosmos data-plane RBAC scope on `azurerm_cosmosdb_sql_role_assignment.scope` — that resource expects the data-plane path form (`.../databaseAccounts/<acct>/dbs/<db>`), not the ARM form (`.../sqlDatabases/<db>`). Construct that scope at the call site from the account id + `database_name`."
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
