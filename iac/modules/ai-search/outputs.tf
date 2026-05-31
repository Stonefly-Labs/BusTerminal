output "id" {
  description = "Resource ID of the AI Search service."
  value       = module.search.resource_id
}

output "name" {
  description = "Search service name (echo of var.name)."
  value       = var.name
}

output "endpoint" {
  description = "Public search-service endpoint URL (`https://<name>.search.windows.net`). Bound to `SearchOptions.Endpoint` at the app layer."
  value       = "https://${var.name}.search.windows.net"
}

output "private_endpoint_id" {
  description = "Resource ID of the search PE. Null when no PE is provisioned."
  value       = length(module.private_endpoint) > 0 ? module.private_endpoint[0].id : null
}
