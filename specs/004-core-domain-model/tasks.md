---

description: "Task list for Core Domain Model (spec 004)"
---

# Tasks: Core Domain Model

**Input**: Design documents from `specs/004-core-domain-model/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/ (24 schemas + 2 vocabulary docs), quickstart.md (all present)

**Tests**: This slice's spec defines explicit Independent Tests per user story (P1–P3) and twelve measurable Success Criteria (SC-001 through SC-012). The plan calls out unit + integration + contract + round-trip + smoke tiers explicitly. Test tasks are included accordingly.

**Organization**: Tasks are grouped by user story per `tasks-template.md`. Foundational tasks (Phase 2) deliver the platform primitives (Cosmos infrastructure + base domain types + serialization framework + validation engine shell + change-event log) every story depends on. Story-specific tasks (Phases 3–10) deliver each user story independently. Polish (Phase 11) handles cross-cutting concerns.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: User story label (US1–US8) for phase-3-onwards tasks; omitted in Setup, Foundational, Polish
- All file paths are repo-relative

## Path Conventions (per plan.md)

- Backend: `api/BusTerminal.Api/`, tests in `api/BusTerminal.Api.Tests/`
- IaC: `iac/modules/`, `iac/environments/`
- Tools: `tools/load-fixtures/`
- Docs: `docs/`
- Spec / contracts: `specs/004-core-domain-model/contracts/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Add the new packages, IaC providers, and emulator wiring this slice depends on. No business logic.

- [ ] T001 [P] Add `Microsoft.Azure.Cosmos` (latest 3.x) NuGet package reference to `api/BusTerminal.Api/BusTerminal.Api.csproj`. Consult Microsoft Learn MCP for the current published version and any transitive override warnings (mirror the pattern used in 003's `Microsoft.Graph.Core` override comment).
- [ ] T002 [P] Add `YamlDotNet` (latest 16.x) NuGet package reference to `api/BusTerminal.Api/BusTerminal.Api.csproj`. Consult context7 MCP for the current YamlDotNet builder API surface so the import in T072 (US8 YAML serializer) matches.
- [ ] T003 [P] Create `tools/load-fixtures/LoadFixtures.csproj` as a `Microsoft.NET.Sdk` console project targeting `net10.0` with a project reference to `api/BusTerminal.Api/BusTerminal.Api.csproj` so it can use the domain model + persistence adapter directly. Add `<RootNamespace>BusTerminal.Tools.LoadFixtures</RootNamespace>`.
- [ ] T004 [P] Create `tools/load-fixtures/Program.cs` with a minimal `System.CommandLine`-style host scaffold (subcommands stubbed: `create-database`, `import`, `export`, `show`, `show-owner`, `traverse`, `transition`, `soft-delete`, `restore`, `changelog`, `truncate`). Subcommand bodies throw `NotImplementedException` for now; later tasks wire each one.
- [ ] T005 Modify `docker-compose.yml` at repo root to add a `cosmos-emulator` service using image `mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:vnext-preview` with port `8081` published, `AZURE_COSMOS_EMULATOR_PARTITION_COUNT=4` env var, and a healthcheck that hits the emulator's `/_explorer/emulator.pem` endpoint. Document that the well-known emulator key is intentional public test data per `research.md` §2.
- [ ] T006 [P] Update `api/BusTerminal.Api/appsettings.Development.json.example` to add the `Cosmos` section: `Endpoint`, `Database` (`busterminal-canonical`), `Containers:Resources` (`resources`), `Containers:ChangeEvents` (`change-events`), `LocalEmulatorKey` (well-known emulator key). Document the env-var precedence rule in a sibling comment block.
- [ ] T007 Update `.gitignore` to add `tools/load-fixtures/bin`, `tools/load-fixtures/obj`. Update `BusTerminal.sln` (if present) to add the `tools/load-fixtures/LoadFixtures.csproj` project. If no solution file, document the new project in `docs/local-development.md`.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Platform primitives — Cosmos infrastructure, base domain value types, abstract `Resource`, change-event log, validation engine framework, JSON serialization framework, persistence adapter. **No user-story task can begin until this phase is complete.**

**⚠️ CRITICAL**: This phase defines the entire chassis the 14 resource types and all subsequent stories plug into. Get it wrong and every later phase rebases.

### Cosmos infrastructure (OpenTofu)

- [ ] T008 Create `iac/modules/cosmos-account/main.tf` that wraps `Azure/avm-res-documentdb-databaseaccount/azurerm` at the version pinned via `versions.tf`. Inputs: `name`, `resource_group_name`, `location`, `tags`. Opinionated defaults: encryption at rest on, serverless capacity mode for dev, AAD-only data access (disable local auth via `local_authentication_disabled = true`), automatic-failover off (dev only). Consult Microsoft Learn MCP for the AVM input shape and the `local_authentication_disabled` argument name. Outputs: `account_endpoint`, `account_name`, `account_id`.
- [ ] T009 [P] Create `iac/modules/cosmos-account/variables.tf` and `iac/modules/cosmos-account/outputs.tf` mirroring T008's inputs/outputs.
- [ ] T010 [P] Create `iac/modules/cosmos-account/versions.tf` pinning `terraform >= 1.10`, `azurerm ~> 4.x` (match the version pinned by 002/003), and the AVM source pinned explicitly.
- [ ] T011 Create `iac/modules/cosmos-canonical-store/main.tf` that takes a `cosmos_account_id` input and provisions: one `azurerm_cosmosdb_sql_database` (`busterminal-canonical`), one `azurerm_cosmosdb_sql_container` named `resources` with partition key `/resourceType` and an indexing policy that excludes `/extensions/*` paths (per FR-012 per-extension indexing control), and one `azurerm_cosmosdb_sql_container` named `change-events` with partition key `/resourceId` and a minimal indexing policy that indexes only `/resourceId`, `/timestamp`, `/eventType`. Consult Microsoft Learn MCP for the indexing-policy schema.
- [ ] T012 [P] Create `iac/modules/cosmos-canonical-store/variables.tf` and `outputs.tf`. Outputs: `database_name`, `resources_container_name`, `change_events_container_name`, `database_id`.
- [ ] T013 [P] Create `iac/modules/cosmos-canonical-store/versions.tf` matching T010.
- [ ] T014 Modify `iac/environments/dev/main.tf` to instantiate `module.cosmos_account` and `module.cosmos_canonical_store`, and to add an `azurerm_cosmosdb_sql_role_assignment` granting the API UAMI (inherited from 003) the built-in `Cosmos DB Built-in Data Contributor` role at the database scope. Consult Microsoft Learn MCP for the role-assignment resource shape and the built-in role id `00000000-0000-0000-0000-000000000002`.
- [ ] T015 [P] Modify `iac/environments/dev/main.tf` to add a second `azurerm_cosmosdb_sql_role_assignment` granting developer-access (the same identity used by the platform-bootstrap module's developer principal) the same data-contributor role, so `dotnet run --project tools/load-fixtures --auth aad` works from developer machines per `quickstart.md` Path B.
- [ ] T016 Modify `iac/environments/dev/terraform.tfvars` to add `cosmos_account_name`, `canonical_db_name`, and any environment-specific overrides used by the new modules.
- [ ] T017 Run `tofu fmt -recursive` against `iac/`; run `tofu init -upgrade` in `iac/environments/dev` to download the new AVM module; run `tofu validate`; verify `checkov` (existing CI gate) reports `CKV_AZURE_140` PASS (encryption at rest enabled).

### Base domain value types (in-process; pure CPU; no I/O)

- [ ] T018 [P] Create `api/BusTerminal.Api/Domain/ResourceId.cs` as a readonly record struct wrapping `Guid`, with `New()`, `Parse(string)`, `ToString()` overrides, and `ImplicitOperator(Guid)`. Sealed against direct construction without going through factory methods. FR-021. Consult context7 MCP for current .NET 10 record-struct + primary-constructor idioms.
- [ ] T019 [P] Create `api/BusTerminal.Api/Domain/ResourceName.cs` as a readonly record struct wrapping `string`, validating against the regex `^[a-z0-9]+(-[a-z0-9]+)*$` at construction. Throws `ArgumentException` with a structured message on invalid input. FR-022.
- [ ] T020 [P] Create `api/BusTerminal.Api/Domain/NamespacePath.cs` as a readonly record struct wrapping `string` representing a slash-separated path. Methods: `Segments` (returns `ImmutableArray<string>`), `Parent` (returns `NamespacePath?`), `Append(string)`, `IsRoot`. Each segment validated against the same regex as `ResourceName`. FR-003.
- [ ] T021 [P] Create `api/BusTerminal.Api/Domain/ConcurrencyToken.cs` as a readonly record struct wrapping `string`. Opaque from the domain perspective; corresponds to Cosmos ETag. FR-025 / Q2.
- [ ] T022 [P] Create `api/BusTerminal.Api/Domain/LifecycleState.cs` as an enum with values `Draft`, `Active`, `Deprecated`, `Retired`, `Archived`. Add `[JsonStringEnumConverter]`-equivalent attribute so STJ serializes as lowercase strings.
- [ ] T023 [P] Create `api/BusTerminal.Api/Domain/LifecycleTransitions.cs` as a static class exposing `bool IsTransitionLegal(LifecycleState from, LifecycleState to)` and `IEnumerable<LifecycleState> LegalSuccessors(LifecycleState from)`. Implementation matches the table in `contracts/lifecycle-transitions.md` exactly. FR-010 / Q1.
- [ ] T024 [P] Create `api/BusTerminal.Api/Domain/EnvironmentClassification.cs` as a discriminated value type: known cases (`Development`, `Test`, `QA`, `Staging`, `Production`, `DisasterRecovery`) plus a `Custom(string)` case. JSON form is lowercase string for known cases, raw string for custom. FR-017.
- [ ] T025 [P] Create `api/BusTerminal.Api/Domain/SemanticVersion.cs` as a record carrying `Major`, `Minor`, `Patch`, `Compatibility` (enum: `Backward`/`Forward`/`Full`/`None`), `CurrentVersionRef`, and `VersionHistory` (`IReadOnlyCollection<HistoricalVersionEntry>?`). Implement `IComparable<SemanticVersion>` and equality. FR-011.
- [ ] T026 [P] Create `api/BusTerminal.Api/Domain/AuditRecord.cs` as a record matching `contracts/audit.schema.json` exactly. Includes `PrincipalReference` discriminated record (kinds: `Human`, `Workload`, `System`). FR-015 latest-state.
- [ ] T027 [P] Create `api/BusTerminal.Api/Domain/OwnershipRecord.cs` as a record matching `contracts/ownership.schema.json`. Includes `ContactReference` discriminated record (kinds: `Entra`, `Freeform`) to satisfy the forward-compatibility note in `data-model.md`. FR-009.
- [ ] T028 [P] Create `api/BusTerminal.Api/Domain/Extensions.cs` as a wrapper around `IReadOnlyDictionary<string, JsonElement>` with: namespaced-key validation regex (`^[a-z][a-z0-9-]*:[a-zA-Z][a-zA-Z0-9._-]*$`), reserved-key handling for `__indexable`, structured-value preservation. FR-012.
- [ ] T029 [P] Create `api/BusTerminal.Api/Domain/Tag.cs` as a readonly record struct (`TagId: ResourceId`, `Name: string`) representing an in-document tag reference (distinct from the `TagResource` first-class type).
- [ ] T030 [P] Create `api/BusTerminal.Api/Domain/DocumentationReference.cs` as a record (`AssetKind: DocumentationAssetKind`, `Uri: string`). `DocumentationAssetKind` enum in same file. FR-019.

### Abstract Resource + ResourceType registry

- [ ] T031 [P] Create `api/BusTerminal.Api/Domain/ResourceType.cs` exposing both an enum (`Namespace`, `Broker`, `Queue`, `Topic`, `Subscription`, `MessageContract`, `ProducerApplication`, `ConsumerApplication`, `Team`, `Environment`, `Tag`, `Policy`, `IntegrationFlow`, `DocumentationAsset`) AND a string-discriminator constant set (lowercase-first-letter camelCase strings matching the JSON Schema `const` values). Q4 / FR-002.
- [ ] T032 Create `api/BusTerminal.Api/Domain/ResourceTypeRegistry.cs` exposing `TryGetType(string discriminator, out Type clrType)`, `GetDiscriminator(Type clrType)`, and `IsKnown(string discriminator)`. Registry is populated at startup by reflection over the `Resources/` namespace (or by source-generated mapping for AOT-readiness). Q4.
- [ ] T033 Create `api/BusTerminal.Api/Domain/Resource.cs` as an abstract record carrying the base shape from `data-model.md` § 1 (Id, ResourceType, Name, DisplayName, Description, NamespacePath, Environments, Lifecycle, Version, Ownership, Audit, Classification, Tags, Extensions, Documentation, ValidationState, ConcurrencyToken, IsDeleted). Marked `[JsonPolymorphic(TypeDiscriminatorPropertyName = "resourceType")]` with `[JsonDerivedType]` attributes populated by T032's registry. FR-001.

### Validation engine framework

- [ ] T034 [P] Create `api/BusTerminal.Api/Domain/Validation/ValidationSeverity.cs` (enum: `Error`, `Warning`, `Info`).
- [ ] T035 [P] Create `api/BusTerminal.Api/Domain/Validation/ValidationFinding.cs` as a record matching `contracts/validation-result.schema.json#/$defs/finding`.
- [ ] T036 [P] Create `api/BusTerminal.Api/Domain/Validation/ValidationResult.cs` as a record (`EvaluatedAt`, `Findings`, `OverallSeverity`). Implement `OverallSeverity` getter that scans findings and returns the max. FR-013.
- [ ] T037 [P] Create `api/BusTerminal.Api/Domain/Validation/IValidationRule.cs` with method `IEnumerable<ValidationFinding> Validate(Resource resource, ValidationContext context)`. Define `ValidationContext` (carries a relationship resolver, a duplicate-detector callback, and a `IServiceProvider` for rule-internal dependencies) in the same file.
- [ ] T038 Create `api/BusTerminal.Api/Domain/Validation/ValidationEngine.cs` exposing `Task<ValidationResult> ValidateAsync(Resource resource, CancellationToken ct)`. Dispatches over `IEnumerable<IValidationRule>` registered via DI; rules can declare themselves universal or type-specific via an optional `AppliesTo(Type resourceType)` predicate. Q3 + FR-013. (Rule implementations come in individual user-story phases.)

### Change-event log scaffolding

- [ ] T039 [P] Create `api/BusTerminal.Api/Domain/Lifecycle/ChangeEventType.cs` (enum: `Created`, `Updated`, `LifecycleTransitioned`, `SoftDeleted`, `Restored`).
- [ ] T040 [P] Create `api/BusTerminal.Api/Domain/Lifecycle/ChangeEvent.cs` as a record matching `contracts/change-event.schema.json`. Q5.

### Persistence adapter (Cosmos)

- [ ] T041 [P] Create `api/BusTerminal.Api/Infrastructure/Persistence/CosmosOptions.cs` bound from configuration section `Cosmos` (Endpoint, Database, Containers.Resources, Containers.ChangeEvents, LocalEmulatorKey). Validate non-empty values via `IValidateOptions<CosmosOptions>`.
- [ ] T042 Create `api/BusTerminal.Api/Infrastructure/Persistence/CosmosClientFactory.cs` exposing `CosmosClient Create()` that constructs the client with AAD credential via `IAzureCredentialFactory` (inherited from 003) for production / dev Azure, OR with the well-known emulator key when the endpoint host equals `localhost`. Configures the client serializer to use STJ with the polymorphic options from T044. Consult Microsoft Learn MCP for the current `CosmosClientOptions.Serializer` injection pattern.
- [ ] T043 Create `api/BusTerminal.Api/Infrastructure/Persistence/ConcurrencyExceptionMapper.cs` as a static class translating `CosmosException` with `StatusCode == HttpStatusCode.PreconditionFailed` into a domain `ConcurrencyConflictException(ResourceId id, ConcurrencyToken presented, ConcurrencyToken? current)`. FR-025.
- [ ] T044 Create `api/BusTerminal.Api/Domain/Serialization/IResourceSerializer.cs` exposing `string SerializeToJson(Resource)`, `Resource DeserializeFromJson(string)`, plus envelope-level methods `string SerializeEnvelopeToJson(ImportExportEnvelope)` and `ImportExportEnvelope DeserializeEnvelopeFromJson(string)`. (YAML methods deferred to US8.) FR-016.
- [ ] T045 [P] Create `api/BusTerminal.Api/Domain/Serialization/ResourceJsonConverter.cs` as a custom `JsonConverter<Resource>` performing polymorphic dispatch on `resourceType` via `ResourceTypeRegistry`, falling through to `UnknownResource` (created in US1 task T053) for unknown values. Q4. Consult context7 MCP for the current System.Text.Json custom-converter pattern for polymorphic with fallback.
- [ ] T046 [P] Create `api/BusTerminal.Api/Domain/Serialization/ExtensionsJsonConverter.cs` as a custom `JsonConverter<Extensions>` preserving structured `JsonElement` values intact (no nested re-serialization). FR-012.
- [ ] T047 Create `api/BusTerminal.Api/Domain/Serialization/JsonResourceSerializer.cs` implementing `IResourceSerializer` for JSON. Configures `JsonSerializerOptions` once with: `PropertyNamingPolicy = JsonNamingPolicy.CamelCase`, `WriteIndented = false` for storage, `Converters = [ResourceJsonConverter, ExtensionsJsonConverter, ...lifecycle/severity enum converters]`. Exposed as a singleton via DI so the Cosmos serializer reuses the same options. Consult context7 MCP for STJ best practices in .NET 10.
- [ ] T048 Create `api/BusTerminal.Api/Domain/Serialization/ImportExportEnvelope.cs` as a record matching `contracts/import-export-envelope.schema.json` (`SchemaVersion`, `ExportedAt`, `ExportedBy`, `SourceSystem`, `Resources`, `Relationships`, `ChangeEvents?`, `ConflictResolution?`).
- [ ] T049 Create `api/BusTerminal.Api/Infrastructure/Persistence/ICanonicalResourceStore.cs` exposing: `Task<Resource?> GetAsync(ResourceId id, ResourceType type, bool includeDeleted, CancellationToken)`, `IAsyncEnumerable<Resource> QueryAsync(ResourceQuery query, CancellationToken)`, `Task<Resource> CreateAsync(Resource resource, PrincipalReference actor, string? sourceSystem, CancellationToken)`, `Task<Resource> UpdateAsync(Resource resource, PrincipalReference actor, string? sourceSystem, CancellationToken)`, `Task<Resource> SoftDeleteAsync(ResourceId id, ResourceType type, ConcurrencyToken token, PrincipalReference actor, string? sourceSystem, CancellationToken)`, `Task<Resource> RestoreAsync(ResourceId id, ResourceType type, ConcurrencyToken token, PrincipalReference actor, string? sourceSystem, CancellationToken)`.
- [ ] T050 Create `api/BusTerminal.Api/Infrastructure/Persistence/CosmosCanonicalResourceStore.cs` implementing T049 against the `resources` container. Reads filter `WHERE c.isDeleted = false` unless `includeDeleted: true`. Writes set `IfMatch` from `Resource.ConcurrencyToken`; on `412` map via `ConcurrencyExceptionMapper`. Every write triggers an `IChangeEventLog.AppendAsync` (T052) immediately after the canonical write succeeds, capturing the before/after concurrency tokens. FR-020 + FR-025 + Q5.
- [ ] T051 [P] Create `api/BusTerminal.Api/Infrastructure/Persistence/IChangeEventLog.cs` exposing `Task AppendAsync(ChangeEvent evt, CancellationToken)`, `IAsyncEnumerable<ChangeEvent> QueryAsync(ResourceId resourceId, CancellationToken)`.
- [ ] T052 Create `api/BusTerminal.Api/Infrastructure/Persistence/CosmosChangeEventLog.cs` implementing T051 against the `change-events` container with partition key `/resourceId`. Append-only; never updates or deletes. Failures are logged at Error and re-thrown (the canonical write is NOT rolled back; documented behavior per `data-model.md` § Change-event log emission).

### Configuration / DI wiring

- [ ] T053 Create `api/BusTerminal.Api/Infrastructure/Configuration/CosmosConfigurationExtensions.cs` exposing `AddCosmosCanonicalStore(this IServiceCollection, IConfiguration)` that: binds `CosmosOptions`; registers `CosmosClientFactory` as singleton with a factory that calls `Create()`; registers `CosmosClient` as singleton (resolved via the factory); registers `ICanonicalResourceStore` and `IChangeEventLog` as scoped; registers `IResourceSerializer` (JSON impl) as singleton; registers `ValidationEngine` as scoped; registers `IPrincipalAccessor`-derived `PrincipalReference` provider as scoped (humans → from `PlatformPrincipal`, system jobs → injected via factory).
- [ ] T054 Modify `api/BusTerminal.Api/Program.cs` to call `builder.Services.AddCosmosCanonicalStore(builder.Configuration)`. Add OpenTelemetry instrumentation activation: `.AddSource("Azure.Cosmos.Operation")` on the existing tracer-provider builder so persistence operations emit spans into the existing OTel → App Insights pipeline (Constitution §V). Consult Microsoft Learn MCP for the current Cosmos SDK OTel source name.
- [ ] T055 [P] Modify `api/BusTerminal.Api.Tests/BusTerminal.Api.Tests.csproj` to add `FluentAssertions` reference (already pinned at 8.x per `1c5dfff`; verify), and add a `Microsoft.Azure.Cosmos` test-only reference if needed for ETag/exception-type assertions.

### Foundational test infrastructure

- [ ] T056 Create `api/BusTerminal.Api.Tests/Integration/Persistence/CosmosEmulatorFixture.cs` as an xUnit collection fixture that: validates the `cosmos-emulator` Docker service is reachable on `https://localhost:8081`, creates the `busterminal-canonical` database + both containers if not present, truncates them before each test class, and exposes a configured `CosmosClient` and `ICanonicalResourceStore` to derived test classes. Mark as `[CollectionDefinition("CosmosEmulator")]`.

**Checkpoint**: Foundation ready. Cosmos exists, the abstract `Resource` chassis is in place, the persistence adapter compiles, the change-event log is wired, the validation engine framework is registered. User stories can now plug specific resource types and rules into the chassis in parallel.

---

## Phase 3: User Story 1 — Canonical inventory of messaging topology (Priority: P1) 🎯 MVP

**Goal**: Materialize the 14 first-class resource types as canonical documents with the shared base shape, persistent identifiers, namespace paths, lifecycle visibility, classification, and audit history — and prove round-trip integrity over the full set.

**Independent Test** (from spec): Populate the canonical store with a representative fixture set covering every first-class resource type. Confirm round-trip, identifier stability, base-field exposure, and validation-without-fix-up. **SC-001.**

### Per-type domain records (each [P] — different files)

- [ ] T057 [P] [US1] Create `api/BusTerminal.Api/Domain/Resources/Namespace.cs` as `sealed record Namespace : Resource` with `ParentNamespaceId: ResourceId?`. Matches `contracts/resources/namespace.schema.json`. FR-003.
- [ ] T058 [P] [US1] Create `api/BusTerminal.Api/Domain/Resources/Broker.cs` as `sealed record Broker : Resource` with `BrokerKind`, `Endpoint?`, `Capabilities`. Matches `contracts/resources/broker.schema.json`.
- [ ] T059 [P] [US1] Create `api/BusTerminal.Api/Domain/Resources/Queue.cs` matching `contracts/resources/queue.schema.json` (all of FR-004's fields). Decimal/time fields use `TimeSpan?` where applicable.
- [ ] T060 [P] [US1] Create `api/BusTerminal.Api/Domain/Resources/Topic.cs` matching `contracts/resources/topic.schema.json`. FR-005.
- [ ] T061 [P] [US1] Create `api/BusTerminal.Api/Domain/Resources/Subscription.cs` matching `contracts/resources/subscription.schema.json`. FR-006.
- [ ] T062 [P] [US1] Create `api/BusTerminal.Api/Domain/Resources/MessageContract.cs` matching `contracts/resources/message-contract.schema.json`. FR-007. (Validation rules for compatibility live in US4.)
- [ ] T063 [P] [US1] Create `api/BusTerminal.Api/Domain/Resources/ProducerApplication.cs` matching `contracts/resources/producer-application.schema.json`.
- [ ] T064 [P] [US1] Create `api/BusTerminal.Api/Domain/Resources/ConsumerApplication.cs` matching `contracts/resources/consumer-application.schema.json`.
- [ ] T065 [P] [US1] Create `api/BusTerminal.Api/Domain/Resources/Team.cs` matching `contracts/resources/team.schema.json`. Ownership field is null (Teams are owned organizationally).
- [ ] T066 [P] [US1] Create `api/BusTerminal.Api/Domain/Resources/EnvironmentResource.cs` matching `contracts/resources/environment.schema.json`. (Class is named `EnvironmentResource` to avoid clash with `System.Environment`; the persisted `resourceType` discriminator remains `environment`.) FR-017.
- [ ] T067 [P] [US1] Create `api/BusTerminal.Api/Domain/Resources/TagResource.cs` matching `contracts/resources/tag.schema.json`. (Class is `TagResource`; persisted discriminator is `tag`. Distinct from the `Tag` value type from T029.)
- [ ] T068 [P] [US1] Create `api/BusTerminal.Api/Domain/Resources/Policy.cs` matching `contracts/resources/policy.schema.json`.
- [ ] T069 [P] [US1] Create `api/BusTerminal.Api/Domain/Resources/IntegrationFlow.cs` matching `contracts/resources/integration-flow.schema.json`.
- [ ] T070 [P] [US1] Create `api/BusTerminal.Api/Domain/Resources/DocumentationAsset.cs` matching `contracts/resources/documentation-asset.schema.json`. FR-019.
- [ ] T071 [P] [US1] Create `api/BusTerminal.Api/Domain/Resources/UnknownResource.cs` as `sealed record UnknownResource : Resource` with a `RawJson: JsonElement` field carrying the original document body for diagnostic surfacing. Q4.
- [ ] T072 [US1] Update `ResourceTypeRegistry.cs` (T032) to register each of T057–T070 by reflection or by adding them to a hand-maintained static map. Verify all 14 known types resolve.

### Namespace inheritance

- [ ] T073 [US1] Create `api/BusTerminal.Api/Domain/NamespaceInheritance.cs` as a service that, given a `NamespacePath`, resolves the parent chain via `ICanonicalResourceStore.GetByNamespacePath()` (uses `QueryAsync`) and returns inherited governance / ownership metadata. Override semantics: child namespace's explicit metadata wins over inherited. FR-003 + Edge Case "Ownership of a Namespace itself."

### Universal validation rules

- [ ] T074 [P] [US1] Create `api/BusTerminal.Api/Domain/Validation/Rules/RequiredFieldsRule.cs` implementing `IValidationRule`. Asserts `Id`, `ResourceType`, `Name`, `DisplayName`, `Lifecycle`, `Version`, `Audit` are populated. Severity: Error. FR-013.
- [ ] T075 [P] [US1] Create `api/BusTerminal.Api/Domain/Validation/Rules/NamingStandardsRule.cs` implementing `IValidationRule`. Re-runs the `ResourceName` regex (T019) as a validation-time check (defense in depth). Severity: Error. FR-013 + FR-022.
- [ ] T076 [P] [US1] Create `api/BusTerminal.Api/Domain/Validation/Rules/UnknownResourceTypeRule.cs` implementing `IValidationRule`. Emits an Info finding when the resource is materialized as `UnknownResource`. Q4 + FR-002.
- [ ] T077 [US1] Modify `CosmosConfigurationExtensions.cs` (T053) to register T074–T076 as `IValidationRule` (singleton, multi-registration).

### Fixture set + load tool wiring

- [ ] T078 [US1] Create `api/BusTerminal.Api/Fixtures/canonical-fixtures.json` containing **one of each first-class type** (14 documents), with realistic field values, a producer-application + topic + 2-subscription + 2-consumer-application + 1-message-contract relationship cluster matching the FR-008 example, ownership references to a single Team, and one extension `contoso:costCenter` on the Queue and one `__indexable` entry. The fixture set MUST validate against the JSON Schemas in `contracts/` — see T091.
- [ ] T079 [US1] Implement the `tools/load-fixtures` `create-database` subcommand: ensures database + containers exist (idempotent). Calls into Cosmos SDK directly using the same `CosmosClientFactory`.
- [ ] T080 [US1] Implement the `tools/load-fixtures` `import` subcommand: reads the fixtures JSON file, deserializes via `IResourceSerializer` envelope path, calls `ICanonicalResourceStore.CreateAsync` per resource. Prints a summary `Loaded N resources, M relationships. Validation: X OK, Y Error, Z Warning, W Info.` per `quickstart.md` § A.3 expectation.
- [ ] T081 [US1] Implement the `tools/load-fixtures` `show` subcommand: takes `--resource-id`, prints the resource as JSON (`--format json`, default) or YAML (deferred to US8). Used by `quickstart.md` smoke validations 5 and 6.
- [ ] T082 [US1] Implement the `tools/load-fixtures` `truncate` subcommand: deletes all documents in both containers. Required by `quickstart.md` Path B § B.4 and Smoke 7.

### US1 tests

- [ ] T083 [P] [US1] Create `api/BusTerminal.Api.Tests/Unit/Domain/ResourceTypeRegistryTests.cs` asserting all 14 known types resolve, unknown discriminator returns `UnknownResource`, and the round-trip discriminator string is stable. Q4.
- [ ] T084 [P] [US1] Create `api/BusTerminal.Api.Tests/Unit/Domain/ResourceNameTests.cs` covering valid names, invalid names (uppercase, spaces, underscores, leading/trailing hyphens), and edge cases (single-char, max-length). FR-022.
- [ ] T085 [P] [US1] Create `api/BusTerminal.Api.Tests/Unit/Domain/NamespacePathTests.cs` covering parent chain, root detection, append, and segment validation. FR-003.
- [ ] T086 [P] [US1] Create `api/BusTerminal.Api.Tests/Unit/Domain/SemanticVersionTests.cs` covering ordering, equality, and compatibility-indicator carriage. FR-011.
- [ ] T087 [P] [US1] Create `api/BusTerminal.Api.Tests/Unit/Serialization/JsonRoundTripTests.cs` asserting every first-class type (T057–T070) round-trips JSON losslessly. One `[Theory]` with `[MemberData]` driving all 14 types. **SC-001 evidence.**
- [ ] T088 [P] [US1] Create `api/BusTerminal.Api.Tests/Unit/Serialization/PolymorphismTests.cs` asserting discriminator-based deserialization across the 14 known types + `UnknownResource` fallback. Q4.
- [ ] T089 [P] [US1] Create `api/BusTerminal.Api.Tests/Unit/Serialization/ExtensionPreservationTests.cs` asserting structured (nested-object) extension values survive round-trip and `__indexable` marker is preserved. Q4 + FR-012.
- [ ] T090 [P] [US1] Create `api/BusTerminal.Api.Tests/Unit/Validation/Rules/RequiredFieldsRuleTests.cs` and matching tests for T074, T075, T076 (one test file each in `Rules/`).
- [ ] T091 [US1] Create `api/BusTerminal.Api.Tests/Unit/Serialization/SchemaDriftGuardTests.cs` that loads each JSON Schema from `specs/004-core-domain-model/contracts/` and validates the corresponding fixture from `Fixtures/canonical-fixtures.json` against it using a JSON Schema validator (e.g., `JsonSchema.Net` or equivalent — consult context7 MCP for the current preferred .NET JSON Schema library; cite the choice in research.md if a new package is added). Asserts schemas and types stay aligned.
- [ ] T092 [US1] Create `api/BusTerminal.Api.Tests/Integration/Persistence/FixtureLoadAndQueryTests.cs` (in the `CosmosEmulator` collection). Loads the fixture set, asserts every resource round-trips through Cosmos with identifiers stable, lifecycle states preserved, namespace paths normalized, and validation-state populated. **SC-001 final evidence.**
- [ ] T093 [US1] Create `api/BusTerminal.Api.Tests/Integration/Persistence/CanonicalStoreIntegrationTests.cs` covering CRUD against the emulator: create → read → update with valid ETag (success) → update with stale ETag (rejected with `ConcurrencyConflictException`). FR-025 / Q2.

**Checkpoint**: User Story 1 fully functional. Fixture set loads end-to-end. All 14 first-class types persist + round-trip + validate. **SC-001 complete.**

---

## Phase 4: User Story 2 — Structured queryable ownership (Priority: P1)

**Goal**: Every operational resource carries an `OwnershipRecord` resolving to a Team by stable identifier; ownership validation fires when missing or dangling; rename-safe references; "all resources owned by Team X" is an indexable query.

**Independent Test** (from spec): For each operational type, confirm `OwnershipPresenceRule` fires; renaming a Team's logical name does not break ownership references; ownership shape carries all six structured contact/escalation/support/tier fields. **SC-002.**

- [ ] T094 [P] [US2] Create `api/BusTerminal.Api/Domain/Validation/Rules/OwnershipPresenceRule.cs` implementing `IValidationRule`. Applies only to operational types (Broker, Queue, Topic, Subscription, MessageContract, ProducerApplication, ConsumerApplication, IntegrationFlow). Asserts `Resource.Ownership` is non-null and `Ownership.OwningTeamId` resolves via the context's relationship resolver. Severity: Error. FR-009.
- [ ] T095 [US2] Register `OwnershipPresenceRule` in `CosmosConfigurationExtensions.cs`.
- [ ] T096 [US2] Add an "owned-by" query to `CosmosCanonicalResourceStore.cs` exposed via `ICanonicalResourceStore.QueryAsync` accepting a `ResourceQuery.OwnedByTeam(ResourceId teamId)` shape. Cosmos query: `SELECT * FROM c WHERE c.ownership.owningTeamId = @teamId AND c.isDeleted = false`. Add the implicit index entry on `/ownership/owningTeamId` to the canonical container's indexing policy (modify T011).
- [ ] T097 [US2] Implement the `tools/load-fixtures` `show-owner` subcommand: takes `--resource-id`, prints the structured ownership block (team display name resolved by a follow-up lookup against the Team resource). Used by `quickstart.md` Smoke 1.
- [ ] T098 [P] [US2] Create `api/BusTerminal.Api.Tests/Unit/Domain/OwnershipRecordTests.cs` covering construction validation, contact-kind round-trip (Entra and Freeform), and operational-tier enum.
- [ ] T099 [P] [US2] Create `api/BusTerminal.Api.Tests/Unit/Validation/Rules/OwnershipPresenceRuleTests.cs` covering the operational-type matrix: each of the 8 operational types fails when Ownership is null; each of the 6 non-operational types passes when Ownership is null; dangling team reference fires Error.
- [ ] T100 [US2] Create `api/BusTerminal.Api.Tests/Integration/Persistence/OwnershipQueryIntegrationTests.cs` covering: load fixtures, query by owning team, rename the team's `Name` (not `Id`), re-query — ownership references still resolve. **SC-002 evidence.**

**Checkpoint**: User Story 2 fully functional. Every operational resource has resolvable structured ownership; rename-safe; queryable.

---

## Phase 5: User Story 3 — Explicit relationship graph (Priority: P1)

**Goal**: Producers, consumers, topics, subscriptions, queues, contracts, teams, and integration flows are linked by explicit typed directional relationships. Multi-hop traversal answers "who consumes from this topic" deterministically without name-based inference.

**Independent Test** (from spec): Load the FR-008 relationship fixture cluster, traverse from a producer application to every consuming application, verify the traversal returns the correct set with each hop typed and directional. **SC-003.**

- [ ] T101 [P] [US3] Create `api/BusTerminal.Api/Domain/Relationships/RelationshipType.cs` as an enum matching `contracts/relationship-types.md`: `PublishesTo`, `ConsumedBy`, `SubscriptionOf`, `UsesContract`, `Owns`, `AttachedTo`, `Replaces`, `PartOfFlow`. Includes a static table mapping each type to its allowed source/target resource types.
- [ ] T102 [P] [US3] Create `api/BusTerminal.Api/Domain/Relationships/Relationship.cs` as a record matching `contracts/relationship.schema.json` (peer document, not a `Resource` subtype). Fields: `Id`, `ResourceType` (`"relationship"`), `SourceId`, `TargetId`, `Type`, `Annotations`, `Audit`, `ValidationState`, `IsDeleted`, `ConcurrencyToken`.
- [ ] T103 [US3] Extend `ResourceJsonConverter.cs` (T045) to recognize the `relationship` discriminator and dispatch to `Relationship`'s converter — relationships live in the same `resources` Cosmos container as a peer document type.
- [ ] T104 [US3] Extend `ICanonicalResourceStore` and `CosmosCanonicalResourceStore.cs` with `CreateRelationshipAsync`, `GetRelationshipAsync`, `QueryRelationshipsAsync(ResourceId resourceId, Direction direction)` where `Direction` is `Inbound | Outbound | Both`. Cosmos queries filter on `c.resourceType = 'relationship' AND (c.sourceId = @id OR c.targetId = @id)`.
- [ ] T105 [P] [US3] Create `api/BusTerminal.Api/Domain/Relationships/RelationshipGraph.cs` as an in-process traversal helper. Methods: `Task<TraversalResult> TraverseAsync(ResourceId startId, RelationshipType[] allowedTypes, int maxHops, Direction direction, CancellationToken)`. Uses BFS with visited-set cycle protection (Edge Case "Circular relationships"). FR-008.
- [ ] T106 [P] [US3] Create `api/BusTerminal.Api/Domain/Validation/Rules/DanglingReferenceRule.cs` implementing `IValidationRule`. For every reference field in any resource (subscription's `parentTopicId`, integration-flow's foreign-key fields, contract/producer/consumer associations, ownership's `owningTeamId`), assert the referent exists. Severity: Error for non-existent target; Warning for soft-deleted target (per Edge Case "Dangling references after soft-delete"). Resolution uses the `ValidationContext`'s relationship resolver against a per-validation-pass cache so a fixture-load doesn't issue N queries.
- [ ] T107 [P] [US3] Create `api/BusTerminal.Api/Domain/Validation/Rules/RelationshipTypeValidityRule.cs` implementing `IValidationRule`. Applies only to `Relationship` documents. Asserts source and target types match the allowed pairings from T101's table. Severity: Error for mismatch.
- [ ] T108 [US3] Register `DanglingReferenceRule` and `RelationshipTypeValidityRule` in `CosmosConfigurationExtensions.cs`.
- [ ] T109 [US3] Implement the `tools/load-fixtures` `traverse` subcommand: takes `--from <resource-id>`, `--max-hops <n>`, optional `--types <comma-separated>`. Calls `RelationshipGraph.TraverseAsync` and prints the typed multi-hop path. Used by `quickstart.md` Smoke 2.
- [ ] T110 [US3] Extend `Fixtures/canonical-fixtures.json` (T078) to add the relationship documents for the FR-008 example cluster: ProducerApp → publishesTo → Topic → subscriptionOf ← Subscription → consumedBy → ConsumerApp; Team → owns → (every operational resource); Queue → usesContract → MessageContract; one `replaces` and one `partOfFlow` relationship to exercise those types. Net 24 relationships per the plan's task summary.
- [ ] T111 [P] [US3] Create `api/BusTerminal.Api.Tests/Unit/Relationships/RelationshipGraphTests.cs` covering BFS termination, cycle protection, direction enforcement, max-hop respect, and type-filtered traversal. FR-008.
- [ ] T112 [P] [US3] Create `api/BusTerminal.Api.Tests/Unit/Validation/Rules/DanglingReferenceRuleTests.cs` covering live-target pass, missing-target Error, soft-deleted-target Warning.
- [ ] T113 [P] [US3] Create `api/BusTerminal.Api.Tests/Unit/Validation/Rules/RelationshipTypeValidityRuleTests.cs` covering: each of the 8 relationship types with a matching source/target pair (pass); each with a mismatched source or target (Error).
- [ ] T114 [US3] Create `api/BusTerminal.Api.Tests/Integration/Persistence/RelationshipTraversalIntegrationTests.cs`. Loads fixtures, traverses producer-app → consumer-app via topic + subscription, asserts deterministic ordering of the returned path and correct typing of each hop. **SC-003 evidence.**

**Checkpoint**: User Story 3 fully functional. Relationship graph queryable end-to-end; traversal answers impact-analysis questions.

---

## Phase 6: User Story 4 — Contracts with semantic versioning + compatibility (Priority: P2)

**Goal**: Message contracts attach to queues and topics with semantic version, format classification, compatibility metadata, producer/consumer associations, and per-version lifecycle. Multiple formats coexist with no per-format fork.

**Independent Test** (from spec): Define a contract at v1.0.0, attach to a queue + topic, attach producer + consumer apps, mark a hypothetical 0.9.0 as Deprecated, confirm the model exposes active version, deprecated lineage, format, schema reference, examples, compatibility, and the producer/consumer linkage. **SC-001 (contracts subset) + indirectly SC-008 via compatibility findings.**

- [ ] T115 [P] [US4] Create `api/BusTerminal.Api/Domain/Validation/Rules/ContractCompatibilityRule.cs` implementing `IValidationRule`. Applies only to `MessageContract` resources. Asserts `Compatibility` is set; asserts `VersionHistory` is consistent (no duplicate versions; deprecated versions have a `replacedBy` pointing at a non-deprecated version when one exists); does NOT run schema-level compatibility (pluggable validators are deferred per spec assumption). Severity: Warning for inconsistent lineage; Info for "version N is older but not deprecated." FR-013 + FR-007.
- [ ] T116 [US4] Register `ContractCompatibilityRule` in `CosmosConfigurationExtensions.cs`.
- [ ] T117 [US4] Extend `Fixtures/canonical-fixtures.json` (T078) to add: a MessageContract at v1.0.0 (Active) with version-history entry for v0.9.0 (Deprecated, scheduled retirement date in the past, replacedBy v1.0.0); a contract of format `protobuf`; a contract of format `cloudEvents`. Asserts multi-format coexistence (SC-001 / scenario 5).
- [ ] T118 [P] [US4] Create `api/BusTerminal.Api.Tests/Unit/Validation/Rules/ContractCompatibilityRuleTests.cs` covering consistent lineage (no findings), duplicate-version (Warning), missing replacedBy on deprecated version (Info).
- [ ] T119 [US4] Create `api/BusTerminal.Api.Tests/Integration/Persistence/ContractsIntegrationTests.cs` asserting multi-format contract round-trip and per-version lifecycle queryability against the emulator. **SC-001 contracts subset evidence.**

**Checkpoint**: User Story 4 fully functional. Contracts with semantic versioning, multi-format support, per-version lifecycle.

---

## Phase 7: User Story 5 — Lifecycle states + soft-delete + restoration (Priority: P2)

**Goal**: Resources move through Draft → Active → Deprecated → Retired → Archived under strict legal-transition rules; soft-delete preserves identifier + audit history + change-log + relationships; restoration returns the resource to its prior lifecycle state.

**Independent Test** (from spec): Create Active queue, transition through full lifecycle path, soft-delete + restore a topic, attempt an illegal transition and confirm validation rejects it. **SC-004 + SC-005 + SC-012.**

- [ ] T120 [P] [US5] Create `api/BusTerminal.Api/Domain/Validation/Rules/LifecycleTransitionRule.cs` implementing `IValidationRule`. Hooks into `ICanonicalResourceStore.UpdateAsync` via the `ValidationContext`'s "intended transition" carrier: when a write changes `Resource.Lifecycle`, the rule asserts `LifecycleTransitions.IsTransitionLegal(from, to)`. Severity: Error. FR-013 + FR-010 + Q1.
- [ ] T121 [US5] Modify `CosmosCanonicalResourceStore.UpdateAsync` (T050) to populate the `ValidationContext` with the previous lifecycle state (read before write) so `LifecycleTransitionRule` can compare. Register the rule in `CosmosConfigurationExtensions.cs`.
- [ ] T122 [US5] Create `api/BusTerminal.Api/Domain/Lifecycle/SoftDelete.cs` exposing helpers `Resource MarkDeleted(Resource resource)` and `Resource MarkRestored(Resource resource, LifecycleState restoredState)`. Pure functions; do not touch persistence. FR-020.
- [ ] T123 [US5] Extend `CosmosCanonicalResourceStore.SoftDeleteAsync` and `RestoreAsync` (T049 / T050) to: (a) toggle `IsDeleted` via `SoftDelete` helpers; (b) preserve the prior `Lifecycle` on soft-delete and restore to it on restore; (c) emit `ChangeEvent` with `EventType = SoftDeleted` / `Restored` per Q5; (d) bypass `LifecycleTransitionRule` on restore (per `contracts/lifecycle-transitions.md` § "Soft-delete and restoration are NOT lifecycle transitions").
- [ ] T124 [US5] Implement the `tools/load-fixtures` `transition`, `soft-delete`, and `restore` subcommands. `transition --to <state>` triggers an update with the new lifecycle; the rule will reject illegal transitions. Used by `quickstart.md` Smokes 3 and 4.
- [ ] T125 [US5] Implement the `tools/load-fixtures` `changelog` subcommand: takes `--resource-id`, prints ordered change events via `IChangeEventLog.QueryAsync`. Used by `quickstart.md` Smoke 8.
- [ ] T126 [P] [US5] Create `api/BusTerminal.Api.Tests/Unit/Domain/LifecycleTransitionsTests.cs` covering the full transition matrix from `contracts/lifecycle-transitions.md` (legal pass, illegal fail). FR-010 / Q1.
- [ ] T127 [P] [US5] Create `api/BusTerminal.Api.Tests/Unit/Validation/Rules/LifecycleTransitionRuleTests.cs` covering the rule's integration with the validation context.
- [ ] T128 [US5] Create `api/BusTerminal.Api.Tests/Integration/Persistence/SoftDeleteRetentionTests.cs` covering: soft-delete preserves identifier, audit, version, relationships; restore returns prior state; soft-delete emits a `SoftDeleted` event; restore emits a `Restored` event. **SC-005 evidence.**
- [ ] T129 [US5] Create `api/BusTerminal.Api.Tests/Integration/Persistence/ChangeEventLogIntegrationTests.cs` covering: full ordered change-event sequence per resource (Created → Updated → LifecycleTransitioned → SoftDeleted → Restored) with actor, timestamp, source system, concurrency tokens, and snapshot/diff per event. **SC-012 evidence.**
- [ ] T130 [US5] Create `api/BusTerminal.Api.Tests/Integration/Validation/EndToEndValidationTests.cs` driving Error, Warning, and Info findings against the loaded fixture set; verifies Error blocks the write while Warning and Info persist. **SC-008 evidence.**

**Checkpoint**: User Story 5 fully functional. Lifecycle enforced; soft-delete + restore round-trip with full change-log linkage.

---

## Phase 8: User Story 6 — Namespaced extensions without schema forks (Priority: P2)

**Goal**: Organizations attach namespaced custom-metadata extensions (`contoso:costCenter`, `fabrikam:dataSensitivity`) with structured JSON values, per-extension indexing-inclusion control, no canonical-schema fork, no required-field impact, no extension-key collision across vendors.

**Independent Test** (from spec): Attach extensions of structured (object) value and primitive value to several resources, round-trip them, confirm preservation; confirm canonical-schema validation does not depend on any extension; confirm `__indexable: false` excludes from a search projection. **SC-006.**

- [ ] T131 [P] [US6] Create `api/BusTerminal.Api/Domain/Validation/Rules/ExtensionKeyFormatRule.cs` implementing `IValidationRule`. Asserts every key in `Extensions` (other than `__indexable`) matches the namespaced-key regex. Severity: Warning (we tolerate but flag non-standard keys to avoid breaking soft-delete + restore of legacy documents). FR-012.
- [ ] T132 [US6] Register `ExtensionKeyFormatRule` in `CosmosConfigurationExtensions.cs`.
- [ ] T133 [US6] Modify the canonical container's indexing policy (T011) to exclude `/extensions/*` paths by default. Document in `docs/cosmos-operations.md` (created in T155) that the future search-projection slice consults each resource's `extensions.__indexable.<key> == true` and re-indexes selectively.
- [ ] T134 [US6] Extend `Fixtures/canonical-fixtures.json` (T078) to add: two extensions on the Queue (`contoso:costCenter` primitive, `contoso:sla` structured object); one extension on a Topic with `__indexable: false`; and one extension on a Producer Application using a `fabrikam:` prefix to exercise multi-vendor coexistence (Scenario 2 / SC-006).
- [ ] T135 [P] [US6] Create `api/BusTerminal.Api.Tests/Unit/Domain/ExtensionsTests.cs` covering: structured-object round-trip; primitive round-trip; multi-vendor coexistence with same suffix; `__indexable` opt-out marker preservation; rejection of malformed keys at construction.
- [ ] T136 [P] [US6] Create `api/BusTerminal.Api.Tests/Unit/Validation/Rules/ExtensionKeyFormatRuleTests.cs` covering namespaced-key pass, malformed-key Warning, `__indexable` ignored by the rule.

**Checkpoint**: User Story 6 fully functional. Extensions survive round-trip; vendor isolation enforced.

---

## Phase 9: User Story 7 — Environment classification without duplicate logical resources (Priority: P2)

**Goal**: A single logical queue carries associations to all six minimum environments (Development, Test, QA, Staging, Production, DisasterRecovery); environment filter queries return only matching resources; custom environment vocabulary supported by extension.

**Independent Test** (from spec): Create a single logical queue with associations to all six environments; query by Production filter; confirm no per-environment duplicate documents exist. **SC-007.**

- [ ] T137 [US7] Extend `ICanonicalResourceStore` and `CosmosCanonicalResourceStore` (T049 / T050) with `QueryAsync(ResourceQuery.InEnvironment(EnvironmentClassification env))` returning only resources whose `Environments` array contains the requested classification. Cosmos query: `WHERE ARRAY_CONTAINS(c.environments, @env)`.
- [ ] T138 [US7] Extend `Fixtures/canonical-fixtures.json` (T078) to ensure exactly one Queue carries all six minimum environments as a single document, and at least two resources carry a custom environment value (e.g., `"training"`) to exercise the extensibility path.
- [ ] T139 [P] [US7] Create `api/BusTerminal.Api.Tests/Unit/Domain/EnvironmentClassificationTests.cs` covering: known-case serialization; custom-case serialization; round-trip preservation.
- [ ] T140 [US7] Create `api/BusTerminal.Api.Tests/Integration/Persistence/EnvironmentFilterIntegrationTests.cs` covering: load fixtures; query by Production; assert no duplicate logical resources per environment; query by custom environment value. **SC-007 evidence.**

**Checkpoint**: User Story 7 fully functional. Environment-aware without document duplication.

---

## Phase 10: User Story 8 — Lossless import / export (Priority: P3)

**Goal**: Export the canonical store to JSON (and YAML); re-import into an empty store; verify identifiers, relationships, version lineage, ownership, lifecycle, and extensions are preserved byte-equivalently.

**Independent Test** (from spec): Round-trip an export → empty store → import; verify lossless preservation. Repeat for YAML. **SC-009.**

- [ ] T141 [US8] Extend `IResourceSerializer` (T044) with YAML methods: `string SerializeToYaml(Resource)`, `Resource DeserializeFromYaml(string)`, `string SerializeEnvelopeToYaml(ImportExportEnvelope)`, `ImportExportEnvelope DeserializeEnvelopeFromYaml(string)`.
- [ ] T142 [US8] Create `api/BusTerminal.Api/Domain/Serialization/YamlResourceSerializer.cs` implementing the YAML methods of `IResourceSerializer` using YamlDotNet 16.x. Consult context7 MCP for current YamlDotNet builder/converter patterns. The logical shape MUST match JSON exactly (envelope structure, extensions, polymorphic discriminator).
- [ ] T143 [US8] Implement the `tools/load-fixtures` `export` subcommand: queries every resource (including soft-deleted via `--include-deleted` flag) and every relationship, optionally queries the change-event log via `--include-change-log`, wraps into `ImportExportEnvelope`, serializes via `--format json|yaml`, writes to `--output <path>`. FR-016.
- [ ] T144 [US8] Implement the `tools/load-fixtures` `import` subcommand (extension of T080): adds `--conflict-resolution reject|skip|overwrite` flag (default `reject`). Resolution outcome is recorded in audit metadata per Scenario 3.
- [ ] T145 [US8] Update the `tools/load-fixtures` `show` subcommand (T081) to support `--format yaml`.
- [ ] T146 [P] [US8] Create `api/BusTerminal.Api.Tests/Unit/Serialization/YamlRoundTripTests.cs` asserting every first-class type round-trips YAML losslessly (mirror of T087). **SC-009 evidence per-type.**
- [ ] T147 [US8] Create `api/BusTerminal.Api.Tests/Integration/Persistence/ExportImportRoundTripTests.cs` covering: load fixtures → export to JSON → truncate → import → assert byte-equivalence of resource set, relationships (with direction and type), version lineage, ownership references, lifecycle, and extensions. Repeat for YAML format. Repeat with conflict-resolution `reject` against a non-empty store and assert the structured conflict result. **SC-009 final evidence.**

**Checkpoint**: User Story 8 fully functional. Portable serialization confirmed.

---

## Phase 11: Polish & Cross-Cutting Concerns

**Purpose**: Documentation, secret-scanning, schema-drift CI gate, MCP-citation review, and quickstart smoke validation.

- [ ] T148 [P] Create `docs/domain-model.md` — architectural overview of the canonical model. Links to spec.md, data-model.md, and the per-type contracts. Explains the Resource-base / per-type derivation, the relationship-graph model, ownership semantics, and the change-event log. **No code samples**; pure conceptual orientation.
- [ ] T149 [P] Create `docs/cosmos-operations.md` — operator runbook. Documents the two containers + partition strategy, troubleshooting `ConcurrencyConflictException` (re-read + retry), the soft-delete query convention (`isDeleted = false`), change-log queries (per-resource history), and the public-endpoint posture (with the planned future Private Link follow-up referenced).
- [ ] T150 Modify the existing `.github/workflows/*.yml` CI pipeline (or equivalent) to: (a) start the `cosmos-emulator` service for the integration test job; (b) run `dotnet test --filter "Category=Integration"` with `CosmosOptions__Endpoint=https://localhost:8081` set; (c) add a `checkov` step asserting `CKV_AZURE_140` PASS on the cosmos modules; (d) ensure `gitleaks` (existing) covers `iac/` and `tools/`.
- [ ] T151 [P] Create `api/BusTerminal.Api.Tests/Integration/Persistence/SecretScanGuardTests.cs` (in the `CosmosEmulator` collection) that loads fixtures, scans every persisted document body for the denylist (`password`, `secret`, `connectionstring`, `accesskey`, `sastoken`, `bearer`, `apikey` — case-insensitive substring match), and fails the test if any match is found. **SC-011 evidence.**
- [ ] T152 [P] Add per-test category attributes (`[Trait("Category", "Integration")]`) to every integration test class created in this slice so the CI filter (T150) selects only the integration tier when the emulator is available.
- [ ] T153 Modify `api/BusTerminal.Api/Program.cs` to register an OTel meter for validation finding counts (`busterminal.validation.finding_count_error`, `..._warning`, `..._info` per `data-model.md` § Naming Cross-Reference) and emit them on every validation pass via a `IValidationEngine` decorator. Consult Microsoft Learn MCP for the current OpenTelemetry .NET metrics API.
- [ ] T154 [P] Review every task in Phases 2–10 and confirm the MCP citations referenced are still current. For each MCP cite (Microsoft Learn for Cosmos / AVM / OTel; context7 for STJ / YamlDotNet / JSON Schema), spot-check the live MCP result against the implementation. Record any deviations as comments alongside the cite.
- [ ] T155 Execute `quickstart.md` Path A end-to-end against the local emulator. Confirm SC-001 through SC-008 + SC-011 + SC-012 outputs match expectations. Capture any deviations as follow-up tasks (do NOT mark this task complete until the runbook executes clean).
- [ ] T156 Execute `quickstart.md` Path B end-to-end against the dev Cosmos account (via OpenTofu apply + load-fixtures with `--auth aad`). Confirm SC-009 in addition to the above. Capture any dev-environment-specific drift.
- [ ] T157 Re-evaluate the Constitution Check in `plan.md` against the actual built code. If any deviation surfaced during implementation that the plan did not anticipate, append a row to Complexity Tracking with rationale and the simpler-alternative-rejected reasoning. Otherwise, append a "Post-implementation re-check: no new deviations" note.

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately. T001–T007 mostly parallelizable.
- **Foundational (Phase 2)**: Depends on Setup completion. BLOCKS all user stories. Internal ordering:
  - T008–T017 (IaC) can proceed in parallel with T018–T056 (.NET) once Setup completes.
  - Within .NET: value types (T018–T030) parallelize fully; T031–T033 (registry + abstract `Resource`) depend on value types; T034–T038 (validation engine) parallelize with each other; T039–T040 (change-event scaffolding) parallelize; T041–T048 (persistence + serializer) ordering — T041 (options) → T042 (factory) → T043 (mapper) ‖ T044–T048 (serializer framework) → T049 (store interface) → T050 (store impl) ‖ T051–T052 (change-event log) → T053 (DI) → T054 (Program.cs) → T055–T056 (test infra).
- **User Stories (Phases 3–10)**: All depend on Foundational completion. Can then proceed in parallel (if staffed) or sequentially in priority order.
- **Polish (Phase 11)**: Depends on all desired user stories being complete. Quickstart smoke tests (T155–T157) are gating.

### User story dependencies

- **US1 (Canonical inventory — P1)**: Can start after Foundational. Required by every other story (the per-type records are the chassis the per-story rules attach to). MVP candidate.
- **US2 (Ownership — P1)**: Can start after Foundational + US1's T072 (registry populated) so OwnershipPresenceRule has resource types to operate on. Otherwise independent of US3–US8.
- **US3 (Relationships — P1)**: Can start after Foundational + US1's T072. Adds a peer document type to the canonical container. DanglingReferenceRule from US3 is used by US2's ownership validation, so if US3 lands before US2 the cross-story dependency is satisfied implicitly. If US2 lands first, OwnershipPresenceRule's resolver uses a temporary stub that US3 replaces.
- **US4 (Contracts — P2)**: Depends on US1's `MessageContract` type. Otherwise independent.
- **US5 (Lifecycle + soft-delete — P2)**: Can start after Foundational + US1. The LifecycleTransitionRule wraps the persistence layer's update path; the soft-delete + restore methods extend the store.
- **US6 (Extensions — P2)**: Can start after Foundational + US1. Independent of US2–US5, US7, US8.
- **US7 (Environments — P2)**: Can start after Foundational + US1. Independent of US2–US6, US8.
- **US8 (Import/Export — P3)**: Depends on US1 (resources), US3 (relationships), US5 (soft-delete events in the change log). The YAML serializer specifically lands here.

### Within each user story

- Tests can be written first (TDD) or alongside implementation. The spec's measurable SCs map directly to specific integration tests called out in each story's task list.
- Per-type records within US1 (T057–T071) are fully parallelizable — different files, no cross-dependencies.
- Validation rules within each story parallelize among themselves but depend on the rule's prerequisites (e.g., DanglingReferenceRule needs RelationshipGraph; LifecycleTransitionRule needs LifecycleTransitions).
- Story complete before moving to next priority (recommended for MVP path; parallel team allocation is supported per the strategy below).

### Parallel opportunities

- All Setup tasks marked [P] can run in parallel (T001 ‖ T002 ‖ T003 ‖ T004 ‖ T006).
- All foundational value types marked [P] can run in parallel (T018 through T030).
- All foundational IaC modules marked [P] can run in parallel (T009 ‖ T010 ‖ T012 ‖ T013 ‖ T015).
- All per-type domain records in US1 run in parallel (T057–T071).
- All US1 unit tests marked [P] run in parallel.
- All validation rules across stories run in parallel as their owning stories progress.
- All polish tasks marked [P] run in parallel.

---

## Parallel Example: Foundational value types

```bash
# Launch all base value types together (different files, no inter-dependencies):
Task: "Create ResourceId in api/BusTerminal.Api/Domain/ResourceId.cs"
Task: "Create ResourceName in api/BusTerminal.Api/Domain/ResourceName.cs"
Task: "Create NamespacePath in api/BusTerminal.Api/Domain/NamespacePath.cs"
Task: "Create ConcurrencyToken in api/BusTerminal.Api/Domain/ConcurrencyToken.cs"
Task: "Create LifecycleState enum in api/BusTerminal.Api/Domain/LifecycleState.cs"
Task: "Create LifecycleTransitions in api/BusTerminal.Api/Domain/LifecycleTransitions.cs"
Task: "Create EnvironmentClassification in api/BusTerminal.Api/Domain/EnvironmentClassification.cs"
Task: "Create SemanticVersion in api/BusTerminal.Api/Domain/SemanticVersion.cs"
Task: "Create AuditRecord in api/BusTerminal.Api/Domain/AuditRecord.cs"
Task: "Create OwnershipRecord in api/BusTerminal.Api/Domain/OwnershipRecord.cs"
Task: "Create Extensions in api/BusTerminal.Api/Domain/Extensions.cs"
Task: "Create Tag value type in api/BusTerminal.Api/Domain/Tag.cs"
Task: "Create DocumentationReference in api/BusTerminal.Api/Domain/DocumentationReference.cs"
```

## Parallel Example: User Story 1 per-type records

```bash
# Launch all 14 first-class type records simultaneously:
Task: "Create Namespace record in api/BusTerminal.Api/Domain/Resources/Namespace.cs"
Task: "Create Broker record in api/BusTerminal.Api/Domain/Resources/Broker.cs"
Task: "Create Queue record in api/BusTerminal.Api/Domain/Resources/Queue.cs"
Task: "Create Topic record in api/BusTerminal.Api/Domain/Resources/Topic.cs"
Task: "Create Subscription record in api/BusTerminal.Api/Domain/Resources/Subscription.cs"
Task: "Create MessageContract record in api/BusTerminal.Api/Domain/Resources/MessageContract.cs"
Task: "Create ProducerApplication record in api/BusTerminal.Api/Domain/Resources/ProducerApplication.cs"
Task: "Create ConsumerApplication record in api/BusTerminal.Api/Domain/Resources/ConsumerApplication.cs"
Task: "Create Team record in api/BusTerminal.Api/Domain/Resources/Team.cs"
Task: "Create EnvironmentResource record in api/BusTerminal.Api/Domain/Resources/EnvironmentResource.cs"
Task: "Create TagResource record in api/BusTerminal.Api/Domain/Resources/TagResource.cs"
Task: "Create Policy record in api/BusTerminal.Api/Domain/Resources/Policy.cs"
Task: "Create IntegrationFlow record in api/BusTerminal.Api/Domain/Resources/IntegrationFlow.cs"
Task: "Create DocumentationAsset record in api/BusTerminal.Api/Domain/Resources/DocumentationAsset.cs"
Task: "Create UnknownResource record in api/BusTerminal.Api/Domain/Resources/UnknownResource.cs"
```

---

## Implementation Strategy

### MVP First (Phases 1 + 2 + 3 — Setup + Foundational + US1)

1. Complete Phase 1 (Setup).
2. Complete Phase 2 (Foundational). This is the bulk of the chassis work and is the long pole.
3. Complete Phase 3 (US1 — canonical inventory of 14 types + JSON round-trip + fixture-load).
4. **STOP and VALIDATE**: Run `quickstart.md` Path A. Confirm SC-001 + SC-008 (partial — at least the universal rules fire).
5. Demo: every first-class resource type round-trips, the fixture set materializes, validation runs.

### Incremental delivery

1. MVP (Setup + Foundational + US1) → SC-001 + universal validation evidence. ✅ Ship-ready.
2. Add US2 (ownership) → SC-002 evidence. → Demo "who owns this?" query.
3. Add US3 (relationships) → SC-003 evidence. → Demo "who consumes this topic?" traversal.
4. Add US5 (lifecycle + soft-delete) → SC-004 + SC-005 + SC-012 evidence. → Demo deprecate → retire → archive + soft-delete-and-restore.
5. Add US4 (contracts), US6 (extensions), US7 (environments) → these are independent of each other and can land in any order. Each delivers its specific SC.
6. Add US8 (import/export) → SC-009 evidence. → Demo backup + restore via export.
7. Polish (Phase 11) → SC-011 + final quickstart validation + docs + CI gate updates.

### Parallel team strategy

With multiple developers post-Foundational:

1. Team completes Phase 1 + Phase 2 together (most of Phase 2's IaC and value-type tasks are [P]).
2. Once Foundational lands:
   - Developer A: US1 (the bulk — 14 type records + validation rules + tests).
   - Developer B: US2 (ownership — small surface; can start as soon as the first few US1 type records exist).
   - Developer C: US3 (relationships — independent surface; can start in parallel with US1/US2).
   - Developer D: US5 (lifecycle + soft-delete — touches the persistence adapter; coordinate with whoever owns Foundational T050).
3. P2 stories (US4, US6, US7) absorbed by whoever finishes their P1 first.
4. US8 lands last (depends on the change-log being populated by US5 + relationships being populated by US3).
5. Polish (Phase 11) runs cross-team at the end with the runbook gate at T155/T156.

---

## Notes

- [P] tasks = different files, no dependencies on incomplete tasks.
- [Story] label maps task to specific user story for traceability.
- Each user story is independently completable and testable (the spec's Independent Test text was written to guarantee this).
- Verify tests fail before implementing (TDD-friendly path).
- Commit after each task or logical group (auto-commit hooks are configured per `.specify/extensions.yml`).
- Stop at any checkpoint to validate the story independently.
- Avoid: vague tasks, same-file conflicts, cross-story dependencies that break independent shipability.
- **MCP consultation is required** for: Cosmos SDK (Microsoft Learn), AVM modules (Microsoft Learn), AAD-RBAC role assignment (Microsoft Learn), OpenTelemetry .NET (Microsoft Learn), System.Text.Json polymorphism (context7), YamlDotNet (context7), JSON Schema .NET libraries (context7). When a task touches one of these domains and the relevant MCP has not been consulted, treat it as a process failure per `CLAUDE.md` "MCP Consultation Default."
