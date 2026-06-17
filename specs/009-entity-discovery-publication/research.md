# Phase 0 Research: Entity Discovery and Publication

**Spec**: [spec.md](./spec.md) · **Plan**: [plan.md](./plan.md) · **Date**: 2026-06-17

This document records the technical decisions taken during Phase 0 of the `/speckit-plan` workflow for Spec 009. Each decision is paired with rationale and the alternatives that were evaluated and rejected. There are **no open `NEEDS CLARIFICATION` items**; all four spec-level ambiguities were resolved during `/speckit-clarify` on 2026-06-17 (see `spec.md` Clarifications section).

---

## R-01 — Azure SDK choice for entity listing

**Decision**: Use **`Azure.ResourceManager.ServiceBus` 1.x** (ARM management plane) as the primary surface for listing queues, topics, subscriptions, and rules.

**Rationale**:
- Required RBAC is **Reader** at the namespace scope — least privilege, aligns with constitution Principle IV. The Service Bus *admin protocol* (`ServiceBusAdministrationClient`) requires the much broader "Manage" claim or "Azure Service Bus Data Owner" role, which would grant the platform write access it does not need (spec non-goal: "platform issues only read calls").
- Already in use in the repo by `Infrastructure/ServiceBus/ArmNamespaceProbe` (spec 008). Reusing the same SDK avoids introducing a second Azure Service Bus client surface and reuses the existing `ArmClient` singleton and credential factory.
- ARM returns every attribute called out in FR-004 through FR-007 for Standard and Premium tiers (Basic tier topics/subscriptions n/a). Filter expressions, action expressions, and rule types are all on `ServiceBusRuleResource.Data.{SqlFilter|CorrelationFilter|Action}`.
- Forward-allowlisted role GUID (`acdd72a7-3385-48ef-bd42-f606fba81ae7` — "Reader") already exists in `iac/platform-bootstrap` per the IaC report; the existing runbook (`iac/runbooks/grant-namespace-reader.md`) covers per-namespace grants.

**Alternatives considered and rejected**:
- **`Azure.Messaging.ServiceBus.Administration.ServiceBusAdministrationClient`** — the data-plane admin client. Rejected because it requires "Manage" claim / Data Owner, which over-grants. Would also introduce a second SDK surface for the same operations.
- **Raw ARM REST + `HttpClient`** — bypasses the typed SDK. Rejected: no benefit, more maintenance, loses Azure SDK retry policy and OpenTelemetry integration.
- **Mix of ARM (for queues/topics) + admin client (for subscriptions/rules)** — sometimes proposed for performance. Rejected: the performance differential at our scale is not measurable, and the security cost of broader RBAC is real.

---

## R-02 — Compute model for the discovery worker

**Decision**: Add a new function to the **existing `BusTerminal.Indexer` project** with a **Service Bus queue trigger** (`[ServiceBusTrigger("discovery-requested")]`). No new compute resource is introduced.

**Rationale**:
- `BusTerminal.Indexer` is already deployed as a Functions v2 native container (`Microsoft.App/containerApps` with `kind = "functionapp"`), already wired with the workload UAMI, already configured for managed-identity-only Storage and Cosmos auth, and already routes diagnostics to LAW via the existing IaC.
- A Service Bus queue trigger gives us **natural durability** (the message survives crashes, restarts, and scale-to-zero), **natural retry** (delivery retries with built-in dead-letter), **natural backpressure** (queue depth caps concurrent runs), and **natural traceability** (the Functions Service Bus binding propagates W3C Trace Context message properties out of the box). All of these would be hand-rolled if we used HTTP or a Container Apps Job.
- The same Functions worker can scale horizontally (KEDA + Service Bus queue length scaler) when many namespaces request discovery simultaneously, without any code changes.
- Service Bus binding supports AAD-only ("`__fullyQualifiedNamespace`" mode) — no SAS or connection string required, satisfying the constitution's "no embedded credentials" rule.

**Alternatives considered and rejected**:
- **Container Apps Job triggered per request** — the existing `probe-job-internal-caller` pattern. Rejected because (a) per-job startup latency (~10–20 s for image pull and runtime spin-up) erodes the 5-min SC-005 budget; (b) job triggering requires the API to call ARM, adding management-plane latency and another RBAC grant; (c) no built-in dead-letter; (d) Container Apps Jobs are not well suited to fan-in (one job per request multiplies operational visibility cost).
- **Run discovery synchronously inside the API request handler** — rejected because (a) the spec says discovery is async (Assumption: "initiating discovery returns a discovery run reference immediately"); (b) blocks an API container thread for up to 5 minutes; (c) makes coalescing (FR-003) more complex and prone to races; (d) no durability if the API pod crashes mid-run.
- **HTTP-triggered Function called from the API** — rejected because (a) reintroduces fan-out latency at request time; (b) no durability if the function host restarts mid-call; (c) the Functions v2 model already prefers Service Bus triggers for the "request → durable async work" pattern.

---

## R-03 — FR-003 coalescing implementation (concurrent discovery requests)

**Decision**: Acquire a **per-namespace Cosmos lock document** before enqueueing. The lock lives in a new `discovery-locks` container with PK `/namespaceId` and `id = "lock"` (one document per namespace partition). Acquisition uses **Cosmos optimistic concurrency** (`IfMatch` ETag) plus a deterministic `currentRunId` field. On conflict (lock already held), the API reads the lock, fetches the referenced DiscoveryRun, and returns it as the coalesced response.

**Rationale**:
- Atomic per-namespace serialization with no separate distributed-lock infrastructure.
- Cosmos ETag-based concurrency is already the project's idiom (`CosmosRegistryEntityStore` uses it for registry entities); no new pattern to learn.
- Lock document carries the active `discoveryRunId` and `acquiredUtc`, giving the API everything it needs to return the in-flight run reference per FR-003 (the response includes a `coalescedFromExisting: true` field so the caller can observe whether a new run was started).
- Lock is **released by the worker** when it transitions the DiscoveryRun to a terminal status (Succeeded / Failed). A separate **expiry sweep** (5-minute TTL evaluated lazily on every acquisition attempt) recovers locks from worker crashes — if the lock is older than `5 min + 30s grace`, the API may forcibly steal it and mark the orphaned DiscoveryRun as Failed with reason `WorkerLost`.

**Alternatives considered and rejected**:
- **Service Bus session ID = namespace ID** — uses Service Bus's own session locking to serialize per-namespace consumption. Rejected: works for serialization but doesn't help the *API* know there's an in-flight run before enqueueing (the API would have to enqueue blindly and a downstream consumer would coalesce, which loses the synchronous coalesced response the spec requires).
- **Azure Storage blob lease** — adds a second storage system for coordination. Rejected: extra dependency, extra IaC, no advantage over Cosmos.
- **In-memory lock in the API process** — rejected: API runs multiple replicas (Container Apps scales); in-memory state is per-pod, doesn't serialize across pods.

---

## R-04 — Retry policy for transient ARM errors (FR-021a)

**Decision**: Tune the **Azure SDK's built-in `RetryOptions`** on the `ArmClient` used by the worker — `Mode = Exponential`, `MaxRetries = 3`, `Delay = 800 ms`, `MaxDelay = 5 s`. Do not implement a custom retry wrapper.

**Rationale**:
- The Azure SDK's built-in retry policy already handles HTTP 429 (Retry-After honored), 500, 502, 503, 504, and transient transport exceptions. Adding a hand-rolled retry layer would double-retry and risk exceeding the 5-min budget.
- 3 attempts × cumulative ~5 s ceiling × roughly the slowest-10% of calls = ~30 s total retry overhead in the worst plausible burst. Well within SC-005's budget at the 32-way parallel target.
- Authentication/authorization failures (401/403), 404s for the namespace itself, and 400 (bad request) are **not** retriable by the SDK default — they fail fast and bubble out as `RequestFailedException`. The worker's exception handler classifies these as terminal and fails the DiscoveryRun immediately per FR-021a.

**Alternatives considered and rejected**:
- **Polly with custom policies** — rejected: introduces a new dependency for behavior the Azure SDK already provides. Pre-existing project pattern (`ArmNamespaceProbe`) uses the SDK's retry options directly.
- **Unbounded "keep trying until budget" retry** — explicitly rejected during `/speckit-clarify` (Option C was not selected; Option B with bounded retry won).

---

## R-05 — Parallelism strategy in the worker

**Decision**: Use **`Parallel.ForEachAsync` with `MaxDegreeOfParallelism = 16`** for the per-topic subscription fan-out and the per-subscription rule fan-out. Sequential outer iteration for queues and topics. Use a **bounded `Channel<PublishedEntityChange>`** between the discovery pipeline and the Cosmos write batcher; writer concurrency = 32.

**Rationale**:
- Math: 5,000 subscriptions / 16-way concurrency × ~100 ms ARM call = ~31 s. 5,000 rules / 16-way concurrency × ~100 ms = ~31 s. 500 topics sequential × ~50 ms = ~25 s. Queues 500 sequential × ~50 ms = ~25 s. Cosmos upsert 10,000 entities / 32-way × ~20 ms = ~6 s. Plus startup + lock-acquire + result write ≈ 30 s overhead. Total budget: ~150 s for the largest namespace — leaves >50% headroom against the 5-min SC-005 ceiling.
- `Parallel.ForEachAsync` is the modern .NET 10 idiom; respects cancellation; clean back-pressure when paired with `Channel<T>`.
- 16-way ARM concurrency stays well under documented ARM throttle limits (~12,000 reads per hour per subscription is the lowest documented cap) for a single namespace's worth of traffic. Multiple namespaces' discoveries running in parallel will compound — KEDA scaling on the worker is the natural release valve, but the **shared budget** is a known watch item (see R-10).
- 32-way Cosmos write concurrency keeps RU/s usage in the documented serverless / autoscale headroom for a 10,000-document burst.

**Alternatives considered and rejected**:
- **TPL Dataflow** — rejected: more machinery than needed for a linear pipeline. `Parallel.ForEachAsync` + `Channel<T>` is the idiomatic equivalent.
- **Full sequential** — rejected: blows the 5-min budget at SC-005 scale (estimated 8+ minutes for 5,000 subscriptions × 100 ms sequentially).
- **`Task.WhenAll(allEntities.Select(...))`** with no bound — rejected: unbounded concurrency would burst-throttle ARM and Cosmos and is a known anti-pattern at scale.

---

## R-06 — Cosmos schema choice for PublishedEntity

**Decision**: **Extend the existing `registry-entities` container** (PK `/environment`) with three new top-level fields per entity document: `lifecycleStatus`, `azureSourced` (sub-document), and `serviceAssociations` (array of `EntityServiceAssociation`). Do not introduce a separate `published-entities` container.

**Rationale**:
- Spec 006 already models the registry catalog using `registry-entities` with entity-type discriminators (`Queue` / `Topic` / `Subscription` / `Rule`). Spec 009's "published entities" *are* the same domain object as Spec 006's manually-curated entities — they're the natural extension of that model with an automatically-populated technical-attributes blob.
- Single-container reads keep search projection, change-feed propagation, audit hookup, ETag-based metadata edits, and the existing AI Search index trivial to extend (no second indexer, no second change-feed lease container).
- Constitution Principle III: "Domain terminology MUST remain consistent across APIs, UI, documentation, database models, search indexes, and telemetry." A separate `published-entities` container would create two parallel domain models for the same concept — exactly the kind of synonym drift the principle prohibits.
- The 2 MB Cosmos document size limit is comfortable for the largest expected entity payload: Azure-sourced attributes are flat (≤ 2 KB), service associations cap at a few hundred entries (≤ 30 KB), curated metadata is text-bounded (≤ 50 KB).

**Alternatives considered and rejected**:
- **Separate `published-entities` container** — rejected per the Principle III argument plus the extra indexer/change-feed surface area.
- **Sub-document inside the parent namespace document** — rejected: entity counts (10,000+ per namespace) exceed the 2 MB single-doc limit.
- **Separate document per (entity, source) tuple** — rejected: introduces a join requirement on every read for the entity detail view; the existing single-document reads stay simple.

---

## R-07 — Stable entity identity (FR-009)

**Decision**: Derive the registry `id` of a PublishedEntity from a **stable hash** of the entity's **Azure Resource Identifier path** within the namespace, namely:
- Queue: `q:{namespaceId}/{queueName}`
- Topic: `t:{namespaceId}/{topicName}`
- Subscription: `s:{namespaceId}/{topicName}/{subscriptionName}`
- Rule: `r:{namespaceId}/{topicName}/{subscriptionName}/{ruleName}`

Hash with SHA-256 → take first 24 base32-encoded characters → prepend `pe_` for human-readability (e.g., `pe_2HJZW6XQGKLM8YN5R4P7S9TB`). Store both the human-readable composite key (`compositeKey: "q:..."`) and the hashed `id` on the document; index both.

**Rationale**:
- Stable across discoveries: the same Azure resource always hashes to the same `id`, so repeated discoveries update the existing record (FR-029 idempotency).
- Human-debuggable: the prefix + composite key makes triage in Cosmos Data Explorer straightforward.
- Short and URL-safe: works in path segments without escaping (e.g., `GET /api/entities/pe_2HJZW6XQGKLM8YN5R4P7S9TB`).
- Name changes in Azure produce a new identity (a renamed queue is a different queue in the registry) — consistent with how Azure Service Bus itself models entity identity. Renames in Azure surface as a Missing of the old name and a New of the new name; users can re-link curated metadata if desired (a future spec could auto-link on rename detection via property heuristics).
- Hashing the namespace ID (a stable Cosmos document ID) instead of subscription/RG/namespace names avoids brittleness when an Azure namespace is re-registered with the same ARM coordinates but a different platform-side namespace document.

**Alternatives considered and rejected**:
- **Use the raw ARM resource ID as `id`** — rejected: ARM IDs are 100+ chars long, contain slashes and casing that complicate URL handling, and tie the registry identity to ARM-formatting choices that may evolve.
- **GUID assigned on first discovery** — rejected: breaks idempotency for entities that get deleted-then-recreated with the same name, and adds a lookup step on every discovery (must query by `compositeKey` to find existing).
- **Composite (namespaceId, name) without hash** — rejected: human-readable but exceeds Cosmos `id` length limits when concatenated for deeply-nested rules.

---

## R-08 — Change-detection algorithm

**Decision**: Compute a deterministic **SHA-256 hash over a canonical JSON serialization** of the `azureSourced` sub-document (sorted keys, normalized number formats, normalized TimeSpan formats). Compare against the persisted `azureSourcedHash` field. Classify:
- `azureSourcedHash` absent → **new**.
- `azureSourcedHash` present, equal → **unchanged** (update `lastSeenUtc` only; no other writes).
- `azureSourcedHash` present, different → **updated** (overwrite `azureSourced.*` and `azureSourcedHash`; preserve every curated field).

Missing detection runs as a **second pass** after the entity-walk completes: query `registry-entities` for the namespace where `lifecycleStatus = Active` and `lastSeenUtc < runStartUtc`; for each, set `lifecycleStatus = Missing`.

**Rationale**:
- Hash-based comparison is O(1) per entity at classification time — no field-by-field walk.
- Canonical JSON serialization is deterministic and language-agnostic (the same algorithm could be implemented in any future Spec 009 worker port).
- Two-pass missing detection avoids a per-entity "did Azure return this?" lookup during the streaming pass; the run-start cutoff timestamp is the natural watermark.
- Idempotent under FR-029: an unchanged entity becomes a single `lastSeenUtc` update (one Cosmos write, no impact on hash or curated fields), exactly meeting "MUST NOT alter the Active set or any Azure-sourced field values beyond updating LastSeenTimestamp."

**Alternatives considered and rejected**:
- **Field-by-field diff** — rejected: more code, more chances for synonym drift between the Azure SDK property names and the registry field names.
- **Last-modified watermark from Azure (if available)** — rejected: not all entity types expose a reliable `updatedAt` from ARM (and partial-update semantics vary across SDK versions). Hash comparison is robust regardless.
- **Always overwrite (no classification)** — rejected: violates FR-013's classification requirement and inflates Cosmos RU usage with redundant writes.

---

## R-09 — Service association data placement (M:N)

**Decision**: Embed `serviceAssociations: EntityServiceAssociation[]` as a **denormalized array inside each PublishedEntity document**. A separate `EntityServiceAssociation` Cosmos document is not introduced.

**Rationale**:
- Reads are entity-centric: when rendering the entity detail or filtering the catalog, the associations are needed together with the entity. Embedding eliminates per-entity joins.
- Writes are entity-scoped: adding/removing an association updates one document (the entity's), respecting Cosmos's single-document-transaction model.
- Bounded cardinality: realistic associations cap at ~tens per entity (a topic with 10 producers and 10 consumers is unusual). Stays well within Cosmos's 2 MB document limit.
- For the inverse query ("show all entities associated with service X"), the AI Search projection (R-11) carries `associatedServiceIds[]` and `associationRoles[]` as filterable facets — fast inverted index lookups instead of cross-document Cosmos joins.

**Alternatives considered and rejected**:
- **Separate `entity-service-associations` container** — rejected: extra container, extra indexer surface, joins required for the common entity-centric read.
- **Embed in the service document instead of the entity document** — rejected: makes the entity detail view slower and forces a fan-out read across many service docs.
- **Edge collection (graph-style)** — rejected: over-engineered for the M:N cardinality we have; Cosmos doesn't natively model graph edges and would require a custom abstraction.

---

## R-10 — Cross-namespace ARM throttling shared budget

**Decision**: Accept the shared-budget risk for v1. Document the constraint, monitor it via the new `discovery.run.duration` metric, and add a follow-up spec slot if observed sustained throttling appears.

**Rationale**:
- ARM throttle scope is per-subscription, not per-namespace. Two large discoveries running simultaneously against namespaces in the same subscription compete for the same budget.
- At expected v1 scale (~10 dev namespaces, growing to ~500), simultaneous large-namespace discoveries are uncommon and naturally serialized by user behavior (an admin triggering discovery rarely does so for multiple namespaces at once).
- Implementing a cross-namespace concurrency cap now (e.g., a global "discovery permit" semaphore) is premature optimization (Decision Priority 7) and would require either a distributed lock service or a hand-rolled coordinator — neither of which fits the "operational simplicity" Decision Priority 1.
- If observed throttling becomes a problem (worker telemetry will show it as elevated `discovery.run.duration` paired with non-zero retry counts in the `discovery.runs.completed` metric), a future spec can introduce KEDA queue concurrency caps or per-subscription semaphores.

**Alternatives considered and rejected**:
- **Global semaphore in shared storage** — rejected as premature; see above.
- **Single-instance worker (no horizontal scaling)** — rejected: removes the natural KEDA scaling that makes per-namespace discovery responsive when multiple admins act at once.

---

## R-11 — AI Search index schema evolution

**Decision**: Extend the existing `registry-entities-v1` index with three new fields (`lifecycleStatus`, `associatedServiceIds[]`, `associationRoles[]`, `azureSourced` as a complex type). Do **not** create a `registry-entities-v2` index in v1; the additions are backward-compatible (new fields are nullable).

**Rationale**:
- Azure AI Search supports adding non-filterable, non-sortable, non-facetable fields without re-indexing. We make `lifecycleStatus` and the association arrays filterable/facetable (re-indexing the existing corpus once during the spec 009 rollout is acceptable — the canonical-rebuild script in `iac/scripts/` already exists for this).
- Keeping a single index avoids dual-writing or routing-aware reads.

**Alternatives considered and rejected**:
- **New `registry-entities-v2` index** — rejected: doubles indexer work, requires version-aware reads in `SearchEndpoint`, and the additions are additive and backward-compatible.
- **External association index** — rejected: extra index, extra projection, no win.

---

## R-12 — Telemetry surface

**Decision**: Add a new ActivitySource `BusTerminal.Discovery` and a new Meter `BusTerminal.Discovery`.

**Spans** (parent → child):
- `discovery.run` (worker root span; attributes: `discovery.run_id`, `discovery.namespace_id`, `discovery.classification.new`, `.updated`, `.missing`)
- ↳ `discovery.fetch.queues` / `.topics` / `.subscriptions` / `.rules` (one child per scope; attributes: `discovery.entity_type`, `discovery.fetch.count`)
- ↳ `discovery.classify` (one child per batch; attributes: `discovery.classify.entity_type`, `discovery.classify.batch_size`, `discovery.classify.duration_ms`)
- ↳ `discovery.persist.batch` (one child per Cosmos batch; attributes: `discovery.persist.batch_size`, `discovery.persist.ru_consumed`)

**Metrics** (instruments):
- `discovery.runs.started` (counter, dims: `outcome` = `new` | `coalesced`)
- `discovery.runs.completed` (counter, dims: `status` = `succeeded` | `failed`, `failure_category` = `authn` | `authz` | `not_found` | `transport` | `unknown` | `n/a`)
- `discovery.run.duration` (histogram, ms, dims: `namespace_tier`)
- `discovery.entities.classified` (counter, dims: `entity_type`, `outcome` = `new` | `updated` | `unchanged` | `missing`)
- `discovery.arm.retries` (counter, dims: `failure_class`)

**Logs**: Serilog structured events at INFO for run start/completion, WARN for retry exhaustion that still ultimately succeeded, ERROR for run failures. **No** display names, descriptions, tag values, or other potentially-sensitive fields in any telemetry surface (constitution + spec assumption: "no PII in telemetry").

**Rationale**:
- The dimension set is deliberately small to keep Application Insights metric cardinality bounded.
- `failure_category` mirrors the existing `ValidationFailureCategory` enum used by `ArmNamespaceProbe` for terminology consistency.
- The span hierarchy lets ops drill from "discovery run X took longer than usual" → "the topics fetch was the bottleneck" → "specific Cosmos batch consumed unusually high RU" in three clicks.

**Alternatives considered and rejected**:
- **One span per entity** — rejected: span volume would dominate Application Insights ingestion cost at 10,000-entity scale.
- **Per-namespace metric dimension** — rejected: high cardinality (potentially thousands of namespaces at scale). Namespace ID is captured on the parent span instead.

---

## R-13 — Internal Service Bus message contract

**Decision**: The `discovery-requested` queue carries a JSON message with this shape:

```json
{
  "schemaVersion": "1.0",
  "discoveryRunId": "dr_01HZAB...",
  "namespaceId": "ns_01HKXP...",
  "requestedBy": "00000000-1111-2222-3333-444444444444",
  "requestedUtc": "2026-06-17T14:32:11.123Z",
  "correlationId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"
}
```

`correlationId` is the W3C `traceparent` of the originating API request, propagated end-to-end so spans correlate in Application Insights. The Functions worker reads this from the message property `Diagnostic-Id` (set automatically by the Service Bus SDK when an Activity is active at send time) and seeds the worker's root span with it.

**Rationale**:
- Minimal payload — the worker queries Cosmos for the full DiscoveryRun document (just-written) using the `discoveryRunId`. No risk of envelope/record divergence.
- `schemaVersion` enables future evolution without breaking in-flight messages during a rolling deploy.
- W3C Trace Context propagation satisfies constitution C-16 and the spec's project-wide trace requirement (mentioned in Assumptions).

**Alternatives considered and rejected**:
- **Carrying the full DiscoveryRun document in the message** — rejected: duplicates data, risks staleness if the API writes the DiscoveryRun and the message slightly out of order.
- **No correlation ID propagation** — rejected: explicit constitution violation (Principle V: "Diagnostic correlation (request/correlation IDs propagated end-to-end)").

---

## R-14 — UI polling cadence for in-flight discovery runs

**Decision**: When the namespace overview shows an in-flight discovery run, the `discovery-status-panel` client component polls the DiscoveryRun via TanStack Query with `refetchInterval = 3000` (3 s), `refetchIntervalInBackground = false`. Stop polling once status is terminal (`Succeeded` / `Failed`).

**Rationale**:
- 3 s is responsive without being noisy. Over a 5-minute run that's ~100 polls — well below ASP.NET cost concerns and far below any rate-limit threshold.
- Background-tab pause respects the user's browser by not burning battery / network when the tab is hidden.
- Server-Sent Events or WebSockets were considered but rejected as overkill for v1 — polling on a 3 s cadence achieves the same UX with zero new infrastructure (no signaling layer, no proxy WebSocket support concern, no reconnection-state machine).

**Alternatives considered and rejected**:
- **Server-Sent Events (SSE)** — rejected: adds a long-lived connection per active namespace overview tab, complicates the Container Apps ingress configuration, and the UX delta is invisible at 3 s polling cadence.
- **Real-time push via SignalR** — rejected: adds a new dependency and a new client surface for a use case where polling is adequate.
- **One-shot fetch + manual refresh button** — rejected: forces the user to know when to refresh; bad UX for a primary acceptance scenario (US1 AS4 implies the user sees the run progress to completion).

---

## R-15 — Authorization model for metadata edits

**Decision**: Implement a new endpoint policy `RequireEntityMetadataEditor` that resolves at request time as follows: succeed if the caller holds **any one of**:
1. `BusTerminal.Admin` (platform admin), OR
2. `BusTerminal.NamespaceAdministrator` for the entity's parent namespace, OR
3. `BusTerminal.ServiceOwner` for any service that has an `Owner`-role `EntityServiceAssociation` with the entity.

The check executes inside the endpoint handler (after the entity has been read from Cosmos), not as a static ASP.NET policy, because branch (3) depends on dynamic per-entity data. Authorization decisions are logged with their disposition for audit (terminology: `authz.decision = allow|deny`, `authz.via = admin|namespaceAdmin|serviceOwner`, no PII).

**Rationale**:
- Static policies can encode (1) and (2) via existing role claims. Branch (3) needs a runtime lookup against the entity's `serviceAssociations[]` and the caller's owned services — there is no clean way to express this as a static `[Authorize(Policy=...)]`.
- The pattern mirrors the existing `RequireNamespaceAdministrator` extension in `BusTerminal.Api/Authorization` and is invoked from the handler just after the entity is loaded — the additional Cosmos read (caller's owned services) is amortized into the handler.

**Alternatives considered and rejected**:
- **Static policy + 403 inside handler** — rejected: produces a 403 for legitimate Service Owners that fail the static check, which is a confusing UX.
- **Materialized "can edit this entity" projection updated on every association change** — rejected: premature optimization; the per-request lookup is < 5 ms.

---

## R-16 — Local development experience

**Decision**: Provide a `make discovery-smoke` (or equivalent shell script) in `quickstart.md` that:
1. Spins up the Cosmos emulator + Service Bus emulator (the latter via the new official preview emulator container).
2. Seeds a registered namespace pointing at the developer's personal dev Service Bus namespace.
3. Runs the API and Indexer locally via `dotnet run`.
4. Triggers a discovery via `curl` and tails the worker logs.

Where the official Service Bus emulator is too limited (rule discovery support varies), an HTTP recording fixture (`tests/fixtures/arm-recorded-namespace/*.json`) lets the worker integration tests run deterministically against captured ARM responses.

**Rationale**:
- Matches the existing local-dev story (Spec 008 used the same emulator-first pattern).
- ARM HTTP recording is the project's existing test idiom — same as the namespace-validation integration tests.

---

## Open Questions Deferred to Implementation Phase

None blocking. Items below are implementation-time choices that the plan flags so `/speckit-tasks` and `/speckit-implement` agents resolve them with full code-context:

- **Cosmos RU/s sizing for the `discovery-runs` container** — start at the project default autoscale floor; revisit if observed RU consumption during US1 testing exceeds the band.
- **KEDA scale rule tuning for the `discovery-requested` queue** — start with `messageCount = 1, maxReplicas = 4` (the existing Indexer pattern) and tune from observed metrics.
- **Optimistic-concurrency mechanism for `PATCH /api/entities/{id}`** — defer to the platform's established REST convention (ETag header). Implementation will follow the same pattern used by the Spec 006 registry edit endpoints.

These are documented here so future agents do not re-research them.

---

**Phase 0 status**: ✅ Complete. All decisions recorded with rationale and rejected alternatives. Proceeding to Phase 1.
