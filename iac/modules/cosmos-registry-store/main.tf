# Spec 006 / T013 / data-model.md §4.1. Provisions the three SQL containers
# that back the Service Bus registry slice on the existing `canonical` database
# (output by the cosmos-canonical-store module). The account is serverless
# (cosmos-account/main.tf — EnableServerless capability) so neither database
# nor container throughput attributes are configured here; serverless RU is
# billed per-request.
#
#   registry-entities          — PK /environment. ETag-based concurrency,
#                                tombstone-then-delete (research §10). Per-item
#                                TTL enabled (default_ttl = -1) so tombstones
#                                self-expire after 60s.
#   registry-audit             — PK /entityId. Append-only (FR-034). Entity-
#                                scoped reads via SELECT ... ORDER BY timestamp DESC.
#   registry-entities-leases   — PK /id. Change-feed lease state for the indexer
#                                (research §17 — managed-identity auth forbids
#                                the trigger from auto-creating it).

resource "azurerm_cosmosdb_sql_container" "registry_entities" {
  name                  = var.entities_container_name
  resource_group_name   = var.resource_group_name
  account_name          = var.cosmos_account_name
  database_name         = var.cosmos_canonical_database_name
  partition_key_paths   = ["/environment"]
  partition_key_version = 2

  # Per-item TTL: enabled by setting default_ttl = -1 (research §10). Items
  # without a `ttl` property persist indefinitely; tombstone markers set
  # `ttl = 60` so Cosmos auto-purges them.
  default_ttl = var.entity_default_ttl_seconds

  indexing_policy {
    indexing_mode = "consistent"

    included_path {
      path = "/*"
    }

    # data-model.md §4.3 — `metadata` is opaque JSON, indexed only via the
    # search projection. Excluding the path here keeps RU cost predictable
    # for hand-written queries (the indexer projects metadata into
    # `metadataFlat` on the AI Search index instead).
    excluded_path {
      path = "/metadata/*"
    }

    # data-model.md §4.3 — composite index on (parentId, entityType, name)
    # accelerates the duplicate-name and child-enumeration queries.
    composite_index {
      index {
        path  = "/parentId"
        order = "Ascending"
      }
      index {
        path  = "/entityType"
        order = "Ascending"
      }
      index {
        path  = "/name"
        order = "Ascending"
      }
    }
  }

  # Spec 006 / BT-IAC-007. The container holds the registry's primary data
  # plane — accidental replace destroys every operator-curated entity.
  lifecycle {
    prevent_destroy = true
  }
}

resource "azurerm_cosmosdb_sql_container" "registry_audit" {
  name                  = var.audit_container_name
  resource_group_name   = var.resource_group_name
  account_name          = var.cosmos_account_name
  database_name         = var.cosmos_canonical_database_name
  partition_key_paths   = ["/entityId"]
  partition_key_version = 2

  indexing_policy {
    indexing_mode = "consistent"

    # The audit panel queries by entityId (partition key, free) and orders by
    # timestamp DESC. Minimal indexing keeps writes cheap.
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

  lifecycle {
    prevent_destroy = true
  }
}

resource "azurerm_cosmosdb_sql_container" "registry_entities_leases" {
  name                = var.leases_container_name
  resource_group_name = var.resource_group_name
  account_name        = var.cosmos_account_name
  database_name       = var.cosmos_canonical_database_name
  # Cosmos change-feed trigger requires PK /id. The lease container is an
  # internal coordination surface — workloads do not query it directly.
  partition_key_paths   = ["/id"]
  partition_key_version = 2

  indexing_policy {
    indexing_mode = "consistent"
    included_path {
      path = "/*"
    }
  }

  # Lease state is reconstructable in principle but a replace would force the
  # change feed to re-deliver every document, regenerating audit-event-shaped
  # side effects in the indexer. Keep prevent_destroy on to make that
  # deliberate.
  lifecycle {
    prevent_destroy = true
  }
}
