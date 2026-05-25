# BusTerminal — Canonical Domain Model

**Status**: Living document. Updated per slice; current: slice 004 (core domain model).

This document is the architectural overview of the canonical metadata model that BusTerminal is built around. It is intentionally **conceptual** — schemas, field-level constraints, JSON shapes, and validation rule signatures live in the slice-local artifacts linked below. Use this page to build a mental model of how the pieces fit together; use the deep-dive artifacts when you need exact details.

---

## Authoritative sources

This document summarizes; the following artifacts are authoritative for their respective concerns:

- **Feature spec** — [`specs/004-core-domain-model/spec.md`](../specs/004-core-domain-model/spec.md): the requirements and acceptance criteria the model exists to satisfy.
- **Data model** — [`specs/004-core-domain-model/data-model.md`](../specs/004-core-domain-model/data-model.md): the field-level entity catalog, lifecycle transitions, severity grading, naming cross-reference.
- **Per-type contracts** — [`specs/004-core-domain-model/contracts/`](../specs/004-core-domain-model/contracts/): JSON Schemas for every resource type, the relationship document, the change event, ownership, audit, version, extensions, and the import/export envelope. Two markdown companions in the same directory document the relationship-type vocabulary and the lifecycle-transition graph.
- **Operations runbook** — [`docs/cosmos-operations.md`](./cosmos-operations.md): how to read, write, and troubleshoot the canonical store at runtime.

Where this page conflicts with any of the above, the linked artifact wins and this page is out of date.

---

## What the canonical model is

The canonical domain model is the **durable, persistence-and-API-agnostic** shape that every other surface in BusTerminal — APIs, the web UI, search projections, governance engines, drift detectors, AI-enrichment pipelines, import/export tooling — consumes. It is the source of truth for the messaging-topology vocabulary the platform reasons about: namespaces, brokers, queues, topics, subscriptions, contracts, applications, teams, environments, tags, policies, integration flows, and documentation assets.

The model is operationalized in three layers:

1. **In-process C# domain** — abstract base `Resource` + sealed per-type records, value types, validation engine. Lives in `api/BusTerminal.Api/Domain/`.
2. **Persisted JSON documents** — Azure Cosmos DB containers, polymorphic JSON via System.Text.Json with a `resourceType` discriminator, optimistic concurrency via Cosmos `_etag`.
3. **Portable JSON Schemas** — the on-the-wire contract that future REST/event surfaces consume. Schemas in `contracts/` are the documented external shape and ARE the durable interface.

The model is intentionally **vendor-agnostic at the schema layer** (Azure-first at the persistence and identity layers per constitution Principle I; extensibility-friendly at the model layer per Principle VI).

---

## Resource — the shared base

Every first-class messaging entity inherits the `Resource` abstract base. The shared shape lives in `Domain/Resource.cs`; per-type derived records contribute only the fields unique to their concern.

The base carries the load-bearing identity, governance, and lifecycle fields:

- **Identifier** — a globally-unique `ResourceId` (Guid wrapper). Never embeds environment names or Azure resource IDs; the identifier survives environment moves, Azure subscription changes, and resource renaming.
- **Discriminator** — an opaque `ResourceType` string. Known values map to typed records via the in-process registry; unknown values materialize as `UnknownResource` (see "Additive extensibility" below). The discriminator is the polymorphic-serialization dispatch key and the Cosmos partition key.
- **Name + display name** — `Name` is the canonical machine-friendly identifier (lowercase, hyphen-separated, no spaces); `DisplayName` is the human-friendly label.
- **Namespace path** — a `/`-separated hierarchical path that locates the resource in the organizational topology (e.g., `enterprise/payments/order-processing`). Namespace inheritance resolves governance and ownership metadata up the parent chain.
- **Environments** — a collection of `EnvironmentClassification` values per resource. A single resource document may carry multiple environment associations (`development`, `production`, etc.). The minimum vocabulary is fixed; arbitrary custom environments are allowed.
- **Lifecycle** — one of `Draft`, `Active`, `Deprecated`, `Retired`, `Archived`. Transitions between states are constrained by a closed graph (see "Lifecycle" below).
- **Version** — a `SemanticVersion` (major.minor.patch) with compatibility metadata and lineage references.
- **Ownership** — an embedded `OwnershipRecord` (see "Ownership" below).
- **Audit** — embedded `AuditRecord` carrying the latest-write created-by/at and modified-by/at, plus the originating source system. Full change history lives in the change-event log, not on the resource itself.
- **Tags + classification + documentation references** — orthogonal metadata that any caller may attach.
- **Extensions** — a namespaced, structured-JSON dictionary that lets vendors and integrators attach arbitrary metadata without forking the schema. See "Extensions" below.
- **Validation state** — the last validation pass's findings, embedded on the resource itself so downstream consumers can read governance state without re-running the engine.
- **Concurrency token** — opaque ETag wrapper used for optimistic concurrency on write.
- **Soft-delete flag** — `IsDeleted = true` documents are tombstoned but preserved with full identifier and relationship lineage.

Per-type derived records (`Queue`, `Topic`, `Subscription`, `MessageContract`, etc.) add **only** the fields unique to their concern. They never override base behavior, and they never duplicate field definitions; if a concern is shared across types, it belongs on `Resource` or on a reusable embedded block.

The fourteen first-class types and their fields are catalogued in `data-model.md` § Entity Catalog and individually schema'd under `contracts/resources/`.

---

## Relationship graph

Relationships between resources are modeled as **first-class peer documents**, not as inline foreign-key fields. The `Relationship` record (in `Domain/Relationships/Relationship.cs`, schema in `contracts/relationship.schema.json`) captures the directional edge between two resources with the following load-bearing structure:

- A unique `Id` (independent of either endpoint's id).
- `SourceId` + `TargetId` — the two resources the edge connects.
- A typed `RelationshipType` from a controlled vocabulary (`publishesTo`, `consumedBy`, `subscriptionOf`, `usesContract`, `owns`, `partOfFlow`, and a handful more — the full list lives in `contracts/relationship-types.md`).
- An open `Annotations` JSON payload for edge metadata (rate limits, SLAs, sampling rules) that callers attach without schema changes.
- Audit + concurrency + soft-delete fields, matching the resource conventions.

Because relationships are peer documents, they:

- **Compose across resource types automatically.** Adding a new resource type does not require schema migration of existing edges.
- **Support multi-hop traversal** without join logic. The in-process `RelationshipGraph` traverses the edges with cycle protection.
- **Outlive endpoint mutation.** When a resource is soft-deleted, its inbound and outbound edges remain readable; traversal callers decide whether to walk through tombstones via the `includeDeleted` filter.

Edge direction and type are validated structurally on write (`RelationshipTypeValidityRule`) and against endpoint resource types (`DanglingReferenceRule`).

---

## Ownership

Ownership is an embedded `OwnershipRecord` on every resource (no separate join). The shape is fixed to a small, opinionated set of fields:

- The owning **team identifier** (a reference to a `Team` resource).
- **Technical contact** — operational point of contact (a principal reference, not a free-form string).
- **Business contact** — accountable stakeholder.
- **Escalation contact** — incident path.
- **Support channel** — where to reach the on-call team (a structured reference, not raw URLs in the model layer).
- **Operational tier** — `Tier1` / `Tier2` / `Tier3`, the support-severity floor for the resource.

The shape is **fixed and named**, not a free-form bag. The `OwnershipPresenceRule` enforces that every first-class resource carries an owning team; the absence of one is a Warning, not an Error, so legacy documents can be loaded and incrementally back-filled.

Ownership inherits up the namespace chain via `NamespaceInheritance`: a resource without an explicit `OwnershipRecord` resolves to the nearest ancestor namespace's ownership. The inheritance resolver returns both the resolved record and the namespace it came from so callers can surface "inherited from `enterprise/payments`" in UI.

---

## Lifecycle

Every resource carries a `LifecycleState`. Transitions between states are constrained by a closed graph:

- `Draft → Active` — first promotion.
- `Active → Deprecated → Retired → Archived` — the standard sunset path.
- `Deprecated → Active` — revival, supported.
- Cross-cuts to `Archived` — supported from any non-`Archived` state.

The full legal-transition matrix lives in [`contracts/lifecycle-transitions.md`](../specs/004-core-domain-model/contracts/lifecycle-transitions.md). `LifecycleTransitionRule` blocks (Error finding) any write that proposes an illegal transition from the persisted state.

**Soft-delete and restore are NOT lifecycle transitions.** A soft-deleted document keeps its lifecycle state intact; restore returns the document to its pre-deletion lifecycle (not unconditionally to `Active`). This preserves the "this resource was Active before someone tombstoned it" signal across the delete-restore boundary.

---

## Extensions

The canonical model is closed in shape (the fields above are the only fields) but **open at the edges via the `Extensions` block**. Extensions are a namespaced JSON dictionary attached to any resource:

- Keys are vendor-namespaced (`<vendor>:<name>`, e.g., `contoso:costCenter`). Malformed keys are flagged Warning by `ExtensionKeyFormatRule` but never block a write.
- Values are arbitrary structured JSON — strings, numbers, booleans, objects, arrays. Nested shapes are preserved verbatim through serialization round-trips.
- The reserved `__indexable` key (no namespace) is an opt-in marker for the future search projection. Operational details in `docs/cosmos-operations.md` § "The `__indexable` opt-back-in contract".

Extensions exist so integrators can attach the metadata they need without forking the core schema. The platform never interprets extension values; they pass through every layer untouched.

---

## Change-event log

The canonical store records a separate, append-only **change-event log** that captures every state transition. The log is a peer Cosmos container partitioned by `resourceId` so the "history of resource X" query is a single-partition read.

Each entry carries:

- The resource id and its at-the-time discriminator.
- The event type (`Created`, `Updated`, `LifecycleTransitioned`, `SoftDeleted`, `Restored`).
- The actor who performed the change (a principal reference, never a free-form string).
- The timestamp + source system.
- The before/after lifecycle states (when relevant).
- The concurrency token of the document at the moment of the change.

The log is the operational visibility surface — "who changed what, when, and from what state to what state" — without leaving the registry. It is **the** audit-and-incident-response data plane for the canonical store.

The log is append-only by construction: there is no update or delete API on `IChangeEventLog`. Soft-deleting a resource appends a `SoftDeleted` event; restoring a resource appends a `Restored` event. The resource document and its history are always reconcilable.

---

## Validation

The validation engine runs a registered set of rules over a resource or relationship and produces a structured `ValidationResult` of `ValidationFinding`s. Each finding carries a `RuleId`, a graded `Severity` (`Error`, `Warning`, `Info`), a human-readable `Message`, the field reference, and the evaluation timestamp.

Severity grading is the contract:

- **Error** — blocks the write. The caller receives a structured failure and the resource is not persisted.
- **Warning** — the write proceeds; the finding is recorded on the resource's `ValidationState` for downstream triage.
- **Info** — the write proceeds; the finding is advisory. Future AI-enrichment surfaces consume Info-severity findings as suggestions, not directives.

The rule set is registered through the DI container; per-story modules contribute their rules without engine modification. Rules opt in to specific resource types via `AppliesTo()`; unsupported types are skipped. A faulty rule cannot poison the engine: rule failures are wrapped as `Error` findings with a structured rule-failure marker, so the validation pass still completes.

Finding counts per pass are emitted as OTel counters (`busterminal.validation.finding_count_error`, `..._warning`, `..._info`) tagged by resource type, so dashboards surface validation health without scraping resource documents.

---

## Concurrency

Writes use **optimistic concurrency** via the resource's `ConcurrencyToken` (a wrapper over Cosmos's `_etag`). The store sends `If-Match: <token>` on every update; a mismatch produces `ConcurrencyConflictException`. The runbook in [`docs/cosmos-operations.md` § "ConcurrencyConflictException"](./cosmos-operations.md#concurrencyconflictexception) documents the read-retry pattern callers use.

There is no automatic retry built into the store — a conflict surfaces structurally so callers can decide whether the safe behavior is "re-read and re-apply" or "abort and surface the conflict to a human."

---

## Additive extensibility

The canonical model is designed for **additive evolution**. Two properties hold:

1. **A future build can introduce a new resource type without migrating existing documents.** New types register themselves at composition time; existing documents persist unchanged.
2. **A sibling or older build that does not know about a type still reads the document.** Documents with an unregistered discriminator materialize as `UnknownResource` with the original JSON preserved on `RawJson`, and the `UnknownResourceTypeRule` emits an Info finding so operators can surface "we have N documents the current build doesn't understand."

This is the additive-extensibility guarantee that lets the model grow without retroactive migration. It is structurally tested by `AdditiveEvolutionGuardTests` (slice 004 / T158) which simulates the register-then-unregister flow against a real persistence layer.

---

## Naming consistency

Every name in the model — type name, field name, JSON schema name, relationship-type vocabulary value, telemetry attribute — is enforced consistent across all surfaces by the cross-reference table in [`data-model.md` § Naming Cross-Reference](../specs/004-core-domain-model/data-model.md#naming-cross-reference). Future surfaces (REST API, web UI, search index, documentation) MUST use the names in that table; inventing synonyms is a constitutional violation.

---

## What this slice does NOT yet do

The model is in-process plus persisted. The following surfaces are explicitly out of scope at this slice and will land in later slices:

- **REST API endpoints.** Future API slices consume the model; the JSON Schemas in `contracts/` are the wire shape they will reference.
- **Web UI.** The UI exploration of the canonical model is a later slice.
- **Search index.** Discovery, faceted browse, free-text search — the model is the source, the projection lands later. The `__indexable` extension contract documented above is forward-compatible with that projection.
- **Broker-runtime synchronization.** The model is operator-authored today; live synchronization from Azure Service Bus (or other brokers) is a separate slice.

These omissions are intentional. The model is the durable shape; the surfaces that consume it land on top of it without retroactively reshaping it.
