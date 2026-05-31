output "id" {
  description = "Resource ID of the Key Vault."
  value       = module.keyvault.resource_id
}

output "name" {
  description = "Key Vault name."
  value       = var.name
}

output "uri" {
  description = "Key Vault DNS endpoint (vault URI) — set this as `AZURE_KEY_VAULT_URI` on workload containers."
  value       = module.keyvault.uri
}

output "private_endpoint_id" {
  description = "Resource ID of the Key Vault PE. Null when no PE is provisioned."
  value       = length(module.private_endpoint) > 0 ? module.private_endpoint[0].id : null
}
