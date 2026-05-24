output "id" {
  description = "Resource ID of the Container Apps Job."
  value       = azurerm_container_app_job.this.id
}

output "name" {
  description = "Job name (echoed for operator commands like `az containerapp job start`)."
  value       = azurerm_container_app_job.this.name
}
