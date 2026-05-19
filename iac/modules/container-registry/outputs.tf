output "id" {
  description = "Resource ID of the registry."
  value       = module.registry.resource_id
}

output "name" {
  description = "Registry name."
  value       = var.name
}

output "login_server" {
  description = "Login server hostname — used as the image prefix (e.g., `<login_server>/busterminal/api:<sha>`)."
  value       = module.registry.resource.login_server
}
