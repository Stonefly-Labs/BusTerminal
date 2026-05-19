variable "name" {
  description = "Name of the user-assigned managed identity."
  type        = string
}

variable "resource_group_name" {
  description = "Resource group holding the identity."
  type        = string
}

variable "location" {
  description = "Azure region for the identity."
  type        = string
}

variable "tags" {
  description = "Tags applied to the identity."
  type        = map(string)
  default     = {}
}

variable "role_assignments" {
  description = "Map of role assignments to create. Key is a stable identifier (e.g., `kv-secrets-user`); value contains the role name and scope."
  type = map(object({
    role_definition_name = string
    scope                = string
  }))
  default = {}
}
