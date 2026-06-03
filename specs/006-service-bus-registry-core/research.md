# Phase 0 Research — Service Bus Registry Core

**Plan**: [plan.md](./plan.md) · **Spec**: [spec.md](./spec.md) · **Date**: 2026-06-02

This document records the numbered decisions that resolve every NEEDS-CLARIFICATION from the Technical Context plus the best-practices research needed to write `data-model.md`, `contracts/`, and the implementation tasks. Each entry has the shape **Decision / Rationale / Alternatives considered / Sources**. MCP sources cited inline are Microsoft Learn for Azure decisions, context7 for library decisions, and shadcn/ui MCP for component decisions (no new shadcn primitives are introduced — every UI choice composes existing project-owned primitives, so the shadcn/ui MCP consultation is only a verification pass).

---

## 1. Backend request validation library

**Decision**: **FluentValidation 11.10.x** (pin in research §13 lockfile note). Used at the API boundary for HTTP request payloads (create / update DTOs, search query DTOs, audit query DTOs). Registered via `services.AddValidatorsFromAssemblyContaining<RegistryEntityValidator>()`. Validators are invoked **manually** from the endpoint handler (not via the `AddFluentValidationAutoValidation` ASP.NET filter, because Minimal APIs do not participate in MVC filter pipelines and the explicit-call pattern is the documented Minimal-API integration shape).

**Rationale**:
- The spec-006 source artifact explicitly calls for FluentValidation.
- Cleaner than DataAnnotations for the structured rules spec-006 needs: conditional validation on parent existence (FR-008), tag key/value shape (FR-002), Azure resource ID format (Edge Case "Invalid Azure resource IDs"), and duplicate-name-within-parent-scope (FR-014).
- Pairs naturally with the spec-004 `ValidationEngine` (which validates the *canonical* domain at write time) as a complementary boundary-validation layer — different concerns, no overlap.
- The frontend Zod schema is the source of truth for shape; the FluentValidation backend rules mirror the same shape and are verified by a contract test (`SharedSchemaContractTests`) that compares the two against `contracts/registry-entity.schema.json`.

**Alternatives considered**:
- **DataAnnotations**: too primitive for the conditional cross-field rules (e.g., "rule entity requires parent subscription id"); would force a lot of `IValidatableObject` glue.
- **Reuse spec-004's `ValidationEngine`**: wrong layer — that engine validates the canonical domain model after persistence shape mapping, against semantic rules (e.g., `OwnershipPresenceRule`). Spec-006 needs *input* validation before mapping, against HTTP-shape rules. Mixing the two would bleed canonical-domain concerns into the registry API.
- **MiniValidation**: too thin; no rule-grouping or conditional-rule support.

**Sources**: context7 for FluentValidation 11.10 ASP.NET integration patterns (`/jeremySkinner/fluentvalidation`); Minimal-API integration shape documented in their `aspnetcore.html#minimal-apis` reference.

---

## 2. Should registry entities reuse spec-004's `Resource` canonical type?

**Decision**: **No. Registry entities are a parallel data plane with their own concrete types and Cosmos containers.** Five record types (`RegistryNamespace`, `RegistryQueue`, `RegistryTopic`, `RegistrySubscription`, `RegistryRule`) live under `Features/Registry/_Shared/` and implement a shared `IRegistryEntity` interface. They do **not** inherit from `Domain/Resource`. They persist to **new** containers (`registry-entities` PK `/environment`, `registry-audit` PK `/entityId`) on the **existing** spec-004 Cosmos canonical-store database. The spec-004 containers (`resources`, `change-events`) are untouched.

**Rationale**:
- Field-set divergence is structural, not cosmetic (see Complexity Tracking entry #1 in `plan.md`): 2-value `status` vs 5-value `Lifecycle`; free-form `owner` string vs `OwnershipRecord`; key/value `tags` vs `TagReference` to first-class `TagResource`; hard delete vs soft-delete; `Rule` as first-class entity vs embedded `Subscription.Rule`.
- Reusing the spec-004 `Resource` base would force every registry write through `RequiredFieldsRule` (mandating `DisplayName`, `Lifecycle`, `Version`, `Audit`) and `OwnershipPresenceRule` (mandating `OwnershipRecord` for operational types) — breaking spec-006 FR-003's minimum-required-field contract (`id`, `entityType`, `name`, `environment`, `status` only).
- Reusing the spec-004 *containers* would conflict on the discriminator: spec-004 stores `Queue`/`Topic`/`Subscription` records with the full canonical shape, and the registry would need to write a divergent shape under the same `resourceType` discriminator — corrupting the spec-004 dataset.
- Reusing the spec-004 *substrate* (CosmosClientFactory, AzureCredentialFactory, the ETag concurrency pattern, the change-event-on-write pattern) IS valuable and IS done — these are infrastructure concerns, not domain concerns.

**Alternatives considered**:
- **Single unified model satisfying both**: rejected because it bloats the registry API DTO with optional spec-004 fields users never fill (Slow time-to-value, fails SC-001) OR forces a translation layer at the persistence boundary that the team has to maintain forever.
- **Project registry shape into spec-004 shape at write time**: rejected because it permanently couples spec-006's UX to spec-004's governance vocabulary; the registry can never deviate from `Lifecycle` without breaking the projection.
- **Use spec-004 containers with a `source` discriminator (`canonical` vs `registry-slice`)**: rejected because the discriminator pollutes spec-004 queries and the dataset (operators reading the canonical resource list would see two shapes intermixed).

**Vocabulary alignment**: `data-model.md §Vocabulary Alignment` maps every registry term to its spec-004 counterpart so a future "registry-domain unification" spec can reconcile them without reverse-engineering.

**Sources**: spec-004 `data-model.md` (the cited rules); spec-004 `contracts/canonical-resource.schema.json`; spec-006 `spec.md` FR-002, FR-003, FR-013, FR-013a.

---

## 3. Search indexing pipeline architecture

**Decision**: **Cosmos DB change feed → Azure Functions (containerized, on the Container Apps Environment) → Azure AI Search upsert/delete.** A single Function (`RegistryEntityIndexer`) with a Cosmos change-feed trigger on the `registry-entities` container. A second container (`registry-entities-leases`, PK `/id`) holds the change-feed lease state and IS provisioned by IaC (Microsoft Learn explicitly notes the trigger does NOT auto-create the lease container under managed-identity auth because container creation is a management operation, not data-plane). The Function projects each changed document into an AI Search document shape (see `contracts/search-index.json`) and calls `SearchClient.MergeOrUploadDocumentsAsync` (for upserts) or `SearchClient.DeleteDocumentsAsync` (when the change feed delivers a `_isDeleted` marker — set by the API on hard delete via a tombstone-then-delete pattern, see §10 below). Failures retry inline up to N times (per the Functions worker config), then a poison-handler logs an Error with `entityId`, `changeFeedOffset`, `failureCategory`, and a deterministic `correlationId` that ties back to the originating API write trace.

**Rationale**:
- The spec-006 source artifact says "indexing must be resilient and retryable" and constitution §V mandates "explicit failure visibility (no silent retries that mask systemic issues)" — the Cosmos change-feed processor's at-least-once delivery + lease checkpointing + Functions retry/poison handling satisfies all three.
- Cosmos change feed is the leanest event source: no Service Bus topic, no separate publish-from-API path, no schema-evolution-across-systems concern. One source of truth (the registry container) feeds the index automatically.
- The Functions-for-CAE hosting model is the constitution-mandated "containerized Azure Functions on the Container Apps Environment using the newest native hosting" — and it's exactly what spec-005 stood up the CAE for.
- Cosmos change-feed → Functions is documented as the recommended pattern for serverless event-based architectures on Cosmos: `learn.microsoft.com/azure/cosmos-db/change-feed-functions`.

**Alternatives considered**:
- **Service Bus topic published from API → Functions subscription → AI Search**: rejected because it adds an extra publish step on the API hot path (every CRUD must publish a message before returning 200, slowing CRUD p95 and risking dual-write inconsistency) — and because spec-005 stood up the Service Bus namespace for *runtime messaging-domain integration* (a future spec), not for the registry's internal pipeline.
- **In-process background hosted service in the API**: rejected because it scales-with-API not scale-with-indexing-work, and a slow indexer would back-pressure HTTP request handling on the API replicas. Also conflicts with constitution §Event-driven processing.
- **Cosmos change feed → Container Apps Job**: rejected because Jobs are for *scheduled or one-shot* workloads; continuous change-feed consumption needs a long-running worker, and Container Apps Jobs don't have a "run continuously and process events as they arrive" mode that beats the Functions trigger.
- **Direct synchronous index update from API**: rejected because it makes the API hard-fail when AI Search is unavailable (violates SC-011: "Browse, detail, and CRUD experiences continue to function correctly when the search service is temporarily unavailable").

**Sources**: Microsoft Learn `learn.microsoft.com/azure/cosmos-db/change-feed-functions` (change feed + Functions pattern); `learn.microsoft.com/azure/azure-functions/functions-bindings-cosmosdb-v2-trigger` (lease container requirement under identity auth); the spec-006 source artifact (resilient retryable indexing); the constitution (§Hosting, §Async-First Thinking).

---

## 4. Azure Functions hosting model on Container Apps

**Decision**: **Native Azure Functions on Azure Container Apps ("Functions v2") — single `Microsoft.App/containerApps` resource with `kind: functionapp`.** This is the newest native hosting model announced 2024 and recommended by Microsoft Learn for all new workloads. Provisioned via `azurerm_container_app` (azurerm v4 supports the `kind` argument) — falling back to `azapi` if the azurerm version pinned by spec 005 doesn't expose `kind` yet (decision deferred to implementation if needed, with the existing pinned provider version verified during `tofu plan`).

**Rationale**:
- The legacy v1 model uses a proxy `Microsoft.Web/sites` Function App plus a hidden container app — two resources, indirect log access, no native Container Apps features (revisions, custom domains, scaling polling intervals, EasyAuth, certificates).
- The v2 model gives a single resource that has full access to Container Apps native features: revisions, ingress, health probes, secret references, KEDA scaling, identity bindings, diagnostic settings via the existing `iac/modules/diagnostic-settings` wrapper.
- Microsoft Learn explicitly recommends v2 for new workloads and provides a migration guide for v1 → v2.
- Aligns with the constitution's "Ensure that the newest, native Azure Functions for Container Apps hosting is used" requirement.

**Alternatives considered**:
- **v1 (Microsoft.Web Function App + hidden CAE container app)**: rejected by Microsoft Learn's own recommendation and by the constitution's "newest native" requirement.
- **Functions on Elastic Premium or Dedicated plan (not on CAE at all)**: rejected because the constitution mandates CAE hosting for event-driven processing; the existing CAE has the VNet, the diagnostics convention, and the workload UAMI patterns already wired.

**Sources**: Microsoft Learn `learn.microsoft.com/azure/container-apps/functions-overview`, `learn.microsoft.com/azure/container-apps/migrate-functions`, `learn.microsoft.com/azure/azure-functions/functions-container-apps-hosting`.

---

## 5. Azure AI Search index provisioning approach

**Decision**: **Provision the search index via the `azapi` provider** (`azapi_resource` with `type = "Microsoft.Search/searchServices/indexes@2024-07-01"`). The index definition lives in `contracts/search-index.json` (single source of truth) and is read into the IaC module via `jsondecode(file("..."))`.

**Rationale**:
- The `azurerm` provider (v4.x as pinned in spec 005) **does not** expose an index resource — only the service. AI Search index definitions are a data-plane management API not surfaced by azurerm.
- Azure Verified Modules do not cover index definitions either (the AI Search AVM only covers the service).
- `azapi` is already an Azure-supported provider and is acceptable per spec-005's "hand-authored where AVM does not cover" pattern — the `ai-search-index` module is hand-authored, documented in its README, and version-pinned (`hashicorp/azurerm` for the service is already there; we add `Azure/azapi ~> 2.0` to the env composition's `versions.tf`).
- Defining the index as code in `contracts/search-index.json` keeps the contract authoritative — the IaC, the indexer's mapping code (`SearchDocumentMapper`), and the OpenAPI schema all reference the same field list.

**Alternatives considered**:
- **Create the index at API startup via `SearchIndexClient.CreateOrUpdateIndexAsync`**: rejected because it requires the API to have `Search Service Contributor` role (management plane) rather than the least-privilege `Search Index Data Contributor` (data plane) it needs at runtime; moves a structural decision into runtime code where it's harder to review.
- **Create the index in the indexer Functions container at startup**: same objection — elevates the indexer's required RBAC unnecessarily.
- **Manual portal action**: rejected by FR-049 (no manual portal actions).

**Sources**: Microsoft Learn `learn.microsoft.com/azure/search/search-howto-reindex` (REST API surface for index definition); the AVM index — verified absent on the `Azure/avm-res-search-searchservice/azurerm` README; `registry.terraform.io/providers/Azure/azapi/latest/docs` (azapi_resource).

---

## 6. Frontend data layer — TanStack Query vs RSC-only fetching

**Decision**: **Hybrid. RSC + `fetch` on the server for read-only routes (explorer page, detail page); TanStack Query 5.x on the client for interactive surfaces (search box with debounced typeahead, create/edit forms with conflict handling, audit-panel client-side refresh).**

**Rationale**:
- RSC + `fetch` is the simpler default for read-heavy pages and is what Next.js 16 App Router recommends; using TanStack Query for everything would forgo SSR benefits and bloat the client bundle.
- TanStack Query earns its place on three specific interactions that benefit from cache, mutation tracking, and optimistic-conflict UX:
  1. **Search**: debounced typeahead with stable results stitching, prefetch on hover, server-state-as-cache.
  2. **Create/Edit forms**: mutation rollback on conflict (FR-020 refresh-or-overwrite modal), automatic retry on transient failure, integration with React Hook Form's submit lifecycle.
  3. **Audit panel**: lightweight client refresh after a mutation completes so the operator sees their own event immediately.
- Adds **one** package (`@tanstack/react-query ^5`); no additional sub-packages (devtools is dev-only and not used in CI/prod).
- Tech-stack reference (`speckit-artifacts/tech-stack.md` §2) is updated as a follow-up to add TanStack Query to the approved stack — this is a material addition per the constitution's open-stack rule.

**Alternatives considered**:
- **RSC + `fetch` only, no client cache**: rejected for the three interactive surfaces above — the conflict-modal flow specifically benefits from Query's `useMutation` + onError pattern; rebuilding that ad hoc would re-invent the wheel.
- **SWR**: rejected — TanStack Query's mutation lifecycle + retry primitives are richer and the team is already familiar with the API surface via `@tanstack/react-table`.
- **Zustand / Jotai for client state**: rejected — those are app-state libraries, not server-state libraries; the registry's state IS server state.

**Tech-stack follow-up**: the `tech-stack.md` §2 row "Forms: React Hook Form" gets a new sibling row "Server state / data fetching: TanStack Query 5.x (used for interactive surfaces only — RSC + fetch remains the default for read-only routes)". This follow-up is logged in the §Tech-Stack Updates table in `quickstart.md`.

**Sources**: context7 `/tanstack/query` v5 docs; Next.js 16 App Router data-fetching guidance (Next.js DevTools MCP).

---

## 7. AI Search authentication — admin key vs AAD vs managed identity

**Decision**: **Managed identity end-to-end.** The workload UAMI (already provisioned in spec 005) gets `Search Index Data Contributor` role on the AI Search service (allowlisted in spec-005 FR-033). The indexer Functions container uses the same UAMI and grants for ingestion. The API uses the same UAMI for query (`Search Index Data Reader` is sufficient for queries, but `Data Contributor` covers it without an extra grant — research recommends granting `Search Index Data Reader` to the API and `Search Index Data Contributor` to the indexer, finalized in spec-006 data-model §RBAC).

**Rationale**:
- Constitution: managed identity preferred over secrets.
- The AI Search service was created with `local_authentication_enabled = false` (or will be — verify in spec-005 dev composition; if currently `true`, this slice flips it to `false` as the spec-005 forward-looking RBAC was designed to). With local auth disabled, the admin key path is closed by design.
- The spec-005 RBAC-Admin condition allowlist already permits `Search Index Data Contributor` per the spec-005 FR-033 enumeration — no new role GUID needs to be added to the pipeline-MI condition (per [[project_spec005_bootstrap_gate]] memory: bootstrap gate cleared 2026-05-26).

**Alternatives considered**:
- **Admin key in Key Vault**: rejected (forbidden by constitution Principle IV's "managed identity preferred"); also closes off the spec-005 design intent.
- **Query API key**: deprecated direction; AAD-only is the modern recommendation.

**Sources**: Microsoft Learn `learn.microsoft.com/azure/search/search-security-rbac`, `learn.microsoft.com/azure/search/keyless-connections`.

---

## 8. Concurrency conflict UX (FR-020) — wire-level shape

**Decision**: On a stale-version PUT the API returns **`409 Conflict` with a body conforming to `contracts/conflict-response.schema.json`**. Body shape:

```jsonc
{
  "type": "https://busterminal.dev/probs/concurrency-conflict",
  "title": "Concurrency conflict",
  "status": 409,
  "code": "ConcurrencyConflict",
  "detail": "The entity was modified by another user since you loaded it.",
  "instance": "/api/registry/queues/{id}",
  "entityId": "...",
  "currentVersion": "\"00000000-0000-0000-1234-567890abcdef\"",   // current server ETag
  "submittedVersion": "\"00000000-0000-0000-0000-000000000000\"", // ETag the client sent in If-Match
  "currentEntity": { /* full current server-side entity per registry-entity.schema.json */ },
  "changedFields": [
    { "field": "description", "currentValue": "...", "submittedValue": "..." },
    { "field": "tags",        "currentValue": [...], "submittedValue": [...] }
  ]
}
```

Built on RFC 7807 + the `currentEntity` and `changedFields` extension members. The frontend `registry-conflict-modal.tsx` renders the field diff and offers two actions: **Discard my changes and refresh** (closes the modal, drops the form state, navigates to a fresh GET) and **Force overwrite** (re-submits the PUT with `If-Match: <currentVersion>` AND a body extension `_overwriteAcknowledged: true` so the audit event records the explicit user choice).

**Rationale**:
- FR-020 mandates: recoverable conflict response carrying current server state + identification of changed fields; two-action UI; force-overwrite recorded as explicit choice in audit.
- RFC 7807 is the project's standard problem shape (already used elsewhere) — keeps error contracts consistent.
- ETags are opaque, so `currentVersion` is a string. The client doesn't compute the diff — the server does, because the server has both shapes and the client only has its submission.
- `_overwriteAcknowledged: true` is the audit signal; the audit event for that update sets `wasForceOverwrite: true` (see `contracts/audit-event.schema.json`).

**Alternatives considered**:
- **`412 Precondition Failed`** (the literal Cosmos response code): rejected because 412 is a generic "your If-Match didn't match" response. 409 + a typed problem document is more communicative and gives us room to attach `currentEntity` and `changedFields`.
- **Don't return current entity, force the client to re-GET**: rejected because it forces an extra round-trip and a race window where another conflict can land between the 409 and the re-GET, repeatedly.
- **Server merge**: rejected (silent merge violates the spec).

**Sources**: RFC 7807; spec-006 spec.md FR-020; Cosmos client `412` mapping pattern from `api/BusTerminal.Api/Infrastructure/Persistence/ConcurrencyExceptionMapper.cs` (spec-004 precedent reused).

---

## 9. Tag schema — case sensitivity and storage shape

**Decision**: Persisted as `tags: [{ "key": "Owner", "value": "PaymentsTeam" }, ...]` (an array of explicit objects, NOT a JSON object) for stable JSON serialization and to support multi-value cases (same key, different values). Each entity carries a sibling **`tagKeysLower`** computed field that is a lowercase-normalized de-duplicated array of every tag's key, populated by the persistence layer on every write. The AI Search index has a `tags` field (full collection projection) AND a `tagKeysLower` field for fast case-insensitive key filtering. Tag *values* are matched and displayed case-preserved (spec-006 clarification).

**Rationale**:
- Spec-006 clarification: keys are matched case-insensitively, displayed case-preserved (first-write wins for display normalization); values are matched and displayed case-preserved.
- An array-of-objects shape preserves multi-value semantics (two tags `Owner=Alice` and `Owner=Bob` on one entity are both visible — important for cross-team-owned assets).
- The `tagKeysLower` sibling field is the projection AI Search can use for `$filter=tagKeysLower/any(k: k eq 'owner')` without burning per-query compute on lowercasing.
- The first-write wins display normalization is enforced by the API on PUT: if the submitted tags contain `OWNER=...` and there's already a tag with key `Owner` on the entity, the API writes `Owner` (preserving first-write casing) — a `tag-display-normalization` rule in `RegistryEntityValidationRules` handles this.

**Alternatives considered**:
- **`tags: { "Owner": "Alice" }` (JSON object)**: rejected because it disallows multi-value keys and complicates filtering on the search index.
- **Tags as first-class resources (spec-004 `TagResource`)**: rejected for spec-006 simplicity (operators want to type a key, not pick from a managed taxonomy).

**Sources**: spec-006 spec.md FR-002, FR-023; spec-004 `Domain/Resources/TagResource.cs` (the divergence point recorded in §Vocabulary Alignment).

---

## 10. Hard-delete + index propagation — the tombstone trick

**Decision**: Hard delete is implemented as a **two-phase delete-then-tombstone**:

1. API receives `DELETE /api/registry/{id}`.
2. API checks for children via `ChildCountChecker` — if any exist, returns `409 Conflict` with body identifying counts/types (FR-009).
3. If no children, the API writes a tombstone marker document to `registry-entities` with `_isTombstone: true, _tombstoneFor: <id>, ttl: 60` (TTL 60s — Cosmos deletes the marker automatically after 60s) AND then deletes the original document.
4. The change feed delivers BOTH operations to the indexer: the tombstone (which the indexer maps to a `DeleteDocumentsAsync` call against the AI Search index) and the deletion (which the change feed surfaces as a "delete" event — handled identically).

**Rationale**:
- Cosmos change feed in `latest-version` mode (the only mode the Functions trigger supports) **does not** deliver "delete" events natively (it only delivers inserts and updates). Without a tombstone, hard-deleted documents would be removed from Cosmos and never propagate to AI Search — which would stay stale forever.
- The tombstone approach is the documented Cosmos pattern for change-feed-driven deletes (`learn.microsoft.com/azure/cosmos-db/change-feed-design-patterns`).
- TTL of 60s is long enough to guarantee the indexer processes the tombstone under normal conditions (SC-005 mandates index lag p95 < 5s, so 60s is 12x safety margin) and short enough that operators querying the live store don't see lingering tombstones for long.
- A separate `_isTombstone` field (NOT `isDeleted`) is used to avoid confusion with the spec-004 `isDeleted` soft-delete predicate — registry entities have no `isDeleted` field at all (hard delete, per FR-013).

**Alternatives considered**:
- **Switch Cosmos to "all-versions and deletes" change-feed mode** (which DOES surface deletes): rejected because the Functions trigger only supports `latest-version` mode (Microsoft Learn note).
- **Synchronous API → AI Search delete in the same request**: rejected because it makes the API depend on AI Search availability for deletes (violates SC-011).
- **Reconciliation job that compares Cosmos vs AI Search and deletes orphans**: rejected because it adds a second pipeline, runs on a schedule (so delete lag is unbounded), and adds operational surface.

**Sources**: Microsoft Learn `learn.microsoft.com/azure/cosmos-db/change-feed-design-patterns`, `learn.microsoft.com/azure/cosmos-db/nosql/time-to-live` (item-level TTL).

---

## 11. Parent existence validation (FR-008) and child-counting (FR-009)

**Decision**:
- **Parent existence on create**: the API handler performs a point-read against `registry-entities` partitioned by `/environment` (same environment as the child) and matched on `id = <parentId>` and `entityType = <expectedParentType>`. A miss returns `400 Bad Request` with field-level validation error tied to `parentId`. Cost: 1 RU per point read.
- **Child-count on delete**: the API handler issues a partition-scoped `SELECT VALUE COUNT(1) FROM c WHERE c.parentId = '<id>'` query against the same environment partition. Cost: 1-5 RU per query under spec-006 scale (tens of thousands of entities per env, fan-out under 1000 children per parent). If count > 0, returns `409 Conflict` with body identifying the count (and entity-type breakdown if scale permits — research §11 recommends including the breakdown).

**Rationale**:
- Both operations are partition-scoped (parent and child live in the same `/environment` partition by construction — a child cannot live in a different env from its parent per spec-006's environment semantics), so cost is bounded.
- An alternative "maintain a `childCount` field on every parent" denormalization adds write amplification on every child create/delete + concurrency concerns updating the count atomically with the child — not worth it at this scale.

**Alternatives considered**:
- **Denormalized child-count field on parents**: rejected (see above).
- **Materialized "children-by-parent" view in AI Search**: rejected — AI Search is the wrong tool (indexer lag would make delete-validation incorrect).

**Sources**: Cosmos NoSQL aggregation queries `learn.microsoft.com/azure/cosmos-db/nosql/query/aggregate-functions`.

---

## 12. Browse hierarchy data source — Cosmos or AI Search?

**Decision**: **Cosmos.** The explorer tree (FR-027) and detail page (FR-028) are served from `registry-entities`, NOT from the AI Search index. The search box (FR-022) IS served from AI Search. FR-026 explicitly mandates this: "Browse and detail experiences MUST be served from the persistent store, NOT from the search index."

**Rationale**:
- Latency budget for detail page p95 < 500ms (FR-044) is comfortably hit by a Cosmos point-read (5-15 RU, 5-50ms).
- Latency budget for explorer tree first load is met by env-partitioned queries `SELECT c.id, c.name, c.entityType, c.parentId FROM c WHERE c.environment = 'dev'` returning a flat list the client assembles into a tree (the API caps at 10k entities per env on first load with pagination beyond — see §13 below).
- FR-026 is the *correctness* requirement (search-index lag must not cause browse to show stale state) — this decision is forced.

**Alternatives considered**: none (FR-026 mandates).

**Sources**: spec-006 spec.md FR-022, FR-026, FR-027, FR-028, FR-043, FR-044.

---

## 13. Pagination and sort for browse and search

**Decision**:
- **Explorer browse**: continuation-token pagination via Cosmos `RequestOptions.MaxItemCount = 500` + the SDK's `FeedIterator`. The frontend lazy-loads children on tree-node expand.
- **Search results**: skip/top pagination via AI Search (`$skip`, `$top`), stable sort on `(score desc, updatedAtUtc desc, id asc)` for deterministic ordering.
- **Audit list**: page size 50 by default, configurable up to 200 via `?limit=N` query param, sorted `timestamp desc`.

**Rationale**:
- Cosmos continuation-token pagination is the canonical "very large registries" pattern (Edge Case: tens of thousands of entities).
- AI Search supports `$skip` up to 100k results — well within the spec-006 scale of tens of thousands per env.
- Stable sort tiebreakers (`id asc`) prevent result-flicker on repeated queries.

**Alternatives considered**:
- **Cursor-based search pagination**: AI Search doesn't expose stable cursors; skip/top is the documented approach.

**Sources**: Microsoft Learn `learn.microsoft.com/azure/cosmos-db/nosql/query/pagination`, `learn.microsoft.com/azure/search/search-pagination-page-layout`.

---

## 14. Frontend conflict-modal interaction with React Hook Form

**Decision**: The edit form (`namespace-form.tsx`, `queue-form.tsx`, etc.) carries an `_etag` hidden field initialized from the loaded entity. On submit, the form mutation passes `If-Match: <_etag>` to the API. On `409 Conflict` + the typed problem body, the form's TanStack Query `useMutation` `onError` handler opens `registry-conflict-modal.tsx` and passes it `currentEntity` + `changedFields`. The modal's two actions:

- **Discard & refresh**: resets the form via `form.reset(currentEntity)`, closes the modal, surfaces a toast "Refreshed to current state".
- **Force overwrite**: re-submits the same form payload with the *current* ETag AND `_overwriteAcknowledged: true` (added via a form-state escape hatch that does NOT live in the visible form schema). The audit event records `wasForceOverwrite: true`.

**Rationale**:
- Keeps the form state and conflict resolution in one place.
- The `_overwriteAcknowledged` flag is a server-side opt-in — the API rejects PUTs with `_overwriteAcknowledged: true` if there is NO active conflict (so a malicious client can't pre-seed the flag).
- Force overwrite skips re-validation against the new server state — the user has explicitly chosen to override.

**Alternatives considered**:
- **Auto-merge non-conflicting fields**: rejected (silent overwrite is forbidden).
- **Page-reload on conflict instead of modal**: rejected (loses in-progress form data).

**Sources**: TanStack Query 5 mutation lifecycle (context7 `/tanstack/query`); React Hook Form `reset` API (context7 `/react-hook-form/react-hook-form`).

---

## 15. Audit event storage shape

**Decision**: One JSON document per audit event in the `registry-audit` Cosmos container, partitioned by `/entityId`. Documents are append-only (FR-034 — users cannot edit or delete audit events from the application; the API exposes no write endpoint on `/audit`). TTL is NOT set on audit events in this slice (retention policy is a future ops-hardening concern). Shape per `contracts/audit-event.schema.json`:

```jsonc
{
  "id": "<guid>",
  "entityId": "<entity-guid>",
  "entityType": "Queue",
  "environment": "dev",
  "eventType": "Created" | "Updated" | "Deleted" | "StatusChanged",
  "timestamp": "2026-06-02T14:30:00Z",
  "actor": { "principalId": "<entra-oid>", "displayName": "alice@busterminal.dev" },
  "changeSummary": "Created Queue 'orders-incoming' under namespace 'orders-prod'",
  "fieldChanges": [ { "field": "description", "before": "...", "after": "..." } ],
  "wasForceOverwrite": false,
  "correlationId": "<trace-id>"
}
```

**Rationale**:
- Entity-scoped audit retrieval (FR-033) is a partition-scoped query — fast and cheap.
- `wasForceOverwrite` directly satisfies FR-020's "Force overwrite is recorded as explicit user choice".
- `correlationId` is the W3C `traceparent` trace ID — links the audit event back to the originating frontend trace in App Insights (per FR-042 / SC-012).
- `fieldChanges` is computed on the API server by diffing the loaded entity vs the submitted payload — the client never computes the diff (the server has both sides).

**Alternatives considered**:
- **Write audit events to the spec-004 `change-events` container**: rejected for the same vocabulary-divergence reasons as the entity model (the change-event shape differs).
- **Audit events as nested documents inside the entity**: rejected because (a) it grows the entity document unboundedly and (b) FR-034 append-only-from-user-perspective is harder to enforce when audit events live inside a mutable entity.

**Sources**: spec-006 spec.md FR-032, FR-033, FR-034.

---

## 16. Cosmos RU provisioning and partition key

**Decision**:
- **`registry-entities`** — partition key `/environment`; autoscale max RU/s 4000 (min 400). Aligns with spec-006 scale assumption (tens of thousands of entities per env, hundreds of concurrent operators).
- **`registry-audit`** — partition key `/entityId`; autoscale max RU/s 1000 (min 100). Audit writes are 1-2 RU each, entity-scoped reads are 1-5 RU each.
- **`registry-entities-leases`** — partition key `/id` (required by the Cosmos change-feed trigger); autoscale max RU/s 400 (min 100). Lease container has very low RU needs.

**Rationale**:
- The spec-006 source artifact says PK = `/environment` for `registry-entities`. This matches spec-006's single-tenant + environment-level query isolation rationale.
- Hot-partition risk: production envs will eventually dominate. Hierarchical PK (`/environment` + `/entityType`) is the future-proofing answer; this slice ships flat `/environment` per the source artifact and reserves HPK for a future scaling spec.
- Lease container PK is forced to `/id` by Cosmos change-feed trigger requirements (Microsoft Learn).
- Autoscale bands are conservative and can be tuned in a future ops spec.

**Alternatives considered**:
- **Hierarchical PK `/environment, /entityType`**: deferred to a future spec (this slice ships the documented contract from the source artifact and avoids forking the future-proofing decision into spec-006).
- **Provisioned (non-autoscale) RU**: rejected — cost optimization on a feature spec is premature.

**Sources**: Microsoft Learn `learn.microsoft.com/azure/cosmos-db/nosql/how-to-provision-autoscale-throughput`, `learn.microsoft.com/azure/cosmos-db/hierarchical-partition-keys`; spec-006 source artifact §Partitioning Strategy.

---

## 17. Indexer Cosmos trigger configuration

**Decision**: The indexer Function declares the trigger with:

```csharp
[Function("RegistryEntityIndexer")]
public async Task Run(
    [CosmosDBTrigger(
        databaseName: "%COSMOS_DATABASE_NAME%",
        containerName: "registry-entities",
        Connection = "Cosmos",
        LeaseContainerName = "registry-entities-leases",
        CreateLeaseContainerIfNotExists = false,
        MaxItemsPerInvocation = 100,
        StartFromBeginning = true)]
    IReadOnlyList<RegistryEntityChangeFeedItem> changes,
    FunctionContext context)
```

Connection settings (no secrets):
- `Cosmos__accountEndpoint` = `https://<dev-cosmos-name>.documents.azure.com:443/` (from spec-005 outputs)
- `Cosmos__credential` = `managedidentity`
- `Cosmos__clientId` = workload UAMI client ID (from spec-005 outputs)

The lease container is **explicitly NOT auto-created** — IaC provisions it (Microsoft Learn note: under managed-identity auth, container creation is a management operation and the trigger cannot perform it).

**Rationale**:
- `MaxItemsPerInvocation = 100` keeps per-invocation latency bounded under SC-005's 5s p95 budget; the trigger naturally batches in small bursts.
- `StartFromBeginning = true` on first deployment ensures the index is fully populated from existing data; subsequent deployments resume from lease checkpoint regardless of this flag.
- Configuration uses `%ENV_VAR%` substitution per Functions worker conventions; no inline literals.

**Alternatives considered**:
- **`CreateLeaseContainerIfNotExists = true` with admin-grade RBAC**: rejected because it requires `Cosmos DB Account Contributor` on the trigger identity — a control-plane role that the workload UAMI does not have and should not have (least privilege).

**Sources**: Microsoft Learn `learn.microsoft.com/azure/azure-functions/functions-bindings-cosmosdb-v2-trigger#attributes`, `#connections`.

---

## 18. Frontend observability — propagation of W3C Trace Context

**Decision**: The existing `web/lib/http/` typed fetch client (which already adds `traceparent`/`tracestate` per spec-001 / tech-stack §4) is reused unchanged. The new `web/lib/registry/api.ts` module composes this client. TanStack Query's `queryFn` and `mutationFn` invoke the typed client; no headers are added at the Query layer. Trace context is verifiable in App Insights by selecting any registry-API call from a UI trace and confirming the linked backend span (SC-012).

**Rationale**:
- FR-042 mandates W3C Trace Context propagation on every UI-originated HTTP request to a registry API.
- Reusing the existing client is the right answer — the propagation logic is already audited and tested (spec-001 + spec-003 acceptance criteria).

**Sources**: tech-stack.md §4; spec-001 frontend observability decisions.

---

## 19. IaC module strategy — what's new, what's extended

**Decision**:
- **NEW**: `iac/modules/cosmos-registry-store/` — creates the three new containers (`registry-entities`, `registry-audit`, `registry-entities-leases`) on the existing spec-004 `canonical` database. Hand-authored (no AVM coverage for SQL containers inside an existing database; matches the spec-004 `cosmos-canonical-store` pattern).
- **NEW**: `iac/modules/ai-search-index/` — creates the `registry-entities-v1` index via `azapi_resource`. Reads `contracts/search-index.json`.
- **NEW**: `iac/modules/functions-container-app/` — wraps `azurerm_container_app` with `kind = "functionapp"`, the v2-native Functions-for-CAE model from §4. Inputs: CAE id, workload UAMI id+client_id, ACR login server, image tag, Cosmos endpoint, AI Search endpoint, App Insights connection-string KV secret URI. Mirrors the existing `iac/modules/container-app` pattern.
- **EXTENDED**: `iac/environments/dev/main.tf` — compose the three new modules; bind workload UAMI to `Search Index Data Contributor` on AI Search (existing spec-005 allowlist already permits this GUID); add diagnostic settings on the new container app via the existing `iac/modules/diagnostic-settings` wrapper.
- **NOT TOUCHED**: `iac/modules/cosmos-canonical-store/`, `iac/modules/ai-search/`, `iac/modules/networking/`, `iac/modules/keyvault/`, and all other spec-005 modules — they remain authoritative for their scope.

**Rationale**:
- Surface area minimized; no destructive modifications to existing infrastructure.
- New modules follow existing naming and structural conventions, keeping CI policy gates (BT-IAC-001..007) green.
- All new resources are taggable (BT-IAC tag gate), forward `allLogs` to LAW (BT-IAC-003 diagnostics gate), use managed identity for service-to-service (BT-IAC-005 inline-credentials gate), and have pinned module versions (BT-IAC-006 lockfile gate).

**Alternatives considered**:
- **Roll Functions-container-app into the existing `container-app` module**: rejected — the `kind=functionapp` and Functions-specific environment-variable conventions are different enough that one shared module would have many conditional code paths.

**Sources**: spec-005 `contracts/module-contracts.md`, `contracts/policy-rules.md`.

---

## 20. Test strategy — coverage by layer

**Decision**:

| Layer | Tool | Scope |
|---|---|---|
| Backend unit | xUnit | `RegistryDtoMapping`, FluentValidation rules, `ConcurrencyConflictMapper`, `ChildCountChecker`, `SearchDocumentMapper`, conflict-response shaping, tag display-normalization rule |
| Backend integration | xUnit + real dev Cosmos via `RegistryFixture` (test entities prefixed with a per-test GUID to enable parallel runs) | `CosmosRegistryEntityStore` end-to-end: create/read/update/delete, ETag concurrency (412 → ConflictResponse), child-count validation, audit-event append-after-write |
| Indexer integration | xUnit + Functions worker test harness + real dev Cosmos + real dev AI Search (test index suffix per-test) | Cosmos-write → change-feed → AI Search upsert within 5s p95 (SC-005), poison-handler emits permanent-failure log |
| API contract | xUnit | OpenAPI runtime document conforms to `contracts/registry-api.yaml`; conflict-response shape conforms to `contracts/conflict-response.schema.json`; audit-event shape conforms to `contracts/audit-event.schema.json` |
| Shared-schema contract | xUnit + node (via `pnpm run test:contracts`) | Zod schemas in `web/lib/registry/schemas.ts` produce JSON-schema output matching `contracts/registry-entity.schema.json`; FluentValidation rules in `RegistryEntityValidationRules` mirror Zod constraints |
| Frontend unit | Vitest + RTL | All new components in `web/components/registry/`; `lib/registry/api.ts`; `lib/registry/conflict.ts`; tag-utils |
| Frontend E2E | Playwright | Registry create → browse → search → edit → conflict → force-overwrite → delete-with-children-blocked → delete-leaf flows on each entity type |
| Frontend a11y | axe-playwright | Each of the 6 new App Router segments (explorer, search, new, detail, edit) passes axe with zero violations on dark and light themes |
| IaC | tofu fmt/validate/plan + checkov + tfsec + BT-IAC-001..007 | New modules and env composition pass all existing gates without allowlist additions |

**Rationale**: Mirrors the spec-005 testing strategy (which the team is already running) and satisfies the constitution's required testing layers (unit, integration, contract, UI component, E2E). The shared-schema contract test is the key invariant that keeps the Zod + FluentValidation + OpenAPI surfaces in sync without manual review.

**Alternatives considered**:
- **Cosmos emulator for integration tests**: rejected — dev Cosmos is reachable (spec-005 public-access dev posture) and produces higher-fidelity results than the emulator's known limitations.
- **Contract test against runtime OpenAPI but not against `registry-api.yaml`**: rejected — the YAML is the human-authored contract; we test both directions (YAML matches runtime, runtime matches YAML).

**Sources**: spec-005 plan.md §Testing; constitution §Testing Standards.

---

## 21. Tech-stack reference updates (follow-up after this slice ships)

<a id="tech-stack-updates"></a>

> **Mirrored**: this section is duplicated for operator handoff at [`quickstart.md` §11](./quickstart.md#11-tech-stack-updates-post-merge-follow-up). Any change here MUST be reflected there (and vice-versa). The shared-schema contract test (T060) does NOT cover this drift — review is manual.

The following additions to `speckit-artifacts/tech-stack.md` are required as a follow-up — they are *new approved technologies* introduced by this slice:

| Section | Addition |
|---|---|
| §2 Frontend | New row: "Server state / data fetching: TanStack Query 5.x — used for interactive surfaces (search, mutations, conflict UX); RSC + fetch remains the default for read-only routes." |
| §1 Backend | New row: "HTTP request validation: FluentValidation 11.10.x — boundary validation on API endpoints; complements the spec-004 `ValidationEngine` (canonical-domain validation)." |
| §5 Data Platform | Annotation on Cosmos DB row: "Cosmos change feed (latest-version mode) is the supported event source for indexing pipelines; lease containers must be IaC-provisioned under managed-identity auth." |
| §6 Hosting & Infrastructure | New row: "Event-driven processing: containerized Azure Functions on Azure Container Apps using the **native (Functions v2) hosting model** — single `Microsoft.App/containerApps` resource with `kind = functionapp`. Legacy v1 (`Microsoft.Web/sites` proxy) is prohibited for new workloads." |

The tech-stack follow-up is logged in `quickstart.md` §Tech-Stack Updates so the operator who merges this slice knows to author the update.

---

## Summary of resolved unknowns

Every NEEDS-CLARIFICATION from `plan.md` Technical Context is resolved above:

- ✅ Validation library → FluentValidation 11.10 (§1)
- ✅ Functions-for-CAE hosting model → v2 native (§4)
- ✅ AI Search index provisioning → azapi (§5)
- ✅ Frontend data layer → RSC + TanStack Query hybrid (§6)
- ✅ AI Search authentication → managed identity (§7)
- ✅ Concurrency conflict wire shape → 409 + RFC-7807 extension (§8)
- ✅ Tag storage shape → array-of-objects + lowercase-key sibling (§9)
- ✅ Delete propagation to index → tombstone-then-delete (§10)
- ✅ Parent / child validation cost → partition-scoped Cosmos queries (§11)
- ✅ Browse vs search data source → Cosmos for browse, AI Search for search (§12)
- ✅ Pagination strategy → continuation tokens (browse), skip/top (search), 50 default (audit) (§13)
- ✅ Conflict modal UX → RHF + TanStack Query mutation onError (§14)
- ✅ Audit storage → entity-scoped Cosmos container (§15)
- ✅ Cosmos RU provisioning → autoscale per container, flat `/environment` PK (§16)
- ✅ Indexer Cosmos trigger config → managed identity + IaC-provisioned lease container (§17)
- ✅ W3C Trace Context propagation → reuse existing `web/lib/http/` (§18)
- ✅ IaC module strategy → 3 new modules + extended dev composition (§19)
- ✅ Test strategy → 9-layer coverage matrix (§20)
- ✅ Tech-stack follow-up → 4 additions to `speckit-artifacts/tech-stack.md` (§21)

Phase 0 complete. Proceed to Phase 1.
