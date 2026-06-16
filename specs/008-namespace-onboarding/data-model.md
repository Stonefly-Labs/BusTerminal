# Data Model — Namespace Onboarding (Spec 008)

**Plan**: [plan.md](./plan.md) · **Spec**: [spec.md](./spec.md) · **Research**: [research.md](./research.md)

This document is the registry-side source of truth for the **extended namespace entity shape**, the new **ValidationRun** entity, the structured **OwnershipAssignment** composition, the **persistence layout**, the **extended audit-event shape**, **validation rules**, and **concurrency semantics**. Naming is uniform across in-process C# types, persisted JSON, JSON Schemas in `contracts/`, OpenAPI DTOs, the AI Search index projection, and telemetry attributes — verified in §Naming Cross-Reference.

This data model is **additive** to spec 006's `RegistryNamespace` document — it does not introduce a parallel entity. The §Coexistence section maps every interaction between spec-006 `source = Manual` documents and spec-008 `source = Onboarded` documents.

---

## 1. Entity catalog

### 1.1 `OnboardedNamespace` (extends spec 006's `RegistryNamespace`)

The same Cosmos document type as spec 006's `RegistryNamespace`. Documents with `source = Onboarded` carry the canonical spec-006 fields PLUS the spec-008 additions below. Documents with `source = Manual` (legacy spec-006 records) leave the spec-008 fields null and continue to work through spec-006's polymorphic API.

| Field | Type | Notes |
|---|---|---|
| (canonical spec-006 shared fields) | (per spec 006 §2) | `id`, `entityType = "Namespace"`, `name`, `fullyQualifiedName`, `description`, `tags`, `owner` (free-form string — preserved for legacy, NOT written by spec 008), `environment`, `status` (Active / Deprecated — governance axis, spec 006), `createdAtUtc`, `updatedAtUtc`, `source`, `azureResourceId`, `namespaceName`, `metadata`, `parentId` (always null for namespaces), `_etag`. |
| `source` | `RegistrySource` enum | Spec 006: `Manual`. **Spec 008 adds**: `Onboarded`. `Discovered` remains reserved per spec 006. |
| `subscriptionId` | `Guid` | Spec 008 — parsed from ARM resource id. Required on Onboarded docs. |
| `subscriptionName` | `string?` | Spec 008 — best-effort resolved from ARM at onboarding time. Nullable. |
| `resourceGroup` | `string` | Spec 008 — parsed from ARM resource id. Required on Onboarded docs. |
| `tenantId` | `Guid` | Spec 008 — resolved from ARM subscription. Required on Onboarded docs. MUST match `IConfiguration["AzureAd:TenantId"]` (FR-006). |
| `region` | `string` | Spec 008 — Azure region of the namespace resource (e.g., `eastus2`). Required on Onboarded docs; read-only after onboarding (FR-005). |
| `displayName` | `string` | Spec 008 — human-facing identifier; defaults from `namespaceName` but independently editable (FR-009). Required on Onboarded docs (1–200 chars). |
| `businessUnit` | `string?` | Spec 008 — free-form ≤ 200 chars. Optional. |
| `productOrApplication` | `string?` | Spec 008 — free-form ≤ 200 chars. Optional. |
| `costCenter` | `string?` | Spec 008 — free-form ≤ 100 chars. Optional. |
| `notes` | `string?` | Spec 008 — free-form ≤ 4000 chars. Optional. |
| `lifecycleStatus` | `LifecycleStatus` enum | Spec 008 — operational axis: `Active`, `Disabled`, `Archived`. (`PendingValidation` is transient-only and NEVER appears on a persisted document per FR-022.) Required on Onboarded docs. |
| `validationStatus` | `ValidationStatus` enum | Spec 008 — `Healthy`, `Degraded`, `Unhealthy`. Mirrors the latest ValidationRun's `aggregateStatus`. Required on Onboarded docs. |
| `lastValidationRunId` | `Guid?` | Spec 008 — points at the most recent `ValidationRun.id` for this namespace. Required on Onboarded docs (first onboarding establishes it). |
| `lastValidatedAtUtc` | `DateTime?` | Spec 008 — UTC timestamp of the most recent ValidationRun. Required on Onboarded docs. |
| `ownership` | `OwnershipBlock` | Spec 008 — structured composition; see §1.3. Required on Onboarded docs. |
| `onboardingActor` | `OnboardingActor` | Spec 008 — `{ objectId, displayNameSnapshot, onboardedAtUtc }`. Captured at first registration; immutable. |

**Persisted JSON shape** (excerpt — full schema in [`contracts/onboarded-namespace.schema.json`](./contracts/onboarded-namespace.schema.json)):

```json
{
  "id": "5d4f3b48-...",
  "entityType": "Namespace",
  "name": "orders-prod-eus2",
  "fullyQualifiedName": "orders-prod-eus2",
  "source": "Onboarded",
  "status": "Active",
  "environment": "prod",
  "azureResourceId": "/subscriptions/.../Microsoft.ServiceBus/namespaces/orders-prod-eus2",
  "subscriptionId": "11111111-2222-3333-4444-555555555555",
  "subscriptionName": "Payments — Production",
  "resourceGroup": "rg-payments-prod-eus2",
  "tenantId": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
  "region": "eastus2",
  "displayName": "Payments — Orders (prod, eastus2)",
  "description": "Authoritative orders messaging namespace for Payments.",
  "businessUnit": "Payments",
  "productOrApplication": "Orders",
  "costCenter": "CC-1042",
  "notes": "Primary; failover to westus2 not yet enabled.",
  "tags": [{"key": "team", "value": "payments-platform"}],
  "lifecycleStatus": "Active",
  "validationStatus": "Healthy",
  "lastValidationRunId": "8f1c...",
  "lastValidatedAtUtc": "2026-06-14T18:42:17Z",
  "ownership": { ... },
  "onboardingActor": { "objectId": "...", "displayNameSnapshot": "Jane Operator", "onboardedAtUtc": "2026-06-14T18:42:17Z" },
  "createdAtUtc": "2026-06-14T18:42:17Z",
  "updatedAtUtc": "2026-06-14T18:42:17Z",
  "_etag": "..."
}
```

### 1.2 `ValidationRun` (new entity)

An append-only record of one validation execution per FR-016.

**`namespaceId` allocation** (per research §18): the wizard pre-allocates the namespace's `id` (`Guid`) at the start of step 4 and re-uses that `Guid` as the `namespaceId` for every validation run AND as the persisted `OnboardedNamespace.id` once step-5 Register succeeds. This keeps pre-onboarding runs partition-aligned with the eventual namespace document instead of scattering them across `Guid.Empty` (hot partition) or per-run fresh Guids (wasteful single-document partitions). Direct API callers (e.g., CI scripts) MAY omit `proposedNamespaceId` on `POST /api/namespaces/_validate`; the runner generates a fresh `Guid` in that case, and the resulting ValidationRun is not bindable to a future namespace document.

| Field | Type | Notes |
|---|---|---|
| `id` | `Guid` | Stable identifier; equals `runId` everywhere else. |
| `namespaceId` | `Guid` | Foreign key into the `OnboardedNamespace.id`. **Partition key.** For pre-onboarding runs, this is a wizard-pre-allocated Guid that becomes the namespace's `id` on register. For runs initiated against an existing namespace, this is the namespace's persisted `id`. |
| `executedAtUtc` | `DateTime` | UTC timestamp of run start. |
| `executedBy` | `Guid` | Entra `objectId` of the principal that initiated the run (administrator for on-demand, workload identity for first onboarding). |
| `executedByDisplayNameSnapshot` | `string` | Display name at run time for audit-panel rendering. |
| `azureResourceIdAtRun` | `string` | The ARM ID that was validated (may differ from `OnboardedNamespace.azureResourceId` if the spec ever permits ARM-id mutation — currently it does not, per FR-005). |
| `aggregateStatus` | `ValidationStatus` enum | `Healthy` (all checks Pass), `Degraded` (at least one non-fatal Fail; Existence + Accessibility Pass), `Unhealthy` (Existence or Accessibility Fail). |
| `checkResults` | `ValidationCheckResult[]` | Exactly five entries per FR-014 (Existence, Accessibility, RequiredPermissions, IdentityAuthorization, ApiReachability). |
| `armResourceSnapshot` | `ArmResourceSnapshot?` | Spec 008 §11 — captures observed `region`, `resourceGroup`, `subscriptionId` for drift detection. Null when Existence fails. |
| `driftDetected` | `bool` | True iff `armResourceSnapshot` differs from the persisted `OnboardedNamespace` Azure-identifier fields. |
| `driftFields` | `DriftField[]` | Empty when `driftDetected = false`. Each entry: `{ field, persistedValue, observedValue }`. |
| `totalDurationMs` | `int` | Wall-clock duration of the whole run. |
| `_etag` | `string` | Cosmos system field. Not modifiable by code paths. |
| `_ts` | `int` | Cosmos system field. Implicit. |

**`ValidationCheckResult`**:

| Field | Type | Notes |
|---|---|---|
| `name` | `ValidationCheckName` enum | `Existence`, `Accessibility`, `RequiredPermissions`, `IdentityAuthorization`, `ApiReachability`. |
| `outcome` | `ValidationCheckOutcome` enum | `Pass`, `Fail`, `Skipped`. |
| `reason` | `string` | Human-readable, categorical per FR-035 — categories enumerated in §Reason Categories below. No PII, no raw exception messages. |
| `reasonCategory` | `ValidationFailureCategory` enum | `Ok` (when outcome = Pass), `Timeout`, `Unauthorized`, `NotFound`, `Throttled`, `CrossTenant`, `Unknown`. |
| `durationMs` | `int` | Wall-clock duration of this check. |
| `correlationRequestId` | `string?` | ARM `x-ms-correlation-request-id` header if available — supports cross-referencing in Azure Monitor logs. |

**`ArmResourceSnapshot`**:

| Field | Type | Notes |
|---|---|---|
| `region` | `string` | Azure region as returned by ARM at run time. |
| `resourceGroup` | `string` | Resource group as returned by ARM at run time. |
| `subscriptionId` | `Guid` | Subscription id as returned by ARM at run time. |
| `capturedAtUtc` | `DateTime` | Timestamp of the snapshot. |

**`DriftField`**:

| Field | Type | Notes |
|---|---|---|
| `field` | `string` | Field name on the namespace document: `region`, `resourceGroup`, or `subscriptionId`. |
| `persistedValue` | `string` | Value stored on the namespace doc. |
| `observedValue` | `string` | Value returned by ARM at run time. |

### 1.3 `OwnershipBlock` (embedded into `OnboardedNamespace`)

| Field | Type | Notes |
|---|---|---|
| `primaryOwner` | `OwnershipAssignment` | Exactly one; required (FR-010). |
| `secondaryOwners` | `OwnershipAssignment[]` | Zero or more. |
| `technicalStewards` | `OwnershipAssignment[]` | Zero or more. |
| `supportContacts` | `OwnershipAssignment[]` | Zero or more. |

**`OwnershipAssignment`**:

| Field | Type | Notes |
|---|---|---|
| `role` | `OwnershipRole` enum | `PrimaryOwner`, `SecondaryOwner`, `TechnicalSteward`, `SupportContact`. Discriminator matches the containing `OwnershipBlock` slot. |
| `principalType` | `PrincipalType` enum | `User`, `Group`. |
| `objectId` | `Guid` | Entra `objectId`. |
| `displayNameSnapshot` | `string` | Display name captured at assignment time for fallback rendering when Graph re-resolution fails (FR-011). |
| `assignedAtUtc` | `DateTime` | Timestamp of this specific assignment. |
| `assignedBy` | `Guid` | Entra `objectId` of the administrator who assigned this principal. |

### 1.4 Extended `AuditEvent` (registry-audit container — same shape as spec 006, additive)

Spec 008 extends the existing `AuditEvent` record (`Features/Registry/_Shared/AuditEvent.cs`):

| Field | Type | Notes (delta vs spec 006) |
|---|---|---|
| `eventType` | `AuditEventType` enum | **Spec 008 adds**: `NamespaceOnboarded`, `NamespaceMetadataUpdated`, `NamespaceOwnershipUpdated`, `NamespaceLifecycleTransitioned`, `NamespaceValidationExecuted`. Spec 006's `Created`, `Updated`, `Deleted`, `StatusChanged` remain — spec 008 does NOT emit them for Onboarded namespaces (it emits the more specific types). |
| `lifecycleReason` | `string?` | **NEW field** — populated only on `NamespaceLifecycleTransitioned`. Bounded at 1000 chars. |
| (all other fields per spec 006) | | Unchanged. |

---

## 2. Enumerations

### `RegistrySource` (extended)

| Value | Origin | Meaning |
|---|---|---|
| `Manual` | spec 006 | Operator-typed via the spec-006 polymorphic registry form. |
| `Onboarded` | **spec 008 — new** | Onboarded via the spec-008 wizard. Validation-verified at registration time. |
| `Discovered` | spec 006 — reserved | Reserved for a future auto-discovery spec. Not emitted. |

### `LifecycleStatus` (new, spec 008)

| Value | Meaning |
|---|---|
| `PendingValidation` | **TRANSIENT-ONLY** — never persisted (per FR-022). Reserved for a future async-validation spec. |
| `Active` | Namespace is onboarded and in active use. Default state on successful first onboarding. |
| `Disabled` | Operator-disabled (e.g., during planned decommission). Visible in inventory; `Re-run validation` disabled. |
| `Archived` | Terminal state — removed from active use. Hidden from default inventory; read-only. Restorable to `Disabled` only. |

Permitted transitions (per FR-023):
- (initial create) → `Active`
- `Active` ⇄ `Disabled`
- `Active` | `Disabled` → `Archived`
- `Archived` → `Disabled`

### `ValidationStatus` (new, spec 008)

| Value | Meaning |
|---|---|
| `Healthy` | All five checks `Pass`. |
| `Degraded` | At least one non-fatal check `Fail`; Existence + Accessibility both `Pass`. |
| `Unhealthy` | Existence or Accessibility `Fail`. Initial onboarding hard-blocked at this status (FR-023a). |

### `ValidationCheckName` (new, spec 008)

| Value | Meaning |
|---|---|
| `Existence` | ARM `GET` returns the namespace resource. |
| `Accessibility` | ARM call succeeds without auth error. |
| `RequiredPermissions` | Effective permissions at namespace scope include `Microsoft.ServiceBus/namespaces/read`. |
| `IdentityAuthorization` | Token exchange completes; managed identity is correctly federated. |
| `ApiReachability` | Service Bus management endpoint responds to a lightweight metadata probe. |

### `ValidationCheckOutcome` (new, spec 008)

| Value | Meaning |
|---|---|
| `Pass` | Check succeeded. |
| `Fail` | Check failed; `reasonCategory` distinguishes the failure mode. |
| `Skipped` | Check skipped because a prerequisite failed (e.g., Existence failed, so RequiredPermissions is meaningless). |

### `ValidationFailureCategory` (new, spec 008)

| Value | Meaning |
|---|---|
| `Ok` | Outcome = Pass. |
| `Timeout` | Per-check budget exceeded (research §5). |
| `Unauthorized` | 401/403 from ARM or Service Bus management endpoint. |
| `NotFound` | 404 from ARM. |
| `Throttled` | 429 from ARM or Service Bus management endpoint. |
| `CrossTenant` | ARM subscription's tenant id ≠ configured tenant id (FR-006). Only emitted by Existence/Accessibility. |
| `Unknown` | Catch-all (must be exceedingly rare; emits a `WARNING` log with full exception detail kept in App Insights). |

### `OwnershipRole` (new, spec 008)

| Value | Meaning |
|---|---|
| `PrimaryOwner` | Exactly-one; required (FR-010). |
| `SecondaryOwner` | Zero-or-more. |
| `TechnicalSteward` | Zero-or-more. |
| `SupportContact` | Zero-or-more. |

### `PrincipalType` (new, spec 008)

| Value | Meaning |
|---|---|
| `User` | Entra user principal. |
| `Group` | Entra group. Reserved for a future "service principal" extension; not added in v1. |

### `AuditEventType` (extended)

| Value | Origin | Notes |
|---|---|---|
| `Created` | spec 006 | Emitted for spec-006 Manual namespaces only. |
| `Updated` | spec 006 | Same. |
| `Deleted` | spec 006 | Same. |
| `StatusChanged` | spec 006 | Same. |
| `NamespaceOnboarded` | **spec 008 — new** | First registration. `changeSummary` shape: `Onboarded namespace '{displayName}' in environment '{environment}'`. |
| `NamespaceMetadataUpdated` | **spec 008 — new** | Metadata edit. `fieldChanges` populated. |
| `NamespaceOwnershipUpdated` | **spec 008 — new** | Ownership-block edit. `fieldChanges` populated with role-level diffs. |
| `NamespaceLifecycleTransitioned` | **spec 008 — new** | Lifecycle transition. `lifecycleReason` populated. |
| `NamespaceValidationExecuted` | **spec 008 — new** | Re-run validation. `changeSummary` carries the resulting `aggregateStatus`. |

---

## 3. Persistence layout

| Container | Database | PK | Purpose | Provisioned by |
|---|---|---|---|---|
| `registry-entities` | `canonical` | `/environment` | OnboardedNamespace documents (same container as spec 006's RegistryNamespace; nullable additions accommodate spec 008 fields). | spec 006 — extended in place |
| `registry-audit` | `canonical` | `/entityId` | AuditEvent documents (extended with new event types and the `lifecycleReason` field). | spec 006 — extended in place |
| `namespace-validation-runs` | `canonical` | `/namespaceId` | ValidationRun documents (append-only). | spec 008 — **new container**; added to `iac/modules/cosmos-registry-store/` |
| `registry-entities-leases` | `canonical` | `/id` | Cosmos change-feed leases for the spec-006 indexer (untouched). | spec 006 |

### Container details

**`namespace-validation-runs`** (new):
- Partition key path: `/namespaceId`
- Autoscale RU: lowest band the account allows (matches `registry-audit`).
- Indexing policy: default. Composite indexes optional (e.g., `(namespaceId, executedAtUtc DESC)`) — added in v1.x if list queries show p99 > target.
- TTL: none (indefinite retention per spec FR-031).

---

## 4. Concurrency semantics

- **OnboardedNamespace updates** (metadata, ownership, lifecycle): ETag-based optimistic concurrency via Cosmos `If-Match`. Stale writes surface the existing spec-006 conflict response (`contracts/conflict-response.schema.json` in the 006 spec) — reused unchanged.
- **ValidationRun writes**: append-only; no concurrency conflicts possible at the document level. Concurrent re-validation requests on the same namespace are *not* serialized at the API layer in v1 — a future spec MAY add per-namespace locking. Operationally this is acceptable because each ValidationRun is independent and the namespace document's `lastValidationRunId` and `validationStatus` fields are updated using ETag-based last-writer-wins semantics with the *latest* run winning.
- **Lifecycle transitions**: served by a `POST /api/namespaces/{id}/lifecycle` action endpoint that takes the current ETag in the request body and performs the transition + audit write as a single Cosmos transactional batch when possible (same partition key for namespace + audit). If transactional batch is not possible (cross-partition: namespace `/environment` ≠ audit `/entityId`), the audit write is best-effort post-namespace-update with a structured warning log on failure, matching the spec-006 audit-write pattern.

---

## 5. Validation rules

Server-side via FluentValidation; client-side via Zod (mirrored to backend rules — verified by a contract test).

### `OnboardingRequest`

| Rule | Source |
|---|---|
| `azureResourceId` matches the canonical ARM Service Bus namespace format | FR-004 |
| `azureResourceId`'s subscription resolves to `tenantId` matching `IConfiguration["AzureAd:TenantId"]` | FR-006 |
| `azureResourceId` not already onboarded (case-insensitive match) | FR-007 |
| `displayName` 1..200 chars | FR-008 |
| `description` ≤ 4000 chars | FR-008 |
| `environment` non-empty | FR-008 |
| `businessUnit`, `productOrApplication` ≤ 200 chars each | FR-008 |
| `costCenter` ≤ 100 chars | FR-008 |
| `notes` ≤ 4000 chars | FR-008 |
| `tags`: max 50; keys 1..256 chars; values ≤ 1024 chars; per spec 006 §2 case-insensitive key match | spec 006 FR-002 reuse |
| `ownership.primaryOwner` present and `principalType ∈ {User, Group}` | FR-010 |
| All Ownership Entries: `objectId` is a valid Guid; `displayNameSnapshot` non-empty 1..256 chars | FR-011 |
| `validationRunId` references a ValidationRun whose `aggregateStatus ∈ {Healthy, Degraded}` and whose `executedAtUtc` is within the last 30 minutes | FR-023a |

### `UpdateMetadataRequest`

| Rule | Source |
|---|---|
| Azure-identifier fields (`azureResourceId`, `subscriptionId`, `subscriptionName`, `resourceGroup`, `tenantId`, `region`, `namespaceName`) NOT present in the request body | FR-005 |
| Same length and presence rules as `OnboardingRequest` for `displayName`, `description`, `businessUnit`, `productOrApplication`, `costCenter`, `notes`, `tags`, `environment` (where mutable) | FR-008 |

### `UpdateOwnershipRequest`

| Rule | Source |
|---|---|
| `primaryOwner` present; exactly one assignment for the PrimaryOwner role | FR-010 |
| No duplicate `(role, objectId)` pairs within the same role list | clarity |

### `LifecycleTransitionRequest`

| Rule | Source |
|---|---|
| `action ∈ {disable, enable, archive, restore}` | FR-023 |
| `reason` required when `action ∈ {disable, archive, restore}`; 1..1000 chars | FR-023 |
| Current `lifecycleStatus` permits the requested transition per the §LifecycleStatus transition table | FR-023 |

---

## 6. Reason categories (per-check `reason` strings)

Categorical strings — operators see these on the validation panel; downstream telemetry consumers can aggregate over them. **No PII, no raw exception text.** Each category maps to one `ValidationFailureCategory` enum value.

| Category | Emitted by | Meaning |
|---|---|---|
| `OK` | any check (outcome = Pass) | Check succeeded. |
| `Timeout` | any check | Per-check budget exceeded. |
| `ArmNamespaceNotFound` | Existence | ARM returned 404 for the resource id. |
| `ArmAccessDenied` | Accessibility | ARM returned 401/403 — BusTerminal UAMI cannot reach the resource at all. |
| `ReaderRoleMissing` | RequiredPermissions | Effective permissions at namespace scope do not include `Microsoft.ServiceBus/namespaces/read`. **Remediation hint**: surface the runbook `iac/runbooks/grant-namespace-reader.md` and a copy-pasteable `az role assignment create` block. |
| `TokenExchangeFailed` | IdentityAuthorization | Workload UAMI token acquisition failed — typically a transient Entra issue. |
| `ServiceBusManagementUnreachable` | ApiReachability | Network-level failure on the Service Bus management endpoint (DNS, TLS, timeout). |
| `ArmThrottled` | any check | 429 from ARM; the check failed inside the per-check budget. (Throttling-aware retry within the budget is acceptable — the categorization reflects the *final* outcome.) |
| `CrossTenantArmId` | Existence (or Accessibility, depending on which surfaced first) | ARM subscription belongs to a different Entra tenant. (Re-rendered as a wizard step-1 field-level error before reaching step 4 in normal UX; this category covers the edge case where step 1 validation was bypassed.) |
| `Unknown` | any check | Catch-all. Emits a structured `WARNING` log with full exception detail captured in App Insights. |

---

## 7. Coexistence with spec 006

| Concern | Spec 006 (`source = Manual`) | Spec 008 (`source = Onboarded`) |
|---|---|---|
| Where it appears | Registry Explorer (`/registry`) | Namespace Inventory (`/namespaces`) |
| Created via | `POST /api/registry` (polymorphic) | `POST /api/namespaces` (typed) |
| Read via | `GET /api/registry/{id}` (polymorphic) AND `GET /api/namespaces/{id}` | Same |
| Edited via | `PUT /api/registry/{id}` — accepts polymorphic shape | `PUT /api/namespaces/{id}/metadata` + `PUT /api/namespaces/{id}/ownership` |
| Lifecycle | `PATCH /api/registry/{id}/status` (Active ⇄ Deprecated) | `POST /api/namespaces/{id}/lifecycle` (disable/enable/archive/restore) + `PATCH /api/registry/{id}/status` (Active ⇄ Deprecated still works) |
| Deletion | `DELETE /api/registry/{id}` — hard delete | `POST /api/namespaces/{id}/lifecycle` with `action = archive` — physical delete NOT exposed (FR-026) |
| Authorization | `[Authorize]` only (any authenticated tenant user) | `[Authorize]` + `CanAdministerNamespaces` policy |
| Ownership shape | Free-form `owner: string` | Structured `ownership: OwnershipBlock` |
| Status enum | `Active`, `Deprecated` (governance) | `lifecycleStatus`: `Active`, `Disabled`, `Archived` (operational) + the same `status` (governance) |

**Cross-API gates**:
- `PUT /api/registry/{id}` (spec 006) → **rejects with 409** when target document has `source = Onboarded` (research §7). Error body identifies the conflicting source and points to `/api/namespaces/{id}/metadata`.
- `DELETE /api/registry/{id}` (spec 006) → **rejects with 409** when target document has `source = Onboarded`. Error body points to the lifecycle Archive action.
- `POST /api/namespaces/{id}/metadata` etc (spec 008) → **rejects with 409** when target document has `source = Manual` (operators must migrate the document or use spec 006's endpoints).

---

## 8. Naming cross-reference

Every term used across the spec-008 surfaces appears here exactly once with its canonical form per surface.

| Concept | C# type | Persisted JSON | OpenAPI schema | AI Search field (projected) | OTel span attribute |
|---|---|---|---|---|---|
| Onboarded namespace document | `OnboardedNamespace` (or `RegistryNamespace` with `Source = Onboarded`) | `RegistryNamespace` doc with `"source": "Onboarded"` | `OnboardedNamespace` | (existing `RegistryEntity` projection — extended) | `busterminal.registry.entity` family (existing) |
| Source enum | `RegistrySource` | `"source": "Manual" | "Onboarded"` | enum string | `busterminal.registry.entity.source` |
| Lifecycle status | `LifecycleStatus` | `"lifecycleStatus": "Active" | "Disabled" | "Archived"` | enum string | filterable string | `namespace.lifecycle_status` |
| Validation status | `ValidationStatus` | `"validationStatus": "Healthy" | "Degraded" | "Unhealthy"` | enum string | filterable string | `namespace.validation_status` |
| Validation run document | `ValidationRun` | `validation-run` document in `namespace-validation-runs` container | `ValidationRun` | (not projected — separate container) | `namespace.validation.run` (parent span) |
| Validation check | `ValidationCheckResult` | embedded `checkResults[*]` | `ValidationCheckResult` | (n/a) | `namespace.validation.check.<name>` (child span) |
| Validation check name | `ValidationCheckName` | `"name": "Existence" | "Accessibility" | ...` | enum string | (n/a) | `validation.check.name` |
| Validation check outcome | `ValidationCheckOutcome` | `"outcome": "Pass" | "Fail" | "Skipped"` | enum string | (n/a) | `validation.check.outcome` |
| Validation failure category | `ValidationFailureCategory` | `"reasonCategory": "..."` | enum string | (n/a) | `validation.check.reason_category` |
| Per-check duration | `int` (ms) | `"durationMs": 1234` | int | (n/a) | `validation.check.duration_ms` |
| Ownership block | `OwnershipBlock` | `"ownership": {...}` | `OwnershipBlock` | flattened (`ownership.primary_owner.object_id`, etc.) | (not in spans — PII boundary) |
| Ownership assignment | `OwnershipAssignment` | `{role, principalType, objectId, displayNameSnapshot, assignedAtUtc, assignedBy}` | `OwnershipAssignment` | (per-role flattened) | (not in spans) |
| Drift detection | `DriftField[]` | embedded `driftFields[*]` on ValidationRun | `DriftField` | (n/a) | `namespace.validation.run.drift_detected` (bool) |
| Audit event (extended) | `AuditEvent` | `audit-event` doc | `AuditEvent` | (existing projection — extended event-type enum) | (existing audit telemetry — extended) |
| Lifecycle reason | `string?` | `"lifecycleReason": "..."` | string | (n/a) | (not in spans — operator-supplied free-form, PII boundary) |
| Workload UAMI identity | `WorkloadIdentity` | (not persisted) | `WorkloadIdentity` (id, runbook URL) | (n/a) | (n/a) |

---

## 9. Migration / data backfill

**None required** for v1.

- Existing spec-006 `RegistryNamespace` documents with `source = Manual` continue to deserialize correctly — the new fields are nullable.
- No existing data is rewritten.
- The new `namespace-validation-runs` container is created empty.
- Existing audit events with the four spec-006 `eventType` values continue to deserialize correctly — the new event-type strings are additive.

A future spec MAY introduce a one-time migration pass to *promote* Manual namespaces to Onboarded by running validation against their `azureResourceId` — explicitly out of scope here.

---

## 10. Open considerations (deferred — not blockers)

- **Composite indexes on `namespace-validation-runs`**: deferred until p99 list-runs latency is observed. The natural composite is `(namespaceId, executedAtUtc DESC)`.
- **TTL on `namespace-validation-runs`**: deferred per spec FR-031. A future ops-hardening spec will set a sensible retention period.
- **Drift auto-reconciliation**: out of scope per spec Non-Goal.
- **Service Principal ownership (`PrincipalType = ServicePrincipal`)**: future extension; the enum is forward-friendly.
- **Multi-tenant onboarding**: out of scope; rejected at FR-006.

---

This data model is binding for the implementation in spec 008's task graph. Any deviation requires a clarification round on `spec.md` and a refresh of this document.
