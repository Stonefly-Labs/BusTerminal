output "id" {
  description = "Resource ID of the Container Apps Environment."
  value       = module.environment.resource_id
}

output "name" {
  description = "Environment name."
  value       = var.name
}

output "default_domain" {
  description = "Default DNS suffix assigned by Azure — workloads land at `<app-name>.<default_domain>`."
  value       = module.environment.default_domain
}
