# Data Model — Service Bus Registry Core (Phase 1)

**Plan**: [plan.md](./plan.md) · **Spec**: [spec.md](./spec.md) · **Research**: [research.md](./research.md)

This document is the registry-side source of truth for entity shape, persistence layout, search projection, audit schema, validation rules, and concurrency semantics. Naming is uniform across in-process C# types, persisted JSON, JSON Schemas in `contracts/`, the AI Search index, OpenAPI DTOs, and telemetry attributes — verified in §Naming Cross-Reference. The §Vocabulary Alignment section maps every registry-side term to its spec-004 counterpart so a future "registry-domain unification" spec can reconcile the two models.

---

## 1. Entity catalog

The registry models five concrete entity types, all implementing `IRegistryEntity` and sharing the canonical-field set defined in §2.

### 1.1 `Namespace` (`entityType: "Namespace"`)

The root of the messaging hierarchy. Represents an Azure Service Bus namespace.

| Field | Type | Notes |
|---|---|---|
| (canonical fields) | (§2) | |
| `parentId` | `Guid?` | Always null; namespaces are roots. Persisted as `null`. |
| `namespaceName` | `string` | Logical namespace name; mirror of `name` for namespace entities. |

**Children**: zero-or-more Queues, zero-or-more Topics.

### 1.2 `Queue` (`entityType: "Queue"`)

A queue inside a namespace.

| Field | Type | Notes |
|---|---|---|
| (canonical fields) | (§2) | |
| `parentId` | `Guid` | Required; references a `Namespace` in the same environment. |

### 1.3 `Topic` (`entityType: "Topic"`)

A topic inside a namespace.

| Field | Type | Notes |
|---|---|---|
| (canonical fields) | (§2) | |
| `parentId` | `Guid` | Required; references a `Namespace` in the same environment. |

**Children**: zero-or-more Subscriptions.

### 1.4 `Subscription` (`entityType: "Subscription"`)

A subscription on a topic.

| Field | Type | Notes |
|---|---|---|
| (canonical fields) | (§2) | |
| `parentId` | `Guid` | Required; references a `Topic` in the same environment. |

**Children**: zero-or-more Rules.

### 1.5 `Rule` (`entityType: "Rule"`)

A subscription rule/filter. Spec-006 makes Rule a first-class entity (divergence from spec-004 where `Rule` is embedded in `Subscription`).

| Field | Type | Notes |
|---|---|---|
| (canonical fields) | (§2) | |
| `parentId` | `Guid` | Required; references a `Subscription` in the same environment. |

---

## 2. Canonical shared field set

Every registry entity carries every field in this table. The first column is the in-process C# property name (PascalCase); the second column is the persisted JSON field (camelCase); the third column maps to the OpenAPI DTO (camelCase) and the AI Search index (camelCase). Naming is identical across layers (Constitution Principle III).

| C# property | JSON field | Type | Required | Notes (FR refs) |
|---|---|---|---|---|
| `Id` | `id` | `Guid` (string) | ✅ FR-003 | Stable identifier; immutable after first save. |
| `EntityType` | `entityType` | `string` enum | ✅ FR-003 | One of `Namespace` / `Queue` / `Topic` / `Subscription` / `Rule`. Immutable after first save (FR-012). |
| `Name` | `name` | `string` | ✅ FR-003 | Azure-Service-Bus-compliant (FR-015). Case-preserved exactly as entered (Edge Case "Special characters"). |
| `FullyQualifiedName` | `fullyQualifiedName` | `string` | optional | Server-computed on write: `<namespaceName>/<parent path>/<name>`. Read-only from client perspective. |
| `Description` | `description` | `string?` | optional | Free-form. |
| `Tags` | `tags` | `[{key:string,value:string}]` | optional (default `[]`) | Free-form key/value pairs (FR-002 + research §9). Key matched case-insensitively, displayed case-preserved (first-write wins for display normalization). Multi-value-per-key permitted. |
| `Owner` | `owner` | `string?` | optional | Free-form owning team/person. NOT a reference to a Team entity in this slice. |
| `Environment` | `environment` | `string` | ✅ FR-003 | Configurable list per spec.md Assumptions; not a closed enum. Cosmos partition key. |
| `Status` | `status` | `string` enum | ✅ FR-003 | `Active` or `Deprecated` only in this slice (FR-002 clarification). `Deleted` reserved, never emitted. |
| `CreatedAtUtc` | `createdAtUtc` | ISO 8601 UTC | ✅ FR-005 | Immutable on first save. Server-stamped. |
| `UpdatedAtUtc` | `updatedAtUtc` | ISO 8601 UTC | ✅ FR-005 | Server-stamped on every mutation. |
| `Source` | `source` | `string` enum | ✅ FR-004 | `Manual` only in this slice. `Discovered` reserved. Server-stamped. |
| `AzureResourceId` | `azureResourceId` | `string?` | optional | ARM resource ID. Format-validated when present (FR-015 + research §1). |
| `NamespaceName` | `namespaceName` | `string?` | optional | Logical parent namespace name (for child entities; namespaces echo their own name). |
| `Metadata` | `metadata` | `object?` | optional | Extensible structured JSON. Persisted as opaque `JsonElement`. Projected to AI Search as a flattened map. |
| `ParentId` | `parentId` | `Guid?` | conditional | Required for Queue/Topic/Subscription/Rule (FR-008); null for Namespace. |
| `Etag` | `_etag` | `string` | server-managed | Cosmos-managed; surfaced to API responses as the `ETag` header for optimistic concurrency (FR-020). |

**Canonical JSON shape** (per `contracts/registry-entity.schema.json`):

```jsonc
{
  "id": "9c8f3b1a-...-b2",
  "entityType": "Queue",
  "name": "orders-incoming",
  "fullyQualifiedName": "orders-prod/orders-incoming",
  "description": "Primary order intake queue",
  "tags": [
    { "key": "Owner", "value": "PaymentsTeam" },
    { "key": "Tier", "value": "1" }
  ],
  "owner": "payments-platform",
  "environment": "dev",
  "status": "Active",
  "createdAtUtc": "2026-06-02T14:30:00Z",
  "updatedAtUtc": "2026-06-02T14:35:12Z",
  "source": "Manual",
  "azureResourceId": "/subscriptions/.../namespaces/orders-prod/queues/orders-incoming",
  "namespaceName": "orders-prod",
  "metadata": { "maxDeliveryCount": 10, "ttlSeconds": 86400 },
  "parentId": "5e3c2a7d-...-1c",
  "_etag": "\"00000000-0000-0000-1234-567890abcdef\""
}
```

---

## 3. Validation rules

Validation runs on the API boundary via FluentValidation (research §1). The frontend mirrors the same rules via Zod (research §20 contract test guarantees parity).

### 3.1 Universal rules (every entity, every write)

| Rule | Severity | Description |
|---|---|---|
| `RequiredFieldsRule` | Error | `id`, `entityType`, `name`, `environment`, `status` MUST be non-null/non-empty. (FR-003) |
| `EntityTypeImmutableRule` | Error | On PUT, the submitted `entityType` must match the persisted value. (FR-012) |
| `IdImmutableRule` | Error | On PUT, the submitted `id` must match the URL `{id}`. (FR-012) |
| `TimestampImmutableRule` | Error | `createdAtUtc` and `entityType` must not change. `updatedAtUtc` is server-stamped on every successful write. (FR-005, FR-012) |
| `NameFormatRule` | Error | `name` matches the **base** Azure Service Bus naming pattern: `^[A-Za-z0-9][A-Za-z0-9._\-/]{0,259}$` (first char alphanumeric). Per-entity-type length and charset specialization is layered on top by `EntityTypeNameSpecializationRule` (§3.2). |
| `TimestampImmutableRule` | Error | `createdAtUtc` MUST NOT change on update; `updatedAtUtc` is server-stamped on every successful write (FR-005). Clients MUST NOT submit these fields; the API ignores client-supplied values. |
| `StatusValueRule` | Error | `status` ∈ { `Active`, `Deprecated` }. `Deleted` is rejected (FR-002 clarification). |
| `SourceValueRule` | Error | `source` = `Manual` only in this slice. `Discovered` is rejected. (FR-004) |
| `AzureResourceIdFormatRule` | Error | When present: matches ARM-resource-ID pattern AND the resource-type segment is consistent with `entityType` (e.g., for `entityType: "Queue"`, the ARM ID must contain `.../namespaces/<ns>/queues/<name>`). (Edge Case "Invalid Azure resource IDs"). |
| `TagShapeRule` | Error | Each tag has non-empty `key` and `value`. Tag list size ≤ 50 per entity (defensive cap). |
| `TagDisplayNormalizationRule` | (transform) | If the entity already has a tag with key matching the submitted key case-insensitively, the persisted casing is the first-write casing (research §9). |
| `MetadataSizeRule` | Error | `metadata` serialized JSON ≤ 100KB (defensive cap; Cosmos doc size limit is 2MB). |

### 3.2 Entity-type rules

| Rule | Severity | Description |
|---|---|---|
| `EntityTypeNameSpecializationRule` | Error | Per Azure Service Bus naming reference, name length and charset narrow by type: **Namespace** 6–50 chars, charset `^[A-Za-z][A-Za-z0-9\-]{4,48}[A-Za-z0-9]$` (must start with letter, end alphanumeric, hyphens only inside); **Queue / Topic** ≤ 260 chars, base pattern; **Subscription / Rule** ≤ 50 chars, base pattern. Validators per type compose `NameFormatRule` (base) AND this rule. |
| `ParentRequiredRule` | Error | Queue / Topic / Subscription / Rule require `parentId`. Namespace requires `parentId` to be null. (FR-008) |
| `ParentExistenceRule` | Error | When `parentId` is set, the parent entity must exist in the same `environment` partition AND its `entityType` must match the expected parent type (Namespace for Queue/Topic, Topic for Subscription, Subscription for Rule). Validated by Cosmos point-read (research §11). (FR-008) |
| `DuplicateNameRule` | Error | Within the same `(parentId, environment)` scope, no two entities of the same `entityType` may share the same `name`. Validated by partition-scoped Cosmos query. (FR-014) |
| `ChildlessOnDeleteRule` | Error (409) | Delete is rejected with `409 Conflict` when any entity in the same `environment` has `parentId = <subject's id>`. Response body includes the child count and the entity-type breakdown. (FR-009, FR-013) |
| `StatusTransitionRule` | (allowed) | `Active ↔ Deprecated` are both directions permitted as operator-driven transitions. No `Active → Active` or `Deprecated → Deprecated` no-op rejection — same-status writes are accepted but produce no `StatusChanged` audit event. (FR-013a) |

### 3.3 Concurrency rules

| Rule | Behavior |
|---|---|
| Optimistic concurrency on PUT/DELETE | Client provides `If-Match: <etag>`; Cosmos returns `412 PreconditionFailed` if stale; `ConcurrencyConflictMapper` translates to `409 Conflict` with the response body shape in `contracts/conflict-response.schema.json` (research §8). |
| Force-overwrite signal | Client may re-submit the PUT with body extension `_overwriteAcknowledged: true` AND a fresh `If-Match: <current-etag>`; the resulting audit event sets `wasForceOverwrite: true`. (FR-020) |
| Force-overwrite without conflict | If `_overwriteAcknowledged: true` is sent when there is NO active conflict (the `If-Match` matches), the API returns `400 Bad Request` (`code: "ForceOverwriteWithoutConflict"`) — prevents flag-stuffing. |

---

## 4. Persistence layout

### 4.1 Cosmos containers

| Container | Database | Partition key | RU mode | Notes |
|---|---|---|---|---|
| `registry-entities` | `canonical` (existing spec-004 db) | `/environment` | Autoscale max 4000 / min 400 | Primary registry data; one document per entity (regardless of `entityType`). |
| `registry-audit` | `canonical` | `/entityId` | Autoscale max 1000 / min 100 | One document per audit event; append-only from the user perspective. |
| `registry-entities-leases` | `canonical` | `/id` | Autoscale max 400 / min 100 | Cosmos change-feed lease state for the indexer; IaC-provisioned (research §17). |

### 4.2 Document shapes

`registry-entities` carries the canonical-fields JSON from §2 plus the `parentId` and tombstone fields:

```jsonc
{
  // ... all canonical fields from §2 ...
  "_isTombstone": false,         // true only on the brief tombstone document written during hard delete (research §10)
  "_tombstoneFor": null          // when _isTombstone = true, the id of the entity being deleted
}
```

`registry-audit` carries the audit event shape from `contracts/audit-event.schema.json` (see §5 below).

### 4.3 Indexing policy (Cosmos)

- Default indexing on every canonical field.
- `metadata` is excluded from indexing by default (large arbitrary JSON; indexed via the search projection instead).
- Composite index on `(parentId ASC, entityType ASC, name ASC)` to accelerate `DuplicateNameRule` queries (and child enumeration for the explorer).

---

## 5. Audit event shape

Per `contracts/audit-event.schema.json` and research §15. One document per event in `registry-audit`.

| Field | Type | Notes |
|---|---|---|
| `id` | `Guid` | Event identifier. Partition key for natural fan-out under high-frequency entities. |
| `entityId` | `Guid` | Subject of the change. Cosmos partition key (`/entityId`). |
| `entityType` | `string` | Mirror of the entity's type at event time (frozen). |
| `environment` | `string` | Mirror at event time. |
| `eventType` | `string` enum | `Created` / `Updated` / `Deleted` / `StatusChanged`. |
| `timestamp` | ISO 8601 UTC | Server-stamped. |
| `actor` | `{ principalId, displayName }` | From the platform principal accessor; `principalId` is the Entra OID, `displayName` is the human-readable preferred-name claim. |
| `changeSummary` | `string` | Human-readable summary (e.g., "Created Queue 'orders-incoming' under namespace 'orders-prod'"). |
| `fieldChanges` | `[{ field, before, after }]?` | Field diff on `Updated` and `StatusChanged`. Computed server-side. Null for `Created` and `Deleted`. |
| `wasForceOverwrite` | `bool` | True iff the originating PUT carried `_overwriteAcknowledged: true` AND the API detected a conflict. False otherwise. (FR-020) |
| `correlationId` | `string` | The W3C `traceparent` trace ID. Links to the originating frontend trace (FR-042 + SC-012). |

Audit events are written **after** a successful entity write in the same API request. If the audit append fails, the failure is logged at Error and surfaced as a `audit-append-failed` structured event; the entity write is NOT rolled back (the audit container is an *additional* observability surface, not a transactional prerequisite — same trade-off as spec-004's `ChangeEvent` log).

**Audit vs telemetry — PII boundary**: Audit events are a **first-class user-facing governance record**, NOT telemetry. Constitution Principle IV and tech-stack §4 forbid PII in telemetry payloads by default ("Only correlation identifiers propagate unless an explicit opt-in is added by a future spec"). The `actor.displayName` field on audit events intentionally carries the operator's preferred-name claim (mild PII) because FR-032 mandates "actor identity" on every recorded write. This is permitted because audit storage:
- Lives in a dedicated Cosmos container (`registry-audit`), NOT in the App Insights / OTel telemetry pipeline.
- Is access-gated by the same authentication wall as the registry itself (FR-037).
- Is retained per audit-retention policy (TBD by a future ops-hardening spec), separately from telemetry retention (30d in LAW per spec-005 Q5c).
- Is surfaced to operators in the application UI by design (FR-033 audit panel on detail pages).

Telemetry emitted by registry code paths (OTel spans, App Insights events, structured logs) MUST continue to carry only correlation identifiers (`entityId`, `correlationId`/`traceparent`, principal `objectId`) and NOT the `displayName` or any other PII. The audit-vs-telemetry boundary is enforced by:
- `IAuditEventStore` writes go to Cosmos `registry-audit` only — never echoed into log/trace events.
- `IPlatformPrincipalAccessor` returns the principal OID for telemetry attribution; the `displayName` claim is read separately and used only at audit-write time.

---

## 6. AI Search index — `registry-entities-v1`

Per `contracts/search-index.json`. The index is the source of truth for full-text search and filter UX (FR-022, FR-023, FR-024); the canonical Cosmos container is the source of truth for browse and detail (FR-026, research §12).

### 6.1 Fields

| Index field | Type | searchable | filterable | sortable | facetable | retrievable | Notes |
|---|---|---|---|---|---|---|---|
| `id` | `Edm.String` (key) | ✗ | ✗ | ✗ | ✗ | ✅ | The registry entity's `id`. |
| `entityType` | `Edm.String` | ✗ | ✅ | ✗ | ✅ | ✅ | (FR-023) |
| `name` | `Edm.String` | ✅ | ✅ | ✅ | ✗ | ✅ | (FR-022, FR-023) |
| `fullyQualifiedName` | `Edm.String` | ✅ | ✗ | ✗ | ✗ | ✅ | (FR-022) |
| `description` | `Edm.String` | ✅ | ✗ | ✗ | ✗ | ✅ | (FR-022) |
| `owner` | `Edm.String` | ✅ | ✅ | ✗ | ✅ | ✅ | (FR-022, FR-023) |
| `environment` | `Edm.String` | ✗ | ✅ | ✗ | ✅ | ✅ | (FR-023, FR-035) |
| `status` | `Edm.String` | ✗ | ✅ | ✗ | ✅ | ✅ | (FR-023) |
| `namespaceName` | `Edm.String` | ✅ | ✅ | ✗ | ✗ | ✅ | (FR-022) |
| `azureResourceId` | `Edm.String` | ✅ | ✅ | ✗ | ✗ | ✅ | |
| `tags` | `Collection(Edm.ComplexType)` `{key, value}` | ✅ on `value` | ✅ on `key` and `value` | ✗ | ✅ on both | ✅ | (FR-022, FR-023 — value-only / key+value filter forms) |
| `tagKeysLower` | `Collection(Edm.String)` | ✗ | ✅ | ✗ | ✅ | ✗ | Lowercase-projected key set; supports case-insensitive key-only filter (research §9). |
| `metadataFlat` | `Collection(Edm.String)` | ✅ | ✗ | ✗ | ✗ | ✗ | Flattened `key=value` strings from `metadata`; provides discoverability of structured metadata via full-text search. (FR-022) |
| `parentId` | `Edm.String` | ✗ | ✅ | ✗ | ✗ | ✅ | Enables "find children of X" prefetch when needed. |
| `updatedAtUtc` | `Edm.DateTimeOffset` | ✗ | ✅ | ✅ | ✗ | ✅ | (FR-023 sort by updated time) |
| `createdAtUtc` | `Edm.DateTimeOffset` | ✗ | ✅ | ✅ | ✗ | ✅ | |
| `brokerKind` | `Edm.String` | ✗ | ✅ | ✗ | ✅ | ✅ | Reserved for future multi-broker support (Principle VI). Always `AzureServiceBus` in this slice. |

### 6.2 Analyzers and scoring

- Default analyzer: `en.microsoft` (handles plurals, possessives, common stopwords — appropriate for English-only v1 per spec.md Assumptions).
- No custom scoring profile in v1 (default BM25-like ranking is sufficient for SC-002 sub-1s search latency at the spec's scale).

### 6.3 Indexer pipeline contract

Cosmos document → `SearchDocumentMapper` projection → `MergeOrUploadDocumentsAsync`. Tombstone documents (`_isTombstone: true`) → `DeleteDocumentsAsync(documents.Select(t => t._tombstoneFor))`. See `contracts/indexer-events.md` for the full contract including poison-handler semantics.

---

## 7. Vocabulary alignment with spec 004

This table satisfies Constitution Principle III's cross-layer consistency requirement AND documents the spec-004 ↔ spec-006 divergences for a future "registry-domain unification" spec.

| Concept | Spec-006 (this slice) | Spec-004 (canonical domain) | Reconciliation note |
|---|---|---|---|
| Stable identifier | `id` (Guid string) | `Id` / `ResourceId` (Guid string) | Identical wire shape; types differ in name only. |
| Type discriminator | `entityType` (string enum) | `resourceType` (opaque string) | Different field name; different validation philosophy (spec-006 closed enum, spec-004 opaque). Reconciler maps `entityType` values to the corresponding `resourceType` values. |
| Logical resource name | `name` | `Name` (`ResourceName` value type, lowercase-hyphen) | **Diverges**: spec-006 preserves case and accepts Azure Service Bus naming rules (broader than spec-004). Reconciler picks the narrower spec-004 rules and tags spec-006 entities that don't comply. |
| Free-form display name | (none — `name` is case-preserved) | `DisplayName` (free-form) | Future reconciler: add `DisplayName` field, default to `Name`. |
| Parent navigation | `parentId` | `NamespacePath` (`/`-separated path) + `Relationships` (typed edges) | **Diverges**: spec-006 uses ID-based pointers; spec-004 uses path + typed edges. Reconciler walks `parentId` to derive `NamespacePath`. |
| Lifecycle | `status` ∈ { Active, Deprecated } | `Lifecycle` ∈ { Draft, Active, Deprecated, Retired, Archived } | **Diverges**: spec-006 has a flatter 2-state model. Reconciler maps spec-006 `Active` → `Active`, `Deprecated` → `Deprecated`; backfills missing `Lifecycle` to `Active`. |
| Soft vs hard delete | Hard delete (no `isDeleted` field) | Soft delete (`IsDeleted` + `Restored` change-event) | **Diverges**: hard delete in spec-006 means the historical record lives only in `registry-audit`. Reconciler must accept that pre-spec-006 deletions cannot be retroactively soft-deleted; future deletions reconciled into spec-004's soft-delete model. |
| Ownership | `owner` (free-form string) | `OwnershipRecord` (typed; references `Team` resource) | **Diverges**: spec-006 trades structure for friction. Reconciler: parse `owner` string as a Team slug match; on miss, create a `freeform-contact` OwnershipRecord shim. |
| Tags | `tags: [{key,value}]` (free-form) | `Tags: [TagReference]` (refs to `TagResource`) | **Diverges**: spec-006 inlines key/value; spec-004 references first-class tags. Reconciler: auto-create `TagResource` per unique key (using lowercase-key normalization). |
| Audit (latest state) | (not present; audit lives only in `registry-audit`) | `AuditRecord` (latest-state on the resource) | Reconciler: derive `AuditRecord` from the most recent `registry-audit` event per entity. |
| Audit (history) | `registry-audit` container | `change-events` container | **Diverges**: two parallel append-only logs. Reconciler: future spec migrates `registry-audit` events into `change-events` (or vice-versa). |
| Version | (not present) | `SemanticVersion` (Major.Minor.Patch + compatibility) | Reconciler: backfill `1.0.0 / Backward` for spec-006 entities. |
| Extension surface | `metadata` (opaque JSON) | `Extensions` (namespaced dictionary) + `__indexable` | Reconciler: hoist spec-006 `metadata` into `Extensions` under a `registry-slice:` namespace. |
| Indexable extensions | `metadataFlat` (search projection) | per-extension `__indexable` flag | Reconciler: project `metadataFlat` from `Extensions` only where `__indexable` is true. |
| Concurrency token | `_etag` (Cosmos-managed) | `ConcurrencyToken` (`_etag` Cosmos-managed) | **Aligned**. Both use Cosmos ETag. |
| Containers | `registry-entities`, `registry-audit` | `resources`, `change-events` | **Diverges**: two parallel container pairs on the same `canonical` database. |
| Partition key (entities) | `/environment` | `/resourceType` | **Diverges**: spec-006 partitions by environment for query isolation per spec.md; spec-004 partitions by resource type for co-location of same-type queries. |

Vocabulary outside this table is registry-only (no spec-004 equivalent) or spec-004-only (not adopted by this slice).

---

## 8. RBAC bindings consumed by this slice

| Identity | Resource | Role | Source |
|---|---|---|---|
| Workload UAMI (existing, spec 005) | `registry-entities` container | Cosmos DB Built-in Data Contributor (database-scope grant from spec 004 already covers new containers under `canonical`) | spec 004 RBAC; no new grant required |
| Workload UAMI | `registry-audit` container | Cosmos DB Built-in Data Contributor (inherited from database-scope) | spec 004 RBAC |
| Workload UAMI | AI Search service | Search Index Data Reader | NEW grant — spec 005's RBAC-Admin condition allowlist already permits the role GUID; this slice adds the assignment scoped to the service |
| Indexer Function (uses workload UAMI) | `registry-entities` container | Cosmos DB Built-in Data Contributor (inherited) | spec 004 RBAC |
| Indexer Function | `registry-entities-leases` container | Cosmos DB Built-in Data Contributor (inherited) | spec 004 RBAC |
| Indexer Function | AI Search service | Search Index Data Contributor | NEW grant — spec 005's allowlist permits the GUID; this slice adds the assignment scoped to the service |
| Indexer Function | App Insights | Monitoring Metrics Publisher | Existing spec-005 grant covers any workload UAMI; no change |

No new role GUIDs are added to the pipeline-MI RBAC-Admin condition allowlist (per [[project_spec005_bootstrap_gate]] memory: the bootstrap gate was cleared 2026-05-26 and permits all four spec-005 FR-033 GUIDs including both AI Search roles).

---

## 9. Naming Cross-Reference (Constitution Principle III verification)

| Concept | C# type/property | Persisted JSON | OpenAPI DTO | AI Search field | OTel attribute |
|---|---|---|---|---|---|
| Identifier | `IRegistryEntity.Id` / `Guid` | `id` | `id` | `id` (key) | `busterminal.registry.entity.id` |
| Type | `IRegistryEntity.EntityType` / `RegistryEntityType` | `entityType` | `entityType` | `entityType` | `busterminal.registry.entity.type` |
| Name | `IRegistryEntity.Name` | `name` | `name` | `name` | `busterminal.registry.entity.name` |
| FQN | `IRegistryEntity.FullyQualifiedName` | `fullyQualifiedName` | `fullyQualifiedName` | `fullyQualifiedName` | `busterminal.registry.entity.fqn` |
| Description | `IRegistryEntity.Description` | `description` | `description` | `description` | n/a |
| Tag | `RegistryTag.Key`/`.Value` | `tags[].key/value` | `tags[].key/value` | `tags/key, tags/value` + `tagKeysLower` | n/a |
| Owner | `IRegistryEntity.Owner` | `owner` | `owner` | `owner` | `busterminal.registry.entity.owner` |
| Environment | `IRegistryEntity.Environment` | `environment` | `environment` | `environment` | `busterminal.registry.entity.environment` |
| Status | `IRegistryEntity.Status` / `RegistryEntityStatus` | `status` | `status` | `status` | `busterminal.registry.entity.status` |
| CreatedAt | `IRegistryEntity.CreatedAtUtc` | `createdAtUtc` | `createdAtUtc` | `createdAtUtc` | n/a |
| UpdatedAt | `IRegistryEntity.UpdatedAtUtc` | `updatedAtUtc` | `updatedAtUtc` | `updatedAtUtc` | n/a |
| Source | `IRegistryEntity.Source` / `RegistrySource` | `source` | `source` | (not indexed) | `busterminal.registry.entity.source` |
| AzureResourceId | `IRegistryEntity.AzureResourceId` | `azureResourceId` | `azureResourceId` | `azureResourceId` | n/a |
| NamespaceName | `IRegistryEntity.NamespaceName` | `namespaceName` | `namespaceName` | `namespaceName` | n/a |
| Metadata | `IRegistryEntity.Metadata` | `metadata` | `metadata` | `metadataFlat` (projection) | n/a |
| ParentId | `IRegistryEntity.ParentId` | `parentId` | `parentId` | `parentId` | n/a |
| Etag | `IRegistryEntity.Etag` | `_etag` (Cosmos) | (HTTP `ETag` header) | n/a | n/a |
| Audit eventType | `AuditEvent.EventType` | `eventType` | `eventType` | n/a | `busterminal.registry.audit.eventType` |
| Audit actor | `AuditEvent.Actor.PrincipalId` | `actor.principalId` | `actor.principalId` | n/a | `busterminal.registry.audit.actor.principalId` |
| Audit force overwrite | `AuditEvent.WasForceOverwrite` | `wasForceOverwrite` | `wasForceOverwrite` | n/a | `busterminal.registry.audit.wasForceOverwrite` |
| Broker kind (reserved) | (constant `"AzureServiceBus"`) | n/a (not persisted yet) | n/a | `brokerKind` | n/a |

No vocabulary drift. Any future slice (relationships, governance, discovery) MUST use these names.

---

## 10. Forward compatibility notes

- **`brokerKind` reserved field in the search index** lets a future spec add Kafka / RabbitMQ entity types without re-indexing.
- **`metadata` opaque JSON** lets entity-type-specific properties accumulate without schema migration; AI Search projection (`metadataFlat`) makes them searchable.
- **`source` enum** lets a future automatic-discovery spec set `source: "Discovered"` without changing the wire shape.
- **`status: "Deleted"` reservation** lets a future soft-delete spec recover the slot.
- **`/environment` partition key** is the slice's deliberate trade-off vs hierarchical partitioning; a future scaling spec migrates to HPK if needed without changing the API surface.

Phase 1 §data-model complete.
