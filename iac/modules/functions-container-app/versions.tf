# Spec 006 / Phase 1 T010 — provider pins for the functions-container-app module.
# Uses azurerm v4 which exposes the kind="functionapp" path on
# azurerm_container_app for the v2 native Functions-on-CAE hosting model
# (research §4). Falls back to azapi if the pinned azurerm version turns out
# to lack the `kind` argument (decision deferred to T015 implementation).

terraform {
  required_version = ">= 1.11.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.0"
    }
  }
}
