terraform {
  required_version = ">= 1.11.0"

  required_providers {
    azuread = {
      source  = "hashicorp/azuread"
      version = "~> 3.1"
    }
  }
}

# Spec 003 / US6 / FR-024 — declares Microsoft Graph **application** permissions
# on a target app registration via the modern `azuread_application_api_access`
# resource (preferred over the legacy `required_resource_access` block on
# `azuread_application`; see research.md § 6).
#
# Why a separate module:
#   - The Graph permissions inventory at
#     `specs/003-auth-and-identity/contracts/graph-permissions-inventory.md`
#     is the source of truth for which permissions exist; this module is the
#     IaC mirror. Every entry added to the inventory in a future slice must
#     show up here (and vice versa) — the doc is reviewer-enforced.
#   - Admin consent is NOT automated. The grant declared by this resource is
#     "permission requested"; making it "permission active" requires a tenant
#     admin to grant consent in the Entra portal (or via
#     `az ad app permission admin-consent`). See A.2.3 in
#     `specs/003-auth-and-identity/quickstart.md` and the future
#     `docs/identity-graph-permissions.md` runbook.
#
# Microsoft Graph's well-known app id is resolved via the
# `azuread_application_published_app_ids` data source (which returns the stable
# `MicrosoftGraph` => `00000003-0000-0000-c000-000000000000` mapping). Using
# the data source rather than hardcoding the UUID future-proofs against any
# (extremely unlikely) Microsoft re-issuance.

data "azuread_application_published_app_ids" "well_known" {}

resource "azuread_application_api_access" "graph" {
  application_id = var.api_application_id
  api_client_id  = data.azuread_application_published_app_ids.well_known.result.MicrosoftGraph

  # Application-permission UUIDs only; delegated scopes are intentionally empty
  # — this slice grants app-only Graph access (FR-024, FR-025). Future slices
  # that need delegated flows declare them on the *web* app registration, not
  # here.
  role_ids = var.granted_application_permission_ids
}
