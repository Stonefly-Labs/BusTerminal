output "id" {
  description = "Resource ID of the managed identity."
  value       = module.identity.resource_id
}

output "principal_id" {
  description = "Object ID (principal ID) for RBAC / app-role assignments and for `Platform Principal.ObjectId` on workload-issued tokens."
  value       = module.identity.principal_id
}

output "client_id" {
  description = "Client ID (applicationId) — set on the workload's `AZURE_CLIENT_ID` env var so `DefaultAzureCredential` picks the right MI when multiple are attached."
  value       = module.identity.client_id
}

output "name" {
  description = "Identity name (echoed for downstream references)."
  value       = var.name
}

output "assigned_api_app_role_ids" {
  description = "Map of API app role nickname → role_id assigned to this workload MI. Empty when the workload does not call the API."
  value       = { for k, r in azuread_app_role_assignment.api_roles : k => r.app_role_id }
}
