output "id" {
  description = "Resource ID of the Container App."
  value       = module.app.resource_id
}

output "name" {
  description = "Container App name."
  value       = var.name
}

output "fqdn_url" {
  description = "HTTPS URL of the workload's ingress (empty string when ingress is disabled)."
  value       = module.app.fqdn_url
}

output "ingress_external" {
  description = "Whether the workload accepts external traffic. Echoed for downstream policy assertions."
  value       = var.ingress_external
}
