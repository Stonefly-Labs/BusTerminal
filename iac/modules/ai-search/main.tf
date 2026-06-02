# Azure AI Search module — search service + diagnostic settings + optional PE +
# workload RBAC grant (Search Index Data Contributor). Wraps the Azure Verified
# Module Azure/avm-res-search-searchservice (v0.2.0). Per research §4 + §11.
#
# SKU validation (precondition): `free` is rejected when public access is off
# OR when a PE is requested — free SKU supports neither AAD/RBAC nor PEs.

resource "terraform_data" "sku_validation" {
  input = {
    sku                           = var.sku
    public_network_access_enabled = var.public_network_access_enabled
    private_endpoint_subnet_id    = var.private_endpoint_subnet_id
  }

  lifecycle {
    precondition {
      condition     = var.sku != "free" || (var.public_network_access_enabled && var.private_endpoint_subnet_id == null)
      error_message = "ai-search: sku=\"free\" rejected when public access is disabled or a private endpoint is requested. Free SKU supports neither AAD/RBAC nor private endpoints."
    }
    precondition {
      condition     = var.private_endpoint_subnet_id == null || var.private_dns_zone_id != null
      error_message = "ai-search: private_dns_zone_id is required when private_endpoint_subnet_id is set."
    }
  }
}

module "search" {
  source  = "Azure/avm-res-search-searchservice/azurerm"
  version = "0.2.0"

  name                = var.name
  resource_group_name = var.resource_group_name
  location            = var.location
  sku                 = var.sku
  tags                = var.tags
  enable_telemetry    = false

  public_network_access_enabled = var.public_network_access_enabled

  # AAD-only data plane per FR-016. Browser/admin keys disabled.
  # `authentication_failure_mode` is INVALID when local auth is disabled —
  # Azure rejects the resource with "'authentication_failure_mode' cannot
  # be defined if 'local_authentication_enabled' has been set to 'false'".
  # The setting only controls the failure response shape for key-based
  # auth; AAD-only callers always get a 401 with a bearer challenge.
  local_authentication_enabled = false

  # System-assigned identity intentionally disabled — the search service does not
  # need to authenticate to other services in this slice. Workload access flows
  # the other way: the workload UAMI authenticates to the search service via the
  # Search Index Data Contributor role assignment below.
  managed_identities = {
    system_assigned = false
  }
}

# allLogs-only diagnostic forwarding per Q5c.
module "diagnostics" {
  source = "../diagnostic-settings"

  name                       = "${var.name}-diagnostics"
  target_resource_id         = module.search.resource_id
  log_analytics_workspace_id = var.log_analytics_workspace_id
}

# Search Index Data Contributor (GUID 8ebe5a00-799e-43f5-93ac-243d3dce84a7) to
# the workload UAMI scoped to this search service — FR-033. The role permits
# index reads, writes, and creates; not service-level administration.
resource "azurerm_role_assignment" "workload_search_index_data_contributor" {
  scope                = module.search.resource_id
  role_definition_name = "Search Index Data Contributor"
  principal_id         = var.workload_principal_id
  description          = "Spec 005 FR-033 — workload UAMI index data-plane access (no admin)."
}

# Conditional private endpoint via the project's PE wrapper (research §11).
# `count` keyed off the plan-time-known bool `private_endpoint_enabled` —
# see variables.tf for why we can't key off `subnet_id != null` (subnet_id
# is known-after-apply from the networking module's output).
resource "terraform_data" "pe_inputs_validation" {
  count = var.private_endpoint_enabled ? 1 : 0

  input = {
    subnet_id   = var.private_endpoint_subnet_id
    dns_zone_id = var.private_dns_zone_id
  }

  lifecycle {
    precondition {
      condition     = var.private_endpoint_subnet_id != null && var.private_dns_zone_id != null
      error_message = "ai-search: private_endpoint_subnet_id and private_dns_zone_id are required when private_endpoint_enabled = true."
    }
  }
}

module "private_endpoint" {
  count = var.private_endpoint_enabled ? 1 : 0

  source = "../private-endpoint"

  name                = "pe-${var.name}"
  resource_group_name = var.resource_group_name
  # PE must live in the SUBNET's region, not the search service's. When
  # `var.private_endpoint_location` is supplied (env composition pinned the
  # search service to a different region than its VNet), use it; otherwise
  # fall back to `var.location`.
  location            = coalesce(var.private_endpoint_location, var.location)
  subnet_id           = var.private_endpoint_subnet_id
  target_resource_id  = module.search.resource_id
  subresource_name    = "searchService"
  private_dns_zone_id = var.private_dns_zone_id
  tags                = var.tags
}
