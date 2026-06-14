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

- [ ] T055 [P] [US1] [TEST] Write `api/BusTerminal.Api.Tests/Features/Namespaces/Validation/Checks/ExistenceCheckTests.cs` — happy path (Pass), 404 (`ArmNamespaceNotFound`), 401/403 (`Unauthorized`), 429 (`Throttled`), timeout (`Timeout`), cross-tenant (`CrossTenant`).
- [ ] T056 [P] [US1] [TEST] Write `api/BusTerminal.Api.Tests/Features/Namespaces/Validation/Checks/AccessibilityCheckTests.cs` — same matrix as Existence.
- [ ] T057 [P] [US1] [TEST] Write `api/BusTerminal.Api.Tests/Features/Namespaces/Validation/Checks/RequiredPermissionsCheckTests.cs` — `permissions/list` returns Reader action (Pass), returns empty (`ReaderRoleMissing`), returns inherited wildcard (Pass), timeout (`Timeout`).
- [ ] T058 [P] [US1] [TEST] Write `api/BusTerminal.Api.Tests/Features/Namespaces/Validation/Checks/IdentityAuthorizationCheckTests.cs` — token exchange success (Pass), token exchange failure (`TokenExchangeFailed`).
- [ ] T059 [P] [US1] [TEST] Write `api/BusTerminal.Api.Tests/Features/Namespaces/Validation/Checks/ApiReachabilityCheckTests.cs` — 200 (Pass), 401 (Pass — auth not the concern here), 403 (Pass), DNS/TLS failure (`ServiceBusManagementUnreachable`), timeout (`Timeout`).
- [ ] T060 [US1] [TEST] Write `api/BusTerminal.Api.Tests/Features/Namespaces/Validation/NamespaceValidationRunnerTests.cs` — parallel-run + aggregate scoring + per-check timeout + span emission (uses an OTel test listener); covers Healthy / Degraded / Unhealthy aggregate paths.
- [ ] T061 [US1] [TEST] Write `api/BusTerminal.Api.Tests/Features/Namespaces/Validation/PreOnboardingValidationEndpointTests.cs` — POST `/api/namespaces/_validate` returns a persisted ValidationRun; non-admin gets 403; malformed ARM id returns 400; cross-tenant returns 400; persisted across success and failure paths.
- [ ] T062 [US1] [TEST] Write `api/BusTerminal.Api.Tests/Features/Namespaces/Onboarding/OnboardingEndpointTests.cs` — POST `/api/namespaces` happy path (validation Healthy → 201 + namespace persisted + `NamespaceOnboarded` audit event + `lastValidationRunId` set), Unhealthy validation → 409 hard-block (no persistence), stale validation run (> 30 min) → 409, duplicate ARM id → 409.
- [ ] T063 [US1] [TEST] Write `api/BusTerminal.Api.Tests/Features/Namespaces/Identity/WorkloadIdentityEndpointTests.cs` — `GET /api/namespaces/identity` returns expected shape (principalId, clientId, runbookUrl, sampleGrantCommand); AuthN-only (anonymous → 401; any authenticated user → 200).
- [ ] T064 [US1] [TEST] Write `api/BusTerminal.Api.Tests/Features/Namespaces/Ownership/PickerEndpointTests.cs` — `GET /api/namespaces/_picker?q=...` returns ≤25 results; AuthN-only; query length validation.
- [ ] T065 [US1] [TEST] Write `web/tests/unit/namespaces/wizard-storage.test.ts` (Vitest) — save/load round-trip; clear on cancel; debounced save; beforeunload clearing.
- [ ] T066 [US1] [TEST] Write `web/tests/unit/namespaces/components/azure-resource-id-input.test.tsx` (Vitest + RTL) — valid input + invalid format + cross-tenant warning + clipboard paste handling.
- [ ] T067 [US1] [TEST] Write `web/tests/unit/namespaces/components/entra-principal-picker.test.tsx` — debounced search, user/group disambiguation, selection state, keyboard navigation.
- [ ] T068 [US1] [TEST] Write `web/tests/e2e/namespaces/onboard.happy.spec.ts` (Playwright) — full happy-path onboarding flow using the spec-007 authenticated fixture + MSW mocks for `/api/namespaces/identity`, `/_validate`, `/_picker`, `POST /api/namespaces`.
- [ ] T069 [US1] [TEST] Write `web/tests/e2e/namespaces/onboard.validation-failure.spec.ts` — Unhealthy validation → Register disabled; ReaderRoleMissing remediation visible; no namespace persisted.
- [ ] T070 [US1] [TEST] Write `web/tests/a11y/namespaces/onboard.spec.ts` (axe-playwright) — zero a11y violations on each of the 5 wizard steps.

### 3.2 Backend implementation — validation runner + checks

- [ ] T071 [P] [US1] Implement `ExistenceCheck.cs` in `api/BusTerminal.Api/Features/Namespaces/Validation/Checks/` — ARM `GET` via `ArmClient`; maps 404 → `NotFound`, 401/403 → `Unauthorized`, 429 → `Throttled`, timeout → `Timeout`, success → `Pass`. Emits span attributes per `data-model.md §8`.
- [ ] T072 [P] [US1] Implement `AccessibilityCheck.cs` — same ARM call as Existence but evaluates the auth/response shape only (NOT the resource presence). Pass when ARM responds without auth failure.
- [ ] T073 [P] [US1] Implement `RequiredPermissionsCheck.cs` — ARM `permissions/list` at namespace scope per `research.md §3`; asserts `Microsoft.ServiceBus/namespaces/read` (or wildcard subsuming) is present in `actions[]`; reason category `ReaderRoleMissing` on miss.
- [ ] T074 [P] [US1] Implement `IdentityAuthorizationCheck.cs` — explicitly attempts token acquisition for ARM via `DefaultAzureCredential`; outcome reflects federation health.
- [ ] T075 [P] [US1] Implement `ApiReachabilityCheck.cs` per `research.md §14` — `GET https://{namespaceName}.servicebus.windows.net/$Resources?api-version=2017-04`; 200/401/403 = Pass; network failure = Fail.
- [ ] T076 [US1] Implement `NamespaceValidationRunner.cs` in `api/BusTerminal.Api/Features/Namespaces/Validation/` per `research.md §5`: fans out the 5 checks via `Task.WhenAll`, per-check 5s `CancellationTokenSource.CancelAfter`, aggregate budget 15s, builds the `ValidationRun` record, persists via `INamespaceValidationRunStore`, emits parent + child OTel spans, captures `armResourceSnapshot` when Existence passes.

### 3.3 Backend implementation — wizard-supporting endpoints

- [ ] T077 [US1] Implement `WorkloadIdentityEndpoint.cs` in `api/BusTerminal.Api/Features/Namespaces/Identity/` — `GET /api/namespaces/identity` per `contracts/namespace-onboarding-api.yaml`; reads from `WorkloadIdentityProvider`; AuthN-only.
- [ ] T078 [US1] Implement `PickerEndpoint.cs` in `api/BusTerminal.Api/Features/Namespaces/Ownership/` — `GET /api/namespaces/_picker?q=...` per OpenAPI; AuthN-only; delegates to `IGraphPrincipalPicker.SearchAsync`.
- [ ] T079 [US1] Implement `PreOnboardingValidationEndpoint.cs` in `api/BusTerminal.Api/Features/Namespaces/Validation/` per `research.md §18` — `POST /api/namespaces/_validate`; accepts optional `proposedNamespaceId: Guid?` field in the request body. If supplied (wizard path), the runner stamps `ValidationRun.namespaceId = proposedNamespaceId`. If absent (direct API caller path), the runner generates a fresh `Guid` for `namespaceId`. Persists the resulting ValidationRun in the `namespace-validation-runs` container; returns it. On step-5 register (T080), `OnboardingEndpoint` verifies the referenced ValidationRun's `namespaceId` equals the new namespace's `id`; mismatch → 400 with `Code = "NamespaceIdMismatch"`.
- [ ] T080 [US1] Implement `OnboardingEndpoint.cs` in `api/BusTerminal.Api/Features/Namespaces/Onboarding/` — `POST /api/namespaces` per OpenAPI; FluentValidation via `OnboardingValidator`; persists `OnboardedNamespace` document with `source = Onboarded`, `lifecycleStatus = Active`, `validationStatus` mirroring the run, `lastValidationRunId` + `lastValidatedAtUtc` set; writes `NamespaceOnboarded` audit event with `actor` = current principal; returns 201 + `OnboardedNamespace`; hard-blocks per FR-023a when run is Unhealthy or older than 30 min.
- [ ] T081 [US1] Wire the new endpoints in the `MapNamespaceEndpoints()` extension; verify the runtime OpenAPI document conforms to `contracts/namespace-onboarding-api.yaml` for the routes implemented so far.

### 3.4 Frontend implementation — wizard

- [ ] T082 [P] [US1] Author `web/components/namespaces/shared/azure-resource-id-input.tsx` — ARM id parser + inline validation (format, cross-tenant via MSAL `tid` claim, already-onboarded TanStack Query check); RHF integration.
- [ ] T083 [P] [US1] Author `web/components/namespaces/shared/entra-principal-picker.tsx` — composes `Popover` + `Command` (cmdk) per `research.md §13`; debounced search via TanStack Query; user/group visual disambiguation; keyboard navigation.
- [ ] T084 [P] [US1] Author `web/components/namespaces/wizard/grant-reader-guidance.tsx` — sidebar block rendered in step 1; queries `/api/namespaces/identity`; displays copy-pasteable `az role assignment create` command. **Empty-ARM-id state** (first render, before the user has typed anything): show the principalId resolved + a `{azureResourceId}` placeholder in the command template, with a hint "paste an ARM id above to populate the scope." **Populated state**: substitute the live ARM id reactively as the user types/pastes (debounced 200ms). Copy-to-clipboard button + accessibility label.
- [ ] T085 [P] [US1] Author `web/components/namespaces/wizard/wizard-stepper.tsx` — custom step indicator composing `Card` + `Badge` + framer-motion step dots; honors `prefers-reduced-motion`; 5 steps.
- [ ] T086 [US1] Author `web/components/namespaces/wizard/step-1-identification.tsx` — uses `<AzureResourceIdInput>` + `<GrantReaderGuidance>`; advances only on valid ARM id.
- [ ] T087 [US1] Author `web/components/namespaces/wizard/step-2-metadata.tsx` — display name (defaults from namespace name), description, environment, business unit, product/application, cost center, tags, notes.
- [ ] T088 [US1] Author `web/components/namespaces/wizard/step-3-ownership.tsx` — `<EntraPrincipalPicker>` × 4 roles; required PrimaryOwner; add/remove additional rows.
- [ ] T089 [US1] Author `web/components/namespaces/wizard/step-4-validation.tsx` — Run-validation button triggers `runPreOnboardingValidation`; per-check progress UI (spinner → result icon + reason); aggregate status badge; remediation hints (especially `ReaderRoleMissing` → embed the §step-1 guidance again).
- [ ] T090 [US1] Author `web/components/namespaces/wizard/step-5-review.tsx` — read-only summary; Register button enabled iff validation aggregate is Healthy or Degraded; on submit calls `register` and on 201 routes to the new namespace's details page.
- [ ] T091 [US1] Author `web/components/namespaces/wizard/namespace-onboarding-wizard.tsx` — root Client Component; single RHF `useForm<WizardValues>` spanning all steps; sessionStorage persistence via `wizard-storage.ts`; back-navigation preserves state; re-runs validation only on validation-relevant field change per FR-003; clears state on register/cancel/beforeunload.
- [ ] T092 [US1] Author `web/app/(authenticated)/namespaces/onboard/page.tsx` — Client Component route; renders `<NamespaceOnboardingWizard>`; requires `NamespaceAdministrator` role (renders forbidden state otherwise — read role from `/whoami`).

### 3.5 US1 checkpoint

- [ ] T093 [US1] Smoke verification per `quickstart.md §6`: log in as a `NamespaceAdministrator`, open `/namespaces/onboard`, paste a valid ARM id with Reader granted, run validation (all green), register, and confirm the namespace appears with status `Active` and validation badge `Healthy`. Reload and confirm persistence.
- [ ] T094 [US1] Confirm the contract test (T062) passes and that the runtime OpenAPI document for the routes touched in this phase conforms to `contracts/namespace-onboarding-api.yaml` (use the existing spec-006 OpenAPI-conformance CI assertion pattern).

**Checkpoint**: User Story 1 is fully functional and testable independently. **This is the MVP.**

---

## Phase 4: User Story 2 — Browse, search, inspect onboarded namespaces (Priority: P2)

**Goal**: An on-call engineer or messaging architect can find an onboarded namespace by partial name or business unit, see its ownership chain at a glance, drill into its full metadata, and verify its current operational and validation status without leaving the application.

**Independent Test**: With at least three onboarded namespaces across environments, an authenticated user can open the Namespace Inventory, filter by environment, search by partial display name, sort by last-validated time, click a row, see the Namespace Details page with all captured metadata, ownership (with resolved Entra display names), Azure identifiers, validation results, lifecycle status, and a recent audit summary.

### 4.1 Tests for US2 — write first ⚠️

- [ ] T095 [P] [US2] [TEST] Write `api/BusTerminal.Api.Tests/Features/Namespaces/Inventory/InventoryEndpointTests.cs` — pagination + continuation token, environment filter, lifecycle/validation status filter (multi-value), tag filter (key-only / value-only / key+value), partial-name search across displayName + businessUnit, sort by every supported column, includeArchived toggle, defaults (page size 25, sort by lastValidatedAt_desc).
- [ ] T096 [P] [US2] [TEST] Write `api/BusTerminal.Api.Tests/Features/Namespaces/Details/DetailsEndpointTests.cs` — response shape includes `latestValidationRun` (joined from `namespace-validation-runs`), `recentAuditEvents` (joined from `registry-audit`), resolved ownership display names via Graph (with snapshot fallback when unresolvable per FR-011); ETag header set; 404 on missing id; AuthN-only read.
- [ ] T097 [P] [US2] [TEST] Write `web/tests/unit/namespaces/components/namespace-inventory-table.test.tsx` (Vitest + RTL) — table renders columns; sortable headers; row click navigation; archived-toggle behavior.
- [ ] T098 [P] [US2] [TEST] Write `web/tests/unit/namespaces/components/lifecycle-status-badge.test.tsx` + `validation-status-badge.test.tsx` — color + icon + text presence (never color-alone per FR-041).
- [ ] T099 [US2] [TEST] Write `web/tests/e2e/namespaces/browse.spec.ts` (Playwright) — login → inventory → search → filter → click row → details page renders all panels (metadata, ownership, validation, audit).
- [ ] T100 [US2] [TEST] Write `web/tests/a11y/namespaces/inventory.spec.ts` and `details.spec.ts` (axe-playwright) — zero violations on inventory + details.

### 4.2 Backend implementation — inventory + details

- [ ] T101 [US2] Extend `IRegistryEntityStore` (or add a new `INamespaceQueryStore`) with `ListOnboardedAsync(filter, sort, paging)` and a partial-name search helper using Cosmos `CONTAINS()` against `displayName` + `businessUnit`. Implementation lives in `api/BusTerminal.Api/Infrastructure/Persistence/CosmosRegistryEntityStore.cs` (extension) per `research.md §12`.
- [ ] T102 [US2] Implement `InventoryEndpoint.cs` in `api/BusTerminal.Api/Features/Namespaces/Inventory/` — `GET /api/namespaces`; binds query params per OpenAPI; uses the store extension from T101; archived hidden by default (FR-019); returns `InventoryListResponse` with continuation token.
- [ ] T103 [US2] Implement `DetailsEndpoint.cs` in `api/BusTerminal.Api/Features/Namespaces/Details/` — `GET /api/namespaces/{id}` per OpenAPI; resolves ownership display names via Graph at render time with the snapshot fallback; joins latest ValidationRun via `INamespaceValidationRunStore.GetAsync(namespaceId, lastValidationRunId)`; joins recent audit events via `IAuditEventStore.ListForEntityAsync(id, limit=20)`; sets ETag header.
- [ ] T104 [P] [US2] Implement `web/lib/namespaces/ownership-resolution.ts` (server-side helper) — calls Graph to re-resolve display names; per FR-011 falls back to `displayNameSnapshot` with a UI hint flag when unresolvable.

### 4.3 Frontend implementation — inventory + details

- [ ] T105 [P] [US2] Author `web/components/namespaces/inventory/lifecycle-status-badge.tsx` — color + icon + text (Active / Disabled / Archived); reused by detail panel + inventory.
- [ ] T106 [P] [US2] Author `web/components/namespaces/inventory/validation-status-badge.tsx` — Healthy / Degraded / Unhealthy; same convention.
- [ ] T107 [P] [US2] Author `web/components/namespaces/inventory/namespace-inventory-filters.tsx` — chip-list filter UI driven by URL search params (environment, lifecycle, validation, tag, includeArchived). Shareable links via URL state.
- [ ] T108 [US2] Author `web/components/namespaces/inventory/namespace-inventory-table.tsx` — TanStack Table v8 with sortable columns; row click → details; pagination via continuation token.
- [ ] T109 [US2] Author `web/app/(authenticated)/namespaces/page.tsx` — Server Component for initial render (calls `listInventory` with default params), composes `<NamespaceInventoryFilters>` + `<NamespaceInventoryTable>`; nav-link "Onboard a namespace" visible to NamespaceAdministrator role.
- [ ] T110 [P] [US2] Author `web/components/namespaces/details/namespace-metadata-panel.tsx` — renders business metadata, Azure identifiers, environment badge, tags.
- [ ] T111 [P] [US2] Author `web/components/namespaces/details/namespace-ownership-panel.tsx` — renders the 4 ownership roles with Entra display names (resolved or snapshot); flags unresolvable principals per FR-011 edge case.
- [ ] T112 [P] [US2] Author `web/components/namespaces/details/namespace-validation-panel.tsx` — renders the latest ValidationRun: aggregate status, per-check breakdown (icon + name + outcome + reason + duration), drift warning if `driftDetected`, "Re-run validation" button (visible only to NamespaceAdministrator; wired in Phase 5).
- [ ] T113 [P] [US2] Author `web/components/namespaces/details/namespace-audit-panel.tsx` — renders recent audit events with actor, timestamp, event type, change summary, lifecycle reason where present.
- [ ] T114 [US2] Author `web/app/(authenticated)/namespaces/[id]/page.tsx` — Server Component; calls `getDetails(id)`; composes the four detail panels + a header with display name + environment badge + lifecycle badge.

### 4.4 US2 checkpoint

- [ ] T115 [US2] Smoke verification: with US1 having onboarded ≥3 namespaces, navigate `/namespaces`, sort/filter/search, click into one, verify all four detail panels render. Confirm "Re-run validation" button is *visible* but tied into Phase 5's endpoint (no-op stub OK at this checkpoint).
- [ ] T116 [US2] Confirm Inventory + Details endpoints conform to `contracts/namespace-onboarding-api.yaml` via the CI OpenAPI assertion.

**Checkpoint**: Users can onboard (US1) AND browse/search/inspect (US2) onboarded namespaces.

---

## Phase 5: User Story 3 — Lifecycle and metadata management (Priority: P3)

**Goal**: A `namespace-administrator` can edit any mutable field on a namespace details page, transition lifecycle status (Active → Disabled → Active → Archived), and trigger an on-demand validation run. Every action is reflected in the audit log with actor, UTC timestamp, and change summary.

**Independent Test**: With an onboarded namespace from US1, an admin can (a) edit metadata + ownership and see the changes persist with audit, (b) transition lifecycle through Active → Disabled → Active → Archived → restore, (c) trigger a Re-run validation from the details page and see the new ValidationRun replace the latest. Non-admin users hit 403 on every write/lifecycle/validation endpoint.

### 5.1 Tests for US3 — write first ⚠️

- [ ] T117 [P] [US3] [TEST] Write `api/BusTerminal.Api.Tests/Features/Namespaces/Metadata/UpdateMetadataEndpointTests.cs` — happy path (200 + audit event + ETag advance), Azure-identifier in body → 400, missing If-Match → 412, stale ETag → 409 (conflict response shape per spec-006), non-admin → 403.
- [ ] T118 [P] [US3] [TEST] Write `api/BusTerminal.Api.Tests/Features/Namespaces/Ownership/UpdateOwnershipEndpointTests.cs` — happy path with full block replace + field-changes diff in audit event; missing PrimaryOwner → 400; duplicate (role, objectId) → 400; non-admin → 403.
- [ ] T119 [P] [US3] [TEST] Write `api/BusTerminal.Api.Tests/Features/Namespaces/Lifecycle/TransitionLifecycleEndpointTests.cs` — every permitted transition path (Active↔Disabled, Active/Disabled→Archived, Archived→Disabled) succeeds; impermissible transitions → 400 with reason; reason required for Disable/Archive/Restore; Active→Active no-op rejected; non-admin → 403; Disabled→Active auto-triggers validation run.
- [ ] T120 [P] [US3] [TEST] Write `api/BusTerminal.Api.Tests/Features/Namespaces/Validation/RunValidationEndpointTests.cs` — happy path persists a new ValidationRun + advances `lastValidationRunId` + `validationStatus` on the namespace; Archived namespace → 409; non-admin → 403; drift detection populates `driftDetected` + `driftFields[]`.
- [ ] T121 [P] [US3] [TEST] Write `api/BusTerminal.Api.Tests/Features/Namespaces/Validation/ListValidationRunsEndpointTests.cs` and `GetValidationRunEndpointTests.cs` — time-descending list + paging; AuthN-only read.
- [ ] T122 [P] [US3] [TEST] Write `web/tests/unit/namespaces/components/metadata-edit-form.test.tsx` and `ownership-edit-form.test.tsx` — happy path submit, conflict-modal trigger on 409, field-level validation.
- [ ] T123 [P] [US3] [TEST] Write `web/tests/unit/namespaces/components/lifecycle-action-dialog.test.tsx` — action selection, reason required for disable/archive/restore, confirm vs cancel paths.
- [ ] T124 [US3] [TEST] Write `web/tests/e2e/namespaces/lifecycle.spec.ts` (Playwright) — full lifecycle journey: Active → Disabled (with reason) → Active (auto-revalidation) → Archived → Restore to Disabled.
- [ ] T125 [US3] [TEST] Write `web/tests/e2e/namespaces/edit.spec.ts` — metadata edit + ownership edit happy paths + concurrent-edit conflict modal (reusing spec-006's `ConflictModal`).
- [ ] T126 [US3] [TEST] Write `web/tests/e2e/namespaces/revalidate.spec.ts` — Re-run validation from details page; drift detection surface.
- [ ] T127 [US3] [TEST] Write `web/tests/a11y/namespaces/edit.spec.ts` and `lifecycle.spec.ts` — zero violations.

### 5.2 Backend implementation — metadata + ownership + lifecycle

- [ ] T128 [US3] Implement `UpdateMetadataEndpoint.cs` in `api/BusTerminal.Api/Features/Namespaces/Metadata/` — `PUT /api/namespaces/{id}/metadata`; FluentValidation via `UpdateMetadataValidator`; ETag-based concurrency via Cosmos `If-Match`; on 412 maps to the spec-006 conflict response (reuses `ConcurrencyConflictMapper`); writes `NamespaceMetadataUpdated` audit event with field-level `fieldChanges[]`.
- [ ] T129 [US3] Implement `UpdateOwnershipEndpoint.cs` in `api/BusTerminal.Api/Features/Namespaces/Ownership/` — `PUT /api/namespaces/{id}/ownership`; full-block replace; same concurrency pattern; writes `NamespaceOwnershipUpdated` audit event with per-role diffs.
- [ ] T130 [US3] Implement `TransitionLifecycleEndpoint.cs` in `api/BusTerminal.Api/Features/Namespaces/Lifecycle/` — `POST /api/namespaces/{id}/lifecycle`; FluentValidation via `LifecycleTransitionValidator`; performs transition + audit write as a Cosmos transactional batch where possible (same PK), best-effort with warning log otherwise; on `enable` from `Disabled`, automatically invokes `NamespaceValidationRunner` and reflects the new validation status (per FR-024).
- [ ] T131 [US3] Implement `RunValidationEndpoint.cs` in `api/BusTerminal.Api/Features/Namespaces/Validation/` — `POST /api/namespaces/{id}/validation-runs`; rejects Archived namespaces (409); runs `NamespaceValidationRunner`; persists ValidationRun; updates namespace's `lastValidationRunId`, `lastValidatedAtUtc`, `validationStatus` via ETag-based optimistic concurrency; writes `NamespaceValidationExecuted` audit event; surfaces `driftDetected` in the response.
- [ ] T132 [US3] Implement `ListValidationRunsEndpoint.cs` and `GetValidationRunEndpoint.cs` in the same slice — `GET /api/namespaces/{id}/validation-runs` (paginated, time-descending) and `GET /api/namespaces/{id}/validation-runs/{runId}`.
- [ ] T133 [US3] Wire the new endpoints in `MapNamespaceEndpoints()`; verify OpenAPI runtime conformance.

### 5.3 Frontend implementation — edit + lifecycle + revalidate

- [ ] T134 [P] [US3] Author `web/components/namespaces/edit/metadata-edit-form.tsx` — RHF + Zod; uses `entity-form-shell` pattern from spec 006 (reuse `RegistryConflictModal` for 409 handling).
- [ ] T135 [P] [US3] Author `web/components/namespaces/edit/ownership-edit-form.tsx` — RHF + Zod; full-block replace UX (add/remove rows per role); reuses `<EntraPrincipalPicker>`.
- [ ] T136 [P] [US3] Author `web/components/namespaces/lifecycle/lifecycle-action-dialog.tsx` — confirm-dialog rendering action + reason field (per `data-model.md §5 LifecycleTransitionRequest`); enables/disables actions per `lifecycle.ts` predicates.
- [ ] T137 [P] [US3] Author `web/components/namespaces/lifecycle/lifecycle-transition-button.tsx` — button group exposing valid actions based on current `lifecycleStatus`.
- [ ] T138 [US3] Author `web/app/(authenticated)/namespaces/[id]/edit/page.tsx` — Client Component; tabbed `<MetadataEditForm>` + `<OwnershipEditForm>`; visibility gated by NamespaceAdministrator role.
- [ ] T139 [US3] Author `web/app/(authenticated)/namespaces/[id]/lifecycle/page.tsx` — Client Component; renders `<LifecycleActionDialog>`; navigates back to details on success.
- [ ] T140 [US3] Wire the "Re-run validation" button in `<NamespaceValidationPanel>` (from US2 T112) to call `runValidation(id)`; visible only to NamespaceAdministrator; disabled while a run is in flight; refreshes the panel on completion.

### 5.4 US3 checkpoint

- [ ] T141 [US3] Smoke verification per `quickstart.md §6` step 7: edit metadata + ownership, transition lifecycle through Active → Disabled → Active → Archived → Restore, re-run validation from the details page. Confirm audit events appear in the audit panel for each action with actor + timestamp + change summary + reason (where supplied).
- [ ] T142 [US3] Confirm every spec-008 endpoint's runtime OpenAPI document conforms to `contracts/namespace-onboarding-api.yaml`.

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
