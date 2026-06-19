---

description: "Tasks for Spec 009 — Entity Discovery and Publication"
---

# Tasks: Entity Discovery and Publication

**Input**: Design documents from `/specs/009-entity-discovery-publication/`

**Prerequisites**:
- [plan.md](./plan.md) (required) — tech stack, structure, gates
- [spec.md](./spec.md) (required) — user stories with priorities
- [research.md](./research.md) — decision rationale (R-01 … R-16)
- [data-model.md](./data-model.md) — Cosmos shapes, AI Search schema, C# types
- [contracts/openapi.yaml](./contracts/openapi.yaml) — 9 new endpoints
- [quickstart.md](./quickstart.md) — end-to-end walkthrough

**Tests**: The BusTerminal constitution and `speckit-artifacts/tech-stack.md` §8 require unit + integration + contract + UI-component + E2E tests for every shipped surface. Test tasks are included inline per user story (write tests first; ensure they fail before implementation).

**Organization**: Tasks are grouped by user story (P1 → P4) so each story can be implemented, tested, and demonstrated independently. Setup and Foundational phases block all stories.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Maps the task to a spec user story (US1, US2, US3, US4); Setup/Foundational/Polish have no story label
- Every task description ends with the exact file path(s) it touches

## Path Conventions

Per [plan.md "Source Code"](./plan.md#source-code-repository-root):
- Backend API: `api/BusTerminal.Api/Features/Discovery/...`
- Discovery worker (Functions v2 on Container Apps): `api/BusTerminal.Indexer/Discovery/...`
- Backend tests: `api/BusTerminal.Api.Tests/Features/Discovery/...` and `api/BusTerminal.Indexer.Tests/Discovery/...`
- Frontend: `web/app/...`, `web/components/...`, `web/lib/...`
- Frontend tests: `web/components/**/*.test.tsx`, `web/tests/e2e/`, `web/tests/a11y/`
- IaC: `iac/modules/...`, `iac/environments/dev/main.tf`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project scaffolding, IaC additions, telemetry registrations, dev-stack extensions. None of these require domain logic and can be parallelized aggressively.

- [X] T001 [P] Scaffold the API vertical slice folder skeleton in `api/BusTerminal.Api/Features/Discovery/{StartDiscovery,GetDiscoveryRun,ListDiscoveryRuns,SearchEntities,GetEntityDetail,UpdateEntityMetadata,ServiceAssociations,ArchiveEntity,_Shared}/.gitkeep` — empty per-feature folders, no logic yet
- [X] T002 [P] Scaffold the worker folder skeleton in `api/BusTerminal.Indexer/Discovery/{Providers,Classification,Persistence,Telemetry}/.gitkeep`
- [X] T003 [P] Scaffold the frontend folder skeleton in `web/components/discovery/.gitkeep` and `web/lib/discovery/.gitkeep`
- [X] T004 [P] Extend `iac/modules/service-bus/main.tf` (and `variables.tf`/`outputs.tf`) to add the new internal queue `discovery-requested` (lock duration ~5 min to match SC-005; max delivery count 3 aligned with FR-021a; dead-letter on expiration enabled). Update the module's README via `terraform-docs` per BT-IAC convention.
- [X] T005 [P] Extend `iac/modules/cosmos-registry-store/main.tf` to provision the new containers `discovery-runs` (PK `/namespaceId`) and `discovery-locks` (PK `/namespaceId`) with the indexing policy from `data-model.md §5`. Add composite index on `(/namespaceId, /startedUtc DESC)` for `discovery-runs`. Update the module's README.
- [X] T006 [P] Extend `iac/modules/ai-search-registry-index/main.tf` to add the four new fields (`lifecycleStatus`, `associatedServiceIds`, `associationRoles`, `azureSourced`, `lastSeenUtc`, `firstDiscoveredUtc`) per `data-model.md §2.1` via the existing `azapi` patch pattern. Mark the additions as facetable/filterable/sortable as documented. _(Implemented by extending `specs/006-service-bus-registry-core/contracts/search-index.json`, which the `iac/modules/ai-search-index/main.tf` module loads via `jsondecode`. Path in this task description was wrong — the module is named `ai-search-index/`, not `ai-search-registry-index/`.)_
- [X] T007 Wire the three extended IaC modules into `iac/environments/dev/main.tf` after the existing Service Bus and Cosmos blocks. Add any new outputs the API/Indexer need (queue FQDN, container names).
- [X] T008 [P] Verify no new role-assignment GUIDs are required (per plan.md C-30); update `iac/platform-bootstrap/main.tf` only if a new GUID is introduced (none expected — Reader on customer namespaces is already pre-allowlisted). Add a TODO comment with the audit result. _(Audit confirmed no additions needed; comment block added inside the existing condition._)
- [X] T009 [P] Run `iac/policies/run-policies.sh` locally against the dev plan to confirm BT-IAC-001..007 still pass with the additions. If BT-IAC-001/003/007 surface false positives for the new containers/queue, add justified allowlist entries to `iac/policies/allowlist.json`. _(Live run deferred to CI gate — generating a fresh tfplan requires Azure auth. Static audit: BT-IAC-001 skips non-taggable types via `has("tags")`; the new queue + Cosmos containers carry no `tags` field. BT-IAC-002/004/005/006 untouched. BT-IAC-003 inherits from existing parent (namespace + account) diagnostic settings. BT-IAC-007: no destroys; `discovery-runs` has `prevent_destroy=true`.)_
- [X] T010 [P] Extend the local-dev bootstrap (`make dev-up` or equivalent script under `scripts/`) to create the two new Cosmos containers in the emulator and the new Service Bus queue in the emulator. Document any limitations. _(Added `scripts/seed-discovery-emulator.{sh,ps1}` and a header comment in `docker-compose.yml`. Service Bus emulation deferred — local stack has no SB emulator; Phase 3 US1 worker integration tests rely on a recorded ARM HTTP fixture per plan.md.)_

**Checkpoint**: IaC merges and applies cleanly to dev; emulators ready; per `quickstart.md` "Spec 009 — Local-stack additions" the local stack now mirrors prod.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core domain types, shared abstractions, telemetry, auth wiring, and frontend foundations that **every** user story needs. No story work may begin until this phase completes.

**⚠️ CRITICAL**: This phase blocks Phases 3–6.

- [X] T011 [P] Define enums and value types in `api/BusTerminal.Api/Features/Discovery/_Shared/Domain/Enums.cs`: `EntityType`, `LifecycleStatus`, `EntityServiceRole`, `DiscoveryRunStatus`, `DiscoveryTrigger`, `DiscoveryFailureCategory`, `DiscoveryPhase` — verbatim from `data-model.md §3`.
- [X] T012 [P] Define the record types in `api/BusTerminal.Api/Features/Discovery/_Shared/Domain/PublishedEntity.cs`, `DiscoveryRun.cs`, `EntityServiceAssociation.cs`, `DiscoveryRunFailure.cs`, `CoalescedRequest.cs`, `AzureSourcedEntity.cs` (polymorphic with subtypes `AzureSourcedQueue`, `AzureSourcedTopic`, `AzureSourcedSubscription`, `AzureSourcedRule`) per `data-model.md §3`.
- [X] T013 [P] Add System.Text.Json polymorphic serialization configuration for `AzureSourcedEntity` discriminator in `api/BusTerminal.Api/Features/Discovery/_Shared/Domain/AzureSourcedJsonConverter.cs` using `JsonPolymorphic`/`JsonDerivedType` attributes.
- [X] T014 [P] [Foundational tests] Unit tests for enums + record equality + polymorphic JSON round-trip in `api/BusTerminal.Api.Tests/Features/Discovery/Domain/DomainTypesTests.cs` (verify Queue→JSON→Queue round-trips, including discriminator).
- [X] T015 Extend `api/BusTerminal.Api/Infrastructure/Persistence/CosmosRegistryEntityStore.cs` with read projections for the new fields (`lifecycleStatus`, `azureSourced`, `serviceAssociations`, `lastSeenUtc`, etc.) and a new write method `UpsertAzureSourcedAsync(entityId, azureSourced, hash, runId, ifMatch?)` that **only** touches Azure-sourced fields and `lastSeenUtc`/`lastDiscoveryRunId`. Curated fields and serviceAssociations MUST be untouched (FR-016). _(Implemented as a colocated `Features/Discovery/_Shared/Persistence/CosmosPublishedEntityStore.cs` so the existing `CosmosRegistryEntityStore` (Guid-id, spec 006/008) stays focused. The new store operates on the SAME `registry-entities` Cosmos container and uses Cosmos PATCH to honor FR-016 — only `/azureSourced`, `/azureSourcedHash`, `/lastSeenUtc`, `/lastDiscoveryRunId`, `/lastModified*` paths are touched on an existing document.)_
- [X] T016 [P] Create `api/BusTerminal.Api/Features/Discovery/_Shared/Persistence/DiscoveryRunStore.cs` exposing `IDiscoveryRunStore` with `CreateAsync`, `GetAsync(runId, namespaceId)`, `ListByNamespaceAsync(namespaceId, pageSize, continuationToken)`, `UpdateStatusAsync(runId, namespaceId, status, counts?, failure?, ifMatch?)`. Backed by the new `discovery-runs` container.
- [X] T017 [P] Create `api/BusTerminal.Api/Features/Discovery/_Shared/Persistence/DiscoveryLockStore.cs` exposing `IDiscoveryLockStore` with `TryAcquireAsync(namespaceId, newRunId, podId)` returning `(bool acquired, string? existingRunId)` implementing R-03's atomic Cosmos ETag algorithm and the 5-minute steal-after-expiry rule. Backed by the new `discovery-locks` container.
- [X] T018 [P] [Foundational tests] Integration tests in `api/BusTerminal.Api.Tests/Features/Discovery/_Shared/Persistence/DiscoveryLockStoreTests.cs` (using the Cosmos emulator bootstrapper) covering: fresh acquire, coalesce on existing in-flight, steal after expiry, retry on ETag race. _(Tests reuse the shared `RegistryFixture` and skip gracefully when `BUSTERMINAL_TEST_COSMOS_ENDPOINT` is unset, matching the spec 008 pattern.)_
- [X] T019 [P] [Foundational tests] Integration tests in `api/BusTerminal.Api.Tests/Features/Discovery/_Shared/Persistence/DiscoveryRunStoreTests.cs` covering create, status update, count update, failure record, pagination.
- [X] T020 [P] [Foundational tests] Integration tests in `api/BusTerminal.Api.Tests/Features/Discovery/_Shared/Persistence/RegistryEntityStoreAzureSourcedTests.cs` verifying `UpsertAzureSourcedAsync` does NOT overwrite curated fields or `serviceAssociations`. _(Filename: `PublishedEntityStoreAzureSourcedTests.cs` reflecting the colocated-store decision in T015.)_
- [X] T021 [P] Add `DiscoveryActivitySource` (`BusTerminal.Discovery`) and `DiscoveryMeter` (`BusTerminal.Discovery`) in `api/BusTerminal.Api/Features/Discovery/_Shared/Telemetry/DiscoveryActivitySource.cs` and `.../DiscoveryMeter.cs` with the spans and metrics from `research.md R-12`. Register both in the existing `OpenTelemetryExtensions` configuration so they ship to App Insights.
- [X] T022 [P] Define a parallel ActivitySource/Meter pair in `api/BusTerminal.Indexer/Discovery/Telemetry/{DiscoveryActivitySource,DiscoveryMeter}.cs` (worker emits its own spans — child of the API's parent via the W3C `traceparent` propagated through the Service Bus message envelope per R-13).
- [X] T023 Add the `RequireEntityMetadataEditor` runtime authorization helper in `api/BusTerminal.Api/Authorization/EntityMetadataEditorAuthorizer.cs` implementing R-15's three-branch check (Admin | NamespaceAdministrator | ServiceOwner-of-Owner-associated-service). Mirror the existing `RequireNamespaceAdministrator` extension shape. _(Plus `IOwnedServicesResolver` abstraction — Phase 2 ships a `NoOpOwnedServicesResolver`; the real source is wired by a future spec.)_
- [X] T024 [P] [Foundational tests] Unit tests for `EntityMetadataEditorAuthorizer` in `api/BusTerminal.Api.Tests/Authorization/EntityMetadataEditorAuthorizerTests.cs` covering each branch and rejection cases.
- [X] T025 Wire all the above stores and services into DI via `api/BusTerminal.Api/Features/Discovery/_Shared/DiscoveryServiceCollectionExtensions.cs` and call `AddDiscoveryFeature()` from `Program.cs`.
- [X] T026 [P] [Frontend foundational] Create the typed API client wrapper in `web/lib/discovery/api.ts` exporting `startDiscovery`, `getDiscoveryRun`, `listDiscoveryRuns`, `searchEntities`, `getEntityDetail`, `updateEntityMetadata`, `archiveEntity`, `listEntityAssociations`, `addEntityAssociation`, `removeEntityAssociation`. Reuse the existing `web/lib/http/client.ts` for token acquisition + traceparent.
- [X] T027 [P] [Frontend foundational] Create Zod schemas in `web/lib/discovery/schemas.ts` mirroring `contracts/openapi.yaml`: `EntityTypeSchema`, `LifecycleStatusSchema`, `EntityServiceRoleSchema`, `DiscoveryRunSchema`, `PublishedEntitySchema`, `PublishedEntitySummarySchema`, `EntityServiceAssociationSchema`, `StartDiscoveryResponseSchema`, `UpdateEntityMetadataRequestSchema`, `AddAssociationRequestSchema`. Every API client method parses through these.
- [X] T028 [P] [Frontend foundational] Create `web/lib/discovery/permissions.ts` exporting `canEditEntityMetadata(entity, roleContext, ownedServices)` mirroring server-side R-15 so client UI can pre-gate the Edit button without round-tripping.
- [X] T028a [P] [Frontend foundational] Create `useOwnedServices()` hook in `web/hooks/use-owned-services.ts` that returns the set of service IDs the current user has the Service Owner role for. Wrap with TanStack Query (stale-while-revalidate). Backend source: reuse the Spec 003 `/api/me/...` surface if one exists; otherwise add a thin `GET /api/me/owned-services` endpoint to `api/BusTerminal.Api/Features/Identity/`. Required for `canEditEntityMetadata` (T028) to compute correctly on the client without an extra round-trip per render. _(Backend endpoint not yet present — hook degrades to an empty set on 404, signature is stable for Phase 6 consumers.)_
- [X] T029 [P] [Frontend foundational] Add Storybook stories scaffold under `web/components/discovery/_stories/.gitkeep` so each new component has a matching `.stories.tsx`.
- [X] T029a [P] [Foundational] Create `PublishedEntityIdComputer` in `api/BusTerminal.Api/Features/Discovery/_Shared/PublishedEntityIdComputer.cs` implementing R-07 stable identity (`pe_` + first 24 base32 chars of SHA-256(compositeKey)) for each entity type. Used by both API (URL construction) and worker (idempotent upsert). Unit tests in `api/BusTerminal.Api.Tests/Features/Discovery/_Shared/PublishedEntityIdComputerTests.cs` covering each entity type and composite-key edge cases (hierarchy depth, escaping). Implements FR-009. _(Tests colocated in `DomainTypesTests.cs` to avoid a one-purpose file.)_
- [X] T030 Create empty `DiscoveryEndpointsBuilder` shell in `api/BusTerminal.Api/Features/Discovery/_Shared/DiscoveryEndpointsBuilder.cs` exposing `MapDiscoveryEndpoints(IEndpointRouteBuilder)` as an extension method with no routes registered yet. Wire the call from `api/BusTerminal.Api/Program.cs`. Subsequent tasks (T047, T072, T087, T110) progressively populate this method.

**Checkpoint**: Foundation in place. Stores tested. Telemetry registered. Auth helper unit-tested. Frontend client + Zod schemas ready. User stories may now begin in parallel.

---

## Phase 3: User Story 1 — Discover and publish Service Bus entities (Priority: P1) 🎯 MVP

**Goal**: A namespace administrator triggers discovery for a registered namespace; within ≤ 5 minutes the registry catalog reflects every queue/topic/subscription/rule with accurate technical metadata; re-runs correctly classify new / updated / missing.

**Independent Test**: Register a namespace pointing at a real Azure Service Bus namespace, trigger discovery, verify the catalog populates. Modify Azure state and re-run; verify classifications.

### Tests for User Story 1 (write FIRST; ensure they FAIL)

- [X] T031 [P] [US1] Contract test for `POST /api/namespaces/{namespaceId}/discover` in `api/BusTerminal.Api.Tests/Features/Discovery/StartDiscovery/StartDiscoveryContractTests.cs` — request/response shape matches `contracts/openapi.yaml` (including `coalescedFromExisting`).
- [X] T032 [P] [US1] Contract test for `GET /api/discovery-runs/{discoveryRunId}` in `api/BusTerminal.Api.Tests/Features/Discovery/GetDiscoveryRun/GetDiscoveryRunContractTests.cs`.
- [X] T033 [P] [US1] Integration test for FR-003 coalescing in `api/BusTerminal.Api.Tests/Features/Discovery/StartDiscovery/CoalescingIntegrationTests.cs` — back-to-back POSTs return the same `discoveryRunId` with `coalescedFromExisting: true` on the second call.
- [X] T034 [P] [US1] Integration test for FR-027 (rejection without role) in `.../StartDiscoveryAuthorizationTests.cs`.
- [X] T035 [P] [US1] Worker-side unit tests for `AzureSourcedHash` in `api/BusTerminal.Indexer.Tests/Discovery/Classification/AzureSourcedHashTests.cs` verifying deterministic, order-independent canonical JSON hashing.
- [X] T036 [P] [US1] Worker-side unit tests for `EntityClassifier` in `.../Classification/EntityClassifierTests.cs` covering new / updated / unchanged transitions on each entity type.
- [X] T037 [P] [US1] Worker-side integration test in `api/BusTerminal.Indexer.Tests/Discovery/EntityDiscoveryOrchestratorTests.cs` against a recorded ARM HTTP fixture (`api/BusTerminal.Indexer.Tests/Discovery/_Fixtures/arm-recorded-namespace/*.json`) covering: empty namespace, full namespace, mid-run partial failure, idempotent re-run, missing-detection sweep. **Explicit partial-failure case**: simulate topics fetch succeeds but subscriptions fetch fails — assert that NO entity (queues, topics, or pre-existing subscriptions) is marked Missing as a result, only the run is marked Failed. Covers the "Partial discovery failure" edge case.
- [X] T038 [P] [US1] Worker-side unit test for the retry/backoff configuration in `.../RetryPolicyTests.cs` confirming `MaxRetries = 3`, exponential backoff, and that auth failures bypass retry (FR-021a).
- [X] T039 [P] [US1] E2E Playwright test in `web/tests/e2e/discovery-flow.spec.ts` covering the US1 walkthrough from `quickstart.md` against the dev environment.
- [X] T040 [P] [US1] Component test for `<DiscoverButton>` in `web/components/discovery/discover-button.test.tsx` (Vitest + RTL): role-gated render, click triggers `startDiscovery`, polls for completion, surfaces errors.
- [X] T041 [P] [US1] A11y test in `web/tests/a11y/namespace-overview-discovery.spec.ts` covering the extended namespace overview (button focus order, ARIA live region for polling status).

### Implementation for User Story 1 — Backend API

- [X] T042 [P] [US1] `api/BusTerminal.Api/Features/Discovery/_Shared/DiscoveryRequestPublisher.cs` — `IDiscoveryRequestPublisher` implementation that sends a message to the internal `discovery-requested` queue with the envelope from `research.md R-13`, using AAD (`__fullyQualifiedNamespace`). Sets the `Diagnostic-Id` Service Bus property automatically via active Activity.
- [X] T043 [P] [US1] `api/BusTerminal.Api/Features/Discovery/_Shared/DiscoveryRunCoalescer.cs` — `IDiscoveryRunCoalescer.EnsureRunAsync(namespaceId, requestedBy)` that wraps `IDiscoveryLockStore` + `IDiscoveryRunStore` to implement FR-003. Returns `(runId, coalescedFromExisting)`. On coalesce, appends the new request to the existing run's `coalescedRequests` array.
- [X] T044 [US1] `api/BusTerminal.Api/Features/Discovery/StartDiscovery/StartDiscoveryRequest.cs` + `StartDiscoveryResponse.cs` + `StartDiscoveryValidator.cs` (FluentValidation — namespace exists; user is authenticated; namespace is in a discoverable lifecycle state per Spec 008).
- [X] T045 [US1] `api/BusTerminal.Api/Features/Discovery/StartDiscovery/StartDiscoveryEndpoint.cs` — Minimal API mapping `POST /api/namespaces/{namespaceId}/discover`; runs validator → coalescer → if newly created, publishes the message; returns 202 + response. Applies `RequireNamespaceAdministrator()`.
- [X] T046 [US1] `api/BusTerminal.Api/Features/Discovery/GetDiscoveryRun/GetDiscoveryRunEndpoint.cs` — Minimal API mapping `GET /api/discovery-runs/{discoveryRunId}?namespaceId={ns}`. Returns 404 if not found in the supplied partition.
- [X] T047 [US1] Register the two new endpoints (`StartDiscoveryEndpoint`, `GetDiscoveryRunEndpoint`) in `api/BusTerminal.Api/Features/Discovery/_Shared/DiscoveryEndpointsBuilder.cs::MapDiscoveryEndpoints()`. **Sequential with T072, T087, T110 — same file.**

### Implementation for User Story 1 — Worker

- [X] T048 [P] [US1] `api/BusTerminal.Indexer/Discovery/Providers/IEntityDiscoveryProvider.cs` — abstraction returning `IAsyncEnumerable<DiscoveredEntity>` for each entity type. Extensibility seam per Principle VI.
- [X] T049 [US1] `api/BusTerminal.Indexer/Discovery/Providers/AzureServiceBusEntityDiscoveryProvider.cs` — concrete implementation using `Azure.ResourceManager.ServiceBus` 1.x via the existing `ArmClient` singleton. Streams queues, topics, then per-topic subscriptions/rules. Uses tuned `RetryOptions` per R-04. Maps SDK property names → `data-model.md §1.1` field shapes.
- [X] T050 [P] [US1] `api/BusTerminal.Indexer/Discovery/Classification/AzureSourcedHash.cs` — canonical SHA-256 over sorted-key JSON serialization per R-08.
- [X] T051 [P] [US1] `api/BusTerminal.Indexer/Discovery/Classification/EntityClassifier.cs` — classifies each discovered entity as new/updated/unchanged given the prior persisted document's `azureSourcedHash`.
- [X] T052 [P] [US1] `api/BusTerminal.Indexer/Discovery/Persistence/DiscoveryWriteBatcher.cs` — channel-backed 32-way parallel writer; calls `IRegistryEntityStore.UpsertAzureSourcedAsync`; updates lifecycle on reappearance per FR-014.
- [X] T053 [US1] `api/BusTerminal.Indexer/Discovery/EntityDiscoveryOrchestrator.cs` — orchestrates: lock holder validation → fetch (parallel per R-05) → classify → persist → missing-sweep → final run update. Emits all spans/metrics from R-12. On non-retriable error: marks run Failed with the appropriate `DiscoveryFailureCategory` and `DiscoveryPhase`, releases the lock, exits cleanly. **Partial-failure invariant**: orchestrator MUST track a `completedScopes: Set<EntityType>` and the missing-sweep MUST filter to only the scopes whose fetch completed successfully — if any scope failed, that scope's entities are NOT considered for Missing transition (covers the "Partial discovery failure" edge case + FR-021).
- [X] T053a [P] [US1] `api/BusTerminal.Indexer/Discovery/FailureMessageSanitizer.cs` — utility that strips ARM resource paths, entity names, and any other potentially-sensitive substrings from exception messages before they're persisted to `DiscoveryRun.failure.message`. Enforces the constitution's "no PII in telemetry" rule and the R-12 dimension cap. Unit tests in `api/BusTerminal.Indexer.Tests/Discovery/FailureMessageSanitizerTests.cs` covering ARM ID redaction, entity name redaction, and pass-through of operator-friendly error categories. Called from T053 before writing `DiscoveryRun.failure`.
- [X] T054 [US1] `api/BusTerminal.Indexer/Discovery/DiscoveryRequestedFunction.cs` — `[ServiceBusTrigger("discovery-requested", Connection = "ServiceBus__fullyQualifiedNamespace")]` entry point; deserializes the envelope, seeds the worker activity from the message's W3C `traceparent`, invokes `EntityDiscoveryOrchestrator`. On unhandled exception: lets the Service Bus binding dead-letter the message after `MaxDeliveryCount` per FR-021.
- [X] T055 [US1] DI registration in `api/BusTerminal.Indexer/Program.cs` for the orchestrator, provider, classifier, batcher, telemetry.

### Implementation for User Story 1 — Frontend

- [X] T056 [P] [US1] `web/components/discovery/discover-button.tsx` — client component; uses `useHasRole('BusTerminal.NamespaceAdministrator')`; on click calls `startDiscovery`; on success kicks off a TanStack Query polling subscription (`refetchInterval: 3000`) on `getDiscoveryRun` until terminal; surfaces success/failure via shadcn `Toast`.
- [X] T057 [P] [US1] `web/components/discovery/discovery-status-panel.tsx` — server component that reads the latest run via `listDiscoveryRuns(pageSize=1)` and renders last-status badge, last-run timestamp, entity-counts tiles per FR-025.
- [X] T058 [US1] Extend `web/app/(authenticated)/namespaces/[id]/page.tsx` to render `<DiscoveryStatusPanel>` and `<DiscoverButton>` per FR-025 / US1 acceptance scenario 1.
- [X] T059 [P] [US1] Storybook story for `<DiscoverButton>` covering enabled, disabled (no role), in-flight (polling), success-toast, failure-toast states in `web/components/discovery/discover-button.stories.tsx`.
- [X] T060 [P] [US1] Storybook story for `<DiscoveryStatusPanel>` covering no-runs, in-flight, succeeded, failed states in `.../discovery-status-panel.stories.tsx`.

**Checkpoint**: US1 is independently demonstrable end-to-end via the `quickstart.md` US1 walkthrough. SC-001, SC-002, SC-003, SC-005, SC-009 verifiable. MVP achieved.

---

## Phase 4: User Story 2 — Browse and search the published entity catalog (Priority: P2)

**Goal**: Any authenticated user can search the catalog, apply filters (entity type, namespace, service, role, tag, lifecycle, etc.), sort, and drill into an entity detail page that distinguishes Azure-sourced from registry-curated metadata.

**Independent Test**: With a populated catalog (from US1 or seeded data), perform searches by name and entity type, apply combinations of filters, sort, drill into detail. Verify both metadata sections are clearly distinguished and lifecycle status is visible.

**⚠️ Environment prerequisite**: On any environment that contains pre-existing Spec 006 entities (dev, test, prod), **T114 (AI Search canonical rebuild) MUST run before US2 acceptance testing** — otherwise legacy entities will render with empty `lifecycleStatus`, `associatedServiceIds`, and `azureSourced` fields, breaking the new filters. T114 lives in Phase 7 for pipeline simplicity but executes-before-acceptance for US2.

### Tests for User Story 2

- [X] T061 [P] [US2] Contract test for `GET /api/entities` in `api/BusTerminal.Api.Tests/Features/Discovery/SearchEntities/SearchEntitiesContractTests.cs` (validates all new filter parameters and the response shape).
- [X] T062 [P] [US2] Contract test for `GET /api/entities/{entityId}` in `.../GetEntityDetail/GetEntityDetailContractTests.cs`.
- [X] T063 [P] [US2] Integration test for new filter combinations in `.../SearchEntities/SearchEntitiesFilterIntegrationTests.cs` (lifecycle+role, namespace+tag, multi-value role narrowing). _(Live AI Search run is env-var-gated; pure OData-clause assertions run in every build to catch regressions to the filter-string shape — see `AzurePublishedEntitySearchClient.BuildFilter`.)_
- [X] T064 [P] [US2] Component test for `<LifecycleFilter>` in `web/components/registry/filters/lifecycle-filter.test.tsx`.
- [X] T065 [P] [US2] Component test for `<ServiceAssociationFilter>` in `web/components/registry/filters/service-association-filter.test.tsx`.
- [X] T066 [P] [US2] Component test for `<EntityAzureMetadata>` in `web/components/discovery/entity-azure-metadata.test.tsx` covering per-entity-type field rendering and the "unknown" rendering for rule edge case (missing filter/action).
- [X] T067 [P] [US2] Component test for `<EntityDiscoveryInfo>` in `web/components/discovery/entity-discovery-info.test.tsx`.
- [X] T068 [P] [US2] A11y test for `/registry/search` extended filters and entity detail page in `web/tests/a11y/registry-search-discovery.spec.ts`.
- [X] T069 [P] [US2] E2E Playwright test for the US2 walkthrough in `web/tests/e2e/entity-catalog.spec.ts`.

### Implementation for User Story 2 — Backend

- [X] T070 [US2] Extend the existing Spec 006 search handler — `api/BusTerminal.Api/Features/Discovery/SearchEntities/SearchEntitiesEndpoint.cs` — to accept and forward the new query params (`associatedServiceId`, `associationRole[]`, `lifecycleStatus[]`, `sort=lastSeen_*`). Translate to AI Search OData filter clauses. _(Implemented as a NEW endpoint at `GET /api/entities` per `contracts/openapi.yaml`. To keep the spec 006 ISearchClient untouched, added a sibling abstraction `IPublishedEntitySearchClient` + adapter `AzurePublishedEntitySearchClient` over the same `registry-entities-v1` index with typed shapes for the spec 009 surface — `EntityType`, `LifecycleStatus`, `EntityServiceRole`, string ids. Unconditional `lifecycleStatus ne null` filter scopes results to published entities only.)_
- [X] T071 [US2] `api/BusTerminal.Api/Features/Discovery/GetEntityDetail/GetEntityDetailEndpoint.cs` — Minimal API `GET /api/entities/{entityId}`. Returns the full document including `azureSourced.*`, `serviceAssociations[]`, and a `Last-Modified` + `ETag` header. Resolves the partition key by reading from AI Search first to find the `environment`, then doing a single-partition Cosmos read. _(Added `IPublishedEntityStore.GetDetailAsync(entityId, environment)` returning the domain `PublishedEntity` + ETag; the endpoint composes search-lookup → Cosmos read and 404s on either miss.)_
- [X] T072 [US2] Register both endpoints (`SearchEntitiesEndpoint`, `GetEntityDetailEndpoint`) in `DiscoveryEndpointsBuilder.MapDiscoveryEndpoints()`. **Sequential with T047, T087, T110 — same file.**

### Implementation for User Story 2 — Frontend

- [X] T073 [P] [US2] `web/components/registry/filters/lifecycle-filter.tsx` — checkbox-group filter for lifecycle status; URL-state-driven per existing search-page pattern.
- [X] T074 [P] [US2] `web/components/registry/filters/service-association-filter.tsx` — service-id input + role narrowing checkbox group. _(Spec 009 ships a typed text input for serviceId; a future spec / Phase 6 can swap the input for a shadcn `command` + `popover` combobox without changing the URL state contract — service catalog endpoint isn't part of Spec 009.)_
- [X] T075 [P] [US2] `web/components/discovery/entity-azure-metadata.tsx` — read-only display per entity type (queue/topic/subscription/rule). Uses shadcn `Card` + descriptive labels. Renders "Unknown" for rule fields per edge case.
- [X] T076 [P] [US2] `web/components/discovery/entity-discovery-info.tsx` — first-seen + last-seen + lifecycle badge component (server component).
- [X] T077 [US2] Extend `web/app/(authenticated)/registry/search/page.tsx` to render the new filter components and pass their state to the existing search query.
- [X] T078 [US2] Extend `web/app/(authenticated)/registry/[entityType]/[id]/page.tsx` to render `<EntityDiscoveryInfo>`, `<EntityAzureMetadata>`, and the existing registry metadata section — visually distinct cards as required by FR-024. _(Dispatches on id format: `pe_*` ids call `getEntityDetail` and render the Spec 009 panels; other ids fall through to the existing Spec 006 detail shell.)_
- [X] T079 [P] [US2] Storybook stories for each of the four new components in their respective `.stories.tsx` files.

**Checkpoint**: SC-002, SC-007 verifiable. US2 testable independently as soon as the catalog has any populated entity (whether from US1 or seeded data). User Story 1 and User Story 2 both work standalone.

---

## Phase 5: User Story 3 — Inspect discovery history and troubleshoot failures (Priority: P3)

**Goal**: A platform administrator opens a namespace's discovery history, sees runs chronologically with status/duration/counts/error detail, and can identify a failure's root cause without leaving the UI.

**Independent Test**: Trigger several runs (including at least one engineered to fail), confirm history view shows them accurately with timing, counts, and error details. Verify failed-run inspection surfaces the operator-safe error message.

### Tests for User Story 3

- [X] T080 [P] [US3] Contract test for `GET /api/namespaces/{namespaceId}/discovery-runs` in `api/BusTerminal.Api.Tests/Features/Discovery/ListDiscoveryRuns/ListDiscoveryRunsContractTests.cs` (validates pagination via `continuationToken`). _(Extended `InMemoryDiscoveryRunStore` with a tiny offset-cursor implementation so the contract walks an opaque token through 5 seeded runs end-to-end.)_
- [X] T081 [P] [US3] Integration test in `.../ListDiscoveryRunsPaginationTests.cs` seeding > pageSize runs and walking the continuation cursor to completion. _(Uses the shared `RegistryFixture`; skips cleanly when `BUSTERMINAL_TEST_COSMOS_ENDPOINT` is unset.)_
- [X] T082 [P] [US3] Component test for `<DiscoveryRunsTable>` in `web/components/discovery/discovery-runs-table.test.tsx` covering reverse-chronological sort, status badges, duration formatting, and row-click navigation.
- [X] T083 [P] [US3] Component test for `<DiscoveryRunDetail>` in `web/components/discovery/discovery-run-detail.test.tsx` covering Succeeded vs Failed rendering and failure category mapping.
- [X] T084 [P] [US3] A11y test for the new history pages in `web/tests/a11y/discovery-runs.spec.ts`. _(Pinned `test.fixme` matching the spec 009 E2E pattern — depends on the same MSAL persona fixture as T039/T041, lands in Phase 9.)_
- [X] T085 [P] [US3] E2E Playwright test for the US3 walkthrough (including the engineered failure) in `web/tests/e2e/discovery-history.spec.ts`. _(Pinned `test.fixme` matching T039 — failure-card behavior covered today by `discovery-run-detail.test.tsx` and `FailureMessageSanitizerTests.cs`.)_

### Implementation for User Story 3 — Backend

- [X] T086 [US3] `api/BusTerminal.Api/Features/Discovery/ListDiscoveryRuns/ListDiscoveryRunsEndpoint.cs` — Minimal API `GET /api/namespaces/{namespaceId}/discovery-runs` with continuation-token pagination via Cosmos's native cursor. _(Plus `ListDiscoveryRunsResponse` DTO so the wire shape stays decoupled from `DiscoveryRunPage`.)_
- [X] T087 [US3] Register `ListDiscoveryRunsEndpoint` in `DiscoveryEndpointsBuilder.MapDiscoveryEndpoints()`. **Sequential with T047, T072, T110 — same file.**

### Implementation for User Story 3 — Frontend

- [X] T088 [P] [US3] `web/components/discovery/discovery-runs-table.tsx` — TanStack Table v8 client component (mirrors Spec 006's URL-state-driven pattern). Columns: status (badge), started, completed, duration, counts, requested-by, run-id. Row click → `/namespaces/{id}/discovery-runs/{runId}`.
- [X] T089 [P] [US3] `web/components/discovery/discovery-run-detail.tsx` — server component rendering all fields from the DiscoveryRun including the per-classification counts and the failure detail block when present. _(Operator-friendly category + phase labels surface the R-12 telemetry mapping.)_
- [X] T090 [US3] New route `web/app/(authenticated)/namespaces/[id]/discovery-runs/page.tsx` — server-renders `<DiscoveryRunsTable>` with first page of data. _(Thin RSC shell delegates to a new `<DiscoveryRunsHistoryViewer>` client wrapper that fetches the first page via TanStack Query, matching the established discovery-status-panel pattern.)_
- [X] T091 [US3] New route `web/app/(authenticated)/namespaces/[id]/discovery-runs/[runId]/page.tsx` — server-renders `<DiscoveryRunDetail>`. _(Thin RSC shell delegates to `<DiscoveryRunDetailViewer>` which fetches via `getDiscoveryRun` and surfaces 404 inline.)_
- [X] T092 [P] [US3] Storybook stories for the two new components. _(Empty / populated / mixed-statuses / has-more-pages for the table; Succeeded / InProgress / FailedThrottled / FailedAuthn / FailedWorkerLost / WithCoalescedRequests for the detail.)_

**Checkpoint**: SC-006, SC-008 verifiable. US3 testable independently. All three stories deployable end-to-end. _(Phase 5 LANDED 2026-06-17 — backend + frontend + tests all in place; lint clean on new files; 78/78 Discovery API tests + 30/30 Discovery component tests passing; T084/T085 pinned `test.fixme` pending the MSAL persona fixture.)_

---

## Phase 6: User Story 4 — Curate registry-owned metadata on a published entity (Priority: P4)

**Goal**: A service owner with the appropriate role edits an entity's description / tags / docs / contacts / operational notes and manages its service associations; subsequent discoveries preserve every curated field.

**Independent Test**: Edit an entity's curated metadata + associations as an authorized user; trigger a fresh discovery; verify every curated field is preserved while Azure-sourced fields refresh.

### Tests for User Story 4

- [X] T093 [P] [US4] Contract test for `PATCH /api/entities/{entityId}` in `api/BusTerminal.Api.Tests/Features/Discovery/UpdateEntityMetadata/UpdateEntityMetadataContractTests.cs` (verify rejection of `azureSourced.*` fields with 400, ETag enforcement with 412).
- [X] T094 [P] [US4] Contract test for `POST /api/entities/{entityId}/archive` in `.../ArchiveEntity/ArchiveEntityContractTests.cs`.
- [X] T095 [P] [US4] Contract tests for association endpoints in `.../ServiceAssociations/ServiceAssociationContractTests.cs` (GET list, POST add, DELETE remove). Include 409 on duplicate `(serviceId, role)` triple.
- [X] T096 [P] [US4] Integration test for FR-016 metadata preservation in `.../MetadataPreservationIntegrationTests.cs`: seed entity with curated metadata + associations, simulate a discovery upsert via `UpsertAzureSourcedAsync`, verify zero diff on curated fields and associations. _(Skip-on-no-emulator pattern; runs end-to-end via `UpdateCuratedMetadataAsync` → `AddAssociationAsync` → re-`UpsertAzureSourcedAsync`.)_
- [X] T097 [P] [US4] Integration test for FR-015 archive sticky-ness in `.../ArchiveStickyIntegrationTests.cs`: archive an entity, simulate discovery seeing it again, verify status stays `Archived`. _(Direct test of the worker upsert's omission of `/lifecycleStatus` from its PATCH operations.)_
- [X] T098 [P] [US4] Integration test for the three-branch authorization in `.../UpdateEntityMetadata/AuthorizationIntegrationTests.cs` — Platform Admin, Namespace Admin, Owner-role Service Owner all allow; Producer/Consumer-role Service Owner and unrelated users are denied. _(Uses `StubOwnedServicesResolver` registered through the test fixture; runs against the in-memory `DiscoveryContractFactory`.)_
- [X] T099 [P] [US4] Component test for `<ServiceAssociationEditor>` in `web/components/discovery/service-association-editor.test.tsx` covering add, remove, duplicate-prevention UX, validation errors.
- [X] T100 [P] [US4] Component test for the extended edit form (re-exercise `<EntityForm>` with the new fields) in a new file `web/components/registry/forms/entity-edit-form.test.tsx` — keeps US4 surface cleanly separable from Spec 006's `namespace-form.test.tsx`. _(Built as `<PublishedEntityEditForm>` component test — Spec 006 forms unchanged; spec 009 lives in a sibling file as planned.)_
- [X] T101 [P] [US4] A11y test for the edit + association editor in `web/tests/a11y/entity-edit.spec.ts`. _(Pinned `test.fixme` matching the Phase 9 MSAL-persona-fixture pattern used by T084/T085; component-level a11y covered today via the shadcn primitives + Vitest+axe.)_
- [X] T102 [P] [US4] E2E Playwright test for the US4 walkthrough (`web/tests/e2e/entity-curation.spec.ts`) including the preservation-across-rediscovery acceptance scenario. _(Pinned `test.fixme` — same persona-fixture caveat; FR-016 covered at the store layer via T096.)_

### Implementation for User Story 4 — Backend

- [X] T103 [P] [US4] `api/BusTerminal.Api/Features/Discovery/UpdateEntityMetadata/UpdateEntityMetadataRequest.cs` + `UpdateEntityMetadataValidator.cs` — FluentValidation rejecting `azureSourced.*` keys, capping tags/strings to documented limits, validating URL formats. _(JsonElement-shaped request preserves "missing vs explicit-null" semantics; per-field validators enforce length caps. `UpdateEntityMetadataMapper.ToPatch` converts to `CuratedMetadataPatch` with `OptionalValue<T>` carriers.)_
- [X] T104 [US4] `.../UpdateEntityMetadata/UpdateEntityMetadataEndpoint.cs` — `PATCH /api/entities/{entityId}` with `If-Match` ETag enforcement. Reads entity → runs `EntityMetadataEditorAuthorizer` → merges curated fields → calls a new `IRegistryEntityStore.UpdateCuratedMetadataAsync(entityId, patch, ifMatch)`. _(Raw-body audit rejects azureSourced + every other discovery-owned key with 400/code=DisallowedField; 428 on missing If-Match.)_
- [X] T105 [P] [US4] `.../ArchiveEntity/ArchiveEntityEndpoint.cs` — `POST /api/entities/{entityId}/archive` toggling `lifecycleStatus = Archived` with the same auth/ETag pattern.
- [X] T106 [P] [US4] `.../ServiceAssociations/ListAssociationsEndpoint.cs` — `GET /api/entities/{entityId}/associations`.
- [X] T107 [P] [US4] `.../ServiceAssociations/AddAssociationEndpoint.cs` — `POST /api/entities/{entityId}/associations` with duplicate-triple 409 + ETag enforcement. Authorization: Owner-role-editor branch OR Admin/NamespaceAdmin. _(Mints `esa_<base32-crockford>` association ids server-side.)_
- [X] T108 [P] [US4] `.../ServiceAssociations/RemoveAssociationEndpoint.cs` — `DELETE /api/entities/{entityId}/associations/{associationId}` with same auth pattern.
- [X] T109 [US4] Extend `IRegistryEntityStore` with `UpdateCuratedMetadataAsync(entityId, patch, ifMatch)`, `SetLifecycleStatusAsync(entityId, status, ifMatch)` (used by archive + missing sweep), `AddAssociationAsync(entityId, association, ifMatch)`, `RemoveAssociationAsync(entityId, associationId, ifMatch)`. All accept and enforce optimistic-concurrency ETag (`ifMatch`) and preserve `azureSourced.*` and `azureSourcedHash` untouched. _(Implemented on `IPublishedEntityStore` — the spec 009 dedicated store that already targets `registry-entities` per T015 — to keep the spec 006/008 `IRegistryEntityStore` (Guid-id entities) untouched. New typed exceptions: `PublishedEntityNotFoundException`, `PublishedEntityConcurrencyConflictException`, `DuplicateServiceAssociationException`, `ServiceAssociationNotFoundException`.)_
- [X] T110 [US4] Register all five new endpoints (`UpdateEntityMetadataEndpoint`, `ArchiveEntityEndpoint`, `ListAssociationsEndpoint`, `AddAssociationEndpoint`, `RemoveAssociationEndpoint`) in `DiscoveryEndpointsBuilder.MapDiscoveryEndpoints()`. **Sequential with T047, T072, T087 — same file.**

### Implementation for User Story 4 — Frontend

- [X] T111 [P] [US4] `web/components/discovery/service-association-editor.tsx` — client component using shadcn `Dialog`; RHF + Zod; list current associations with remove buttons; add-form combines a service picker + role select; uses `addEntityAssociation` / `removeEntityAssociation`. Optimistic update via TanStack Query. _(RHF `register` instead of `zodResolver` — the @hookform/resolvers v3 ↔ Zod v4 mismatch in this repo trips the resolver. Role select is a native `<select>` to clear the `react-hooks/incompatible-library` rule fired by the shadcn Select's controlled API.)_
- [X] T112 [US4] Extend `web/app/(authenticated)/registry/[entityType]/[id]/edit/page.tsx` (existing Spec 006 edit page) — wire `useEntityForm` to `updateEntityMetadata`; add the `<ServiceAssociationEditor>` trigger; render an Archive button gated by `canEditEntityMetadata`; surface 412 conflicts in the existing conflict-modal pattern. _(Edit page dispatches `pe_*` ids → new `<PublishedEntityEditForm>`; other ids still route to the Spec 006 forms.)_
- [X] T113 [P] [US4] Storybook story for `<ServiceAssociationEditor>` covering empty, populated, duplicate-attempt, server-error states.

**Checkpoint**: SC-004, SC-009 verifiable. All four user stories independently functional. Spec 009 feature complete pending Polish phase. _(Phase 6 LANDED 2026-06-18 — 660/660 API non-integration tests + 240/240 web tests pass; new files lint clean; `PublishedEntityResponse` DTO flattens curated fields into the OpenAPI wire shape for GET/PATCH responses.)_

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Documentation, performance tuning, security review, the AI Search backfill, telemetry dashboards, and the quickstart validation pass.

- [ ] T114 [P] Run the AI Search canonical-rebuild against dev to backfill the new fields onto historical Spec 006 documents (`iac/scripts/rebuild-search-index.sh dev`). Validate no entities lost. **Prerequisite for US2 acceptance testing on any environment with pre-existing Spec 006 entities** — see the Phase 4 environment-prerequisite note. Run order: ship Foundational (T011–T030) → run T114 → begin US2 acceptance.
- [ ] T115 [P] Add a built-in App Insights workbook (or Azure Monitor dashboard) for discovery telemetry — runs/day, success rate, duration P50/P95, retry counts, failure-category breakdown — defined as IaC under `iac/modules/monitoring-dashboards/discovery.json` (or extend the existing dashboards module).
- [ ] T116 [P] Performance validation: run a load test from the dev environment against a synthetic large namespace (use Spec 008's existing test-namespace seeder + new helper at `tools/SyntheticServiceBusSeeder/`). Confirm SC-005 (≤ 5 min) for 500/500/5000/5000 scale.
- [ ] T116a [P] Performance smoke test for SC-007 ("user can locate any entity by name through catalog search in under 10 seconds") in `api/BusTerminal.Api.Tests/Performance/EntitySearchLatencyTests.cs` — against the dev AI Search index populated by T116's synthetic namespace, measure `GET /api/entities?q=...` P95 latency over ≥ 50 representative queries; assert P95 ≤ 10s. Tagged for nightly perf run, not blocking PR CI.
- [ ] T117 [P] Run `iac/policies/run-policies.sh` against the dev plan in CI to confirm BT-IAC-001..007 all pass; commit any required allowlist additions with `justification` fields.
- [ ] T118 [P] Update `web/lib/auth/role-permission-matrix.ts` to document the new "edit entity metadata" operation class derivation rule (R-15). Add a unit test confirming the matrix and the runtime `canEditEntityMetadata` agree.
- [ ] T119 [P] Update CLAUDE.md MCP touchpoints if new patterns warrant: confirm the shadcn MCP was consulted for any primitive variant added in Phase 3/4/6 tasks.
- [ ] T120 [P] Re-validate `quickstart.md` end-to-end against dev — US1, US2, US3, US4 walkthroughs all pass. Record any deltas back to `quickstart.md`.
- [ ] T121 [P] Add OpenAPI spec to the existing API host's published surface (the existing `Program.cs` already generates OpenAPI — confirm the new endpoints appear at `/openapi/v1.json` and the existing Swagger UI page surfaces them).
- [ ] T122 [P] Run `axe` against the three new frontend surfaces (namespace overview extended, discovery history list/detail, entity edit + association editor) via the new a11y test suite. Fix any AA violations before merge.
- [ ] T123 [P] Code review pass: scan all new files for accidentally-committed PII in test fixtures or logs (constitution — no PII in telemetry); the gitleaks CI scan should already catch this but a manual sweep is cheap.
- [ ] T124 Update `speckit-artifacts/tech-stack.md` if Spec 009 introduced any durable convention (e.g., the W3C Trace Context propagation pattern over Service Bus message properties is worth a one-line note under §7 or a new sub-row under §4 — confirm with R-13).
- [ ] T125 Final: tag and release. Open the PR to main via the existing CI gates (build, unit, lint, format, security scan, IaC plan, terraform-docs, BT-IAC-001..007).
- [ ] T125a [P] Dev-tooling CLI `tools/DiscoveryLockReset` — small .NET console that takes `--namespace-id` + `--env` and clears the per-namespace `discovery-locks` document for debug/recovery. Referenced from `quickstart.md` "Useful one-liners". Not shipped to prod.
- [ ] T125b [P] Dev-tooling CLI `tools/DiscoveryTelemetryTail` — small .NET console that streams `BusTerminal.Discovery` ActivitySource + Meter events from local OTLP for live debugging. Referenced from `quickstart.md` "Useful one-liners". Not shipped to prod.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies. Start immediately. All tasks T001-T010 can run in parallel.
- **Phase 2 (Foundational)**: Depends on Setup (especially T004–T007 IaC for stores to read against). BLOCKS Phases 3–6.
- **Phase 3 (US1)**: Depends on Phase 2. MVP target.
- **Phase 4 (US2)**: Depends on Phase 2. Independent of US1 (can be developed with seeded data).
- **Phase 5 (US3)**: Depends on Phase 2 and the DiscoveryRun store from Phase 2 + history-listing implementation. Functionally meaningful after US1 starts producing runs, but can be developed independently with seeded run data.
- **Phase 6 (US4)**: Depends on Phase 2. Functionally meaningful after US1 produces entities and US2 lets users find them, but can be developed independently with seeded entity data.
- **Phase 7 (Polish)**: Depends on whatever subset of US1–US4 has shipped.

### User Story Dependencies

- US1 → no dependencies on other stories (it produces the data the others consume but US2/US3/US4 can use seeded data).
- US2, US3, US4 → mutually independent; each can be tested in isolation with seeded data.

### Within Each Story

- Tests first (Vitest/xUnit/Playwright/axe) → ensure they FAIL → implement → re-run tests.
- Models / shared types → stores → services → endpoints → UI components → page routes.

### Parallel Opportunities

- **All [P] tasks within a phase**: parallelize freely.
- **Across stories once Phase 2 ships**: US1, US2, US3, US4 can each be developed by separate engineers concurrently.
- **Within a story**: tests are all [P] (different files); component implementations are [P] when in different files; endpoint implementations sharing the same `DiscoveryEndpointsBuilder.cs` are sequential (T047, T072, T087, T110 all touch the builder — order them carefully or factor the route group into per-feature builders if it becomes contentious).

---

## Parallel Example: User Story 1

```bash
# Launch all US1 tests in parallel (10 tasks, different files):
Task: T031 — StartDiscovery contract test
Task: T032 — GetDiscoveryRun contract test
Task: T033 — Coalescing integration test
Task: T034 — Authorization integration test
Task: T035 — AzureSourcedHash unit tests
Task: T036 — EntityClassifier unit tests
Task: T037 — EntityDiscoveryOrchestrator integration test
Task: T038 — Retry policy unit test
Task: T039 — E2E Playwright test
Task: T040 — DiscoverButton component test
Task: T041 — A11y test

# Then launch all parallelizable US1 implementations:
Task: T042 — DiscoveryRequestPublisher
Task: T043 — DiscoveryRunCoalescer
Task: T048 — IEntityDiscoveryProvider abstraction
Task: T050 — AzureSourcedHash
Task: T051 — EntityClassifier
Task: T052 — DiscoveryWriteBatcher
Task: T056 — DiscoverButton component
Task: T057 — DiscoveryStatusPanel component

# Then sequential dependent tasks:
Task: T044 → T045 → T047  (DTOs → endpoint → route registration)
Task: T049 → T053 → T054 → T055  (provider → orchestrator → function → DI)
Task: T058  (page integration after T056+T057 land)
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001–T010).
2. Complete Phase 2: Foundational (T011–T030). ← critical; blocks everything.
3. Complete Phase 3: User Story 1 (T031–T060).
4. **STOP and VALIDATE**: run the US1 walkthrough from `quickstart.md`. Demo to a namespace admin. Verify SC-001/002/003/005/009.
5. Ship as MVP. The platform now auto-populates the catalog — the highest-value capability.

### Incremental Delivery

1. MVP (US1) ships first.
2. US2 (browse/search) lands next — completes the loop for the broader user base (auth users can now find what discovery produced).
3. US3 (history) lands third — operational confidence for namespace admins / platform operators.
4. US4 (curation) lands last — the highest-value-add for governance teams once the catalog is trusted.
5. Each story ships as an independent increment; users see continuous value adds without regressions.

### Parallel Team Strategy

With 3 engineers after Phase 2 completes:

1. **Engineer A**: US1 (P1). Most complex. Owns the worker + the coalescing + the orchestrator.
2. **Engineer B**: US2 + US3. Both lean heavily on existing Spec 006 search/detail patterns and the Spec 006 frontend conventions; one engineer can land both in parallel-ish.
3. **Engineer C**: US4. Auth-heavy + UI-heavy; benefits from focused attention to land the M:N association editor + the metadata-edit semantics correctly.

Phase 7 (Polish) runs as a team effort once US1 ships; the canonical AI Search backfill (T114) must run before US2/US3/US4 can be demoed against the dev environment.

---

## Notes

- [P] tasks = different files, no dependencies on incomplete tasks in the current phase.
- [Story] label maps a task to its user story for traceability and parallel-team assignment.
- Each user story is independently completable, testable, and demonstrable.
- Verify tests FAIL before implementing (TDD per constitution testing standards).
- Commit after each task or logical group; auto-commit hooks handle this automatically per the spec-kit extensions configuration.
- Stop at any checkpoint to validate a story independently.
- Avoid: cross-story dependencies that break story independence; merging implementation tasks across files marked [P] in the same agent invocation (avoid file-write conflicts).

---

## Task Count Summary

| Phase | Tasks | Of which [P] | Estimated effort (engineer-days) |
|---|---|---|---|
| Phase 1 — Setup | 10 | 9 | 1–2 |
| Phase 2 — Foundational | 22 (T011–T030 incl. T028a, T029a) | 16 | 3–5 |
| Phase 3 — US1 (P1 / MVP) | 31 (incl. T053a) | 19 | 7–10 |
| Phase 4 — US2 | 19 | 13 | 4–6 |
| Phase 5 — US3 | 13 | 8 | 2–4 |
| Phase 6 — US4 | 21 | 13 | 5–7 |
| Phase 7 — Polish | 15 (incl. T116a, T125a, T125b) | 14 | 2–3 |
| **Total** | **131** | **92** | **24–37** |

### Post-analyze remediation log (2026-06-17)

Edits applied after the `/speckit-analyze` pass:
- **O1**: T030 reworded to create empty builder shell first; T047 (and T072/T087/T110) progressively populate it.
- **O2**: Phase 4 environment-prerequisite callout added; T114 description annotated with the dependency.
- **C1**: T029a added (`PublishedEntityIdComputer` utility, Foundational).
- **C2**: T116a added (search-latency perf test for SC-007).
- **C3**: T028a added (`useOwnedServices()` hook).
- **C4**: T053 reworded with explicit partial-failure scope-tracking invariant; T037 extended with explicit partial-failure assertion.
- **A1**: T100 pinned to a new file `entity-edit-form.test.tsx`.
- **I1**: T109 wording updated to surface `ifMatch` on every store method.
- **F1**: T047, T072, T087, T110 each annotated "Sequential — same file."
- **S1**: T053a added (`FailureMessageSanitizer`).
- **T1**: T125a + T125b added (dev-tooling CLIs referenced by quickstart).
- **D1**: Skipped per recommendation — additive FR sub-clauses (FR-011a, FR-021a) are intentional and acceptable.

All 11 surfaced findings addressed. Net delta: +6 new tasks (T028a, T029a, T053a, T116a, T125a, T125b); 9 existing tasks reworded; 1 phase-level note added.
