# Spec 006 / T014 / contracts/outputs-contract.md.

output "index_name" {
  description = "Name of the registry search index. Bound to AiSearchOptions.IndexName."
  value       = azapi_resource.registry_index.name
}

output "index_id" {
  description = "Full azapi-managed resource id of the index."
  value       = azapi_resource.registry_index.id
}
