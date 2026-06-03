# Cosmos registry store module

Spec 006 / Phase 1 T008 — module skeleton. Phase 2 T013 fills the body.

Provisions the three SQL containers that back the Service Bus registry slice
on the existing `canonical` database (spec 004) of the existing Cosmos account
(spec 005):

- `registry-entities` — partition key `/environment`. Autoscale max 4000 / min 400.
  ETag-based optimistic concurrency. Composite index `(parentId, entityType, name)`
  for duplicate-name + child-enumeration queries. `/metadata/*` excluded from
  indexing.
- `registry-audit` — partition key `/entityId`. Autoscale max 1000 / min 100.
  Append-only from the user perspective (FR-034); no TTL in v1.
- `registry-entities-leases` — partition key `/id`. Autoscale max 400 / min 100.
  Cosmos change-feed lease state for the indexer (research §17 — managed-identity
  auth forbids the trigger from auto-creating the lease container).

All three containers carry `lifecycle.prevent_destroy = true` per BT-IAC-007.

See [`specs/006-service-bus-registry-core/data-model.md` §4.1](../../../specs/006-service-bus-registry-core/data-model.md#41-cosmos-containers).

<!-- BEGIN_TF_DOCS -->
<!-- END_TF_DOCS -->
