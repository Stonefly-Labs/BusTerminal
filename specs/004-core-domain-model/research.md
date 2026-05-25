# Research — Core Domain Model (Phase 0)

**Plan**: [plan.md](./plan.md) · **Spec**: [spec.md](./spec.md)

Resolution record for the technical unknowns surfaced by the spec and the Technical Context. Each topic records the decision, rationale, and rejected alternatives. MCP citations indicate which knowledge source agents must consult during implementation tasks.

---

## 1. Cosmos DB SDK choice

**Decision**: `Microsoft.Azure.Cosmos` 3.x (latest stable on NuGet).

**Rationale**: Microsoft Learn confirms 3.x is the supported mainline .NET SDK for the SQL API. It targets `netstandard2.0`/`net8.0`+ and runs fine on `net10.0`. Native async, native `JsonSerializerOptions` integration (so we can plug our System.Text.Json converters directly), and full AAD-RBAC data-plane support.

**Alternatives rejected**:

- `Microsoft.Azure.DocumentDB` 2.x — superseded; missing critical APIs (bulk, async streams, AAD-RBAC). Migration cost vs. starting on 3.x: trivial; the 3.x API is already the documented current surface.
- `Microsoft.Azure.Cosmos` V4 preview — preview; we do not adopt previews for foundational persistence in a slice the entire platform will depend on.

**MCP for implementation**: Microsoft Learn MCP for SDK usage patterns, AAD-RBAC role configuration, optimistic concurrency via ETag.

---

## 2. Cosmos DB authentication

**Decision**: `DefaultAzureCredential` (via the existing `IAzureCredentialFactory` from slice 003). The Container App's UAMI is granted the **`Cosmos DB Built-in Data Contributor`** role at the database scope. No account keys anywhere — not in `appsettings`, not in container env, not in Key Vault.

**Rationale**: Tech-stack §7 requires Managed Identity over secrets. Cosmos DB AAD-RBAC for the data plane has been GA since 2022; it covers every operation we use (read item, query, create, replace, delete, change feed). Slice 003 already standardized `DefaultAzureCredential` resolution and we reuse the same chain locally (Azure CLI → VS Code → emulator-specific path).

**Alternatives rejected**:

- Account keys in Key Vault — defeats Managed Identity preference; introduces secret rotation; secret-scanner false-positive surface.
- Connection string — same as above; explicitly forbidden by FR-024 + SC-011.
- Resource tokens — Cosmos resource tokens (per-user permission tokens) are a server-side delegation mechanism for client-side scenarios. We don't have client-side direct Cosmos access; the backend always intermediates.

**Local dev nuance**: The Cosmos Linux emulator currently requires the emulator master key for the **data plane** in early preview builds — AAD-RBAC against the emulator is on the roadmap but not yet GA. The `CosmosClientFactory` therefore checks for an emulator endpoint and falls back to the well-known emulator key (which is a hardcoded public string, not a secret). See `quickstart.md` for the env-gating. Production / dev Azure environments always use AAD.

**MCP for implementation**: Microsoft Learn MCP for `Cosmos DB Built-in Data Contributor` role assignment via `azurerm_cosmosdb_sql_role_assignment`.

---

## 3. Cosmos container composition via AVM

**Decision**: Use **`Azure/avm-res-documentdb-databaseaccount/azurerm`** for the **account**. Wrap container declarations in a thin **first-party module** (`iac/modules/cosmos-canonical-store/`) that takes the AVM account as input and composes the two containers (resources, change-events) with per-container indexing policies, partition keys, and (future) TTL.

**Rationale**: AVM coverage for the Cosmos DB *account* is mature (encryption at rest, AAD-only data access, backup mode, capacity mode all expressible). AVM coverage for *containers* exists but its `containers` input map shape is awkward for our needs: each of our two containers wants distinct indexing policies (the canonical container's `extensions.*` paths should be excluded from indexing per FR-012's per-extension indexing-inclusion control; the change-event container indexes only `resourceId` and `eventTimestamp`). Expressing two heterogeneous container policies via the AVM input map is more painful than authoring a small wrapper module. We get the security and observability defaults from AVM and the policy precision from the wrapper.

**Alternatives rejected**:

- Pure AVM with all containers declared in the account module call — fights the AVM input shape; per-container indexing policies become unreadable.
- Hand-authored Cosmos account (no AVM) — gives up AVM's secure-by-default and observability outputs; needs more `checkov` allowlist entries.

**MCP for implementation**: Microsoft Learn MCP for AVM Cosmos DB module reference; tech-stack §6 for AVM versioning policy.

---

## 4. Partition key for the canonical `resources` container

**Decision**: `/resourceType` as the partition key. Starting-point default per the spec's explicit deferral of final partition strategy.

**Rationale**:

- The dominant read pattern at v1 is type-scoped: "all queues," "all topics," "all teams." `/resourceType` co-locates these onto a single logical partition each.
- The hottest single partition will be **subscriptions**, **queues**, or **topics** (the messaging primitives). Even at large enterprise scale (10K+ queues), this stays under Cosmos's 20 GB / 10K RU/s per logical partition limit by a comfortable margin (a typical queue document is < 4 KB; 10K queues × 4 KB = 40 MB).
- A future operational slice can introduce a hierarchical partition key (`/resourceType/namespacePath`) without a data migration — Cosmos supports adding hierarchical partition keys to existing containers via the new V2 partition-key model.

**Alternatives rejected**:

- `/id` — kills cross-resource queries (each document on its own partition); cross-partition queries are RU-expensive.
- `/namespacePath` — co-locates by namespace which sounds appealing, but the long-tail of empty/sparse namespaces creates many tiny partitions; type-scoped queries become cross-partition.
- `/ownership/teamId` — co-locates by team which serves the "all resources owned by team X" query well, but every other query becomes cross-partition. This is genuinely a candidate for a *secondary projection* (denormalized to a separate container by a future search/governance slice) rather than the primary partition.

**Documented forward path**: Operational slice can rebalance to hierarchical partition `(/resourceType, /namespacePath)` without code changes — the SDK abstracts partition-key composition.

**MCP for implementation**: Microsoft Learn MCP for Cosmos partition-key best practices and the hierarchical partition key feature.

---

## 5. Partition key for the change-event container

**Decision**: `/resourceId` as the partition key.

**Rationale**: The dominant query pattern on the change log is "history of resource X" — point-reads of all events for one resource. `/resourceId` co-locates every event for a single resource on one logical partition, enabling that query as an in-partition scan (cheap). The change log is append-only and writes are per-resource so write contention is impossible.

**Alternatives rejected**:

- `/eventTimestamp` bucket (e.g., `/eventDate`) — temporal locality is irrelevant for our query patterns; "what happened on this day across all resources" is a governance-pipeline question (better served by a stream-based projection) and is out of scope for this slice.
- Same as canonical (`/resourceType`) — co-locates events for unrelated resources of the same type, hurts the per-resource history query.

**MCP for implementation**: Microsoft Learn MCP for Cosmos append-only patterns.

---

## 6. Polymorphic JSON serialization for the Resource hierarchy

**Decision**: `System.Text.Json` with the **`[JsonPolymorphic]`** + `[JsonDerivedType]` attribute model on the abstract `Resource` base. Discriminator: `"resourceType"` (string). Custom converter `ResourceJsonConverter` handles the unknown-type case (Q4) by falling through to `UnknownResource`. Extensions dictionary uses `ExtensionsJsonConverter` to preserve structured `JsonElement` values.

**Rationale**: STJ in .NET 10 has first-class polymorphism with a custom-discriminator-name option. It's the official Microsoft path, ships in-box, and integrates with the Cosmos SDK's serializer pipeline without adapter code. Performance is excellent (source-generated converters available if we need them later). The `UnknownResource` fallback is the FR-002 + Q4 unknown-type behavior — implemented as a custom converter wrapping the polymorphic dispatcher.

**Alternatives rejected**:

- `Newtonsoft.Json` — would require dragging a second JSON library into the codebase. Cosmos SDK still defaults to Newtonsoft for older customers; we explicitly opt the client into STJ via `CosmosClientOptions.Serializer`. No reason to keep Newtonsoft.
- Custom hand-written serializer — too much code; STJ polymorphism is mature.

**MCP for implementation**: context7 MCP for the latest `System.Text.Json` polymorphism patterns in .NET 10 (the API has shifted between 7/8/9/10).

---

## 7. Validation framework: build vs. adopt

**Decision**: Build a **small first-party validation engine** in `BusTerminal.Api/Domain/Validation/`. Pattern: `IValidationRule` with `Validate(Resource, ValidationContext) → IEnumerable<ValidationFinding>`. The engine dispatches per resource type (rules can be registered as universal or type-specific). Findings carry severity (Q3).

**Rationale**:

- The rule set in FR-013 is small (7 rules) and bespoke (none are off-the-shelf — relationship-validity, dangling-reference, lifecycle-transition all require domain context).
- Graded severity (Q3) is not a first-class concept in FluentValidation. Implementing it on top of FluentValidation requires either subclassing every rule or wrapping the API in a shim that's larger than a first-party engine.
- The engine is < 300 LOC including tests; a third-party dependency for that volume is a poor trade.
- First-party gives us full control over the finding shape (which is itself a persisted contract per FR-013 and the `validation-result.schema.json`).

**Alternatives rejected**:

- **FluentValidation** — popular but optimized for input-DTO validation, not domain-object validation with cross-resource context. Adding severity, finding metadata, and dangling-reference resolution as adapters costs more than a first-party engine.
- **JSON Schema runtime validation** — we *do* use JSON Schema as the contract for serialization (`contracts/*.schema.json`), but runtime relationship validation, lifecycle-transition validation, ownership-presence validation, and duplicate-detection validation all need cross-document context that JSON Schema cannot express.
- **NValidator / DataAnnotations** — annotation-based; doesn't support per-rule severity or cross-document context.

**MCP for implementation**: context7 MCP for any .NET 10 idiom we want to lean on (collection expressions, primary constructors in records).

---

## 8. YAML library choice

**Decision**: **`YamlDotNet`** 16.x.

**Rationale**: De-facto .NET YAML library; trusted (millions of downloads, active maintenance, BSD license); supports the round-trip semantics SC-009 requires. Used only inside the import/export serializer; not exposed elsewhere in the codebase.

**Alternatives rejected**:

- `Microsoft.Extensions.Configuration.Yaml` — config-binding library, not a general-purpose YAML serializer.
- Hand-write a YAML emitter — absurd; YAML is too complex for ad-hoc emission.
- Convert YAML to JSON via an external CLI (`yq`) and round-trip via STJ — adds a runtime dependency on a non-.NET tool; rejected.

**MCP for implementation**: context7 MCP for YamlDotNet usage patterns (its API surface is stable but has multiple builder styles).

---

## 9. Cosmos emulator for local dev

**Decision**: The **Linux container** emulator (`mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:vnext-preview`) added as a service in `docker-compose.yml`. Endpoint `https://localhost:8081`; well-known master key documented in `quickstart.md`.

**Rationale**: The Windows emulator does not run on the macOS contingent of the dev team without WSL2 or a full Windows VM. The Linux emulator works natively on macOS (arm64 and x64) and Linux. It is "preview-but-stable" — Microsoft has been signaling GA for over a year; production parity is sufficient for our integration tests.

**Local dev pathway**:

```bash
docker compose up -d cosmos-emulator
dotnet test --filter "Category=Integration"
```

**Alternatives rejected**:

- Windows emulator — non-starter on macOS without VM overhead.
- Real dev Cosmos account for every developer — provisioning friction, RU cost, and a unique-per-dev container collision story. Reserved for nightly integration runs and CI smoke tests against the dev environment.
- TestContainers wrapper for the emulator — possible future polish; not needed for v1 since `docker-compose.yml` covers the integration runtime adequately.

**MCP for implementation**: Microsoft Learn MCP for Cosmos Linux emulator configuration and the AAD-RBAC roadmap (so we know when to drop the emulator-key fallback).

---

## 10. Optimistic concurrency mechanism

**Decision**: Cosmos DB's native **ETag** model, surfaced through `Microsoft.Azure.Cosmos`'s `ItemResponse.ETag` and `ItemRequestOptions.IfMatchEtag`. Stale writes are rejected with `412 Precondition Failed`; we map that to a domain-level `ConcurrencyConflictException` in `ConcurrencyExceptionMapper.cs` (Q2 → FR-025).

**Rationale**: Q2's recommended option is ETag-based optimistic concurrency. Cosmos provides ETag natively at no cost. We do **not** invent a separate monotonic version counter; the ETag is the concurrency token. We surface it through the domain model as `ConcurrencyToken` (an opaque wrapper around a string) so the persistence detail doesn't leak.

**Alternatives rejected**:

- Custom monotonic integer counter on each document — requires reading-before-writing on every update; ETag does this in one round-trip.
- Last-writer-wins (Q2 option B) — rejected per Q2 clarification.
- Pessimistic Cosmos session tokens — not what session tokens do (they're read-consistency tokens, not concurrency control).

**MCP for implementation**: Microsoft Learn MCP for `ItemRequestOptions.IfMatchEtag` and `CosmosException.StatusCode == 412` handling.

---

## 11. Soft-delete query filter

**Decision**: Application-level **`isDeleted: true`** boolean field on the canonical document + **query-time filter** in `ICanonicalResourceStore`. Default `GetAsync` and `QueryAsync` methods apply the filter; `GetAsync(includeDeleted: true)` and `GetDeletedAsync` opt back in for restoration workflows.

**Rationale**: Cosmos's built-in TTL would delete the document outright, taking the audit history and relationship lineage with it — that violates FR-020 directly. We need application-level soft-delete because the spec mandates retention of identifier, audit history, and relationships *after* deletion. Indexing on `isDeleted` is cheap and the filter is enforced in one place.

**Retention policy**: This slice does **not** enforce a retention duration on soft-deleted documents. Per the spec's deferral of "retention window defaults to industry standard," a future operational slice will introduce either Cosmos TTL on the soft-delete predicate (via a derived TTL property) or a scheduled-job-driven hard-delete. Recorded in the spec's Assumptions.

**Alternatives rejected**:

- Cosmos TTL = soft delete — destroys the document including audit and relationships; contradicts FR-020.
- Hard delete + separate "tombstone" container — adds operational complexity; the canonical query path now has to query two containers; restoration is harder.
- Versioned-immutable-events model (event sourcing for the resource itself) — over-engineering; the spec's change-event log (Q5) already serves the audit need and is a separate concern.

**MCP for implementation**: Microsoft Learn MCP for Cosmos query patterns and indexing exclusions (we exclude `extensions.*` paths per FR-012 per-extension indexing-inclusion control).

---

## Summary

All 11 research topics resolved. Zero `NEEDS CLARIFICATION` markers remain. The plan can proceed to `/speckit-tasks` without re-running `/speckit-clarify`.
