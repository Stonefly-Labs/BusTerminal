output "workbook_id" {
  description = "Resource ID of the discovery workbook."
  value       = azurerm_application_insights_workbook.discovery.id
}

output "workbook_name" {
  description = "GUID-shaped name of the workbook (consumes as workbook id in the Azure portal URL)."
  value       = azurerm_application_insights_workbook.discovery.name
}
