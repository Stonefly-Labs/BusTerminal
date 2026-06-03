# Spec 006 / T014 — provider pins for the ai-search-index module. Index
# resources are not exposed by azurerm v4 nor by AVM, so the module uses
# azapi to apply the index definition from
# specs/006-service-bus-registry-core/contracts/search-index.json (research §5).
# `azurerm` is intentionally NOT declared here — this module has no azurerm
# resources, and tflint flags unused provider declarations.

terraform {
  required_version = ">= 1.11.0"

  required_providers {
    azapi = {
      source  = "Azure/azapi"
      version = "~> 2.0"
    }
  }
}
