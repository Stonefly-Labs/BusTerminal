# Spec 006 / Phase 1 T008 — provider pins for the cosmos-registry-store module.
# Aligns with the spec-005 baseline (azurerm ~> 4.0). The module bodies are
# scaffolded empty in Phase 1 and implemented in Phase 2 T013.

terraform {
  required_version = ">= 1.11.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.0"
    }
  }
}
