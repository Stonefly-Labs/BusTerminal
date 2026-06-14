# Feature Specification: Namespace Onboarding

**Feature Branch**: `008-namespace-onboarding`

**Created**: 2026-06-14

**Status**: Draft

**Input**: User description: "008 Namespace Onboarding — introduce a guided workflow to onboard existing Azure Service Bus namespaces into BusTerminal as authoritative registry entities, with structured ownership, Azure connectivity/permission validation, and lifecycle management (no asset discovery, no provisioning, no sync)."

---

## Overview

Azure Service Bus namespaces are the root organizational boundary in BusTerminal. Spec 006 established the Service Bus Registry Core, which allows an operator to manually create a `Namespace` record with a single free-form `owner` string and a `(name, environment)` uniqueness scope. That registration is a flat metadata record — it does not verify that an actual Azure resource exists at the declared `azureResourceId`, it does not capture structured ownership across primary/secondary/steward/support roles, and it does not distinguish operational lifecycle (active vs disabled vs archived) from governance signal (active vs deprecated).

This spec introduces **Namespace Onboarding** — a guided, validated workflow that elevates Namespace from "any string an operator types in a form" to an *authoritative registry entity* whose existence in BusTerminal has been Azure-verified, Entra-backed, and explicitly attested by an administrator. Onboarding is the platform-adoption entry point: a namespace must be onboarded before any future Service Bus asset discovery, governance, synchronization, or cross-environment comparison capability can hang off it.

The slice intentionally excludes Azure resource provisioning, asset discovery (queues/topics/subscriptions/rules/schemas), scheduled synchronization, drift detection, compliance scoring, policy enforcement, approval workflows, and bulk onboarding — every one of those is reserved for a later spec and must not shape current design.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Onboard an Azure Service Bus namespace via the guided wizard (Priority: P1)

A platform administrator responsible for messaging infrastructure has just stood up (or already operates) an Azure Service Bus namespace and needs BusTerminal to treat it as a managed registry asset. They want a single guided path that asks for the Azure Resource ID, lets them attest the business and ownership context, *actually verifies BusTerminal can reach the namespace and has the permissions it needs*, and either records the namespace as Active or surfaces a clear failure before anything is persisted.

**Why this priority**: Without an onboarding wizard with real validation, BusTerminal cannot tell the difference between a namespace it can govern and a typo. This is the foundational workflow for every future capability — discovery, governance, sync — and is the minimum slice that delivers user-facing value beyond what 006 already provides.

**Independent Test**: An authenticated administrator can sign in, open Namespace Onboarding, paste a valid ARM Resource ID, fill in metadata and ownership across the wizard steps, run validation, and see the namespace appear as `Active` in the Namespace Inventory with a green validation badge. With an *invalid* or *inaccessible* ARM ID, validation fails clearly in step 4 and no namespace record is created until issues are resolved.

**Acceptance Scenarios**:

1. **Given** an administrator on an empty namespace inventory, **When** they paste a valid Service Bus ARM Resource ID into step 1, fill metadata in step 2, assign at least one primary owner from Entra ID in step 3, run validation in step 4 (all checks green), and confirm step 5, **Then** the namespace appears in the inventory with status `Active`, validation status `Healthy`, and `source = Onboarded`, and persistence survives a page reload.
2. **Given** an administrator pasting a syntactically invalid Azure Resource ID (wrong format, wrong resource provider, or wrong resource type) in step 1, **When** they attempt to advance, **Then** the wizard surfaces an inline validation error identifying the specific defect (e.g., "Expected `Microsoft.ServiceBus/namespaces`, got `Microsoft.EventHub/namespaces`") and the wizard does not advance.
3. **Given** an administrator who has reached step 4 with a syntactically valid ARM ID for a namespace BusTerminal's managed identity cannot reach (resource missing, subscription not granted, or RBAC missing), **When** validation runs, **Then** each failed check (Existence, Accessibility, Required Permissions, Identity Authorization, API Reachability) is shown individually with a human-readable reason and remediation hint, and the **Register** button on step 5 is disabled until at least Existence + Accessibility pass.
4. **Given** an administrator attempting to onboard a namespace whose ARM Resource ID is already onboarded (case-insensitive match), **When** they advance from step 1, **Then** the wizard rejects the attempt with a clear "already onboarded" message linking to the existing namespace's detail page.
5. **Given** an administrator in step 5 reviewing the captured details, **When** they click **Back** to any earlier step, edit a field, and return, **Then** the wizard preserves all other state and re-runs validation only if validation-relevant fields changed.
6. **Given** the administrator cancels mid-wizard by closing the dialog or navigating away, **When** they return, **Then** no partial namespace record exists in the inventory and no audit event was emitted for an aborted onboarding.

---

### User Story 2 — Browse, search, and inspect onboarded namespaces (Priority: P2)

An on-call engineer or messaging architect needs to find an onboarded namespace by partial name or business unit, see its ownership chain at a glance, drill into its full metadata, and verify its current operational and validation status without leaving the application.

**Why this priority**: Once even one namespace is onboarded, operators need a place to find it again. This story is what turns onboarding from a write-once action into an ongoing operational surface. It depends on Story 1 (namespaces must exist to be browsed) but is independently valuable — once shipped, every administrator can answer "what namespaces do we manage, who owns them, and are they healthy?" in seconds.

**Independent Test**: With at least three onboarded namespaces across environments, an administrator can open the Namespace Inventory, filter by environment, search by partial display name, sort by last-validated time, click a row, see the Namespace Details page with all captured metadata, ownership, Azure identifiers, validation results, lifecycle status, and a recent audit summary.

**Acceptance Scenarios**:

1. **Given** an inventory containing onboarded namespaces across multiple environments, **When** the administrator opens the inventory, **Then** they see a table of namespaces with display name, environment, lifecycle status, validation status, primary owner, region, and last-validated timestamp — sortable by each column and paginated server-side.
2. **Given** the inventory, **When** the administrator types a partial display name or business unit into the search field, **Then** matching namespaces appear in under one second at p95, with the active filters chip-listed above the table.
3. **Given** an administrator clicks a namespace row, **When** the Namespace Details page renders, **Then** the page shows: business metadata, ownership assignments (with Entra identity display names resolved), Azure identifiers, latest validation results per check, current lifecycle status, environment badge, tags, notes, and an audit summary of the most recent lifecycle/metadata/ownership changes.
4. **Given** an administrator filters by `lifecycleStatus = Disabled`, **When** the inventory refreshes, **Then** only disabled namespaces appear and the lifecycle filter is reflected in the URL so the view is shareable.
5. **Given** an administrator views the details of a namespace whose last validation run failed, **When** the page renders, **Then** the validation section visually distinguishes failed checks from passed checks (color is not the sole cue — icon and text also convey state) and offers a **Re-run validation** action.

---

### User Story 3 — Manage namespace lifecycle and update metadata over time (Priority: P3)

An administrator needs to keep onboarded namespaces accurate as the business changes: rename the display name, reassign ownership when a team reorganizes, mark a namespace as `Disabled` during a planned decommission window without forgetting it exists, re-enable it after restoration, archive it when permanently retired, and re-run connectivity validation on demand to confirm BusTerminal's access still works.

**Why this priority**: Onboarding is a moment-in-time event; namespaces drift, teams change, environments roll. Lifecycle and metadata management is what makes the registry a durable system of record rather than a snapshot. Critical for the long-term vision but not blocking first-use value — operators can survive the first weeks with onboard + browse alone.

**Independent Test**: An administrator can edit any mutable field on a namespace details page (display name, description, business unit, tags, notes, owners/stewards/support contacts), transition lifecycle status (Active → Disabled → Active → Archived), and trigger an on-demand validation run. Every action is reflected in the audit log on the details page with actor, UTC timestamp, and a human-readable change summary.

**Acceptance Scenarios**:

1. **Given** an active onboarded namespace, **When** the administrator opens the edit form, modifies the description, primary owner, and tags, and saves, **Then** the changes persist, `updatedAtUtc` advances, the audit log records an `Updated` event with field-level before/after diffs, and the Azure-identifier fields (`azureResourceId`, `subscriptionId`, `tenantId`, `region`) are not editable.
2. **Given** an active namespace, **When** the administrator transitions its lifecycle status to `Disabled` (with a required reason note), **Then** the namespace remains visible in inventory with a disabled badge, the audit log records the transition with the supplied reason, and the **Re-run validation** action is disabled while in `Disabled` state.
3. **Given** a disabled namespace, **When** the administrator transitions it back to `Active`, **Then** the audit log records the transition and validation is automatically re-run; the namespace's `validationStatus` reflects the fresh outcome.
4. **Given** an active or disabled namespace, **When** the administrator transitions it to `Archived` with confirmation, **Then** the namespace is hidden from the default inventory view, remains accessible via an "Include archived" toggle, becomes read-only (no further metadata edits, no further lifecycle transitions other than restore-to-Disabled), and the audit log records the archival.
5. **Given** an active namespace, **When** the administrator clicks **Re-run validation**, **Then** the validation runs synchronously with a progress UI showing each check in flight, results are persisted, the details page updates, and the audit log records a `ValidationExecuted` event with the outcome of each individual check.
6. **Given** an attempt by an authenticated user *without* the namespace-administrator role to perform any mutate or lifecycle action, **When** they hit the API directly or click an action in the UI, **Then** the API responds 403 and the UI hides or disables the action with a clear "requires administrator role" affordance.

---

### Edge Cases

- **ARM resource ID normalization**: Operators paste ARM IDs with varying casing for the subscription GUID, resource group name, and namespace name. The system MUST canonicalize Azure Resource IDs case-insensitively for duplicate detection, while preserving the original casing the operator supplied for display.
- **Cross-tenant ARM ID**: An administrator pastes an ARM ID whose `tid` (tenant) does not match BusTerminal's configured Entra tenant. The system MUST reject the onboarding in step 1 with a "namespace tenant differs from BusTerminal tenant" message; cross-tenant onboarding is reserved for a future spec.
- **Validation timeout**: A validation check (e.g., ARM management plane reachability) does not respond within a bounded budget. The check MUST fail with a "timed out — Azure API did not respond within N seconds" outcome rather than hanging the wizard indefinitely; the user may **Retry** that specific check without restarting the wizard.
- **Entra identity no longer resolvable**: A user assigned as primary owner is later removed from Entra ID (or their `objectId` becomes unresolvable). The Namespace Details page MUST surface the assignment with a "User no longer resolvable in directory" warning rather than failing to load, and the administrator MUST be able to reassign without losing the rest of the ownership record.
- **Partial validation success on re-onboarding attempt**: A previously-onboarded namespace was deleted from Azure but the BusTerminal record exists in `Active` state. The next `Re-run validation` MUST detect this (Existence check fails), set `validationStatus = Unhealthy`, and surface the discrepancy in the inventory's validation badge — the BusTerminal record is NOT auto-archived; lifecycle decisions remain explicit.
- **Concurrent edits**: Two administrators open the same namespace's edit form, both edit it, both save. The first save succeeds; the second receives the FR-020-style refresh-or-overwrite conflict response established in spec 006 — the same UX pattern applies here.
- **Same display name, different namespaces**: Two ARM IDs for *different* Azure namespaces both classified by the operator as `prod` with the same display name. The system MUST permit this — duplicate detection is on ARM Resource ID, not display name; two prod namespaces can legitimately exist with the same human-facing display.
- **Lifecycle action on archived namespace**: An administrator attempts to edit metadata, change ownership, or re-run validation on an Archived namespace. The system MUST block the action with a clear "namespace is archived; restore it first" message; the only permitted transition from Archived is `Restore` back to `Disabled` for explicit reconsideration.
- **Region or subscription drift detected during validation**: A re-run validation discovers the Azure resource is now in a different region or subscription than was recorded at onboarding. The system MUST surface a "metadata drift" warning on the details page identifying the drifted fields; auto-update of Azure-identifier fields is NOT performed in this slice (drift reconciliation is reserved for a future sync spec).
- **Missing optional metadata**: A namespace is onboarded with only required fields (Azure Resource ID, display name, environment, primary owner). The details page MUST render cleanly with empty-state placeholders for optional sections (description, business unit, cost center, tags, notes, secondary owners, stewards, support contacts).

---

## Requirements *(mandatory)*

### Functional Requirements

#### Onboarding Workflow

- **FR-001**: The system MUST provide a guided five-step Namespace Onboarding workflow: (1) Namespace Identification, (2) Business Metadata, (3) Ownership, (4) Validation, (5) Review & Register. The wizard MUST support **Back** navigation that preserves all entered state and MUST be safely cancellable at any step without leaving a partial namespace record or emitting an audit event.
- **FR-002**: The system MUST persist the namespace record only after the administrator confirms step 5. Prior to step 5 confirmation, no `Namespace` document, no audit event, and no validation-result record MUST be written to durable storage. Transient wizard state MAY be held in browser session only.
- **FR-003**: The wizard MUST re-run validation when, on revisit to step 4 via **Back**, any validation-relevant field has changed since the prior validation run (Azure Resource ID is the only such field in this slice). Otherwise the prior validation result MUST be presented unchanged.

#### Identification & Azure Metadata

- **FR-004**: The system MUST require an Azure Resource ID matching the canonical Service Bus ARM format `/subscriptions/{subscriptionId}/resourceGroups/{rgName}/providers/Microsoft.ServiceBus/namespaces/{namespaceName}`. The system MUST reject IDs of other resource types (e.g., Event Hubs, Relay) or malformed segments with a field-level error identifying the defect.
- **FR-005**: The system MUST extract and persist, from the validated Azure Resource ID and (where step 4 validation succeeds) the ARM management plane: `subscriptionId`, `subscriptionName` (best-effort resolved from ARM at onboarding time; nullable if unresolvable), `resourceGroup`, `tenantId`, `namespaceName`, and `region`. These six fields MUST be read-only after onboarding — drift is surfaced (FR-029) but never silently overwritten.
- **FR-006**: The system MUST reject onboarding when the parsed `tenantId` does not match BusTerminal's configured Entra tenant.
- **FR-007**: The system MUST treat the Azure Resource ID as the uniqueness scope for namespace onboarding — duplicate detection is case-insensitive on the full ARM ID. The `(displayName, environment)` pair MAY duplicate (no false-positive blocks on display-name collisions).

#### Business Metadata

- **FR-008**: The system MUST allow capture of: `displayName` (required, 1–200 chars), `description` (optional, ≤ 4000 chars), `environment` classification (required; consumes the same environment model as spec 006's FR-035 — implicitly defined by the set of environments referenced by any persisted entity), `businessUnit` (optional, free-form ≤ 200 chars), `productOrApplication` (optional, free-form ≤ 200 chars), `costCenter` (optional, free-form ≤ 100 chars), `tags` (optional key/value pairs per spec 006 FR-002 semantics — case-insensitive key match, case-preserving display; max 50 tags), and `notes` (optional free-form ≤ 4000 chars).
- **FR-009**: `displayName` MUST default in step 2 to the Azure `namespaceName` extracted in step 1 but MUST be independently editable. `displayName` is the human-facing identifier; `namespaceName` is the Azure-truth identifier.

#### Ownership Metadata

- **FR-010**: The system MUST support four ownership roles: `primaryOwner` (exactly one; required), `secondaryOwners` (zero or more), `technicalStewards` (zero or more), `supportContacts` (zero or more).
- **FR-011**: Each ownership assignment MUST reference an Entra ID principal (user or group) by stable identifier (`objectId`) and MUST capture a display-time `displayName` snapshot for resilience against Entra-side renames; on render, the system SHOULD attempt to re-resolve the live display name from the Entra integration established in spec 003 and fall back to the snapshot when unresolvable.
- **FR-012**: Ownership assignments MUST NOT permit raw email addresses or free-form names — the model is Entra-backed only. (Free-form `owner` strings on the spec 006 `Namespace` document MAY remain for backward compatibility but are not editable through this slice's UI; new onboardings MUST populate the structured ownership fields.)
- **FR-013**: The Entra picker in step 3 MUST support search by display name and email and MUST distinguish users from groups visually.

#### Connectivity & Authorization Validation

- **FR-014**: The validation step MUST execute five named checks against the supplied Azure Resource ID, each individually reported with `Pass | Fail | Skipped` and a human-readable reason: **Existence** (ARM `GET` returns the namespace resource), **Accessibility** (ARM call succeeds without auth error), **RequiredPermissions** (BusTerminal's managed identity holds at least an Azure Service Bus reader-equivalent role on the namespace scope), **IdentityAuthorization** (token exchange completes; managed identity is correctly federated), **ApiReachability** (Service Bus management endpoint responds to a lightweight metadata probe).
- **FR-015**: Validation MUST execute synchronously when invoked from the wizard (step 4) or from the details page (`Re-run validation`), with a bounded per-check timeout. The aggregate UX MUST present per-check progress and complete in under 15 seconds at p95 under normal Azure ARM responsiveness.
- **FR-016**: The system MUST persist each validation execution as a `ValidationRun` record carrying: `runId`, `executedAtUtc`, `executedBy` (Entra `objectId`), `azureResourceIdAtRun` (the ID validated), per-check outcomes, and an aggregate `validationStatus` of `Healthy` (all checks pass), `Degraded` (at least one non-fatal check fails; Existence + Accessibility still pass), or `Unhealthy` (Existence or Accessibility fails). The namespace's `validationStatus` field MUST mirror the latest run's aggregate.
- **FR-017**: Service-to-service authentication to Azure for validation MUST use managed identity. Connection strings, SAS tokens, and other Service Bus credentials MUST NOT be requested from the operator, stored, or referenced anywhere in this slice's surface or persistence.

#### Inventory & Details UI

- **FR-018**: The system MUST provide a Namespace Inventory view listing onboarded namespaces with: display name, environment, lifecycle status, validation status, primary owner display name, region, last-validated UTC timestamp. The view MUST support server-side filtering by environment, lifecycle status, validation status, and tag (per spec 006 FR-023 semantics); sorting by display name, environment, lifecycle status, validation status, and last-validated time; and partial-name search across display name and business unit.
- **FR-019**: The Inventory MUST hide `Archived` namespaces by default and MUST expose an explicit toggle to include them.
- **FR-020**: The system MUST provide a Namespace Details page displaying every captured field (business metadata, structured ownership with re-resolved display names, Azure identifiers, latest per-check validation results, current lifecycle status, environment badge, tags, notes) plus an audit summary panel showing the most recent N lifecycle, metadata, ownership, and validation events for that namespace.
- **FR-021**: All Inventory and Details surfaces MUST be served from the persistent store (not from the spec-006 search index), so they always reflect the latest committed state regardless of indexing lag.

#### Lifecycle Management

- **FR-022**: The system MUST model `lifecycleStatus` with values `PendingValidation`, `Active`, `Disabled`, `Archived` (operational axis), independent of spec 006's `status` field (`Active`/`Deprecated` — governance axis). A namespace MAY simultaneously be `lifecycleStatus = Active` and `status = Deprecated`.
- **FR-023**: Permitted lifecycle transitions are: `PendingValidation → Active` (on first successful step-5 register), `Active ⇄ Disabled` (administrator action with required reason note), `Active|Disabled → Archived` (administrator action with confirmation), `Archived → Disabled` (administrator restore action). Any other transition MUST be rejected.
- **FR-024**: Transition to `Disabled` MUST be permitted regardless of validation status. Transition from `Disabled → Active` MUST automatically trigger a validation run; the transition completes regardless of validation outcome (validation result is recorded but does not block the lifecycle transition itself).
- **FR-025**: While `Archived`, the namespace MUST be read-only — metadata edit, ownership change, re-run validation, and re-archive MUST all be rejected. Only restore-to-Disabled is permitted.
- **FR-026**: Destructive (physical) deletion of an onboarded namespace MUST NOT be exposed in this slice. The `Archived` state is the terminal "removed from active use" position; physical-delete tooling is reserved for a future ops-hardening or compliance spec.

#### API Surface

- **FR-027**: The system MUST expose an HTTP API consistent with the BusTerminal Minimal API + Vertical Slice conventions used in prior specs, supporting: namespace onboarding (POST), retrieval (GET by id, GET by ARM ID), listing with filter/sort/search (GET), update of mutable fields (PUT/PATCH), structured ownership update (PUT/PATCH), lifecycle transition (POST action), and validation execution (POST action). The OpenAPI document MUST cover every operation.
- **FR-028**: Listing responses MUST be paginated with stable ordering per the same pattern as spec 006 FR-024 (default page size 25, max 100, stable secondary sort by `id ASC`).
- **FR-029**: The API MUST expose drift information when a re-run validation detects a region or subscription mismatch against the persisted record. Drift MUST be surfaced in the response payload (and the Details page) but MUST NOT mutate the persisted Azure-identifier fields automatically.

#### Audit

- **FR-030**: The system MUST emit audit events for: `NamespaceOnboarded`, `NamespaceMetadataUpdated`, `NamespaceOwnershipUpdated`, `NamespaceLifecycleTransitioned`, `NamespaceValidationExecuted`. Each event MUST carry actor identity (Entra `objectId`), actor display name snapshot, UTC timestamp, namespace id, event type, and a human-readable change summary. `Updated` events MUST additionally carry a structured `fieldChanges` array of `{field, before, after}` per spec 006 FR-032. Lifecycle transitions MUST carry the supplied reason note.
- **FR-031**: Audit events MUST be append-only from the user perspective. They MUST be retrievable in namespace-scoped form (most recent N events for namespace X) for display on the Details page and MUST follow spec 006's retention model (indefinite in v1).

#### Security & Authorization

- **FR-032**: Namespace onboarding, metadata update, ownership change, lifecycle transition, and on-demand validation execution MUST all require the **namespace-administrator** role. Any authenticated tenant user (per spec 006 FR-037) MAY read the inventory and details views. This is a deliberate divergence from spec 006's "any authenticated tenant user may read and write" stance — onboarding decisions carry governance weight and MUST be gated. The role MUST be backed by an Entra App Role granted via Enterprise App assignment (consistent with spec 003's identity foundation) and surfaced as an `IsNamespaceAdministrator()` check on the platform principal accessor.
- **FR-033**: Service-to-service authentication to Azure resources (Cosmos, AI Search, ARM, Service Bus management plane) MUST prefer managed identity per spec 005's baseline. No Service Bus connection strings or SAS tokens MUST be requested, stored, transmitted, or logged at any point in this slice.
- **FR-034**: The pre-go-live tenant-population attestation defined in spec 006 FR-037 remains in effect; this slice does NOT relax it. The namespace-administrator role is an additional gate on top of, not a replacement for, that attestation.

#### Observability

- **FR-035**: The system MUST emit structured logs, distributed tracing spans, and correlation identifiers for: namespace onboarding, validation execution (per check), authorization failures (403 paths), and lifecycle transitions. Validation telemetry MUST carry `azureResourceId` and per-check outcome but MUST NOT carry secrets, tokens, or ARM response bodies.
- **FR-036**: UI-originated HTTP requests to the namespace API MUST propagate W3C Trace Context headers (`traceparent`/`tracestate`) per spec 006 FR-042 / project convention. This requirement applies regardless of which telemetry adapter is configured at runtime.

#### Performance

- **FR-037**: Namespace inventory list and search MUST return results in under one second at p95 under expected load (hundreds of onboarded namespaces per environment).
- **FR-038**: Namespace details page load MUST complete in under 500 milliseconds at p95 under expected load.
- **FR-039**: The synchronous validation run MUST complete in under 15 seconds at p95 under normal ARM responsiveness (per FR-015). Hard timeout per check MUST be enforced so a slow Azure response cannot exceed the budget.

#### Accessibility

- **FR-040**: All namespace onboarding, inventory, details, and lifecycle UI MUST meet WCAG 2.2 AA per project convention: full keyboard navigability, semantic markup, screen-reader compatibility, visible focus states, accessible forms with associated labels and error messages, and respect for `prefers-reduced-motion` for any wizard step transitions or validation progress animations.
- **FR-041**: Validation status, lifecycle status, and environment indicators MUST NOT rely on color alone — text and iconography MUST also convey the state.

#### Infrastructure

- **FR-042**: No new Azure runtime resources are expected for this slice — the persistence, search, observability, and managed-identity scaffolding from spec 005 carry over. If the role-assignment surface needs extending (e.g., a new Entra App Role definition, additional managed-identity role grants on subscription scope for ARM read), those MUST be expressed in OpenTofu and applied via the same CI/CD pipeline path used by spec 005 — no manual portal actions.
- **FR-043**: Any added Azure resource role assignments (e.g., granting BusTerminal's workload identity scoped reader rights against operator-supplied subscriptions) MUST be parameterized per environment and MUST follow the spec 005 BT-IAC-001..007 policy gates and the spec-005 Q5c `allLogs`-only diagnostic convention where applicable.

---

### Key Entities

- **OnboardedNamespace** (extends spec 006's `Namespace`): The first-class registry entity representing an Azure Service Bus namespace that has completed onboarding. Carries the canonical spec-006 shared fields plus: `subscriptionName`, `tenantId`, `region`, `businessUnit`, `productOrApplication`, `costCenter`, `notes`, `lifecycleStatus`, `validationStatus`, `lastValidationRunId`, `lastValidatedAtUtc`, `source = "Onboarded"`, plus the structured `ownership` block (primary/secondary/stewards/support). `source = "Manual"` records from spec 006 remain readable but are NOT considered "onboarded" — they appear in the spec-006 Registry Explorer but do NOT appear in the new Namespace Inventory introduced by this spec.
- **OwnershipAssignment**: A structured reference to an Entra ID principal in one of four roles (primary, secondary, steward, support). Carries `role`, `principalType` (`User` | `Group`), `objectId`, `displayNameSnapshot`, `assignedAtUtc`, `assignedBy`. Not a stand-alone entity — composed into OnboardedNamespace.
- **ValidationRun**: An immutable record of one validation execution for an OnboardedNamespace. Carries `runId`, `namespaceId`, `executedAtUtc`, `executedBy`, `azureResourceIdAtRun`, `aggregateStatus` (`Healthy` | `Degraded` | `Unhealthy`), and a `checkResults` collection of `{name, outcome, reason, durationMs}` for the five named checks (FR-014). Retained in append-only form alongside audit events.
- **NamespaceAuditEvent**: An immutable record of one onboarding-related action (per FR-030). Carries actor, UTC timestamp, namespace id, event type, change summary, reason note (lifecycle transitions only), and structured `fieldChanges` (updates only).
- **OwnershipPrincipalReference**: A logical reference shape used by ownership assignments to point at an Entra ID user or group. Resolved at render time via the Entra integration; falls back to `displayNameSnapshot` when unresolvable. Not a registry entity in its own right.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A new administrator, given only the URL of a freshly-deployed BusTerminal environment and a valid login, can onboard their first Azure Service Bus namespace end-to-end (wizard step 1 through registered) in under 5 minutes, with no documentation outside the in-app UI.
- **SC-002**: 95% of namespace inventory list/search requests return results in under one second under expected load.
- **SC-003**: 95% of namespace details page loads complete in under 500 milliseconds under expected load.
- **SC-004**: 95% of synchronous validation runs complete in under 15 seconds under normal ARM responsiveness.
- **SC-005**: 100% of namespace onboarding, metadata update, ownership update, lifecycle transition, and validation execution actions produce a retrievable audit event capturing actor, UTC timestamp, namespace id, event type, and change summary (or reason note, on lifecycle).
- **SC-006**: 0% of onboarding attempts that fail the Existence or Accessibility validation check result in a persisted `Active` namespace record — failed onboardings either remain at `PendingValidation` or are not registered at all, never silently active.
- **SC-007**: No Service Bus connection string, SAS token, or other namespace-scoped credential appears in source code, build artifacts, container images, request payloads, response payloads, persisted documents, logs, traces, or metrics at any time in this slice's surface.
- **SC-008**: The namespace onboarding UI passes automated WCAG 2.2 AA accessibility checks with zero violations on the wizard (all five steps), inventory, details, edit, and lifecycle-action surfaces.
- **SC-009**: An on-call administrator can, given only the partial display name or business unit of an onboarded namespace, locate the namespace, identify its primary owner and support contact, and determine its current lifecycle and validation status in under 30 seconds from any page in the application.
- **SC-010**: Every UI-originated HTTP request to a namespace API endpoint propagates W3C Trace Context headers, verifiable by selecting any UI trace in Azure Monitor and confirming the corresponding backend spans are linked under the same trace ID.
- **SC-011**: Browse, search, details, and lifecycle/edit operations continue to function correctly when the synchronous validation path is temporarily unavailable (Azure ARM degradation) — operators can still inspect, search, and lifecycle-manage records; only on-demand revalidation is affected.
- **SC-012**: Every authenticated user *without* the namespace-administrator role receives a 403 from every mutate/lifecycle/validation endpoint, and the UI hides or disables every corresponding action, with zero false-positive write attempts reaching the persistence layer.

---

## Assumptions

- **Foundations are in place**: Specs 001 (brand/design), 002 (solution foundation), 003 (auth & identity), 004 (core domain model), 005 (infrastructure baseline), 006 (Service Bus Registry Core), and 007 (Playwright auth fixture) are merged and deployed. This slice consumes them and does NOT redefine any foundation primitive.
- **OnboardedNamespace extends spec 006's `Namespace`, in place — not a parallel entity**: This slice elevates the existing `Namespace` document with the new fields rather than introducing a separate "OnboardedNamespace" Cosmos partition. Spec 006's manually-registered namespaces (`source = "Manual"`) remain readable in spec 006's Registry Explorer; they do NOT appear in the new Namespace Inventory introduced by this spec, which lists `source = "Onboarded"` records only. A future reconciliation spec MAY migrate spec-006 manual namespaces into the onboarded model.
- **Two-axis status model**: `lifecycleStatus` (operational: PendingValidation/Active/Disabled/Archived) is orthogonal to spec 006's `status` (governance: Active/Deprecated). The two axes coexist; this spec introduces the operational axis and does NOT modify the governance axis.
- **Source = "Onboarded" only in this slice**: The `source` enumeration is extended with `Onboarded`. `Discovered` remains reserved (spec 006 assumption) and is not emitted by any code path.
- **No asset discovery, no provisioning, no sync**: This slice creates *namespace records*, not Azure resources, and does not enumerate queues/topics/subscriptions/rules/schemas. Those are explicitly excluded by the source description and reserved for later specs.
- **Synchronous inline validation only**: Validation runs synchronously in the wizard step 4 and on-demand from the details page, bounded by per-check timeouts. Background scheduled re-validation, drift reconciliation, and asynchronous validation queues are reserved for a future ops/sync spec.
- **Entra-backed ownership only for new onboardings**: The structured `OwnershipAssignment` model is mandatory for any new namespace going through this spec's wizard. Spec 006's free-form `owner` string is preserved on legacy records for backward compatibility but is NOT writable through this slice's UI.
- **Namespace-administrator role is a new Entra App Role**: This slice introduces an additional Entra App Role, granted via Enterprise App assignment per spec 003's identity foundation. The pre-go-live tenant-population attestation from spec 006 FR-037 remains in effect.
- **Region selection is operator-classified, not derived from operator geography**: The `region` field stored on an onboarded namespace is taken from the Azure resource (via ARM at validation time), not from the operator's browser locale or IP. Operators in any geography may onboard namespaces in any region they have access to.
- **No SLO on validation success rate**: Validation outcomes depend on Azure ARM responsiveness and external factors outside BusTerminal's control. v1 ships best-effort with no SLO commitment on the *success rate* of validation runs; only the *latency* of the inline path is bounded (FR-039, SC-004).
- **English-only content, RTL-safe foundation; dark mode primary; browser support per project convention** — all inherited from spec 006's assumptions and the project conventions in CLAUDE.md.
- **Telemetry contains no PII or secrets by default**: ARM Resource IDs and Entra `objectId`s are correlation identifiers, not PII; they may appear in telemetry. Operator-supplied free-form fields (notes, descriptions, tags) MUST NOT propagate into telemetry payloads.
- **Drift surfacing only, no drift reconciliation**: Region/subscription/resource-group drift detected during re-validation is *surfaced* on the Details page but never auto-corrected. Reconciliation is reserved for a future sync spec.

---

## Non-Goals (Explicit Out-of-Scope)

These capabilities are explicitly excluded from this slice and MUST NOT shape current design. They MAY be addressed by future specs.

- **Topic / Queue / Subscription / Rule / Schema discovery**: This slice does not enumerate, fetch, or persist any child Service Bus asset. Only namespace-level metadata is captured.
- **Scheduled or background synchronization**: No background discovery jobs, no scheduled validation re-runs, no drift detection beyond surfacing it on on-demand revalidation, no metadata reconciliation pipeline.
- **Azure resource provisioning**: BusTerminal does NOT create, modify, or delete Azure Service Bus namespaces. The slice operates exclusively on *existing* Azure resources.
- **Namespace deletion within Azure**: Out of scope. `Archived` is BusTerminal's terminal "out of use" position; Azure-side resource lifecycle is the operator's responsibility outside BusTerminal.
- **Compliance scoring, policy enforcement, approval workflows**: No automated governance gate, no approval queue, no compliance score on a namespace. The namespace-administrator role is the sole governance gate in this slice.
- **Bulk onboarding, automated onboarding, import/export tooling**: Every onboarding in this slice is a single-namespace wizard interaction. CSV/Excel import, multi-select onboarding, and bulk operations are reserved for a future productivity spec.
- **Cross-tenant onboarding**: Rejected at step 1 per FR-006. Multi-Entra-tenant support is reserved for a future spec.
- **Connection string / SAS token capture**: Never. Managed identity only (FR-017, FR-033, SC-007).
- **Physical (hard) deletion of onboarded namespaces**: Not exposed in v1 (FR-026). `Archived` is the terminal state.
- **Drift auto-reconciliation**: Surfaced only; never auto-corrected.
- **A "namespace discovery" view that enumerates Azure subscriptions to suggest namespaces for onboarding**: Reserved for a future discovery spec. The operator supplies the ARM Resource ID by hand in v1.
- **Free-form (non-Entra) ownership assignments**: Out of scope. Ownership is Entra-backed in this slice (FR-012).
