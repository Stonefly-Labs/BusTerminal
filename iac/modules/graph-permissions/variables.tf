variable "api_application_id" {
  description = <<-EOT
    Resource ID of the API `azuread_application` the Graph permissions attach to.
    Pass the `id` attribute of either a managed `azuread_application` resource
    or a `data "azuread_application"` source for an app registration that
    lives outside tofu state.
  EOT
  type        = string

  validation {
    condition     = length(var.api_application_id) > 0
    error_message = "api_application_id must be a non-empty `azuread_application` resource id."
  }
}

variable "granted_application_permission_ids" {
  description = <<-EOT
    List of Microsoft Graph **application-permission** role UUIDs the API app
    registration is requesting. Every entry MUST also appear in
    `specs/003-auth-and-identity/contracts/graph-permissions-inventory.md`
    (or the durable successor doc) with a rationale — adding here without
    updating the inventory is a defect.

    Current entries (slice 003):
      - `df021288-bdef-4463-88db-98f22de89214` → `User.Read.All` (Application)

    Reference: https://learn.microsoft.com/graph/permissions-reference
  EOT
  type        = list(string)

  validation {
    condition     = length(var.granted_application_permission_ids) > 0
    error_message = "granted_application_permission_ids cannot be empty — declare at least one Graph application permission UUID."
  }

  validation {
    condition = alltrue([
      for id in var.granted_application_permission_ids :
      can(regex("^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$", id))
    ])
    error_message = "Every granted_application_permission_ids entry must be a UUID (Graph permission role id)."
  }
}
