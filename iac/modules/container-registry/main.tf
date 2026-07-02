terraform {
  required_version = ">= 1.11.0"
}

module "registry" {
  source  = "Azure/avm-res-containerregistry-registry/azurerm"
  version = "0.4.0"

  name                          = var.name
  resource_group_name           = var.resource_group_name
  location                      = var.location
  sku                           = var.sku
  admin_enabled                 = false
  public_network_access_enabled = var.public_network_access_enabled
  zone_redundancy_enabled       = var.zone_redundancy_enabled

  # The AVM defaults `retention_policy_in_days = 7` (untagged-manifest purge),
  # which — like zone redundancy — is Premium-only: azurerm errors at plan
  # time on any other SKU unless it is unset (null).
  retention_policy_in_days = var.sku == "Premium" ? var.retention_policy_in_days : null

  # Static key (`audit`) so `for_each` inside the AVM diagnostic-setting
  # resource can enumerate at plan time even when `workspace_resource_id`
  # is only known after apply (first plan against empty state).
  #
  # Spec 005 / T085 / Q5c — `metric_categories = []` drops the AVM's default
  # `["AllMetrics"]` so the emitted diagnostic setting contains zero metric
  # blocks. The `allLogs` log group still flows. Satisfies BT-IAC-003.
  diagnostic_settings = {
    audit = {
      name                  = "acr-diagnostics"
      workspace_resource_id = var.log_analytics_workspace_id
      log_groups            = ["allLogs"]
      metric_categories     = []
    }
  }

  tags = var.tags
}

# Spec 005 — conditional private endpoint via the project's PE wrapper (T058).
# Subresource `registry` per the Azure private-endpoint DNS reference
# (research §11). ACR PEs require Premium SKU (the module defaults sku=Premium).
resource "terraform_data" "pe_validation" {
  input = {
    private_endpoint_subnet_id = var.private_endpoint_subnet_id
    private_dns_zone_id        = var.private_dns_zone_id
    sku                        = var.sku
  }

  lifecycle {
    precondition {
      condition     = var.private_endpoint_subnet_id == null || var.private_dns_zone_id != null
      error_message = "container-registry: private_dns_zone_id is required when private_endpoint_subnet_id is set."
    }
    precondition {
      condition     = var.private_endpoint_subnet_id == null || var.sku == "Premium"
      error_message = "container-registry: private endpoints require sku=\"Premium\"."
    }
  }
}

module "private_endpoint" {
  count = var.private_endpoint_subnet_id != null ? 1 : 0

  source = "../private-endpoint"

  name                = "pe-${var.name}"
  resource_group_name = var.resource_group_name
  location            = var.location
  subnet_id           = var.private_endpoint_subnet_id
  target_resource_id  = module.registry.resource_id
  subresource_name    = "registry"
  private_dns_zone_id = var.private_dns_zone_id
  tags                = var.tags
}
