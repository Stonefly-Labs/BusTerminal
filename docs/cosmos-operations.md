# BusTerminal — Cosmos DB Operations Runbook

**Status**: Living document. Updated per slice; current: slice 004 (core domain model).

This document is the operator-facing companion to the canonical-store implementation. The authoritative source for design decisions is the data model and contracts in [`specs/004-core-domain-model/`](../specs/004-core-domain-model/); this runbook explains how to run, troubleshoot, and reason about the deployed state.

---

## Containers and partitioning

The canonical store uses two Cosmos containers per Cosmos account:

| Container | Partition key | Holds |
|---|---|---|
| `Resources` | `/resourceType` | First-class resource documents (Namespace, Broker, Queue, Topic, Subscription, MessageContract, ProducerApplication, ConsumerApplication, Team, Environment, Tag, Policy, IntegrationFlow, DocumentationAsset) and Relationship peer documents under partition key `resourceType = "relationship"`. |
| `ChangeEvents` | `/resourceId` | The append-only change-event log: Created, Updated, LifecycleTransitioned, SoftDeleted, Restored. Per-resource history reads on partition key. |

The partition strategy keeps single-type bulk queries on a single logical partition and keeps per-resource change-log reads on a single logical partition. Cross-type queries fan out via Cosmos cross-partition mode.

### Indexing exclusions

The `Resources` container's indexing policy **wholesale excludes the `/extensions/*` path**. The rationale:

- **Extensions are structured JSON values of unbounded shape.** Indexing every nested path of every vendor's metadata would inflate RU charges on every write without any guaranteed query benefit.
- **The canonical store is not the discovery surface.** Discovery (free-text search, faceted browse) is the job of the future Azure AI Search projection slice. Cosmos indexes only what the canonical CRUD path needs to filter on.

This is enforced by the OpenTofu module and verified by the integration tests' Cosmos checkov gate.

### The `__indexable` opt-back-in contract

Vendors who want a specific extension key included in the future search projection signal so by setting that key to `true` in a sibling `__indexable` map on the resource:

```json
{
  "extensions": {
    "contoso:costCenter": "FIN-102",
    "contoso:sla": { "target": "99.9", "windowDays": 30 },
    "__indexable": {
      "contoso:costCenter": true,
      "contoso:sla": false
    }
  }
}
```

The contract:

1. **The canonical Cosmos container ignores `__indexable` entirely.** No Cosmos indexing-policy change is required, and no Cosmos query consults the marker. The wholesale `/extensions/*` exclusion holds.
2. **The future search-projection slice consults `extensions.__indexable.<key>` per resource** to decide whether to include each extension in its index. Keys with `__indexable[key] = true` are projected to the search index; keys with `false` or no entry are not.
3. **Omitting the `__indexable` marker entirely defaults each extension to "not projected to search."** This makes the default safe — no surprise leakage of an internal vendor extension into a discovery surface — and forces an explicit opt-in.
4. **The reserved `__indexable` key is not subject to the `<vendor>:<name>` namespacing rule** that applies to every other extension key. It is the only exception.

The search-projection slice is **not** part of slice 004. This document records the contract so the canonical writes already happening today are wire-compatible with the projection that lands later.

---

## Common operator queries

| Need | Query |
|---|---|
| All non-deleted resources of a type | `SELECT * FROM c WHERE c.resourceType = @t AND c.isDeleted = false` |
| Include soft-deleted | drop the `isDeleted = false` filter |
| Single resource by id | `ReadItemAsync(id, PartitionKey(resourceType))` — never a cross-partition query |
| Per-resource change history | `SELECT * FROM c WHERE c.resourceId = @id ORDER BY c.eventTimestamp ASC` (on `ChangeEvents`, partition key = `resourceId`) |
| Soft-deleted resources only | `WHERE c.isDeleted = true` |
| Resources in a given environment | `WHERE ARRAY_CONTAINS(c.environments, @env)` (US7 / T137 lands the helper) |

The **soft-delete query convention** is: writers must explicitly pass `includeDeleted: true` to retrieve tombstoned records. Every default read path filters `isDeleted = false`.

---

## Troubleshooting

### `ConcurrencyConflictException`

The store maps Cosmos `HTTP 412 PreconditionFailed` to `ConcurrencyConflictException`. This means the resource's `ConcurrencyToken` (Cosmos `_etag`) on disk has moved since the caller last read it.

**Recover by**: re-reading the current state, re-applying the intended change to the fresh record, and retrying the write. The CLI's `transition`, `soft-delete`, and `restore` subcommands re-read on every call so they are concurrency-safe by construction; long-running flows should follow the same pattern.

**Do not** drop the `IfMatch` token to "force" a write — that breaks the audit chain.

### Lifecycle transition rejected

`LifecycleTransitionRule` returns an Error finding when the incoming write changes `Resource.Lifecycle` to a state that is not legal from the previously persisted state. The legal-transition matrix is enforced by `LifecycleTransitions.IsTransitionLegal` and documented in [`specs/004-core-domain-model/contracts/lifecycle-transitions.md`](../specs/004-core-domain-model/contracts/lifecycle-transitions.md).

**Soft-delete and restore are NOT lifecycle transitions.** They run through dedicated store methods (`SoftDeleteAsync` / `RestoreAsync`) that bypass the rule. Restore returns the resource to its pre-deletion lifecycle state — never to `Active` unconditionally.

### Unknown resource type on read

A document persisted with a `resourceType` value that the current build's `ResourceTypeRegistry` does not know is materialized as `UnknownResource` and surfaces an Info finding via `UnknownResourceTypeRule`. The original JSON payload is preserved on `UnknownResource.RawJson`. This is the additive-evolution guarantee: a newer build that knows the type will read the document without migration.

### Extension key warning

`ExtensionKeyFormatRule` raises a Warning (not Error) when an extension key is present that is neither the reserved `__indexable` marker nor matches `^[a-z][a-z0-9-]*:[a-zA-Z][a-zA-Z0-9._-]*$`. The write is **not** blocked. The Warning is recorded on the resource's `ValidationState.Findings` for operator triage. Fix forward by re-writing the resource with the namespaced key.

---

## Network posture

The dev environment's Cosmos account is **public-endpoint** with Entra-ID-only data-plane auth (no shared keys). A Private Link follow-up is tracked separately and will replace public access in the test and production environments before any non-fixture data is loaded. The dev environment's threat model is "fixture data only; no PII" — see the integration `SecretScanGuardTests` (slice 004 / T151) for the denylist that enforces it.

---

## Local development against the emulator

The integration test suite boots against the official Cosmos Linux emulator container at `https://localhost:8081`. The `cosmos-emulator` service is started by the CI workflow (slice 004 / T150) and is also runnable locally — see [`docs/local-development.md`](./local-development.md) for the Docker compose snippet.

To bypass the emulator entirely for unit tests, the test project's `Category=Unit` filter selects only in-process unit tests with no Cosmos dependency.
