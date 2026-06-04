---
description: "Task list for spec 006 — Service Bus Registry Core"
---

# Tasks: Service Bus Registry Core

**Input**: Design documents from `/specs/006-service-bus-registry-core/`

**Prerequisites**: [`plan.md`](./plan.md), [`spec.md`](./spec.md), [`research.md`](./research.md), [`data-model.md`](./data-model.md), [`contracts/`](./contracts/), [`quickstart.md`](./quickstart.md).

**Tests**: Included. Spec 006 mandates unit, integration, contract, UI component, and E2E coverage (see `plan.md §Testing` and `research.md §20`). Test tasks are written before the corresponding implementation tasks per the spec's TDD discipline.

**Organization**: Tasks are grouped by user story (US1, US2, US3) so each story can be implemented, tested, and demoed independently. Setup and Foundational phases precede stories. Polish & cross-cutting concerns come last.

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Different files / no dependencies on incomplete tasks ⇒ safe to run in parallel.
- **[Story]**: Maps task to its user story for traceability.
- Every task names exact file paths (absolute from repo root).

## Path Conventions (this feature)

- Backend API: `api/BusTerminal.Api/`
- Backend indexer (NEW project): `api/BusTerminal.Indexer/`
- Backend tests: `api/BusTerminal.Api.Tests/`, `api/BusTerminal.Indexer.Tests/` (NEW)
- Frontend: `web/`
- IaC: `iac/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project scaffolding, new dependencies, new IaC modules, new directories — every change that does not touch business logic but is required before foundational work begins.

- [X] T001 [P] Add `BusTerminal.Indexer` project skeleton at `api/BusTerminal.Indexer/BusTerminal.Indexer.csproj` (.NET 10 isolated worker, Functions worker SDK 2.x, package refs per `plan.md §Primary Dependencies`) and register it in `api/BusTerminal.slnx`.
- [X] T002 [P] Add `BusTerminal.Indexer.Tests` project skeleton at `api/BusTerminal.Indexer.Tests/BusTerminal.Indexer.Tests.csproj` (xUnit + `Microsoft.Azure.Functions.Worker.Testing`); register in `api/BusTerminal.slnx`.
- [X] T003 Add FluentValidation package reference to `api/BusTerminal.Api/BusTerminal.Api.csproj` (`<PackageReference Include="FluentValidation" Version="11.10.0" />`) per `research.md §1`.
- [X] T004 Add Azure.Search.Documents package reference to `api/BusTerminal.Api/BusTerminal.Api.csproj` and `api/BusTerminal.Indexer/BusTerminal.Indexer.csproj` (`<PackageReference Include="Azure.Search.Documents" Version="11.6.0" />`).
- [X] T005 [P] Add `@tanstack/react-query@^5` to `web/package.json` dependencies (per `research.md §6`); run `pnpm install` and verify lockfile updates cleanly.
- [X] T006 [P] Create the empty registry feature directory tree under `api/BusTerminal.Api/Features/Registry/`: `_Shared/`, `Namespaces/`, `Queues/`, `Topics/`, `Subscriptions/`, `Rules/`, `Search/`, `Audit/` (placeholder `.gitkeep` files allowed).
- [X] T007 [P] Create the empty registry frontend tree under `web/app/(authenticated)/registry/` (`layout.tsx` stub, `page.tsx` stub, `search/`, `new/[entityType]/`, `[entityType]/[id]/`, `[entityType]/[id]/edit/`) and `web/components/registry/{forms/,forms/shared/}` and `web/lib/registry/`.
- [X] T008 [P] Create new IaC module skeleton `iac/modules/cosmos-registry-store/` (`main.tf`, `variables.tf`, `outputs.tf`, `versions.tf`, `README.md` with `terraform-docs` markers) — empty bodies, will be filled in foundational.
- [X] T009 [P] Create new IaC module skeleton `iac/modules/ai-search-index/` with the same file set; pin `Azure/azapi ~> 2.0` in `versions.tf`.
- [X] T010 [P] Create new IaC module skeleton `iac/modules/functions-container-app/` with the same file set.
- [X] T011 [P] Register the new modules in `iac/environments/dev/versions.tf` — verified `azapi ~> 2.4` already pinned at dev env root (spec 005 baseline); satisfies the new `ai-search-index` module's `~> 2.0` constraint. No env-level change required; module-level pins added. (add `azapi` provider pin) and prepare empty composition stubs in `iac/environments/dev/main.tf` (commented placeholder blocks pointing at the new modules).
- [X] T012 [P] Build verification: `dotnet build BusTerminal.slnx` → 0 errors / 0 warnings across all 5 projects (Api, Api.Tests, Indexer, Indexer.Tests, LoadFixtures); `pnpm typecheck` → clean; `tofu init -backend=false && tofu validate` → success on all 3 new IaC modules.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Persistence layer, indexer pipeline, shared backend types, shared frontend types, IaC apply for new infra. Every user story depends on these.

**⚠️ CRITICAL**: No user-story work can begin until this phase is complete.

### 2.1 IaC — provision the registry data plane

- [X] T013 Implement `iac/modules/cosmos-registry-store/main.tf` to provision three SQL containers on the existing canonical database: `registry-entities` (PK `/environment`, autoscale 400→4000), `registry-audit` (PK `/entityId`, autoscale 100→1000), `registry-entities-leases` (PK `/id`, autoscale 100→400). Include `lifecycle { prevent_destroy = true }` per BT-IAC-007 and the composite index `(parentId ASC, entityType ASC, name ASC)` per `data-model.md §4.3`. Exclude `/metadata/*` from indexing.
- [X] T014 Implement `iac/modules/ai-search-index/main.tf` to provision the index via `azapi_resource` (type `Microsoft.Search/searchServices/indexes@2024-07-01`); read the index body from `contracts/search-index.json` via `jsondecode(file(...))`. Outputs: `index_name`, `index_id`.
- [X] T015 Implement `iac/modules/functions-container-app/main.tf` to provision a Container App with `kind = "functionapp"` (v2 native Functions-for-CAE per `research.md §4`); bind workload UAMI; inject Cosmos + AI Search env vars (no secrets) and KV-secret-reference for App Insights connection string (mirroring `iac/modules/container-app`).
- [X] T016 Wire all three modules into `iac/environments/dev/main.tf`; bind workload UAMI to `Search Index Data Reader` and `Search Index Data Contributor` on the AI Search service; attach the `iac/modules/diagnostic-settings` wrapper to the indexer container app per `contracts/outputs-contract.md`.
- [X] T017 Add new outputs to `iac/environments/dev/outputs.tf` per `contracts/outputs-contract.md §New outputs`.
- [X] T018 [P] Author module READMEs (`iac/modules/{cosmos-registry-store,ai-search-index,functions-container-app}/README.md`) with `terraform-docs` injection markers; run `terraform-docs -c iac/.terraform-docs.yml iac` and verify drift-free.
- [ ] T019 Run `tofu fmt -recursive`, `tofu validate`, `tofu plan` in `iac/environments/dev/`; verify checkov + tfsec + BT-IAC-001..007 gates are green with zero allowlist additions. Apply to dev. **Status (2026-06-02): `tofu fmt` and `tofu validate` succeeded locally; full `tofu plan` requires the pipeline-supplied tfvars (subscription_id, entra_*, image refs, github_org_repo, unique_suffix) so it must run under CI. Apply pending operator authorization.**

### 2.2 Backend — shared registry types and contracts

- [X] T020 [P] Implement `IRegistryEntity` interface + `RegistryEntityType`/`RegistryEntityStatus`/`RegistrySource` enums in `api/BusTerminal.Api/Features/Registry/_Shared/` matching the closed enums in `data-model.md §2`.
- [X] T021 [P] Implement `RegistryEntity` record (canonical fields) and concrete records `RegistryNamespace`, `RegistryQueue`, `RegistryTopic`, `RegistrySubscription`, `RegistryRule` in `api/BusTerminal.Api/Features/Registry/_Shared/RegistryEntity.cs` per `data-model.md §1`.
- [X] T022 [P] Implement `RegistryTag` (key/value record) in `api/BusTerminal.Api/Features/Registry/_Shared/RegistryTag.cs`; include the `TagKeyLower` projection helper used by the persistence layer.
- [X] T023 [P] Implement `ConflictResponse` DTO in `api/BusTerminal.Api/Features/Registry/_Shared/ConflictResponse.cs` conforming exactly to `contracts/conflict-response.schema.json`.
- [X] T024 [P] Implement `HasChildrenResponse` DTO in `api/BusTerminal.Api/Features/Registry/_Shared/HasChildrenResponse.cs` conforming to `contracts/registry-api.yaml#components.schemas.HasChildrenResponse`.
- [X] T025 [P] Implement `AuditEvent` record in `api/BusTerminal.Api/Features/Registry/_Shared/AuditEvent.cs` per `contracts/audit-event.schema.json`.
- [X] T026 [P] Implement shared FluentValidation rules in `api/BusTerminal.Api/Features/Registry/_Shared/RegistryEntityValidationRules.cs`: `RequiredFieldsRule`, `EntityTypeImmutableRule`, `IdImmutableRule`, `TimestampImmutableRule` (FR-005 — `createdAtUtc` immutable on update; `updatedAtUtc` server-stamped; client-supplied values for either field MUST be ignored), `NameFormatRule` (base regex per `data-model.md §3.1`), `StatusValueRule`, `SourceValueRule`, `AzureResourceIdFormatRule`, `TagShapeRule`, `MetadataSizeRule`, `TagDisplayNormalizationRule`.

### 2.3 Backend — persistence layer

- [X] T027 [P] [TEST] Write `api/BusTerminal.Api.Tests/Features/Registry/_Shared/RegistryEntityValidationRulesTests.cs` covering every rule in T026 (each rule has at least one pass + one fail case); should fail until T026 is wired.
- [X] T028 Implement `IRegistryEntityStore` port in `api/BusTerminal.Api/Features/Registry/_Shared/IRegistryEntityStore.cs` with methods `GetAsync`, `ListAsync` (env-scoped, paginated, filterable), `CreateAsync`, `UpdateAsync` (with `IfMatch`), `DeleteAsync` (tombstone-then-delete), `CountChildrenAsync`.
- [X] T029 Implement `CosmosRegistryEntityStore` in `api/BusTerminal.Api/Infrastructure/Persistence/CosmosRegistryEntityStore.cs`: reuses spec-004 `CosmosClientFactory` + `CosmosOptions`; PK `/environment`; ETag-based concurrency; raises `ConcurrencyConflictException` on 412; tombstone-then-delete per `research.md §10` and `contracts/indexer-events.md §2`; `CountChildrenAsync` issues partition-scoped count query per `research.md §11`.
- [X] T030 [P] Implement `IAuditEventStore` port at `api/BusTerminal.Api/Features/Registry/_Shared/IAuditEventStore.cs` (Write, ListForEntity) and `CosmosAuditEventStore` at `api/BusTerminal.Api/Infrastructure/Persistence/CosmosAuditEventStore.cs`; PK `/entityId`; append-only; entity-scoped `SELECT TOP @limit ... ORDER BY timestamp DESC` query.
- [X] T031 Add `CosmosRegistryOptions` to `api/BusTerminal.Api/Infrastructure/Persistence/CosmosRegistryOptions.cs` (container names, RU bands as documentation, partition key paths) and bind via `appsettings.json` + `appsettings.Development.json.example`.
- [X] T032 Extend `api/BusTerminal.Api/Program.cs` with `services.AddRegistryFeature(builder.Configuration)` extension method that wires `IRegistryEntityStore`, `IAuditEventStore`, FluentValidators, `ConcurrencyConflictMapper`, `ChildCountChecker`, `ISearchClient`, and `IRegistryDtoMapper`. The method lives in `api/BusTerminal.Api/Features/Registry/_Shared/RegistryServiceCollectionExtensions.cs`.
- [X] T033 [TEST] Write `api/BusTerminal.Api.Tests/Features/Registry/_Shared/RegistryFixture.cs` — Cosmos integration fixture that uses the dev account, prefixes test entity ids with a per-test GUID so parallel runs are isolated, and tears down via point-deletes after each test class.
- [X] T034 [TEST] Write `api/BusTerminal.Api.Tests/Infrastructure/Persistence/CosmosRegistryEntityStoreTests.cs` covering: create-read-update-delete happy path, ETag concurrency (stale write → 412), tombstone-then-delete behavior, child-count query, name-uniqueness query.
- [X] T035 [TEST] Write `api/BusTerminal.Api.Tests/Infrastructure/Persistence/CosmosAuditEventStoreTests.cs` covering: append, entity-scoped list with limit, ordering by timestamp desc, no edit/delete surface.

### 2.4 Backend — search adapter

- [X] T036 Implement `ISearchClient` port at `api/BusTerminal.Api/Features/Registry/_Shared/ISearchClient.cs` (Search, Suggest) and `AzureAiSearchClient` at `api/BusTerminal.Api/Infrastructure/Search/AzureAiSearchClient.cs` using `Azure.Search.Documents.SearchClient` + `DefaultAzureCredential` (via existing `IAzureCredentialFactory`). Read index name from `AiSearchOptions`.
- [X] T037 Add `AiSearchOptions` at `api/BusTerminal.Api/Infrastructure/Search/AiSearchOptions.cs` (Endpoint, IndexName); bind via `Program.cs` + `appsettings.*`.

### 2.5 Backend — concurrency, child-count, endpoint builder

- [X] T038 Implement `ConcurrencyConflictMapper` in `api/BusTerminal.Api/Features/Registry/_Shared/ConcurrencyConflictMapper.cs`: maps Cosmos 412 + the current entity + the submitted entity into a `ConflictResponse` per `contracts/conflict-response.schema.json`; computes the `changedFields` array by JSON-shape diff.
- [X] T039 Implement `ChildCountChecker` in `api/BusTerminal.Api/Features/Registry/_Shared/ChildCountChecker.cs`: delegates to `IRegistryEntityStore.CountChildrenAsync`; produces `HasChildrenResponse` with per-entity-type breakdown.
- [X] T040 Implement `RegistryEndpointsBuilder` in `api/BusTerminal.Api/Features/Registry/_Shared/RegistryEndpointsBuilder.cs`: shared `MapGroup("/api/registry").RequireAuthorization()` pattern; sets RFC-7807 problem-content-type defaults; surfaces W3C trace context on responses; no role policy (FR-037 deviation per `plan.md` Complexity Tracking).
- [X] T041 Implement `RegistryDtoMapping` in `api/BusTerminal.Api/Features/Registry/_Shared/RegistryDtoMapping.cs`: entity↔request/response DTO converters; centralizes the `_overwriteAcknowledged` extraction and the `fullyQualifiedName` server-side computation.
- [X] T042 [TEST] Write `api/BusTerminal.Api.Tests/Features/Registry/_Shared/ConcurrencyConflictMapperTests.cs` covering: clean diff, tag-array diff (multi-value-per-key edge case), metadata-object diff (nested JSON), null↔value transitions.
- [X] T043 [TEST] Write `api/BusTerminal.Api.Tests/Features/Registry/_Shared/ChildCountCheckerTests.cs` covering: zero children, single child, multi-type children breakdown.

### 2.6 Indexer — Functions project

- [X] T044 [P] Author `api/BusTerminal.Indexer/Program.cs` (FunctionsApplicationBuilder + OTel adapter pointing at the shared App Insights resource with `Cloud Role Name = busterminal-indexer`). Register the following services explicitly via `builder.Services`: `IAzureCredentialFactory` (singleton, shared factory pattern from spec-004), `CosmosClient` (singleton; constructed via `CosmosClientFactory` using the credential factory; consumed by the change-feed-trigger extension), `SearchClient` (singleton; constructed via `new SearchClient(endpoint, indexName, credential)` per `Azure.Search.Documents` SDK), `ISearchDocumentMapper` → `SearchDocumentMapper`, `IPoisonHandler` → `PoisonHandler`, and `TimeProvider` (singleton, system default). Bind `IndexerOptions` from configuration.
- [X] T045 [P] Author `api/BusTerminal.Indexer/Dockerfile` based on the official Microsoft Functions v2 base image for .NET 10 isolated worker, exposing port 8080 and including the `func` runtime.
- [X] T046 [P] Author `api/BusTerminal.Indexer/host.json` (worker SDK + telemetry config) and `local.settings.json.example` (env-var keys with placeholders per `contracts/indexer-events.md §1`).
- [X] T047 [P] Implement `api/BusTerminal.Indexer/Indexing/IndexNames.cs` (single source of truth — env-var-backed) and `api/BusTerminal.Indexer/Indexing/SearchDocumentMapper.cs` per `contracts/indexer-events.md §3`.
- [X] T048 Implement `api/BusTerminal.Indexer/Functions/RegistryEntityIndexer.cs` — Cosmos change-feed trigger per `contracts/indexer-events.md §1`; handles tombstone vs upsert; calls `SearchClient.MergeOrUploadDocumentsAsync` / `DeleteDocumentsAsync`; emits OTel spans per change.
- [X] T049 Implement `api/BusTerminal.Indexer/Functions/PoisonHandler.cs` — structured Error log with `entityId`, `eventType`, `errorCategory`, `retryCount`, `correlationId` per `contracts/indexer-events.md §5`.
- [X] T050 [TEST] Write `api/BusTerminal.Indexer.Tests/SearchDocumentMapperTests.cs` covering: every field mapping in `contracts/indexer-events.md §3` + tag-key lowercase projection + metadata-flat dot-path keys + null normalizations.
- [X] T051 [TEST] Write `api/BusTerminal.Indexer.Tests/RegistryEntityIndexerTests.cs` (integration; uses dev Cosmos + dev AI Search test index): Cosmos write → trigger fires → AI Search document appears within 5s p95 (SC-005).

### 2.7 Frontend — shared registry foundations

- [X] T052 [P] Author `web/lib/registry/types.ts` (TypeScript types inferred from Zod schemas) and `web/lib/registry/schemas.ts` (Zod schemas for `RegistryEntity`, create-request, update-request, conflict-response, audit-event, search-result) mirroring `contracts/registry-entity.schema.json` and friends.
- [X] T053 [P] Author `web/lib/registry/api.ts` — typed fetch client (RSC-safe + client-safe) composing `web/lib/http/` (which already propagates W3C Trace Context per FR-042); exposes `list`, `get`, `create`, `update`, `delete`, `search`, `listAudit` with strongly typed input/output.
- [X] T054 [P] Author `web/lib/registry/conflict.ts` — `parseConflictResponse(error)` + `diffEntities(current, submitted)` helpers that the form layer consumes.
- [X] T055 [P] Author `web/lib/registry/tag-utils.ts` — lowercase-key normalization + multi-value-per-key helpers per `research.md §9`.
- [X] T056 [P] Author `web/lib/registry/query-keys.ts` — TanStack Query key factories for entities, search, audit.
- [X] T057 Wire TanStack Query `QueryClientProvider` into `web/app/providers.tsx` (mounted in the existing `layout.tsx` provider tree). Configure stale-time for entity queries (60s) and audit queries (10s); disable suspense by default; integrate the existing observability adapter for query-error reporting.
- [X] T058 [P] [TEST] Write `web/lib/registry/__tests__/conflict.test.ts` covering diff helpers across canonical-field-shapes, tags, metadata.
- [X] T059 [P] [TEST] Write `web/lib/registry/__tests__/tag-utils.test.ts` covering case-insensitive key match + display-normalization rules.

### 2.8 Shared-schema contract test

- [X] T060 [TEST] Write `web/scripts/audit-shared-schemas.mjs` (invoked via new `pnpm run test:contracts`) that compares Zod-derived JSON schemas in `web/lib/registry/schemas.ts` to `specs/006-service-bus-registry-core/contracts/registry-entity.schema.json` and friends, failing on drift.
- [X] T061 [TEST] Write `api/BusTerminal.Api.Tests/Features/Registry/_Shared/SharedSchemaContractTests.cs` that compares the FluentValidation rule set in `RegistryEntityValidationRules` to the canonical `registry-entity.schema.json`, failing on drift (asserts: every Error-severity rule maps to a Schema constraint and vice versa).

**Checkpoint**: Foundation ready — IaC live in dev, persistence + indexer + shared backend + shared frontend in place. User-story implementation can now begin.

---

## Phase 3: User Story 1 — Manually register and browse Service Bus assets (Priority: P1) 🎯 MVP

**Goal**: Operator can sign in, register namespaces / queues / topics / subscriptions / rules, browse them in an environment-aware explorer, open detail pages, edit metadata, delete leaves. Persistence survives reload and service restart. (Spec §User Story 1.)

**Independent Test**: From the quickstart §5 walkthrough, create one of each entity type, edit one, delete one, attempt a duplicate-name create (expect 409), attempt a missing-required-field create (expect 400). All API responses match `contracts/registry-api.yaml`; all entities appear in the explorer immediately and after reload.

### Tests for User Story 1 (TDD — write FIRST, ensure FAIL before implementation) ⚠️

- [X] T062 [P] [US1] [TEST] Contract test for `POST /api/registry` in `api/BusTerminal.Api.Tests/Features/Registry/CreateEntityEndpointTests.cs`: covers the canonical happy path for each `entityType`; asserts response shape conforms to `RegistryEntity` schema; ETag header present; Location header present.
- [X] T063 [P] [US1] [TEST] Contract test for `GET /api/registry/{id}` in `api/BusTerminal.Api.Tests/Features/Registry/GetEntityEndpointTests.cs` — covers happy path, 404, AND the tombstone-exclusion case (a recently-deleted entity whose tombstone document still exists in `registry-entities` MUST return 404, not the tombstone shape).
- [X] T064 [P] [US1] [TEST] Contract test for `PUT /api/registry/{id}` in `api/BusTerminal.Api.Tests/Features/Registry/UpdateEntityEndpointTests.cs`: covers happy path + `409 ConcurrencyConflict` body conforms to `ConflictResponse` schema + `400 ForceOverwriteWithoutConflict` rejection.
- [X] T065 [P] [US1] [TEST] Contract test for `DELETE /api/registry/{id}` in `api/BusTerminal.Api.Tests/Features/Registry/DeleteEntityEndpointTests.cs`: covers happy leaf delete + `409 HasChildren` body conforms to `HasChildrenResponse` schema.
- [X] T066 [P] [US1] [TEST] Contract test for `GET /api/registry` (list) in `api/BusTerminal.Api.Tests/Features/Registry/ListEntitiesEndpointTests.cs`: covers env-scoped pagination + continuation token + `entityType` filter + `parentId` filter + **tombstone exclusion** (a list query that includes the partition of a recently-deleted entity MUST NOT return any document with `_isTombstone = true`).
- [X] T067 [P] [US1] [TEST] Integration test for end-to-end CRUD across all five entity types in `api/BusTerminal.Api.Tests/Features/Registry/RegistryEndToEndTests.cs` (uses `RegistryFixture` from T033).
- [X] T068 [P] [US1] [TEST] Vitest component tests for the explorer tree in `web/components/registry/__tests__/registry-explorer-tree.test.tsx`: empty state, single namespace, nested children, expand/collapse.
- [X] T069 [P] [US1] [TEST] Vitest component test for `registry-conflict-modal` in `web/components/registry/__tests__/registry-conflict-modal.test.tsx`: rendering field diff, discard/force-overwrite actions invoking correct callbacks.
- [X] T070 [P] [US1] [TEST] Playwright E2E `web/tests/e2e/registry/create-browse.e2e.spec.ts` covering the quickstart §5 golden path.
- [X] T071 [P] [US1] [TEST] Playwright E2E `web/tests/e2e/registry/edit-conflict.e2e.spec.ts` covering the quickstart §8 conflict walkthrough.
- [X] T072 [P] [US1] [TEST] Playwright E2E `web/tests/e2e/registry/delete-blocked.e2e.spec.ts` covering FR-009 (delete blocked by children).
- [X] T073 [P] [US1] [TEST] axe-playwright a11y test `web/tests/a11y/registry/explorer.a11y.spec.ts` covering the explorer route on both dark and light themes.

### Implementation for User Story 1 — Backend endpoints

- [X] T074 [P] [US1] Implement per-entity-type FluentValidators in `api/BusTerminal.Api/Features/Registry/{Namespaces,Queues,Topics,Subscriptions,Rules}/<Type>Validator.cs` — each composes the shared rules plus its `ParentRequiredRule` shape AND its `EntityTypeNameSpecializationRule` per `data-model.md §3.2`: **Namespace** length 6–50 + must-start-letter + hyphens-only-inside charset; **Queue / Topic** length ≤ 260 with the base charset; **Subscription / Rule** length ≤ 50 with the base charset. Per-type validators MUST be exercised by name-format unit-test cases in `RegistryEntityValidationRulesTests.cs` (T027) covering one valid and one invalid name per type.
- [X] T075 [US1] Implement `POST /api/registry` create endpoint in `api/BusTerminal.Api/Features/Registry/_Shared/CreateEndpoint.cs`: polymorphic via `entityType` discriminator; runs `RegistryEntityValidator`, `ParentExistenceRule`, `DuplicateNameRule`; calls `IRegistryEntityStore.CreateAsync`; writes audit `Created` event; returns 201 + ETag + Location. **When the parent entity's `status` is `Deprecated`** (per Story 1 AC #7 / spec amendment for CHK029), the audit event's `changeSummary` MUST be prefixed with the literal string `UNDER_DEPRECATED_PARENT: ` so downstream governance queries can filter for them; the create itself is still permitted.
- [X] T076 [US1] Implement `GET /api/registry/{id}` in `api/BusTerminal.Api/Features/Registry/_Shared/GetEndpoint.cs`: Cosmos point-read; returns ETag header; 404 when missing **OR when the read returns a document with `_isTombstone = true`** (tombstones are an internal change-feed-propagation artifact per `research.md §10` and MUST NEVER surface to the API client).
- [X] T077 [US1] Implement `GET /api/registry` list endpoint in `api/BusTerminal.Api/Features/Registry/_Shared/ListEndpoint.cs`: env-scoped (env query parameter REQUIRED per amended FR-035), paginated, filterable by `entityType`/`parentId`/`status`; returns `{items, continuationToken}`. **Server-side filter MUST include `WHERE NOT IS_DEFINED(c._isTombstone) OR c._isTombstone = false`** so internal tombstone documents never appear in client-facing list results.
- [X] T078 [US1] Implement `PUT /api/registry/{id}` in `api/BusTerminal.Api/Features/Registry/_Shared/UpdateEndpoint.cs`: requires `If-Match`; validates payload; calls `IRegistryEntityStore.UpdateAsync`; on 412 → `ConcurrencyConflictMapper` → 409 with `ConflictResponse`; on `_overwriteAcknowledged: true` without conflict → 400 `ForceOverwriteWithoutConflict`; writes audit `Updated` event (with `wasForceOverwrite` flag).
- [X] T079 [US1] Implement `DELETE /api/registry/{id}` in `api/BusTerminal.Api/Features/Registry/_Shared/DeleteEndpoint.cs`: `ChildCountChecker` first → 409 `HasChildren` if non-zero; tombstone-then-delete per `research.md §10`; writes audit `Deleted` event; returns 204.
- [X] T080 [US1] Wire `MapRegistryEndpoints()` extension method in `api/BusTerminal.Api/Features/Registry/_Shared/RegistryEndpointsBuilder.cs` that mounts T075–T079 (and the search/audit endpoints from US2/US3 once those phases land); call from `Program.cs`.
- [X] T081 [US1] Implement `PATCH /api/registry/{id}/status` endpoint in `api/BusTerminal.Api/Features/Registry/_Shared/StatusEndpoint.cs` per FR-013a (Active ↔ Deprecated); writes audit `StatusChanged` event with field diff.
- [X] T082 [US1] Add runtime OpenAPI-vs-contract assertion test `api/BusTerminal.Api.Tests/Features/Registry/OpenApiContractTests.cs` that loads `/openapi/v1.json` from the WebApplicationFactory and compares it to `contracts/registry-api.yaml`.

### Implementation for User Story 1 — Frontend explorer + detail + forms

- [X] T083 [P] [US1] Author `web/app/(authenticated)/registry/layout.tsx` — two-pane shell with the explorer tree on the left (Client Component) and an outlet on the right.
- [X] T084 [P] [US1] Author `web/app/(authenticated)/registry/page.tsx` — RSC landing page with welcome + "recent activity" placeholder; instructs operator to "select an entity from the explorer or click New".
- [X] T085 [P] [US1] Author `web/components/registry/registry-explorer-tree.tsx` (Client Component) using the existing tree primitives + lazy-load on expand via TanStack Query; composes `registry-tree-node.tsx`.
- [X] T086 [P] [US1] Author `web/components/registry/registry-tree-node.tsx` — single-node rendering, composes existing `environment-badge`, `entity-relationship-badge`, and lucide icons per `entityType`.
- [X] T087 [P] [US1] Author `web/app/(authenticated)/registry/[entityType]/[id]/page.tsx` — RSC detail page that fetches via `web/lib/registry/api.ts get()`; passes data to detail-shell.
- [X] T088 [P] [US1] Author `web/components/registry/registry-detail-shell.tsx` — composes `registry-metadata-panel`, `registry-relationships-panel` (placeholder in US1; populated by US3), `registry-audit-panel` (placeholder; populated by US3), `registry-status-badge`.
- [X] T089 [P] [US1] Author `web/components/registry/registry-metadata-panel.tsx` — composes existing `metadata-key-value-panel` for canonical fields; renders empty-state placeholders for null fields per Edge Case "Missing optional metadata".
- [X] T090 [P] [US1] Author `web/components/registry/registry-status-badge.tsx` — visually distinguishes Active vs Deprecated (color + icon + label per FR-047).
- [X] T091 [P] [US1] Author `web/components/registry/registry-tag-editor.tsx` — free-form key/value list editor; uses `web/lib/registry/tag-utils.ts`.
- [X] T092 [P] [US1] Author `web/components/registry/forms/shared/entity-form-shell.tsx` — RHF + Zod scaffold; submit / saving / saved / error states (FR-029); hooks up `registry-conflict-modal`.
- [X] T093 [P] [US1] Author `web/components/registry/forms/shared/azure-resource-id-input.tsx` — ARM-resource-id text input with inline parse-and-validate per the `AzureResourceIdFormatRule`.
- [X] T094 [P] [US1] Author `web/components/registry/forms/namespace-form.tsx` (RHF + Zod) — fields: name, environment, owner, description, azureResourceId, tags, metadata.
- [X] T095 [P] [US1] Author `web/components/registry/forms/queue-form.tsx` — adds parent-namespace picker (queries existing namespaces in selected env); when the selected parent's `status === 'Deprecated'`, surfaces an inline warning banner ("This parent is Deprecated — new children created here will be flagged in audit") above the submit row but DOES NOT block submission (Story 1 AC #7).
- [X] T096 [P] [US1] Author `web/components/registry/forms/topic-form.tsx` — same as queue-form with topic-specific metadata defaults.
- [X] T097 [P] [US1] Author `web/components/registry/forms/subscription-form.tsx` — adds parent-topic picker; same Deprecated-parent warning behavior as T095 (Story 1 AC #7).
- [X] T098 [P] [US1] Author `web/components/registry/forms/rule-form.tsx` — adds parent-subscription picker; same Deprecated-parent warning behavior as T095 (Story 1 AC #7).
- [X] T099 [P] [US1] Author `web/app/(authenticated)/registry/new/[entityType]/page.tsx` — Client Component that switches on `entityType` and renders the matching form from T094–T098.
- [X] T100 [P] [US1] Author `web/app/(authenticated)/registry/[entityType]/[id]/edit/page.tsx` — Client Component edit page that prefetches the entity, renders the matching form with `_etag` hidden field, integrates `useMutation` with conflict-modal `onError` per `research.md §14`.
- [X] T101 [P] [US1] Author `web/components/registry/registry-conflict-modal.tsx` — composes existing shadcn Dialog + the diff renderer from `web/lib/registry/conflict.ts`; two CTAs ("Discard my changes and refresh", "Force overwrite").
- [X] T102 [P] [US1] Author `web/components/registry/registry-delete-confirmation.tsx` — confirms deletion + clearly communicates the block-with-children policy (FR-030).
- [X] T103 [P] [US1] Author `web/components/registry/registry-empty-state.tsx` — shared empty-state component used in explorer, search, and audit panels.
- [X] T103a [P] [US1] Author `web/components/registry/registry-unauthorized-state.tsx` — registry-specific unauthorized state (FR-031) used by error boundaries across explorer, search, detail, and form routes. Composes existing shadcn `Alert` + a "Sign in again" CTA that triggers MSAL re-auth via the existing `web/lib/auth/` helpers; preserves the current URL as the post-auth redirect target so the operator lands back where they were.
- [X] T103b [P] [US1] Author `web/components/registry/registry-env-switcher.tsx` (Client Component) — the environment selector mandated by amended FR-035. Persists selection in `localStorage` under key `busterminal.registry.lastEnvironment`; auto-selects the first configured environment alphabetically on first visit; reads the configured environment list from a typed config endpoint (`/api/registry/environments` — list returned from the persistent store via a partition-agnostic distinct query). Mount the switcher in the explorer layout (`registry/layout.tsx` — T083) and propagate selection via URL query (`?environment=<env>`) so server components see it.
- [X] T103c [US1] Implement `GET /api/registry/environments` in `api/BusTerminal.Api/Features/Registry/_Shared/EnvironmentsEndpoint.cs`: returns the distinct sorted list of environments currently present in `registry-entities` (cross-partition query — bounded result size, cached for 60 seconds via `IMemoryCache`). Used by T103b to populate the switcher.
- [X] T103d [P] [US1] [TEST] Playwright E2E `web/tests/e2e/registry/unauthorized-state.e2e.spec.ts` — sign-out mid-edit, attempt save, verify the unauthorized state renders with the re-auth CTA and that the operator returns to the original URL after sign-in.
- [X] T103e [P] [US1] [TEST] Vitest test for `registry-env-switcher` in `web/components/registry/__tests__/registry-env-switcher.test.tsx` — covers: first-visit default selection (alphabetical first env), localStorage persistence across reloads, URL-query propagation, list refresh.

**Checkpoint**: User Story 1 is fully functional. Operator can do every CRUD action on every entity type via UI and API; explorer reflects state; persistence survives reload. **MVP demo-ready.**

---

## Phase 4: User Story 2 — Discover assets via search and filters (Priority: P2)

**Goal**: Operator runs full-text search across the registry with filters (entity type, environment, status, tag) and gets sub-second ranked results.

**Independent Test**: From the quickstart §6 walkthrough, with a populated registry, type a partial entity name and see ranked results in under one second; apply filters and verify narrowing; verify the empty-state distinguishes "no results" from "search unavailable"; disconnect AI Search briefly and verify browse/detail still work (SC-011).

### Tests for User Story 2 (TDD) ⚠️

- [ ] T104 [P] [US2] [TEST] Contract test for `GET /api/registry/search` in `api/BusTerminal.Api.Tests/Features/Registry/SearchEndpointTests.cs` covering query, filters, sort variants, pagination, and 503 fallback.
- [ ] T105 [P] [US2] [TEST] Integration test for end-to-end search lag in `api/BusTerminal.Api.Tests/Features/Registry/SearchIndexLagTests.cs` (uses `RegistryFixture` + dev AI Search): create entity → search within SC-005 budget; verify ranked order.
- [ ] T106 [P] [US2] [TEST] Vitest component test `web/components/registry/__tests__/registry-search-results-table.test.tsx` covering empty/loading/loaded/error states and the empty-state distinction (FR-031).
- [ ] T107 [P] [US2] [TEST] Playwright E2E `web/tests/e2e/registry/search.e2e.spec.ts` covering the quickstart §6 walkthrough.
- [ ] T108 [P] [US2] [TEST] axe-playwright a11y test `web/tests/a11y/registry/search.a11y.spec.ts` on dark + light themes.

### Implementation for User Story 2 — Backend search endpoint

- [ ] T109 [US2] Implement `GET /api/registry/search` in `api/BusTerminal.Api/Features/Registry/Search/SearchEndpoint.cs`: validates query params; constructs OData `$filter` (entityType, environment, status, tagKeysLower, tags/value); sets `$top`/`$skip` per `research.md §13`; calls `ISearchClient.Search`; maps results to `SearchResult` DTOs; returns 503 on AI Search outage with RFC-7807 body.
- [ ] T110 [US2] Implement request DTO `SearchRequest` and response DTO `SearchResponse` in `api/BusTerminal.Api/Features/Registry/Search/SearchRequests.cs` and `SearchResponses.cs` per `contracts/registry-api.yaml`.

### Implementation for User Story 2 — Frontend search route

- [ ] T111 [P] [US2] Author `web/app/(authenticated)/registry/search/page.tsx` — RSC entrypoint that reads search params and delegates to a Client Component for the interactive search box.
- [ ] T112 [P] [US2] Author `web/components/registry/registry-search-input.tsx` — `cmdk`-based global search bar with debounced typeahead via TanStack Query.
- [ ] T113 [P] [US2] Author `web/components/registry/registry-search-results-table.tsx` — TanStack Table rendering paginated results; row click navigates to detail page; columns: name, entity type, environment, parent namespace, owner, score.
- [ ] T114 [P] [US2] Author `web/components/registry/registry-search-filters.tsx` — chip-style filters for entity type, environment, status, tag key, tag value; URL-synced.
- [ ] T115 [US2] Mount the search input as a global app-shell affordance (`web/components/app-shell/` integration) so search is reachable from any page.

**Checkpoint**: User Story 2 layered onto Story 1. Operators can search the registry; filters narrow; sub-1s p95; search outage gracefully degrades.

---

## Phase 5: User Story 3 — Traverse relationships and review change history (Priority: P3)

**Goal**: Operator can navigate from a parent entity to its children (and back), and view the entity's recent audit history on its detail page.

**Independent Test**: From the quickstart §7 walkthrough, navigate topic → subscription list → subscription detail → rule list → rule detail; on each entity's detail page, the audit panel lists recent create/update/delete events with actor, timestamp, change summary; clicking a `StatusChanged` event reveals the field diff.

### Tests for User Story 3 (TDD) ⚠️

- [ ] T116 [P] [US3] [TEST] Contract test for `GET /api/registry/{id}/audit` in `api/BusTerminal.Api.Tests/Features/Registry/AuditEndpointTests.cs` covering: ordered by timestamp desc, `limit` query param, max-200 enforcement, append-only enforcement (no POST/PUT/DELETE exposed).
- [ ] T117 [P] [US3] [TEST] Integration test `api/BusTerminal.Api.Tests/Features/Registry/AuditEventEmissionTests.cs` verifying every CRUD operation emits exactly one audit event with correct shape per `contracts/audit-event.schema.json`, including `wasForceOverwrite` and `correlationId`.
- [ ] T118 [P] [US3] [TEST] Vitest component tests for `registry-relationships-panel` and `registry-audit-panel` in `web/components/registry/__tests__/`.
- [ ] T119 [P] [US3] [TEST] Playwright E2E `web/tests/e2e/registry/relationships-audit.e2e.spec.ts` covering the quickstart §7 walkthrough.
- [ ] T120 [P] [US3] [TEST] axe-playwright a11y test `web/tests/a11y/registry/detail.a11y.spec.ts` on dark + light themes (covers relationships + audit panels).

### Implementation for User Story 3 — Backend audit endpoint

- [ ] T121 [US3] Implement `GET /api/registry/{id}/audit` in `api/BusTerminal.Api/Features/Registry/Audit/AuditEndpoint.cs`: delegates to `IAuditEventStore.ListForEntity`; supports `?limit=N` (1..200, default 50); returns `{items}` with newest first; 200 on success.
- [ ] T122 [US3] Implement response DTO `AuditListResponse` in `api/BusTerminal.Api/Features/Registry/Audit/AuditResponses.cs` per `contracts/registry-api.yaml`.

### Implementation for User Story 3 — Frontend relationships + audit

- [ ] T123 [P] [US3] Replace the relationships-placeholder in `registry-detail-shell.tsx` (T088) by wiring the implementation of `web/components/registry/registry-relationships-panel.tsx`: lists children (queries via list-endpoint with `parentId` filter) using TanStack Table; row click navigates to child detail.
- [ ] T124 [P] [US3] Replace the audit-placeholder in `registry-detail-shell.tsx` by wiring the implementation of `web/components/registry/registry-audit-panel.tsx`: lists most recent N events via TanStack Query; renders actor, timestamp, change summary; click reveals field-diff popover for `Updated` / `StatusChanged` events.
- [ ] T125 [US3] Augment `web/components/registry/forms/shared/entity-form-shell.tsx` (T092) to invalidate the audit-panel query after every successful mutation so the new event appears immediately (quickstart §7 expectation).

**Checkpoint**: All three user stories functional and independently testable. Relationships + audit deepen the slice into a governance tool.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Tech-stack registration, documentation, perf validation, post-merge follow-ups.

- [ ] T126 [P] Update `speckit-artifacts/tech-stack.md` per `research.md §21` and `quickstart.md §11`: add TanStack Query 5.x (§2 Frontend), FluentValidation 11.10.x (§1 Backend), Cosmos change-feed-lease-container convention (§5), v2 native Functions-for-CAE (§6).
- [ ] T127 [P] Update `web/README.md` and `api/BusTerminal.Api/` README (if present, else create stub) to point at `specs/006-service-bus-registry-core/quickstart.md` for the registry walkthrough.
- [ ] T128 [P] Run the SC-002/SC-003/SC-004/SC-005 perf validations from the dev environment: capture App Insights metrics for search p95 (< 1s), detail p95 (< 500ms), CRUD p95 (< 1s), index lag p95 (< 5s); record results in `specs/006-service-bus-registry-core/checklists/perf-results.md`.
- [ ] T129 [P] Run the SC-008 a11y validation: confirm `pnpm test:a11y` passes with zero violations on every registry route on both dark and light themes.
- [ ] T129a [P] [TEST] Add `web/tests/a11y/registry/reduced-motion.spec.ts` — Playwright a11y test that emulates `prefers-reduced-motion: reduce` (`page.emulateMedia({ reducedMotion: 'reduce' })`) and asserts (a) the explorer tree expand/collapse does NOT animate, (b) the conflict-modal open/close does NOT animate, (c) the status-change visual cue does NOT animate. Covers FR-048.
- [ ] T129b [P] [TEST] Add `web/tests/e2e/registry/sc-010-time-to-find.e2e.spec.ts` — Playwright timed assertion for SC-010: with a populated registry, the operator types a partial name in the global search affordance from an arbitrary page and reaches the entity's detail page in under 30 seconds (asserts wall-clock from first keystroke to detail-page `domcontentloaded`). Captures the metric to App Insights via the existing observability adapter.
- [ ] T130 [P] Run the SC-012 trace-correlation validation: with a populated registry, select an arbitrary UI trace in App Insights and confirm the linked backend spans share the same trace id.
- [ ] T131 [P] [TEST] Cross-story integration test `api/BusTerminal.Api.Tests/Features/Registry/EndToEndScenariosTests.cs` covering all three quickstart walkthroughs (golden-path, search, relationships+audit) in a single fixture.
- [ ] T132 [P] Update `web/components/app-shell/` global navigation to expose a top-level "Registry" link (icon + label) so the registry route is discoverable from the platform's landing pages.
- [ ] T133 Confirm spec-005 `iac/policies/run-policies.sh` reports zero BT-IAC violations against the new resources; capture the report in the PR description.
- [ ] T134 Run `pnpm run test:contracts` (T060) + the OpenAPI contract test (T082) + the FluentValidation/schema contract test (T061) as a final cross-layer parity gate; if any drift exists, fix at the source.
- [ ] T135 Update spec-006 `CLAUDE.md` SPECKIT block reference if any subsequent slice supersedes this plan (deferred; do not touch in this slice).

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no upstream dependencies.
- **Foundational (Phase 2)**: depends on Setup. BLOCKS all user stories.
- **User Story 1 (Phase 3)**: depends on Foundational. Independently testable.
- **User Story 2 (Phase 4)**: depends on Foundational + the entity-creation paths (T075) so the test fixtures have data to search; otherwise independent of US1 UI.
- **User Story 3 (Phase 5)**: depends on Foundational + the audit-event emission in US1's CRUD endpoints (T075–T079); otherwise independent of US1/US2 UI.
- **Polish (Phase 6)**: depends on all three user stories being complete.

### User Story Dependencies

- **US1 → US2**: US2's search relies on the indexer (Foundational T048) populating the AI Search index from the entities US1 creates. The search code path itself does not depend on US1's UI.
- **US1 → US3**: US3's audit panel relies on the audit events US1's CRUD endpoints emit. The audit-endpoint and audit-panel code itself does not depend on US1's UI.
- **US2 ⊥ US3**: independent of each other.

### Within Each User Story

- Tests (T062–T073 / T104–T108 / T116–T120) are written FIRST and MUST fail before the corresponding implementation tasks run.
- Backend before frontend within a story (form components depend on real API responses for integration tests).
- Shared components before route pages.
- Forms before edit/new routes.

### Parallel Opportunities

- **Phase 1 Setup**: T001 ⊥ T002 ⊥ T005 ⊥ T006 ⊥ T007 ⊥ T008 ⊥ T009 ⊥ T010 ⊥ T011 ⊥ T012 (all touch different files).
- **Phase 2 Foundational**:
  - T013 ⊥ T014 ⊥ T015 (different IaC modules)
  - T020–T026 (different backend `_Shared` files)
  - T044–T047 (different indexer files)
  - T052–T056 (different frontend lib files)
- **Phase 3 US1**: T062–T073 (different test files) all parallelizable; T083–T103 (different component files) all parallelizable EXCEPT T085 depends on T086, T088 depends on T089/T090/T091, and the route pages T099/T100 depend on the form components.
- **Phase 4 US2**: T104–T108 all parallel; T111–T114 all parallel.
- **Phase 5 US3**: T116–T120 all parallel; T123 + T124 parallel.

---

## Parallel Example: Phase 2 Foundational (backend `_Shared`)

```bash
# Launch in parallel — distinct files, no inter-dependencies:
Task: "Implement IRegistryEntity + enums in api/BusTerminal.Api/Features/Registry/_Shared/" (T020)
Task: "Implement RegistryEntity records in api/BusTerminal.Api/Features/Registry/_Shared/RegistryEntity.cs" (T021)
Task: "Implement RegistryTag in api/BusTerminal.Api/Features/Registry/_Shared/RegistryTag.cs" (T022)
Task: "Implement ConflictResponse in api/BusTerminal.Api/Features/Registry/_Shared/ConflictResponse.cs" (T023)
Task: "Implement HasChildrenResponse in api/BusTerminal.Api/Features/Registry/_Shared/HasChildrenResponse.cs" (T024)
Task: "Implement AuditEvent in api/BusTerminal.Api/Features/Registry/_Shared/AuditEvent.cs" (T025)
Task: "Implement RegistryEntityValidationRules.cs" (T026)
```

## Parallel Example: Phase 3 US1 (frontend forms)

```bash
# Launch all five entity-type forms in parallel — distinct files:
Task: "Author namespace-form.tsx" (T094)
Task: "Author queue-form.tsx" (T095)
Task: "Author topic-form.tsx" (T096)
Task: "Author subscription-form.tsx" (T097)
Task: "Author rule-form.tsx" (T098)
```

## Parallel Example: Phase 3 US1 (test suite)

```bash
# Launch all US1 contract + integration + component + E2E + a11y tests in parallel:
Task: "Contract test POST /api/registry" (T062)
Task: "Contract test GET /api/registry/{id}" (T063)
Task: "Contract test PUT /api/registry/{id}" (T064)
Task: "Contract test DELETE /api/registry/{id}" (T065)
Task: "Contract test GET /api/registry list" (T066)
Task: "Integration test end-to-end CRUD" (T067)
Task: "Vitest test registry-explorer-tree" (T068)
Task: "Vitest test registry-conflict-modal" (T069)
Task: "Playwright E2E create-browse" (T070)
Task: "Playwright E2E edit-conflict" (T071)
Task: "Playwright E2E delete-blocked" (T072)
Task: "axe-playwright explorer a11y" (T073)
```

---

## Implementation Strategy

### MVP First (User Story 1 only)

1. Complete **Phase 1: Setup** (T001–T012).
2. Complete **Phase 2: Foundational** (T013–T061). This stands up the IaC, persistence, indexer, and shared scaffolding. The indexer can run unconnected — Story 1 doesn't query the search index.
3. Complete **Phase 3: User Story 1** (T062–T103).
4. **STOP and VALIDATE**: run the quickstart §5 walkthrough end-to-end. CRUD, browse, detail, edit, delete-leaf, delete-blocked, duplicate-name, missing-field, conflict-modal-force-overwrite, status-transition — all pass.
5. **Demo / deploy MVP**.

### Incremental Delivery

After MVP:

1. Add **Phase 4: US2** → demo search (quickstart §6) → deploy.
2. Add **Phase 5: US3** → demo relationships + audit (quickstart §7) → deploy.
3. Add **Phase 6: Polish** → final tech-stack updates, perf validation, a11y gates, cross-story integration tests → deploy.

### Parallel Team Strategy

With multiple developers:

1. Team completes **Setup + Foundational** together. The IaC apply is sequential; backend `_Shared` types (T020–T026) parallelize across engineers; indexer (T044–T051) parallelizes alongside; frontend foundations (T052–T059) parallelize alongside.
2. Once Foundational is done:
   - Developer A: **US1 backend** (T074–T082).
   - Developer B: **US1 frontend** (T083–T103) — starts as soon as US1 backend contract tests (T062–T066) lock the API shape.
   - Developer C: **US2 backend + frontend** (T104–T115) once US1 backend endpoints are running.
   - Developer D: **US3 backend + frontend** (T116–T125) once US1 backend endpoints are running.
3. Three stories complete and integrate independently — each gated by its own quickstart walkthrough.

---

## Notes

- **[P] tasks** = different files, no incomplete-task dependencies. The dependency annotations in T075-style tasks ("depends on T026, T028") govern serialization within a story.
- **[Story] labels** = task → user story traceability. Tasks in Phase 1, Phase 2, Phase 6 carry no story label per the format convention.
- **[TEST] tasks** flagged inline so reviewers can see test-first discipline at a glance.
- Each user story should be independently completable and testable per the spec's acceptance scenarios.
- Verify tests **fail** before implementing.
- Commit after each task or logical group; the `.specify/extensions.yml` auto-commit hooks apply between phases.
- Stop at any checkpoint to validate story independently.
- Avoid: vague tasks, same-file conflicts, cross-story dependencies that break independence.

---

## Post-Analysis Amendments (2026-06-02)

`/speckit-analyze` produced findings U1, U2, U3, G1–G4, I1, L1, D1. All resolved in this document by amendments to T026, T044, T063, T066, T074, T076, T077 and by the addition of new tasks **T103a, T103b, T103c, T103d, T103e, T129a, T129b** (preserving original IDs to maintain referential stability with the analysis report). The I1 environment-scoping decision (mandate env-bound browse; cross-env discovery via search only) is reflected in:

- `spec.md` FR-035 (revised).
- `contracts/registry-api.yaml` `/registry/environments` endpoint (new).
- `data-model.md` §3 (new `EntityTypeNameSpecializationRule` + `TimestampImmutableRule`) and §5 (audit-vs-telemetry PII boundary note).
- `research.md` §21 ↔ `quickstart.md` §11 cross-link.

## Format Validation

All tasks above conform to the strict checklist format:

- ✅ Checkbox `- [ ]` prefix
- ✅ Sequential task IDs `T001..T135` + amendments `T103a..T103e`, `T129a..T129b`
- ✅ `[P]` markers only where files genuinely don't overlap and dependencies are met
- ✅ `[US1]`/`[US2]`/`[US3]` labels appear ONLY in Phases 3-5 (story phases)
- ✅ Setup, Foundational, and Polish phases carry NO story label
- ✅ Every task names an exact file path
- ✅ `[TEST]` annotations are inline (do not violate format — they are part of the description)
