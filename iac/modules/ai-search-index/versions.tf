# Spec 006 / Phase 1 T009 — provider pins for the ai-search-index module.
# Index resources are not exposed by azurerm v4 nor by AVM, so the module uses
# azapi to apply the index definition from
# specs/006-service-bus-registry-core/contracts/search-index.json (research §5).

terraform {
  required_version = ">= 1.11.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.0"
    }
    azapi = {
      source  = "Azure/azapi"
      version = "~> 2.0"
    }
  }
}
