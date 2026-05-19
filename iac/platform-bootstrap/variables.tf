variable "subscription_id" {
  description = "Azure subscription ID where the bootstrap resources are provisioned."
  type        = string
}

variable "github_org_repo" {
  description = "GitHub repository identifier in `<org>/<repo>` form. Used as the federated-credential subject prefix."
  type        = string
}

variable "environments" {
  description = "Set of environment names to create pipeline identities + federated credentials for (e.g., [\"dev\", \"test\", \"prod\"])."
  type        = set(string)
  default     = ["dev"]
}

variable "location" {
  description = "Azure region for the bootstrap resource group and state storage account."
  type        = string
  default     = "eastus2"
}

variable "tfstate_storage_account_name" {
  description = "Globally unique name for the storage account hosting OpenTofu state. Must be 3-24 lowercase alphanumeric characters."
  type        = string

  validation {
    condition     = can(regex("^[a-z0-9]{3,24}$", var.tfstate_storage_account_name))
    error_message = "tfstate_storage_account_name must be 3-24 lowercase alphanumeric characters."
  }
}
