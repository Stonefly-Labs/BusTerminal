output "granted_role_ids" {
  description = <<-EOT
    The list of Microsoft Graph application-permission UUIDs that this module
    requested on the target app registration. Mirrors
    `var.granted_application_permission_ids` and is exposed so downstream
    documentation generators / drift detectors can compare against the
    inventory document without re-reading variables.
  EOT
  value       = var.granted_application_permission_ids
}

output "graph_api_client_id" {
  description = "Microsoft Graph's well-known app id (resolved via the `azuread_application_published_app_ids` data source). Useful for downstream `azuread_app_role_assignment` resources if a future slice needs to assign individual Graph app roles directly."
  value       = data.azuread_application_published_app_ids.well_known.result.MicrosoftGraph
}
