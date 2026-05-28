# tflint-ignore-file: terraform_unused_required_providers
#
# `random`, `azapi`, and `modtm` are declared because the AVM modules consumed
# below (`avm-res-network-virtualnetwork`, `avm-res-network-privatednszone`)
# require them transitively (research §13). This module does not directly
# reference any `random_*` / `azapi_*` / `modtm_*` resources — the AVMs do.
# The file-level ignore prevents tflint from flagging the intentional pattern.

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
