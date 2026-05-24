output "role_ids" {
  description = "Map of role nickname → role_id (UUID) for downstream `azuread_app_role_assignment` consumers."
  value       = { for k, r in azuread_application_app_role.this : k => r.role_id }
}

output "role_values" {
  description = "Map of role nickname → on-wire role claim value (e.g. `BusTerminal.Admin`)."
  value       = { for k, r in azuread_application_app_role.this : k => r.value }
}
