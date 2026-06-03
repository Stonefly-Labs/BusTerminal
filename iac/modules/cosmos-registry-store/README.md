# Cosmos registry store module

Spec 006 / T013. Provisions the three SQL containers that back the Service
Bus registry slice on the existing `canonical` database (spec 004) of the
existing Cosmos account (spec 005):

- `registry-entities` — partition key `/environment`. ETag-based optimistic
  concurrency, tombstone-then-delete pattern (research §10). Composite index
  `(parentId, entityType, name)` for duplicate-name + child-enumeration
  queries. `/metadata/*` excluded from indexing. Per-item TTL enabled
  (`default_ttl = -1`) so tombstone markers self-expire after 60s.
- `registry-audit` — partition key `/entityId`. Append-only from the user
  perspective (FR-034); no TTL in v1. Minimal indexing (only `/timestamp`,
  `/eventType`) — entity-scoped queries hit the partition key for free.
- `registry-entities-leases` — partition key `/id`. Cosmos change-feed
  lease state for the indexer (research §17 — managed-identity auth forbids
  the trigger from auto-creating the lease container).

All three containers carry `lifecycle.prevent_destroy = true` per BT-IAC-007.
The account is serverless, so no throughput configuration is set on the
containers (the account capability rejects it).

See [`specs/006-service-bus-registry-core/data-model.md` §4.1](../../../specs/006-service-bus-registry-core/data-model.md#41-cosmos-containers).

<!-- BEGIN_TF_DOCS -->
<!-- END_TF_DOCS -->
