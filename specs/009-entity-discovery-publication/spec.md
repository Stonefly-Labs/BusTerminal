# Feature Specification: Entity Discovery and Publication

**Feature Branch**: `009-entity-discovery-publication`

**Created**: 2026-06-17

**Status**: Draft

**Input**: User description: "Spec 008: Entity Discovery and Publication — Implement automated entity discovery and publication capabilities for the Service Bus Registry platform. Discover queues, topics, subscriptions, and rules from registered Azure Service Bus namespaces; publish them to the registry catalog; detect changes across runs; preserve registry-owned metadata; track discovery operations and outcomes."

> **Numbering note**: The user-supplied title labeled this as "Spec 008", but `specs/008-namespace-onboarding` already exists. This spec has been assigned the next available sequential slot, `009`, and renamed accordingly. The dependency reference to "Spec 007 – Namespace Onboarding Fixes and Improvements" in the source input maps to the existing `008-namespace-onboarding` directory.

## Clarifications

### Session 2026-06-17

- Q: For a "large" namespace (500 queues, 500 topics, 5,000 subscriptions, 5,000 rules), what is the maximum acceptable duration of a single discovery run? → A: ≤ 5 minutes (aggressive, near-interactive feel; requires significant parallelism in the discovery worker)
- Q: How should the discovery worker handle transient Azure API failures (HTTP 429 throttling, 503, network timeouts) encountered mid-run? → A: Bounded retry/backoff on transient errors (small attempt budget with exponential backoff, e.g., up to 3 attempts per call); fail the run only on persistent errors or authorization failures. Total retry overhead MUST stay within the 5-minute SC-005 budget.
- Q: When a discovery request arrives for a namespace that already has a run in flight, what should the system do? → A: Coalesce — return a reference to the in-flight run as a successful idempotent response; do not start a second run.
- Q: What is the cardinality of the relationship between a published entity and a registered service? → A: Many-to-many — an entity may be associated with any number of services, and each association carries a role label drawn from a v1 taxonomy of `Owner` (governance-accountable), `Producer` (publishes messages to the entity), and `Consumer` (receives messages from the entity). Any number of services may hold any role; multiple Owners are permitted (conflict resolution is left to organizational convention).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Discover and publish Service Bus entities for a registered namespace (Priority: P1)

A namespace administrator opens a registered namespace in the registry, initiates a discovery operation, and within minutes the registry catalog reflects every queue, topic, subscription, and rule that exists in the underlying Azure Service Bus namespace — including their technical configuration (lock durations, TTLs, partitioning, forwarding, filters, etc.). Re-running discovery later updates changed entities, surfaces newly created entities, and flags entities that no longer exist in Azure.

**Why this priority**: This is the foundational capability of the feature. Without successful discovery and publication, none of the other stories deliver value. It eliminates the manual documentation problem that motivated the feature and is the gate that converts the registry from a manually curated inventory into an authoritative one.

**Independent Test**: Register a namespace pointing at a real Azure Service Bus namespace containing a known set of entities, trigger discovery, and verify the registry catalog now lists every queue/topic/subscription/rule with accurate technical metadata. Re-run discovery after creating, modifying, and deleting entities in Azure, and verify the registry correctly classifies each affected entity as new, updated, or missing.

**Acceptance Scenarios**:

1. **Given** a registered namespace whose Azure Service Bus namespace contains queues, topics, subscriptions, and rules, **When** a namespace administrator initiates discovery, **Then** every entity is persisted to the registry catalog with its current technical configuration and a discovery run record is created showing the count of each entity type.
2. **Given** a prior successful discovery run for a namespace, **When** the namespace administrator initiates a second discovery after a new queue has been created in Azure, an existing topic's TTL has been changed, and a subscription has been deleted, **Then** the new queue appears in the catalog with Active status, the topic's recorded TTL matches the new Azure value, the deleted subscription is marked Missing, and the discovery run records counts of 1 new, 1 updated, and 1 missing.
3. **Given** identical Azure state between two consecutive discovery runs, **When** the second run completes, **Then** no duplicate entity records are created and the run summary reports 0 new, 0 updated, and 0 missing.
4. **Given** discovery is in progress, **When** the operation completes, **Then** each published entity reflects an updated `LastSeenTimestamp` and entities that existed in the prior catalog but were not observed in the current run transition to Missing status.

---

### User Story 2 - Browse and search the published entity catalog (Priority: P2)

Any authenticated registry user opens the entity catalog, searches by entity name, filters by entity type/namespace/service/status/tag, and drills into a specific entity to see both the technical configuration discovered from Azure and the business metadata curated by the registry owners.

**Why this priority**: Once entities are published (US1), the value proposition for the broader user base depends on being able to find and inspect them. Engineers, architects, and operators rely on the catalog to answer "where is this queue, who owns it, what's its lock duration?" without leaving the registry. This story is testable as soon as US1 lands any entities.

**Independent Test**: With a populated catalog, perform searches by name and entity type, apply combinations of filters (namespace + status, service + tag), sort results, and open an entity detail view. Verify both Azure-sourced and registry-curated metadata are clearly presented and that lifecycle status (Active / Missing / Archived) is visible.

**Acceptance Scenarios**:

1. **Given** a catalog containing entities from multiple namespaces, **When** an authenticated user searches by partial entity name, **Then** matching entities across all namespaces appear in the results with namespace, associated services (with roles), type, status, and last-discovered timestamp visible.
2. **Given** the entity catalog, **When** a user filters by entity type = Topic and status = Active, **Then** only active topics are listed and results can be further sorted by name or last-discovered timestamp.
3. **Given** a published entity, **When** a user opens its detail view, **Then** Azure-sourced technical configuration and registry-curated metadata are presented as visually distinct sections, alongside first-discovered timestamp, last-discovered timestamp, and current lifecycle status.

---

### User Story 3 - Inspect discovery history and troubleshoot failures (Priority: P3)

A platform administrator opens a namespace's discovery history, reviews chronologically ordered runs with status, duration, and outcome counts, and clicks into a failed run to read the captured error detail so they can identify the root cause (authorization gap, transient Azure outage, namespace deletion).

**Why this priority**: Operational confidence in the catalog depends on visibility into the synchronization process. Without history, administrators cannot tell when the catalog last reflected reality, why a run failed, or whether problems are recurring. Lower priority than US1/US2 because the catalog still delivers value before this lands, but it becomes essential for ongoing operation.

**Independent Test**: Trigger several discovery runs against a namespace — including at least one engineered to fail (e.g., revoke the discovery identity's access before triggering) — then open the history view and confirm successful and failed runs are listed with accurate status, timing, counts, and error details.

**Acceptance Scenarios**:

1. **Given** a namespace with multiple completed discovery runs, **When** a platform administrator opens the discovery history, **Then** runs are listed in reverse chronological order showing status, start/end timestamps, duration, and entity counts.
2. **Given** a discovery run that failed mid-execution, **When** an administrator opens the run detail, **Then** the failure status, captured error message, and any partial counts collected before failure are displayed.
3. **Given** a failed discovery run, **When** the administrator returns to the entity catalog, **Then** previously published entities for that namespace remain visible at their last successfully synchronized state.

---

### User Story 4 - Curate registry-owned metadata on a published entity (Priority: P4)

A service owner opens an entity that their service owns (i.e., has an `Owner`-role association with), adds a business description, attaches a documentation link, tags it, attaches additional `Producer`/`Consumer` associations for the services that interact with it, and saves. A later discovery run that updates the entity's Azure-sourced configuration does not disturb any of this curated metadata or the service associations.

**Why this priority**: Curation is what transforms a raw inventory into a governance asset, but it is sequenced after US1–US3 because users must be able to publish, find, and trust the catalog before curation effort is worthwhile. Independent of US3.

**Independent Test**: On an active published entity, edit description, tags, documentation link, and service association as a service owner; verify changes persist. Trigger a fresh discovery run after modifying the entity's Azure configuration; verify Azure-sourced fields refresh while every curated field is preserved unchanged.

**Acceptance Scenarios**:

1. **Given** a published entity, **When** a service owner of an `Owner`-role associated service updates its description, tags, documentation link, contact info, and adds a `Consumer`-role association for another service, **Then** the changes are saved and visible in the entity detail view.
2. **Given** an entity with curated registry metadata, **When** a subsequent discovery run updates the entity's Azure-sourced configuration, **Then** every curated field (description, ownership, tags, documentation, contacts, operational notes) is preserved unchanged while Azure-sourced fields reflect the new values.
3. **Given** a user without the required role, **When** they attempt to edit an entity's registry metadata, **Then** the operation is rejected with an authorization error.

---

### Edge Cases

- **Unreachable namespace**: discovery against a namespace whose Azure resource has been deleted, whose identity lacks RBAC, or which cannot be reached over the network must record a failed run with the underlying error and must leave the existing catalog unchanged.
- **Concurrent discovery**: a second discovery request issued against the same namespace while one is already running must not start a parallel run; the system must either reject the duplicate request or coalesce it with the in-flight run.
- **Entity reappearance**: an entity previously marked Missing that reappears in Azure on a subsequent run must transition back to Active and update its `LastSeenTimestamp`, preserving its existing registry metadata and its original `DiscoveryTimestamp`.
- **Archived entity reappears in Azure**: an entity in Archived status that is observed by a later discovery run must remain Archived (manual user action overrides automatic lifecycle) and the user-visible status must make clear it is archived despite still existing in Azure.
- **Partial discovery failure**: if discovery succeeds for some entity types and fails for others mid-run (e.g., topics succeed but subscriptions error), the run must be recorded as Failed with the error detail; successfully fetched entities may be persisted but unfetched scopes must not cause entities to be marked Missing (i.e., absence-of-observation is treated differently from explicit non-existence).
- **Empty namespace**: a namespace with no entities must result in a successful run with all counts at zero.
- **Very large namespace**: a namespace containing thousands of entities must complete discovery without manual paging or batching by the user and without exhausting system resources.
- **Rule filter not retrievable**: if Azure does not return a filter or action expression for a discovered rule (API gap), the rule must still be published with whatever attributes were obtained and the missing fields recorded as unknown rather than treated as a failure.
- **Entity hierarchy break**: discovery must never publish a subscription whose parent topic is absent, or a rule whose parent subscription is absent, from the same run's results.

## Requirements *(mandatory)*

### Functional Requirements

**Initiating discovery**

- **FR-001**: The system MUST allow a user holding the Namespace Administrator or Platform Administrator role for a registered namespace to initiate a discovery operation for that namespace through both the UI and a programmatic API.
- **FR-002**: The system MUST authenticate to Azure Service Bus using the platform's configured managed identity and MUST use the registered namespace's subscription ID, resource group, and namespace name to address Azure Resource Manager and Service Bus administration APIs.
- **FR-003**: The system MUST prevent two discovery operations for the same namespace from running concurrently. When a discovery is requested for a namespace that already has an in-flight run, the system MUST return a successful response carrying a reference to the in-flight run rather than starting a second run (idempotent coalescing). The caller MUST be able to observe from the response whether a new run was started or an existing run was returned.

**What is discovered**

- **FR-004**: Discovery MUST retrieve, for every queue in the namespace, at minimum: name, status, lock duration, max delivery count, duplicate detection settings, dead-lettering configuration, partitioning configuration, session support, forwarding configuration, default TTL, and max size.
- **FR-005**: Discovery MUST retrieve, for every topic in the namespace, at minimum: name, status, duplicate detection settings, partitioning configuration, default TTL, and max size.
- **FR-006**: Discovery MUST retrieve, for every subscription of every topic, at minimum: name, parent topic, dead-lettering settings, lock duration, max delivery count, session support, forwarding configuration, and default TTL.
- **FR-007**: Discovery MUST retrieve, for every rule on every subscription, the attributes available from Azure APIs, including at minimum: name, parent subscription, parent topic, filter type, filter expression, and action expression.

**Publication and catalog**

- **FR-008**: The system MUST persist every discovered entity as a registry-managed catalog record that is queryable through APIs and surfaced through UI experiences.
- **FR-009**: The system MUST assign each published entity a stable identity derived from its Azure resource identifier and its position in the Service Bus entity hierarchy, so that repeated discoveries update the existing record rather than creating duplicates.
- **FR-010**: The system MUST preserve and surface the parent–child relationships of the Service Bus hierarchy (topic → subscription → rule; namespace → queue|topic) on every published entity.
- **FR-011**: The system MUST associate every published entity with its parent registered namespace. The system MUST also permit any number of associations between a published entity and registered services, where each association carries a role label from the v1 taxonomy `Owner` | `Producer` | `Consumer`. Associations may be set manually by an authorized user. Multiple services may hold the same role for the same entity (including multiple `Owner` associations); an entity may have zero service associations.
- **FR-011a**: Role definitions for v1:
  - `Owner` — a service accountable for the entity's governance and curated metadata.
  - `Producer` — a service that publishes messages to the entity.
  - `Consumer` — a service that receives messages from the entity (subscribes to a topic, or reads from a queue).

**Lifecycle and change detection**

- **FR-012**: The system MUST maintain a lifecycle status on every published entity with values Active, Missing, and Archived, where Active means observed in the latest successful discovery, Missing means previously published but not observed in the latest successful discovery, and Archived means manually hidden from active catalog visibility.
- **FR-013**: On every successful discovery run, the system MUST classify each entity touched by the run as new (first observation), updated (Azure metadata changed since prior observation), or unchanged, and MUST classify every previously Active entity not observed in the run as missing.
- **FR-014**: The system MUST automatically transition a Missing entity back to Active when it is observed by a subsequent discovery run, while preserving its first-discovered timestamp.
- **FR-015**: The system MUST keep an Archived entity Archived even if subsequent discoveries observe it in Azure; only an explicit user action may change its status away from Archived.

**Metadata preservation**

- **FR-016**: The system MUST separately track Azure-sourced (technical) attributes and registry-owned (curated) attributes for every published entity, such that discovery refreshes Azure-sourced attributes without ever overwriting registry-owned attributes.
- **FR-017**: Registry-owned attributes that MUST survive every discovery run include, at minimum: description, business purpose, service ownership/association, tags, documentation links, contact information, and operational notes.

**Discovery history**

- **FR-018**: The system MUST persist a record of every discovery run capturing: run identifier, namespace identifier, status, start timestamp, completion timestamp, error details (when applicable), counts of queues/topics/subscriptions/rules discovered, and counts of new/updated/missing entities classified by the run.
- **FR-019**: Users MUST be able to view a namespace's discovery history including per-run status, duration, outcome counts, and any captured error detail.
- **FR-020**: Users MUST be able to view the summary of a specific discovery run via both UI and API.

**Failure handling**

- **FR-021**: When a discovery run fails, the system MUST record the failure status and captured error details, MUST leave the prior catalog state intact, and MUST NOT mark previously Active entities as Missing on the basis of an unsuccessful run.
- **FR-021a**: The discovery worker MUST retry transient Azure API failures (HTTP 429, 503, network timeouts, and equivalent retriable errors) using a bounded exponential-backoff strategy with a small per-call attempt budget. The worker MUST NOT retry persistent failures (authentication/authorization errors, "not found" responses for the namespace itself, malformed-request errors) and MUST fail the run immediately when such errors are encountered. Total retry overhead MUST be bounded so that successful runs against a SC-005-sized namespace still complete within 5 minutes.

**Searching and viewing**

- **FR-022**: The published entity catalog MUST be searchable and filterable by, at minimum: entity name (substring), entity type, namespace, associated service, association role (`Owner` | `Producer` | `Consumer`), tag, and lifecycle status, and MUST support result sorting by at least name and last-discovered timestamp. The "associated service" filter MUST match an entity if any of its `EntityServiceAssociation` entries reference the selected service (across all roles unless the user narrows by role).
- **FR-023**: Any authenticated user MUST be able to read the published entity catalog and view any entity's detail.
- **FR-024**: The entity detail view MUST visually distinguish Azure-sourced metadata from registry-owned metadata and MUST display first-discovered timestamp, last-discovered timestamp, and current lifecycle status.
- **FR-025**: The namespace overview experience MUST surface a discovery action, the last discovery status, the last discovery timestamp, and entity counts by type for the namespace.

**Authorization**

- **FR-026**: The system MUST permit editing of an entity's registry-owned metadata only to users who satisfy at least one of: (a) hold the Service Owner role for *any* service that has an `Owner`-role association with the entity, (b) hold the Namespace Administrator role for the entity's namespace, or (c) hold the Platform Administrator role. Holding the Service Owner role for a service that is only associated with the entity as `Producer` or `Consumer` does NOT grant metadata-edit permission.
- **FR-027**: The system MUST reject any discovery initiation request from a user who does not hold the Namespace Administrator or Platform Administrator role for the target namespace.

**Scalability and idempotency**

- **FR-028**: A discovery operation MUST complete successfully against a namespace containing on the order of hundreds of queues, hundreds of topics, thousands of subscriptions, and thousands of rules, without requiring manual paging or batching by the user.
- **FR-029**: A discovery operation MUST be idempotent — repeated runs against an unchanged Azure namespace MUST NOT create duplicate entity records and MUST NOT alter the Active set or any Azure-sourced field values beyond updating `LastSeenTimestamp`.

### Key Entities

- **DiscoveryRun** — Represents a single discovery execution against one registered namespace. Captures the run identifier, parent namespace, status (e.g., InProgress, Succeeded, Failed), start and completion timestamps, any error detail, per-entity-type discovery counts (queues, topics, subscriptions, rules), and per-classification counts (new, updated, missing).

- **PublishedEntity** — Represents a discovered Service Bus entity as a first-class registry resource. Carries the entity identifier, parent namespace, entity type (Queue | Topic | Subscription | Rule), name, the Azure resource identifier that anchors its identity, the parent published-entity identifier (for subscriptions and rules), lifecycle status (Active | Missing | Archived), first-discovered timestamp, last-seen timestamp, the Azure-sourced technical attributes, the registry-owned curated attributes, and a collection of `EntityServiceAssociation` entries.

- **EntityServiceAssociation** — Represents a many-to-many link between a `PublishedEntity` and a registered service. Carries the associated entity identifier, associated service identifier, and a role label drawn from the v1 taxonomy `Owner` | `Producer` | `Consumer`. An entity may have any number of associations; a service may participate in any number of associations across entities; the same service may hold multiple roles for the same entity (one association record per (entity, service, role) triple).

- **Supported entity types**: Queue, Topic, Subscription, Rule.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A namespace administrator can complete a first-time discovery for a registered namespace — from clicking the discover action to seeing the populated catalog — without any manual configuration beyond the existing namespace registration.
- **SC-002**: After a successful discovery, the registry catalog reflects 100% of the queues, topics, subscriptions, and rules present in the underlying Azure Service Bus namespace, with no missing entities and no spurious entries.
- **SC-003**: Re-running discovery on an unchanged namespace produces zero new, zero updated, and zero missing entity classifications and zero duplicate records.
- **SC-004**: Curated registry metadata (description, ownership, tags, documentation links, contact info, operational notes) is preserved across every subsequent discovery run with zero overwrites.
- **SC-005**: Discovery completes for a namespace containing at least 500 queues, 500 topics, 5,000 subscriptions, and 5,000 rules in **≤ 5 minutes** end-to-end, without requiring manual paging or batching by the user.
- **SC-006**: A failed discovery run leaves 100% of previously published entities visible at their last-known-good state, and the failure is recoverable by simply re-initiating discovery once the underlying cause is resolved.
- **SC-007**: A registry user can locate any specific published entity by name through catalog search in under 10 seconds from opening the catalog.
- **SC-008**: A platform administrator investigating a synchronization issue can identify the cause of a failed run from the discovery history view without needing to inspect platform logs or external systems for the recorded error detail.
- **SC-009**: An unauthorized attempt to initiate discovery or to edit registry-owned metadata is rejected 100% of the time.

## Assumptions

- The platform authenticates to Azure Service Bus using its own managed identity (consistent with the project's "Managed Identity preferred over secrets" rule); the identity is expected to hold the Azure roles needed to read Service Bus topology against every registered namespace prior to discovery.
- Discovery operations are asynchronous: initiating discovery returns a discovery run reference immediately, and progress / completion are observed by reading the run record or polling the catalog. Discovery does not block the requesting user's UI.
- Once initiated, a discovery run cannot be cancelled by the user; it runs to completion or to failure. Cancellation may be added by a future spec.
- The platform issues only read calls to Azure Service Bus and never issues create/update/delete operations against Azure-managed entities (consistent with the stated non-goal of automated remediation).
- Discovery is manually triggered for this release; no scheduled or recurring discovery is included (consistent with the stated non-goal).
- The registry's existing role model (Namespace Administrator, Platform Administrator, Service Owner) and authentication mechanism, established by prior specs, are reused. No new identity provider integration is introduced by this spec.
- The Service Bus administration protocol and Azure Resource Manager APIs surface every entity attribute called out in FR-004 through FR-007 for currently supported namespace tiers; cases where a specific rule attribute is unavailable from Azure are handled per the edge-case rule.
- Discovery run records are retained indefinitely for this release; a retention policy may be defined by a future spec if storage growth warrants.
- Tag taxonomy and service-association rules are governed by the existing service registry (Spec 003) and namespace registry (Spec 002) and are not redefined here.
- Telemetry for discovery operations (start/end events, durations, success/failure rates) flows through the platform's existing observability stack (Application Insights / OpenTelemetry per the project constitution) and does not require a new observability adapter for this spec.
- W3C Trace Context propagation for any UI-originated discovery request follows the mandatory project-wide rule and is not separately re-specified here.
