output "id" {
  description = "Resource ID of the Service Bus namespace."
  value       = module.namespace.resource_id
}

output "name" {
  description = "Namespace name (echo of var.name)."
  value       = var.name
}

output "fqdn" {
  description = "Namespace FQDN (`<name>.servicebus.windows.net`). Bound to `ServiceBusOptions.FullyQualifiedNamespace` at the app layer."
  value       = "${var.name}.servicebus.windows.net"
}

output "private_endpoint_id" {
  description = "Resource ID of the namespace PE. Null when no PE is provisioned (Standard SKU or PE inputs nulled)."
  value       = length(module.private_endpoint) > 0 ? module.private_endpoint[0].id : null
}
