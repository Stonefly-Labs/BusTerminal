# Networking module — VNet + subnets + private DNS zones + zone-VNet links.
# Wraps the Azure Verified Modules avm-res-network-virtualnetwork (v0.16.0) and
# avm-res-network-privatednszone (v0.4.2). Per research §10 + §11.
#
# Preconditions (per contracts/module-contracts.md §networking):
#   1. subnet_integration_cidr + subnet_private_endpoints_cidr are inside address_space
#   2. The two subnet CIDRs do not overlap
#   3. subnet_integration_cidr is /23 or larger (CAE minimum)

data "azurerm_resource_group" "this" {
  name = var.resource_group_name
}

# Cross-variable preconditions live on a terraform_data resource because
# variable-level `validation` blocks can only reference a single variable
# (Tofu < 1.9 limitation that still applies for portability) and `lifecycle`
# blocks are not permitted on module calls.
resource "terraform_data" "subnet_validation" {
  input = {
    address_space                 = var.address_space
    subnet_integration_cidr       = var.subnet_integration_cidr
    subnet_private_endpoints_cidr = var.subnet_private_endpoints_cidr
  }

  lifecycle {
    precondition {
      condition     = !cidrcontains(var.subnet_integration_cidr, cidrhost(var.subnet_private_endpoints_cidr, 0))
      error_message = "subnet_integration_cidr and subnet_private_endpoints_cidr must not overlap."
    }
    precondition {
      condition     = !cidrcontains(var.subnet_private_endpoints_cidr, cidrhost(var.subnet_integration_cidr, 0))
      error_message = "subnet_integration_cidr and subnet_private_endpoints_cidr must not overlap."
    }
    precondition {
      condition     = anytrue([for c in var.address_space : cidrcontains(c, cidrhost(var.subnet_integration_cidr, 0))])
      error_message = "subnet_integration_cidr must be inside one of the address_space CIDR blocks."
    }
    precondition {
      condition     = anytrue([for c in var.address_space : cidrcontains(c, cidrhost(var.subnet_private_endpoints_cidr, 0))])
      error_message = "subnet_private_endpoints_cidr must be inside one of the address_space CIDR blocks."
    }
  }
}

# VNet + two subnets via AVM. The CAE integration subnet is delegated to
# Microsoft.App/environments so a future container-apps-env retrofit (deferred
# per Q2c) can attach without provisioning a new subnet. The PE subnet has
# private_endpoint_network_policies set to "Disabled" — required for PE
# attachment per Azure PE documentation.
module "vnet" {
  source  = "Azure/avm-res-network-virtualnetwork/azurerm"
  version = "0.16.0"

  name             = var.vnet_name
  parent_id        = data.azurerm_resource_group.this.id
  location         = var.location
  address_space    = var.address_space
  tags             = var.tags
  enable_telemetry = false

  subnets = {
    integration = {
      name             = "snet-cae-integration"
      address_prefixes = [var.subnet_integration_cidr]
      delegations = [{
        name = "Microsoft.App.environments"
        service_delegation = {
          name = "Microsoft.App/environments"
        }
      }]
    }
    private_endpoints = {
      name                              = "snet-private-endpoints"
      address_prefixes                  = [var.subnet_private_endpoints_cidr]
      private_endpoint_network_policies = "Disabled"
    }
  }
}

# One private DNS zone per supplied zone name, each linked to the new VNet so
# any private endpoint registered in the zone resolves from any resource on
# the VNet (or any future peered VNet that links the same zone).
#
# `private_dns_zone_supports_private_link = true` is the AVM hint that this
# zone hosts privatelink.* A-records — the resolver applies the NxDomainRedirect
# policy so non-PE queries fall back to public DNS rather than returning
# NXDOMAIN.
module "private_dns_zones" {
  source   = "Azure/avm-res-network-privatednszone/azurerm"
  version  = "0.4.2"
  for_each = toset(var.private_dns_zones)

  domain_name      = each.value
  parent_id        = data.azurerm_resource_group.this.id
  tags             = var.tags
  enable_telemetry = false

  virtual_network_links = {
    env_vnet = {
      name                                   = "vnet-link-${replace(each.value, ".", "-")}"
      virtual_network_id                     = module.vnet.resource_id
      registration_enabled                   = false
      resolution_policy                      = "NxDomainRedirect"
      private_dns_zone_supports_private_link = true
    }
  }
}
