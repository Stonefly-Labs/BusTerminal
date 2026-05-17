output "azure_tenant_id" {
  description = "Azure AD tenant ID for the subscription (set as `AZURE_TENANT_ID` repository variable in GitHub)."
  value       = data.azurerm_client_config.current.tenant_id
}

output "azure_subscription_id" {
  description = "Azure subscription ID (set as `AZURE_SUBSCRIPTION_ID` repository variable in GitHub)."
  value       = var.subscription_id
}

output "azure_client_ids" {
  description = "Per-environment pipeline managed identity client IDs (set as `AZURE_CLIENT_ID` per GitHub deployment environment)."
  value       = { for env, mi in module.pipeline_identity : env => mi.client_id }
}

output "tfstate_resource_group" {
  description = "Resource group hosting the OpenTofu state storage account."
  value       = azurerm_resource_group.tfstate.name
}

output "tfstate_storage_account_name" {
  description = "Storage account name hosting OpenTofu state."
  value       = var.tfstate_storage_account_name
}

output "tfstate_container_name" {
  description = "Blob container name holding state files."
  value       = local.tfstate_container_name
}

data "azurerm_client_config" "current" {}
