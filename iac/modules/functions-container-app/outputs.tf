# Spec 006 / T015 / contracts/outputs-contract.md.

output "container_app_id" {
  description = "Container App ARM resource id. Consumed by the env composition for diagnostic settings."
  value       = azapi_resource.indexer.id
}

output "container_app_name" {
  description = "Container App resource name. Useful for `az containerapp ...` follow-up commands."
  value       = azapi_resource.indexer.name
}

output "container_app_fqdn" {
  description = "Internal FQDN on the CAE default domain. No public ingress is configured in this slice (the indexer has no inbound HTTP surface), so this resolves to an empty string."
  value       = try(azapi_resource.indexer.output.properties.configuration.ingress.fqdn, "")
}
