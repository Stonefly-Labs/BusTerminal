# Spec 004 — canonical resource store + change-event log on the Cosmos DB
# account provisioned by the cosmos-account module.
#
# Two containers, one database. The split mirrors the persistence design in
# data-model.md (entities 1 + 6) and plan.md §Storage:
#
#   resources       — partition key /resourceType. ETag-based optimistic
#                     concurrency (FR-025). Soft-delete via the isDeleted
#                     predicate filtered at query time (FR-020). The wholesale
#                     /extensions/* indexing exclusion supports FR-012's
#                     per-extension `__indexable` opt-back-in (downstream
#                     search-projection consumes the marker).
#
#   change-events   — partition key /resourceId. Append-only change log
#                     (FR-015 / Q5). One document per state change. Minimal
#                     indexing (only the fields queried by the per-resource
#                     history view) keeps RU cost low.
#
# Raw `azurerm_cosmosdb_sql_*` resources are used rather than the AVM module's
# `sql_databases` input because the cosmos-account module's AVM bypass leaves
# database+container provisioning here. See cosmos-account/main.tf for the AVM
# bypass rationale.

resource "azurerm_cosmosdb_sql_database" "canonical" {
  name                = var.database_name
  resource_group_name = var.resource_group_name
  account_name        = var.cosmos_account_name
  # Throughput intentionally null — the account is serverless. Setting a
  # throughput on a serverless account is rejected by the service.
}

resource "azurerm_cosmosdb_sql_container" "resources" {
  name                = var.resources_container_name
  resource_group_name = var.resource_group_name
  account_name        = var.cosmos_account_name
  database_name       = azurerm_cosmosdb_sql_database.canonical.name
  partition_key_paths = ["/resourceType"]
  # Partition-key v2 (the current default for new containers) — wider partition
  # key value space than v1.
  partition_key_version = 2

  indexing_policy {
    indexing_mode = "consistent"

    # /* is required as either included_path or excluded_path. Index everything
    # by default; then exclude the extensions surface so vendor-specific keys
    # don't burn RU. Per-extension opt-back-in happens at the search-projection
    # layer (downstream slice) via the resource's `extensions.__indexable` map.
    included_path {
      path = "/*"
    }

    excluded_path {
      path = "/extensions/*"
    }

    # Cosmos auto-indexes _etag etc., but it does NOT skip the system /_etag
    # path by default — and we never query by ETag, only use it as IfMatch.
    excluded_path {
      path = "/\"_etag\"/?"
    }
  }
}

resource "azurerm_cosmosdb_sql_container" "change_events" {
  name                  = var.change_events_container_name
  resource_group_name   = var.resource_group_name
  account_name          = var.cosmos_account_name
  database_name         = azurerm_cosmosdb_sql_database.canonical.name
  partition_key_paths   = ["/resourceId"]
  partition_key_version = 2

  indexing_policy {
    indexing_mode = "consistent"

    # Minimal index footprint: only the three fields the per-resource history
    # view queries by. Everything else (snapshot, diff, actor.*) is excluded so
    # append-only writes stay cheap and RU-efficient.
    included_path {
      path = "/resourceId/?"
    }

    included_path {
      path = "/timestamp/?"
    }

    included_path {
      path = "/eventType/?"
    }

    excluded_path {
      path = "/*"
    }
  }
}
