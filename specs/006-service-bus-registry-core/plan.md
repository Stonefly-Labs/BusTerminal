# Implementation Plan: Service Bus Registry Core

**Branch**: `feature/006-service-bus-registry-core` | **Date**: 2026-06-01 | **Spec**: [`spec.md`](./spec.md)

**Input**: Feature specification from `/specs/006-service-bus-registry-core/spec.md`

## Summary

Spec 006 ships the **first feature-complete product slice** of BusTerminal: a manually-populated registry of Azure Service Bus assets (Namespace, Queue, Topic, Subscription, Rule) with CRUD, environment-aware browse, full-text search, parent/child traversal, and an audit trail. The slice adds a registry-specific persistence layer (two new Cosmos containers — `registry-entities` partitioned by `/environment` and `registry-audit` partitioned by `/entityId`), an AI Search index populated by a **Cosmos DB change-feed → Azure Functions → AI Search upsert** pipeline (containerized Functions deployed on the existing Container Apps Environment per the constitution), a Minimal-API CRUD surface on the existing `BusTerminal.Api` project (new vertical slice under `Features/Registry/`), and a Next.js App Router experience under `web/app/(authenticated)/registry/` that composes the spec-001 brand primitives (`namespace-card`, `queue-card`, `topic-card`, `subscription-card`, etc. — already shipped) into a hierarchical explorer, detail pages, search, and create/edit forms. ETag-based optimistic concurrency surfaces a refresh-or-overwrite modal (FR-020). Tags are free-form key/value pairs (FR-002 + FR-023). Deletion is hard delete blocked-when-children (FR-009, FR-013). Authorization is "any authenticated tenant user reads and writes" (FR-037 — recorded as a documented deviation from the spec-003 role-permission matrix in Complexity Tracking). The slice consumes the spec-005 infrastructure baseline outputs without modification and adds incremental IaC: registry containers on the existing Cosmos account, an AI Search index definition + indexer Function app + workload UAMI role-assignment extensions, plus diagnostic settings on the new resources via the `iac/modules/diagnostic-settings` wrapper.

## Technical Context

**Language/Version**:
- Backend: **.NET 10 / C#** (matches existing `api/BusTerminal.Api`). New event-driven indexer is a **containerized Azure Functions** project (`api/BusTerminal.Indexer`) on .NET 10 isolated worker — the newest native Azure Functions for Container Apps hosting per the constitution.
- Frontend: **TypeScript strict** on **Next.js 16.x App Router** + **React 19** (matches existing `web/`).
- IaC: **OpenTofu ≥ 1.11** (matches existing `iac/`).

**Primary Dependencies**:
- Backend (additions to `BusTerminal.Api.csproj`):
  - `Microsoft.Azure.Cosmos 3.60.0` — already pinned; new code reuses the spec-004 `CosmosClientFactory` and `AzureCredentialFactory`.
  - `FluentValidation 11.x` — **new** dependency. Spec-006 source artifact explicitly calls for FluentValidation; aligns with the spec-004 `IValidationEngine` pattern (different library, different concern: spec-004 validates the canonical domain at write time, spec-006 validates HTTP request payloads at the API boundary). Pin TBD in research §1.
  - `Azure.Messaging.ServiceBus 7.x` — **not used in this slice**. The change-feed pattern bypasses Service Bus to keep the pipeline minimal (research §3).
- Backend (new `BusTerminal.Indexer` project):
  - `Microsoft.Azure.Functions.Worker 2.x` (isolated worker host).
  - `Microsoft.Azure.Functions.Worker.Extensions.CosmosDB` — Cosmos change-feed trigger.
  - `Azure.Search.Documents 11.x` — AI Search SDK; `DefaultAzureCredential` for AAD ingestion.
  - `Azure.Identity 1.21.0` — shared with `BusTerminal.Api`.
  - `Azure.Monitor.OpenTelemetry.AspNetCore` — Functions OTel adapter; shared App Insights with the API.
- Frontend (additions to `web/package.json`):
  - `@tanstack/react-query ^5` — **new** dependency. Used **only** for client-interactive flows (search box, create/edit forms with conflict handling, mutation retries). RSC + server fetch remains the default for explorer browse + detail pages. Decision recorded in research §6. Tech-stack reference (`speckit-artifacts/tech-stack.md` §2) must be updated as a follow-up to add TanStack Query to the approved stack.
  - All other frontend packages already present: `@hookform/resolvers`, `react-hook-form`, `zod`, `@tanstack/react-table`, `lucide-react`, `next-themes`, `clsx`, `class-variance-authority`, `tailwind-merge`, `@radix-ui/*`, `@azure/msal-browser`/`msal-react`, `@microsoft/applicationinsights-*`, `framer-motion`, `sonner`, `cmdk`.
- IaC (additions to `iac/modules/`):
  - **New** module `iac/modules/cosmos-registry-store` — provisions the two registry containers (`registry-entities` PK `/environment`, `registry-audit` PK `/entityId`) on the existing Cosmos canonical-store database; mirrors the pattern of the existing `iac/modules/cosmos-canonical-store`.
  - **New** module `iac/modules/ai-search-index` — declares the registry search index via `azapi` provider (AVM does not currently cover AI Search index definitions; `azurerm` lacks an index resource as of provider v4.x — research §5 confirms).
  - **New** module `iac/modules/functions-container-app` — wraps `azurerm_container_app` configured for the **newest native Azure Functions for Container Apps hosting** (i.e., `Microsoft.Web/sites?kind=functionapp,linux,container,azurecontainerapps` via `azurerm_function_app` provisioned onto the existing Container Apps Environment, OR the `azurerm_container_app` direct-functions hosting model — the exact resource choice is finalized in research §4 after consulting Microsoft Learn MCP).

**Storage**:
- **Cosmos DB**: existing dev account (`bt-dev-cosmos-*`) from spec 004 + 005. New containers on the existing `canonical` database: `registry-entities` (PK `/environment`, autoscale RU per the canonical-store pattern) and `registry-audit` (PK `/entityId`, append-only, autoscale RU lower band). The existing `resources` and `change-events` containers from spec 004 are **untouched** — spec 006's registry is a parallel data plane that reuses the account but not the schemas (rationale in research §2).
- **Azure AI Search**: existing `basic`-SKU service from spec 005. New index `registry-entities-v1`. No second service.
- **Audit retrieval**: audit container queried by `/entityId = X ORDER BY timestamp DESC LIMIT N` for entity-scoped panels (FR-033). No separate audit search index in this slice.

**Testing**:
- **Backend unit / integration**: `xUnit` (existing) + the spec-004 `CosmosFixture` pattern for integration against a real dev Cosmos account (no emulator dependency — the dev account is reachable per the spec-005 quickstart). New `RegistryFixture` collects the registry containers + indexer offsets so test isolation is partition-scoped (PK = test-id prefix).
- **API contract tests**: assert OpenAPI document conformance + canonical error shapes (RFC 7807 + the spec-006 conflict response extension defined in `contracts/conflict-response.schema.json`).
- **Indexer integration tests**: Functions integration via `Microsoft.Azure.Functions.Worker.Testing` (or the manual TestHost pattern) — verifies a Cosmos document insertion produces an AI Search upsert within the SC-005 budget (5s at p95).
- **Frontend**:
  - **Vitest + React Testing Library** for component and hook tests (existing).
  - **Playwright** for E2E (existing).
  - **axe-playwright** for a11y gates on the registry routes (existing pattern).
  - New test suites: `tests/registry/{explorer,detail,search,create,edit,conflict}.e2e.spec.ts`.

**Target Platform**:
- Backend: Linux container on Azure Container Apps (existing dev env); .NET 10 runtime.
- Indexer: containerized Azure Function on the same Container Apps Environment; .NET 10 isolated worker; Cosmos change-feed trigger; managed-identity AAD against AI Search and Cosmos.
- Frontend: SSR-capable Next.js 16 container on Azure Container Apps (existing); React 19; browser baseline = last two majors of Chrome/Edge/Firefox/Safari (desktop) + iPadOS Safari + Android Chrome.

**Project Type**: Web application (Next.js frontend + ASP.NET Core Minimal API backend + .NET Azure Functions event-driven indexer + OpenTofu IaC). Modular monolith: backend remains a single deployable; the indexer is a *separate process* because it is event-driven, not because it carries different domain concerns (constitution §Modular Monolith First — event-driven extraction is an explicit allowed boundary).

**Performance Goals** (binding — derived from FR-043, FR-044, FR-045, SC-002, SC-003, SC-004, SC-005):
- Search p95 < 1s under expected load.
- Detail page load p95 < 500ms under expected load.
- CRUD API p95 < 1s under expected load.
- Index lag p95 < 5s under normal indexing-pipeline conditions.
- Frontend Core Web Vitals on the explorer + detail screens: LCP ≤ 2.5s, INP ≤ 200ms, CLS ≤ 0.1 (tech-stack §2 — already binding).

**Constraints**:
- Constitution: Azure-first, Minimal APIs (not Controllers), OpenTofu only, Vertical Slice Architecture, managed identity preferred, W3C Trace Context on every UI-originated HTTP call, dark-mode primary, RTL-safe via logical CSS properties, no second design system, no CSS-in-JS, no PII in telemetry.
- Spec-005 carryover: no destructive changes to the dev composition; new resources added incrementally; CD pipeline UAMI's RBAC-Admin condition allowlist already permits the four spec-005 FR-033 role GUIDs (Cosmos Data Contributor, SB Data Sender, SB Data Receiver, Search Index Data Contributor) per [[project_spec005_bootstrap_gate]] — spec 006 reuses this and does not add new role GUIDs to the condition.
- Spec-004 carryover: existing `resources` + `change-events` containers untouched; spec-004's `CosmosClientFactory`, `AzureCredentialFactory`, `JsonResourceSerializer` patterns are reused as building blocks (not as base classes — registry entities are NOT `Resource` subtypes; rationale in research §2).
- Spec-003 carryover: backend authentication via `Microsoft.Identity.Web` JWT bearer is unchanged; the new registry endpoints declare `[Authorize]` (require an authenticated identity) but do NOT bind to a role policy — this is the FR-037 deviation recorded in Complexity Tracking.
- Spec-001 carryover: the registry UI composes the brand primitives already in `web/components/domain/*` (namespace-card, queue-card, topic-card, subscription-card, queue-row, etc.) and the layout primitives in `web/components/app-shell/`, `web/components/navigation/`, `web/components/layout/`. No new primitive families are introduced.
- Frontend observability: continue routing through the existing pluggable adapter (`web/lib/observability/`); W3C Trace Context propagation on every registry API call is mandatory (FR-042 + tech-stack §4).

**Scale/Scope**:
- Backend: ~5 new vertical slices (`Features/Registry/{Namespaces,Queues,Topics,Subscriptions,Rules}`) + 1 shared slice (`Features/Registry/_Shared` for the canonical entity contract, the conflict response, the search proxy, and the audit endpoint). Estimated 2500–4000 LOC of C# + tests.
- Indexer: ~600–1000 LOC of C# (a single Functions project hosting a Cosmos change-feed trigger + an AI Search upsert client + retry/poison handling).
- Frontend: ~6 new App Router segments under `web/app/(authenticated)/registry/`, ~25 new domain components composing existing primitives, ~5 React Hook Form + Zod form modules, ~5 new data-fetching modules. Estimated 4000–7000 LOC of TS/TSX + tests.
- IaC: ~3 new modules + extensions to `iac/environments/dev/main.tf` to compose them. Net new Azure resources in dev: 2 Cosmos containers, 1 AI Search index, 1 Functions container app + its UAMI role assignments + diagnostic settings. Estimated +400 LOC of HCL.
- Total: ~10k–15k LOC including tests.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Constitution version: 1.0.0 (`.specify/memory/constitution.md`, ratified 2026-05-14).

### Principle I — Azure-First Architecture

**Gate**: ✅ PASS. Every new resource is Azure-native (Cosmos containers, AI Search index, Container Apps Function hosting). No multi-cloud abstractions introduced. The pluggable observability adapter on the frontend (already in place) preserves OSS optionality without introducing a cloud-agnostic registry — the registry IS Azure-Service-Bus-shaped per Principle VI (incremental extensibility — broader broker support is reserved for a future spec).

### Principle II — API-First Design

**Gate**: ✅ PASS. The slice ships a full REST surface (`/api/registry`, `/api/registry/{id}`, `/api/registry/search`, `/api/registry/{id}/audit`) with OpenAPI documents generated by the existing `Microsoft.AspNetCore.OpenApi` pipeline. The UI consumes only these public endpoints — no UI backdoor that bypasses the contract. The conflict response (FR-020) and the audit list response are versioned via `Accept: application/vnd.busterminal.registry+json; v=1` per the project's emerging contract-versioning convention (formalized in `contracts/registry-api.yaml`).

### Principle III — Strong Domain Modeling

**Gate**: ✅ PASS with one acknowledged divergence from spec 004. Spec 006 introduces five concrete registry entity types (Namespace, Queue, Topic, Subscription, Rule) with a registry-specific canonical field set (per spec.md FR-002). These are **not** subtypes of spec-004's `Resource` abstract base, because their field set diverges materially (2-value `status` vs 5-value `Lifecycle`, free-form `owner` string vs `OwnershipRecord` reference, free-form key/value `tags` vs `TagReference` to first-class `TagResource`, hard delete vs `IsDeleted` soft-delete, `Rule` as first-class entity vs embedded `Subscription.Rule`). Maintaining one model that satisfies both spec 006's "minimum-friction registry" UX and spec 004's "rich canonical domain" governance constraints would force every registry write through the heavier canonical pipeline and break the FR-002 minimum-required-field contract. The two models will be **reconciled by a future "registry-domain unification" spec** once spec 006 has shipped and produced operator feedback. The vocabulary divergence is recorded in `contracts/registry-entity.schema.json` and explicitly tagged in `data-model.md §Vocabulary Alignment` — every registry term is mapped to its spec-004 counterpart so future reconcilers do not have to reverse-engineer the relationship. This is logged as the **first** Complexity Tracking entry.

### Principle IV — Security by Default

**Gate**: ✅ PASS. Service-to-Cosmos and service-to-AI-Search use the existing workload UAMI (`Cosmos DB Built-in Data Contributor` already in place from spec 004; `Search Index Data Contributor` allowlisted in spec 005 — extended to the registry index scope). No new secrets are introduced; the App Insights connection string remains the documented exception per spec 005. The indexer Functions container uses managed identity for Cosmos change-feed lease management and AI Search ingestion. Secrets gating (gitleaks, no plaintext outputs, etc.) is inherited unchanged.

One Principle-IV variance is logged as the **second** Complexity Tracking entry: spec 006 FR-037 says any authenticated tenant user may read AND write. The spec-003 role-permission matrix says writes require `MutateDomain` (Operator+Admin only). Spec 006's endpoints will be `[Authorize]` without a role policy. This is FR-037's explicit clarification and the spec's risk acceptance (Q in spec 006), and it is reversed by a future "registry governance" spec without code rewrites.

### Principle V — Operational Excellence

**Gate**: ✅ PASS. All new resources route to the LAW via the existing `iac/modules/diagnostic-settings` wrapper (`allLogs`-only convention from spec 005's Q5c). The API emits OTel traces via the existing `BusTerminalTelemetry` pipeline. The indexer Functions container emits OTel traces via the same App Insights resource (shared instrumentation key, distinguished by Cloud Role Name `busterminal-indexer`). Frontend traces propagate W3C Trace Context on every registry API call (FR-042). Indexer permanent failures emit a structured warn-or-error log with the failing document ID + retry count + error category; an ops dashboard binding is **deferred** to a future ops-hardening spec (consistent with spec 005's same deferral; rationale: dashboards before downstream specs have produced operational data yields empty dashboards and churn).

### Principle VI — Incremental Extensibility

**Gate**: ✅ PASS. The data model and APIs use Azure-Service-Bus-specific terminology (Namespace/Queue/Topic/Subscription/Rule) but the persistence layer (`registry-entities` container with `/environment` PK and the registry entity contract) carries an `entityType` discriminator that can extend cleanly to other broker shapes (Kafka topic/partition/consumer-group, RabbitMQ queue/exchange/binding) without restructuring the container — a future spec adds a discriminator value, ships new validation, and reuses the same index/Functions/UI scaffolding. The frontend explorer's tree-and-detail pattern is broker-agnostic. The search index schema includes a `brokerKind` filterable field reserved for future multi-broker filtering even though v1 only emits `AzureServiceBus`. No Service-Bus-specific schema is baked into the index, the container, or the UI in a way that blocks future expansion.

### Technology Standards (Constitution §Technology Standards)

| Standard | Compliance |
|---|---|
| Backend: .NET 10 + ASP.NET Core Minimal APIs preferred | ✅ Minimal APIs. New endpoints registered via `MapRegistryEndpoints()` extension following the spec-003 `MapWhoAmIEndpoint` + spec-004 endpoint-builder pattern. No Controllers. |
| Vertical Slice Architecture | ✅ New code lives in `Features/Registry/{Namespaces,Queues,Topics,Subscriptions,Rules,_Shared}` — one folder per slice, contains endpoint + request/response DTOs + validators + handler + persistence calls. Cross-slice shared types live in `Features/Registry/_Shared` (canonical entity contract, conflict response, audit list response, search proxy). |
| Built-in DI container | ✅ All new services registered via `Program.cs` extension methods. No third-party DI. |
| OpenAPI for every public API | ✅ `Microsoft.AspNetCore.OpenApi` generates the document; `contracts/registry-api.yaml` is the authoring source for the documented contract and is verified against the runtime document by a CI assertion. |
| Frontend: Next.js 16.x App Router | ✅ App Router only; no Pages Router. RSC by default; Client Components only for the explorer tree (interactive), search box, write forms, and conflict modal. |
| TypeScript strict | ✅ Existing config unchanged. |
| Tailwind v4 + shadcn/ui (project-owned) | ✅ All new UI composes existing shadcn primitives (`Button`, `Dialog`, `Tabs`, `Input`, `Select`, `Toast` via Sonner, etc.) from `web/components/ui/`. No CSS-in-JS. No second design system. |
| TanStack Table (data tables) | ✅ Used for the search-results table and the entity-children tables on detail pages. |
| React Hook Form + Zod | ✅ All forms (create/edit Namespace/Queue/Topic/Subscription/Rule) use RHF + Zod; the same Zod schema is the source of truth for client-side validation and is mirrored against the backend FluentValidation rules via a contract test (research §1). |
| Recharts (charts) | ✅ Not used in this slice (registry has no chart surfaces). |
| Framer Motion sparingly | ✅ Used only for the explorer tree expand/collapse + dialog transitions; `prefers-reduced-motion` honored by the existing wrappers. |
| next-themes (dark/light) | ✅ Existing theme provider unchanged. |
| Browser baseline | ✅ Unchanged. |
| Cosmos DB metadata storage + AI Search discovery | ✅ Per spec; new registry containers on the existing account; new index on the existing service. |
| Container Apps + ACR | ✅ Backend reuses existing image build pipeline. Indexer adds a new container image to the existing ACR. |
| Containerized Azure Functions on the Container Apps Environment | ✅ The indexer is **exactly** this hosting model — the newest native Functions-for-CAE option is selected in research §4. |
| OpenTofu, AVM preferred, versions pinned | ✅ New module `cosmos-registry-store` extends the existing `cosmos-canonical-store` pattern (hand-authored — AVM does not cover SQL container definitions inside an existing database). New `ai-search-index` uses `azapi` (AVM does not cover index resources). New `functions-container-app` uses `azurerm_container_app` (the newest native Functions-for-CAE path documented by Microsoft Learn). |
| Managed identity preferred over secrets | ✅ Workload UAMI for Cosmos + AI Search + App Insights AAD ingestion; no new secrets. |
| W3C Trace Context propagation on UI-originated HTTP | ✅ Existing `web/lib/http/` client already propagates `traceparent`/`tracestate`; registry data-fetching modules consume it. |
| Diagnostics to the LAW via `iac/modules/diagnostic-settings` | ✅ New AI Search index does not have a per-index diagnostic setting (the setting is at the AI Search **service** level — already in place from spec 005). The new Functions container app gets a per-resource diagnostic setting via the wrapper. |

### Engineering Workflow & Quality Standards

| Standard | Compliance |
|---|---|
| Spec-driven development | ✅ `/speckit-specify` → `/speckit-clarify` → `/speckit-plan` (this artifact) → `/speckit-tasks` → `/speckit-implement`. |
| CI gates (build, unit, lint, format, security, dependency scan) | ✅ Existing CI workflows unchanged. New backend code and frontend code are picked up by the existing per-PR check matrix. The indexer project is a new csproj added to `api/BusTerminal.slnx` — the existing `dotnet test` matrix covers it. The new IaC modules are picked up by the existing `iac-validate.yml` workflow. |
| Testing strategy (unit/integration/contract/UI/E2E) | ✅ All five layers present in the plan. |
| Trunk-based with feature branches | ✅ On `feature/006-service-bus-registry-core`. |

### Result: ✅ PASS (with two documented exceptions under Complexity Tracking)

Phase 0 may proceed.

## Project Structure

### Documentation (this feature)

```text
specs/006-service-bus-registry-core/
├── plan.md                           # This file
├── research.md                       # Phase 0 output — numbered decisions
├── data-model.md                     # Phase 1 output — registry entity model + search index schema
├── quickstart.md                     # Phase 1 output — local dev + first-deploy walkthrough
├── contracts/                        # Phase 1 output
│   ├── registry-api.yaml             # OpenAPI 3.1 — CRUD + search + audit endpoints
│   ├── registry-entity.schema.json   # Canonical JSON shape for every registry entity (FR-002)
│   ├── conflict-response.schema.json # FR-020 conflict response shape (recoverable conflict + field diffs)
│   ├── audit-event.schema.json       # FR-032 audit event shape
│   ├── search-index.json             # Azure AI Search index definition (consumed by the IaC module)
│   ├── indexer-events.md             # Cosmos change-feed → indexer event contract + poison handling
│   └── outputs-contract.md           # Incremental IaC outputs introduced by spec 006
└── tasks.md                          # Phase 2 output — NOT created by /speckit-plan
```

### Source Code (repository root)

```text
api/
├── BusTerminal.Api/
│   ├── Features/
│   │   ├── Registry/                                 # NEW — top-level slice family for spec 006
│   │   │   ├── _Shared/
│   │   │   │   ├── RegistryEntity.cs                 # Canonical registry entity (record) — distinct from Domain/Resources/Resource
│   │   │   │   ├── RegistryEntityType.cs             # Closed enum: Namespace, Queue, Topic, Subscription, Rule
│   │   │   │   ├── RegistryEntityStatus.cs           # Closed enum: Active, Deprecated  (Deleted reserved, not emitted)
│   │   │   │   ├── RegistryTag.cs                    # Key/Value pair (free-form, case-insensitive key match)
│   │   │   │   ├── RegistrySource.cs                 # Closed enum: Manual (Discovered reserved, not emitted)
│   │   │   │   ├── ConflictResponse.cs               # FR-020 dto: { currentState, changedFields[], submittedVersion }
│   │   │   │   ├── AuditEvent.cs                     # FR-032 record + append-only writer contract
│   │   │   │   ├── IRegistryEntityStore.cs           # Persistence port
│   │   │   │   ├── IAuditEventStore.cs               # Audit persistence port
│   │   │   │   ├── ISearchClient.cs                  # AI Search adapter port
│   │   │   │   ├── RegistryEntityValidationRules.cs  # Shared FluentValidation rules (name, env, ARM id, tag shape)
│   │   │   │   ├── ChildCountChecker.cs              # FR-009 — counts children before allowing delete
│   │   │   │   ├── ConcurrencyConflictMapper.cs      # Cosmos 412 → ConflictResponse
│   │   │   │   ├── RegistryEndpointsBuilder.cs       # Shared MapGroup pattern + AuthN-only filter
│   │   │   │   └── RegistryDtoMapping.cs             # Entity ↔ Request/Response DTOs
│   │   │   ├── Namespaces/
│   │   │   │   ├── NamespaceEndpoints.cs             # GET/POST/PUT/DELETE/list — Minimal APIs
│   │   │   │   ├── NamespaceRequests.cs              # Create/Update DTOs
│   │   │   │   ├── NamespaceResponses.cs             # Detail + List DTOs
│   │   │   │   └── NamespaceValidator.cs             # FluentValidation
│   │   │   ├── Queues/ … Topics/ … Subscriptions/ … Rules/   # Same shape per entity
│   │   │   ├── Search/
│   │   │   │   ├── SearchEndpoint.cs                 # GET /api/registry/search
│   │   │   │   ├── SearchRequests.cs                 # query, filters{entityType, environment, status, tag}, page, sort
│   │   │   │   └── SearchResponses.cs                # paginated, ranked
│   │   │   └── Audit/
│   │   │       ├── AuditEndpoint.cs                  # GET /api/registry/{id}/audit?limit=N
│   │   │       └── AuditResponses.cs
│   │   ├── Health/                                   # Existing (untouched)
│   │   ├── Identity/                                 # Existing (untouched)
│   │   └── RoleProbes/                               # Existing (untouched)
│   ├── Infrastructure/
│   │   ├── Persistence/                              # EXTENDED — adds registry-specific stores
│   │   │   ├── (existing CosmosCanonicalResourceStore etc — untouched)
│   │   │   ├── CosmosRegistryEntityStore.cs          # NEW — registry-entities container; ETag-based concurrency; FR-014 duplicate detection within parent+env scope
│   │   │   ├── CosmosAuditEventStore.cs              # NEW — registry-audit container; append-only writes; entity-scoped query
│   │   │   └── CosmosRegistryOptions.cs              # NEW — container names, RU bands, partition key paths
│   │   ├── Search/                                   # NEW
│   │   │   ├── AzureAiSearchClient.cs                # Implements ISearchClient via Azure.Search.Documents + DefaultAzureCredential
│   │   │   └── AiSearchOptions.cs
│   │   ├── Authentication/ Configuration/ Credentials/ Graph/ Observability/   # Existing (untouched)
│   ├── Authorization/                                # Existing — RegistryEndpointsBuilder requires AuthN, no role policy (FR-037)
│   └── Program.cs                                    # EXTENDED — services.AddRegistryFeature(); app.MapRegistryEndpoints();
├── BusTerminal.Api.Tests/                            # EXTENDED — Features.Registry.* test suites
└── BusTerminal.Indexer/                              # NEW — containerized Azure Functions project
    ├── BusTerminal.Indexer.csproj                    # .NET 10 isolated worker; Cosmos + Search SDKs
    ├── Program.cs                                    # FunctionsApplicationBuilder + AAD config
    ├── Dockerfile                                    # Functions-for-CAE base image
    ├── Functions/
    │   ├── RegistryEntityIndexer.cs                  # Cosmos change-feed trigger → AI Search upsert/delete
    │   └── PoisonHandler.cs                          # Bounded retries → log permanent failure
    ├── Indexing/
    │   ├── SearchDocumentMapper.cs                   # RegistryEntity → SearchDocument projection
    │   └── IndexNames.cs                             # Single source of truth for the index name
    └── host.json, local.settings.json.example

web/
├── app/
│   ├── (authenticated)/
│   │   ├── layout.tsx                                # Existing (untouched)
│   │   ├── platform-status/ … (existing)
│   │   └── registry/                                 # NEW — top-level App Router segment
│   │       ├── layout.tsx                            # Two-pane shell: explorer tree (left) + outlet (right)
│   │       ├── page.tsx                              # Default landing — welcome + recent activity panel
│   │       ├── search/
│   │       │   └── page.tsx                          # /registry/search?q=&type=&env=&tag=&page=
│   │       ├── new/
│   │       │   └── [entityType]/page.tsx             # /registry/new/namespace, /registry/new/queue, etc.
│   │       └── [entityType]/
│   │           └── [id]/
│   │               ├── page.tsx                      # Detail page — metadata, relationships, audit panel
│   │               └── edit/page.tsx                 # Edit form (Client Component) — conflict modal
├── components/
│   ├── registry/                                     # NEW — registry-specific composite components
│   │   ├── registry-explorer-tree.tsx                # Tree (Client) using existing tree primitives
│   │   ├── registry-tree-node.tsx
│   │   ├── registry-detail-shell.tsx
│   │   ├── registry-metadata-panel.tsx               # Composes existing metadata-key-value-panel
│   │   ├── registry-relationships-panel.tsx          # Lists children + parent links (uses TanStack Table)
│   │   ├── registry-audit-panel.tsx                  # Last-N audit list
│   │   ├── registry-search-input.tsx                 # Global search bar — uses cmdk + RHF
│   │   ├── registry-search-results-table.tsx        # TanStack Table
│   │   ├── registry-search-filters.tsx               # Entity type, env, tag chips
│   │   ├── registry-empty-state.tsx
│   │   ├── registry-conflict-modal.tsx               # FR-020 — Discard & refresh / Force overwrite
│   │   ├── registry-delete-confirmation.tsx
│   │   ├── registry-tag-editor.tsx                   # Free-form key/value list editor
│   │   ├── registry-status-badge.tsx                 # Active / Deprecated visual (FR-013a)
│   │   └── forms/
│   │       ├── namespace-form.tsx                    # RHF + Zod
│   │       ├── queue-form.tsx
│   │       ├── topic-form.tsx
│   │       ├── subscription-form.tsx
│   │       ├── rule-form.tsx
│   │       └── shared/
│   │           ├── entity-form-shell.tsx             # Submit states, error surface, conflict modal hookup
│   │           └── azure-resource-id-input.tsx       # ARM-resource-id parser + inline validation
│   ├── domain/                                       # Existing primitives — reused (untouched)
│   ├── app-shell/ navigation/ layout/ data-table/ feedback/ forms/ ui/   # Existing (untouched)
├── lib/
│   ├── registry/                                     # NEW
│   │   ├── api.ts                                    # Typed fetch client (RSC-safe + client-safe)
│   │   ├── schemas.ts                                # Zod schemas for every entity create/update payload
│   │   ├── types.ts                                  # TypeScript counterparts (inferred from Zod)
│   │   ├── tag-utils.ts                              # Key normalization (case-insensitive match)
│   │   ├── conflict.ts                               # ConflictResponse parsing + diff helpers
│   │   └── query-keys.ts                             # TanStack Query key factories
│   ├── http/                                         # Existing — already propagates W3C Trace Context (untouched)
│   ├── observability/                                # Existing (untouched)
│   ├── auth/                                         # Existing (untouched)
│   └── (others — untouched)
├── tests/
│   ├── e2e/registry/                                 # NEW — Playwright e2e specs (create, edit, delete, search, conflict)
│   ├── a11y/registry/                                # NEW — axe-playwright a11y gates on each route
│   └── unit/                                         # EXTENDED — Vitest unit tests for new components + lib
└── package.json                                      # +@tanstack/react-query ^5

iac/
├── environments/
│   └── dev/
│       └── main.tf                                   # EXTENDED — compose cosmos-registry-store, ai-search-index, functions-container-app
├── modules/
│   ├── cosmos-registry-store/                        # NEW — two SQL containers on the existing canonical-store database
│   │   ├── main.tf  variables.tf  outputs.tf  versions.tf  README.md
│   ├── ai-search-index/                              # NEW — AI Search index resource via azapi
│   │   ├── main.tf  variables.tf  outputs.tf  versions.tf  README.md
│   │   └── index-definition.json                     # Sources contracts/search-index.json
│   ├── functions-container-app/                      # NEW — Functions-for-CAE container app + workload UAMI bindings
│   │   ├── main.tf  variables.tf  outputs.tf  versions.tf  README.md
│   └── (all existing modules — untouched)
└── policies/                                         # Existing BT-IAC-001..007 cover new resources automatically (tags, diagnostics, allLogs-only, RBAC scope, etc.)
```

**Structure Decision**: Spec 006 fits cleanly into the existing repository layout. The backend slice family `Features/Registry/` matches the vertical-slice convention already used by `Features/Identity/` and `Features/RoleProbes/`. The indexer is a new sibling .NET project (`api/BusTerminal.Indexer/`) added to the existing `BusTerminal.slnx` — keeping the indexer separate from the API mirrors the constitution's "async-first, event-driven processing as containerized Functions" guidance without prematurely splitting the modular monolith. The frontend follows the existing `web/app/(authenticated)/` segment pattern. The new IaC modules mirror the existing `iac/modules/cosmos-canonical-store/` and `iac/modules/ai-search/` patterns. No directory rename or restructure is required.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|---|---|---|
| **Registry entities do NOT inherit from spec-004's `Resource` canonical type and live in a parallel pair of Cosmos containers (`registry-entities`, `registry-audit`) rather than reusing spec-004's `resources` and `change-events` containers.** This is a Principle-III (Strong Domain Modeling) vocabulary divergence — two entity models that overlap in concept (Namespace/Queue/Topic/Subscription) but differ in field set (2-value status vs 5-value lifecycle, free-form owner vs OwnershipRecord, key/value tags vs TagReference, hard delete vs soft-delete, Rule as first-class entity vs embedded). | Spec 006's UX target is the *minimum-friction registry experience* a new operator can pick up in 10 minutes (SC-001) without confronting the spec-004 governance machinery (semantic versioning, contract compatibility metadata, classification taxonomies, lifecycle transitions). Spec 006's FR-002 explicitly enumerates a smaller required-field set. Forcing every registry write through the spec-004 pipeline would break the FR-003 "only `id`, `entityType`, `name`, `environment`, `status` are required" contract by triggering spec-004's `RequiredFieldsRule` (which requires `DisplayName`, `Lifecycle`, `Version`, `Audit`, plus `Ownership` for operational types). The hard-delete semantics (FR-013) also clash with spec-004's `IsDeleted` soft-delete + `Restored` change-event vocabulary. The spec-004 substrate (`CosmosClientFactory`, `AzureCredentialFactory`, the ETag concurrency pattern, the change-event-on-write pattern) IS reused as building blocks. The vocabulary mapping table in `data-model.md §Vocabulary Alignment` documents every divergence so a future "registry-domain unification" spec can reconcile the two models without reverse-engineering. | Building one unified model that satisfies both spec 006's lean UX AND spec 004's rich governance would either (a) bloat the registry API request shape with optional spec-004 fields users never fill, slowing first-use time-to-value (failing SC-001) — or (b) project the registry shape into the spec-004 shape at the persistence boundary, requiring a translation layer the team has to maintain and reason about for every read and write, conflicting with Decision Priority §1 (Operational Simplicity) and §3 (Maintainability). The deferred reconciliation spec gives us operator feedback first, which is a stronger basis for the unified design than upfront speculation. |
| **Spec 006 API endpoints use `[Authorize]` (require authentication) WITHOUT binding to a spec-003 role policy.** This means a holder of the `BusTerminal.Reader` role can call `POST /api/registry`, `PUT /api/registry/{id}`, and `DELETE /api/registry/{id}` — violating the spec-003 role-permission matrix's classification of those operations as `MutateDomain` (Operator+Admin only). | This is the spec-006 FR-037 clarification's explicit decision and the spec's stated risk acceptance: "the initial deployment is expected to be scoped to a tenant whose member population is already restricted to messaging engineering personnel." Spec-006 is the first product-usable slice and ships value-to-time-to-value; role gating adds a coordination step (the operator must request `Operator` from a tenant admin) that defers first-use indefinitely in self-onboarding scenarios. The audit trail (FR-032) records the actor identity on every write, so traceability is preserved even without role gating. A future "registry governance" spec restores the role binding by changing the endpoint policy from `[Authorize]` to `[Authorize(Policy = "CanMutateDomain")]` — a one-line change per endpoint with no schema, persistence, or UX consequence. | The simpler alternative (use `CanMutateDomain` from day one) was rejected by the spec's explicit clarification answer (Q: "who counts as an operator" → A: "any authenticated tenant user"). The spec-006 audit foundations (FR-032) provide the compensating control. Recording this as a documented exception per the constitution's §Governance/Compliance Review keeps the deviation traceable; the spec-003 role-permission matrix (`specs/003-auth-and-identity/contracts/role-permission-matrix.md`) is updated by a future spec, not by this one. |

---

## Post-Design Constitution Re-Check

*Performed after Phase 0 (`research.md`) and Phase 1 (`data-model.md`, `contracts/`, `quickstart.md`).*

Re-evaluating each principle against the concrete decisions captured in the Phase-0 and Phase-1 artifacts:

- **I. Azure-First Architecture** — ✅ Still PASS. Research §3 confirms Cosmos DB Change Feed + Azure Functions for Container Apps as the chosen indexing pipeline — Azure-native, no abstraction layer. Research §5 confirms the AI Search index is provisioned via `azapi` (Azure REST envelope) — Azure-native. No multi-cloud abstraction crept in during design.

- **II. API-First Design** — ✅ Still PASS. `contracts/registry-api.yaml` is the formal OpenAPI 3.1 document for the registry surface — versioned, automation-friendly, generated-and-verified against the runtime document. The conflict response shape (`contracts/conflict-response.schema.json`) is part of the public contract, not a UI internal. The Cosmos change-feed → AI Search projection contract (`contracts/indexer-events.md`) is documented and stable (FR-025 indexing semantics).

- **III. Strong Domain Modeling** — ✅ PASS with the documented Complexity-Tracking divergence from spec 004. The `data-model.md §Vocabulary Alignment` table is the cross-reference per Principle III; every registry-side term has its spec-004 counterpart documented (or its status as "registry-only with no spec-004 equivalent" recorded). Vocabulary remains internally consistent: API request shapes, response shapes, persisted JSON shapes, search-index field names, and OTel attribute names all match `RegistryEntity.Field`-style naming, matching the spec-004 cross-reference convention.

- **IV. Security by Default** — ✅ PASS with the documented Complexity-Tracking FR-037 deviation. Research §7 confirms the AI Search ingestion path uses workload UAMI + `Search Index Data Contributor` role (no admin key); the Functions container uses managed identity for both Cosmos lease management and AI Search ingestion. No new secrets are introduced. Network-wise: in dev, public access stays on for the data services (per spec-005 Q2c) so the indexer Function can reach Cosmos and AI Search without VNet integration; in test/prod templates, VNet integration of the Functions container is wired (research §4).

- **V. Operational Excellence** — ✅ Still PASS. The indexer emits structured log events for every successful upsert (Info), every retry (Warning), and every permanent failure (Error) with the failing document ID + a deterministic correlation ID that links to the Cosmos change-feed lease offset (so an operator can `kusto-search` from a permanent-failure log entry to the originating API write and back to the actor). FR-025 indexer-failure visibility is achieved by these logs + the LAW retention (30d default per spec-005 Q5c tf-var). Dashboards remain a deferred ops-hardening concern.

- **VI. Incremental Extensibility** — ✅ Strengthened by Phase 1. The search index schema (`contracts/search-index.json`) carries `brokerKind` and `entityType` as filterable fields and uses an extensible `metadata` field projected as a flattened JSON map — future broker types add an `entityType` discriminator without re-indexing. The frontend `RegistryEntityType` union is a string-literal type that can extend without breaking existing routes (the dynamic `[entityType]` segment accepts any value the API recognizes). The IaC `cosmos-registry-store` module accepts a list of containers as a variable, so additional containers for future feature slices (e.g., relationship overrides, ownership tickets) can be added without forking the module.

### Technology Standards re-check

| Standard | Compliance after design |
|---|---|
| Minimal APIs (not Controllers) | ✅ Confirmed in `Features/Registry/*/*Endpoints.cs` pattern |
| Vertical Slice Architecture | ✅ Confirmed: one slice per entity + `_Shared` |
| OpenAPI for public APIs | ✅ `contracts/registry-api.yaml` + runtime doc + CI assertion |
| Cosmos DB metadata + AI Search discovery | ✅ Confirmed |
| Containerized Azure Functions on CAE (newest native) | ✅ Confirmed in research §4 |
| OpenTofu, AVM preferred, pinned | ✅ Hand-authored where AVM does not cover (SQL containers, AI Search index, Functions-for-CAE) — rationale documented |
| Managed identity preferred | ✅ Confirmed: no new secrets |
| W3C Trace Context propagation | ✅ Confirmed: existing `web/lib/http/` client unchanged, registry data layer consumes it |
| All Azure diagnostics → LAW via `allLogs`-only convention | ✅ Confirmed for the Functions container app |

### Result: ✅ PASS

No new Complexity Tracking entries needed beyond the two documented above (registry-vs-spec-004 vocabulary divergence + FR-037 authorization deviation). Phase 0 and Phase 1 artifacts are coherent with the constitution.

---

## Artifact Index (post-`/speckit-plan`)

| Artifact | Purpose | Status |
|---|---|---|
| [`plan.md`](./plan.md) | This file — Technical Context, Constitution Check, Project Structure, Complexity Tracking | ✅ produced |
| [`research.md`](./research.md) | Phase 0 — numbered decisions resolving every NEEDS-CLARIFICATION + best-practices research | ✅ produced |
| [`data-model.md`](./data-model.md) | Phase 1 — registry entity model, search-index schema, audit-event schema, vocabulary-alignment-to-spec-004 cross-reference | ✅ produced |
| [`contracts/registry-api.yaml`](./contracts/registry-api.yaml) | Phase 1 — OpenAPI 3.1 contract for registry CRUD, search, and audit endpoints | ✅ produced |
| [`contracts/registry-entity.schema.json`](./contracts/registry-entity.schema.json) | Phase 1 — canonical JSON shape for every registry entity | ✅ produced |
| [`contracts/conflict-response.schema.json`](./contracts/conflict-response.schema.json) | Phase 1 — FR-020 conflict response shape | ✅ produced |
| [`contracts/audit-event.schema.json`](./contracts/audit-event.schema.json) | Phase 1 — FR-032 audit event shape | ✅ produced |
| [`contracts/search-index.json`](./contracts/search-index.json) | Phase 1 — Azure AI Search index definition | ✅ produced |
| [`contracts/indexer-events.md`](./contracts/indexer-events.md) | Phase 1 — Cosmos change-feed → indexer event contract + poison handling | ✅ produced |
| [`contracts/outputs-contract.md`](./contracts/outputs-contract.md) | Phase 1 — incremental IaC outputs introduced by spec 006 | ✅ produced |
| [`quickstart.md`](./quickstart.md) | Phase 1 — operator walkthrough: local dev, dev deploy, first-namespace flow | ✅ produced |
| `tasks.md` | Phase 2 output — NOT created by `/speckit-plan`; produced by `/speckit-tasks` | ⏳ pending |
| `CLAUDE.md` SPECKIT-block reference | Updated to point at this plan | ✅ updated |
