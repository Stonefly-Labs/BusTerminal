variable "vnet_name" {
  description = "Virtual network name (from naming module). Convention: `vnet-<naming_prefix>`."
  type        = string
}

variable "resource_group_name" {
  description = "Resource group hosting the VNet and private DNS zones (typically the env RG)."
  type        = string
}

variable "location" {
  description = "Azure region for the VNet."
  type        = string
}

variable "address_space" {
  description = "VNet address space (e.g., [\"10.50.0.0/16\"]). Per env per research §10."
  type        = list(string)

  validation {
    condition     = length(var.address_space) > 0 && alltrue([for c in var.address_space : can(cidrnetmask(c))])
    error_message = "address_space must contain one or more valid CIDR blocks."
  }
}

variable "subnet_integration_cidr" {
  description = "CIDR for the Container Apps Environment integration subnet. /23 minimum (Azure CAE requirement). Must be inside address_space."
  type        = string

  validation {
    condition     = can(cidrnetmask(var.subnet_integration_cidr)) && tonumber(split("/", var.subnet_integration_cidr)[1]) <= 23
    error_message = "subnet_integration_cidr must be a valid CIDR with prefix /23 or larger (Container Apps Environment minimum)."
  }
}

variable "subnet_private_endpoints_cidr" {
  description = "CIDR for the private-endpoints subnet. /24 recommended. Must be inside address_space and non-overlapping with subnet_integration_cidr."
  type        = string

  validation {
    condition     = can(cidrnetmask(var.subnet_private_endpoints_cidr))
    error_message = "subnet_private_endpoints_cidr must be a valid CIDR block."
  }
}

variable "private_dns_zones" {
  description = <<-EOT
    List of private DNS zone names to provision and link to this VNet (e.g.,
    `privatelink.vaultcore.azure.net`, `privatelink.documents.azure.com`,
    `privatelink.search.windows.net`, `privatelink.servicebus.windows.net`,
    `privatelink.azurecr.io`). Per research §11 — the env composition decides
    which zones to provision based on `private_endpoints_enabled`.
  EOT
  type        = list(string)
}

variable "tags" {
  description = "Tags merged onto the VNet, subnets, and every private DNS zone."
  type        = map(string)
  default     = {}
}
