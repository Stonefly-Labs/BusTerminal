# Feature Specification: Core Domain Model

**Feature Branch**: `004-core-domain-model`

**Created**: 2026-05-23

**Status**: Draft

**Input**: Source artifact: `speckit-artifacts/004_core_domain_model_spec.md`

**Source Artifact**: [`speckit-artifacts/004_core_domain_model_spec.md`](../../speckit-artifacts/004_core_domain_model_spec.md)

---

## Overview

BusTerminal's value depends on one thing above all else: a canonical, durable, well-modeled representation of an organization's messaging topology — namespaces, queues, topics, subscriptions, message contracts, the producers and consumers attached to them, the teams that own them, the environments they live in, and the relationships that bind them together. Every later capability — search, the resource explorer, governance, drift detection, import/export, AI enrichment, federation — is a projection of this model.

This slice establishes that canonical model: the resource taxonomy, identifier strategy, lifecycle and versioning semantics, ownership structure, relationship graph, validation conventions, extensibility surface, audit metadata, and the persistence shape they all share. It deliberately does **not** ship API routes, UI screens, search projections, or runtime broker integration — those are downstream consumers that this spec exists to enable.

Treat the output of this slice as a long-lived compatibility boundary. Once contracts, ownership records, and relationship graphs begin accumulating in the registry, breaking schema changes become extremely expensive. The model must be additive-evolution-friendly from day one.

---

## Clarifications

### Session 2026-05-23

- Q: Which lifecycle transitions are legal between { Draft, Active, Deprecated, Retired, Archived }? → A: Strict forward-only progression with a single Draft-edit loop. Legal transitions are: Draft↔Draft (free edits while still in Draft), Draft→Active, Active→Deprecated, Deprecated→Active (un-deprecate permitted), Deprecated→Retired, Retired→Archived. Backward transitions from Retired or Archived are prohibited; Active→Draft is prohibited (once Active, the resource cannot return to Draft — a successor resource must be created instead).
- Q: How should concurrent updates to the same resource be handled? → A: Optimistic concurrency via a per-resource version/ETag token. Every resource carries a monotonically-increasing concurrency token; writes specify the token they read; the canonical store rejects stale writes with a structured concurrency-conflict error and the caller is expected to re-read and retry. No automatic field-level merging; no pessimistic locks.
- Q: Are validation outcomes binary (pass/fail) or graduated? → A: Graduated severity — Error, Warning, Info. Every validation rule declares its severity; results carry the severity per finding. Error blocks writes; Warning is persisted and surfaced but does not block; Info is advisory metadata for governance dashboards and AI enrichment. Per-rule severity overrides via policy are NOT supported in this slice (deferred until SaaS tenancy lands).
- Q: How are first-class resource types registered and extended? → A: Closed enum at framework level + open string in persisted documents. This slice ships the 14 named types as the known registry; the persisted type field is an opaque string so unknown types deserialize as "unknown type" placeholders without crashing; only known types are validated against per-type rules. New first-class types are added by future spec slices that extend the registry. Org-specific custom resource types are NOT supported in this slice (deferred until a CRD-style governance story exists).
- Q: What is the audit-trail granularity? → A: Latest-state audit metadata on the resource document plus a separate immutable change-event log (one event per state change) stored independently from the canonical resource. The change-event log captures actor, timestamp, source system, lifecycle/state diff or snapshot, and the concurrency token before/after. Retention on the change log is configurable independently of the canonical resource (e.g., via Cosmos DB TTL). Latest-state reads do NOT need to hydrate change history; historical queries traverse the change log explicitly.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Operators see a single canonical inventory of messaging topology across environments (Priority: P1)

A platform operator or messaging architect needs to answer: "what queues, topics, subscriptions, and namespaces exist across our messaging estate, in which environments, with what classifications, and which are live versus deprecated?" Today that answer lives in scattered Azure portals, ad-hoc spreadsheets, and engineers' heads. BusTerminal's job is to be the one authoritative answer.

This story is satisfied when the domain model can express a complete inventory in a uniform shape: every namespace, queue, topic, subscription, contract, producing application, consuming application, team, and environment is represented by a first-class resource with stable identity, type, name, display name, description, lifecycle state, environment association, classification metadata, audit history, and namespace path — independent of whether the underlying Azure resource has been provisioned, will be provisioned, or has been retired.

**Why this priority**: BusTerminal is a registry first. If the canonical inventory shape is wrong, every downstream capability inherits the defect. Every other priority in this slice (ownership, relationships, contracts, lifecycle, extensibility) is a property *of* this inventory — so the inventory itself must be P1.

**Independent Test**: Populate the canonical store with a representative fixture set covering every first-class resource type (namespace, broker, queue, topic, subscription, message contract, consumer application, producer application, team, environment, tag, policy, integration flow, documentation asset). Confirm that each resource serializes to and deserializes from the canonical document shape, retains its identifier across round-trip, exposes the common base fields (id, type, name, display name, description, lifecycle state, version, ownership, audit, tags, extensions, namespace path, environment), and validates against the model's schema rules without manual fix-up. Verifiable without any API, UI, or search index.

**Acceptance Scenarios**:

1. **Given** the canonical model is defined, **When** a fixture file containing one of each first-class resource type is loaded, **Then** every resource is materialized with the full base shape, retains its stable identifier, and reports its resource type, namespace path, environment, lifecycle state, and ownership without missing-field errors.
2. **Given** a queue resource is created, **When** its document is inspected, **Then** it carries namespace association, environment association, queue-type classification, duplicate-detection metadata, session-requirement metadata, ordering metadata, partitioning metadata, dead-letter behavior metadata, TTL metadata, message-size metadata, ownership metadata, contract associations, operational metadata, and deprecation metadata — all without inferring values from naming conventions.
3. **Given** a topic resource and a subscription resource are created with the subscription's parent set to the topic, **When** the model is queried, **Then** the parent-child relationship is materialized as an explicit, directional, typed relationship — not inferred from the subscription's name or namespace path.
4. **Given** a namespace `enterprise/payments/order-processing`, **When** its hierarchy is queried, **Then** the model exposes the full path, the parent chain, and any inherited governance/ownership metadata, without depending on Azure resource hierarchy or naming conventions.
5. **Given** any resource document, **When** its identifier is examined, **Then** the identifier is a stable, globally unique value that does not embed environment names, does not depend on Azure resource IDs, and does not change when the resource's logical name is updated.

---

### User Story 2 — Every operational resource has a structured, queryable owner (Priority: P1)

An operator paged at 02:00 about a dead-letter spike on a queue needs to know — within seconds — which team owns the queue, who the technical contact is, who the business contact is, what the escalation path is, what the operational tier is, and where the runbook lives. Today that information lives in tribal knowledge. BusTerminal exists in large part to end that.

This story is satisfied when every first-class operational resource carries structured ownership metadata that names an owning team, a technical contact, a business contact, an escalation path, support information, and an operational tier — and when teams themselves are first-class resources with stable identity, so ownership records survive renames and reassignments.

**Why this priority**: Ownership is the most operationally consequential metadata in a messaging registry. A perfect inventory with unknown owners is operationally useless. This is co-equal P1 with the inventory model itself.

**Independent Test**: For each first-class operational resource type (broker, queue, topic, subscription, contract, integration flow, producer application, consumer application), confirm an ownership-required validation rule fires when ownership is absent or refers to a non-existent team. Confirm that renaming a team's logical name does not break ownership references on existing resources because the references resolve by stable identifier. Confirm that the ownership shape can carry a technical contact, business contact, escalation reference, support reference, and operational tier without ad-hoc string fields.

**Acceptance Scenarios**:

1. **Given** an operator queries any first-class operational resource, **When** they request its owner, **Then** the result includes an owning team identifier, technical contact, business contact, escalation reference, support reference, and operational tier — using structured fields, not freeform text dumps.
2. **Given** a team's logical name is changed, **When** previously-owned resources are queried, **Then** their ownership references continue to resolve to the same team because resolution is by stable identifier, not by name.
3. **Given** an operational resource is created without ownership metadata, **When** the resource is validated, **Then** validation fails with a structured ownership-required error and the validation result is captured on the resource's validation state.
4. **Given** the model is queried for "all resources owned by team X," **When** the team is referenced by its stable identifier, **Then** the model can answer that query without scanning every document because ownership is indexable.
5. **Given** the ownership shape is inspected, **When** it is compared against the planned Entra ID / Microsoft Graph integration surface, **Then** technical and business contact fields are shaped to accept Entra principal identifiers in a future slice without requiring a schema break.

---

### User Story 3 — Producers, consumers, and messaging resources are linked by explicit, traversable relationships (Priority: P1)

A messaging architect needs to answer "if we deprecate this topic, which consuming applications break?" and "which producers publish to this topic, and what contract version are they on?" — not by grepping codebases or asking on Slack, but by traversing a relationship graph the registry already knows.

This story is satisfied when relationships between resources are modeled explicitly, with direction, with a relationship type, with optional metadata annotations, and with validation rules — so the model can answer questions like "Producer App `orders-api` publishes-to Topic `payments.events`, which is consumed-by Subscriptions `risk-scoring` and `audit-log`, which are owned-by Teams `risk` and `compliance`, using Contract `payment.requested@1.4`."

**Why this priority**: The relationship graph is the differentiator between BusTerminal and a flat resource list. The whole point of a registry is to answer dependency and impact questions. P1 because every later capability — impact analysis, deprecation workflows, dependency visualization, governance, drift detection — depends on the graph being well-modeled from the start.

**Independent Test**: Load a fixture set containing producer applications, topics, subscriptions, consumer applications, contracts, and teams with the example relationships from the source artifact (Producer App publishes-to Topic; Queue uses-contract Message Contract; Subscription consumed-by Consumer App; Team owns Queue). Traverse from a producer application to every consuming application reachable through topics and subscriptions. Confirm that traversal returns the correct set, that each hop is typed, that each hop is directional, and that the same model can be projected into a search index without losing relationship information.

**Acceptance Scenarios**:

1. **Given** a producer application linked to a topic via `publishes-to`, and a subscription on that topic linked to a consumer application via `consumed-by`, **When** the model is traversed from the producer, **Then** the consumer is reachable via a typed, directional, multi-hop relationship path with the topic and subscription as intermediate hops.
2. **Given** a relationship between two resources, **When** it is inspected, **Then** it exposes its source, target, direction, type, optional metadata annotations, and any validation results — not just a pair of identifiers.
3. **Given** a relationship references a target that does not exist, **When** the relationship is validated, **Then** validation fails with a structured dangling-reference error.
4. **Given** the canonical store, **When** a downstream consumer (search projection, import/export, visualization tooling) reads the relationships, **Then** all relationships are surfaced through the same model — no relationships are inferred only from naming conventions or namespace paths.
5. **Given** a resource is soft-deleted, **When** the relationship graph is queried, **Then** relationship lineage is retained so historical impact analysis remains possible (per FR-020 soft delete).

---

### User Story 4 — Message contracts attach to queues and topics with semantic versioning and compatibility metadata (Priority: P2)

A consumer team needs to know: "what message shape does this queue expect today, what version am I on, what versions are deprecated, and is the producer's next version backwards-compatible with mine?" A producer team needs to know: "if I bump this contract to v2, who do I have to notify?"

This story is satisfied when message contracts are first-class resources with their own identity, semantic version, format classification (JSON Schema, Avro, Protobuf, XML Schema, CloudEvents, custom), schema reference (inline or external), compatibility metadata, example payloads, deprecation status, and producer/consumer associations — and when queues and topics can carry one or more contract associations.

**Why this priority**: Contracts are central to the long-term value of a messaging registry, but they are not strictly required for the first inventory-and-ownership pass. Ranked P2 because the inventory and ownership model in P1 must exist *before* contracts have anything to attach to.

**Independent Test**: Define a contract with semantic version 1.0.0, attach it to a queue and a topic, attach producing and consuming applications, mark version 1.0.0 as Active and a hypothetical 0.9.0 as Deprecated, and confirm that the model exposes the active version, the deprecated lineage, the format classification, the schema reference, the example payload, compatibility metadata, and the producer/consumer linkage — all queryable without a custom code path per format.

**Acceptance Scenarios**:

1. **Given** a contract resource, **When** it is inspected, **Then** it exposes a semantic version (major/minor/patch), format classification, schema reference, compatibility indicator, example payload(s), validation metadata, producer associations, consumer associations, and deprecation status.
2. **Given** a contract is versioned from 1.4.2 to 1.5.0, **When** the model is queried, **Then** historical lineage (prior versions, current version pointer, deprecation status of older versions) is preserved.
3. **Given** a queue or topic with a `uses-contract` relationship to a contract, **When** the queue/topic is queried, **Then** the contract resolves via stable identifier and the contract's current version is reachable in one hop.
4. **Given** two compatible contract versions and one incompatible version, **When** compatibility is inspected, **Then** the model exposes compatibility metadata in a structured form a downstream validator (future slice) can act on — without that validator existing yet in this slice.
5. **Given** contracts of multiple formats coexist (JSON Schema, Avro, Protobuf, CloudEvents), **When** the canonical store is queried, **Then** all contract resources share the same base shape and differ only in their format-specific fields — no format requires a fork of the contract entity.

---

### User Story 5 — Lifecycle states and soft deletion let resources be deprecated and retired without losing history (Priority: P2)

An operator needs to mark a topic as Deprecated, run a window in which existing consumers migrate off it, then mark it Retired — without losing the historical record of who used it, which contracts it carried, or what its relationships were. A platform owner needs to recover an accidentally-deleted resource within the retention window.

This story is satisfied when every first-class resource supports the lifecycle states Draft, Active, Deprecated, Retired, and Archived; when state transitions are validated; when "deletion" is a soft-delete that retains identifier, audit history, and relationship lineage; and when restoration is possible within the retention window.

**Why this priority**: Lifecycle is foundational governance, but it builds on the inventory (P1) and benefits from the relationship graph (P1). P2 because it can ship after the inventory exists.

**Independent Test**: Create an Active queue. Transition it through Deprecated → Retired → Archived and confirm each transition is recorded with timestamp and actor. Soft-delete a topic and confirm its identifier, audit history, and relationships are retained. Restore the soft-deleted topic and confirm it returns to its prior state. Attempt an illegal transition (e.g., Retired → Draft) and confirm validation rejects it.

**Acceptance Scenarios**:

1. **Given** a first-class resource, **When** its lifecycle state is queried, **Then** the value is one of Draft, Active, Deprecated, Retired, or Archived — never an ad-hoc string.
2. **Given** a lifecycle transition is requested, **When** the requested transition is not permitted by the rules, **Then** the transition is rejected with a structured validation error.
3. **Given** a resource is soft-deleted, **When** the canonical store is queried, **Then** the resource's identifier, audit history, version metadata, and relationships are retained and the resource is restorable through a documented restoration workflow.
4. **Given** a deprecated topic with active consumers, **When** the model is queried, **Then** the deprecation status and any deprecation metadata (scheduled retirement date, replacement reference) are exposed so downstream tooling can surface migration prompts.

---

### User Story 6 — Organizations extend metadata without forking the canonical schema (Priority: P2)

A finance-conscious org needs to tag every queue with a cost center. A regulated org needs to mark resources with a data-sensitivity classification. A platform org wants to attach a runbook URL and a SLO target. None of these are universal enough to belong in the canonical schema, but all of them are critical to the organization that needs them.

This story is satisfied when the canonical resource shape carries a namespaced extension surface — `{ "contoso:costCenter": "FIN-102", "contoso:dataSensitivity": "confidential" }` — that organizations can use without modifying the canonical schema, without colliding with other organizations' extensions, and with optional indexing inclusion controls.

**Why this priority**: Extensibility is a hard guarantee that makes the canonical schema politically tractable for adopters. Without it, every org pressures for their fields in the core schema, the schema bloats, and breaking changes proliferate. P2 because it must ship with the foundation — but only after the canonical shape is settled.

**Independent Test**: Attach namespaced extensions to several resources, persist them, round-trip them through serialization, and confirm: extensions survive intact; the canonical schema's required-field validation does not depend on any extension; an extension's value can be a structured JSON object (not only a primitive); and extensions can opt into or out of search indexing inclusion via metadata.

**Acceptance Scenarios**:

1. **Given** a resource with extensions `contoso:costCenter` and `contoso:dataSensitivity`, **When** the resource is persisted and re-read, **Then** the extensions are preserved exactly, including structured object values.
2. **Given** two organizations both define an extension named `costCenter` under their own namespace prefixes (`contoso:costCenter`, `fabrikam:costCenter`), **When** both extensions coexist on related fixtures, **Then** neither collides with the other.
3. **Given** an extension is marked as excluded from search indexing, **When** the canonical store emits its search projection, **Then** the extension is omitted from the projection.
4. **Given** the canonical schema is validated, **When** the validator runs, **Then** no extension field is required and no extension absence triggers a validation failure.

---

### User Story 7 — Resources support environment classification without duplicating logical definitions (Priority: P2)

A queue named `payments.orders.processing` is a single logical resource that exists in Development, Test, QA, Staging, Production, and Disaster Recovery. Operators must be able to see "the queue" and its per-environment status — not six copies of the queue cluttering the registry.

This story is satisfied when resources carry environment associations (Development, Test, QA, Staging, Production, Disaster Recovery, plus extensibility for organization-specific environments) without requiring a separate logical resource per environment.

**Why this priority**: Environment awareness is operationally important but conceptually straightforward once the base resource shape exists. P2 because it depends on the inventory model from P1.

**Independent Test**: Create a single logical queue and attach environment associations covering all six minimum environment classifications. Confirm the model exposes per-environment metadata (such as differing operational metadata or differing deployment status) without forcing a duplicate logical document per environment. Confirm queries filter by environment.

**Acceptance Scenarios**:

1. **Given** a logical queue with environment associations to Development, Test, QA, Staging, Production, and Disaster Recovery, **When** the queue is queried, **Then** all six associations are exposed on the single logical resource — no environment duplicates exist in the canonical store.
2. **Given** the canonical model is queried with an environment filter, **When** the filter is set to Production, **Then** only resources associated to Production are returned, using indexable environment metadata.
3. **Given** an organization needs a non-standard environment ("training", "pre-prod"), **When** the environment vocabulary is extended, **Then** the extension does not require a schema change to the canonical model.

---

### User Story 8 — Metadata exports and imports preserve identity, relationships, and extensions (Priority: P3)

A platform owner needs to export the registry — for backup, for review, for migration between environments, for handoff to a downstream system — and re-import it with all identifiers, relationships, ownership, versions, and extensions intact.

This story is satisfied when the canonical model serializes losslessly to JSON and YAML (with future support for OpenAPI extensions and AsyncAPI-aligned exports), and when round-tripping a representative fixture set preserves every load-bearing piece of metadata.

**Why this priority**: Portability is foundational for long-term confidence in the platform, but it is not blocking for first usage. P3 because it builds on the canonical shape being settled.

**Independent Test**: Export a representative fixture set to JSON, re-import into an empty canonical store, and confirm a byte-meaningful equivalence: identifiers preserved, relationships preserved with direction and type, version lineage preserved, ownership preserved, extensions preserved, lifecycle state preserved. Repeat for YAML.

**Acceptance Scenarios**:

1. **Given** a populated canonical store, **When** it is exported to JSON and re-imported into an empty store, **Then** identifiers, relationships (with direction and type), version lineage, ownership references, lifecycle state, and extensions are preserved exactly.
2. **Given** an export, **When** it is inspected by a human reader, **Then** the document structure matches the canonical model's conceptual shape — no implementation-specific surrogate fields leak into the export.
3. **Given** an import encounters a duplicate identifier, **When** the import resolves the conflict, **Then** the resolution policy is explicit (e.g., reject, skip, overwrite) and the resolution outcome is recorded in audit metadata.

---

### Edge Cases

- **Circular relationships**: How does the model handle a relationship graph that contains cycles (e.g., a producer that consumes from a topic it also publishes to via a feedback loop)? Traversal must terminate, but cycles must not be rejected at validation time — they are legal in messaging.
- **Cross-namespace relationships**: A topic in `enterprise/payments/order-processing` may legitimately be consumed by an application in `enterprise/logistics/shipping`. The relationship graph must not be artificially constrained to within-namespace edges.
- **Dangling references after soft-delete**: When a resource is soft-deleted, relationships pointing at it remain. Downstream consumers (search, visualization) must be able to distinguish "live target" from "soft-deleted target" without re-traversing.
- **Identifier collisions across exports**: When importing from two separate canonical stores, identifier collisions are possible. The import must surface them deterministically, not silently coalesce.
- **Extension value depth**: Extension values can be deeply nested JSON. Validation, indexing, and serialization must handle arbitrarily nested structures (within a documented reasonable bound) without truncation.
- **Lifecycle of a contract version**: A contract's *version* has its own lifecycle independent of the contract resource itself. The model must distinguish "contract is Active overall" from "version 1.2.0 of this contract is Deprecated."
- **Resource type expansion**: A future slice will add new first-class resource types (e.g., Kafka registry support, dead-letter routing rules). The base shape must accommodate new types without retroactive migrations of existing documents.
- **Ownership of a Namespace itself**: A namespace can carry ownership and propagate it to child namespaces. Override semantics — when a child namespace defines its own ownership — must be explicit, not implicit.
- **Audit metadata for system-originated changes**: Some changes will come from synchronization workers, not human users. Audit metadata must accommodate "created by system" and a source-system reference without forcing a human principal.
- **Concurrent updates to the same resource**: When two callers update the same resource simultaneously, the second writer's call must be rejected with a structured concurrency-conflict error (per FR-025) — never silently overwritten. The caller is responsible for re-reading the current state, reconciling intent, and re-submitting.

---

## Requirements *(mandatory)*

### Functional Requirements

**Canonical resource shape**

- **FR-001**: The canonical resource model MUST define a shared base shape that every first-class resource type inherits. The shape MUST include: immutable identifier, resource type, logical name, display name, description, creation metadata, last-modified metadata, tags, ownership metadata, lifecycle state, classification metadata, version metadata, source metadata, validation state, and an extensibility surface.
- **FR-002**: The model MUST define the following first-class resource types as the known registry shipped in this slice: Namespace, Broker, Queue, Topic, Subscription, Message Contract, Consumer Application, Producer Application, Team, Environment, Tag, Policy, Integration Flow, and Documentation Asset. The known registry MUST be a closed enum at the framework level; new first-class types MUST be added by future spec slices that extend the registry (not by runtime configuration). The persisted resource-type field MUST be an opaque string so that documents whose type is not in the current known registry deserialize as "unknown type" placeholders without crashing — preserving forward and backward compatibility when types are added in later slices. Per-type validation rules MUST execute only for types in the known registry; unknown-type documents MUST NOT cause framework-level failures and MUST be flagged with an Info-severity finding (per FR-013). Organization-specific custom resource types are out of scope for this slice; orgs requiring bespoke per-resource metadata MUST use the namespaced extension surface (FR-012).

**Namespaces**

- **FR-003**: The model MUST support hierarchical namespaces (e.g., `enterprise/payments/order-processing`, `shared/platform/events`) that organize resources, support nested hierarchy, support ownership delegation, support metadata inheritance where appropriate, support governance boundaries, and support search scoping. Namespaces MUST NOT encode infrastructure topology or depend on Azure resource hierarchy.

**Per-type metadata**

- **FR-004**: Queue resources MUST carry namespace association, environment association, queue-type classification, duplicate-detection metadata, session-requirements metadata, ordering metadata, partitioning metadata, dead-letter behavior metadata, TTL metadata, message-size metadata, ownership metadata, consumer associations, producer associations, contract associations, operational metadata, and deprecation metadata.
- **FR-005**: Topic resources MUST carry namespace association, subscription relationships, contract relationships, producer relationships, classification metadata, ordering metadata, partitioning metadata, ownership metadata, lifecycle metadata, environment associations, and governance metadata.
- **FR-006**: Subscription resources MUST carry parent-topic relationship, filter metadata, rule metadata, consumer-application relationships, delivery-semantics metadata, dead-letter metadata, retry metadata, ownership metadata, lifecycle metadata, and operational metadata.
- **FR-007**: Message Contract resources MUST carry semantic version (major/minor/patch), schema reference (inline or external), format classification (JSON Schema, Avro, Protobuf, XML Schema, CloudEvents, custom), compatibility metadata, producer associations, consumer associations, deprecation status, example payload(s), and validation metadata.

**Relationships**

- **FR-008**: The model MUST express relationships between resources as explicit, directional, typed entities with optional metadata annotations. Relationships MUST be traversable and indexable. Relationships MUST NOT depend solely on inferred naming or namespace paths. The model MUST NOT require graph-database adoption to support relationship traversal in this slice.

**Ownership**

- **FR-009**: Every first-class operational resource MUST carry structured ownership metadata: owning team (by stable identifier), technical contact, business contact, escalation reference, support reference, and operational tier. The ownership shape MUST be compatible with a future Entra ID / Microsoft Graph integration without requiring a schema break.

**Lifecycle**

- **FR-010**: Every first-class resource MUST carry a lifecycle state drawn from the closed set { Draft, Active, Deprecated, Retired, Archived }. Legal transitions are: Draft→Draft (free edits while still in Draft), Draft→Active, Active→Deprecated, Deprecated→Active (un-deprecate), Deprecated→Retired, Retired→Archived. All other transitions — including Active→Draft and any backward transition from Retired or Archived — MUST be rejected by the validation framework with a structured illegal-transition error. Replacement of a Retired or Archived resource MUST occur by creating a successor resource (with its own identifier and version lineage reference back to the predecessor), not by reviving the original. Restoration of a soft-deleted resource (per FR-020) is a distinct operation that returns the resource to its prior lifecycle state and is NOT subject to the legal-transition graph.

**Versioning**

- **FR-011**: The model MUST support semantic versioning (major/minor/patch) with compatibility indicators, current-version references, deprecated-version tracking, and historical lineage. Versioning MUST be available at both the resource level and the contract level.

**Extensibility**

- **FR-012**: The canonical schema MUST support custom metadata extensions via a namespaced extension surface (e.g., `contoso:costCenter`). Extensions MUST: avoid requiring forks of the canonical schema; support arbitrary namespaced extension keys; support structured JSON payloads (not only primitives); support validation metadata; support per-extension search-indexing inclusion/exclusion.

**Validation**

- **FR-013**: The model MUST define a validation framework that supports: required-field rules, naming-standard rules, relationship-validity rules (including dangling-reference detection), duplicate-detection rules, lifecycle-transition rules, ownership-presence rules, and contract-compatibility rules. Every validation rule MUST declare a severity drawn from the closed set { Error, Warning, Info }. Validation results MUST be persisted as structured metadata on the validated resource, capturing per finding: the rule identifier, the severity, a human-readable message, the offending field or relationship reference (when applicable), and the timestamp of evaluation. Error-severity findings MUST block the write that produced them; Warning-severity findings MUST be persisted and surfaced but MUST NOT block the write; Info-severity findings MUST be persisted as advisory metadata only (consumed by governance dashboards and AI-enrichment pipelines in later slices). Per-rule severity overrides via policy are out of scope for this slice and deferred until SaaS tenancy is introduced.

**Searchability**

- **FR-014**: The model MUST be shaped to support efficient search indexing across all major entities, including full-text search, faceted filtering, tag filtering, ownership filtering, environment filtering, lifecycle filtering, contract filtering, and relationship traversal. This slice MUST NOT implement the search index itself, but MUST produce a canonical shape that downstream search projections can read without lossy transformation. **Compliance with this requirement is structural** — verified by the Naming Cross-Reference table in this slice's `data-model.md` (every field a search projection would key on is named consistently across in-process, persisted, schema, and telemetry surfaces) and by the per-type JSON Schemas in `contracts/`. No runtime test in this slice exercises the search projection; the future search-indexing slice is the testable consumer.

**Audit**

- **FR-015**: Every mutable resource MUST carry latest-state audit metadata: created-by (principal or system identity), created-timestamp, modified-by, modified-timestamp, source system, and synchronization metadata. Audit metadata MUST tolerate non-human actors (synchronization workers, import jobs, future agents). In addition, the model MUST persist an immutable change-event log separate from the canonical resource document. The log MUST append one event per state change, capturing the resource identifier, the actor, the timestamp, the source system, the concurrency token before and after the change (per FR-025), and either a structured diff or a full snapshot of the changed fields. The change log MUST be queryable per-resource and support independent retention configuration without affecting the canonical resource. Latest-state reads MUST NOT require hydrating the change log; historical "who changed what when?" queries MUST be answerable by traversing the change log explicitly. **Failure to append a change event after a successful canonical write is NOT a validation failure** — it is a persistence-pipeline failure logged at Error severity and surfaced to the caller. The canonical resource is NOT rolled back when the change-event append fails; the change-event log is an additional observability surface, not a transactional prerequisite. A future operational slice may upgrade this to true cross-container transactionality if Cosmos DB capabilities evolve.

**Import/Export**

- **FR-016**: The model MUST support portable serialization in JSON and YAML, with optional alignment to OpenAPI extensions and AsyncAPI exports as future targets. Round-trip serialization MUST preserve identifiers, relationships (with direction and type), version metadata, ownership references, lifecycle state, and extensions.

**Environments**

- **FR-017**: Resources MUST support environment associations drawn from at minimum { Development, Test, QA, Staging, Production, Disaster Recovery }, with extensibility for organization-specific environment classifications. Environment metadata MUST NOT require duplicate logical resources per environment.

**Classification**

- **FR-018**: Resources MUST support structured classification metadata covering at minimum criticality, data sensitivity, compliance scope, availability tier, business domain, and operational tier. In v1, classification is persisted as **opaque structured metadata** on the canonical resource: the canonical schema defines the field names and enumerated values where applicable (`criticality`, `operationalTier`), but no validation rule enforces population, value combinations, or business-policy alignment. Rule-based classification governance (e.g., "Tier-1 resources MUST have a runbook documentation asset attached") is deferred to a later governance slice that will plug new rules into the validation framework (FR-013).

**Documentation references**

- **FR-019**: Resources MUST support linked documentation metadata covering at minimum runbooks, wikis, architecture diagrams, AsyncAPI specs, operational guides, and external repository references.

**Soft delete**

- **FR-020**: The model MUST support soft deletion: deleted resources retain identifiers, latest-state audit metadata, the per-resource change-event log (per FR-015), and relationship lineage, and support restoration workflows within a retention window. Soft deletion itself MUST emit a change event into the log. Restoration of a soft-deleted resource MUST emit a corresponding restoration event and MUST return the resource to its prior lifecycle state without triggering the legal-transition rules of FR-010.

**Identifiers**

- **FR-021**: Identifiers MUST be globally unique, immutable, and decoupled from environment names, logical names, and Azure resource identifiers. The preferred form is GUID/UUID. Logical names and human-readable slugs MAY exist alongside the identifier but MUST NOT replace it as the canonical reference.

**Naming**

- **FR-022**: Logical names MUST default to lowercase, hyphen-separated, no spaces. Display names MAY contain spaces and casing. Fully qualified namespace paths MUST be derivable from the namespace hierarchy.

**API and persistence independence**

- **FR-023**: The canonical model MUST remain API-agnostic and persistence-implementation-agnostic. The conceptual model MUST be expressible independent of any specific REST shape, GraphQL schema, or Cosmos DB container layout. Persistence-specific concerns (partition keys, container structure, RU optimization, regional topology) are explicitly deferred to a later operational specification.

**Security boundaries**

- **FR-024**: The model MUST support resource-level authorization metadata, classification metadata, audit traceability, ownership accountability, and governance annotations. The model MUST NOT store secrets, credentials, connection strings, or message payload history.

**Concurrency**

- **FR-025**: Every mutable resource MUST carry a per-resource concurrency token (monotonically-increasing version or ETag-equivalent) distinct from its semantic version (FR-011). Every write MUST present the concurrency token observed on read; the canonical store MUST reject writes whose token is stale with a structured concurrency-conflict error that names the resource identifier, the presented token, and the current token. The model MUST NOT attempt automatic field-level merging on conflict; conflict resolution is the caller's responsibility (re-read, reconcile, re-submit). Pessimistic locking is NOT supported in this slice.

### Key Entities

- **Resource (base)**: The shared shape every first-class entity inherits. Identifies the entity, carries its name and description, locates it in a namespace, names its owner, records its lifecycle state and semantic version, stamps its audit history, carries a concurrency token for optimistic-concurrency control, attaches tags and extensions, and reports its validation state.
- **Namespace**: A hierarchical organizational boundary that groups resources, supports nested hierarchy, supports ownership delegation, and may carry inheritable metadata. Independent of Azure resource hierarchy.
- **Broker**: A logical messaging broker definition. Decoupled from any specific Azure Service Bus namespace, so the model is portable across brokers (Service Bus today, others later).
- **Queue / Topic / Subscription**: The first-class messaging resources, each carrying their type-specific metadata (queue behavior, topic relationships, subscription rules and filters) on top of the base shape.
- **Message Contract**: A versioned, format-classified schema definition (JSON Schema, Avro, Protobuf, XML Schema, CloudEvents, custom) with producer/consumer associations, compatibility metadata, deprecation status, and example payloads.
- **Producer Application / Consumer Application**: First-class representations of systems that publish to or consume from messaging resources. Linked into the relationship graph via typed edges.
- **Team**: The unit of ownership. Carries a stable identifier so ownership references survive renames.
- **Environment**: A classification (Development, Test, QA, Staging, Production, Disaster Recovery, plus organization-specific extensions). Associated with resources without forcing per-environment duplication.
- **Tag**: A taxonomy element attached to resources for filtering and grouping.
- **Policy**: A governance policy reference attachable to resources or namespaces.
- **Integration Flow**: A logical producer→messaging-resource→consumer flow expressing an end-to-end integration the registry tracks as a single unit.
- **Documentation Asset**: A linked reference to external documentation (runbook, wiki, AsyncAPI spec, architecture diagram).
- **Relationship**: A first-class edge between two resources with direction, type, optional metadata annotations, and validation results.
- **Ownership record**: The structured ownership shape carried by every operational resource — owning team, technical contact, business contact, escalation, support, operational tier.
- **Audit record**: The structured latest-state audit shape carried by every mutable resource — created by, created timestamp, modified by, modified timestamp, source system, synchronization metadata.
- **Change event**: An immutable, per-resource log entry capturing one state change — resource identifier, actor, timestamp, source system, concurrency token before and after, and either a structured diff or a snapshot of the affected fields. Stored independently of the canonical resource document with independently configurable retention.
- **Lifecycle state**: The closed set { Draft, Active, Deprecated, Retired, Archived }.
- **Version metadata**: Semantic version (major, minor, patch), compatibility indicator, current-version reference, deprecated-version tracking, historical lineage.
- **Validation result**: A structured record of validation outcomes per resource — which rules ran, the severity of each finding (Error, Warning, Info), the human-readable message, the offending field/relationship, and the evaluation timestamp. Error-severity findings block writes; Warning and Info findings are persisted but non-blocking.
- **Extension**: A namespaced custom-metadata entry on a resource, with structured value, validation metadata, and indexing-inclusion control.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A representative fixture set containing at least one instance of every first-class resource type (Namespace, Broker, Queue, Topic, Subscription, Message Contract, Consumer Application, Producer Application, Team, Environment, Tag, Policy, Integration Flow, Documentation Asset) loads into the canonical store, validates without manual fix-up, and round-trips through JSON serialization with zero load-bearing data loss.
- **SC-002**: Every first-class operational resource in the fixture set carries structured ownership metadata that resolves to a Team by stable identifier; renaming a Team does not break any ownership reference.
- **SC-003**: A relationship-graph traversal from a producer application reaches its downstream consuming applications via typed, directional, multi-hop relationships in a deterministic order, using only the canonical model — no naming-based inference.
- **SC-004**: Every first-class operational resource transitions through at least one full lifecycle path (Draft → Active → Deprecated → Retired → Archived) with each transition recorded in audit metadata and at least one illegal transition rejected by the validation framework.
- **SC-005**: Soft-deleting a resource retains its identifier, latest-state audit metadata, change-event log, version lineage, and inbound/outbound relationships. Soft deletion emits a change event into the log; restoration emits a corresponding restoration event and returns the resource to its prior lifecycle state. The resource is restorable through a documented workflow within the retention window.
- **SC-006**: A namespaced extension attached to a resource (e.g., `contoso:costCenter`) survives round-trip serialization with its structured value intact, does not affect canonical-schema validation, and can be excluded from search projections via opt-out metadata.
- **SC-007**: A single logical queue carrying environment associations to all six minimum environments (Development, Test, QA, Staging, Production, Disaster Recovery) is represented as a single canonical document — not six.
- **SC-008**: Validation rules covering required fields, naming standards, dangling references, duplicate detection, lifecycle transitions, ownership presence, and contract compatibility execute against the fixture set and store their results as structured metadata on the validated resources. The result set demonstrates findings at all three severities (Error, Warning, Info), confirms that Error findings block their owning write, and confirms that Warning and Info findings are persisted without blocking.
- **SC-009**: A representative export of the canonical store re-imports into an empty store with identifiers, relationships (with direction and type), version lineage, ownership references, lifecycle states, and extensions preserved exactly.
- **SC-010**: Adding a new first-class resource type to the model in a hypothetical future slice does not require migrating any existing canonical document — demonstrating the additive-evolution guarantee.
- **SC-011**: No resource document in the canonical store contains a secret, credential, connection string, or message payload — verifiable by an automated scan over the persisted store.
- **SC-012**: For any mutated resource in the fixture set, the change-event log returns the full ordered sequence of changes (create, update, lifecycle transition, soft delete, restoration) with actor, timestamp, source system, concurrency tokens, and diff/snapshot per event — and a latest-state read of the same resource returns without touching the change log.

---

## Assumptions

- **API surface is out of scope for this slice.** REST endpoints, GraphQL schemas, and route definitions are downstream consumers of this model and are explicitly deferred to later specs.
- **UI surface is out of scope for this slice.** The resource explorer, governance UI, and any operator screens that consume this model are deferred to later UI specs.
- **Search index implementation is out of scope.** This slice defines what the canonical model exposes to a search projection but does not implement the projection or stand up Azure AI Search content.
- **Broker runtime processing is out of scope.** This slice models messaging resources but does not subscribe to brokers, process messages, or reconcile registry state against live Azure Service Bus state. Drift detection is a future slice.
- **Multi-tenant SaaS tenancy is deferred.** The model is shaped to evolve toward tenant isolation later without breaking changes, but no tenant scoping is added now.
- **Federation across registries is deferred.** Cross-registry relationship references (OQ-001 in the source artifact) are noted as a future consideration; the current model does not implement them.
- **Pluggable contract compatibility validation is deferred.** This slice defines compatibility *metadata*; pluggable validator implementations (OQ-002 in the source artifact) are a later slice.
- **AsyncAPI as an internal storage format is deferred.** AsyncAPI is treated as an export target (FR-016) but not as the canonical internal format (OQ-003 in the source artifact).
- **Graph-native storage projections are deferred.** Relationships are explicit and traversable using the document store; a future slice may add graph-native projections (OQ-004 in the source artifact) without breaking changes.
- **Microsoft Entra ID / Graph integration for ownership is deferred.** The ownership shape is designed to accept Entra principal references in a future slice; this slice does not perform Graph lookups.
- **Identifier generation defaults to GUID/UUID v4.** Other strategies (e.g., ULID) are not adopted in this slice; the canonical shape stores opaque string identifiers and does not depend on identifier-format internals.
- **Retention window for soft-deleted resources defaults to industry-standard practice.** A specific retention duration is not pinned in this slice; the model carries the metadata necessary for a retention policy to be enforced in a later operational slice.
- **A small representative fixture set is delivered with this slice.** Fixtures cover every first-class resource type, exercise relationships and lifecycle transitions, and serve as the basis for round-trip and validation tests. Fixtures are not production seed data.
- **Persistence is Azure Cosmos DB, JSON-document-oriented.** Final partition-key strategy, container layout, RU optimization, and cross-region topology are explicitly deferred (per source artifact's storage guidance).
- **Constitutional technology choices apply.** .NET 10, C#, OpenTofu for infrastructure, Azure Cosmos DB for canonical metadata, Azure AI Search for downstream discovery, and the project's OpenTelemetry conventions all govern this slice's implementation without restatement here.
- **Builds on upstream foundation work.** This slice assumes specs 001 (brand and design foundation), 002 (solution foundation), and 003 (auth and identity) are merged. It does not re-implement infrastructure, auth, or design primitives.
