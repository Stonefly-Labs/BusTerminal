# Indexer Event Contract

**Feature**: `006-service-bus-registry-core` | **Date**: 2026-06-02

This document is the binding contract between the API (the producer of changes to the `registry-entities` Cosmos container) and the indexer Functions container (the consumer of those changes that projects to Azure AI Search). The contract is enforced at the change-feed boundary; both sides MUST honor it.

---

## 1. Trigger configuration (consumer)

The indexer Function declares a Cosmos DB change-feed trigger with this binding:

```csharp
[Function("RegistryEntityIndexer")]
public async Task Run(
    [CosmosDBTrigger(
        databaseName: "%COSMOS_DATABASE_NAME%",
        containerName: "%COSMOS_REGISTRY_ENTITIES_CONTAINER%",   // "registry-entities"
        Connection = "Cosmos",
        LeaseContainerName = "%COSMOS_REGISTRY_LEASES_CONTAINER%", // "registry-entities-leases"
        CreateLeaseContainerIfNotExists = false,
        MaxItemsPerInvocation = 100,
        StartFromBeginning = true)]
    IReadOnlyList<RegistryEntityChangeFeedItem> changes,
    FunctionContext context)
```

Connection settings (no secrets — managed identity per research §17):

| Setting | Value |
|---|---|
| `Cosmos__accountEndpoint` | `https://<dev-cosmos-name>.documents.azure.com:443/` (from spec-005 outputs `cosmos_account_endpoint`) |
| `Cosmos__credential` | `managedidentity` |
| `Cosmos__clientId` | workload UAMI client ID (from spec-005 outputs `workload_uami_client_id`) |

The lease container is provisioned by `iac/modules/cosmos-registry-store` (per research §17 — under managed-identity auth the trigger cannot create the lease container itself).

---

## 2. Event payload shape (`RegistryEntityChangeFeedItem`)

The trigger delivers each Cosmos document as it appears at the change-feed offset, including all canonical fields (see `registry-entity.schema.json`) PLUS two registry-internal markers:

| Field | Type | Notes |
|---|---|---|
| (all canonical fields) | (per registry-entity.schema.json) | The full document at the change-feed offset. |
| `_isTombstone` | `bool` | When `true`, this is a tombstone marker written by the API immediately before a hard delete (research §10). When `false`/absent, this is a normal create or update. |
| `_tombstoneFor` | `Guid?` | When `_isTombstone = true`, the `id` of the entity being deleted. Otherwise null. |
| `_etag` | `string` | Cosmos-managed; carried for diagnostic correlation. |

Tombstone documents have **TTL 60 seconds** (Cosmos item-level TTL). The indexer MUST process and delete the corresponding AI Search document within that 60-second window; the SC-005 budget (p95 < 5s) gives a 12× safety margin under normal operating conditions.

---

## 3. Projection — `RegistryEntityChangeFeedItem` → `SearchDocument`

The indexer's `SearchDocumentMapper` projects every non-tombstone event to a search document conforming to `search-index.json` field set. Specifically:

| Source field | Search document field | Transformation |
|---|---|---|
| `id` | `id` | identity |
| `entityType` | `entityType` | identity |
| `name` | `name` | identity (case-preserved) |
| `fullyQualifiedName` | `fullyQualifiedName` | identity |
| `description` | `description` | identity (null → empty string) |
| `owner` | `owner` | identity (null → empty string) |
| `environment` | `environment` | identity |
| `status` | `status` | identity |
| `namespaceName` | `namespaceName` | identity |
| `azureResourceId` | `azureResourceId` | identity (null → empty string) |
| `tags` | `tags` | array of `{key, value}` objects projected 1:1 |
| `tags[].key` | `tagKeysLower` | de-duplicated lowercase keys |
| `metadata` | `metadataFlat` | flattened `"<key>=<jsonValueAsString>"` strings (recursive: nested objects produce dot-path keys, e.g. `"policy.retention.days=30"`) |
| `parentId` | `parentId` | identity (null preserved) |
| `updatedAtUtc` | `updatedAtUtc` | identity |
| `createdAtUtc` | `createdAtUtc` | identity |
| (constant) | `brokerKind` | always `"AzureServiceBus"` in this slice |

For tombstone events (`_isTombstone = true`), the projection is bypassed and the indexer calls `searchClient.DeleteDocumentsAsync("id", new[] { tombstoneItem._tombstoneFor })`.

---

## 4. Idempotence

The change feed is at-least-once. The indexer MUST be safe to invoke with the same change repeatedly:

- **Upserts** use `MergeOrUploadDocumentsAsync`; replaying the same document is a no-op.
- **Deletes** use `DeleteDocumentsAsync`; replaying a delete on an already-deleted document is a no-op (AI Search ignores deletes targeting missing keys).
- **Mixed**: if a delete is replayed AFTER a subsequent recreate (same `id`, possible in theory only if an operator re-uses an id — disallowed by the API but defended-against here), the indexer relies on Cosmos change-feed ordering at the partition level (`/environment`) to deliver the recreate AFTER the delete. Tombstone TTL prevents the recreate from being processed before the tombstone in any plausible operational scenario.

---

## 5. Retry and poison handling

Per the Cosmos change-feed trigger's built-in semantics:

| Phase | Behavior |
|---|---|
| In-trigger retry | Cosmos change feed checkpoints only after the invocation returns successfully. A throwing invocation is retried until success OR until the host's `MaxRetryCount` (default 5 for the worker host) is exhausted. |
| Per-document poison | When an exception is thrown for a specific change-feed item AND the batch retries don't clear it, the indexer logs an `Error`-level structured event with `entityId`, `eventType` (`upsert` / `delete`), `errorCategory` (`mapping`, `transient`, `unauthorized`, `aiSearchSchema`), `retryCount`, and `correlationId` (the `traceparent` trace ID embedded in the document's audit event) — then re-throws so the change feed continues from that offset (the standard Functions trigger behavior). Manual operator intervention is required to clear permanent failures (the entity must be deleted-and-recreated OR the indexer redeployed with a mapping fix). |

There is NO separate poison queue in this slice. A future ops-hardening spec MAY introduce a Service Bus dead-letter destination for permanently-failed items.

---

## 6. Failure visibility

Indexer failures are surfaced via:

- **Logs**: structured Error events as above, routed to App Insights via the existing OTel adapter (spec-005 baseline). LAW retention is 30 days (spec-005 Q5c).
- **Metrics**: indexer emits two custom metrics per invocation — `busterminal.indexer.upsert.count` and `busterminal.indexer.delete.count` — visible in App Insights metrics explorer.
- **Health endpoint**: the Functions container app exposes `/healthz` and `/readyz` — the readyz check fails when the worker host reports unhealthy. The CAE health probe binding (provisioned by `iac/modules/functions-container-app`) routes a failing probe to a container restart.

An ops dashboard binding is **deferred** to a future ops-hardening spec, consistent with the spec-005 deferral.

---

## 7. Versioning

This contract is implicitly versioned by:

- The index name (`registry-entities-v1`) — changing the field shape requires a new index name (`-v2`) AND a side-by-side rebuild.
- The `registry-entity.schema.json` shape — additive changes (new optional canonical fields) do NOT require an index version bump; removing or renaming fields does.

A future spec that introduces an `-v2` index MUST run both indexes in parallel during cutover and atomically swap the API's query target.
