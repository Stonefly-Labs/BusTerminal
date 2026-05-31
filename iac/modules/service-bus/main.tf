# Service Bus namespace module — namespace only (no topics/queues per FR-022 +
# spec clarification Q3) + diagnostic settings + optional PE (Premium only) +
# workload Sender/Receiver RBAC grants. Wraps the Azure Verified Module
# Azure/avm-res-servicebus-namespace v0.4.0. Per research §3 + §11 + §12.
#
# Preconditions (per contracts/module-contracts.md §service-bus):
#   1. sku=Basic rejected (caught by validation on var.sku; double-checked here)
#   2. sku=Standard + non-null PE inputs → ERROR (no PE support on Standard)
#   3. sku=Premium + null capacity → ERROR

resource "terraform_data" "sku_validation" {
  input = {
    sku                        = var.sku
    capacity                   = var.capacity
    private_endpoint_subnet_id = var.private_endpoint_subnet_id
  }

  lifecycle {
    precondition {
      condition     = var.sku != "Standard" || var.private_endpoint_subnet_id == null
      error_message = "service-bus: sku=\"Standard\" + non-null private_endpoint_subnet_id is rejected. Standard SKU does not support private endpoints; either upgrade to Premium or null the PE inputs at the env composition layer."
    }
    precondition {
      condition     = var.sku != "Premium" || var.capacity != null
      error_message = "service-bus: sku=\"Premium\" requires var.capacity (1, 2, 4, 8, or 16 messaging units)."
    }
    precondition {
      condition     = var.private_endpoint_subnet_id == null || var.private_dns_zone_id != null
      error_message = "service-bus: private_dns_zone_id is required when private_endpoint_subnet_id is set."
    }
  }
}

module "namespace" {
  source  = "Azure/avm-res-servicebus-namespace/azurerm"
  version = "0.4.0"

  name                = var.name
  resource_group_name = var.resource_group_name
  location            = var.location
  sku                 = var.sku
  tags                = var.tags
  enable_telemetry    = false

  # capacity is meaningful only for Premium; the AVM ignores it for Standard.
  capacity = var.sku == "Premium" ? var.capacity : null

  public_network_access_enabled = var.public_network_access_enabled

  # AAD-only data plane per FR-016 (no SAS).
  local_auth_enabled = false

  # No topics/queues/subscriptions in this slice per FR-022 + spec Q3 (namespace
  # only). Future specs add messaging entities incrementally.
}

# allLogs-only diagnostic forwarding per Q5c.
module "diagnostics" {
  source = "../diagnostic-settings"

  name                       = "${var.name}-diagnostics"
  target_resource_id         = module.namespace.resource_id
  log_analytics_workspace_id = var.log_analytics_workspace_id
}

# Sender + Receiver roles to the workload UAMI scoped to this namespace —
# FR-033 forward-looking workload RBAC. The role GUIDs come from research §12
# and are mirrored in the bootstrap RBAC-Admin condition allowlist.
#
# Azure Service Bus Data Sender — 69a216fc-b8fb-44d8-bc22-1f3c2cd27a39
# Azure Service Bus Data Receiver — 4f6d3b9b-027b-4f4c-9142-0e5a2a2247e0
resource "azurerm_role_assignment" "workload_sb_data_sender" {
  scope                = module.namespace.resource_id
  role_definition_name = "Azure Service Bus Data Sender"
  principal_id         = var.workload_principal_id
  description          = "Spec 005 FR-033 — workload UAMI send-only data plane."
}

resource "azurerm_role_assignment" "workload_sb_data_receiver" {
  scope                = module.namespace.resource_id
  role_definition_name = "Azure Service Bus Data Receiver"
  principal_id         = var.workload_principal_id
  description          = "Spec 005 FR-033 — workload UAMI receive-only data plane."
}

# Conditional private endpoint via the project's PE wrapper (research §11).
# The env composition is responsible for ensuring PE inputs are only passed
# when sku=Premium (precondition above enforces it).
module "private_endpoint" {
  count = var.private_endpoint_subnet_id != null ? 1 : 0

  source = "../private-endpoint"

  name                = "pe-${var.name}"
  resource_group_name = var.resource_group_name
  location            = var.location
  subnet_id           = var.private_endpoint_subnet_id
  target_resource_id  = module.namespace.resource_id
  subresource_name    = "namespace"
  private_dns_zone_id = var.private_dns_zone_id
  tags                = var.tags
}
