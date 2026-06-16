# Spec 006 / T013 / contracts/outputs-contract.md. Per-container ids + names
# for downstream composition (RBAC scopes, env-var assembly).

output "entities_container_name" {
  description = "Name of the registry-entities container. Consumed by CosmosRegistryOptions.EntitiesContainer."
  value       = azurerm_cosmosdb_sql_container.registry_entities.name
}

output "entities_container_id" {
  description = "ARM resource id of the registry-entities container."
  value       = azurerm_cosmosdb_sql_container.registry_entities.id
}

output "audit_container_name" {
  description = "Name of the registry-audit container. Consumed by CosmosRegistryOptions.AuditContainer."
  value       = azurerm_cosmosdb_sql_container.registry_audit.name
}

output "audit_container_id" {
  description = "ARM resource id of the registry-audit container."
  value       = azurerm_cosmosdb_sql_container.registry_audit.id
}

output "leases_container_name" {
  description = "Name of the change-feed lease container."
  value       = azurerm_cosmosdb_sql_container.registry_entities_leases.name
}

output "leases_container_id" {
  description = "ARM resource id of the leases container."
  value       = azurerm_cosmosdb_sql_container.registry_entities_leases.id
}

# Spec 008 / T007.
output "validation_runs_container_name" {
  description = "Name of the namespace-validation-runs container. Consumed by CosmosRegistryOptions.ValidationRunsContainer (spec 008)."
  value       = azurerm_cosmosdb_sql_container.namespace_validation_runs.name
}

output "validation_runs_container_id" {
  description = "ARM resource id of the namespace-validation-runs container."
  value       = azurerm_cosmosdb_sql_container.namespace_validation_runs.id
}
