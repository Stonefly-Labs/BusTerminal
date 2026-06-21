# tflint-ignore-file: terraform_unused_required_providers
#
# Spec 005 — `random`, `azapi`, `modtm` are declared at the env root because
# the AVM modules consumed by `module.networking`, `module.ai_search`, and
# `module.service_bus` require them transitively (research §13). The env root
# does not directly reference any `random_*` / `azapi_*` / `modtm_*` resources
# (`modtm` does carry a `provider "modtm" {}` config block below — that's a
# provider configuration, not a resource reference, and is not what tflint's
# `terraform_unused_required_providers` rule counts as "used"). This is the
# correct way to satisfy the AVMs' provider requirements while keeping the
# env root resource-free; the file-level ignore above prevents tflint from
# flagging the intentional pattern.

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
  # The pipeline identity is Contributor and every resource provider this
  # composition uses is already registered on the subscription. Skip the
  # provider's per-run "list/register all resource providers" pass: it speeds
  # up init and avoids an intermittent ARM flake — "populating Resource
  # Provider cache: listing Resource Providers: unexpected end of JSON input" —
  # that fails plan/apply for reasons unrelated to the diff. (azurerm v4 syntax;
  # replaces the deprecated skip_provider_registration = true.)
  resource_provider_registrations = "none"

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
