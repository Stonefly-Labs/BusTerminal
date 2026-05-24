terraform {
  required_version = ">= 1.11.0"

  required_providers {
    azurerm = {
      source = "hashicorp/azurerm"
      # Pin matches the environment composition (`iac/environments/dev/providers.tf`)
      # so we do not silently bump the major in a child module ahead of the
      # composition. Update both together when the time comes.
      version = "~> 4.0"
    }
  }
}
