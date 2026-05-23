terraform {
  required_version = ">= 1.11.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.0"
    }
  }
}

# Generalized BusTerminal federated identity credential (spec 003, FR-029 /
# FR-030, data-model.md § Federated Credential).
#
# Produces a single `azurerm_federated_identity_credential` on the parent
# user-assigned managed identity supplied by the caller. The credential
# defines the trust relationship that lets an external IdP token (GitHub
# Actions OIDC by default; any OIDC issuer by argument) be exchanged for an
# Azure access token issued for the parent MI.
#
# **Why this resource and not `azuread_application_federated_identity_credential`**
# — every BusTerminal-issued federation grant target an MI (pipeline MI in
# `iac/platform-bootstrap/`, workload MI in `iac/environments/<env>/`). The
# entity catalog in `specs/003-auth-and-identity/data-model.md § Federated
# Credential` defines `ParentIdentity` as a reference to a Workload Identity
# (MI), which matches `azurerm_federated_identity_credential`'s parent shape.
# If a future slice adds an Entra-app-parented federation grant we can add a
# sibling `federated-credential-app` module without touching this one.
#
# **Subject hygiene** — wildcard subjects (`*` anywhere) widen the trust
# surface to any branch/env that matches the pattern, and so are rejected via
# `variables.tf` validation. ADR-recorded exceptions can subclass this module
# or accept the rejection via an explicit override variable in a later slice.

resource "azurerm_federated_identity_credential" "this" {
  name                = var.name
  resource_group_name = var.resource_group_name
  parent_id           = var.parent_id
  audience            = [var.audience]
  issuer              = var.issuer
  subject             = var.subject
}
