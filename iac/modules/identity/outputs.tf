output "id" {
  description = "Resource ID of the managed identity."
  value       = module.identity.resource_id
}

output "principal_id" {
  description = "Object ID (principal ID) for RBAC assignments."
  value       = module.identity.principal_id
}

output "client_id" {
  description = "Client ID (applicationId) — set this on workload pods/containers as the federated/managed-identity client id."
  value       = module.identity.client_id
}

output "name" {
  description = "Identity name (echoed for downstream references)."
  value       = var.name
}
