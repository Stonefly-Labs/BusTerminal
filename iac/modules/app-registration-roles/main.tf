terraform {
  required_version = ">= 1.11.0"

  required_providers {
    azuread = {
      source  = "hashicorp/azuread"
      version = "~> 3.1"
    }
  }
}

# Declares the BusTerminal platform app roles on a target API app registration,
# one `azuread_application_app_role` resource per entry in `var.role_definitions`.
#
# The role list is the spec-003 binding contract (Reader / Developer / Operator
# / Admin). Each role has `allowed_member_types = ["User", "Application"]` so
# both humans (FR-002) and workload managed identities (FR-022) can be granted
# the role via `azuread_app_role_assignment`.
#
# The parent `azuread_application` (if managed by tofu in the caller) MUST
# carry `lifecycle { ignore_changes = [app_role] }` to avoid drift between
# the inline `app_role` blocks and these `azuread_application_app_role`
# resources. If the parent app registration is OUT of tofu state (referenced
# via a `data` source), no such lifecycle override is required — the data
# source is read-only.
resource "azuread_application_app_role" "this" {
  for_each = var.role_definitions

  application_id       = var.api_application_id
  role_id              = each.value.role_id
  allowed_member_types = each.value.allowed_member_types
  description          = each.value.description
  display_name         = each.value.display_name
  value                = each.value.value
}
