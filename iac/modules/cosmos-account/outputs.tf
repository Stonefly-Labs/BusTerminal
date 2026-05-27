output "account_id" {
  description = "Resource id of the Cosmos DB account. Used by cosmos-canonical-store and by SQL role assignments."
  value       = azurerm_cosmosdb_account.this.id
}

output "account_name" {
  description = "Name of the Cosmos DB account. Used by raw `azurerm_cosmosdb_sql_*` resources downstream."
  value       = azurerm_cosmosdb_account.this.name
}

output "account_endpoint" {
  description = "Public Cosmos DB account endpoint (e.g., https://<name>.documents.azure.com:443/). Bound to CosmosOptions.Endpoint at the app layer."
  value       = azurerm_cosmosdb_account.this.endpoint
}

output "private_endpoint_id" {
  description = "Resource ID of the Cosmos account PE. Null when no PE is provisioned."
  value       = length(module.private_endpoint) > 0 ? module.private_endpoint[0].id : null
}
