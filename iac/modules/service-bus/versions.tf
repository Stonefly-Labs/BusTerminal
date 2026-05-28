# tflint-ignore-file: terraform_unused_required_providers
#
# `random`, `azapi`, and `modtm` are declared because the
# `avm-res-servicebus-namespace` AVM consumed by this module requires them
# transitively (research §13). This module does not directly reference any
# `random_*` / `azapi_*` / `modtm_*` resources — the AVM does. The
# file-level ignore prevents tflint from flagging the intentional pattern.

terraform {
  required_version = ">= 1.11.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.0"
    }
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
