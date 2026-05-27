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

output "private_endpoint_id" {
  description = "Resource ID of the ACR PE. Null when no PE is provisioned."
  value       = length(module.private_endpoint) > 0 ? module.private_endpoint[0].id : null
}
