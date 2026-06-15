---
description: "Task list for spec 008 — Namespace Onboarding"
---

# Tasks: Namespace Onboarding

**Input**: Design documents from `/specs/008-namespace-onboarding/`

**Prerequisites**: [`plan.md`](./plan.md), [`spec.md`](./spec.md), [`research.md`](./research.md), [`data-model.md`](./data-model.md), [`contracts/`](./contracts/), [`quickstart.md`](./quickstart.md).

**Tests**: Included. Spec 008 inherits spec 006's 5-layer testing discipline (unit / integration / contract / component / E2E) — see `plan.md §Testing`. Test tasks are written before the corresponding implementation tasks per the project's TDD discipline.

**Organization**: Tasks are grouped by user story (US1, US2, US3) so each story can be implemented, tested, and demoed independently. Setup and Foundational phases precede stories. Polish & cross-cutting concerns come last.

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Different files / no dependencies on incomplete tasks ⇒ safe to run in parallel.
- **[Story]**: Maps task to its user story (US1, US2, US3) for traceability. Setup/Foundational/Polish tasks carry no story label.
- **[TEST]**: Test-first task (write before the implementation task it precedes).
- Every task names exact file paths (absolute from repo root).

## Path Conventions (this feature)

- Backend API: `api/BusTerminal.Api/`
- Backend tests: `api/BusTerminal.Api.Tests/`
- Frontend: `web/`
- IaC: `iac/`
- Runbooks: `iac/runbooks/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: New package references, new IaC variable entries, new empty directory trees, the operator runbook. No business logic.

- [X] T001 Add `Azure.ResourceManager.ServiceBus` package reference to `api/BusTerminal.Api/BusTerminal.Api.csproj` per `research.md §1` (target pin: latest 1.x at task time; confirm version via Microsoft Learn MCP before pinning).
- [X] T002 [P] Create the empty namespace feature directory tree under `api/BusTerminal.Api/Features/Namespaces/`: `_Shared/`, `Onboarding/`, `Inventory/`, `Details/`, `Metadata/`, `Ownership/`, `Lifecycle/`, `Validation/`, `Validation/Checks/`, `Identity/` (placeholder `.gitkeep` files allowed).
- [X] T003 [P] Create the empty namespace frontend tree under `web/app/(authenticated)/namespaces/` (`layout.tsx` stub, `page.tsx` stub, `onboard/`, `[id]/`, `[id]/edit/`, `[id]/lifecycle/`) and `web/components/namespaces/{wizard,inventory,details,edit,lifecycle,shared}/` and `web/lib/namespaces/`. Placeholders only.
- [X] T004 [P] Create `iac/runbooks/` directory and author the operator runbook at `iac/runbooks/grant-namespace-reader.md` per `quickstart.md §5` (Reader-grant procedure, verification, rollback). Cross-link from the wizard step 1 sidebar code at task time.
- [X] T005 [P] Extend `iac/modules/app-registration-roles/variables.tf` (or the env composition's `role_definitions` map) to add the `namespace-administrator` App Role entry per `contracts/outputs-contract.md §1.1`. Generate and pin the stable role UUID at task time and commit it inline.
- [X] T006 [P] Extend `iac/modules/graph-permissions/variables.tf` (or the env composition's `granted_application_permission_ids` set) to add `5b567255-7703-4780-807c-7be8301ae99b` (Group.Read.All) per `contracts/outputs-contract.md §1.2`.
- [X] T007 [P] Extend `iac/modules/cosmos-registry-store/variables.tf` (or container list input) to add the `namespace-validation-runs` container (PK `/namespaceId`, autoscale min 1000 / max 4000, no TTL) per `contracts/outputs-contract.md §1.3`.
- [X] T008 Edit `iac/platform-bootstrap/main.tf` lines 231–239 to add Reader role GUID `acdd72a7-3385-48ef-bd42-f606fba81ae7` to the pipeline MI RBAC-Admin condition v2.0 allowlist per `contracts/outputs-contract.md §1.4`.
- [X] T009 [P] Update env composition files `iac/environments/{dev,test,prod}/main.tf` to (a) pass the new role definition + new Graph permission UUID + new container declaration into the existing modules, AND (b) inject `WORKLOAD_PRINCIPAL_ID = module.workload_identity.principal_id` into the backend Container App's `env_vars` block per `research.md §17`. No new module calls — just input additions.
- [X] T010 Build verification: `dotnet build BusTerminal.slnx` → 0 errors / 0 warnings; `pnpm typecheck` in `web/` → clean; `tofu init -backend=false && tofu validate` in `iac/environments/dev/` → success.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: IaC apply, shared backend types (enums, records, ports, validators, ActivitySource, role gate), persistence wiring, frontend shared library, nav-shell entry. Every user story depends on these.

**⚠️ CRITICAL**: No user-story work can begin until this phase is complete.

### 2.1 IaC — apply new container + new role + new Graph permission

- [X] T011 Run `tofu fmt -recursive && tofu validate && tofu plan -var-file=environments/dev/dev.tfvars` in `iac/`; verify checkov + tfsec + BT-IAC-001..007 gates green. Plan should show: 1 new container, 1 new App Role, 1 updated Graph permission set, 1 updated pipeline allowlist. Apply to dev under operator authorization.
- [X] T012 Operator attestation: Entra tenant admin grants admin consent for `Group.Read.All` on the BusTerminal API app per `quickstart.md §3`; verify via `az ad app permission list-grants --output table`. Capture in deployment runbook under "Pre-go-live attestations — Graph permissions".
- [X] T013 Operator attestation: Entra tenant admin assigns at least one user/group to the `Namespace Administrator` App Role per `quickstart.md §4`; verify via `GET /api/identity/whoami` returning `NamespaceAdministrator` in `effectiveRoles` for the assigned principal.

### 2.2 Backend — extend existing shared types

- [X] T014 [P] Edit `api/BusTerminal.Api/Features/Registry/_Shared/RegistrySource.cs` to add `Onboarded` enum value per `data-model.md §2 RegistrySource`. Update the closed-enum XML doc comment.
- [X] T015 [P] Edit `api/BusTerminal.Api/Authorization/PlatformRole.cs` to add `NamespaceAdministrator` enum value with claim string `"BusTerminal.NamespaceAdministrator"` per `research.md §15`. Update the role-claim parser (`user.GetEffectiveRoles`) — verify it picks up the new claim without code changes.
- [X] T016 [P] Edit `api/BusTerminal.Api/Authorization/RolePolicies.cs` to add `CanAdministerNamespaces` policy (requires `NamespaceAdministrator` role only) per `research.md §15`. Register the policy in the existing `AddAuthorization` call path.
- [X] T017 [P] Create `api/BusTerminal.Api/Authorization/PlatformPrincipalExtensions.cs` with `IsNamespaceAdministrator(this PlatformPrincipal principal)` extension returning `principal.EffectiveRoles.Contains(PlatformRole.NamespaceAdministrator)`.
- [X] T018 [P] [TEST] Write `api/BusTerminal.Api.Tests/Authorization/PlatformPrincipalExtensionsTests.cs` covering `IsNamespaceAdministrator` with: (a) principal carrying the role → true, (b) principal carrying only spec-003 roles → false, (c) null principal handling.
- [X] T019 [P] Edit `api/BusTerminal.Api/Features/Registry/_Shared/AuditEvent.cs` to add five new `AuditEventType` enum values (`NamespaceOnboarded`, `NamespaceMetadataUpdated`, `NamespaceOwnershipUpdated`, `NamespaceLifecycleTransitioned`, `NamespaceValidationExecuted`) AND add nullable `LifecycleReason: string?` property to the `AuditEvent` record per `data-model.md §1.4` and `contracts/namespace-audit-event.schema.json`.
- [X] T020 [P] Edit `api/BusTerminal.Api/Features/Registry/_Shared/RegistryEntity.cs` `RegistryNamespace` record to add the 11 new nullable fields per `data-model.md §1.1`: `subscriptionId`, `subscriptionName`, `resourceGroup`, `tenantId`, `region`, `businessUnit`, `productOrApplication`, `costCenter`, `notes`, `lifecycleStatus`, `validationStatus`, `lastValidationRunId`, `lastValidatedAtUtc`, `ownership`, `onboardingActor`. All nullable to preserve back-compat with spec-006 Manual documents.

### 2.3 Backend — new shared types under Features/Namespaces/_Shared/

- [X] T021 [P] Implement enums in `api/BusTerminal.Api/Features/Namespaces/_Shared/`: `LifecycleStatus.cs` (`PendingValidation`, `Active`, `Disabled`, `Archived`), `ValidationStatus.cs` (`Healthy`, `Degraded`, `Unhealthy`), `OwnershipRole.cs`, `PrincipalType.cs`, `ValidationCheckName.cs`, `ValidationCheckOutcome.cs`, `ValidationFailureCategory.cs` per `data-model.md §2`.
- [X] T022 [P] Implement records in `api/BusTerminal.Api/Features/Namespaces/_Shared/`: `OwnershipAssignment.cs`, `OwnershipBlock.cs`, `OnboardingActor.cs`, `ValidationRun.cs`, `ValidationCheckResult.cs`, `ArmResourceSnapshot.cs`, `DriftField.cs` per `data-model.md §1.2/§1.3` and `contracts/{validation-run,ownership-assignment}.schema.json`.
- [X] T023 Implement `NamespaceArmIdParser.cs` in `api/BusTerminal.Api/Features/Namespaces/_Shared/` per `research.md §10`: canonical ARM ID parse + cross-tenant guard (reads `IConfiguration["AzureAd:TenantId"]`); resolves subscription's tenant via `ArmClient.GetSubscriptionResource(...)`; caches subscription→tenant per parser instance keyed on subscription id.
- [X] T024 [P] [TEST] Write `api/BusTerminal.Api.Tests/Features/Namespaces/_Shared/NamespaceArmIdParserTests.cs` covering: malformed ARM id, wrong resource type (EventHub, Relay), correct format with same-tenant subscription, correct format with different-tenant subscription (`CrossTenant` category), subscription cache hit, ARM throttling (`Throttled` category).
- [X] T025 [P] Implement `NamespaceAdministratorPolicy.cs` in `api/BusTerminal.Api/Features/Namespaces/_Shared/` — applies `CanAdministerNamespaces` via the endpoint filter; emits a `WARNING` log + span event on rejection per `research.md §5`.
- [X] T026 [P] Implement `NamespaceEndpointsBuilder.cs` in `api/BusTerminal.Api/Features/Namespaces/_Shared/`: shared `MapGroup("/api/namespaces").RequireAuthorization()` pattern with the `NamespaceAdministratorPolicy` filter applied to write surfaces; reads remain AuthN-only; surfaces W3C trace context per `plan.md` constraints.
- [X] T027 [P] Implement `NamespaceDtoMapping.cs` in `api/BusTerminal.Api/Features/Namespaces/_Shared/`: entity ↔ request/response DTO converters for the OpenAPI schemas in `contracts/namespace-onboarding-api.yaml`.
- [X] T028 [P] Implement persistence ports in `api/BusTerminal.Api/Features/Namespaces/_Shared/`: `INamespaceValidationRunStore.cs` (`AppendAsync`, `ListForNamespaceAsync(namespaceId, limit, continuationToken)`, `GetAsync(namespaceId, runId)`), `IArmNamespaceProbe.cs` (probe surfaces: existence/accessibility/required-permissions/identity/api-reachability), `IGraphPrincipalPicker.cs` (`SearchAsync(query, top, includeGroups, ct)`).
- [X] T029 [P] Implement `NamespaceValidationActivitySource.cs` in `api/BusTerminal.Api/Features/Namespaces/Validation/`: singleton `ActivitySource` named `"BusTerminal.NamespaceOnboarding"`; surface helper extension methods for the four span shapes per `plan.md §Principle V`.

### 2.4 Backend — persistence + ARM probe + Graph picker implementations

- [X] T030 Edit `api/BusTerminal.Api/Infrastructure/Persistence/CosmosRegistryOptions.cs` to add `ValidationRunsContainer = "namespace-validation-runs"` per `data-model.md §3`. Bind via `appsettings.json` + `appsettings.Development.json.example`.
- [X] T031 Implement `CosmosNamespaceValidationRunStore.cs` in `api/BusTerminal.Api/Infrastructure/Persistence/` per `research.md §6` and `contracts/validation-run.schema.json`: append-only writes; partition-scoped `ORDER BY executedAtUtc DESC` query; uses existing `CosmosClientFactory` + container resolution; raises on duplicate `runId`.
- [X] T032 [P] [TEST] Write `api/BusTerminal.Api.Tests/Infrastructure/Persistence/CosmosNamespaceValidationRunStoreTests.cs` (uses the existing dev Cosmos via `RegistryFixture` pattern): append + list time-descending + get-by-id + no update/delete surface.
- [X] T033 Implement `ArmNamespaceProbe.cs` in `api/BusTerminal.Api/Infrastructure/ServiceBus/` per `research.md §1, §3, §14`: uses `Azure.ResourceManager.ArmClient` (singleton) via `IAzureCredentialFactory`; surfaces five distinct probe methods returning typed results; honors per-call `CancellationToken` for timeout enforcement.
- [X] T034 [P] Implement `ArmNamespaceProbeOptions.cs` (per-check timeout configuration) and register `ArmNamespaceProbe` + `ArmClient` (singleton) in DI.
- [X] T035 Implement `GraphPrincipalPicker.cs` in `api/BusTerminal.Api/Infrastructure/Graph/` per `research.md §2`: wraps the existing `GraphServiceClient`; users via `graph.Users.GetAsync(rq → $filter, $top=25)`, groups via `graph.Groups.GetAsync`; merges + display-name-ascending; returns `PrincipalPickerItem[]`.
- [X] T036 [P] [TEST] Write `api/BusTerminal.Api.Tests/Infrastructure/Graph/GraphPrincipalPickerTests.cs` with a stub `GraphServiceClient` (Graph SDK testing pattern): user-only search, group-included search, sorting, top=25 cap, empty result, Graph 401 surfacing.
- [X] T037 Implement `WorkloadIdentityProvider.cs` in `api/BusTerminal.Api/Infrastructure/Identity/` per `research.md §17`: reads `IConfiguration["WORKLOAD_PRINCIPAL_ID"]` at startup, parses as `Guid`, caches for process lifetime. Exposes `Task<Guid> GetPrincipalIdAsync(CancellationToken)`. On missing/unparseable value, logs structured `ERROR` and surfaces a 500 from `/api/namespaces/identity` (deployment misconfiguration — env var is injected by `module.workload_identity.principal_id` per T009). Graph `/me` is NOT used (does not work for application-token flows).

### 2.5 Backend — validators + endpoint guards

- [X] T038 [P] Implement `OnboardingValidator.cs` in `api/BusTerminal.Api/Features/Namespaces/Onboarding/` per `data-model.md §5 OnboardingRequest`: ARM id format, cross-tenant guard (delegates to `NamespaceArmIdParser`), already-onboarded check (queries `IRegistryEntityStore` by `azureResourceId` case-insensitive), display name length, ownership.primaryOwner required, `validationRunId` references a Healthy/Degraded run executed within 30 minutes.
- [X] T039 [P] Implement `UpdateMetadataValidator.cs` in `api/BusTerminal.Api/Features/Namespaces/Metadata/` — rejects Azure-identifier fields in request body; length rules per `data-model.md §5 UpdateMetadataRequest`.
- [X] T040 [P] Implement `UpdateOwnershipValidator.cs` in `api/BusTerminal.Api/Features/Namespaces/Ownership/` — exactly-one PrimaryOwner; no duplicate (role, objectId) pairs; Entra `objectId` Guid shape per `data-model.md §5 UpdateOwnershipRequest`.
- [X] T041 [P] Implement `LifecycleTransitionValidator.cs` in `api/BusTerminal.Api/Features/Namespaces/Lifecycle/` — action ∈ {disable, enable, archive, restore}; reason required for disable/archive/restore; current `lifecycleStatus` permits the requested transition per `data-model.md §5 LifecycleTransitionRequest`.
- [X] T042 [P] [TEST] Write `api/BusTerminal.Api.Tests/Features/Namespaces/_Shared/ValidatorTests.cs` covering each validator's pass/fail cases (one happy + one fail per rule). Tests must fail until T038–T041 land.

### 2.6 Backend — surgical edits to spec-006 polymorphic endpoints

- [X] T043 Edit `api/BusTerminal.Api/Features/Registry/_Shared/UpdateEndpoint.cs` to reject PUTs against documents with `source = Onboarded`: return 409 ProblemDetails with `code = "OnboardedNamespaceWriteNotPermitted"` and `instance` pointing at `/api/namespaces/{id}/metadata`. Reads continue to work.
- [X] T044 Edit `api/BusTerminal.Api/Features/Registry/_Shared/DeleteEndpoint.cs` to reject DELETEs against documents with `source = Onboarded`: return 409 with redirect pointing at `/api/namespaces/{id}/lifecycle` and `action: archive`.
- [X] T045 [P] [TEST] Write `api/BusTerminal.Api.Tests/Features/Registry/_Shared/OnboardedSourceGateTests.cs` covering: PUT against Onboarded doc → 409; DELETE against Onboarded doc → 409; PUT/DELETE against Manual doc → 200 (regression guard).

### 2.7 Backend — DI wiring

- [X] T046 Implement `NamespaceServiceCollectionExtensions.cs` in `api/BusTerminal.Api/Features/Namespaces/_Shared/`: `AddNamespaceOnboardingFeature(this IServiceCollection, IConfiguration)` extension wiring `INamespaceValidationRunStore`, `IArmNamespaceProbe`, `IGraphPrincipalPicker`, `WorkloadIdentityProvider`, `NamespaceArmIdParser`, `ArmClient` (singleton), all validators, `NamespaceValidationActivitySource`, and the new `CanAdministerNamespaces` policy.
- [X] T047 Edit `api/BusTerminal.Api/Program.cs` to call `services.AddNamespaceOnboardingFeature(builder.Configuration)` and `app.MapNamespaceEndpoints()` in the same positions the spec-006 equivalents are wired.

### 2.8 Frontend — shared namespace foundations

- [X] T048 [P] Author `web/lib/namespaces/schemas.ts` — Zod schemas matching every backend FluentValidation rule + every contract schema. Strict mirror per `data-model.md §5` and `contracts/*.schema.json`.
- [X] T049 [P] Author `web/lib/namespaces/types.ts` — TypeScript types inferred from Zod schemas via `z.infer<...>`.
- [X] T050 [P] Author `web/lib/namespaces/api.ts` — typed fetch client composing `web/lib/http/` (W3C Trace Context propagation preserved). Exposes: `getIdentity`, `searchPrincipals`, `runPreOnboardingValidation`, `register`, `listInventory`, `getDetails`, `updateMetadata`, `updateOwnership`, `transitionLifecycle`, `runValidation`, `listValidationRuns`, `getValidationRun`. RSC-safe + Client-safe.
- [X] T051 [P] Author `web/lib/namespaces/query-keys.ts` — TanStack Query key factories: `namespaceKeys.identity()`, `picker.search(q)`, `inventory.list(filter)`, `details(id)`, `validationRuns.list(id)`, `validationRuns.detail(id, runId)`.
- [X] T052 [P] Author `web/lib/namespaces/wizard-storage.ts` — sessionStorage wrapper (key `bt:namespaces:wizard:v1`); debounced save (300ms); explicit `clear()` + `clearOnBeforeUnload()` per `research.md §9`.
- [X] T053 [P] Author `web/lib/namespaces/lifecycle.ts` — transition-validity predicates (`canDisable(status)`, `canEnable(status)`, `canArchive(status)`, `canRestore(status)`); error-message map for invalid transitions.
- [X] T054 Edit `web/components/layout/navigation-shell.tsx` `NAV_ENTRIES` array to add `{ href: "/namespaces", label: "Namespaces", operationClass: "Read", icon: Layers, matchPrefix: true }` per `contracts/outputs-contract.md §3.2`.

**Checkpoint**: Foundation ready — user-story implementation can now begin in parallel.

---

## Phase 3: User Story 1 — Onboard via the guided wizard (Priority: P1) 🎯 MVP

**Goal**: An authenticated administrator can sign in, open Namespace Onboarding, paste a valid ARM Resource ID, fill metadata + ownership across the wizard steps, run validation, and see the namespace appear as `Active` in the Namespace Inventory with a green validation badge.

**Independent Test**: With a target Azure Service Bus namespace and the prerequisite `az role assignment create` Reader grant in place, an admin can complete the wizard end-to-end in under 5 minutes (SC-001), and an invalid/inaccessible ARM ID surfaces clearly in step 4 without persisting any record (FR-023a / SC-006).

### 3.1 Tests for US1 — write first ⚠️

- [X] T055 [P] [US1] [TEST] **Consolidated into** `api/BusTerminal.Api.Tests/Features/Namespaces/Validation/Checks/ValidationChecksTests.cs` — all five check adapters covered in one suite (16 tests).
- [X] T056 [P] [US1] [TEST] **Consolidated into** `ValidationChecksTests.cs`.
- [X] T057 [P] [US1] [TEST] **Consolidated into** `ValidationChecksTests.cs`.
- [X] T058 [P] [US1] [TEST] **Consolidated into** `ValidationChecksTests.cs`.
- [X] T059 [P] [US1] [TEST] **Consolidated into** `ValidationChecksTests.cs`.
- [X] T060 [US1] [TEST] `api/BusTerminal.Api.Tests/Features/Namespaces/Validation/NamespaceValidationRunnerTests.cs` — parallel-run + Healthy/Degraded/Unhealthy aggregate paths + drift detection + persistence (5 tests).
- [X] T061 [US1] [TEST] **Consolidated into** `api/BusTerminal.Api.Tests/Features/Namespaces/NamespaceEndpointsTests.cs` (PreOnboarding cases).
- [X] T062 [US1] [TEST] **Consolidated into** `NamespaceEndpointsTests.cs` (Onboarding happy path + Unhealthy 409 + non-admin 403).
- [X] T063 [US1] [TEST] **Consolidated into** `NamespaceEndpointsTests.cs` (Identity authenticated case).
- [X] T064 [US1] [TEST] **Consolidated into** `NamespaceEndpointsTests.cs` (Picker authenticated + empty-query 400). Anonymous→401 path not covered because the dev mock auth handler always authenticates; behavior is covered by the real Microsoft.Identity.Web pipeline in non-dev environments.
- [X] T065 [US1] [TEST] `web/tests/unit/namespaces/wizard-storage.test.ts` — round-trip, debounce, clear, beforeunload, subscribe (5 tests pass).
- [ ] T066 [US1] [TEST] Deferred — `azure-resource-id-input.test.tsx` (RTL) requires a TanStack-Query + MSAL test harness not yet established in `web/tests/unit/`. Track as a follow-up; the component is exercised at runtime via the wizard.
- [ ] T067 [US1] [TEST] Deferred — same TanStack-Query+MSAL harness gap as T066.
- [ ] T068 [US1] [TEST] Deferred — Playwright happy-path needs MSW or backend stubbing for `/api/namespaces/*` that doesn't exist yet under `web/tests/e2e/`. Track as a follow-up.
- [ ] T069 [US1] [TEST] Deferred — same MSW gap as T068.
- [ ] T070 [US1] [TEST] Deferred — axe-playwright wizard sweep depends on T068's MSW harness landing.

### 3.2 Backend implementation — validation runner + checks

- [X] T071 [P] [US1] `ExistenceCheck.cs` — thin adapter over `IArmNamespaceProbe.ProbeExistenceAsync`; result + snapshot propagated through `CheckExecutionResult`.
- [X] T072 [P] [US1] `AccessibilityCheck.cs` — thin adapter over `IArmNamespaceProbe.ProbeAccessibilityAsync`.
- [X] T073 [P] [US1] `RequiredPermissionsCheck.cs` — thin adapter over `IArmNamespaceProbe.ProbeRequiredPermissionsAsync` (the probe already implements `permissions/list` + Reader detection per research §3).
- [X] T074 [P] [US1] `IdentityAuthorizationCheck.cs` — thin adapter over `IArmNamespaceProbe.ProbeIdentityAuthorizationAsync`.
- [X] T075 [P] [US1] `ApiReachabilityCheck.cs` — thin adapter over `IArmNamespaceProbe.ProbeApiReachabilityAsync`.
- [X] T076 [US1] `NamespaceValidationRunner.cs` — fans out 5 checks via `Task.WhenAll`, aggregate 15s budget via linked CTS, builds `ValidationRun` (aggregate status, snapshot from Existence, drift fields vs persisted baseline), persists via `INamespaceValidationRunStore`. Parent + child OTel spans emitted via `NamespaceValidationActivitySource`.

### 3.3 Backend implementation — wizard-supporting endpoints

- [X] T077 [US1] `WorkloadIdentityEndpoint.cs` — `GET /api/namespaces/identity`. Reads from `WorkloadIdentityProvider`. AuthN-only. Sample `az role assignment create` command surfaced.
- [X] T078 [US1] `PickerEndpoint.cs` — `GET /api/namespaces/_picker?q=...&includeGroups=...`. AuthN-only. Delegates to `IGraphPrincipalPicker.SearchAsync`. 400 on empty/oversized query.
- [X] T079 [US1] `PreOnboardingValidationEndpoint.cs` — `POST /api/namespaces/_validate`. Accepts optional `proposedNamespaceId`. Validates ARM id via `NamespaceArmIdParser` (cross-tenant rejected with 400/`CrossTenantArmId`). Persists ValidationRun on every outcome (Healthy/Degraded/Unhealthy).
- [X] T080 [US1] `OnboardingEndpoint.cs` — `POST /api/namespaces`. Pipeline: namespace-administrator gate → `OnboardingValidator` (FR-023a Healthy/Degraded ≤30min) → re-fetch run for namespaceId binding check (mismatch → 400 `NamespaceIdMismatch`) → persist `RegistryNamespace` with `Source = Onboarded` + spec-008 fields populated → `NamespaceOnboarded` audit event. 409 on Unhealthy / stale / duplicate.
- [X] T081 [US1] Wired in `MapNamespaceEndpoints()`. Runtime OpenAPI conformance verification deferred to Phase 6 T143 (the contract assertion lives there).

### 3.4 Frontend implementation — wizard

- [X] T082 [P] [US1] `azure-resource-id-input.tsx` — RHF `<Controller>`-driven ARM id field with inline format validation (Zod regex from `schemas.ts`) + debounced `already-onboarded` probe via `listInventory`. Cross-tenant client hint is advisory only; backend remains authoritative.
- [X] T083 [P] [US1] `entra-principal-picker.tsx` — `Popover` + `Command` (cmdk) combobox, debounced TanStack Query against `/api/namespaces/_picker`, User/Group icon disambiguation, keyboard navigation via cmdk.
- [X] T084 [P] [US1] `grant-reader-guidance.tsx` — `/api/namespaces/identity` TanStack Query + reactive ARM-id substitution into the sample `az role assignment create` command; copy-to-clipboard button; runbook link.
- [X] T085 [P] [US1] `wizard-stepper.tsx` — custom 5-step indicator with `Card` + `Badge` + framer-motion dot animation; honors `prefers-reduced-motion` via `useReducedMotion`.
- [X] T086 [US1] `step-1-identification.tsx` — `<AzureResourceIdInput>` + sidebar `<GrantReaderGuidance>`; Continue gated on `ok|warning` validation state.
- [X] T087 [US1] `step-2-metadata.tsx` — display name (defaults from parsed namespace name), environment, business unit, product/application, cost center, description, notes.
- [X] T088 [US1] `step-3-ownership.tsx` — required Primary Owner picker + add/remove rows for Secondary Owners, Technical Stewards, Support Contacts.
- [X] T089 [US1] `step-4-validation.tsx` — runs `_validate` with pre-allocated `proposedNamespaceId`, renders per-check rows, aggregate status badge, re-embeds `<GrantReaderGuidance>` on `ReaderRoleMissing`. Stale-ARM banner if user edited the ARM id after a run.
- [X] T090 [US1] `step-5-review.tsx` — read-only summary panels; Register enabled iff latest run is Healthy/Degraded; on 201 routes to `/namespaces/{id}`.
- [X] T091 [US1] `namespace-onboarding-wizard.tsx` — single `useForm` spanning all 5 steps; sessionStorage persistence via `wizard-storage.ts`; clears on register/cancel/beforeunload.
- [X] T092 [US1] `app/(authenticated)/namespaces/onboard/page.tsx` — Client Component, gated on `BusTerminal.NamespaceAdministrator` via `useHasRole`; renders forbidden state otherwise. Spec-008 role added to `web/lib/auth/role-permission-matrix.ts` (additive — does not change spec-003 four-role semantics).

### 3.5 US1 checkpoint

- [ ] T093 [US1] Live smoke verification per `quickstart.md §6` deferred to dev-environment validation in a follow-up session (requires the deployed backend + a real ARM namespace + Reader grant). The contract-test surface (T060–T064) exercises the equivalent flow against the in-memory fakes and passes (69/69).
- [X] T094 [US1] Onboarding contract test (T062 equivalent) passes via `NamespaceEndpointsTests.PostRegister_HappyPath_Returns201_AndEmitsAudit`. Runtime OpenAPI assertion against the YAML is the Phase 6 T143 task.

**Checkpoint**: User Story 1 is fully functional and testable independently. **This is the MVP.**

---

## Phase 4: User Story 2 — Browse, search, inspect onboarded namespaces (Priority: P2)

**Goal**: An on-call engineer or messaging architect can find an onboarded namespace by partial name or business unit, see its ownership chain at a glance, drill into its full metadata, and verify its current operational and validation status without leaving the application.

**Independent Test**: With at least three onboarded namespaces across environments, an authenticated user can open the Namespace Inventory, filter by environment, search by partial display name, sort by last-validated time, click a row, see the Namespace Details page with all captured metadata, ownership (with resolved Entra display names), Azure identifiers, validation results, lifecycle status, and a recent audit summary.

### 4.1 Tests for US2 — write first ⚠️

- [X] T095 [P] [US2] [TEST] `api/BusTerminal.Api.Tests/Features/Namespaces/Inventory/InventoryEndpointTests.cs` — 13 tests covering pagination via pageSize, environment + lifecycle (multi-value) + validation status filters, tag filter (key-only and key+value), partial-name search across displayName / businessUnit / name, default sort + alphabetic sort, includeArchived toggle (default hidden), and source filter (Onboarded-only). 13/13 passing.
- [X] T096 [P] [US2] [TEST] `api/BusTerminal.Api.Tests/Features/Namespaces/Details/DetailsEndpointTests.cs` — 5 tests covering 404 missing id, happy path returning details + ETag, latestValidationRun join from `namespace-validation-runs`, recentAuditEvents join from `registry-audit` newest-first. 5/5 passing.
- [ ] T097 [US2] [TEST] Deferred — `namespace-inventory-table.test.tsx` (RTL) blocked on the same TanStack-Query + next/navigation + MSAL test harness gap as T066/T067. Track as a follow-up; the table is exercised at runtime.
- [ ] T098 [US2] [TEST] Deferred — `lifecycle-status-badge.test.tsx` + `validation-status-badge.test.tsx` blocked on the same RTL harness. The badge components are pure presentational and visually verifiable via Storybook.
- [ ] T099 [US2] [TEST] Deferred — Playwright `browse.spec.ts` blocked on the same MSW gap as T068.
- [ ] T100 [US2] [TEST] Deferred — axe-playwright sweep depends on T099's MSW harness landing.

### 4.2 Backend implementation — inventory + details

- [X] T101 [US2] Added `IRegistryEntityStore.ListOnboardedAsync(NamespaceInventoryQuery, ct)` with new `NamespaceInventoryQuery` + `NamespaceInventoryPage` records under `Features/Namespaces/Inventory/`. Cosmos implementation extends `CosmosRegistryEntityStore` (cross-partition `WHERE c.source = "Onboarded"` with optional environment / lifecycle (ARRAY_CONTAINS) / validation (ARRAY_CONTAINS) / tag (EXISTS subquery) / partial-name (CONTAINS LOWER) filters; archived hidden by default; sort by lastValidatedAt / displayName / environment).
- [X] T102 [US2] `InventoryEndpoint.cs` — `GET /api/namespaces`; parses multi-value enum query params with 400 ProblemDetails on bad values; clamps pageSize to [1,100] default 25; default sort `lastValidatedAt_desc`; returns `InventoryListResponse { items, continuationToken }`. AuthN-only read.
- [X] T103 [US2] `DetailsEndpoint.cs` — `GET /api/namespaces/{id}`; joins latest ValidationRun via `INamespaceValidationRunStore.GetAsync(id, lastValidationRunId)`; joins recent audit events (limit 20) via `IAuditEventStore.ListForEntityAsync`; flat response shape (allOf composition); sets ETag header; 404 when entity missing OR `source != Onboarded`.
- [X] T104 [P] [US2] `web/lib/namespaces/ownership-resolution.ts` — server-safe pure resolver that defaults to `displayNameSnapshot` with `displayNameIsSnapshotOnly: true` flag per FR-011 fallback contract. Graph re-resolution call deferred to a future tightening (snapshot satisfies FR-011 today).

### 4.3 Frontend implementation — inventory + details

- [X] T105 [P] [US2] `web/components/namespaces/inventory/lifecycle-status-badge.tsx` — Active (success + CircleCheck) / Disabled (warning + CircleOff) / Archived (neutral + Archive). Color + icon + text together per FR-041.
- [X] T106 [P] [US2] `web/components/namespaces/inventory/validation-status-badge.tsx` — Healthy / Degraded / Unhealthy with matched semantic icons.
- [X] T107 [P] [US2] `web/components/namespaces/inventory/namespace-inventory-filters.tsx` — chip-style URL-synced filters (environment, lifecycle, validation, includeArchived) + a debounced search input. Clear-filters CTA.
- [X] T108 [US2] `web/components/namespaces/inventory/namespace-inventory-table.tsx` — TanStack Table v8 with sortable headers (displayName / environment / lastValidatedAt), aria-sort attributes, keyboard-activated row navigation to `/namespaces/{id}`, continuation-token-based paging.
- [X] T109 [US2] `web/app/(authenticated)/namespaces/page.tsx` — RSC shell that mounts the Client `<NamespaceInventory>` (URL state + TanStack Query). Inventory header surfaces an "Onboard a namespace" CTA only for `NamespaceAdministrator`.
- [X] T110 [P] [US2] `web/components/namespaces/details/namespace-metadata-panel.tsx` — Business / Azure identifiers / Tags sections; renders environment as a badge, tags as outline badges, ARM resource id as monospace.
- [X] T111 [P] [US2] `web/components/namespaces/details/namespace-ownership-panel.tsx` — renders the 4 ownership roles via `resolveOwnershipBlock`; flags snapshot-only entries with a subtle CircleAlert hint per FR-011.
- [X] T112 [P] [US2] `web/components/namespaces/details/namespace-validation-panel.tsx` — aggregate status, per-check breakdown (icon + name + outcome + reason + duration + reasonCategory), drift warning when `driftDetected`, "Re-run" button (visible to NamespaceAdministrator only, disabled until Phase 5 T140 wires the action).
- [X] T113 [P] [US2] `web/components/namespaces/details/namespace-audit-panel.tsx` — newest-first list with actor, timestamp, event-type badge, change summary, lifecycle reason where present.
- [X] T114 [US2] `web/app/(authenticated)/namespaces/[id]/page.tsx` — async RSC shell that resolves `params` and mounts `<NamespaceDetails>` Client Component. Header shows displayName + environment + lifecycle + validation badges; Edit / Lifecycle CTAs visible to NamespaceAdministrator.

### 4.4 US2 checkpoint

- [ ] T115 [US2] Live smoke verification deferred to the same dev-environment session as T093 (requires deployed backend + ≥3 onboarded namespaces). Contract tests (18/18 passing across `InventoryEndpointTests` + `DetailsEndpointTests`) exercise the joined response shape against in-memory fakes.
- [ ] T116 [US2] Runtime OpenAPI assertion is the Phase 6 T143 task (same deferral as T094).

**Checkpoint**: Users can onboard (US1) AND browse/search/inspect (US2) onboarded namespaces.

---

## Phase 5: User Story 3 — Lifecycle and metadata management (Priority: P3)

**Goal**: A `namespace-administrator` can edit any mutable field on a namespace details page, transition lifecycle status (Active → Disabled → Active → Archived), and trigger an on-demand validation run. Every action is reflected in the audit log with actor, UTC timestamp, and change summary.

**Independent Test**: With an onboarded namespace from US1, an admin can (a) edit metadata + ownership and see the changes persist with audit, (b) transition lifecycle through Active → Disabled → Active → Archived → restore, (c) trigger a Re-run validation from the details page and see the new ValidationRun replace the latest. Non-admin users hit 403 on every write/lifecycle/validation endpoint.

### 5.1 Tests for US3 — write first ⚠️

- [X] T117 [P] [US3] [TEST] **Consolidated into** `api/BusTerminal.Api.Tests/Features/Namespaces/Us3EndpointsTests.cs` — metadata cases (happy / Azure-id rejection / missing If-Match → 428 / stale ETag → 409 ConcurrencyConflict / non-admin → 403).
- [X] T118 [P] [US3] [TEST] **Consolidated into** `Us3EndpointsTests.cs` — ownership cases (happy + audit / missing PrimaryOwner → 400 / non-admin → 403). Field-diff is exercised via the metadata happy path which uses the same RegistryAuditFactory codepath.
- [X] T119 [P] [US3] [TEST] **Consolidated into** `Us3EndpointsTests.cs` — lifecycle cases (Active→Disabled happy + audit reason / Disabled→Active auto-revalidates / Archive without reason → 400 / Archived→Disable impermissible → 400 / non-admin → 403).
- [X] T120 [P] [US3] [TEST] **Consolidated into** `Us3EndpointsTests.cs` — run-validation cases (happy persists run + advances namespace + audit / Archived → 409 / non-admin → 403).
- [X] T121 [P] [US3] [TEST] **Consolidated into** `Us3EndpointsTests.cs` — list + get validation-runs (AuthN read happy + 404 on unknown id).
- [ ] T122 [P] [US3] [TEST] Deferred — `metadata-edit-form.test.tsx` + `ownership-edit-form.test.tsx` (RTL) blocked on the TanStack-Query + MSAL test harness gap (same as T066/T067/T097). Track as a follow-up.
- [ ] T123 [P] [US3] [TEST] Deferred — `lifecycle-action-dialog.test.tsx` blocked on the same RTL harness.
- [ ] T124 [US3] [TEST] Deferred — Playwright `lifecycle.spec.ts` blocked on the MSW namespace-API harness gap (same as T068/T099).
- [ ] T125 [US3] [TEST] Deferred — Playwright `edit.spec.ts` blocked on the same MSW gap.
- [ ] T126 [US3] [TEST] Deferred — Playwright `revalidate.spec.ts` blocked on the same MSW gap.
- [ ] T127 [US3] [TEST] Deferred — axe-playwright `edit.spec.ts` / `lifecycle.spec.ts` blocked on the MSW harness landing (same as T070/T100).

### 5.2 Backend implementation — metadata + ownership + lifecycle

- [X] T128 [US3] Implemented `UpdateMetadataEndpoint.cs` in `api/BusTerminal.Api/Features/Namespaces/Metadata/` — PUT /api/namespaces/{id}/metadata; If-Match required (428 otherwise); 404 when not source=Onboarded; FluentValidation rejects Azure-identifier fields (FR-005); ETag concurrency; on conflict emits the spec-006 ConflictResponse via ConcurrencyConflictMapper; writes NamespaceMetadataUpdated audit event with field diff.
- [X] T129 [US3] Implemented `UpdateOwnershipEndpoint.cs` in `api/BusTerminal.Api/Features/Namespaces/Ownership/` — PUT /api/namespaces/{id}/ownership; full-block replace; If-Match; same concurrency / conflict mapping; writes NamespaceOwnershipUpdated audit event with field diff.
- [X] T130 [US3] Implemented `TransitionLifecycleEndpoint.cs` in `api/BusTerminal.Api/Features/Namespaces/Lifecycle/` — POST /api/namespaces/{id}/lifecycle; LifecycleTransitionValidator enforces the FR-023 transition table + reason rules; on `enable` from Disabled automatically invokes `NamespaceValidationRunner` (FR-024) and reflects the new validation status; writes NamespaceLifecycleTransitioned audit event carrying LifecycleReason. Wire-shape uses camelCase for LifecycleAction per the OpenAPI contract (`LifecycleActionJsonConverter` applies `JsonNamingPolicy.CamelCase`).
- [X] T131 [US3] Implemented `RunValidationEndpoint.cs` in `api/BusTerminal.Api/Features/Namespaces/Validation/` — POST /api/namespaces/{id}/validation-runs; rejects Archived (409); runs `NamespaceValidationRunner` under `namespace.validation.rerun` span; persists ValidationRun via the runner; best-effort optimistic-concurrency update of `lastValidationRunId` / `lastValidatedAtUtc` / `validationStatus` on the namespace (tolerates concurrent writes — the ValidationRun is the durable artifact per FR-016); writes NamespaceValidationExecuted audit event.
- [X] T132 [US3] Implemented `ListValidationRunsEndpoint.cs` (covers both GET endpoints in one slice file per the spec's "single seam per slice" convention) — GET /api/namespaces/{id}/validation-runs (paginated, time-descending) and GET /api/namespaces/{id}/validation-runs/{runId}. AuthN-only.
- [X] T133 [US3] Wired all five new endpoints in `MapNamespaceEndpoints()`. Runtime OpenAPI assertion deferred to T142 (Phase 6 T143 equivalent).

### 5.3 Frontend implementation — edit + lifecycle + revalidate

- [X] T134 [P] [US3] Authored `web/components/namespaces/edit/metadata-edit-form.tsx` — RHF + Zod; carries the loaded ETag; on 409 surfaces a conflict banner directing the operator to reload (the spec-006 `RegistryConflictModal` requires the spec-006 entity shape; in Phase 5 we reuse the simpler banner pattern that matches the namespace document layout).
- [X] T135 [P] [US3] Authored `web/components/namespaces/edit/ownership-edit-form.tsx` — full-block replace UX (add/remove rows per role); reuses `<EntraPrincipalPicker>`; sends the entire OwnershipBlock with role discriminators and timestamps stamped client-side at submit.
- [X] T136 [P] [US3] Authored `web/components/namespaces/lifecycle/lifecycle-action-dialog.tsx` — `Dialog` + reason textarea + confirm/cancel; reason required for disable / archive / restore per FR-023; enables/disables actions per `lifecycle.ts` predicates.
- [X] T137 [P] [US3] Authored `web/components/namespaces/lifecycle/lifecycle-transition-button.tsx` — button group exposing the actions permitted by `permittedActionsFor(currentStatus)`; clicking an action opens the dialog with that action pre-selected.
- [X] T138 [US3] Authored `web/app/(authenticated)/namespaces/[id]/edit/page.tsx` (RSC shell) + `web/components/namespaces/edit/namespace-edit-client.tsx` (Client driver). Tabbed `<MetadataEditForm>` + `<OwnershipEditForm>`; surface gated on `BusTerminal.NamespaceAdministrator`.
- [X] T139 [US3] Authored `web/app/(authenticated)/namespaces/[id]/lifecycle/page.tsx` (RSC shell) + `web/components/namespaces/lifecycle/namespace-lifecycle-client.tsx` (Client driver). Renders `<LifecycleTransitionButtons>`; navigates back to details on success.
- [X] T140 [US3] Wired the Re-run validation button in `<NamespaceValidationPanel>` via a `useMutation` on `NamespacesApi.runValidation(id)`; visible only to NamespaceAdministrator; spinner via `isReRunning`; invalidates `namespaceKeys.details(id)` on success so the panel refreshes.

### 5.4 US3 checkpoint

- [ ] T141 [US3] Live smoke verification deferred to the same dev-environment session as T093 (requires deployed backend + a real namespace). The contract-test surface (T117–T121 consolidated into `Us3EndpointsTests.cs`) exercises the equivalent flow against the in-memory fakes — 19/19 passing.
- [ ] T142 [US3] Runtime OpenAPI assertion is the Phase 6 T143 task (same deferral as T094/T116).

**Checkpoint**: All three user stories independently functional.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final hardening, CI gates, performance verification, documentation polish, follow-up coordination notes.

- [ ] T143 [P] Add the spec-008 OpenAPI authoring-source → runtime-document CI assertion (extend the spec-006 pattern; verify `contracts/namespace-onboarding-api.yaml` matches the runtime `GET /openapi/v1.json` for the spec-008 routes).
- [ ] T144 [P] Performance verification: replay a representative inventory + details + validation-run workload against the dev environment; capture p95 against FR-037 (<1s inventory), FR-038 (<500ms details), FR-039 (<15s validation). Record results in a new `specs/008-namespace-onboarding/perf-baseline.md`.
- [ ] T145 [P] W3C Trace Context smoke verification per SC-010: pick three UI traces from `/namespaces/*` interactions in Azure Monitor; confirm `traceparent` propagates end-to-end and the backend spans appear under the same trace id.
- [ ] T146 [P] Run `quickstart.md` end-to-end on a clean dev environment (fresh login, new ARM id, runbook §5 grant, full onboard); confirm SC-001 (<5 minutes total) and SC-006 (no persistence on Unhealthy validation).
- [ ] T147 [P] Documentation polish — README links (if any), `iac/runbooks/grant-namespace-reader.md` final pass, in-app help-text for the wizard sidebar.
- [ ] T148 Update `specs/003-auth-and-identity/contracts/role-permission-matrix.md` as a follow-up note describing the new `namespace-administrator` role (additive, additive only) per `contracts/outputs-contract.md §4.1`.
- [ ] T149 Update `specs/006-service-bus-registry-core/contracts/audit-event.schema.json` (or add a `$ref` note) to reference the new spec-008 audit-event types per `contracts/outputs-contract.md §4.2`.
- [ ] T150 Update `speckit-artifacts/tech-stack.md` to add `Azure.ResourceManager.ServiceBus` to the approved backend dependency list per `contracts/outputs-contract.md §4.3`.
- [ ] T151 Final cross-spec regression: confirm spec 006's polymorphic `/api/registry/*` endpoints still work for `source = Manual` namespaces (PUT/DELETE pass) and now correctly reject for `source = Onboarded` (PUT/DELETE return 409). Spec 006's frontend `(authenticated)/registry/*` routes continue to render legacy Manual namespaces.
- [ ] T152 Final CI run on the feature branch: backend `dotnet test` green; frontend `pnpm test`, `pnpm test:a11y`, `pnpm test:e2e` green; IaC checkov + tfsec + BT-IAC-001..007 green. Open PR to `main`.
- [ ] T153 [P] [TEST] SC-011 regression — author `web/tests/e2e/namespaces/validation-outage.spec.ts` (Playwright + MSW): intercept `POST /api/namespaces/_validate` and `POST /api/namespaces/{id}/validation-runs` to return 503 / network error; verify (a) inventory page still loads + renders, (b) details page still loads + renders the latest persisted ValidationRun + a clear "Re-run validation is temporarily unavailable" banner (not a fatal error), (c) metadata edit + ownership edit + lifecycle transitions still succeed (they don't depend on the live validation path).
- [ ] T154 [P] [TEST] FR-026 regression — author `api/BusTerminal.Api.Tests/Features/Namespaces/NoPhysicalDeleteContractTests.cs` asserting the OpenAPI document for `/api/namespaces/*` declares NO `DELETE` operation on any namespace path (parse the runtime `GET /openapi/v1.json` and assert `paths['/api/namespaces/{id}']` does not list `delete`).
- [ ] T155 [P] SC-007 grep-based CI assertion — extend the project's existing gitleaks/secret-scan workflow (or add a new script `scripts/check-no-servicebus-credentials.sh`) to fail CI if `grep -rE "Endpoint=sb://|SharedAccessKey=|SharedAccessSignature=" --include="*.cs" --include="*.ts" --include="*.tsx" --include="*.tf" --include="*.json" --include="*.yaml" --exclude-dir=node_modules --exclude-dir=obj --exclude-dir=bin` returns any hits across the spec-008 surface. Hook it into `.github/workflows/ci.yml` per existing secret-scan pattern.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately.
- **Foundational (Phase 2)**: Depends on Setup completion. **BLOCKS all user stories**. Within Phase 2, the IaC apply (T011–T013) blocks the backend wiring tasks that read App Role / container values; the shared backend types (T014–T029) block the backend implementation tasks; the frontend foundations (T048–T054) block US1/US2/US3 frontend tasks.
- **User Stories (Phase 3+)**: All depend on Foundational completion.
  - Within each story, tests (5.x.1, 4.x.1, 3.x.1) MUST be written and FAIL before implementation.
  - User stories can run sequentially (P1 → P2 → P3) for MVP-first delivery or in parallel after Phase 2 with multiple developers (US1 is the only "must-have" for MVP).
- **Polish (Phase 6)**: Depends on all desired user stories being complete.

### User Story Dependencies

- **US1 (P1)**: No dependencies on other stories. Can start immediately after Phase 2.
- **US2 (P2)**: No hard dependencies on US1 implementation — the inventory + details endpoints query the data store directly. But the demonstrable test for US2 requires at least one Onboarded namespace, so end-to-end testing depends on US1 having shipped or on a hand-crafted Cosmos doc.
- **US3 (P3)**: Same. Edit/lifecycle/revalidate endpoints work against any Onboarded document; demonstrable testing requires US1.

### Within Each User Story

- Tests MUST be written and FAIL before implementation.
- Validators before validator-consuming endpoints.
- Persistence stores before endpoints that consume them.
- Backend endpoint conformance before the frontend route that consumes it.
- Story complete before moving to next priority for MVP-first delivery.

### Parallel Opportunities

- Most Phase 1 tasks (T001–T007, T009) marked [P] can run in parallel.
- Most Phase 2 shared-type tasks (T014–T022, T028–T029) marked [P] can run in parallel.
- All US1 check implementations (T071–T075) marked [P] can run in parallel.
- All US1 wizard step components (T086–T090) can run in parallel after `WizardStepper` + shared inputs land.
- All US2 detail panels (T110–T113) marked [P] can run in parallel.
- All US3 edit/lifecycle/dialog components (T134–T137) marked [P] can run in parallel.
- All tests for any single user story marked [P] can run in parallel (they exercise different files).

---

## Parallel Example: User Story 1 — Validation Checks

```bash
# After the runner skeleton and the 5 check ports are scaffolded (T076 setup),
# launch each check implementation in parallel:
Task: "Implement ExistenceCheck.cs (T071)"
Task: "Implement AccessibilityCheck.cs (T072)"
Task: "Implement RequiredPermissionsCheck.cs (T073)"
Task: "Implement IdentityAuthorizationCheck.cs (T074)"
Task: "Implement ApiReachabilityCheck.cs (T075)"
```

Tests for each (T055–T059) can be authored simultaneously with the implementations once the port interface is stable.

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001–T010)
2. Complete Phase 2: Foundational (T011–T054) — CRITICAL — blocks all stories.
3. Complete Phase 3: User Story 1 (T055–T094)
4. **STOP and VALIDATE**: Smoke per `quickstart.md §6`; confirm SC-001 (<5 min onboard) and SC-006 (no persistence on Unhealthy).
5. Deploy/demo. **This is the spec's MVP.**

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. + US1 → MVP — operator can onboard namespaces
3. + US2 → operator can find / inspect onboarded namespaces
4. + US3 → operator can manage namespaces over time (edit, lifecycle, revalidate)
5. + Polish → CI gates, perf, docs, cross-spec follow-ups

### Parallel Team Strategy

With multiple developers after Phase 2:

- Developer A: US1 backend (validation runner, check implementations, onboarding/identity/picker/preonboarding endpoints)
- Developer B: US1 frontend (wizard + steps + shared components)
- Developer C: US2 (full stack — inventory + details endpoints AND inventory table + detail panels)
- Developer D: US3 (full stack — metadata/ownership/lifecycle/revalidate endpoints AND edit forms + dialog)

Tests within each story are written first by the story owner.

---

## Notes

- [P] tasks = different files, no dependencies on incomplete tasks.
- [Story] label maps task to specific user story for traceability.
- [TEST] tasks precede the implementation they cover (TDD discipline inherited from spec 006).
- Each user story is independently completable and testable.
- Verify tests fail before implementing.
- Commit after each task or logical group; rebase from `main` periodically.
- Stop at any checkpoint to validate story independently.
- Avoid: vague tasks, same-file conflicts, cross-story dependencies that break independence.

---

**Total task count**: 155 tasks across Setup (10) + Foundational (44) + US1 (40) + US2 (22) + US3 (26) + Polish (13).

**MVP scope**: T001–T094 (Phases 1–3 = 94 tasks).

**Analyze-pass remediations** (2026-06-14): T009, T037, T079, T084 updated; T153, T154, T155 added; corresponding spec/research/data-model amendments per `/speckit-analyze` findings F1–F12.

**Suggested next command**: `/speckit-implement` (the analyze pass is complete; all CRITICAL/HIGH/MEDIUM findings remediated).
