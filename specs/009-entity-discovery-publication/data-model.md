# Phase 1 Data Model: Entity Discovery and Publication

**Spec**: [spec.md](./spec.md) · **Plan**: [plan.md](./plan.md) · **Research**: [research.md](./research.md) · **Date**: 2026-06-17

This document specifies the Cosmos DB document shapes, the AI Search index extensions, and the supporting in-memory domain types introduced by Spec 009. All shapes are versioned via a top-level `schemaVersion` field to enable backward-compatible evolution.

---

## 1. Cosmos containers

### 1.1 `registry-entities` (EXTEND existing container)

Existing container; PK `/environment`; established by Spec 006. Spec 009 adds three new top-level fields and a single nested sub-document, all backward-compatible (every new field is nullable / defaults safely for legacy documents).

**Full PublishedEntity shape (Queue example)**:

```jsonc
{
  // ── Spec 006 fields (existing; unchanged) ──────────────────────────────
  "id": "pe_2HJZW6XQGKLM8YN5R4P7S9TB",        // see R-07 identity scheme
  "schemaVersion": "1.1",                       // bumped from 1.0 by spec 009
  "entityType": "Queue",                        // Queue | Topic | Subscription | Rule
  "environment": "dev",                         // PK
  "namespaceId": "ns_01HKXP...",                // FK → registered namespace
  "name": "orders-inbox",
  "displayName": "orders-inbox",
  "compositeKey": "q:ns_01HKXP.../orders-inbox", // human-debug; indexed
  "parentEntityId": null,                       // null for queues/topics; populated for subscriptions/rules

  // ── Spec 006 curated metadata (existing; preserved verbatim by discovery) ──
  "description": "Orders received from the storefront, ready for fulfillment processing.",
  "businessPurpose": "Bridges checkout API to fulfillment worker pool.",
  "tags": ["domain:orders", "tier:critical"],
  "documentationLinks": [
    { "label": "Runbook", "url": "https://wiki.example.com/orders-inbox" }
  ],
  "contactInformation": {
    "primaryContact": "fulfillment-team@example.com",
    "escalationPath": "https://example.pagerduty.com/orders"
  },
  "operationalNotes": "Drains via fulfillment-worker-job (Spec 005 baseline).",

  // ── Spec 009 NEW: lifecycle status ─────────────────────────────────────
  "lifecycleStatus": "Active",                  // Active | Missing | Archived
  "lifecycleStatusChangedUtc": "2026-06-17T14:32:18.444Z",

  // ── Spec 009 NEW: discovery audit trail ───────────────────────────────
  "firstDiscoveredUtc": "2026-06-15T09:01:11.100Z",
  "lastSeenUtc": "2026-06-17T14:32:14.821Z",
  "lastDiscoveryRunId": "dr_01HZAB...",

  // ── Spec 009 NEW: Azure-sourced technical attributes ──────────────────
  "azureSourced": {
    "azureResourceId": "/subscriptions/.../namespaces/myns/queues/orders-inbox",
    "armEtag": "W/\"datetime'2026-06-17T14%3A32%3A14.0000000Z'\"",  // captured for diagnostics
    "status": "Active",                         // Azure-side status (different field from lifecycleStatus)
    "lockDuration": "PT1M",                     // ISO-8601 duration
    "maxDeliveryCount": 10,
    "duplicateDetection": { "enabled": true, "historyTimeWindow": "PT10M" },
    "deadLettering": { "deadLetterOnMessageExpiration": true },
    "partitioning": { "enabled": false },
    "session": { "enabled": false },
    "forwarding": { "forwardTo": null, "forwardDeadLetteredMessagesTo": null },
    "defaultTimeToLive": "P14D",
    "maxSizeInMegabytes": 5120
  },
  "azureSourcedHash": "sha256:9af2e1...c4b",    // hash over canonical-serialized azureSourced — see R-08

  // ── Spec 009 NEW: service associations (denormalized; see R-09) ───────
  "serviceAssociations": [
    {
      "associationId": "esa_01HZ...",            // ULID; used for DELETE /associations/{id}
      "serviceId": "svc_01HKY...",
      "role": "Owner",                          // Owner | Producer | Consumer
      "createdUtc": "2026-06-17T14:35:01.000Z",
      "createdBy": "00000000-1111-2222-3333-444444444444"
    },
    {
      "associationId": "esa_01HZ...",
      "serviceId": "svc_02HKY...",
      "role": "Consumer",
      "createdUtc": "2026-06-17T14:36:11.500Z",
      "createdBy": "00000000-1111-2222-3333-444444444444"
    }
  ],
  // Derived projection arrays (kept in sync with serviceAssociations[] for AI Search filtering):
  "associatedServiceIds": ["svc_01HKY...", "svc_02HKY..."],
  "associationRoles": ["Owner", "Consumer"],

  // ── Existing audit & concurrency control ───────────────────────────────
  "_etag": "\"af00...\"",                       // Cosmos ETag (auto)
  "_ts": 1750159938,
  "createdUtc": "2026-06-15T09:01:11.100Z",
  "createdBy": "00000000-...",
  "lastModifiedUtc": "2026-06-17T14:35:01.000Z",
  "lastModifiedBy": "00000000-..."
}
```

**Shape variants** by `entityType`:

| Field | Queue | Topic | Subscription | Rule |
|---|---|---|---|---|
| `parentEntityId` | null | null | id of parent Topic | id of parent Subscription |
| `azureSourced.lockDuration` | ✓ | — | ✓ | — |
| `azureSourced.maxDeliveryCount` | ✓ | — | ✓ | — |
| `azureSourced.duplicateDetection` | ✓ | ✓ | — | — |
| `azureSourced.deadLettering` | ✓ | — | ✓ | — |
| `azureSourced.partitioning` | ✓ | ✓ | — | — |
| `azureSourced.session` | ✓ | — | ✓ | — |
| `azureSourced.forwarding` | ✓ | — | ✓ | — |
| `azureSourced.defaultTimeToLive` | ✓ | ✓ | ✓ | — |
| `azureSourced.maxSizeInMegabytes` | ✓ | ✓ | — | — |
| `azureSourced.filterType` | — | — | — | ✓ — `Sql` \| `Correlation` \| `True` \| `False` |
| `azureSourced.filterExpression` | — | — | — | ✓ (nullable per edge case) |
| `azureSourced.actionExpression` | — | — | — | ✓ (nullable) |

**Validation rules** (enforced in domain layer; surface via FluentValidation on inputs):

- `lifecycleStatus` ∈ `{ Active, Missing, Archived }` (enum constraint).
- `serviceAssociations[*].role` ∈ `{ Owner, Producer, Consumer }`.
- For a given entity, at most one `serviceAssociation` per `(serviceId, role)` triple (uniqueness validated on POST `/associations`).
- `compositeKey` MUST match the regex `^[qtsr]:[^/]+(/[^/]+){0,3}$`.
- `azureSourcedHash` MUST be present whenever `azureSourced` is present and MUST be valid base64-encoded SHA-256 prefixed with `sha256:`.
- `firstDiscoveredUtc` MUST be `<= lastSeenUtc` (sanity invariant).
- `parentEntityId` MUST resolve to an existing PublishedEntity in the same namespace whose `entityType` matches the hierarchy expectation (Subscription → Topic; Rule → Subscription).

**State transitions** for `lifecycleStatus`:

```text
       discovery sees entity                   manual archive
new ────────────────────────► Active ──────────────────────────► Archived
                                ▲                                   │
                                │ discovery sees entity             │ manual unarchive
                                │                                   ▼
                            ┌───────┐                            Active
                            │       │
                            │       │ discovery does NOT see entity
                            │       ▼
                            └─── Missing
                                  │ discovery sees entity (automatic)
                                  └──────────────────────────► Active
```

Per FR-015, once `Archived` is set by a user action, subsequent discovery observations do **not** auto-revert it.

---

### 1.2 `discovery-runs` (NEW container)

**Container**: `discovery-runs` · **PK**: `/namespaceId` · **Indexing**: include `id`, `namespaceId`, `status`, `startedUtc` (sortable); default exclude path everything else. Append-only after terminal status.

```jsonc
{
  "id": "dr_01HZAB7VMQ...",
  "schemaVersion": "1.0",
  "namespaceId": "ns_01HKXP...",                // PK
  "status": "Succeeded",                        // Queued | InProgress | Succeeded | Failed
  "trigger": "Manual",                          // v1: always "Manual"; future: "Scheduled" | "Webhook"
  "startedUtc": "2026-06-17T14:32:11.123Z",
  "completedUtc": "2026-06-17T14:34:48.901Z",
  "durationMs": 157778,
  "requestedBy": "00000000-1111-2222-3333-444444444444",   // Entra object ID

  // Counts by entity type (sum is the total entities the run observed in Azure):
  "queueCount": 3,
  "topicCount": 2,
  "subscriptionCount": 4,
  "ruleCount": 6,

  // Counts by classification outcome:
  "newCount": 2,
  "updatedCount": 1,
  "unchangedCount": 12,
  "missingCount": 0,

  // Failure detail (populated only when status = Failed):
  "failure": null,                              // or { category, message, occurredAtPhase, retriesExhausted }
  //  category ∈ { Authn, Authz, NotFound, Throttled, Transport, Internal, Unknown }
  //  occurredAtPhase ∈ { LockAcquire, FetchQueues, FetchTopics, FetchSubscriptions, FetchRules, Persist, ResultWrite }
  //  message — operator-safe (no Azure PII; no entity payloads); falls back to "(redacted)" if a guard trips

  // Coalescing audit (recorded if FR-003 coalescing kicked in):
  "coalescedRequests": [
    { "requestedUtc": "2026-06-17T14:32:42.001Z", "requestedBy": "00000000-..." }
  ],

  "correlationId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"  // W3C traceparent
}
```

**Validation**:
- `status` enum constraint.
- `completedUtc >= startedUtc` when present.
- Counts ≥ 0.
- `failure.message` truncated server-side at 2 KB; longer messages stored as `(truncated; see telemetry traceId=<id>)`.

**Retention**: indefinite for v1 (per spec assumption). Future spec may add a TTL.

---

### 1.3 `discovery-locks` (NEW container)

**Container**: `discovery-locks` · **PK**: `/namespaceId` · **Indexing**: minimal (only `id`, `namespaceId`). One document per registered namespace, created lazily on first discovery attempt.

```jsonc
{
  "id": "lock",                                 // deterministic — one per partition
  "schemaVersion": "1.0",
  "namespaceId": "ns_01HKXP...",                // PK
  "currentRunId": "dr_01HZAB...",               // null when no run is in flight
  "acquiredUtc": "2026-06-17T14:32:11.123Z",
  "acquiredByPodId": "indexer-revision-5-abc12",
  "expectedReleaseByUtc": "2026-06-17T14:37:11.123Z",  // acquiredUtc + 5min (the SC-005 ceiling)
  "_etag": "\"af00...\""                        // used for IfMatch acquisition
}
```

**Acquisition algorithm** (atomic):
1. Try `ReadItemAsync("lock", new PartitionKey(namespaceId))`.
   - Not found → `CreateItemAsync` with `currentRunId = newRunId`. Success = lock acquired.
2. Found and `currentRunId == null` → `ReplaceItemAsync` with `IfMatch = readETag`. Success = lock acquired.
3. Found and `currentRunId != null` and `expectedReleaseByUtc > now()` → **coalesce**: return the existing `currentRunId` to the API as the "new" run (FR-003).
4. Found and `currentRunId != null` and `expectedReleaseByUtc <= now()` → **steal**: write `ReplaceItemAsync` with `IfMatch = readETag` setting `currentRunId = newRunId`. Also write a side-effect to the orphaned DiscoveryRun: `status = Failed, failure.category = WorkerLost, completedUtc = now()`.

**Release algorithm**: worker, on terminal status of the DiscoveryRun, executes `ReplaceItemAsync` setting `currentRunId = null, acquiredByPodId = null, expectedReleaseByUtc = null`. No `IfMatch` (we're the legitimate holder).

---

## 2. AI Search index extensions

### 2.1 `registry-entities-v1` (EXTEND existing schema)

The existing index (defined in `iac/modules/ai-search-registry-index/`) gains four new fields. Additions are backward-compatible — legacy documents missing these fields are simply absent from the corresponding filters.

| Field | Type | Searchable | Filterable | Facetable | Sortable | Notes |
|---|---|---|---|---|---|---|
| `lifecycleStatus` | `Edm.String` | — | ✓ | ✓ | — | Discoverable in UI as a filter chip set. |
| `associatedServiceIds` | `Collection(Edm.String)` | — | ✓ | ✓ | — | Powers "show entities for service X" filter. |
| `associationRoles` | `Collection(Edm.String)` | — | ✓ | ✓ | — | Powers role narrowing inside the service filter. |
| `azureSourced` | `Edm.ComplexType` | — | — | — | — | Read-side projection only (not searched on; deep filtering is satisfied by the dedicated fields). |
| `lastSeenUtc` | `Edm.DateTimeOffset` | — | ✓ | — | ✓ | Enables "last discovered" sort and recency filters. |
| `firstDiscoveredUtc` | `Edm.DateTimeOffset` | — | ✓ | — | ✓ | Optional sort key. |

The existing index version (`v1`) is retained; the additions are made via `azapi` patch in the IaC module. A canonical-rebuild run (existing `iac/scripts/` helper) is executed once during spec 009 rollout to backfill the new fields for historical documents.

---

## 3. In-memory domain types

These C# types live in `api/BusTerminal.Api/Features/Discovery/_Shared/` and `api/BusTerminal.Indexer/Discovery/` and mirror the Cosmos shapes. All use `record`, `init`-only setters, and nullable annotations (constitution: modern C# features).

```csharp
public sealed record PublishedEntity(
    string Id,
    string SchemaVersion,
    EntityType EntityType,
    string Environment,
    string NamespaceId,
    string Name,
    string DisplayName,
    string CompositeKey,
    string? ParentEntityId,
    EntityRegistryMetadata Registry,      // Description, Tags, etc. — spec 006 fields
    LifecycleStatus LifecycleStatus,
    DateTimeOffset LifecycleStatusChangedUtc,
    DateTimeOffset FirstDiscoveredUtc,
    DateTimeOffset LastSeenUtc,
    string LastDiscoveryRunId,
    AzureSourcedEntity AzureSourced,
    string AzureSourcedHash,
    IReadOnlyList<EntityServiceAssociation> ServiceAssociations,
    DateTimeOffset CreatedUtc,
    string CreatedBy,
    DateTimeOffset LastModifiedUtc,
    string LastModifiedBy,
    string ETag);

public enum EntityType { Queue, Topic, Subscription, Rule }
public enum LifecycleStatus { Active, Missing, Archived }
public enum EntityServiceRole { Owner, Producer, Consumer }

public sealed record EntityServiceAssociation(
    string AssociationId,
    string ServiceId,
    EntityServiceRole Role,
    DateTimeOffset CreatedUtc,
    string CreatedBy);

public sealed record DiscoveryRun(
    string Id,
    string SchemaVersion,
    string NamespaceId,
    DiscoveryRunStatus Status,
    DiscoveryTrigger Trigger,
    DateTimeOffset StartedUtc,
    DateTimeOffset? CompletedUtc,
    int? DurationMs,
    string RequestedBy,
    int QueueCount,
    int TopicCount,
    int SubscriptionCount,
    int RuleCount,
    int NewCount,
    int UpdatedCount,
    int UnchangedCount,
    int MissingCount,
    DiscoveryRunFailure? Failure,
    IReadOnlyList<CoalescedRequest> CoalescedRequests,
    string CorrelationId);

public enum DiscoveryRunStatus { Queued, InProgress, Succeeded, Failed }
public enum DiscoveryTrigger { Manual }   // future: Scheduled, Webhook

public sealed record DiscoveryRunFailure(
    DiscoveryFailureCategory Category,
    string Message,
    DiscoveryPhase OccurredAtPhase,
    int? RetriesExhausted);

public enum DiscoveryFailureCategory
{
    Authn, Authz, NotFound, Throttled, Transport, Internal, WorkerLost, Unknown
}

public enum DiscoveryPhase
{
    LockAcquire, FetchQueues, FetchTopics, FetchSubscriptions, FetchRules, Persist, ResultWrite
}

public sealed record CoalescedRequest(DateTimeOffset RequestedUtc, string RequestedBy);
```

`AzureSourcedEntity` is a discriminated union: a base record per entity type (`AzureSourcedQueue`, `AzureSourcedTopic`, `AzureSourcedSubscription`, `AzureSourcedRule`) each implementing a marker interface `IAzureSourcedEntity`. System.Text.Json polymorphic serialization (built-in .NET 8+) is used with `"$type"` discriminator that maps to `entityType`.

---

## 4. Identity & uniqueness summary

| Resource | Identifier | Uniqueness scope |
|---|---|---|
| PublishedEntity | `id` = `pe_` + SHA-256(compositeKey)[:24, base32] | Globally (within environment partition) |
| PublishedEntity | `compositeKey` | Within `namespaceId` |
| PublishedEntity | `(namespaceId, compositeKey)` | Composite unique index in Cosmos |
| DiscoveryRun | `id` = `dr_` + ULID | Globally (within namespace partition) |
| DiscoveryLock | `id` = `"lock"` | One per `namespaceId` partition |
| EntityServiceAssociation | `associationId` = `esa_` + ULID | Globally (denormalized in entity doc) |
| EntityServiceAssociation | `(entityId, serviceId, role)` | Validated on POST (no duplicate triples) |

---

## 5. Indexes and query patterns

### Cosmos indexes

- `registry-entities`: existing indexing policy retained. Explicit included paths: `/id`, `/environment`, `/namespaceId`, `/entityType`, `/lifecycleStatus`, `/compositeKey`, `/associatedServiceIds/*`, `/lastSeenUtc`. All other paths default-excluded.
- `discovery-runs`: included `/id`, `/namespaceId`, `/status`, `/startedUtc`. Composite index on `(/namespaceId, /startedUtc DESC)` for the discovery-history list view.
- `discovery-locks`: included `/id`, `/namespaceId`. Tiny container; default policy fine.

### Hot query paths

| Query | Endpoint | Index used |
|---|---|---|
| Get entity by id | `GET /api/entities/{id}` | PK + id lookup (single-partition read) |
| List entities for a namespace | `GET /api/entities?namespaceId=...` | AI Search projection (filterable `namespaceId`) |
| Missing-entity sweep at end of run | (worker internal) | `registry-entities` query by `namespaceId, lifecycleStatus = Active, lastSeenUtc < runStartUtc` |
| List discovery runs for namespace | `GET /api/namespaces/{id}/discovery-runs` | Composite index on `(/namespaceId, /startedUtc DESC)` |
| Get specific discovery run | `GET /api/discovery-runs/{id}?namespaceId=...` | PK + id lookup |
| Coalesce / acquire lock | (API internal) | `discovery-locks` PK + id read |

The `GET /api/discovery-runs/{id}` route requires `namespaceId` (passed as query param or path; see contracts) to scope the partition key — Cosmos doesn't natively support cross-partition single-doc reads efficiently, and namespace context is always available to the caller (history listing or panel link).

---

## 6. Telemetry schema (cross-reference)

See [research.md R-12](./research.md#r-12--telemetry-surface) for the ActivitySource and Meter schemas. Cardinality summary:

- Total span types: 5
- Total metric instruments: 5
- Max dimension cardinality per instrument: ≤ 8 (`failure_category` × `status` is the worst case)
- Total expected Application Insights cardinality contribution per active dev environment: < 200 unique series.

---

## 7. Migration / rollback

**Forward migration**:
1. Deploy IaC (extends three modules; creates the two new Cosmos containers and the new Service Bus queue).
2. Deploy API + Indexer images (the API tolerates absent `lifecycleStatus` / `azureSourced` on legacy registry entities and defaults them on first read; the worker only writes the new fields).
3. Run the existing canonical-rebuild script once to backfill the new AI Search index fields for legacy documents (lifecycle defaults to `Active`, association arrays default empty, `azureSourced` defaults absent — matches "not yet discovered" semantics).
4. No DB migration script required: every new field is nullable / has a sensible default.

**Rollback**: pin previous container images. The new Cosmos fields are inert and ignored by older API versions (they're additive). The Service Bus queue can be safely deleted if rolling all the way back; the `discovery-runs` and `discovery-locks` containers are also safe to drop (no FK relationships).

---

**Phase 1 data model status**: ✅ Complete. Schemas, validation rules, identity scheme, indexes, and migration plan all documented.
