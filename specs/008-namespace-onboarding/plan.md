# Implementation Plan: Namespace Onboarding

**Branch**: `008-namespace-onboarding` | **Date**: 2026-06-14 | **Spec**: [`spec.md`](./spec.md)

**Input**: Feature specification from `/specs/008-namespace-onboarding/spec.md`

## Summary

Spec 008 ships a guided **five-step onboarding wizard**, a **Namespace Inventory**, a **Namespace Details** page, and **lifecycle/edit** actions that elevate Azure Service Bus namespaces from spec 006's flat manual-create `RegistryNamespace` into Azure-verified, Entra-backed, lifecycle-managed registry entities. The slice extends spec 006's existing `Namespace` document in place (additive nullable fields + new `Source = Onboarded` enum value), introduces a new Cosmos container `namespace-validation-runs` for append-only `ValidationRun` history, adds a new vertical slice `Features/Namespaces/` to `BusTerminal.Api` exposing `/api/namespaces/*` Minimal APIs (parallel to — not replacing — spec 006's polymorphic `/api/registry/*` surface), introduces `Azure.ResourceManager.ServiceBus` for ARM management-plane probing, and extends the existing `Microsoft.Graph` v5 integration with `Group.Read.All` for the tenant-wide Entra picker. The frontend adds a new section under `web/app/(authenticated)/namespaces/` consisting of inventory + details + lifecycle/edit forms + a 5-step wizard (custom-built `Stepper` composing `Card` + dot-indicators per shadcn convention — no new dependency). Authorization is gated by a brand-new fifth platform role `namespace-administrator` (an Entra App Role on the existing BusTerminal API app, additive to spec 003's Admin/Operator/Reader/Developer four-role matrix), exposed as a new `RolePolicies.CanAdministerNamespaces` policy + `IsNamespaceAdministrator()` helper on `PlatformPrincipal`. Validation runs synchronously inline (5 named checks executed in parallel with per-check timeout, aggregate p95 < 15s — per FR-015 / SC-004); each ValidationRun is persisted and emits per-check OTel spans under a new `BusTerminal.NamespaceOnboarding` ActivitySource. Operator-supplied namespaces require BusTerminal's workload UAMI to hold the built-in `Reader` role at the namespace scope — granted **out-of-band** by the operator via a runbook (`az role assignment create` with the principal id surfaced by a new public `/api/namespaces/identity` endpoint that returns the UAMI's `principalId`); this is the FR-014 `RequiredPermissions` check's verification surface and is logged as a documented IaC bypass in Complexity Tracking. The IaC delta is intentionally small: add Reader (`acdd72a7-3385-48ef-bd42-f606fba81ae7`) to the pipeline MI RBAC-Admin allowlist (forward-optionality for a future IaC-driven grant model), add `Group.Read.All` to the Graph permissions module (one new UUID to manage tenant-admin consent for), and declare the new `namespace-administrator` App Role via the existing `app-registration-roles` module.

## Technical Context

**Language/Version**:
- Backend: **.NET 10 / C#** (matches `api/BusTerminal.Api`). No new project — new vertical slice `Features/Namespaces/` lives in the existing assembly.
- Frontend: **TypeScript strict** on **Next.js 16.x App Router** + **React 19** (matches `web/`).
- IaC: **OpenTofu ≥ 1.11** (matches `iac/`).

**Primary Dependencies**:
- Backend (additions to `BusTerminal.Api.csproj`):
  - `Azure.ResourceManager.ServiceBus` — **new** dependency. Used by the validation runner's `Existence`, `Accessibility`, `RequiredPermissions`, and `ApiReachability` checks. Pin verified in research §1. Authenticates via the existing workload UAMI `DefaultAzureCredential` factory (spec 004 / spec 005 pattern). No connection strings, no SAS tokens (FR-017, FR-033, SC-007).
  - `Microsoft.Graph 5.105.0` — **already pinned**. The new ownership-picker code consumes `graph.Users.GetAsync` and `graph.Groups.GetAsync` via the existing `BusTerminal.Graph` integration; the new `Group.Read.All` application permission is declared in IaC (research §2) and admin-consented at deploy time per spec 003's existing manual-consent runbook (`specs/003-auth-and-identity/quickstart.md §A.2.3`).
  - `Microsoft.Azure.Cosmos 3.60.0` — already pinned; the new `CosmosNamespaceValidationRunStore` reuses the existing `CosmosClientFactory` + `AzureCredentialFactory`.
  - `FluentValidation` — already in tree from spec 006; the new `OnboardingValidator`, `NamespaceMetadataValidator`, `OwnershipValidator`, `LifecycleTransitionValidator` follow the spec-006 pattern.
- Frontend: **no new dependencies**. Every spec-008-relevant package is already pinned (verified in research §9): `react-hook-form ^7.76.0`, `zod ^4.4.3`, `@hookform/resolvers ^3.9.1`, `@tanstack/react-query ^5.62.0`, `@tanstack/react-table ^8.21.3`, `cmdk ^1.1.1`, `framer-motion ^12.38.0`, `lucide-react ^1.16.0`, `@azure/msal-browser ^4`, `@azure/msal-react ^5`. The wizard's step indicator is a custom composition of existing shadcn primitives (`Card`, `Badge`, motion-aware step dots) — no third-party stepper component required.
- IaC (additions to `iac/`):
  - Extend `iac/modules/app-registration-roles/` inputs to add the `namespace-administrator` App Role definition (one new entry in the `role_definitions` map; stable UUID generated and pinned).
  - Extend `iac/modules/graph-permissions/` to add `Group.Read.All` (UUID `5b567255-7703-4780-807c-7be8301ae99b`) to the `granted_application_permission_ids` set.
  - Extend `iac/platform-bootstrap/main.tf` pipeline MI RBAC-Admin condition allowlist to permit Reader role GUID `acdd72a7-3385-48ef-bd42-f606fba81ae7` (forward optionality — see Complexity Tracking #1).
  - **No new Azure resources.** No new module directory required for v1. Spec FR-042 is honored.

**Storage**:
- **Cosmos DB** (existing dev account from spec 004 + 005):
  - Existing `registry-entities` container (PK `/environment`) — extended in place. The existing `RegistryNamespace` JSON shape gains nullable fields (`subscriptionName`, `tenantId`, `region`, `businessUnit`, `productOrApplication`, `costCenter`, `notes`, `lifecycleStatus`, `validationStatus`, `lastValidationRunId`, `lastValidatedAtUtc`, `ownership`). `source = Onboarded` is a new enum value (research §7). Existing spec-006 `source = Manual` documents remain readable and writable through spec-006's polymorphic API; new fields stay null on those records. Cosmos's schemaless nature absorbs the change; System.Text.Json's enum-tolerant deserializer accepts the new value across both code paths (research §7).
  - **New** container `namespace-validation-runs` (PK `/namespaceId`, append-only, no TTL in v1, lowest autoscale RU band ≤ existing audit container). Records every validation execution per FR-016. Provisioned by extending the existing `iac/modules/cosmos-registry-store/` module's container list — no new IaC module required.
  - Existing `registry-audit` container (PK `/entityId`) — reused. Spec 008 emits five new `AuditEventType` values: `NamespaceOnboarded`, `NamespaceMetadataUpdated`, `NamespaceOwnershipUpdated`, `NamespaceLifecycleTransitioned`, `NamespaceValidationExecuted` (research §8). The existing `AuditEvent` record gains a nullable `LifecycleReason` field for `NamespaceLifecycleTransitioned` events.
- **Azure AI Search**: NOT TOUCHED in v1. Spec FR-021 requires Inventory + Details to be served from the persistent store (not the search index). The existing spec-006 search index continues to index Namespace docs (with the new fields appearing as projected metadata for forward optionality), but spec 008 does not query the search index for its own surfaces. A future spec MAY add namespace-onboarding-specific search projections; deferred consciously.

**Testing**:
- **Backend unit / integration**: `xUnit` + the existing `CosmosFixture` pattern from spec 004 / 006. New test suites under `api/BusTerminal.Api.Tests/Features/Namespaces/` cover the validation runner (mocked `ArmClient`), the onboarding endpoint flow, structured-ownership validation, lifecycle transition rules, and the `namespace-administrator` role gate (mocked principal). The validation runner has a dedicated integration test against a real-but-shape-only ARM resource via `Azure.ResourceManager` to verify per-check telemetry shape — gated on a `BUSTERMINAL_TEST_ARM_NAMESPACE_ID` env var so CI doesn't require an Azure-side fixture.
- **API contract tests**: assert the OpenAPI document conforms to `contracts/namespace-onboarding-api.yaml` and that the canonical error shapes (RFC 7807 + the spec-006 conflict response extension reused unchanged) are emitted for the new endpoints.
- **Frontend**: Vitest + React Testing Library for the wizard steps, the inventory table, the details page, and the form components. Playwright E2E for the full onboarding flow (mocked Graph + mocked ARM via MSW handlers); axe-playwright for a11y on every new route + every wizard step. Each Playwright test consumes the spec-007 authenticated fixture.
- **IaC**: existing `iac-validate.yml` workflow + the BT-IAC-001..007 gates cover the role-assignment additions and the App Role / Graph permission additions automatically. No new module → no new module-level tests.

**Target Platform**:
- Backend: Linux container on Azure Container Apps (existing dev env); .NET 10 runtime.
- Frontend: SSR-capable Next.js 16 container on Azure Container Apps (existing); React 19; browser baseline = last two majors of Chrome/Edge/Firefox/Safari (desktop) + iPadOS Safari + Android Chrome.

**Performance Goals** (binding — derived from FR-037, FR-038, FR-039, SC-002, SC-003, SC-004):
- Namespace inventory list/search p95 < 1s under expected load (hundreds of onboarded namespaces per environment).
- Namespace details page load p95 < 500ms under expected load.
- Synchronous validation run p95 < 15s under normal ARM responsiveness; per-check hard timeout enforced (research §5).
- Frontend Core Web Vitals on inventory + details + wizard screens: LCP ≤ 2.5s, INP ≤ 200ms, CLS ≤ 0.1.

**Constraints**:
- Constitution: Azure-first, Minimal APIs (not Controllers), OpenTofu only, Vertical Slice Architecture, managed identity preferred, W3C Trace Context on every UI-originated HTTP call, dark-mode primary, RTL-safe via logical CSS properties, no second design system, no CSS-in-JS, no PII in telemetry.
- Spec-006 carryover: registry containers untouched (extended in place via nullable fields + new enum value); spec-006's polymorphic `/api/registry/*` endpoints rebound to **reject writes on documents where `source = Onboarded`** (read remains open) — this is the cleanest way to keep ownership invariants from being violated by a spec-006-shaped PUT. Detailed in research §8.
- Spec-005 carryover: no destructive changes; IaC additions are scoped and pass BT-IAC-001..007. The pipeline MI RBAC-Admin condition allowlist gains the Reader role GUID (Complexity Tracking #1) for forward optionality.
- Spec-003 carryover: backend authentication via `Microsoft.Identity.Web` JWT bearer is unchanged; the spec-008 endpoints declare both `[Authorize]` *and* a role policy (`CanAdministerNamespaces` — new, additive) — this is **stricter** than spec 006's "any authenticated tenant user may write" stance, and is the FR-032 clarified decision (Complexity Tracking #2 documents the 5th-role addition to the spec-003 role matrix).
- Spec-001 carryover: the namespace UI composes the existing brand primitives (`namespace-card`, `metadata-key-value-panel`, `data-table`, `entity-form-shell`, the conflict modal, etc.) — already shipped via spec 001 + spec 006. **No new design primitives.** The wizard's step indicator is a new composite component built from existing shadcn `Card` + `Badge` + framer-motion (already pinned).
- Operator-supplied namespace Reader-role grant is out-of-band (Complexity Tracking #1); the wizard step 1 surfaces a copy-pasteable `az role assignment create` block populated with the workload UAMI's `principalId` (returned by a new public `/api/namespaces/identity` endpoint).

**Scale/Scope**:
- Backend: 1 new vertical slice family `Features/Namespaces/` with sub-slices for `Onboarding/`, `Inventory/`, `Details/`, `Metadata/`, `Ownership/`, `Lifecycle/`, `Validation/`, `Identity/` (the read-only `/api/namespaces/identity` endpoint), plus `_Shared/` (DTOs, validators, validation runner, ARM client adapter, Graph picker adapter, storage ports). Estimated 3500–5000 LOC of C# + tests.
- Frontend: ~5 new App Router segments under `web/app/(authenticated)/namespaces/` (`page.tsx` inventory, `[id]/page.tsx` details, `[id]/edit/page.tsx` edit, `[id]/lifecycle/page.tsx` lifecycle, `onboard/page.tsx` wizard), ~25 new React components composing existing primitives (the wizard step components, the inventory data table, the details panels, the Entra picker, the lifecycle dialog, the validation run viewer), ~8 RHF + Zod form modules (one per wizard step + the edit forms). Estimated 5000–7000 LOC of TS/TSX + tests.
- IaC: 0 new modules; ~30 LOC across the existing `app-registration-roles`, `graph-permissions`, `cosmos-registry-store`, and `platform-bootstrap` modules + a new `iac/runbooks/grant-namespace-reader.md` doc.
- Total: ~8.5k–12k LOC including tests, narrower than spec 006.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Constitution version: 1.0.0 (`.specify/memory/constitution.md`, ratified 2026-05-14).

### Principle I — Azure-First Architecture

**Gate**: ✅ PASS. Every new code path is Azure-native: `Azure.ResourceManager.ServiceBus` for ARM probing, `Microsoft.Graph` 5.x for Entra picker, existing `Microsoft.Azure.Cosmos` for persistence, existing `Azure.Monitor.OpenTelemetry.AspNetCore` for telemetry. No multi-cloud abstraction is introduced. The validation runner's five checks are *deliberately* Azure-Service-Bus-shaped per Principle VI (broader broker support reserved for a future spec).

### Principle II — API-First Design

**Gate**: ✅ PASS. The slice ships a full REST surface (`/api/namespaces`, `/api/namespaces/{id}`, `/api/namespaces/{id}/metadata`, `/api/namespaces/{id}/ownership`, `/api/namespaces/{id}/lifecycle`, `/api/namespaces/{id}/validation-runs`, `/api/namespaces/{id}/validation-runs/{runId}`, `/api/namespaces/identity`) with OpenAPI 3.1 documents generated by the existing `Microsoft.AspNetCore.OpenApi` pipeline and authored as `contracts/namespace-onboarding-api.yaml`. The UI consumes only these public endpoints — no UI backdoor. The new contract is version `v1` per the project's emerging media-type convention (`application/vnd.busterminal.namespaces+json; v=1`). The existing spec-006 conflict response (`contracts/conflict-response.schema.json`) is reused unchanged for concurrent-edit conflicts on metadata/ownership PUTs.

### Principle III — Strong Domain Modeling

**Gate**: ✅ PASS — strengthened relative to spec 006. Spec 008 extends spec 006's existing `RegistryNamespace` in place rather than introducing a parallel entity (per the spec's Assumptions and Q4 clarification). The new fields are nullable additions; `source = Onboarded` joins `Manual` as a peer enum value. Vocabulary remains uniform across API, persisted JSON, search-index projection, and OTel attributes — the `data-model.md §Naming Cross-Reference` confirms this. The two-axis status model (`lifecycleStatus` operational vs spec-006 `status` governance) is explicit in the data model and in every API surface.

### Principle IV — Security by Default

**Gate**: ✅ PASS with one acknowledged variance recorded in Complexity Tracking #1. Service-to-Azure-ARM and service-to-Graph use the existing workload UAMI via `DefaultAzureCredential`. No new secrets are introduced (no connection strings, no SAS tokens — FR-017, FR-033, SC-007). The new `namespace-administrator` role tightens — does NOT loosen — the spec-006 "any authenticated tenant user may write" stance for namespace mutations. The single variance is the operator-supplied namespace Reader-role grant being out-of-band rather than in OpenTofu (rationale in Complexity Tracking).

### Principle V — Operational Excellence

**Gate**: ✅ PASS. The slice introduces a new `ActivitySource` `"BusTerminal.NamespaceOnboarding"` emitting four span trees: `namespace.onboarding.run` (wraps the wizard's step-5 register call end-to-end, child spans per validation check), `namespace.validation.rerun` (wraps standalone re-runs from the details page, same per-check children), `namespace.lifecycle.transition` (per transition; carries reason note as a span attribute), `namespace.metadata.update` / `namespace.ownership.update` (per write; carries the field-change count, NOT the values themselves — PII boundary). Authorization-failure (403) paths emit a dedicated `WARNING` log + span event per FR-035. All diagnostics route to the existing LAW via the existing AI pipeline; no new diagnostic resources are introduced. The new container `namespace-validation-runs` is the durable record of every validation outcome (FR-016 / SC-005); a future ops-hardening spec can dashboard it.

### Principle VI — Incremental Extensibility

**Gate**: ✅ PASS. The data model uses Azure-Service-Bus-specific terminology in the new Cosmos container shape (`Microsoft.ServiceBus/namespaces/{name}` ARM ID format) but the validation-runner architecture (parallel checks, per-check timeout, ValidationRun persistence, span-tree emission) is broker-agnostic — a future broker type (Kafka cluster, RabbitMQ vhost) would add a new `BrokerKind` discriminator field, a new validator implementation, and reuse the same persistence + UI scaffolding. The `validationStatus` and `lifecycleStatus` enums are deliberately small and additive — new values can join without breaking existing readers. The `ownership` block is structured (Entra `objectId` + role) rather than free-form, which makes it future-friendly for governance workflows that need to query "every namespace owned by X" — already feasible without a schema change.

### Technology Standards (Constitution §Technology Standards)

| Standard | Compliance |
|---|---|
| Backend: .NET 10 + ASP.NET Core Minimal APIs preferred | ✅ Minimal APIs. New endpoints registered via `MapNamespaceEndpoints()` following the spec-006 endpoint-builder pattern. No Controllers. |
| Vertical Slice Architecture | ✅ New code lives in `Features/Namespaces/{Onboarding,Inventory,Details,Metadata,Ownership,Lifecycle,Validation,Identity,_Shared}` — one folder per slice, endpoint + request/response DTOs + validators + handler + persistence calls. |
| Built-in DI container | ✅ All new services registered via `Program.cs` extension methods (`AddNamespaceOnboardingFeature`). No third-party DI. |
| OpenAPI for every public API | ✅ `Microsoft.AspNetCore.OpenApi` generates the runtime document; `contracts/namespace-onboarding-api.yaml` is the authoring source and is verified against the runtime document by a CI assertion (same pattern as spec 006). |
| Frontend: Next.js 16.x App Router | ✅ App Router only; no Pages Router. RSC by default for inventory + details; Client Components only for the wizard, edit forms, lifecycle dialog, Entra picker. |
| TypeScript strict | ✅ Existing config unchanged. |
| Tailwind v4 + shadcn/ui (project-owned) | ✅ All new UI composes existing shadcn primitives. No CSS-in-JS. No second design system. The wizard step indicator is composed from `Card` + `Badge` + framer-motion — no new primitive family. |
| TanStack Table (data tables) | ✅ Used for the namespace inventory. |
| React Hook Form + Zod | ✅ All forms (wizard steps, edit metadata, edit ownership, lifecycle action) use RHF + Zod; the same Zod schema is the source of truth for client-side validation and is mirrored against the backend FluentValidation rules via a contract test. |
| Framer Motion sparingly | ✅ Used only for the wizard step transitions, validation-run progress indicators, and dialog transitions; `prefers-reduced-motion` honored. |
| next-themes (dark/light) | ✅ Existing theme provider unchanged. |
| Browser baseline | ✅ Unchanged. |
| Cosmos DB metadata storage | ✅ Per spec; new container added on the existing canonical database. |
| Container Apps + ACR | ✅ Backend reuses existing image build pipeline. No new container image. |
| OpenTofu, AVM preferred, pinned | ✅ All IaC additions are inputs to existing modules — no new module. No AVM-eligible resource added in v1. |
| Managed identity preferred over secrets | ✅ Workload UAMI for ARM + Cosmos + Graph + App Insights AAD ingestion; no new secrets. |
| W3C Trace Context propagation | ✅ Existing `web/lib/http/` client unchanged; new namespace data layer consumes it. |
| All Azure diagnostics → LAW via `allLogs`-only convention | ✅ No new resources → no new diagnostic settings. |

### Engineering Workflow & Quality Standards

| Standard | Compliance |
|---|---|
| Spec-driven development | ✅ `/speckit-specify` → `/speckit-clarify` → `/speckit-plan` (this artifact) → `/speckit-tasks` → `/speckit-implement`. |
| CI gates (build, unit, lint, format, security, dependency scan) | ✅ Existing CI workflows unchanged. New backend code, frontend code, and IaC additions are picked up automatically. |
| Testing strategy (unit/integration/contract/UI/E2E) | ✅ All five layers present in the plan. |
| Trunk-based with feature branches | ✅ On `008-namespace-onboarding`. |

### Result: ✅ PASS (with two documented exceptions under Complexity Tracking)

Phase 0 may proceed.

## Project Structure

### Documentation (this feature)

```text
specs/008-namespace-onboarding/
├── plan.md                                       # This file
├── research.md                                   # Phase 0 — numbered decisions
├── data-model.md                                 # Phase 1 — entity model + persistence layout + audit schema
├── quickstart.md                                 # Phase 1 — local dev + first-onboard walkthrough + runbook excerpts
├── contracts/                                    # Phase 1
│   ├── namespace-onboarding-api.yaml             # OpenAPI 3.1 — onboarding, inventory, lifecycle, validation endpoints
│   ├── onboarded-namespace.schema.json           # Canonical JSON shape of an onboarded namespace document
│   ├── validation-run.schema.json                # ValidationRun document shape
│   ├── ownership-assignment.schema.json          # Structured OwnershipAssignment shape
│   ├── namespace-audit-event.schema.json         # Extended audit event shape (lifecycle reason, validation outcomes)
│   └── outputs-contract.md                       # Incremental IaC outputs + admin-consent attestation guide
├── checklists/
│   └── requirements.md                           # (created by /speckit-specify)
└── tasks.md                                      # Phase 2 output — NOT created by /speckit-plan
```

### Source Code (repository root)

```text
api/
├── BusTerminal.Api/
│   ├── Features/
│   │   ├── Registry/                                       # Existing (spec 006) — UNTOUCHED except for two surgical changes:
│   │   │   ├── _Shared/                                    #   1. RegistrySource.cs — add Onboarded enum value
│   │   │   │   ├── RegistrySource.cs                       #      (Manual, Onboarded — Discovered reserved)
│   │   │   ├── _Shared/UpdateEndpoint.cs                   #   2. Polymorphic UpdateEndpoint — reject writes when source = Onboarded
│   │   │   ├── _Shared/DeleteEndpoint.cs                   #      (forward to /api/namespaces/{id}/lifecycle?action=archive instead)
│   │   │   └── (everything else untouched)
│   │   ├── Namespaces/                                     # NEW — top-level slice family for spec 008
│   │   │   ├── _Shared/
│   │   │   │   ├── OnboardedNamespace.cs                   # Extended RegistryNamespace fields (nullable on the shared type, populated for Onboarded source)
│   │   │   │   ├── OwnershipAssignment.cs                  # { role, principalType, objectId, displayNameSnapshot, assignedAtUtc, assignedBy }
│   │   │   │   ├── OwnershipBlock.cs                       # { primaryOwner: OwnershipAssignment, secondaryOwners: [], stewards: [], supportContacts: [] }
│   │   │   │   ├── LifecycleStatus.cs                      # Closed enum: PendingValidation (transient), Active, Disabled, Archived
│   │   │   │   ├── ValidationStatus.cs                     # Closed enum: Healthy, Degraded, Unhealthy
│   │   │   │   ├── ValidationRun.cs                        # Persisted document shape (PK /namespaceId)
│   │   │   │   ├── ValidationCheckName.cs                  # Closed enum: Existence, Accessibility, RequiredPermissions, IdentityAuthorization, ApiReachability
│   │   │   │   ├── ValidationCheckResult.cs                # { name, outcome, reason, durationMs }
│   │   │   │   ├── INamespaceValidationRunStore.cs         # Persistence port
│   │   │   │   ├── IArmNamespaceProbe.cs                   # Adapter port for Azure.ResourceManager.ServiceBus
│   │   │   │   ├── IGraphPrincipalPicker.cs                # Adapter port for Microsoft.Graph user/group lookups
│   │   │   │   ├── NamespaceArmIdParser.cs                 # Canonical ARM id parser; rejects cross-tenant, wrong-type, malformed
│   │   │   │   ├── NamespaceAdministratorPolicy.cs         # `CanAdministerNamespaces` AuthZ policy definition
│   │   │   │   └── NamespaceEndpointsBuilder.cs            # MapGroup pattern + CanAdministerNamespaces filter on writes
│   │   │   ├── Onboarding/
│   │   │   │   ├── OnboardingEndpoint.cs                   # POST /api/namespaces — orchestrates final register
│   │   │   │   ├── OnboardingRequest.cs                    # { azureResourceId, displayName, ..., ownership, validationRunId }
│   │   │   │   ├── OnboardingResponse.cs
│   │   │   │   └── OnboardingValidator.cs                  # FluentValidation: enforces aggregate Healthy/Degraded; rejects partial persistence (FR-023a)
│   │   │   ├── Inventory/
│   │   │   │   ├── InventoryEndpoint.cs                    # GET /api/namespaces — filter, sort, search, paginate, hide Archived by default
│   │   │   │   ├── InventoryRequest.cs                     # query params model
│   │   │   │   └── InventoryResponse.cs
│   │   │   ├── Details/
│   │   │   │   ├── DetailsEndpoint.cs                      # GET /api/namespaces/{id} — includes resolved ownership display names, latest validation run
│   │   │   │   └── DetailsResponse.cs
│   │   │   ├── Metadata/
│   │   │   │   ├── UpdateMetadataEndpoint.cs               # PUT /api/namespaces/{id}/metadata — concurrent-edit conflict via existing 006 pattern
│   │   │   │   ├── UpdateMetadataRequest.cs
│   │   │   │   └── UpdateMetadataValidator.cs
│   │   │   ├── Ownership/
│   │   │   │   ├── UpdateOwnershipEndpoint.cs              # PUT /api/namespaces/{id}/ownership — full-block replace
│   │   │   │   ├── UpdateOwnershipRequest.cs
│   │   │   │   └── UpdateOwnershipValidator.cs
│   │   │   ├── Lifecycle/
│   │   │   │   ├── TransitionLifecycleEndpoint.cs          # POST /api/namespaces/{id}/lifecycle — action: disable | enable | archive | restore
│   │   │   │   ├── LifecycleTransitionRequest.cs           # { action, reason }
│   │   │   │   └── LifecycleTransitionValidator.cs         # Enforces FR-023 permitted transitions
│   │   │   ├── Validation/
│   │   │   │   ├── RunValidationEndpoint.cs                # POST /api/namespaces/{id}/validation-runs — synchronous, p95 < 15s
│   │   │   │   ├── ListValidationRunsEndpoint.cs           # GET /api/namespaces/{id}/validation-runs — paginated, time-descending
│   │   │   │   ├── GetValidationRunEndpoint.cs             # GET /api/namespaces/{id}/validation-runs/{runId}
│   │   │   │   ├── NamespaceValidationRunner.cs            # Orchestrates 5 parallel checks + per-check timeout + ValidationRun persistence
│   │   │   │   ├── NamespaceValidationActivitySource.cs    # ActivitySource singleton — "BusTerminal.NamespaceOnboarding"
│   │   │   │   └── Checks/
│   │   │   │       ├── ExistenceCheck.cs                   # ARM GET on namespace resource
│   │   │   │       ├── AccessibilityCheck.cs               # ARM call succeeds without auth error
│   │   │   │       ├── RequiredPermissionsCheck.cs         # ARM permissions/list at namespace scope; verifies Reader
│   │   │   │       ├── IdentityAuthorizationCheck.cs       # Token exchange completion
│   │   │   │       └── ApiReachabilityCheck.cs             # Service Bus management endpoint metadata probe
│   │   │   └── Identity/
│   │   │       └── WorkloadIdentityEndpoint.cs             # GET /api/namespaces/identity — returns workload UAMI principalId (read-only, AuthN-only)
│   │   ├── Identity/                                       # Existing (untouched)
│   │   ├── Health/                                         # Existing (untouched)
│   │   └── RoleProbes/                                     # Existing (untouched)
│   ├── Infrastructure/
│   │   ├── Persistence/
│   │   │   ├── CosmosNamespaceValidationRunStore.cs        # NEW — namespace-validation-runs container; append-only writes; namespace-scoped query
│   │   │   ├── CosmosRegistryOptions.cs                    # EXTENDED — add ValidationRunsContainer = "namespace-validation-runs"
│   │   │   └── (existing CosmosRegistryEntityStore, CosmosAuditEventStore — untouched)
│   │   ├── ServiceBus/                                     # NEW directory
│   │   │   ├── ArmNamespaceProbe.cs                        # IArmNamespaceProbe via Azure.ResourceManager.ServiceBus + DefaultAzureCredential
│   │   │   └── ArmNamespaceProbeOptions.cs                 # Per-check timeout configuration
│   │   ├── Graph/                                          # EXTENDED — extends existing IGraphClient
│   │   │   ├── GraphPrincipalPicker.cs                     # NEW — implements IGraphPrincipalPicker via the existing GraphServiceClient
│   │   │   └── (existing GraphClient.cs — untouched)
│   │   ├── Identity/
│   │   │   └── WorkloadIdentityProvider.cs                 # NEW — wraps Azure.Identity to expose the workload UAMI's principalId at runtime
│   │   └── Authentication/ Configuration/ Credentials/ Observability/   # Existing (untouched)
│   ├── Authorization/
│   │   ├── PlatformRole.cs                                 # EXTENDED — add NamespaceAdministrator value (claim "BusTerminal.NamespaceAdministrator")
│   │   ├── PlatformPrincipalExtensions.cs                  # NEW — `IsNamespaceAdministrator()` extension on PlatformPrincipal
│   │   ├── RolePolicies.cs                                 # EXTENDED — add CanAdministerNamespaces policy
│   │   └── (existing PrincipalAccessor, RolePolicies — extended in place)
│   └── Program.cs                                          # EXTENDED — services.AddNamespaceOnboardingFeature(); app.MapNamespaceEndpoints();
├── BusTerminal.Api.Tests/                                  # EXTENDED — Features.Namespaces.* test suites
│   └── Features/Namespaces/                                # NEW — one folder per sub-slice
└── BusTerminal.Indexer/                                    # Existing (untouched — the indexer continues to index registry-entities; new fields appear as projected metadata via the existing SearchDocumentMapper)

web/
├── app/
│   ├── (authenticated)/
│   │   ├── layout.tsx                                       # Existing (untouched)
│   │   ├── platform-status/ … registry/                     # Existing (untouched)
│   │   └── namespaces/                                      # NEW — top-level App Router segment for spec 008
│   │       ├── layout.tsx                                   # Nav + breadcrumb shell
│   │       ├── page.tsx                                     # /namespaces — Inventory (RSC list + Client filter/search)
│   │       ├── onboard/
│   │       │   └── page.tsx                                 # /namespaces/onboard — 5-step wizard (Client Component)
│   │       └── [id]/
│   │           ├── page.tsx                                 # /namespaces/{id} — Details (RSC)
│   │           ├── edit/
│   │           │   └── page.tsx                             # /namespaces/{id}/edit — metadata + ownership edit forms (Client)
│   │           └── lifecycle/
│   │               └── page.tsx                             # /namespaces/{id}/lifecycle — transition dialog flow (Client)
├── components/
│   ├── namespaces/                                          # NEW — namespace-specific composite components
│   │   ├── wizard/
│   │   │   ├── namespace-onboarding-wizard.tsx              # Client — wizard root; RHF + sessionStorage
│   │   │   ├── wizard-stepper.tsx                           # Custom step indicator (Card + Badge + motion)
│   │   │   ├── step-1-identification.tsx                    # ARM id + cross-tenant guard
│   │   │   ├── step-2-metadata.tsx                          # Display name, env, business metadata, tags, notes
│   │   │   ├── step-3-ownership.tsx                         # Entra picker for primary/secondary/stewards/support
│   │   │   ├── step-4-validation.tsx                        # Run validation, render per-check progress + result
│   │   │   ├── step-5-review.tsx                            # Final review + Register
│   │   │   └── grant-reader-guidance.tsx                    # Copy-pasteable az role assignment block (step 1 sidebar)
│   │   ├── inventory/
│   │   │   ├── namespace-inventory-table.tsx                # TanStack Table; URL-driven filters
│   │   │   ├── namespace-inventory-filters.tsx
│   │   │   ├── lifecycle-status-badge.tsx                   # Color + icon + text — never color-alone
│   │   │   └── validation-status-badge.tsx                  # Same convention
│   │   ├── details/
│   │   │   ├── namespace-details-shell.tsx
│   │   │   ├── namespace-metadata-panel.tsx
│   │   │   ├── namespace-ownership-panel.tsx                # Resolves Entra displayNames via /me-style Graph proxy
│   │   │   ├── namespace-validation-panel.tsx               # Latest run + per-check breakdown + Re-run button
│   │   │   └── namespace-audit-panel.tsx                    # Recent N events (lifecycle + metadata + ownership + validation)
│   │   ├── edit/
│   │   │   ├── metadata-edit-form.tsx                       # RHF + Zod; conflict modal reused from spec 006
│   │   │   └── ownership-edit-form.tsx
│   │   ├── lifecycle/
│   │   │   ├── lifecycle-action-dialog.tsx                  # Confirms disable / enable / archive / restore + reason note
│   │   │   └── lifecycle-transition-button.tsx
│   │   └── shared/
│   │       ├── entra-principal-picker.tsx                   # Reusable picker (User|Group) backed by /api/namespaces/_picker (proxy)
│   │       ├── azure-resource-id-input.tsx                  # ARM id parser + inline validation
│   │       └── namespace-detail-link.tsx
│   ├── registry/                                            # Existing — UNTOUCHED. Spec 006's surface remains for legacy Manual namespaces.
│   ├── domain/ app-shell/ navigation/ layout/ data-table/ feedback/ forms/ ui/   # Existing (untouched)
├── lib/
│   ├── namespaces/                                          # NEW
│   │   ├── api.ts                                           # Typed fetch client over /api/namespaces (RSC-safe + client-safe)
│   │   ├── schemas.ts                                       # Zod schemas matching backend FluentValidation
│   │   ├── types.ts                                         # TypeScript counterparts (inferred from Zod)
│   │   ├── query-keys.ts                                    # TanStack Query key factories
│   │   ├── wizard-storage.ts                                # sessionStorage-backed wizard state with cancel-clearing
│   │   └── lifecycle.ts                                     # Transition rules + UI predicate helpers
│   ├── http/ observability/ auth/ design-system/ registry/  # Existing (untouched)
├── tests/
│   ├── e2e/namespaces/                                      # NEW — Playwright e2e specs (onboard happy/fail, lifecycle, edit, conflict)
│   ├── a11y/namespaces/                                     # NEW — axe-playwright a11y gates on each route + each wizard step
│   └── unit/namespaces/                                     # NEW — Vitest unit tests for new components + lib
└── package.json                                             # NO additions

iac/
├── modules/
│   ├── app-registration-roles/                              # EXTENDED — add namespace-administrator app role definition (var.role_definitions)
│   ├── graph-permissions/                                   # EXTENDED — add Group.Read.All permission UUID
│   ├── cosmos-registry-store/                               # EXTENDED — add "namespace-validation-runs" container (PK /namespaceId, autoscale RU low band)
│   ├── workload-identity/                                   # Existing — UNTOUCHED (no new role assignments at IaC time; operator-supplied namespace grants are out-of-band — see runbook)
│   └── (everything else untouched)
├── platform-bootstrap/main.tf                               # EXTENDED — append Reader role GUID (acdd72a7-...) to pipeline MI RBAC-Admin condition allowlist
├── environments/
│   ├── dev/main.tf                                          # EXTENDED — pass new role definition + new graph permission UUIDs
│   ├── test/main.tf                                         # EXTENDED — same
│   └── prod/main.tf                                         # EXTENDED — same
└── runbooks/                                                # NEW directory
    └── grant-namespace-reader.md                            # Operator runbook for granting Reader on operator-supplied namespaces
```

**Structure Decision**: Spec 008 fits cleanly into the existing repository layout. The backend slice family `Features/Namespaces/` matches the vertical-slice convention already used by `Features/Registry/`, `Features/Identity/`, and `Features/RoleProbes/`. The slice is parallel to (not nested under) `Features/Registry/` because the `/api/namespaces/*` route prefix is intentionally separate from `/api/registry/*` to (a) gate writes on a strictly stricter authorization policy than spec 006's permissive stance and (b) keep the spec-008 DTOs from being squeezed into the spec-006 polymorphic request shape. The frontend follows the existing `web/app/(authenticated)/{registry,platform-status}/` segment pattern. The IaC additions are inputs to existing modules + one new doc — no new module directory, consistent with the spec's "no new Azure resources" stance.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|---|---|---|
| **Reader-role grant on operator-supplied Service Bus namespaces is performed OUT-OF-BAND by the operator (`az role assignment create`) rather than declared in OpenTofu.** This is a Principle-IV (Security by Default) variance — the project's strong convention is "all role assignments live in OpenTofu" — and a soft BT-IAC-001..007 boundary case (the resources being granted-against are not in BusTerminal's IaC inventory). | BusTerminal's IaC operates on its own infrastructure and cannot enumerate operator-supplied namespace resource IDs at plan time — they are owned by the operator's separate Azure subscriptions. Pre-declaring them via a tfvars list of namespace ARM IDs would require the operator to update tfvars and re-apply the pipeline for every new onboarded namespace, contradicting the spec's "small-scale manual onboarding" UX goal (SC-001: under 5 minutes per onboarding). Instead, BusTerminal exposes the workload UAMI's `principalId` via the new `GET /api/namespaces/identity` endpoint, and the wizard step 1 sidebar surfaces a copy-pasteable `az role assignment create --assignee {principalId} --role Reader --scope {armId}` block. The validation step's `RequiredPermissions` check is the verification surface — if the operator forgets, validation fails with a clear "Reader role not granted on namespace scope — run the linked `az role assignment` command" remediation hint. The Reader role GUID IS added to the pipeline MI RBAC-Admin condition allowlist (a one-line IaC change in `platform-bootstrap/main.tf`) so a future IaC-driven grant path can be introduced as a non-breaking enhancement without re-litigating the policy. The runbook lives at `iac/runbooks/grant-namespace-reader.md` and is referenced from the wizard. | Pre-declaring every onboardable namespace in tfvars (Option A in research §4) was rejected because it requires IaC re-apply for every new onboarding — a sub-five-minute UX path would be gated on a CI run, contradicting SC-001. Granting subscription-scope Reader (Option C in research §4) was rejected because BT-IAC-004 explicitly forbids workload UAMIs receiving subscription-wide grants — that's not a soft preference, it's a policy gate that would fail the IaC pipeline. The runtime-ARM-action approach (Option D in research §4) was rejected because it requires BusTerminal to itself hold subscription-scope `Microsoft.Authorization/roleAssignments/write` — a privilege escalation that bypasses BT-IAC-004 in the worst possible way. Runbook-driven (Option B in research §4) is the smallest surface that lets v1 ship without violating a hard IaC policy, and the validation runner's `RequiredPermissions` check ensures the prerequisite is *verified* before any onboarding can complete — so the security posture is enforced at runtime even if the role-grant authoring is procedural rather than declarative. |
| **A fifth platform role (`namespace-administrator`) is added to the BusTerminal Entra App, extending spec 003's "exactly four roles" matrix (Admin / Operator / Reader / Developer).** Spec 003's role-permission matrix (`specs/003-auth-and-identity/contracts/role-permission-matrix.md`) is a binding contract; adding a new role is a Principle-IV (Security by Default — least privilege) extension that intersects with that contract. | This is the FR-032 clarified decision (Clarification Q1, recorded in `spec.md §Clarifications`). The new role is **additive** (it does not change the semantics of any of the four existing roles) and is **strictly tighter** than spec 006's "any authenticated tenant user may write" stance for namespace mutations — so the security posture improves, it does not degrade. Spec 006 explicitly recorded the lack of role gating as a Complexity Tracking entry expecting a future "registry governance" spec to restore it; spec 008 is that restoration for the namespace surface. The new role is declared in the existing `iac/modules/app-registration-roles/` module via one new entry in the `role_definitions` map — no new IaC module, no schema-breaking change to the role matrix's contract format. The new role MUST be admin-consented and assigned via Enterprise App per the existing spec-003 runbook (`specs/003-auth-and-identity/quickstart.md §A.2.3`); the spec 003 role-permission matrix contract document will be updated as a follow-up note to reflect the new role's existence (without changing the four existing roles' semantics). | Gating namespace onboarding behind the existing `Admin` role was rejected because `Admin` carries the full platform-administration capability surface (can author App Roles, can administer infrastructure, etc.) — overpowered for routine namespace onboarding. Gating behind `Operator` was rejected because `Operator` is intentionally domain-mutation-only per spec 003 and does not carry the implicit "authoritative attestation about an Azure resource" semantics that namespace onboarding implies; a dedicated role makes the attestation explicit and grantable independently of platform operation rights. Leaving namespace onboarding open to "any authenticated tenant user" (spec 006's stance) was rejected by Clarification Q1 — onboarding decisions carry governance weight and the spec explicitly clarified that they MUST be gated. |

---

## Post-Design Constitution Re-Check

*Performed after Phase 0 (`research.md`) and Phase 1 (`data-model.md`, `contracts/`, `quickstart.md`).*

Re-evaluating each principle against the concrete decisions captured in the Phase-0 and Phase-1 artifacts:

- **I. Azure-First Architecture** — ✅ Still PASS. Research §1 confirms `Azure.ResourceManager.ServiceBus` as the chosen ARM SDK — Azure-native, no abstraction layer. Research §2 confirms `Microsoft.Graph` 5.x reuse for the Entra picker — Azure-native. No multi-cloud abstraction crept in during design.

- **II. API-First Design** — ✅ Still PASS. `contracts/namespace-onboarding-api.yaml` is the formal OpenAPI 3.1 document for the namespace onboarding surface — versioned (`v1`), automation-friendly, generated-and-verified against the runtime document. The Entra picker is exposed as a proxied API endpoint (not a UI backdoor against Graph); validation runs are exposed as resource collections under the namespace; the workload identity endpoint is part of the documented contract.

- **III. Strong Domain Modeling** — ✅ Still PASS — strengthened relative to spec 006. The `data-model.md §Naming Cross-Reference` table confirms every term in the spec-008 surface (LifecycleStatus, ValidationStatus, OwnershipAssignment, ValidationRun, etc.) maps consistently across the OpenAPI document, the persisted JSON, the search-index projection, and OTel span attributes. The two-axis status model is explicit and machine-checkable.

- **IV. Security by Default** — ✅ PASS with the documented Complexity-Tracking variance #1 (operator-supplied namespace Reader grant). Research §3 confirms the `RequiredPermissions` check uses ARM's `permissions/list` operation at the namespace scope — verifying the *effective* permissions of the workload UAMI rather than enumerating role assignments (more accurate; tolerant of nested role assignments at parent scopes). Research §10 confirms FR-006 cross-tenant enforcement reads the configured tenant id from the existing `AzureAd:TenantId` config; no new tenant-trust surface is introduced.

- **V. Operational Excellence** — ✅ Still PASS. Research §5 confirms the validation runner's span tree shape; every check emits its own child span with `name`, `outcome`, `duration_ms`, `reason_category` attributes (no PII; reasons are categorical, not free-form). Authorization failures (403 on namespace write endpoints) emit a `WARNING`-level log with the actor's `objectId` (correlation), the requested action, and the rejection reason. Drift detection during re-validation (FR-029) emits a `NamespaceMetadataDriftDetected` span event so an ops dashboard can subscribe later without requiring a fresh telemetry pipeline.

- **VI. Incremental Extensibility** — ✅ Strengthened by Phase 1. The `ValidationCheck` interface (research §5) accepts a `NamespaceArmId` and returns a typed result — adding a new check kind (e.g., "PrivateEndpointReachability" for a future networking spec) is a one-file addition with no orchestrator changes. The `lifecycleStatus` and `validationStatus` enums are deliberately small and additive. The `OwnershipBlock` shape is structured Entra-backed but the `OwnershipAssignment` shape's `principalType` field already supports `User` and `Group` — a future "service principal as owner" addition is a single new enum value.

### Technology Standards re-check

| Standard | Compliance after design |
|---|---|
| Minimal APIs (not Controllers) | ✅ Confirmed in `Features/Namespaces/**/*Endpoint.cs` pattern |
| Vertical Slice Architecture | ✅ Confirmed: one slice per concern + `_Shared` |
| OpenAPI for public APIs | ✅ `contracts/namespace-onboarding-api.yaml` + runtime doc + CI assertion |
| Cosmos DB metadata + AI Search discovery | ✅ Confirmed: new container on existing account; AI Search not touched in v1 |
| OpenTofu, AVM preferred, pinned | ✅ All IaC additions are inputs to existing modules — no new module → AVM eligibility is N/A |
| Managed identity preferred | ✅ Confirmed: no new secrets, ARM + Graph + Cosmos all use the existing workload UAMI |
| W3C Trace Context propagation | ✅ Confirmed: existing `web/lib/http/` client unchanged, namespace data layer consumes it |
| All Azure diagnostics → LAW via `allLogs`-only convention | ✅ No new resources → no new diagnostic settings required |

### Result: ✅ PASS

No new Complexity Tracking entries needed beyond the two documented above (operator-supplied namespace Reader grant out-of-band + 5th platform role). Phase 0 and Phase 1 artifacts are coherent with the constitution.

---

## Artifact Index (post-`/speckit-plan`)

| Artifact | Purpose | Status |
|---|---|---|
| [`plan.md`](./plan.md) | This file — Technical Context, Constitution Check, Project Structure, Complexity Tracking | ✅ produced |
| [`research.md`](./research.md) | Phase 0 — numbered decisions resolving every NEEDS-CLARIFICATION + best-practices research | ✅ produced |
| [`data-model.md`](./data-model.md) | Phase 1 — extended namespace entity model + ValidationRun + OwnershipAssignment + naming cross-reference | ✅ produced |
| [`contracts/namespace-onboarding-api.yaml`](./contracts/namespace-onboarding-api.yaml) | Phase 1 — OpenAPI 3.1 contract for namespace onboarding, inventory, details, lifecycle, validation, and identity endpoints | ✅ produced |
| [`contracts/onboarded-namespace.schema.json`](./contracts/onboarded-namespace.schema.json) | Phase 1 — canonical JSON shape of an onboarded namespace document | ✅ produced |
| [`contracts/validation-run.schema.json`](./contracts/validation-run.schema.json) | Phase 1 — ValidationRun document shape | ✅ produced |
| [`contracts/ownership-assignment.schema.json`](./contracts/ownership-assignment.schema.json) | Phase 1 — structured OwnershipAssignment shape | ✅ produced |
| [`contracts/namespace-audit-event.schema.json`](./contracts/namespace-audit-event.schema.json) | Phase 1 — extended audit event shape | ✅ produced |
| [`contracts/outputs-contract.md`](./contracts/outputs-contract.md) | Phase 1 — incremental IaC outputs + admin-consent attestation guide | ✅ produced |
| [`quickstart.md`](./quickstart.md) | Phase 1 — operator walkthrough + runbook excerpts | ✅ produced |
| `tasks.md` | Phase 2 output — NOT created by `/speckit-plan`; produced by `/speckit-tasks` | ⏳ pending |
| `CLAUDE.md` SPECKIT-block reference | Updated to point at this plan | ✅ updated |
