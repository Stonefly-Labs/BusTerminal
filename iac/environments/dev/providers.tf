# tflint-ignore-file: terraform_unused_required_providers
#
# Spec 005 Phase 2 (T028) declares three new provider requirements (`random`,
# `azapi`, `modtm`) ahead of the Phase 3 module wiring (T029–T068) that
# consumes them. `random` and `azapi` will be unused until Phase 3 lands; the
# file-level tflint-ignore-file directive above suppresses the
# terraform_unused_required_providers warning for the whole file. This
# directive MUST be removed when Phase 3 wiring lands so future legitimately-
# unused provider declarations are still caught.

terraform {
  required_version = ">= 1.11.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.0"
    }
    azuread = {
      source  = "hashicorp/azuread"
      version = "~> 3.1"
    }
    time = {
      source  = "hashicorp/time"
      version = "~> 0.12"
    }
    # Spec 005 — required by the AVM modules consumed by spec 005's networking,
    # ai-search, and service-bus modules. See research §13.
    random = {
      source  = "hashicorp/random"
      version = "~> 3.6"
    }
    azapi = {
      source  = "azure/azapi"
      version = "~> 2.4"
    }
    modtm = {
      source  = "azure/modtm"
      version = "~> 0.3"
    }
  }
}

provider "azurerm" {
  subscription_id = var.subscription_id

  features {
    resource_group {
      prevent_deletion_if_contains_resources = true
    }

    key_vault {
      purge_soft_delete_on_destroy          = false
      purge_soft_deleted_secrets_on_destroy = false
      recover_soft_deleted_key_vaults       = true
      recover_soft_deleted_secrets          = true
    }
  }
}

provider "azuread" {}

# AVM telemetry — disabled per research §13. The modtm provider attribute is `enabled` (not
# `enable_telemetry`, which is the per-AVM-module input). Each AVM module invocation MUST also
# pass `enable_telemetry = false` at module-call time; this provider-level flag is belt-and-
# suspenders so even if a module call forgets, no telemetry is emitted.
provider "modtm" {
  enabled = false
}
