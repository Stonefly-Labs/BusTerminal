# Data Model — Core Domain Model (Phase 1)

**Plan**: [plan.md](./plan.md) · **Spec**: [spec.md](./spec.md) · **Research**: [research.md](./research.md)

This document captures the logical model for the canonical BusTerminal domain. The model is **persistence-and-API-agnostic at the domain layer** and **JSON-document-shaped at the persistence layer** (Cosmos DB). Naming is uniform across in-process types, persisted documents, JSON schemas in `contracts/`, telemetry attributes, and documentation (Constitution Principle III — verified in the Naming Cross-Reference table at the end of this document).

---

## Entity Catalog

### 1. Resource *(abstract base — every first-class entity inherits)*

The shared shape every first-class entity inherits. Implemented as an abstract C# record with sealed derived records per resource type (Resources/*.cs). FR-001.

| Field | Type | Notes |
|---|---|---|
| `Id` | `ResourceId` (Guid wrapper) | Globally unique, immutable. Never embeds environment names or Azure resource IDs. FR-021. |
| `ResourceType` | `string` (opaque) | The discriminator. Persisted as an opaque string (Q4 / FR-002); deserialization maps known values to typed records, unknown values to `UnknownResource`. |
| `Name` | `ResourceName` | Lowercase, hyphen-separated, no spaces (FR-022). Value type enforces the rules at construction. |
| `DisplayName` | `string` | Free-form; may contain spaces and casing. |
| `Description` | `string?` | Free-form. |
| `NamespacePath` | `NamespacePath` | `/` -separated hierarchical path (e.g., `enterprise/payments/order-processing`). FR-003. |
| `Environments` | `IReadOnlyCollection<EnvironmentClassification>` | Per-resource environment associations; minimum vocabulary { Development, Test, QA, Staging, Production, DisasterRecovery }; supports custom values. FR-017. Empty collection is valid for environment-agnostic resources (Team, Tag, Policy). |
| `Lifecycle` | `LifecycleState` | One of { Draft, Active, Deprecated, Retired, Archived }. FR-010 / Q1. |
| `Version` | `SemanticVersion` | Major/minor/patch + compatibility metadata + lineage references. FR-011. |
| `Ownership` | `OwnershipRecord?` | Required for operational resources; null permitted on Tag / Environment / Documentation Asset / Policy (the non-operational classifications). FR-009. Validated by `OwnershipPresenceRule`. |
| `Audit` | `AuditRecord` | Latest-state audit metadata. FR-015. |
| `Classification` | `ClassificationMetadata?` | Criticality, data sensitivity, compliance scope, availability tier, business domain, operational tier. FR-018. |
| `Tags` | `IReadOnlyCollection<TagReference>` | `TagReference` value types pointing at first-class `TagResource` entries by stable identifier. |
| `Extensions` | `Extensions` (dictionary) | Namespaced extension surface (`contoso:costCenter` etc.). Structured JSON values preserved via `JsonElement`. Per-extension indexing-inclusion control via the `__indexable` sibling key. FR-012. |
| `Documentation` | `IReadOnlyCollection<DocumentationReference>` | Linked runbooks, wikis, AsyncAPI specs, etc. FR-019. |
| `ValidationState` | `ValidationResult` | Persisted result of the last validation pass against this resource. FR-013. |
| `ConcurrencyToken` | `ConcurrencyToken` (string wrapper) | Cosmos ETag; opaque from the domain perspective. FR-025 / Q2. |
| `IsDeleted` | `bool` (default false) | Soft-delete predicate. FR-020. Live reads filter `IsDeleted = false` unless `includeDeleted: true` is passed. |

**Validation rules** that fire universally (in `ValidationEngine`):

- `RequiredFieldsRule`: `Id`, `ResourceType`, `Name`, `DisplayName`, `Lifecycle`, `Version`, `Audit` are all required. Severity: **Error**.
- `NamingStandardsRule`: `Name` matches the `ResourceName` regex. Severity: **Error**.
- `OwnershipPresenceRule`: `Ownership` is non-null for operational types (Broker, Queue, Topic, Subscription, MessageContract, ProducerApplication, ConsumerApplication, IntegrationFlow). Severity: **Error**.
- `UnknownResourceTypeRule`: emits an **Info** finding when the resource is materialized as `UnknownResource` (i.e., the persisted `ResourceType` is not in the known registry). Q4.

**Persisted JSON shape** (per `contracts/canonical-resource.schema.json`):

```json
{
  "id": "b0e0a3c1-1234-...-89ab",
  "resourceType": "queue",
  "name": "payments-orders-processing",
  "displayName": "Payments — Orders Processing",
  "description": "Primary queue for order processing events",
  "namespacePath": "enterprise/payments/order-processing",
  "environments": ["development", "test", "production"],
  "lifecycle": "active",
  "version": { "major": 1, "minor": 4, "patch": 0, "compatibility": "backward" },
  "ownership": { "owningTeamId": "...", "technicalContact": {...}, ... },
  "audit": { "createdBy": "...", "createdAt": "2026-05-20T12:34:56Z", ... },
  "classification": { ... },
  "tags": [{ "tagId": "...", "name": "payments" }],
  "extensions": { "contoso:costCenter": "FIN-102", "__indexable": { "contoso:costCenter": true } },
  "documentation": [...],
  "validationState": { ... },
  "isDeleted": false,
  "_etag": "\"00000000-0000-0000-1234-567890abcdef\""
}
```

The `_etag` field is Cosmos-managed and surfaces to the domain as `ConcurrencyToken`.

---

### 2. First-class resource types

Each derived record inherits `Resource` and adds **type-specific fields only** (no overrides of base fields). The 14 types are the closed framework registry from Q4. Per-type field expectations summarized below; full JSON Schemas in `contracts/resources/`.

#### 2.1 Namespace (`resourceType: "namespace"`)

| Field | Type | Notes |
|---|---|---|
| `ParentNamespaceId` | `ResourceId?` | Null at the root (e.g., `enterprise`). |
| `InheritedMetadata` | `InheritedMetadata` | Computed from parent chain by `NamespaceInheritance.cs`; not persisted directly. |

**Lifecycle**: standard (Draft → Active → Deprecated → Retired → Archived).

**Validation**: `DanglingReferenceRule` validates `ParentNamespaceId` resolves.

#### 2.2 Broker (`resourceType: "broker"`)

| Field | Type | Notes |
|---|---|---|
| `BrokerKind` | `string` | `"AzureServiceBus"` for v1; the field is open-string to anticipate Kafka, RabbitMQ, etc. (Constitution Principle VI). |
| `Endpoint` | `string?` | Logical reference to broker FQDN; **never** a connection string with credentials. |
| `Capabilities` | `BrokerCapabilities` | Flag set indicating queues/topics/sessions/transactions support. |

**Validation**: `RequiredFieldsRule` requires `BrokerKind`. No live broker connectivity is verified (broker runtime processing is out of scope per spec).

#### 2.3 Queue (`resourceType: "queue"`)

| Field | Type | Notes (FR-004) |
|---|---|---|
| `QueueKind` | `string` | `"AzureServiceBus"` for v1. |
| `DuplicateDetection` | `DuplicateDetectionPolicy?` | Window duration, enabled flag. |
| `RequiresSession` | `bool` | |
| `Ordering` | `OrderingPolicy` | FIFO / unordered. |
| `Partitioned` | `bool` | |
| `DeadLetterBehavior` | `DeadLetterPolicy` | Max delivery count, expiration handling. |
| `Ttl` | `TimeSpan?` | Default message TTL. |
| `MaxMessageSizeBytes` | `long?` | |
| `ContractAssociations` | `IReadOnlyCollection<ContractReference>` | Contracts this queue carries. Resolved by `DanglingReferenceRule`. |
| `Producers` | `IReadOnlyCollection<ApplicationReference>` | Producer apps that publish here. |
| `Consumers` | `IReadOnlyCollection<ApplicationReference>` | Consumer apps that consume here. |
| `OperationalMetadata` | `OperationalMetadata?` | Free-form structured operational notes. |
| `Deprecation` | `DeprecationMetadata?` | Scheduled retirement date, replacement reference. |

#### 2.4 Topic (`resourceType: "topic"`)

| Field | Type | Notes (FR-005) |
|---|---|---|
| `Ordering` | `OrderingPolicy` | |
| `Partitioned` | `bool` | |
| `SubscriptionIds` | `IReadOnlyCollection<ResourceId>` | Backward-pointer to subscriptions for query convenience. (Relationships are still the authoritative source.) |
| `ContractAssociations` | `IReadOnlyCollection<ContractReference>` | |
| `Producers` | `IReadOnlyCollection<ApplicationReference>` | |
| `Governance` | `GovernanceMetadata?` | Policy references. |

#### 2.5 Subscription (`resourceType: "subscription"`)

| Field | Type | Notes (FR-006) |
|---|---|---|
| `ParentTopicId` | `ResourceId` | Required. Resolved by `DanglingReferenceRule`. |
| `Filter` | `FilterDefinition?` | SQL filter, correlation filter, or boolean filter. |
| `Rule` | `RuleDefinition?` | |
| `Consumers` | `IReadOnlyCollection<ApplicationReference>` | |
| `DeliverySemantics` | `DeliverySemantics` | At-least-once / at-most-once. |
| `DeadLetter` | `DeadLetterPolicy` | |
| `Retry` | `RetryPolicy` | |
| `OperationalMetadata` | `OperationalMetadata?` | |

#### 2.6 Message Contract (`resourceType: "messageContract"`)

| Field | Type | Notes (FR-007) |
|---|---|---|
| `Format` | `ContractFormat` | One of { JsonSchema, Avro, Protobuf, XmlSchema, CloudEvents, Custom }. |
| `SchemaReference` | `SchemaReference` | Inline schema string OR external URI. Exactly one of the two. |
| `ExamplePayloads` | `IReadOnlyCollection<ExamplePayload>` | |
| `Compatibility` | `CompatibilityMetadata` | One of { Backward, Forward, Full, None }. |
| `Producers` | `IReadOnlyCollection<ApplicationReference>` | |
| `Consumers` | `IReadOnlyCollection<ApplicationReference>` | |
| `DeprecationStatus` | `DeprecationStatus?` | Per-version (a Contract resource carries multiple versions in `Version.VersionHistory`). |
| `ValidationMetadata` | `ContractValidationMetadata?` | Records when external validation last ran (deferred pluggable validators are a future slice). |

**Note on contract-version lifecycle**: A Contract resource has a top-level `Lifecycle` (governing the contract as a whole) AND a per-version `DeprecationStatus` field in `Version.VersionHistory`. This satisfies the spec's Edge Case "Lifecycle of a contract version."

#### 2.7 Producer Application (`resourceType: "producerApplication"`)

| Field | Type | Notes |
|---|---|---|
| `ApplicationKind` | `string` | E.g., `"WebService"`, `"BatchJob"`, `"AzureFunction"`. Open string. |
| `Repository` | `string?` | Logical repo reference. |
| `OnCallReference` | `string?` | Free-form on-call rotation link. |

#### 2.8 Consumer Application (`resourceType: "consumerApplication"`)

| Field | Type | Notes |
|---|---|---|
| `ApplicationKind` | `string` | |
| `Repository` | `string?` | |
| `OnCallReference` | `string?` | |

#### 2.9 Team (`resourceType: "team"`)

| Field | Type | Notes (FR-009 referent) |
|---|---|---|
| `Slug` | `string` | Stable shortname (e.g., `payments-platform`). |
| `EntraGroupId` | `Guid?` | Reference to an Entra group; **populated by a future slice** that integrates Microsoft Graph. Optional until then. |
| `ContactEmail` | `string?` | |
| `OperationalTier` | `OperationalTier?` | Tier-1 / Tier-2 / Tier-3 / Best-Effort. |

**Lifecycle**: standard. Ownership of Team itself is null (Teams are owned organizationally, not by another Team).

#### 2.10 Environment (`resourceType: "environment"`)

| Field | Type | Notes (FR-017) |
|---|---|---|
| `Classification` | `EnvironmentClassification` | Reference to the classification vocabulary; this resource type lets organizations *register* environments with metadata (description, region, compliance scope) beyond the bare classification name. |
| `Region` | `string?` | Azure region for the environment (e.g., `eastus2`). |
| `ComplianceScope` | `string?` | E.g., `"PCI"`, `"HIPAA"`. |

#### 2.11 Tag (`resourceType: "tag"`)

| Field | Type | Notes |
|---|---|---|
| `Category` | `string?` | Optional grouping (e.g., `"team"`, `"environment"`, `"sla"`). |
| `Color` | `string?` | UI hint for future renderers. |

#### 2.12 Policy (`resourceType: "policy"`)

| Field | Type | Notes |
|---|---|---|
| `PolicyKind` | `string` | E.g., `"Retention"`, `"DataResidency"`, `"AccessControl"`. |
| `RuleBody` | `string` | Opaque (policy-engine-specific); rendered as a JSON document. Pluggable policy engines are a future slice. |
| `Scope` | `PolicyScope` | Namespace / resource type / resource instance. |

#### 2.13 Integration Flow (`resourceType: "integrationFlow"`)

| Field | Type | Notes |
|---|---|---|
| `ProducerApplicationId` | `ResourceId` | Required; resolved by `DanglingReferenceRule`. |
| `MessagingResourceId` | `ResourceId` | Queue or Topic. Required; resolved by `DanglingReferenceRule`. |
| `ConsumerApplicationIds` | `IReadOnlyCollection<ResourceId>` | One or more. |
| `BusinessPurpose` | `string?` | Free-form business-flow description. |

#### 2.14 Documentation Asset (`resourceType: "documentationAsset"`)

| Field | Type | Notes (FR-019) |
|---|---|---|
| `AssetKind` | `DocumentationAssetKind` | { Runbook, Wiki, AsyncApiSpec, ArchitectureDiagram, OperationalGuide, ExternalRepository }. |
| `Uri` | `string` | External link. |
| `AttachedResourceIds` | `IReadOnlyCollection<ResourceId>` | Resources this asset documents. |

#### 2.15 UnknownResource (placeholder — not in the framework registry)

Materialized when persisted `resourceType` is **not** in the known registry. Carries the raw document body in a `RawJson` property for diagnostic surfacing. Emits an `UnknownResourceTypeRule` Info finding (Q4). Does **not** have type-specific validation rules; per-type rules execute only against known types.

---

### 3. Relationship *(first-class edge document)*

Explicit, directional, typed edges between resources. FR-008. Persisted in the canonical `resources` container as a discriminated document (`resourceType: "relationship"` — but unlike resource-type discriminators above, `Relationship` is *not* a `Resource` subtype; it's a peer entity with its own JSON Schema and partition strategy). Stored with `/resourceType` partition key (so all relationships co-locate).

| Field | Type | Notes |
|---|---|---|
| `Id` | `ResourceId` | The edge's own identifier. |
| `SourceId` | `ResourceId` | |
| `TargetId` | `ResourceId` | |
| `Type` | `RelationshipType` (enum) | See `contracts/relationship-types.md`. Closed enum in v1 (extension governed by future slice). |
| `Annotations` | `IReadOnlyDictionary<string, JsonElement>` | Optional metadata on the edge. |
| `Audit` | `AuditRecord` | Latest-state audit metadata. |
| `IsDeleted` | `bool` | Soft-delete predicate; relationships persist through resource soft-deletion (FR-020 + Edge Case "Dangling references after soft-delete"). |

**Relationship-type vocabulary** (closed enum; full table in `contracts/relationship-types.md`):

| Type | Source → Target | Example |
|---|---|---|
| `publishesTo` | ProducerApplication → Queue / Topic | `orders-api` publishes to `payments.events` |
| `consumedBy` | Queue / Subscription → ConsumerApplication | `subscription:risk-scoring` consumed by `risk-engine` |
| `subscriptionOf` | Subscription → Topic | (parent-child within topics) |
| `usesContract` | Queue / Topic → MessageContract | |
| `owns` | Team → Resource | (any operational resource) |
| `attachedTo` | DocumentationAsset → Resource | |
| `replaces` | Resource → Resource | Successor pattern after Retire / Archive (Q1). |
| `partOfFlow` | Resource → IntegrationFlow | |

**Validation**:

- `DanglingReferenceRule`: `SourceId` and `TargetId` must resolve to existing resources (including soft-deleted; relationships to soft-deleted targets are surfaced with a Warning finding, not blocked, per Edge Case "Dangling references after soft-delete").
- Type-specific source/target type validation: e.g., `publishesTo` source must be a Producer Application, target must be a Queue or Topic. Severity: **Error**.

---

### 4. OwnershipRecord *(reusable embedded block)*

| Field | Type | Notes (FR-009) |
|---|---|---|
| `OwningTeamId` | `ResourceId` | Resolved by `DanglingReferenceRule` and `OwnershipPresenceRule`. |
| `TechnicalContact` | `ContactReference?` | Entra principal reference (placeholder field for the slice that integrates Graph). |
| `BusinessContact` | `ContactReference?` | |
| `EscalationReference` | `string?` | Free-form URL or contact identifier. |
| `SupportReference` | `string?` | Free-form URL or contact identifier. |
| `OperationalTier` | `OperationalTier` | Tier-1 / Tier-2 / Tier-3 / Best-Effort. |

**Forward compatibility note**: `ContactReference` is shaped to accept either a free-form string today OR an Entra ID object reference once the Microsoft Graph integration slice ships. The persisted JSON shape carries `{ "kind": "entra", "objectId": "..." }` or `{ "kind": "freeform", "value": "..." }` so a future slice can promote freeform references in place without a schema break.

---

### 5. AuditRecord *(reusable embedded block — latest state)*

Per Q5: latest-state audit on the resource document; full change history lives in the separate `ChangeEvent` log (entity 6).

| Field | Type | Notes (FR-015 latest-state) |
|---|---|---|
| `CreatedBy` | `PrincipalReference` | The actor that created the resource. `PrincipalReference` carries `kind: human / workload / system` plus the appropriate identifier. |
| `CreatedAt` | `DateTimeOffset` | UTC. |
| `ModifiedBy` | `PrincipalReference` | The actor that last modified the resource. |
| `ModifiedAt` | `DateTimeOffset` | UTC. |
| `SourceSystem` | `string?` | Where the change originated (e.g., `"manual-ui"`, `"sync-worker"`, `"import-job"`). |
| `Synchronization` | `SyncMetadata?` | Optional. Carries upstream system identifiers (for resources sourced from an external system in a future slice). |

---

### 6. ChangeEvent *(separate immutable log entry — Q5)*

Persisted in the `change-events` Cosmos container, partitioned by `/resourceId`. One document per state change. Never updated, never deleted (except via container-level TTL in a future operational slice).

| Field | Type | Notes (FR-015 + Q5) |
|---|---|---|
| `Id` | `Guid` | Event identifier. |
| `ResourceId` | `ResourceId` | Subject of the change. Partition key. |
| `ResourceType` | `string` | Mirror of the resource's type at the time of the event (frozen — does not migrate if the type is renamed in the future). |
| `EventType` | `ChangeEventType` | One of { `Created`, `Updated`, `LifecycleTransitioned`, `SoftDeleted`, `Restored` }. |
| `Actor` | `PrincipalReference` | Who/what initiated the change. |
| `Timestamp` | `DateTimeOffset` | UTC. |
| `SourceSystem` | `string?` | Same vocabulary as `AuditRecord.SourceSystem`. |
| `ConcurrencyTokenBefore` | `ConcurrencyToken?` | The ETag observed on read (null on Created). |
| `ConcurrencyTokenAfter` | `ConcurrencyToken` | The ETag stamped by Cosmos on write. |
| `LifecycleBefore` | `LifecycleState?` | For `LifecycleTransitioned` events. |
| `LifecycleAfter` | `LifecycleState?` | For `LifecycleTransitioned` events. |
| `Diff` | `JsonElement?` | Structured JSON Patch (RFC 6902) of the fields that changed on `Updated` events. Null for non-update events. |
| `Snapshot` | `JsonElement?` | Full snapshot of the resource as of the event. Always present for `Created`, optional otherwise (we store snapshots on every event in v1 — sub-KB documents make the storage cost trivial; a future operational slice may switch to diff-only for non-create events). |

**Lifecycle**: append-only. The change-event container is intentionally never updated.

**Validation**: none at write time (events are emitted by the persistence layer, not by user input). Query-time integrity (e.g., the events for a resource form a valid sequence) is verified by `ChangeEventLogIntegrationTests`.

---

### 7. SemanticVersion *(reusable embedded block)*

| Field | Type | Notes (FR-011) |
|---|---|---|
| `Major` | `int` | |
| `Minor` | `int` | |
| `Patch` | `int` | |
| `Compatibility` | `CompatibilityIndicator` | { Backward, Forward, Full, None }. |
| `CurrentVersionRef` | `SemanticVersionRef?` | For contract resources, points to the currently-active version when this version is historical. |
| `VersionHistory` | `IReadOnlyCollection<HistoricalVersionEntry>?` | Lineage of prior versions with their deprecation status (per-version, satisfies Edge Case "Lifecycle of a contract version"). |

---

### 8. ValidationResult *(reusable embedded block)*

| Field | Type | Notes (FR-013 + Q3) |
|---|---|---|
| `EvaluatedAt` | `DateTimeOffset` | UTC. |
| `Findings` | `IReadOnlyCollection<ValidationFinding>` | |
| `OverallSeverity` | `ValidationSeverity` | Max severity across findings. |

**`ValidationFinding`**:

| Field | Type | Notes |
|---|---|---|
| `RuleId` | `string` | Stable identifier (e.g., `"ownership.required"`). |
| `Severity` | `ValidationSeverity` | { Error, Warning, Info }. |
| `Message` | `string` | Human-readable. |
| `FieldRef` | `string?` | JSON Pointer to the offending field, when applicable. |
| `RelationshipRef` | `ResourceId?` | The offending relationship, when applicable. |
| `EvaluatedAt` | `DateTimeOffset` | UTC. |

**Write semantics** (Q3 + FR-013): writes are atomic per resource. If any `Error` finding fires, the write is rejected entirely. `Warning` and `Info` findings are persisted on the resource without blocking the write.

---

### 9. Extensions *(reusable embedded block)*

A dictionary keyed by namespaced extension keys (`<vendor>:<key>`). Values are arbitrary structured JSON preserved as `JsonElement`. FR-012.

| Field | Type | Notes |
|---|---|---|
| `(arbitrary namespaced key)` | `JsonElement` | Vendor-prefixed, structured value. |
| `__indexable` | `IReadOnlyDictionary<string, bool>?` | Per-extension indexing inclusion control. `false` excludes the extension from search projections. |

**Validation**: `RequiredFieldsRule` does **not** require any extension. Custom validation per extension is out of scope for this slice (future per-vendor validators).

**Indexing**: the persistence layer instructs Cosmos to exclude `/extensions/*` from indexing by default; the search-projection layer (future slice) consults `__indexable` per extension.

---

## State Transitions

### Lifecycle Transitions (Q1)

**Legal-transition graph** (implemented in `LifecycleTransitions.cs`):

```text
   ┌─────────┐
   │  Draft  │◄─┐    (free edits while Draft)
   └────┬────┘  │
        │       │
        ▼       │
   ┌─────────┐  │
   │ Active  │──┘    (illegal: Active → Draft)
   └────┬────┘
        │
        ▼
   ┌──────────┐
   │Deprecated│◄─┐   (un-deprecate permitted)
   └────┬─────┘  │
        │  ▲     │
        │  └─────┘
        ▼
   ┌─────────┐
   │ Retired │       (terminal: no backward transitions)
   └────┬────┘
        │
        ▼
   ┌─────────┐
   │Archived │       (terminal: no backward transitions)
   └─────────┘
```

`IsTransitionLegal(from, to)` returns true only for the edges shown above. All other transitions are rejected by `LifecycleTransitionRule` with severity **Error**. Replacement of Retired/Archived resources requires a successor (new resource with its own id, optionally linked via `replaces` relationship). Restoration from soft-delete bypasses these rules and returns the resource to its prior state.

### Concurrency (Q2)

Every write specifies the `IfMatch` precondition with the resource's current `ConcurrencyToken`. The Cosmos server rejects stale writes with `412 PreconditionFailed`; `ConcurrencyExceptionMapper` translates this to `ConcurrencyConflictException(resourceId, presentedToken, currentToken)`. Callers are expected to re-read and retry.

### Change-event log emission (Q5)

The `ICanonicalResourceStore` implementation appends to `IChangeEventLog` immediately after every successful write (Cosmos transactional batch is **not** used in v1 because the two containers are independent; both writes are issued sequentially with idempotent retry on transient failure of the change-event append — see research §10 trade-off). Failure to append the change event is logged at Error and surfaced; the canonical resource is **not rolled back** because the change-event log is an *additional* observability surface, not a transactional prerequisite. This is documented behavior, and a future operational slice may upgrade to Cosmos's transactional batch within a single account if both containers move under the same database (which they already are in v1; transactional batches require same partition key value, which they don't share, so true cross-container atomicity isn't available in Cosmos).

---

## Naming Cross-Reference

**Constitution Principle III** requires domain terminology to remain consistent across APIs, UI, documentation, database models, search indexes, and telemetry. This slice is the source of truth for that vocabulary. The table below proves consistency across every artifact this slice produces.

| Concept | In-process C# type | Persisted JSON field | JSON Schema name | Relationship vocab | Telemetry attribute |
|---|---|---|---|---|---|
| Resource identifier | `ResourceId` | `id` | `resource.schema.json#/properties/id` | (used as source/target) | `busterminal.resource.id` |
| Resource type discriminator | `ResourceType` (string) | `resourceType` | `canonical-resource.schema.json#/properties/resourceType` | n/a | `busterminal.resource.type` |
| Namespace path | `NamespacePath` | `namespacePath` | `canonical-resource.schema.json#/properties/namespacePath` | n/a | `busterminal.resource.namespace` |
| Lifecycle | `LifecycleState` | `lifecycle` | `canonical-resource.schema.json#/properties/lifecycle` | n/a | `busterminal.resource.lifecycle` |
| Concurrency token | `ConcurrencyToken` | `_etag` (Cosmos-managed) | `canonical-resource.schema.json#/properties/_etag` | n/a | `busterminal.resource.etag` (never logged in body) |
| Ownership | `OwnershipRecord` | `ownership` | `ownership.schema.json` | (target of `owns` edge) | `busterminal.resource.ownership.team_id` |
| Audit (latest) | `AuditRecord` | `audit` | `audit.schema.json` | n/a | `busterminal.audit.modified_by`, `busterminal.audit.modified_at` |
| Change event | `ChangeEvent` | (separate container) | `change-event.schema.json` | n/a | `busterminal.changeevent.type` |
| Validation finding | `ValidationFinding` | `validationState.findings[]` | `validation-result.schema.json#/$defs/finding` | n/a | `busterminal.validation.finding_count_error`, `..._warning`, `..._info` |
| Extension | `Extensions` (dict) | `extensions` | `extension.schema.json` | n/a | not emitted by default |
| Relationship | `Relationship` | (peer document) | `relationship.schema.json` | n/a | `busterminal.relationship.type` |
| Producer application | `ProducerApplication` | `resourceType: "producerApplication"` | `resources/producer-application.schema.json` | source of `publishesTo` | `busterminal.resource.type=producerApplication` |
| Consumer application | `ConsumerApplication` | `resourceType: "consumerApplication"` | `resources/consumer-application.schema.json` | target of `consumedBy` | `busterminal.resource.type=consumerApplication` |
| Queue | `Queue` | `resourceType: "queue"` | `resources/queue.schema.json` | target of `publishesTo`, source of `consumedBy`, source of `usesContract` | `busterminal.resource.type=queue` |
| Topic | `Topic` | `resourceType: "topic"` | `resources/topic.schema.json` | target of `publishesTo`, target of `subscriptionOf` (from subs), source of `usesContract` | `busterminal.resource.type=topic` |
| Subscription | `Subscription` | `resourceType: "subscription"` | `resources/subscription.schema.json` | source of `subscriptionOf`, source of `consumedBy` | `busterminal.resource.type=subscription` |
| Message contract | `MessageContract` | `resourceType: "messageContract"` | `resources/message-contract.schema.json` | target of `usesContract` | `busterminal.resource.type=messageContract` |
| Team | `Team` | `resourceType: "team"` | `resources/team.schema.json` | source of `owns` | `busterminal.resource.type=team` |
| Environment | `EnvironmentClassification` (enum) + `EnvironmentResource` (type) | `environments[]` + `resourceType: "environment"` | `resources/environment.schema.json` | n/a | `busterminal.resource.environment[]` |
| Integration flow | `IntegrationFlow` | `resourceType: "integrationFlow"` | `resources/integration-flow.schema.json` | source of `partOfFlow` | `busterminal.resource.type=integrationFlow` |

No vocabulary drift. Any future slice (search projection, API, UI, documentation) MUST use the names in column 4 or 3 — never invent synonyms.
