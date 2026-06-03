# Feature Specification: Service Bus Registry Core

**Feature Branch**: `feature/006-service-bus-registry-core`

**Created**: 2026-06-01

**Status**: Draft

**Input**: User description: "let's spec this guy out based on the spec 006 deets here: speckit-artifacts/006-service-bus-registry-core.md"

**Source artifact**: [speckit-artifacts/006-service-bus-registry-core.md](../../speckit-artifacts/006-service-bus-registry-core.md)

---

## Clarifications

### Session 2026-06-01

- Q: Deletion policy for entities with children (block vs cascade vs soft-delete)? → A: Block deletion until children are removed (no cascade in this slice).
- Q: Delete semantics and `status` lifecycle? → A: Hard delete (physical removal). `status` in this slice supports `Active` and `Deprecated` (operator-settable, not auto-driven); `Deleted` is reserved for a future soft-delete spec but is not emitted by any code path. Restoration is not supported; the audit trail (FR-032) carries the historical record.
- Q: Concurrency conflict UX (FR-020)? → A: Refresh-or-overwrite modal. On a stale-version save, the API returns a recoverable conflict carrying the current server-side entity state plus an identification of which fields changed. The UI shows a dialog with two explicit actions: "Discard my changes and refresh" or "Force overwrite". Force overwrite proceeds with the user's submitted values and is recorded as an explicit user choice in the audit event for that update.
- Q: Authorization scope — who counts as an "operator"? → A: Any authenticated tenant user can read AND write. No role gating in this slice. Role-based differentiation (Reader vs Writer vs Admin) is reserved for a future governance spec. Operational risk is accepted on the basis that initial deployments will be in tenants/environments scoped to messaging engineering teams.
- Q: Tag schema — labels vs key/value? → A: Key/value pairs, free-form on both sides. Tag keys are case-insensitive on match and case-preserving on display; tag values are case-preserving and matched as entered. Filter supports key-only, value-only, and key+value matches. No enumerated key vocabulary in this slice.

---

## Overview

This feature establishes the first feature-complete product slice of BusTerminal — the **Service Bus Registry Core**. Before this slice, BusTerminal is a platform shell (auth, branding, infrastructure baseline) with no user-facing registry capability. After this slice, an operator can register, browse, search, edit, and delete Azure Service Bus assets (namespaces, queues, topics, subscriptions, rules) across environments, with persistent storage, full-text discovery, relationship traversal, and basic audit history.

The slice intentionally excludes automatic Azure discovery, AI-assisted semantic search, ownership governance workflows, CLI tooling, and deep operational telemetry — those are reserved for later specs.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Manually register and browse Service Bus assets (Priority: P1)

A platform engineer responsible for messaging infrastructure needs a single, environment-aware place of record for every namespace, queue, topic, subscription, and rule they operate. Today this lives in scattered ARM templates, spreadsheets, Confluence pages, and tribal knowledge. They want to type the asset details in once, see them rendered in a clean explorer, and click an entity to see its full metadata page.

**Why this priority**: Without manual registration and browsing, BusTerminal has no registry — every later capability (search, relationships, audit, future automatic discovery) depends on entities existing and being viewable. This is the smallest slice that makes BusTerminal a *usable application* rather than a shell.

**Independent Test**: An operator can sign in, open the Registry Explorer, register a namespace, add a queue beneath it, navigate to the queue's detail page, edit its metadata, and delete it. Persistence survives a page reload and a service restart. The slice ships value end-to-end even if no other user story is implemented.

**Acceptance Scenarios**:

1. **Given** an authenticated operator on an empty registry, **When** they submit a valid namespace registration with name, environment, Azure resource ID, owner, and tags, **Then** the namespace appears in the Registry Explorer under the chosen environment and persists across reloads.
2. **Given** a registered namespace, **When** the operator registers a queue and selects the namespace as parent, **Then** the queue appears nested under the namespace in the explorer tree and its detail page shows the parent namespace.
3. **Given** an existing queue, **When** the operator opens its detail page and edits the description, owner, or tags, **Then** the changes are saved, the page reflects them immediately, and the `updatedAtUtc` timestamp advances.
4. **Given** an existing entity, **When** the operator deletes it and confirms the prompt, **Then** the entity is removed from the explorer and detail navigation, and a subsequent attempt to view its direct URL returns a not-found state.
5. **Given** an operator attempts to register two queues with the same name under the same namespace and environment, **When** they submit the second one, **Then** the system rejects the submission with a clear duplicate-name validation error and no entity is created.
6. **Given** an operator submits a registration with missing required fields (e.g., no name or no environment), **When** they submit the form, **Then** the form surfaces inline validation errors and the request is not sent to the backend.
7. **Given** a topic whose `status` is `Deprecated`, **When** an operator creates a new subscription beneath it, **Then** the create form surfaces a "parent is deprecated" warning banner before submit, the submit is permitted, the create succeeds, AND the resulting audit event's `changeSummary` carries the prefix `UNDER_DEPRECATED_PARENT:` so downstream governance can filter for them. The behavior is identical for any parent–child pair when the parent is `Deprecated`.

---

### User Story 2 — Discover assets via search and filters (Priority: P2)

An on-call engineer paged at 02:00 needs to find the owner and environment of `orders-dead-letter` without knowing which namespace it lives in. They want one search box that returns matching entities across all environments, with filters to narrow by entity type, environment, and tag.

**Why this priority**: Browsing alone does not scale past a few dozen entities. Search is what turns the registry from a catalog into an operational tool. It depends on Story 1 (entities must exist) but is independently valuable — once shipped, every operator can answer "where does X live and who owns it?" in seconds.

**Independent Test**: With a populated registry, an operator can type a partial entity name into a global search field and see ranked results in under one second. Filtering by `entityType=queue` and `environment=prod` narrows results correctly. Clicking a result opens its detail page.

**Acceptance Scenarios**:

1. **Given** a registry containing entities across multiple environments, **When** the operator types a partial name into the global search, **Then** matching entities appear in a ranked result list within one second, showing entity type, environment, and parent namespace for each.
2. **Given** a search returning many results, **When** the operator applies an entity type filter (queue), an environment filter (prod), and a tag filter, **Then** the result list narrows to only entities matching all selected criteria.
3. **Given** a registry where an entity was just created, updated, or deleted, **When** the operator runs a search that should reflect that change, **Then** the search results reflect the latest state within the SC-005 budget (under five seconds at p95 under normal indexing-pipeline conditions).
4. **Given** a search returns zero results, **When** the operator views the result area, **Then** they see a clear empty-state message distinguishing "no results for this query" from "search is unavailable."
5. **Given** the search backend is temporarily unavailable, **When** the operator runs a search, **Then** the UI surfaces a graceful error state and the rest of the explorer (browse, detail pages, CRUD) continues to work.

---

### User Story 3 — Traverse relationships and review change history (Priority: P3)

A messaging architect reviewing a topic before a refactor needs to see every subscription attached to it, every rule on each subscription, and the recent history of who changed what. They want to click into a topic, see its child subscriptions inline, drill into one, see its rules, and view an audit panel showing the last several create/update/delete events for the entities they're looking at.

**Why this priority**: Relationships and audit are what turn the registry from a flat catalog into a governance tool. They are critical for the long-term vision but not blocking for first-use value — operators can survive the first weeks without them by using search + parent fields. P3 because Stories 1 and 2 deliver the headline user value; this story deepens it.

**Independent Test**: With a populated registry containing topics, subscriptions, and rules, an operator can navigate from a topic detail page to its subscription list, into a subscription detail page, into its rule list, and back. The audit panel on each entity shows recent create/update/delete events with actor, timestamp, entity type, entity ID, and a change summary.

**Acceptance Scenarios**:

1. **Given** a topic with multiple subscriptions, **When** the operator opens the topic detail page, **Then** the subscriptions appear in a relationships section with links to their own detail pages.
2. **Given** a subscription with multiple rules, **When** the operator opens the subscription detail page, **Then** the rules appear in a relationships section linked to their detail pages.
3. **Given** any entity has been created, edited, or deleted by an authenticated user, **When** an operator views that entity's detail page (or, for deleted entities, an admin-accessible audit view), **Then** the audit panel shows each event with actor identity, UTC timestamp, entity type, entity ID, and a human-readable change summary.
4. **Given** an operator attempts to create a relationship that violates structural rules (e.g., a rule with no parent subscription, a subscription whose declared parent topic does not exist), **When** they submit the form, **Then** the system rejects the submission with a clear structural-validation error.

---

### Edge Cases

- **Concurrent edits**: Two operators open the same entity, both edit it, both save. The first save succeeds. The second save fails with a conflict response and the UI shows the dialog defined in FR-020 — the second operator chooses **Discard & refresh** or **Force overwrite**, and Force overwrite is recorded as an explicit choice in the resulting audit event. Silent overwrite is never acceptable.
- **Orphaned children**: A namespace is deleted while it still has queues registered under it. The system MUST block the delete with a clear message identifying the blocking children (e.g., "12 queues still reference this namespace — remove them first"). Cascade deletion is out of scope for this slice.
- **Search indexer lag**: An entity is created, the operator immediately searches for it, but the search index has not caught up. The browse view (sourced from the persistent store) must always reflect the create immediately; search lag of a few seconds is acceptable as long as eventual consistency is reached.
- **Indexer failure**: The search indexing pipeline fails mid-batch for transient reasons. The pipeline must retry, and failures that exhaust retries must be visible in operational logs/telemetry — the persistent store must never become inconsistent with what was actually saved.
- **Invalid Azure resource IDs**: An operator pastes a malformed ARM resource ID. The system must reject it with a validation message that identifies what's wrong (wrong format, wrong resource type, wrong subscription scope), not a generic "invalid input."
- **Very large registries**: An environment contains tens of thousands of entities. Browse and search must remain responsive via pagination and server-side filtering — the UI must never attempt to load the entire registry at once.
- **Missing optional metadata**: An entity is registered with only the required fields. The detail page must render cleanly with empty-state placeholders for optional sections (tags, owner, description, metadata) rather than blank gaps or errors.
- **Unauthenticated/unauthorized access**: A user without valid authentication, or whose session expired mid-edit, attempts a write operation. The system MUST reject the request, preserve the user's in-progress form data in browser-session state, and present an authenticated-only error surface with a "Sign in again" CTA that triggers re-authentication and, on success, returns the user to the **same URL** they were on so the in-progress form is restored. The form-state preservation is best-effort and bounded to the browser tab; closing the tab discards the buffer.
- **Tag key case-collision**: An operator submits a tag with key `OWNER` against an entity that already has a tag with key `Owner`. The system MUST treat the keys as the same key (case-insensitive match), preserve the first-written casing (`Owner`) for display, and store the submitted value under the existing key. The submitter sees `Owner` (not `OWNER`) in the rendered tag list after save. This rule applies on both POST (create) and PUT (update).
- **Special characters / case sensitivity in names**: Entity names containing dashes, dots, underscores, and mixed case must be preserved exactly as entered. Duplicate detection must follow Azure Service Bus's own naming rules.

---

## Requirements *(mandatory)*

### Functional Requirements

#### Registry Entity Model

- **FR-001**: The system MUST support the following registry entity types: Namespace, Queue, Topic, Subscription, and Rule.
- **FR-002**: Every registry entity MUST carry the canonical shared fields defined by the source artifact: `id` (stable identifier), `entityType`, `name`, `fullyQualifiedName`, `description`, `tags`, `owner`, `environment`, `status`, `createdAtUtc`, `updatedAtUtc`, `source` (Manual / Discovered), `azureResourceId`, `namespaceName`, and `metadata` (extensible structured metadata). In this slice the `status` enumeration accepts `Active` and `Deprecated` only; `Deleted` is reserved for a future soft-delete spec and MUST NOT be emitted by any code path in this slice. The `tags` field is a collection of key/value pairs (both free-form strings); a single entity MAY carry zero or more tags. Tag keys are matched case-insensitively and displayed case-preserved (the first-written casing wins for display when normalized); tag values are matched and displayed case-preserved as entered. **Bounds (enforced by validation)**: `name` length follows Azure Service Bus naming rules per entity type (namespace 6–50 chars; queue/topic ≤ 260; subscription/rule ≤ 50); `description` ≤ 4000 chars; tag `key` ≤ 256 chars; tag `value` ≤ 1024 chars; maximum 50 tags per entity; `metadata` ≤ 100KB serialized. **`fullyQualifiedName` composition** is server-computed and read-only from the client perspective: Namespace → `<namespaceName>`; Queue → `<namespaceName>/<queueName>`; Topic → `<namespaceName>/<topicName>`; Subscription → `<namespaceName>/<topicName>/<subscriptionName>`; Rule → `<namespaceName>/<topicName>/<subscriptionName>/<ruleName>`. **Owner vs ownership tags**: `owner` is the canonical owning team/person identifier; tags MAY carry ownership-style key/value pairs for grouping but `owner` remains the authoritative field. **Naming rules source**: Azure Service Bus naming rules cited from <https://learn.microsoft.com/azure/service-bus-messaging/service-bus-quotas> (Naming rules section). Frontend and backend MUST validate identically against this source; the per-type regex set is captured in the plan-phase data model.
- **FR-003**: The system MUST enforce required fields: `id`, `entityType`, `name`, `environment`, and `status` MUST be present on every entity; other shared fields MAY be omitted.
- **FR-004**: The system MUST stamp `source = Manual` for all entities created via the user interface in this slice. Automatic discovery is out of scope (reserved for a future spec).
- **FR-005**: The system MUST maintain `createdAtUtc` immutably on first save and update `updatedAtUtc` on every subsequent mutation.

#### Relationships

- **FR-006**: The system MUST model and persist parent/child relationships: Namespace → Queue, Namespace → Topic, Topic → Subscription, Subscription → Rule.
- **FR-007**: The system MUST allow an operator to traverse relationships in both directions from any entity detail view (parent navigation and child enumeration).
- **FR-008**: The system MUST reject creation of a child entity whose declared parent does not exist, with a clear validation error.
- **FR-009**: The system MUST block deletion of any entity that still has registered children. The attempted deletion MUST return a validation error that enumerates (or identifies the count and types of) the blocking children so the operator knows what to clean up first. Cascade deletion is out of scope for this slice and is reserved for a future spec. Children MUST never be silently orphaned.

#### CRUD Operations

- **FR-010**: Authenticated operators MUST be able to create entities of every supported type.
- **FR-011**: Authenticated operators MUST be able to view any entity's full metadata and relationships.
- **FR-012**: Authenticated operators MUST be able to edit any mutable field on an existing entity. `id`, `createdAtUtc`, and `entityType` MUST NOT be editable.
- **FR-013**: Authenticated operators MUST be able to delete leaf entities (or entities whose children have already been removed), with explicit confirmation, subject to the block-with-children policy in FR-009. Deletion is a physical (hard) removal of the entity record. The historical record of the deletion lives in the audit trail (FR-032), not in the entity table. Restoration is not supported in this slice.
- **FR-013a**: Authenticated operators MUST be able to transition an entity's `status` between `Active` and `Deprecated` as an explicit, audited action. A `Deprecated` entity remains fully visible in browse, search, and detail experiences; the `Deprecated` state is a governance signal, NOT a hide-from-view mechanism. The UI MUST visually distinguish `Deprecated` entities so operators can see at a glance not to build on them.
- **FR-014**: The system MUST reject duplicate names within the same parent scope and environment, with a clear validation error. For **Namespace** entities (which have no parent), the uniqueness scope is `(name, environment)` — two namespaces in the same environment MUST NOT share the same `name`. For all other entity types the scope is `(parentId, environment, name)`. Duplicate detection follows Azure Service Bus's own naming rules (case-sensitivity per entity type).

#### Validation

- **FR-015**: The system MUST validate all submitted entity data before persisting: required-field presence, name format compliant with Azure Service Bus naming rules, well-formed Azure resource IDs (when present), valid environment classifications, and structural relationship integrity.
- **FR-016**: The system MUST surface validation failures with field-level messages on the UI and structured error responses on the API.
- **FR-017**: Validation rules MUST execute on both the frontend (for fast feedback) and the backend (as the authoritative gate). The frontend MUST NOT be the sole enforcement point.

#### Persistence & Concurrency

- **FR-018**: The system MUST treat the registry's primary data store as the authoritative source of truth.
- **FR-019**: The persistent store MUST guarantee that every successful create/update/delete is durable before the operation is reported as successful to the user.
- **FR-020**: The system MUST detect concurrent edits and prevent silent overwrites. On detection, the API MUST return a recoverable conflict response carrying (a) the current server-side state of the entity and (b) an identification of which fields differ from the submitter's basis version. The UI MUST present a conflict dialog with exactly two explicit actions: **Discard my changes and refresh** (abandon the submission, reload current state) and **Force overwrite** (proceed with the submitter's values). The Force-overwrite path MUST be recorded in the audit event for that update as an explicit user choice, so the resolution is traceable.
- **FR-021**: All persistence operations MUST be asynchronous and cancellable.

#### Search & Discovery

- **FR-022**: The system MUST provide a global search experience over registry entities supporting full-text matching against name, description, fully-qualified name, tags, owner, and structured metadata.
- **FR-023**: Search MUST support filtering by entity type, environment, status, and tag, and sorting by name and last-updated time. Tag filtering MUST support three forms: **key-only** (entity has any tag with this key), **value-only** (entity has any tag with this value), and **key+value** (entity has this exact key/value pair). Key matching is case-insensitive; value matching is case-sensitive.
- **FR-024**: Search MUST return paginated results with stable ordering. **Defaults**: page size 25, maximum 100; requests exceeding the maximum return 400 with a field-level validation error. **Stable-sort key** is `(relevance_score DESC, updatedAtUtc DESC, id ASC)` so repeated queries with identical inputs return identical orderings; explicit `sort=name_asc` or `sort=updated_desc` overrides the leading key and retains `id ASC` as the tiebreaker.
- **FR-025**: The system MUST keep the search index eventually consistent with the persistent store. Create, update, and delete events MUST trigger index updates. Index update failures MUST be retried; permanent failures MUST be observable in telemetry. **Operator recovery action for permanent failures in v1**: the indexer's poison handler logs a structured Error event identifying the offending entity; the operator's recovery path is to re-deploy the indexer with a mapping fix OR to perform a re-touch update (a no-op PUT against the entity) which re-emits the change-feed event. A first-class "retry index" operator action is reserved for a future ops-hardening spec.
- **FR-026**: Browse and detail experiences MUST be served from the persistent store, NOT from the search index, so they always reflect the latest committed state regardless of index lag.

#### Registry Explorer & Detail UI

- **FR-027**: The frontend MUST provide a Registry Explorer view with hierarchical tree navigation, environment indicators, entity-type icons, expand/collapse, and integrated search entry. Tree expansion MUST be **lazy-loaded server-side**: child entities under a tree node are fetched only when the node is expanded; the response uses the same continuation-token pagination shape as the list endpoint so very-wide nodes (thousands of children) page in incrementally rather than blocking the UI.
- **FR-028**: Each entity MUST have a detail page displaying its canonical metadata, tags, ownership, environment, Azure resource identifier, parent and child relationships (where applicable), and a placeholder/active audit-history section per the priorities in Story 3.
- **FR-029**: The frontend MUST provide create and edit forms for every supported entity type, with inline validation and clear submission states (saving, saved, error).
- **FR-030**: Entity deletion in the UI MUST require explicit confirmation and clearly communicate the deletion policy (block vs cascade).
- **FR-031**: The frontend MUST surface loading, empty, error, unauthorized, and **conflict** states distinctly across explorer, search, detail, and form experiences. The conflict state on edit forms MUST render the dialog defined in FR-020.

#### Audit Foundations

- **FR-032**: The system MUST record an audit event for every create, update, delete, and status-change operation, capturing actor identity, UTC timestamp, entity type, entity identifier, and a human-readable change summary. The **change summary** is a single sentence describing the action (e.g., `Created Queue 'orders-incoming' under namespace 'orders-prod'`); on `Updated` and `StatusChanged` events the audit event additionally carries a structured `fieldChanges` array of `{field, before, after}` entries so consumers can render a precise diff without re-deriving it. **Audit retention** in v1 is indefinite (no TTL on audit documents) — a future ops-hardening spec defines retention policy and archival path.
- **FR-033**: Audit events MUST be retrievable in entity-scoped form (most recent N events for entity X) for display on detail pages.
- **FR-034**: Audit data MUST be append-only from the user perspective — users MUST NOT be able to edit or delete audit events via the application.

#### Environment Awareness

- **FR-035**: Every entity MUST be classified into an environment. Browse and list operations MUST be scoped to a single environment — cross-environment browse is not supported. Cross-environment discovery is available exclusively via the search experience (FR-022), which MAY span environments when the operator omits the environment filter. The Registry Explorer MUST present an environment switcher; on first visit the UI selects the first configured environment alphabetically and persists the operator's last selection across reloads. **Environment list management in v1**: the environment list is **implicitly defined** by the set of environments currently associated with any persisted entity — there is no separate admin path to "register" an environment before use; an environment becomes available the first time an entity is written into it. Removing all entities from an environment removes it from the switcher. A first-class admin-managed environment registry is reserved for a future governance spec.
- **FR-036**: The system MUST display environment indicators prominently in the explorer, search results, and detail pages so operators never act on a prod entity thinking it is dev.

#### Security & Authorization

- **FR-037**: All registry read and write endpoints MUST require an authenticated identity, using the authentication foundation established by the earlier auth spec. In this slice ANY authenticated **tenant user** is authorized for both read and write operations — there is no role-based differentiation. The user's identity MUST still be captured on every write for the audit trail (FR-032). **"Tenant" definition**: the Microsoft Entra tenant configured in spec 003 (see [`specs/003-auth-and-identity/spec.md` §Identity Foundation](../003-auth-and-identity/spec.md)). Any principal whose access token's `tid` claim matches the configured tenant id qualifies. Role/group-gated authorization is reserved for a future governance spec. **Pre-deployment verification** (operator checklist gate): before each environment go-live, the deploying operator MUST confirm in writing that the configured Entra tenant's member population is restricted to messaging engineering personnel (the justification for the no-RBAC choice). This verification is captured in the deployment runbook under "Pre-go-live attestations".
- **FR-038**: Service-to-service authentication to Azure dependencies (data store, search service, key store) MUST prefer managed identity over secrets.
- **FR-039**: No secrets, connection strings, or credentials MUST appear in source code, build artifacts, or container images. All such values MUST be sourced from the managed secrets store at runtime.

#### Observability

- **FR-040**: The system MUST emit structured logs, distributed tracing spans, and correlation identifiers for every API request, persistence operation, and indexing operation.
- **FR-041**: The system MUST expose health endpoints suitable for orchestration platform liveness/readiness probes.
- **FR-042**: UI-originated HTTP requests MUST propagate W3C Trace Context headers (`traceparent` / `tracestate`) so frontend traces correlate with backend traces in the observability backend, regardless of which telemetry adapter is configured.

#### Performance

- **FR-043**: Registry search results MUST be returned in under one second at the 95th percentile under expected load for the target registry size.
- **FR-044**: Entity detail page load MUST complete in under 500 milliseconds at the 95th percentile under expected load.
- **FR-045**: CRUD API operations (create, update, delete, get) MUST complete in under one second at the 95th percentile under expected load.

#### Accessibility

- **FR-046**: All registry UI MUST meet WCAG 2.2 AA standards: keyboard navigability for every interactive element, semantic markup, screen-reader compatibility, visible focus states, and accessible forms with associated labels and error messages.
- **FR-047**: Color MUST NOT be the sole carrier of meaning for entity type, environment, or status indicators — text or iconography MUST also convey the information.
- **FR-048**: The UI MUST respect `prefers-reduced-motion` for any animated transitions in the explorer or detail experiences.

#### Infrastructure

- **FR-049**: All Azure resources required by this slice (persistent data store, search service, application hosting, observability ingestion, managed identities) MUST be provisioned by the project's chosen infrastructure-as-code tooling, not by manual portal actions.
- **FR-050**: Infrastructure MUST be environment-parameterized (at minimum dev, with the parameterization shaped to extend cleanly to test and prod) and use remote state suitable for CI/CD pipelines.
- **FR-051**: All Azure diagnostic logs from registry-related resources MUST route to the solution's centralized log workspace.

---

### Key Entities *(include if feature involves data)*

- **Namespace**: Represents an Azure Service Bus namespace. Carries the canonical shared fields plus the namespace's specific Azure identifiers. The root of the messaging hierarchy.
- **Queue**: Represents a queue inside a namespace. Has a single parent Namespace. Carries the canonical shared fields plus queue-specific metadata (delivery characteristics, behavior flags) captured under the extensible metadata field.
- **Topic**: Represents a topic inside a namespace. Has a single parent Namespace and zero-or-more child Subscriptions.
- **Subscription**: Represents a subscription on a topic. Has a single parent Topic and zero-or-more child Rules.
- **Rule**: Represents a subscription rule/filter. Has a single parent Subscription.
- **Audit Event**: An immutable record of one create/update/delete operation on a registry entity. Carries actor identity, UTC timestamp, entity type, entity identifier, and change summary.
- **Tag**: A free-form key/value pair attached to an entity for grouping, filtering, and governance signaling. Keys are matched case-insensitively and displayed case-preserved; values are matched and displayed case-preserved. There is no enumerated key vocabulary in this slice. Tags are not entities in their own right — they are properties of entities.
- **Environment**: A logical classification (e.g., dev, test, prod) attached to every entity. Used for partitioning, filtering, and visual scoping. Environments are configurable and not a fixed enumeration baked into the model.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A new operator, given only the URL of a freshly-deployed environment and a valid login, can register their first namespace, queue, topic, subscription, and rule and view each in the explorer in under 10 minutes total, with no documentation outside the in-app UI.
- **SC-002**: 95% of registry searches return results in under one second under expected load on a representative populated registry.
- **SC-003**: 95% of entity detail page loads complete in under 500 milliseconds under expected load.
- **SC-004**: 95% of CRUD operations complete in under one second under expected load.
- **SC-005**: After any create, update, or delete operation, the search index reflects the change in under five seconds at the 95th percentile under normal indexing-pipeline conditions.
- **SC-006**: No two operators editing the same entity concurrently can silently overwrite each other's changes — every conflicting save either succeeds with explicit conflict surfaced or fails with a recoverable error.
- **SC-007**: 100% of registry write operations produce a retrievable audit event capturing actor, timestamp, entity type, entity identifier, and change summary.
- **SC-008**: The registry UI passes automated WCAG 2.2 AA accessibility checks with zero violations on the explorer, search, detail, and form pages.
- **SC-009**: All Azure resources required to operate the registry are deployable to a clean environment by running the project's standard infrastructure-as-code apply, with no manual portal actions required.
- **SC-010**: An on-call operator can, given only a partial entity name, find the entity, its environment, its parent namespace, and its declared owner in under 30 seconds from any page in the application.
- **SC-011**: Browse, detail, and CRUD experiences continue to function correctly when the search service is temporarily unavailable, so search outages never block the registry's primary writeable path.
- **SC-012**: Every UI-originated HTTP request to a registry API endpoint propagates W3C Trace Context headers, verifiable in the observability backend by selecting any UI trace and confirming the corresponding backend spans are linked under the same trace ID.

---

## Assumptions

- **Foundations are in place**: The earlier specs in this project (branding/design foundation, solution foundation, authentication & identity, core domain model, and infrastructure baseline) are merged and deployed. This slice consumes them — it does not redefine authentication, design primitives, project layout, or the foundational infrastructure scaffolding.
- **Single-tenant scope**: This slice is single-tenant. Multi-tenant partitioning is explicitly excluded by the source artifact.
- **Source = Manual only**: All entities in this slice are operator-created via the UI. The `source = Discovered` enumeration value is reserved in the data model for a future automatic-discovery spec but is not emitted by any code path in this slice.
- **Status = Active or Deprecated only in this slice**: The `Deleted` value of the `status` enumeration is reserved in the data model for a future soft-delete spec but is not emitted in this slice. Deletion in this slice is a physical (hard) removal; restoration is not supported. `Deprecated` is an operator-driven governance signal that keeps the entity fully visible.
- **No semantic / AI search**: This slice provides keyword-based full-text search with filters. AI-assisted semantic search is reserved for a future spec.
- **No advanced RBAC**: Authorization in this slice is "any authenticated tenant user may read and write." There is no Reader/Writer/Admin role distinction. The audit trail (FR-032) captures the acting user's identity on every mutation, so traceability is preserved even without role gating. Role/ownership-based fine-grained authorization is reserved for a future governance spec, and the initial deployment is expected to be scoped to a tenant whose member population is already restricted to messaging engineering personnel.
- **No CLI**: This slice is delivered via the web UI and a documented API only. A CLI is reserved for a future spec.
- **Environment list is configurable, not hard-coded**: The environment classification field accepts a configurable list of environment values; the application does not bake `dev/test/prod` into source code as a closed enumeration.
- **English-only content, RTL-safe foundation**: Per project convention, v1 content is English; layouts are built RTL-safely using logical CSS properties, but no translation pipeline is shipped in this slice.
- **Dark mode is primary**: Per project convention, the registry UI is built dark-first with light as a fully-supported peer theme.
- **Browser support**: Per project convention, the registry UI targets the last two major versions of Chrome, Edge, Firefox, and Safari on desktop, plus iPadOS Safari and Android Chrome.
- **Telemetry contains no PII by default**: Only correlation identifiers and structural metadata propagate; user content is not added to telemetry payloads in this slice.
- **Operational scale target**: Performance targets in this spec assume an "expected load" of a few hundred concurrent operators and registry sizes in the tens of thousands of entities per environment. Targets for much larger scales are deferred to a future spec.
- **No formal SLO in v1**: The registry ships best-effort with **no formal SLO** (no uptime %, RTO, or RPO commitments). Underlying Azure platform SLAs apply (Cosmos DB 99.999%, AI Search 99.9% on basic SKU, Container Apps 99.95%). A formal SLO with explicit recovery objectives is reserved for a future ops-hardening spec.
- **Encryption — inherited**: Data-at-rest encryption (Cosmos DB platform-managed keys), data-in-transit encryption (TLS 1.2+ on all Azure endpoints), and Key Vault secret protection are inherited from spec 005's infrastructure baseline (see [`specs/005-infrastructure-baseline/spec.md` §FR-019–FR-023, §FR-041–FR-042](../005-infrastructure-baseline/spec.md)). This slice does NOT redefine those controls.
- **Data residency — inherited**: Region selection is inherited from spec 005's per-environment configuration (dev `eastus2` today; test/prod region pinned at provisioning per the spec-005 env template). Compliance-driven residency constraints (PCI, HIPAA, etc.) are not addressed in v1; a future compliance spec defines region pinning and audit-trail residency.
- **Prior-spec section references**: This slice consumes the following section-level commitments from prior specs:
  - **003 Auth & Identity** — Microsoft Entra ID JWT bearer validation (§Identity Foundation), `IPlatformPrincipalAccessor` (§Platform Principal), the role-permission matrix (`specs/003-auth-and-identity/contracts/role-permission-matrix.md`) as the binding contract this slice DEVIATES from per FR-037 (deviation documented in `plan.md` Complexity Tracking #2).
  - **004 Core Domain Model** — `CosmosClientFactory`, `AzureCredentialFactory`, `JsonResourceSerializer`, and the ETag-based concurrency pattern (§Persistence). The canonical domain entity model is NOT reused; spec 006 introduces a parallel data plane (documented in `plan.md` Complexity Tracking #1).
  - **005 Infrastructure Baseline** — every IaC output in [`specs/005-infrastructure-baseline/contracts/outputs-contract.md`](../005-infrastructure-baseline/contracts/outputs-contract.md) (Cosmos endpoint, AI Search endpoint, CAE id, workload UAMI, App Insights binding, LAW destination); the BT-IAC-001..007 policy gates; the spec-005 Q5c `allLogs`-only diagnostic convention; the spec-005 pipeline-MI RBAC-Admin allowlist (which already permits the AI Search role GUIDs needed by this slice).
- **Pre-go-live verification**: The deploying operator MUST complete the FR-037 tenant-population verification before each environment activation, captured as an attestation in the deployment runbook.

---

## Non-Goals (Explicit Out-of-Scope)

These capabilities are explicitly excluded from this slice and MUST NOT shape current design. They MAY be addressed by future specs.

- **Automatic Azure discovery**: No background scanning of Azure subscriptions to auto-register namespaces/queues/topics. `source = Discovered` is reserved in the data model but never emitted.
- **AI-assisted / semantic search**: Search is keyword + filter only.
- **Documentation generation**: No auto-summarization of entities or topology.
- **Ownership governance workflows**: No approvals, assignments, or escalation flows on the `owner` field.
- **CLI tooling**: Web UI + REST API only.
- **Deep operational telemetry / dashboards**: No registry-specific dashboards in this slice; LAW + App Insights raw query access is the operator surface.
- **Advanced RBAC**: No Reader/Writer/Admin role distinction; see FR-037.
- **Multi-cloud / non-Azure broker support**: Azure Service Bus shapes only; the data model preserves `brokerKind` for forward compatibility but no other broker types are recognized.
- **Multi-tenant SaaS partitioning**: Single-tenant per deployed environment.
- **Bulk operations**: No multi-select edit, no multi-delete, no bulk import/export, no batch tagging. Every mutation is a single-entity request. Bulk operations are reserved for a future productivity spec.
- **Cascade delete**: Per Edge Case "Orphaned children", delete-with-children is BLOCKED, not cascaded.
- **Soft delete / restoration**: Hard delete only (FR-013). Restoration is not supported; the audit trail is the historical record.
- **Cross-environment browse**: Browse and list are env-scoped (FR-035); cross-env discovery is via search only.
- **First-class index-retry operator action**: Permanent index failures recovered via re-deploy or re-touch update (FR-025); a "retry index" button is reserved for a future ops-hardening spec.
- **Formal SLO with RTO/RPO**: v1 is best-effort (see Assumptions).
- **Audit retention policy**: Indefinite retention in v1; archival/TTL reserved for future ops-hardening spec.
- **Compliance-driven data residency / scoped regions**: Region selection is per-environment infra config; no compliance-binding controls in v1.
- **Admin-managed environment registry**: Environment list is implicitly defined by entity writes (FR-035); first-class admin path reserved for future governance spec.
