# Spec 006 / Phase 1 T010 (revised) — provider pins for the
# functions-container-app module. The v2 native Azure Functions hosting model
# requires the `kind = "functionapp"` envelope field on
# Microsoft.App/containerApps, which azurerm v4 does NOT expose (its Function
# App resources target App Service plans). The module therefore provisions the
# indexer via azapi. `azurerm` is intentionally NOT declared here — this module
# has no azurerm resources, and tflint flags unused provider declarations
# (same pattern as iac/modules/ai-search-index).

terraform {
  required_version = ">= 1.11.0"

  required_providers {
    azapi = {
      source  = "Azure/azapi"
      version = "~> 2.0"
    }
  }
}
