variable "environment_name" {
  description = "Logical environment name. One of dev, test, prod."
  type        = string

  validation {
    condition     = contains(["dev", "test", "prod"], var.environment_name)
    error_message = "environment_name must be one of: dev, test, prod."
  }
}

variable "naming_prefix" {
  description = "Short hyphenated prefix applied to every derived resource name (e.g., bt-dev, bt-test, bt-prod)."
  type        = string

  validation {
    condition     = can(regex("^bt-[a-z0-9]{2,8}$", var.naming_prefix))
    error_message = "naming_prefix must match ^bt-[a-z0-9]{2,8}$ (e.g., bt-dev, bt-test, bt-prod)."
  }
}

variable "unique_suffix" {
  description = "Globally-unique suffix appended to names that require uniqueness across Azure (Key Vault, ACR, Cosmos, Search, Service Bus). 4-12 lowercase alphanumeric characters."
  type        = string

  validation {
    condition     = can(regex("^[a-z0-9]{4,12}$", var.unique_suffix))
    error_message = "unique_suffix must be 4-12 lowercase alphanumeric characters."
  }
}
