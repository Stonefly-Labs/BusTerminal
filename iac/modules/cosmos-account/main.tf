# Spec 004 — Cosmos DB account for the canonical metadata store (FR-001 / FR-020 /
# FR-025). This module intentionally provisions the account with `azurerm_cosmosdb_account`
# directly rather than `Azure/avm-res-documentdb-databaseaccount/azurerm` v0.10.0.
#
# AVM bypass rationale: the AVM module silently overrides
# `local_authentication_disabled` to `false` when its `sql_databases` input is empty
# (see `main.tf` line 15 of the AVM at tag v0.10.0). The cosmos-canonical-store
# module owns database + container provisioning per spec design (T011) so the AVM's
# `sql_databases` input would be empty, defeating the AAD-only enforcement T008
# requires. Using the resource directly is the simplest way to honor both the
# AAD-only constraint and the multi-module separation the spec calls for.
#
# When an AVM release fixes this override (track `Azure/terraform-azurerm-avm-res-documentdb-databaseaccount`),
# this module can switch back to the AVM wrapper with no API change.
resource "azurerm_cosmosdb_account" "this" {
  name                = var.name
  location            = var.location
  resource_group_name = var.resource_group_name

  # NoSQL (SQL API) — the only API surface this slice consumes.
  kind              = "GlobalDocumentDB"
  offer_type        = "Standard"
  free_tier_enabled = false

  # Serverless capacity for dev — no provisioned RU/s charges. The spec defers
  # scale envelope (`Assumptions §"Persistence is Azure Cosmos DB"`). Switch to
  # provisioned-throughput in a later operational slice if scale demands it.
  capabilities {
    name = "EnableServerless"
  }

  # AAD-only data plane. No account-key authentication. FR-018 + plan §Constraints
  # ("Managed Identity for SDK auth. Microsoft.Azure.Cosmos.CosmosClient is constructed
  # with DefaultAzureCredential. No account keys.").
  local_authentication_disabled = true

  # Spec 005 FR-031 — per-env public-network-access toggle (Q2c).
  public_network_access_enabled = var.public_network_access_enabled

  # Automatic-failover off for dev — single-region serverless. AVM rejects multi-region
  # with EnableServerless anyway.
  automatic_failover_enabled = false

  # Encryption at rest is on by default with Microsoft-managed keys. No CMK in this
  # slice (no compliance requirement on the table yet — plan §Constraints).

  # Session consistency — the Cosmos default and the right choice for a metadata
  # registry where same-session read-your-writes matters but cross-region/strong
  # consistency does not.
  consistency_policy {
    consistency_level = "Session"
  }

  # Single-region serverless. Required by EnableServerless validation.
  geo_location {
    location          = var.location
    failover_priority = 0
    zone_redundant    = false
  }

  # Continuous backup is the Cosmos default for serverless. Explicit for clarity.
  backup {
    type = "Continuous"
    tier = "Continuous7Days"
  }

  tags = var.tags

  # Spec 005 / US7 / T121 — belt-and-suspenders against accidental destroy.
  # Cosmos holds the canonical store; replacing the account replaces all data
  # and breaks every downstream consumer's connection string. The BT-IAC-007
  # policy gate is the primary defense; this is the secondary block. To
  # intentionally replace, remove this block in a dedicated PR.
  lifecycle {
    prevent_destroy = true
  }
}

# Spec 005 / T085 — diagnostic setting routed through the central
# `diagnostic-settings` wrapper (enforces `allLogs`-only per Q5c by construction
# — the wrapper does NOT emit an `enabled_metric` block). The prior inline
# resource included `enabled_metric { category = "AllMetrics" }`, which is
# removed here per Q5c. The `moved` block migrates existing state addresses
# into the module path so Tofu treats the change as an attribute update on the
# same Azure resource rather than a destroy/create cycle.
module "diagnostics" {
  count = var.log_analytics_workspace_id == null ? 0 : 1

  source = "../diagnostic-settings"

  name                       = "${var.name}-diagnostics"
  target_resource_id         = azurerm_cosmosdb_account.this.id
  log_analytics_workspace_id = var.log_analytics_workspace_id
}

moved {
  from = azurerm_monitor_diagnostic_setting.this[0]
  to   = module.diagnostics[0].azurerm_monitor_diagnostic_setting.this
}

# Spec 005 — conditional private endpoint via the project's PE wrapper
# (T052). Subresource `Sql` per the Azure private-endpoint DNS reference
# (research §11). `count` keyed off the plan-time-known
# `private_endpoint_enabled` bool — see variables.tf for why we can't key
# off `subnet_id != null` (subnet_id is known-after-apply from the
# networking module's output).
resource "terraform_data" "pe_validation" {
  count = var.private_endpoint_enabled ? 1 : 0

  input = {
    private_endpoint_subnet_id = var.private_endpoint_subnet_id
    private_dns_zone_id        = var.private_dns_zone_id
  }

  lifecycle {
    precondition {
      condition     = var.private_endpoint_subnet_id != null && var.private_dns_zone_id != null
      error_message = "cosmos-account: private_endpoint_subnet_id and private_dns_zone_id are required when private_endpoint_enabled = true."
    }
  }
}

module "private_endpoint" {
  count = var.private_endpoint_enabled ? 1 : 0

  source = "../private-endpoint"

  name                = "pe-${var.name}"
  resource_group_name = var.resource_group_name
  location            = var.location
  subnet_id           = var.private_endpoint_subnet_id
  target_resource_id  = azurerm_cosmosdb_account.this.id
  subresource_name    = "Sql"
  private_dns_zone_id = var.private_dns_zone_id
  tags                = var.tags
}
