# Implementation Plan: Core Domain Model

**Branch**: `004-core-domain-model` | **Date**: 2026-05-23 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/004-core-domain-model/spec.md`

---

## Summary

Establish the canonical BusTerminal metadata model — the durable shape every later API, UI, search projection, governance engine, drift detector, AI-enrichment pipeline, and import/export tool will consume. The slice (1) defines the in-process domain model (the shared `Resource` base shape, the 14 first-class resource types from FR-002, the relationship graph, ownership records, lifecycle states, semantic versioning, audit metadata, and the namespaced extension surface); (2) provisions an Azure Cosmos DB account with two containers — the canonical resource store and an immutable change-event log — using OpenTofu and Azure Verified Modules; (3) ships the validation framework with graded severity (Error/Warning/Info) and the rule set called out in FR-013, persisting structured findings onto each resource; (4) implements optimistic concurrency via per-resource ETag tokens (FR-025) and soft-deletion with restoration (FR-020); (5) ships System.Text.Json-based portable JSON and YamlDotNet-based YAML serialization with round-trip fidelity for identifiers, relationships, version lineage, ownership, lifecycle, extensions, and change-log linkage (FR-016); (6) delivers a representative fixture set covering every first-class resource type to drive round-trip and validation tests.

The slice ships **no API endpoints, no UI, no search index, no broker runtime, no AI enrichment**. The acceptance surface is the in-process model + persistence + validation + serialization, exercised by xUnit tests against an authoritative fixture set and a real Cosmos DB container (dev environment + Cosmos linux emulator for local).

This plan introduces **two top-level technologies** that are first uses in BusTerminal but are already named in `speckit-artifacts/tech-stack.md`: **Azure Cosmos DB** (§5 Data Platform) and **OpenTofu's `cosmosdb-account` Azure Verified Module** (§6 IaC). It introduces **one auxiliary library** as an additive extension to the existing System.Text.Json surface: **YamlDotNet** for the YAML side of FR-016 (justified below; YAML is a serialization format, not a separate "design system"). No frontend changes are introduced by this slice.

---

## Technical Context

**Language/Version**:

- Backend: C# 13 / .NET 10 (target framework `net10.0`) — inherited from 002
- IaC: OpenTofu ≥ 1.10 (HCL) — inherited from 002, with `hashicorp/azurerm` provider gaining Cosmos DB resources

**Primary Dependencies** (additions for this slice):

- **Backend (new in 004)**:
  - `Microsoft.Azure.Cosmos` (latest 3.x) — official Cosmos DB .NET SDK; the only Cosmos client used. Configured with `DefaultAzureCredential` (FR-018, inherited from 003) for AAD-based connections — no account-key secrets.
  - `YamlDotNet` (latest 16.x) — the de-facto .NET YAML library; consumed only by the import/export serializer (FR-016 / SC-009). JSON remains the canonical wire format; YAML is an alternate textual projection.
  - No new validation library — the validation framework is a small first-party engine in `BusTerminal.Api/Domain/Validation/` (a few hundred LOC). FluentValidation was considered and rejected (see research §7).
  - `System.Text.Json` (in-box with .NET 10) — used with custom polymorphic converters for the `Resource` hierarchy and the extension dictionary.
- **Backend (unchanged)**: `Microsoft.Identity.Web`, `Microsoft.Graph`, `Azure.Identity`, `Azure.Monitor.OpenTelemetry.AspNetCore`, `Serilog.AspNetCore`, `Serilog.Sinks.OpenTelemetry` — all already pinned in `BusTerminal.Api.csproj`.
- **IaC (new in 004)**:
  - `Azure/avm-res-documentdb-databaseaccount/azurerm` (Azure Verified Module for Cosmos DB account) — version pinned per §6 of tech-stack. AVM coverage exists and is mature for Cosmos DB; we use it.
  - Hand-authored child module: `iac/modules/cosmos-canonical-store/` composes the AVM account with two SQL containers (canonical + change-log) and the per-container indexing/TTL policy specific to this slice. AVM does not cover container-level composition cleanly enough to skip the wrapper.
  - No new providers (the existing `hashicorp/azurerm` provider covers Cosmos DB resources).
- **Test (new in 004)**:
  - `Microsoft.Azure.Cosmos.Emulator` Linux container — used in `docker-compose.yml` so contributors can run integration tests without an Azure subscription. The CI pipeline runs the same emulator in a service container; nightly integration tests additionally run against the dev environment's Cosmos account.
  - `FluentAssertions` (already pinned at 8.x per recent dependabot update `1c5dfff`) — assertion library for the fixture-round-trip suite.
  - `xUnit` + `WebApplicationFactory` (inherited from 002) — though no endpoints are added, `WebApplicationFactory` is reused to wire DI for integration tests.

**Storage**: **First persisted storage in BusTerminal.** This slice introduces Azure Cosmos DB as the canonical metadata store. Two containers in one database (`busterminal-canonical`):

| Container | Partition key | Purpose | Retention |
|---|---|---|---|
| `resources` | `/resourceType` | Canonical resource documents. ETag-based optimistic concurrency. Soft-delete via `isDeleted=true` predicate (live reads use a Cosmos DB query filter). | No TTL (resources persist indefinitely; soft-delete retention enforced at query layer until a later operational slice ships TTL). |
| `change-events` | `/resourceId` | Append-only change log per FR-015. One document per state change. | Configurable Cosmos TTL (default: none — retention to be configured in a later operational slice). |

The partition-key choices above are explicit "starting point" defaults per the spec's deferral of partition strategy (Assumptions §"Persistence is Azure Cosmos DB, JSON-document-oriented"). `/resourceType` on `resources` is the dominant pattern for type-scoped reads (load all queues, load all topics) — the spec's downstream "FR-014 Searchability" projection will denormalize across types anyway. `/resourceId` on `change-events` co-locates all events for a single resource onto one logical partition, which is the dominant query pattern ("history of resource X").

**Testing**:

- **Unit**: xUnit + FluentAssertions for the in-process domain model, validation rules, severity classification, lifecycle-transition graph (FR-010), concurrency-token comparisons (FR-025), serializer round-trip behavior, and the namespace inheritance resolver. No I/O.
- **Integration**: xUnit + `WebApplicationFactory` + Cosmos DB Linux emulator. Tests load the fixture set, persist it, re-read it, validate it, traverse relationships, soft-delete + restore, mutate to drive lifecycle transitions, and verify the change-event log. Uses real Cosmos partition keys, indexing, and ETag semantics.
- **Contract / Schema**: JSON Schema files in `contracts/` are validated against the same fixture set in the unit tier; a guard test asserts every serialized resource validates against its schema (preventing schema drift).
- **Round-trip serialization**: Dedicated `RoundTripTests` cover JSON → object → JSON and YAML → object → YAML for every first-class resource type in the fixture set (SC-001 evidence; SC-009 evidence).
- **CI**: Existing CI gates (build, unit, integration, lint, format, gitleaks, dependency vuln scan, tofu validate/plan/tflint/checkov) are extended with: emulator-backed integration test service container, contract-schema validation, and Cosmos `checkov` rules (encryption at rest is on by default in AVM; verify with `checkov` policy `CKV_AZURE_140`).
- **Smoke**: A `dotnet run --project tools/load-fixtures` command loads the fixture set into a target Cosmos account (emulator or dev) and runs the validation framework. Used by the quickstart and by the developer onboarding flow.

**Target Platform**: Linux containers on Azure Container Apps for deployed (inherited). Linux x64 / macOS arm64 / macOS x64 / Windows x64 for local dev. The Cosmos Linux emulator (Docker image `mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:vnext-preview`) supports all three architectures.

**Project Type**: Backend service + infrastructure as code. No frontend changes. No new top-level component types introduced.

**Performance Goals**:

- Single-resource read (point lookup by id + partition key): ≤ 10 ms p95 against dev Cosmos (Cosmos point-read SLO; we inherit it).
- Single-resource write with optimistic-concurrency check: ≤ 20 ms p95 against dev Cosmos.
- Validation framework execution per resource (full rule set): ≤ 5 ms p95 in-process — validation is pure CPU, no I/O.
- Fixture-set bulk load (one of each first-class type — ~30 documents): ≤ 5 seconds end-to-end against emulator.
- Change-event append on every write: amortized ≤ 5 ms p95 added to write latency (separate point-insert into `change-events`).
- Serializer round-trip for a 50-KB resource document: ≤ 20 ms p95 for JSON, ≤ 50 ms p95 for YAML.

These are dev-environment / emulator targets. The spec defers scale envelope (Assumptions); we adopt Cosmos's published SLOs as the working baseline.

**Constraints**:

- **OpenTofu only.** Cosmos DB is provisioned via OpenTofu + AVM. No Bicep, no portal-clickops, no Azure CLI scripted creation. (Constitution §Hosting / tech-stack §6.)
- **Managed Identity for SDK auth.** `Microsoft.Azure.Cosmos.CosmosClient` is constructed with `DefaultAzureCredential`. No account keys in source, configuration, or container env. The Container App's UAMI (inherited from 003) gets the Cosmos DB built-in role `Cosmos DB Built-in Data Contributor` on the canonical store. (tech-stack §7 — Managed Identity preferred over secrets.)
- **No secrets in any document.** Every persisted resource document is scanned by an integration-tier assertion against a denylist (`password`, `secret`, `connectionstring`, `accesskey`, `sastoken`, `bearer`, `apikey`) — SC-011.
- **W3C Trace Context propagation** continues to apply to every UI- or workload-originated call once API endpoints are introduced in later slices. This slice does not introduce any HTTP boundary, but the Cosmos SDK is configured with the OpenTelemetry instrumentation so persistence operations emit spans into the existing OTel → App Insights pipeline. (Constitution §V Operational Excellence.)
- **No PII in telemetry** beyond correlation ids. Resource documents may contain owner display names (which are PII), but those fields are NOT emitted to traces or metrics. The persistence-layer OTel span attributes are limited to `cosmos.operation`, `cosmos.container`, `cosmos.partition_key_value`, and `cosmos.status_code` — never the document body.
- **Vertical Slice Architecture remains the preferred pattern for feature code.** The domain model itself is a cross-cutting platform construct (same justification 003 used for `Authorization/`); it lives in `BusTerminal.Api/Domain/` alongside the existing `Authorization/` and `Infrastructure/` cross-cutting namespaces. No new top-level project is created.
- **Modular monolith first.** The domain model is consumed by future feature slices via direct project reference; no internal HTTP API, no message bus, no shared library extracted to NuGet. Decomposition is deferred until a workload's deployment-independence requirement justifies it.
- **AVM where it covers cleanly.** Cosmos DB account uses AVM. Container-level composition is wrapped in a thin first-party module because AVM's container surface is awkward for our multi-container, per-container indexing-policy needs (see research §3).
- **Encryption at rest** is on by default in Cosmos DB (AVM-confirmed) — we do not opt out and do not introduce customer-managed keys in this slice (no compliance requirement is on the table yet).
- **Soft delete is application-level**, not Cosmos-level. Cosmos DB's "continuous backup" feature is enabled (AVM default) but is NOT the soft-delete mechanism — application-level `isDeleted=true` + a query-time filter is the FR-020 mechanism. This preserves identifier and relationship lineage explicitly per the spec.

**Scale/Scope**:

- Dev environment: estimated ~50 fixtures + ~10 ad-hoc test resources; well under Cosmos's free-tier 400 RU/s per container.
- Backend code added (this slice): ~25 new `.cs` files — the 14 resource type records + base + relationship + ownership + audit + version + change event + validation engine + 7 validation rules + Cosmos persistence adapter + serializer + extension dictionary + namespace path resolver + concurrency token + fixture loader. Estimated **~3,000 net LOC including tests**.
- IaC added (this slice): 2 new modules (`cosmos-account` thin wrapper + `cosmos-canonical-store`) + environment wiring + RBAC role assignments. Estimated **~250 net HCL LOC**.
- Tests added: ~80 unit tests + ~25 integration tests + 1 fixture set with one of each first-class type (~30 resources covering all relationship patterns).

---

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-evaluated after Phase 1 design (below).*

| Principle | Status | Evidence |
|-----------|--------|----------|
| **I. Azure-First Architecture** | ✅ Pass | Cosmos DB (Azure-native), AVM (Azure-native modules), Managed Identity (Azure-native auth), OpenTelemetry → Azure Monitor (Azure-native observability). No multi-cloud abstraction is introduced — the persistence adapter is a thin Cosmos-specific class, not a database-portability layer (which would be premature). |
| **II. API-First Design** | ⚪ N/A (this slice) | No public API endpoints are added — explicitly out of scope per spec. The model is consumed in-process by future API slices. When those slices arrive, the model's JSON serialization shape IS the on-the-wire contract, so we publish the schemas in `contracts/` now so future API specs reference them rather than re-defining them (compliance preserved by design). |
| **III. Strong Domain Modeling** | ✅ Pass — *this is the slice that operationalizes Principle III* | The 14 first-class messaging entities from the spec (Namespace, Broker, Queue, Topic, Subscription, Message Contract, Producer/Consumer Application, Team, Environment, Tag, Policy, Integration Flow, Documentation Asset) are codified as canonical domain types. Naming is consistent across the in-process model, the persistence layer, the serialization schemas, and the validation framework. Future search index, API, and UI MUST use the same names — verified in `data-model.md` §Naming Cross-Reference. |
| **IV. Security by Default** | ✅ Pass | No secrets in source (FR-024). Managed Identity for Cosmos auth (no account keys). Encryption at rest on by default (Cosmos + AVM). `DefaultAzureCredential` factory inherited from 003. No payload history persisted (FR-024). Secret-pattern denylist in integration tests (SC-011). Private networking on the Cosmos account is deferred to a later operational slice — see Complexity Tracking below for the explicit deviation. |
| **V. Operational Excellence** | ✅ Pass | Cosmos SDK is wired into the existing OTel pipeline; every persistence operation emits a span. Change-event log (FR-015) is itself an operational visibility surface — "who changed what when" queryable for incident response without leaving the registry. Structured validation findings (FR-013) are queryable governance data. No silent retries — `CosmosException`s are surfaced; transient-failure retry policy is the Cosmos SDK default (3 attempts with backoff), with the final failure logged at Error severity. |
| **VI. Incremental Extensibility** | ✅ Pass | Closed enum + opaque-string type field (Q4 clarification, FR-002) means new first-class types are added by future slices without retroactive migration. Namespaced extensions (FR-012) let orgs extend without forking. Relationship graph is open across resource types so new types compose into the graph automatically. Schema additive evolution is enforced by integration-tier compatibility tests. |
| **Modular Monolith First** | ✅ Pass | The domain model lives inside `BusTerminal.Api`; no new project, no new service, no internal HTTP boundary. |
| **Container-Native** | ✅ Pass | Cosmos emulator runs as a sidecar in `docker-compose.yml`; the API container talks to it via the same connection abstraction as production. |
| **Async-First Thinking** | ✅ Pass | Cosmos SDK is async-first; every persistence operation in our codebase is `Task<T>`-returning. Validation framework is synchronous (pure CPU); change-event append is async (it's a second Cosmos write). |
| **AI-Assisted Capability Enablement** | ⚪ N/A (this slice) | No AI features are introduced. The Info-severity validation finding is the surface that future AI enrichment will consume (FR-013) — extensibility preserved. |
| **CI/CD Requirements** | ✅ Pass | Existing CI gates apply. New gates: Cosmos emulator service container for the integration tier; contract-schema validation; `checkov` policy for Cosmos encryption at rest. |
| **Testing Standards** | ✅ Pass | Unit + integration + contract + round-trip + fixture-load smoke. Tests assert observable behavior (round-trip equivalence, validation findings, relationship traversal, ETag conflict rejection, lifecycle transition rejection) — not implementation detail. |
| **AI Tooling / MCP Usage** | ✅ Pass | Implementation tasks will cite Microsoft Learn MCP (Cosmos DB SDK, Cosmos AVM, partition-key best practices), context7 MCP (System.Text.Json polymorphism, YamlDotNet), and Microsoft Learn for `DefaultAzureCredential` + Cosmos AAD-RBAC. No frontend work, so shadcn/ui and Next.js MCPs are not engaged. |

**Gate decision**: PASS with one new deviation recorded in Complexity Tracking (private networking on the Cosmos account is deferred — see below). Every other principle is upheld; this slice **strengthens** Principle III by operationalizing the domain model the constitution mandates.

---

## Project Structure

### Documentation (this feature)

```text
specs/004-core-domain-model/
├── plan.md                                       # This file
├── research.md                                   # Phase 0 output (this run)
├── data-model.md                                 # Phase 1 output (this run)
├── quickstart.md                                 # Phase 1 output (this run)
├── contracts/                                    # Phase 1 output (this run)
│   ├── canonical-resource.schema.json            # Base JSON Schema for every first-class resource
│   ├── resources/                                # Per-type JSON Schemas — one per first-class type
│   │   ├── namespace.schema.json
│   │   ├── broker.schema.json
│   │   ├── queue.schema.json
│   │   ├── topic.schema.json
│   │   ├── subscription.schema.json
│   │   ├── message-contract.schema.json
│   │   ├── producer-application.schema.json
│   │   ├── consumer-application.schema.json
│   │   ├── team.schema.json
│   │   ├── environment.schema.json
│   │   ├── tag.schema.json
│   │   ├── policy.schema.json
│   │   ├── integration-flow.schema.json
│   │   └── documentation-asset.schema.json
│   ├── relationship.schema.json                  # Edge document — direction, type, source/target, metadata
│   ├── change-event.schema.json                  # Immutable change-log entry per FR-015
│   ├── validation-result.schema.json             # Structured validation findings per resource
│   ├── ownership.schema.json                     # Reusable ownership record
│   ├── audit.schema.json                         # Reusable audit metadata block
│   ├── version-info.schema.json                  # Reusable semantic-version block
│   ├── extension.schema.json                     # Namespaced extension surface
│   ├── import-export-envelope.schema.json        # Portable export format (JSON; YAML uses the same logical shape)
│   ├── relationship-types.md                     # Enumerated relationship-type vocabulary + directionality
│   └── lifecycle-transitions.md                  # Legal-transition graph from FR-010 + restoration semantics
├── checklists/
│   └── requirements.md                           # Existing — from /speckit-specify
└── tasks.md                                      # NOT created here — /speckit-tasks output
```

### Source Code (repository root)

```text
/api/                                             # .NET 10 backend — inherited from 002/003
  BusTerminal.Api/
    Program.cs                                    # MODIFIED — registers ICanonicalResourceStore, IChangeEventLog, IValidationEngine, IResourceSerializer, IRelationshipGraph
    Domain/                                       # NEW — cross-cutting canonical domain model (sibling of Authorization/)
      ResourceType.cs                             # NEW — closed enum of the 14 known types (FR-002, Q4 clarification)
      ResourceTypeRegistry.cs                     # NEW — known-type lookup; unknown strings deserialize as UnknownResource placeholder
      LifecycleState.cs                           # NEW — { Draft, Active, Deprecated, Retired, Archived }
      LifecycleTransitions.cs                     # NEW — legal-transition graph from FR-010 + IsTransitionLegal()
      EnvironmentClassification.cs                # NEW — { Development, Test, QA, Staging, Production, DisasterRecovery, Custom(string) } — extensible
      ResourceId.cs                               # NEW — strongly-typed wrapper around Guid; FR-021
      ResourceName.cs                             # NEW — value type; enforces FR-022 naming rules (lowercase, hyphen-separated, no spaces)
      NamespacePath.cs                            # NEW — / -separated hierarchical path; parent-chain resolution
      ConcurrencyToken.cs                         # NEW — opaque ETag wrapper; FR-025
      SemanticVersion.cs                          # NEW — major.minor.patch + compatibility metadata; FR-011
      OwnershipRecord.cs                          # NEW — Team ref + technical/business contacts + escalation + support + operational tier; FR-009
      AuditRecord.cs                              # NEW — created-by/at + modified-by/at + source-system + sync metadata; FR-015 latest-state
      TagReference.cs                             # NEW — value type for in-document references to TagResource entries (renamed from Tag per analysis N1)
      Extensions.cs                               # NEW — IReadOnlyDictionary<string, JsonElement> surface; namespaced keys; FR-012
      Resource.cs                                 # NEW — abstract base type; FR-001
      Resources/                                  # NEW — one record per first-class resource type, inheriting Resource
        Namespace.cs                              # FR-003
        Broker.cs                                 # FR-002
        Queue.cs                                  # FR-004
        Topic.cs                                  # FR-005
        Subscription.cs                           # FR-006
        MessageContract.cs                        # FR-007
        ProducerApplication.cs                    # FR-002
        ConsumerApplication.cs                    # FR-002
        Team.cs                                   # FR-009 referent
        EnvironmentResource.cs                    # FR-017
        TagResource.cs                            # FR-002 (Tag-as-resource for taxonomy management; distinct from the tag value type above)
        Policy.cs                                 # FR-002
        IntegrationFlow.cs                        # FR-002
        DocumentationAsset.cs                     # FR-002
        UnknownResource.cs                        # NEW — placeholder for documents whose ResourceType is not in the known registry (Q4)
      Relationships/
        Relationship.cs                           # NEW — source/target/type/direction/annotations/validation; FR-008
        RelationshipType.cs                       # NEW — enum of known relationship-type vocabulary
        RelationshipGraph.cs                      # NEW — in-process traversal helpers (BFS with cycle protection)
      Lifecycle/
        SoftDelete.cs                             # NEW — FR-020 application-level soft-delete primitive
        ChangeEvent.cs                            # NEW — immutable change-log entry; FR-015 + audit log
        ChangeEventType.cs                        # NEW — { Created, Updated, LifecycleTransitioned, SoftDeleted, Restored }
      Validation/
        IValidationRule.cs                        # NEW — base rule abstraction
        ValidationFinding.cs                      # NEW — { RuleId, Severity, Message, FieldRef, EvaluatedAt }
        ValidationSeverity.cs                     # NEW — { Error, Warning, Info } (Q3 clarification)
        ValidationResult.cs                       # NEW — IReadOnlyCollection<ValidationFinding> + helpers
        ValidationEngine.cs                       # NEW — orchestrates rule execution per resource type
        Rules/
          RequiredFieldsRule.cs                   # FR-013
          NamingStandardsRule.cs                  # FR-013 + FR-022
          DanglingReferenceRule.cs                # FR-013 + FR-008
          DuplicateDetectionRule.cs               # FR-013
          LifecycleTransitionRule.cs              # FR-013 + FR-010 (Q1)
          OwnershipPresenceRule.cs                # FR-013 + FR-009
          ContractCompatibilityRule.cs            # FR-013 + FR-007 (metadata-only check — pluggable validators are deferred)
          UnknownResourceTypeRule.cs              # FR-002 + Q4 — emits Info finding when an UnknownResource is encountered
      Serialization/
        IResourceSerializer.cs                    # NEW — JSON/YAML round-trip surface; FR-016
        JsonResourceSerializer.cs                 # NEW — System.Text.Json with polymorphic + extension-aware converters
        YamlResourceSerializer.cs                 # NEW — YamlDotNet-backed; same logical shape as JSON
        ImportExportEnvelope.cs                   # NEW — wraps a set of resources + relationships for portable export
        ResourceJsonConverter.cs                  # NEW — polymorphic discriminator on resourceType
        ExtensionsJsonConverter.cs                # NEW — preserves structured JSON in the Extensions dictionary
      NamespaceInheritance.cs                     # NEW — resolves inherited governance/ownership metadata per FR-003
    Infrastructure/
      Persistence/                                # NEW — Cosmos DB adapter for the canonical model
        ICanonicalResourceStore.cs                # NEW — CRUD + soft-delete + restore + concurrency-aware writes
        CosmosCanonicalResourceStore.cs           # NEW — Microsoft.Azure.Cosmos impl; partition by /resourceType; ETag-based optimistic concurrency
        IChangeEventLog.cs                        # NEW — append + query-by-resource surface
        CosmosChangeEventLog.cs                   # NEW — separate Cosmos container; partition by /resourceId
        CosmosClientFactory.cs                    # NEW — DefaultAzureCredential-backed CosmosClient construction; integrates with IAzureCredentialFactory from 003
        CosmosOptions.cs                          # NEW — bound configuration (account endpoint, database name, container names)
        ConcurrencyExceptionMapper.cs             # NEW — translates Cosmos 412 PreconditionFailed → ConcurrencyConflictException
      Configuration/
        CosmosConfigurationExtensions.cs          # NEW — DI wiring for CosmosOptions + factory + store
    Fixtures/                                     # NEW — first-party fixture set; tools/load-fixtures imports every *.json in lexicographic order so stories can add per-concern files without merge conflicts (per analysis F1)
      01-base.json                                # NEW (US1) — one of each first-class type
      02-relationships.json                       # NEW (US3) — relationship-graph cluster matching the FR-008 example
      03-contracts.json                           # NEW (US4) — multi-format contracts + version-history fixture
      04-extensions.json                          # NEW (US6) — patch-style extension overlay with multi-vendor coexistence
      05-environments.json                        # NEW (US7) — patch-style env-association overlay with all six minimum envs on a single queue
  BusTerminal.Api.Tests/
    Unit/
      Domain/
        ResourceTypeRegistryTests.cs              # Q4 — known vs unknown type behavior
        LifecycleTransitionsTests.cs              # Q1 — legal/illegal transition matrix
        NamespacePathTests.cs                     # FR-003 — parent chain, normalization, validation
        ResourceNameTests.cs                      # FR-022 — naming rules
        OwnershipRecordTests.cs                   # FR-009 — required fields, identifier resolution
        SemanticVersionTests.cs                   # FR-011 — comparison, compatibility
        ExtensionsTests.cs                        # FR-012 — structured value preservation, exclusion-from-index marker
      Validation/
        ValidationEngineTests.cs                  # Q3 — Error blocks, Warning records, Info advisory
        Rules/                                    # one test class per rule in Domain/Validation/Rules
          RequiredFieldsRuleTests.cs
          NamingStandardsRuleTests.cs
          DanglingReferenceRuleTests.cs
          DuplicateDetectionRuleTests.cs
          LifecycleTransitionRuleTests.cs
          OwnershipPresenceRuleTests.cs
          ContractCompatibilityRuleTests.cs
          UnknownResourceTypeRuleTests.cs
      Serialization/
        JsonRoundTripTests.cs                     # SC-001 — every first-class type round-trips JSON losslessly
        YamlRoundTripTests.cs                     # SC-009 — every first-class type round-trips YAML losslessly
        PolymorphismTests.cs                      # Discriminator-based deserialization across the 14 types + UnknownResource
        ExtensionPreservationTests.cs             # Q4 + FR-012 — structured extension values survive round-trip
      Relationships/
        RelationshipGraphTests.cs                 # FR-008 — multi-hop traversal, direction enforcement, cycle protection
    Integration/
      Persistence/
        CanonicalStoreIntegrationTests.cs         # CRUD + ETag conflict + soft-delete + restore against emulator
        ChangeEventLogIntegrationTests.cs         # SC-012 — full ordered change-event sequence per resource
        FixtureLoadAndQueryTests.cs               # SC-001 — load fixture set, traverse relationships, evaluate validation
        SecretScanGuardTests.cs                   # SC-011 — denylist scan over persisted documents
        SoftDeleteRetentionTests.cs               # SC-005 — restore, audit metadata, change-event linkage
      Validation/
        EndToEndValidationTests.cs                # SC-008 — Error/Warning/Info findings stored as structured metadata

/iac/                                             # OpenTofu — inherited from 002/003
  modules/
    cosmos-account/                               # NEW — thin wrapper around Azure/avm-res-documentdb-databaseaccount; pins AVM version + opinionated defaults (encryption at rest on; serverless capacity for dev; AAD-only data access)
    cosmos-canonical-store/                       # NEW — composes the account + the two containers (resources, change-events); declares per-container indexing + partition key + (future) TTL
  environments/
    dev/
      main.tf                                     # MODIFIED — wires the new cosmos modules; grants the API UAMI the "Cosmos DB Built-in Data Contributor" role on the canonical store
      terraform.tfvars                            # MODIFIED — adds cosmos_account_name, canonical_db_name

/docker-compose.yml                               # MODIFIED — adds cosmos emulator service for local integration tests

/docs/                                            # MODIFIED
  domain-model.md                                 # NEW — high-level architectural overview of the canonical model, links to spec + data-model + contracts
  cosmos-operations.md                            # NEW — operator runbook: how the canonical store is structured, partition strategy, troubleshooting concurrency conflicts, soft-delete behavior, change-log queries

/tools/                                           # NEW (or modify if already exists)
  load-fixtures/                                  # NEW — `dotnet run` CLI that imports every envelope under Fixtures/ into a target Cosmos account (emulator or dev) in lexicographic filename order
    LoadFixtures.csproj
    Program.cs

/CLAUDE.md                                       # MODIFIED — SPECKIT marker block repointed to this plan
```

**Structure Decision**:

The canonical domain model is a cross-cutting platform concern, not a feature. The 003 plan already established the precedent for cross-cutting platform code: `BusTerminal.Api/Authorization/` lives alongside `Features/` and `Infrastructure/`. This slice extends the same pattern with `BusTerminal.Api/Domain/` (the in-process model) and `BusTerminal.Api/Infrastructure/Persistence/` (the Cosmos adapter). Vertical Slice Architecture continues to govern **feature** code — there are no features in this slice, only cross-cutting platform constructs.

No separate `BusTerminal.Domain` project is extracted. Per "Modular Monolith First," a project split is justified only when there's a deployment-independence requirement; there isn't one yet. A future event-driven processing slice (containerized Azure Function) will be the first non-API consumer of the model — at that point, extraction can be revisited as an ADR if direct project reference proves insufficient. For now, internal direct reference is cheaper.

The fixture set lives in `BusTerminal.Api/Fixtures/` (a runtime asset embedded as content for the load-fixtures tool) rather than only in the test project, because operator runbook usage (`dotnet run --project tools/load-fixtures`) needs it at runtime, not just at test time.

---

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| **Cosmos DB account is public-endpoint (no Private Link / VNet integration) in this slice** | The 003 slice did not provision VNet infrastructure. Adding Private Link + Cosmos private endpoint + Container Apps Environment VNet integration is a multi-day infrastructure change that is genuinely separable from the domain-model work and would gate the slice on unrelated networking design. AAD-only data plane access (no account keys) plus encryption at rest are the substantive security mitigations; the public endpoint accepts only AAD-authenticated requests. | Provisioning VNet + private endpoint + DNS zones + Container Apps VNet integration in this slice would balloon scope by ~500 HCL LOC, force a redeploy of the Container Apps Environment (002 inheritance), and is a clean operational slice on its own. Recorded as an explicit deviation; a follow-up operational slice should add private networking before the registry holds production data. Mitigation: AAD-only access, Managed Identity, encryption at rest, denylist scan for credential-shaped fields, IP firewall on the Cosmos account restricting writes to the Container Apps Environment outbound IPs + the developer access IP allowlist. |

**Post-implementation re-check (T157 / 2026-05-25)**: No new constitutional deviations surfaced during Phase 3–11 implementation. The single Complexity Tracking entry above (public-endpoint Cosmos with AAD-only access) is verified in the deployed dev-env state: `cosmos-bt-dev-chdev01` provisioned with `local_authentication_disabled = true`, encryption at rest on by default, and AAD-only data-plane access. The Phase 11 quickstart Path B run (T156) wrote and read the fixture set via `DefaultAzureCredential` end-to-end against the live account.

Implementation surfaced three CODE defects that were fixed inline during Phase 11 (none constitutional):

1. **`tools/load-fixtures` ImportCommand** passed `relationshipResolver: _ => null` (a US1-vintage placeholder) which broke fixture load after US3's `DanglingReferenceRule` landed — every cross-file reference was rejected as dangling. Fixed by pre-deserializing the envelope set and resolving against the in-memory union map.
2. **`tools/load-fixtures` TruncateCommand** raced with concurrent deletes (test-fixture teardown, partial prior truncate) and 404'd on the second delete attempt. Fixed by swallowing per-document `HttpStatusCode.NotFound` in the delete loop — truncate is now idempotent.
3. **`iac/environments/dev/main.tf`** built Cosmos data-plane role-assignment `scope` from the SQL database's ARM resource id (`.../sqlDatabases/<db>`); the Cosmos provider expects the data-plane path shape (`.../dbs/<db>`) and returned HTTP 400. Fixed by constructing the scope from the account id + database name in a `locals` block; `cosmos-canonical-store` module's `database_id` output now carries a usage-warning comment.

Two minor documentation drifts captured for follow-up (not blocking):
- `quickstart.md` Path A expected output names "~2 Warning, ~5 Info" findings; the shipped fixtures produce 0/0/0. Either tighten the fixtures to trip the deprecation/soft-delete-target rules or update the quickstart text. Documentation-only.
- `quickstart.md` Path B § B.4 calls `az cosmosdb sql query` which is not a built-in subcommand in current `azure-cli` (2.86.0). Replace with the `load-fixtures show` and `load-fixtures changelog` verbs or document the required extension. Documentation-only.

---

## Phase 0 (research.md) — completed in this run

See [research.md](./research.md). Eleven research topics resolved:

1. **Cosmos DB SDK choice** → `Microsoft.Azure.Cosmos` 3.x (the official .NET SDK). Earlier 2.x and the V4 preview were rejected; 3.x is the supported mainline.
2. **Cosmos DB authentication** → `DefaultAzureCredential` via the AAD data-plane (AAD-RBAC role: `Cosmos DB Built-in Data Contributor`). No account keys. The UAMI from 003 is the consumer.
3. **Cosmos container composition via AVM** → Use AVM for the account; wrap container declarations in a thin first-party module. AVM coverage of container-level indexing and TTL is workable but awkward for a multi-container, per-container-policy story.
4. **Partition key for the canonical container** → `/resourceType` is the starting-point default — co-locates all queues, all topics, etc., which matches the dominant type-scoped read pattern. The spec defers final partition strategy; a later operational slice may rebalance.
5. **Partition key for the change-event container** → `/resourceId` — co-locates all events for a single resource. Matches the dominant "history of resource X" query pattern.
6. **Polymorphic JSON serialization for the Resource hierarchy** → `System.Text.Json` polymorphic converters with a `resourceType` discriminator. STJ's `JsonPolymorphic` attribute supports this natively in .NET 10. Newtonsoft.Json was rejected (no need for a second JSON library; STJ covers every requirement).
7. **Validation framework: build vs adopt** → Build a small first-party engine. FluentValidation was considered and rejected for two reasons: (a) the rule set is small and bespoke, (b) graded severity (Q3) is not a first-class FluentValidation primitive, requiring shim code that would be larger than a first-party engine.
8. **YAML library choice** → `YamlDotNet` 16.x. The de-facto .NET YAML library; trusted; active maintenance; supports the round-trip semantics SC-009 requires.
9. **Cosmos emulator for local dev** → The Linux container emulator (preview-but-stable) supports macOS arm64. Add as a `docker-compose.yml` service. The Windows emulator is rejected (won't run on the macOS/Linux dev contingent without WSL2 or a VM).
10. **Optimistic concurrency mechanism** → Cosmos's native ETag. The SDK exposes ETags on every `ItemResponse`; the IfMatch precondition rejects stale writes with a `412 PreconditionFailed`. We map that to `ConcurrencyConflictException` (Q2 clarification → FR-025).
11. **Soft-delete query filter** → Application-level `isDeleted=true` predicate + a query-time filter. Cosmos TTL is NOT used for soft-delete (the spec requires retention with the change-log intact). Retention duration is configurable in a future operational slice.

---

## Phase 1 — completed in this run

- **[data-model.md](./data-model.md)** documents the logical model at design granularity: base `Resource` shape, per-type discriminators, relationship edges, ownership/audit/version blocks, change-event log, validation result shape, extensions, lifecycle transition graph (Q1), concurrency tokens (Q2), validation severity (Q3), unknown-type behavior (Q4), and the change-event log structure (Q5). Naming cross-reference table proves consistency across in-process types, persisted documents, JSON schemas, and the relationship vocabulary.
- **[contracts/](./contracts/)** publishes the JSON Schemas for every first-class resource, the shared blocks (ownership, audit, version, extension), the relationship document, the change event, the validation result, and the import/export envelope. Two companion markdown files (`relationship-types.md`, `lifecycle-transitions.md`) document the controlled vocabularies. These schemas ARE the future API on-the-wire contract — downstream API specs will reference them rather than redefine them.
- **[quickstart.md](./quickstart.md)** is the developer runbook: provision Cosmos (Tofu apply OR docker-compose up the emulator), grant the UAMI the data-contributor role, run `dotnet run --project tools/load-fixtures`, verify the fixture set materializes, run the validation framework against it, traverse a producer→consumer relationship path, soft-delete + restore a topic, and confirm change-event log entries. The runbook maps each step to one or more SC outcomes.

Agent context update: `CLAUDE.md`'s SPECKIT marker block is repointed to this plan (`specs/004-core-domain-model/plan.md`).

---

## Post-Phase-1 Constitution Re-Check

| Principle | Status After Design |
|-----------|---------------------|
| I. Azure-First | ✅ Confirmed — Cosmos DB, AVM, Managed Identity, OTel → Azure Monitor. No portability shim. |
| II. API-First | ⚪ N/A this slice; preserved by publishing JSON Schemas in `contracts/` for future API consumers. |
| III. Strong Domain Modeling | ✅ **Operationalized** — the 14 first-class types are codified with consistent naming across in-process, persisted, schema, and documentation surfaces (verified in `data-model.md` Naming Cross-Reference table). |
| IV. Security by Default | ✅ Strengthened (no secrets persisted; AAD-only data plane; encryption at rest) with **one explicit deferral** (private networking) recorded in Complexity Tracking. |
| V. Operational Excellence | ✅ Confirmed — OTel-instrumented persistence; change-event log as operational visibility surface; structured validation findings; explicit failure surfacing (no silent retries beyond Cosmos SDK defaults). |
| VI. Incremental Extensibility | ✅ Confirmed — closed-enum + opaque-string type field; namespaced extensions; open relationship graph; additive-evolution-friendly persisted shape. |
| Modular Monolith First | ✅ Confirmed — single project, no decomposition. |
| Container-Native | ✅ Confirmed — Cosmos emulator runs in `docker-compose.yml`; same connection abstraction in dev and prod. |
| Async-First | ✅ Confirmed — persistence is async; change-event append is async; validation is sync (pure CPU). |
| AI-Assisted | ⚪ N/A this slice; Info-severity findings are the surface future AI enrichment will consume. |
| CI/CD Requirements | ✅ Confirmed — emulator service container + contract-schema validation + Cosmos `checkov` gate added. |
| Testing Standards | ✅ Confirmed — unit + integration + contract + round-trip + smoke + fixture-load coverage planned; assertions are observable-behavior-shaped. |
| AI Tooling / MCP Usage | ✅ Confirmed — Microsoft Learn MCP (Cosmos SDK, AVM, AAD-RBAC, partition-key best practices) and context7 MCP (System.Text.Json polymorphism, YamlDotNet) are cited in research and will be cited again in task descriptions. |

**Gate decision after Phase 1**: PASS. The single Complexity Tracking entry (public-endpoint Cosmos with AAD-only access) is the only deviation and is mitigated; no new violations were introduced by the design phase. Plan is ready for `/speckit-tasks`.
