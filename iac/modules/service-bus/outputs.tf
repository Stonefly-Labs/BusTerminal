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

# Spec 009 / T004 — discovery queue surface so the env composition can
# wire its name + FQDN into the API and Indexer container apps' env vars
# (`ServiceBus__fullyQualifiedNamespace` is the namespace FQDN exposed
# above; this output exposes just the queue name).
output "discovery_queue_name" {
  description = "Name of the internal `discovery-requested` queue when provisioned. Null when `enable_discovery_queue = false`."
  value       = length(azurerm_servicebus_queue.discovery_requested) > 0 ? azurerm_servicebus_queue.discovery_requested[0].name : null
}

output "discovery_queue_id" {
  description = "Resource ID of the internal `discovery-requested` queue when provisioned. Null when `enable_discovery_queue = false`."
  value       = length(azurerm_servicebus_queue.discovery_requested) > 0 ? azurerm_servicebus_queue.discovery_requested[0].id : null
}
