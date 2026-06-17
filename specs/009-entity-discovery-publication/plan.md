# Implementation Plan: Entity Discovery and Publication

**Branch**: `009-entity-discovery-publication` | **Date**: 2026-06-17 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/009-entity-discovery-publication/spec.md`

## Summary

Spec 009 turns the registry's existing manually-curated entity catalog (Spec 006) into an authoritative, automatically-populated index of Azure Service Bus topology. A namespace administrator triggers a discovery run through the existing namespace UX; the API enqueues the run on an internal Service Bus queue and returns a run reference; a Functions v2 worker (a new function in the existing `BusTerminal.Indexer` project) drains the queue, walks the target Azure Service Bus namespace via the ARM `Azure.ResourceManager.ServiceBus` SDK with bounded parallelism, classifies each observed entity as new / updated / unchanged / missing, and persists results to Cosmos. The existing change-feed projector forwards updates to Azure AI Search so the catalog stays searchable in near-real-time.

The plan extends — rather than replicates — the existing Spec 006 `registry-entities` schema by layering an `azureSourced.*` sub-document, a `lifecycleStatus` field, and an `EntityServiceAssociation[]` collection onto the existing entity records. Manually curated metadata fields stay on the same documents and are never overwritten by discovery (write-merge filters Azure-sourced fields only). A new `discovery-runs` Cosmos container records every run with timing, counts, and error detail.

Concurrency control (FR-003 coalescing) is implemented with a per-namespace `discovery-lock-{namespaceId}` document acquired via Cosmos optimistic concurrency before the API enqueues a run. Bounded exponential backoff on transient ARM 429/503/timeouts is provided by the Azure SDK's built-in retry policy with tuned options.

Frontend work extends existing Spec 006 / 008 surfaces:
- **Namespace overview** (existing): adds a Discover button + last-run status panel + entity counts.
- **Discovery history** (new): a route under `/namespaces/{id}/discovery-runs` listing runs with status/duration/counts/error detail.
- **Entity catalog** (existing `/registry/search`): adds `lifecycleStatus`, `associatedService`, and `role` filters.
- **Entity detail** (existing): adds a Discovery Information panel + an Azure Metadata vs. Registry Metadata split + a Service Associations editor.

IaC adds: a single internal Service Bus queue (`discovery-requested`), a new Cosmos container (`discovery-runs`), and an extension to the existing AI Search index schema. No new compute resources are introduced — the worker rides on the existing `BusTerminal.Indexer` Functions v2 container. UAMI role grants already in place (`Data Contributor` on Cosmos, `Search Index Data Contributor` on AI Search, `Reader` on registered namespaces via the existing runbook) suffice; the only new grant is `Azure Service Bus Data Receiver` on the internal namespace, which is already provisioned by `iac/modules/service-bus`.

## Technical Context

**Language/Version**: .NET 10, C# 13. Frontend: TypeScript 5 strict, Next.js 16.x, React 19 (RSC default), Tailwind v4. IaC: OpenTofu (latest, pinned via `.terraform.lock.hcl`).

**Primary Dependencies**:
- Backend API: ASP.NET Core Minimal APIs, FluentValidation 11.10.x, `Microsoft.Azure.Cosmos` (existing), `Azure.Messaging.ServiceBus` (sender side, new use within API), `Microsoft.Identity.Web` (existing JWT bearer).
- Discovery worker: `Microsoft.Azure.Functions.Worker` (existing in `BusTerminal.Indexer`), `Microsoft.Azure.Functions.Worker.Extensions.ServiceBus` (new function trigger), `Azure.ResourceManager.ServiceBus` 1.x (existing — used by `ArmNamespaceProbe`), `Azure.Identity`.
- Frontend: TanStack Query 5.x, TanStack Table v8, React Hook Form 7, Zod, shadcn/ui (existing primitives only — dialog, sheet, form, table, badge, alert, card, select, command, popover, tabs).
- IaC: `Azure/avm-res-app-containerapp/azurerm` v0.5.0 (existing), `Azure/avm-res-search-searchservice/azurerm` v0.2.0 (existing).

**Storage**: Azure Cosmos DB (existing `registry-entities` container, extended; new `discovery-runs` container; new `discovery-locks` container). Azure AI Search (existing `registry-entities-v1` index, schema extended with three new fields).

**Testing**:
- Backend unit + integration tests in `api/BusTerminal.Api.Tests/Features/Discovery/` using the existing Cosmos emulator bootstrapper.
- Discovery worker unit + integration tests in `api/BusTerminal.Indexer.Tests/Discovery/`. Integration tests use a recorded ARM HTTP fixture (deterministic; no live Azure).
- Contract tests verifying the OpenAPI surface matches the implemented endpoints.
- Component tests for new React components (Vitest + RTL + axe).
- E2E test (Playwright) for US1 against the dev environment (`tests/e2e/discovery-flow.spec.ts`).

**Target Platform**:
- Backend API: containerized .NET 10, Azure Container Apps (existing).
- Worker: containerized Azure Function (v2 native on Container Apps, `kind = "functionapp"`, existing `BusTerminal.Indexer` workload).
- Frontend: Next.js 16, Azure Container Apps (existing).

**Project Type**: Web service + frontend application + background worker. Modular monolith (no new microservice).

**Performance Goals**:
- Discovery completes in **≤ 5 minutes** for a SC-005-sized namespace (500 queues + 500 topics + 5,000 subscriptions + 5,000 rules) per the spec's quantified target.
- Per-entity classification + Cosmos upsert: amortized ≤ 30 ms each (allows 10,000 entities in 5 min with 32-way parallel writers).
- API endpoints: p95 ≤ 200 ms (excluding the discovery worker's runtime, which is async via Service Bus).
- Frontend Core Web Vitals: LCP ≤ 2.5s, INP ≤ 200ms, CLS ≤ 0.1 (inherited project budget; no regression on namespace overview or registry search).

**Constraints**:
- Worker must stay within Container Apps Consumption profile memory (≤ 4 GB) even for the largest namespace — stream entities; never materialize the full entity list before persistence.
- Retry overhead from FR-021a must not exceed ~30 s in the worst tolerable case (3 attempts × ~5 s exponential cap per call). For SC-005-sized namespaces this is comfortably absorbed by the 5-min budget.
- No PII in telemetry (constitution); span attributes carry namespace/entity IDs only, never display names or descriptions.
- The discovery worker MUST NOT issue create/update/delete operations against Azure Service Bus (spec non-goal).

**Scale/Scope**:
- Per namespace: up to ~10,000 entities, one discovery run at a time (coalesced).
- Across the platform: starting at ~10 registered namespaces in dev, modeled to scale to ~500 namespaces per tenant.
- Concurrent discovery runs across namespaces: unbounded (one per namespace), capped naturally by worker instance count and Container Apps scale rules.
- Discovery run history retention: indefinite (per spec assumption; reassess if Cosmos RU cost grows).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Gates derived from `.specify/memory/constitution.md` and `speckit-artifacts/tech-stack.md`:

| # | Gate | Status | Note |
|---|---|---|---|
| C-01 | Azure-First Architecture (P-I) | ✅ Pass | Uses ARM `Azure.ResourceManager.ServiceBus` and Functions v2 on Container Apps; no abstraction layer for non-Azure messaging. Provider seam (`IEntityDiscoveryProvider`) preserves Principle VI extensibility without leaking provider concerns into the core slice. |
| C-02 | API-First Design (P-II) | ✅ Pass | Every UI capability is backed by a documented Minimal API endpoint with OpenAPI; no UI-only backdoor. See `contracts/openapi.yaml`. |
| C-03 | Strong Domain Modeling (P-III) | ✅ Pass | `DiscoveryRun`, `PublishedEntity`, `EntityServiceAssociation`, `LifecycleStatus`, `EntityServiceRole` are first-class domain types used identically across API contracts, Cosmos schema, AI Search projection, telemetry attributes, and the React UI types. |
| C-04 | Security by Default (P-IV) | ✅ Pass | Worker authenticates via UAMI + `DefaultAzureCredential` (existing `ArmClient`); no secrets; no SAS/connection-strings; Cosmos + Search use AAD-only; per-endpoint role gates use existing `RequireNamespaceAdministrator` extension and a new `RequireEntityMetadataEditor` extension. |
| C-05 | Operational Excellence (P-V) | ✅ Pass | New `BusTerminal.Discovery` ActivitySource emits parent + child spans; new `Meter` emits `discovery.runs.completed`, `discovery.run.duration`, `discovery.entities.classified`; structured Serilog logs; correlation IDs propagated through Service Bus message properties; all diagnostics route to LAW. |
| C-06 | Incremental Extensibility (P-VI) | ✅ Pass | `IEntityDiscoveryProvider` abstraction allows future messaging systems (Kafka, RabbitMQ, etc.) without rewriting the worker, classifier, or persistence layers. |
| C-07 | Minimal APIs preferred | ✅ Pass | All new endpoints registered via the existing route group pattern in `Features/Discovery/_Shared/DiscoveryEndpointsBuilder.cs`. |
| C-08 | Vertical Slice Architecture | ✅ Pass | New `Features/Discovery/` slice on the API side; new `Discovery/` folder under `BusTerminal.Indexer/`. |
| C-09 | OpenTofu (no Bicep) | ✅ Pass | All IaC additions live under `iac/modules/` and `iac/environments/dev/main.tf` in HCL. |
| C-10 | Functions v2 native | ✅ Pass | Worker rides on the existing `BusTerminal.Indexer` container — already a v2 native `Microsoft.App/containerApps` with `kind = "functionapp"`. |
| C-11 | App Router only | ✅ Pass | New routes live under `web/app/(authenticated)/namespaces/[id]/discovery-runs/`. |
| C-12 | RSC default; client components scoped | ✅ Pass | Discovery history list and entity catalog filters are server-rendered; only the Discover button, association editor dialog, and live status polling are client components. |
| C-13 | Tailwind v4, no CSS-in-JS, no second design system | ✅ Pass | All new UI built from existing `components/ui/` primitives. No new design tokens introduced. |
| C-14 | No additional UI libraries | ✅ Pass | Reuses TanStack Table, TanStack Query, React Hook Form, Zod, shadcn, lucide-react. |
| C-15 | shadcn MCP for component lookups | ✅ Pass | Tasks that touch primitive props or variants will consult the shadcn MCP during `/speckit-tasks` and `/speckit-implement`. |
| C-16 | W3C Trace Context propagation | ✅ Pass | API → worker via Service Bus message `traceparent` property (W3C standard for messaging); worker → ARM via `Activity.Current` (Azure SDK auto-propagates). UI → API already handled by existing `web/lib/http/client.ts`. |
| C-17 | Dark mode primary | ✅ Pass | All new UI inherits theme tokens from existing primitives — no theme-specific overrides. |
| C-18 | No PII in telemetry | ✅ Pass | Span attributes capped to: namespace ID, entity ID, entity type, classification outcome, count metrics, ARM correlation ID. No display names, descriptions, or tag values. |
| C-19 | All diagnostics → LAW | ✅ Pass | New Cosmos container + AI Search index extension inherit the existing `diagnostic-settings` wrapper. New Service Bus queue inherits the existing namespace's `allLogs` diagnostic forwarding. |
| C-20 | CSS logical properties only | ✅ Pass | All new components use `ms-*`, `me-*`, `ps-*`, `pe-*`, `start-*`, `end-*` Tailwind classes. |
| C-21 | Managed Identity preferred | ✅ Pass | All Azure access (ARM, Cosmos, AI Search, Service Bus, App Insights) uses the existing workload UAMI via `DefaultAzureCredential`. |
| C-22 | AVM modules pinned where used | ✅ Pass | No new modules introduced; existing AVM-backed modules remain pinned. |
| C-23 | Async-first thinking (Architecture) | ✅ Pass | Discovery is queue-driven; API never blocks on Azure SDK calls. Worker uses `Parallel.ForEachAsync` for fan-out. |
| C-24 | Modular monolith first (Architecture) | ✅ Pass | No new microservice. Discovery lives as a vertical slice in the existing API + an additional function in the existing Indexer project. |
| C-25 | Performance budget (CWV "Good") | ✅ Pass | New UI surfaces are RSC-first, code-split per route, no new client-side dependencies beyond existing TanStack libs already loaded. |
| C-26 | Browser baseline (last 2 versions, evergreen + iPadOS Safari + Android Chrome) | ✅ Pass | No new browser APIs introduced. |
| C-27 | WCAG 2.2 AA accessibility | ✅ Pass | All new components are keyboard-operable, have ARIA labels via shadcn primitives, and will be axe-scanned in `tests/a11y/`. |
| C-28 | Testing strategy: unit + integration + contract + UI component + E2E | ✅ Pass | All five layers covered (see Testing in Technical Context). |
| C-29 | No new SAS/connection-strings | ✅ Pass | Service Bus queue trigger uses `__fullyQualifiedNamespace` AAD-mode binding (Functions worker pattern). |
| C-30 | BT-IAC-001..007 policy gates | ✅ Pass | No new role assignment GUIDs required (Reader on customer namespaces is already pre-allowlisted in `platform-bootstrap`). New Cosmos container and Service Bus queue inherit existing `allLogs`-only diagnostic forwarding (BT-IAC-003) and stateful-resource lifecycle protections (BT-IAC-007). |

**All gates pass.** No Complexity Tracking entries required.

## Project Structure

### Documentation (this feature)

```text
specs/009-entity-discovery-publication/
├── plan.md              # This file
├── spec.md              # The feature specification (existing)
├── research.md          # Phase 0 output (decisions w/ rationale & alternatives)
├── data-model.md        # Phase 1 output (Cosmos shapes, AI Search schema, telemetry surface)
├── quickstart.md        # Phase 1 output (how to run discovery end-to-end locally)
├── contracts/
│   └── openapi.yaml     # Phase 1 output (new endpoints; OpenAPI 3.1)
├── checklists/
│   └── requirements.md  # Existing spec-quality checklist
└── tasks.md             # Phase 2 output — created by /speckit-tasks (not by this command)
```

### Source Code (repository root)

```text
api/
├── BusTerminal.Api/
│   └── Features/
│       └── Discovery/                                # NEW vertical slice
│           ├── StartDiscovery/
│           │   ├── StartDiscoveryEndpoint.cs        # POST /api/namespaces/{namespaceId}/discover
│           │   ├── StartDiscoveryRequest.cs
│           │   ├── StartDiscoveryValidator.cs
│           │   └── StartDiscoveryResponse.cs        # { discoveryRunId, status, coalescedFromExisting }
│           ├── GetDiscoveryRun/
│           │   └── GetDiscoveryRunEndpoint.cs       # GET /api/discovery-runs/{discoveryRunId}
│           ├── ListDiscoveryRuns/
│           │   └── ListDiscoveryRunsEndpoint.cs     # GET /api/namespaces/{namespaceId}/discovery-runs
│           ├── SearchEntities/                       # extends existing /registry/search semantics
│           │   └── SearchEntitiesEndpoint.cs        # GET /api/entities — adds lifecycle+association filters
│           ├── GetEntityDetail/
│           │   └── GetEntityDetailEndpoint.cs       # GET /api/entities/{entityId}
│           ├── UpdateEntityMetadata/
│           │   ├── UpdateEntityMetadataEndpoint.cs  # PATCH /api/entities/{entityId}
│           │   ├── UpdateEntityMetadataRequest.cs
│           │   └── UpdateEntityMetadataValidator.cs
│           ├── ServiceAssociations/
│           │   ├── AddAssociationEndpoint.cs        # POST /api/entities/{entityId}/associations
│           │   ├── RemoveAssociationEndpoint.cs     # DELETE /api/entities/{entityId}/associations/{associationId}
│           │   └── ListAssociationsEndpoint.cs      # GET /api/entities/{entityId}/associations
│           ├── ArchiveEntity/
│           │   └── ArchiveEntityEndpoint.cs         # POST /api/entities/{entityId}/archive
│           └── _Shared/
│               ├── DiscoveryEndpointsBuilder.cs
│               ├── DiscoveryServiceCollectionExtensions.cs
│               ├── DiscoveryRunCoalescer.cs         # acquires per-namespace lock; returns existing run on conflict
│               ├── DiscoveryRunStore.cs             # IDiscoveryRunStore over the new discovery-runs container
│               ├── PublishedEntityStore.cs          # extends IRegistryEntityStore reads w/ lifecycle + association projections
│               └── DiscoveryRequestPublisher.cs     # publishes 'discovery-requested' Service Bus messages
│
└── BusTerminal.Indexer/
    └── Discovery/                                    # NEW Functions v2 worker code
        ├── DiscoveryRequestedFunction.cs            # [ServiceBusTrigger("discovery-requested")]
        ├── EntityDiscoveryOrchestrator.cs           # streams entities, classifies, persists
        ├── Providers/
        │   ├── IEntityDiscoveryProvider.cs          # extensibility seam (Principle VI)
        │   └── AzureServiceBusEntityDiscoveryProvider.cs  # uses Azure.ResourceManager.ServiceBus
        ├── Classification/
        │   ├── EntityClassifier.cs                  # new vs updated vs unchanged vs missing
        │   └── AzureSourcedHash.cs                  # stable hash over Azure-sourced fields
        ├── Persistence/
        │   └── DiscoveryWriteBatcher.cs             # bounded-parallel Cosmos upserts
        └── Telemetry/
            ├── DiscoveryActivitySource.cs
            └── DiscoveryMeter.cs

iac/
├── modules/
│   ├── service-bus/                                  # EXTEND with a new `discovery-requested` queue
│   ├── cosmos-registry-store/                        # EXTEND with `discovery-runs` + `discovery-locks` containers
│   └── ai-search-registry-index/                     # EXTEND index schema with lifecycleStatus + associations[] + azureSourced
└── environments/
    └── dev/
        └── main.tf                                   # wire the queue + new containers; no new resources beyond these

web/
├── app/
│   └── (authenticated)/
│       ├── namespaces/
│       │   └── [id]/
│       │       ├── page.tsx                          # EXTEND: Discover button + last-run panel
│       │       └── discovery-runs/                   # NEW
│       │           ├── page.tsx                      # discovery history list
│       │           └── [runId]/
│       │               └── page.tsx                  # run detail
│       └── registry/
│           ├── search/page.tsx                       # EXTEND: lifecycle + association filters
│           └── [entityType]/[id]/page.tsx            # EXTEND: Discovery Info panel + Azure/Registry split + associations
├── components/
│   ├── discovery/                                    # NEW
│   │   ├── discover-button.tsx                       # client; role-gated; posts to API; polls run status
│   │   ├── discovery-status-panel.tsx                # server component
│   │   ├── discovery-runs-table.tsx                  # TanStack Table; URL-state-driven
│   │   ├── discovery-run-detail.tsx                  # server component
│   │   ├── entity-discovery-info.tsx                 # first/last seen, status badge
│   │   ├── entity-azure-metadata.tsx                 # read-only display of azureSourced fields
│   │   └── service-association-editor.tsx            # client; shadcn Dialog; RHF + Zod
│   └── registry/
│       └── filters/                                   # EXTEND existing
│           ├── lifecycle-filter.tsx                  # NEW
│           └── service-association-filter.tsx        # NEW
├── lib/
│   └── discovery/                                    # NEW
│       ├── api.ts                                    # typed client wrappers (startDiscovery, getRun, listRuns, etc.)
│       ├── schemas.ts                                # Zod schemas mirroring contracts/openapi.yaml
│       └── permissions.ts                            # canEditEntityMetadata() helper
└── tests/
    ├── e2e/
    │   └── discovery-flow.spec.ts                    # NEW US1 happy path
    └── a11y/
        └── discovery-runs.spec.ts                    # NEW
```

**Structure Decision**: Single-repo, modular monolith. The backend gains a new vertical slice (`api/BusTerminal.Api/Features/Discovery/`) and the existing `BusTerminal.Indexer` Functions v2 project gains a new `Discovery/` folder for the worker — no new project, no new container app, no new microservice. The frontend adds new routes nested under existing namespace and registry surfaces, plus a `components/discovery/` folder for new UI primitives. IaC extends three existing modules and rewires the dev environment; no new module is introduced.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

No violations. All 30 gates pass without deviation.
