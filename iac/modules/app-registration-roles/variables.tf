variable "api_application_id" {
  description = <<-EOT
    Resource ID of the API `azuread_application` the roles attach to. Pass the
    `id` attribute of either a managed `azuread_application` resource (in which
    case its block must declare `lifecycle { ignore_changes = [app_role] }`) or
    a `data "azuread_application"` source for an app registration that lives
    outside tofu state.
  EOT
  type        = string

  validation {
    condition     = length(var.api_application_id) > 0
    error_message = "api_application_id must be a non-empty `azuread_application` resource id."
  }
}

variable "role_definitions" {
  description = <<-EOT
    Map of platform role definitions to declare on the target app registration.
    Each key is a stable role nickname (e.g. `admin`, `operator`, `reader`,
    `developer`); each value carries the on-wire role claim value, display
    metadata, and the stable role GUID used as the resource's role_id.
  EOT
  type = map(object({
    role_id              = string
    value                = string
    display_name         = string
    description          = string
    allowed_member_types = list(string)
  }))

  validation {
    condition     = length(var.role_definitions) == 4
    error_message = "role_definitions must contain exactly four entries (Admin, Operator, Reader, Developer)."
  }

  validation {
    condition = alltrue([
      for v in values(var.role_definitions) :
      can(regex("^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$", v.role_id))
    ])
    error_message = "Every role_definitions[*].role_id must be a UUID."
  }

  validation {
    condition = alltrue([
      for v in values(var.role_definitions) :
      length(v.allowed_member_types) > 0 && alltrue([
        for t in v.allowed_member_types : contains(["User", "Application"], t)
      ])
    ])
    error_message = "allowed_member_types must be a non-empty subset of [\"User\", \"Application\"]."
  }
}
