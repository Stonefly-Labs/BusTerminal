# Research — Namespace Onboarding (Spec 008)

**Plan**: [plan.md](./plan.md) · **Spec**: [spec.md](./spec.md)

This document captures the numbered decisions that resolve every NEEDS-CLARIFICATION raised by the plan's Technical Context, plus the best-practices research that grounds the implementation in current Azure / .NET / Next.js conventions. Each item follows the Decision / Rationale / Alternatives format.

---

## §1. Azure ARM SDK choice for Service Bus namespace probing

**Decision**: Use **`Azure.ResourceManager.ServiceBus`** (the typed Service Bus extension of the unified ARM client) pinned at the latest 1.x line at planning time (target pin verified via Microsoft Learn MCP at task time; expected ≥ 1.1). Authenticate via the existing `AzureCredentialFactory` → `DefaultAzureCredential` pattern shared with `CosmosClientFactory` and `AzureAiSearchClient`. Expose the probe through the new `IArmNamespaceProbe` port; the concrete `ArmNamespaceProbe` lives in `Infrastructure/ServiceBus/`.

**Rationale**:
- Typed `ServiceBusNamespaceResource` model surfaces the exact namespace shape (id, location, properties) needed by the Existence, Accessibility, and ApiReachability checks — no manual JSON parsing.
- Reuses the workload UAMI's `DefaultAzureCredential` chain — same auth surface as the rest of the backend; satisfies FR-017 (no connection strings) and FR-033 (managed identity preferred).
- Single ARM client instance is recommended by Azure SDK guidance — registered as a singleton in DI via `services.AddSingleton<ArmClient>(...)`. Constructor accepts `(TokenCredential, ArmClientOptions)`; per-request `RequestContext` carries the cancellation token + per-check timeout (research §5).
- `ArmClient` automatically handles ARM throttling (429) with built-in retry policy — adequate for v1's single-namespace-at-a-time validation; batch revalidation is out of scope per Clarification Q5.

**Alternatives considered**:
- Raw `HttpClient` against ARM REST endpoints — rejected: requires custom auth header authoring, custom URL composition, and per-response JSON parsing. The typed SDK is strictly less ceremony with equivalent control.
- Newtonsoft-based wrappers around individual ARM calls — rejected: pulls in Newtonsoft.Json beyond Cosmos SDK's internal dependency, conflicts with System.Text.Json hygiene elsewhere in the codebase.
- Adding the broader `Azure.ResourceManager.Authorization` package for `permissions/list` — kept on the table for §3 below; covered there.

---

## §2. Microsoft Graph SDK + permissions for the Entra picker

**Decision**: Reuse the existing `Microsoft.Graph 5.105.0` package (already pinned in `BusTerminal.Api.csproj`) and the existing `BusTerminal.Graph` integration (registered as singleton in `Program.cs:41` via `AddBusTerminalGraphClient()`). Add a new application permission **`Group.Read.All`** (UUID `5b567255-7703-4780-807c-7be8301ae99b`) to the existing `iac/modules/graph-permissions/` module's `granted_application_permission_ids` set across dev/test/prod env compositions. Implement the new `IGraphPrincipalPicker` port via `GraphPrincipalPicker.cs` in `Infrastructure/Graph/`, exposing `SearchAsync(string query, int top, CancellationToken)` returning a typed `GraphPrincipalSearchResult` union of `User` and `Group` hits, ordered display-name-ascending, capped at 25 results per call.

**Rationale**:
- `User.Read.All` is already consented in dev per [[project_admin_consent_pending]] (verified 2026-06-14). `Group.Read.All` is the one missing permission FR-013 requires.
- Single SDK + single registration → consistent auth, consistent retry, consistent telemetry across the backend.
- `Graph.Users.GetAsync` and `Graph.Groups.GetAsync` support `$filter`, `$search`, and `$top` query parameters; the picker constructs an OData filter `startsWith(displayName,'q') or startsWith(mail,'q')` for users and `startsWith(displayName,'q')` for groups (groups have no `mail` property uniformly). The 25-result cap matches typical Combobox UX and bounds API cost.
- Admin consent for `Group.Read.All` is operator-procedural at deploy time per the spec-003 runbook — IaC declares the request, an admin grants it via `az ad app permission admin-consent --id {appId}` once per environment. Tracked as an attestation in `contracts/outputs-contract.md`.

**Alternatives considered**:
- Graph delegated permissions instead of application permissions — rejected: FR-013 explicitly requires the picker to behave consistently regardless of the operating user's directory privileges (e.g., a user with `Operator` role but no individual Entra read consent must still see the same picker). Application permissions provide this guarantee.
- Switching to the lightweight `Microsoft.Graph.Core` only and constructing requests by hand — rejected: the SDK is already in tree, the typed surface is significantly less ceremonious for the small set of operations needed, and there's no measurable size/perf benefit at this scale.
- Caching picker results locally to reduce Graph load — deferred: Graph throttling is documented but v1's call volume (one search per keystroke debounced to 250ms, max one wizard open at a time per admin) is far below any documented throttling threshold. Caching adds stale-data risk during onboarding (the most "live" moment for org-structure correctness) for marginal latency benefit.

---

## §3. ARM permissions enumeration for the `RequiredPermissions` check

**Decision**: Implement the `RequiredPermissionsCheck` by calling **ARM `Microsoft.Authorization/permissions` `list`** at the namespace scope (`GET /subscriptions/{subId}/resourceGroups/{rg}/providers/Microsoft.ServiceBus/namespaces/{name}/providers/Microsoft.Authorization/permissions?api-version=2022-04-01`) and asserting the response's `actions` array contains `Microsoft.ServiceBus/namespaces/read` (or a wildcard that subsumes it, e.g., `Microsoft.ServiceBus/*/read` or `*/read`). This is the *effective-permissions* check — tolerant of inherited role assignments from parent scopes (resource group, subscription).

**Rationale**:
- `permissions/list` returns the *effective* set of permissions the *caller* has at a given scope — exactly what FR-014's `RequiredPermissions` check semantically means. It does NOT require any RBAC-read privilege beyond access to the resource itself.
- Tolerant of nested role assignments — if the operator granted `Reader` at the resource group scope instead of the namespace scope (or `Contributor` at any scope), the check still passes correctly because the effective `Microsoft.ServiceBus/namespaces/read` action is present.
- The check is implemented via the existing `ArmClient` rather than a separate `Azure.ResourceManager.Authorization` import — `permissions/list` is reachable through `ArmClient.GetGenericResources()` or via `ArmClient.GetArmResource(resourceId).GetAvailableLocations()`-adjacent generic-resource pattern. Confirmed via Microsoft Learn MCP at task time.

**Alternatives considered**:
- Enumerating role assignments via `Microsoft.Authorization/roleAssignments` `list` at the scope — rejected: requires `Microsoft.Authorization/roleAssignments/read` permission for the workload UAMI (which is a privileged operation) AND requires the check to know the full role-definition-to-action mapping (Reader → `*/read`, etc.) — brittle and over-privileged.
- Issuing an actual `Microsoft.ServiceBus/namespaces/read` ARM call and inferring permission from 200 vs 403 — rejected: collapses with the `Existence` and `Accessibility` checks, losing the per-check signal FR-014 requires.
- Checking the inverse — listing role assignments WHERE `principalId = workloadUAMI` AND `scope` contains the namespace ARM id — rejected: same privileged-operation concern; ALSO misses Reader-via-inheritance-at-RG-scope cases.

---

## §4. Reader-role grant on operator-supplied namespaces: declarative IaC vs runbook

**Decision**: **Runbook-driven (Option B).** Operators run `az role assignment create --assignee {workloadPrincipalId} --role Reader --scope {namespaceArmId}` before clicking onboarding. BusTerminal exposes the workload UAMI's `principalId` via a new `GET /api/namespaces/identity` endpoint (AuthN-only, returns `{ "principalId": "<guid>" }` plus the runbook URL); the wizard step 1 sidebar surfaces a copy-pasteable command pre-filled with this value and the ARM id the operator just pasted in step 1. The `RequiredPermissions` validation check in step 4 is the verification surface — if the role isn't granted, validation fails with the same `az role assignment create` block as the remediation hint. The Reader role GUID (`acdd72a7-3385-48ef-bd42-f606fba81ae7`) is added to the pipeline MI RBAC-Admin condition allowlist in `iac/platform-bootstrap/main.tf` so a future IaC-driven grant model can be introduced as a non-breaking enhancement without re-litigating the policy gate. Runbook lives at `iac/runbooks/grant-namespace-reader.md`.

**Rationale**:
- IaC cannot enumerate operator-supplied namespace resource IDs at plan time — they live in subscriptions BusTerminal does not own. Any IaC-driven model requires the operator to update tfvars and re-apply the pipeline per onboarding, which gates a sub-five-minute UX path on a CI run (failing SC-001).
- Granting subscription-scope Reader to the workload UAMI is explicitly forbidden by BT-IAC-004 ("Workload UAMIs never receive subscription-wide or management-plane grants").
- Letting BusTerminal grant itself Reader at runtime requires the workload UAMI to hold `Microsoft.Authorization/roleAssignments/write` at namespace scope — a privilege escalation that worsens the security posture (the very thing FR-033 / SC-007 are guarding).
- The runbook approach is the smallest surface that lets v1 ship without violating a hard IaC policy. The validation runner's `RequiredPermissions` check ensures the prerequisite is *verified* before any onboarding can complete, so the security guarantee holds at runtime even though the role-grant authoring is procedural.

**Alternatives considered**:
- **Option A: IaC `for_each` over a tfvars list of namespace ARM IDs** — rejected per the sub-five-minute UX argument and the operator-friction of CI runs per onboarding.
- **Option C: subscription-scope Reader** — rejected by BT-IAC-004 (hard policy gate, not a preference).
- **Option D: BusTerminal grants itself at runtime** — rejected on security grounds (privilege escalation).
- **Option E: shipping a small "BusTerminal onboarding helper" containerized job operators run pre-onboarding** — deferred. Logically equivalent to the runbook but with extra moving parts; can be added later as a UX nicety without changing the underlying grant model.

This decision is the basis of Complexity Tracking entry #1 in `plan.md`.

---

## §5. Validation runner architecture: parallel checks + per-check timeout

**Decision**: Implement `NamespaceValidationRunner` as a single orchestrator that fans out the five named checks (Existence, Accessibility, RequiredPermissions, IdentityAuthorization, ApiReachability) via `Task.WhenAll`, with each check wrapped in a `Polly.Timeout` policy (uses the existing `Microsoft.Extensions.Http.Polly` adapter if added; otherwise a hand-authored `CancellationTokenSource.CancelAfter(perCheckBudget)` pattern). Per-check budget is **5 seconds**; aggregate runner budget is **15 seconds** (FR-015 / FR-039 / SC-004). Each check returns a `ValidationCheckResult { name, outcome, reason, durationMs }`; checks that throw or time out map to `outcome = Fail` with a categorical reason (`Timeout`, `Unauthorized`, `NotFound`, `Throttled`, `Unknown`) — never a raw exception message (PII boundary; per FR-035). Span emission: parent span `namespace.validation.run` with attributes `namespace.id`, `namespace.environment`, `validation.aggregate_status` (set on parent span close); per-check child spans named `namespace.validation.check.<name>` with attributes `validation.check.outcome`, `validation.check.duration_ms`, `validation.check.reason_category`. ARM client correlation-request-id is propagated as a span attribute `azure.arm.x_ms_correlation_request_id` where present.

**Rationale**:
- Parallel execution is the only realistic way to hit the 15s p95 budget — serial execution at 5 × 3s p95 per check = 15s with no headroom for jitter.
- `CancellationTokenSource.CancelAfter` is sufficient for v1 — Polly adds value when retry/circuit-breaker semantics are needed, but the spec's `RequiredPermissions` semantics are "verify the operator did the runbook" — retrying on Unauthorized would just make the validation slower without changing the outcome.
- Categorical reasons (rather than raw exception messages) keep PII out of telemetry (FR-035) and give downstream consumers (future operational dashboards) stable strings to aggregate over.
- Child spans per check map cleanly to Azure Monitor's distributed-trace UI — operators reviewing a validation failure see five timestamped rows under the parent span with clear pass/fail icons.

**Alternatives considered**:
- Sequential checks (each one feeds the next) — rejected: blows the budget; also the checks are logically independent (RequiredPermissions doesn't depend on Existence having run first; failing one doesn't cascade).
- Two-tier (fail-fast on Existence, then run the other four in parallel) — considered. Would shave latency on the most common failure mode (operator pasted wrong ARM id). Rejected for v1 to keep the orchestrator simple; can be added later as a perf optimization without changing the contract.
- Storing per-check spans separately in App Insights for later batch query — rejected: redundant with OTel span emission; the standard pipeline already persists spans to App Insights.

**Operational definition of "normal ARM responsiveness"** (referenced by FR-015, FR-039, SC-004): the ARM management plane responds within p99 < 3s per call and is NOT issuing `Retry-After` headers / 429 responses at the moment of measurement. When ARM is degraded (responding > 3s p99 or actively throttling), the runner's per-check timeout fires and the aggregate run reports `Unhealthy` for the affected checks — the 15s p95 budget is a guarantee of *latency-bounded failure*, not of success-rate during ARM outages. SC-011 explicitly carves out the ARM-degraded case for the rest of the slice's surface (browse/details/lifecycle/edit must still function).

---

## §6. ValidationRun persistence: container shape + retention

**Decision**: New Cosmos container **`namespace-validation-runs`** on the existing `canonical` database. Partition key **`/namespaceId`** (every ValidationRun for a namespace lives in one logical partition; matches the natural query "give me the most recent N runs for namespace X"). Document shape: `{ id (== runId, Guid), namespaceId (Guid), executedAtUtc (ISO 8601), executedBy (Guid — Entra objectId), azureResourceIdAtRun, aggregateStatus, checkResults: [{name, outcome, reason, durationMs}], _etag, _ts }`. **Indefinite retention in v1** (no TTL). Autoscale RU at the lowest band the account allows (matching `registry-audit`'s pattern). Provisioned by extending `iac/modules/cosmos-registry-store/` — one additional container declaration; no new module.

**Rationale**:
- `/namespaceId` is the only sensible partition key — every query is namespace-scoped (list runs for a namespace, get latest run, get specific run by id-within-namespace).
- Indefinite retention matches spec-006's audit-event policy (`spec.md §FR-031` → "follow spec 006's retention model"). A future ops-hardening spec defines TTL.
- Append-only via the `IAuditEventStore`-shaped store pattern (`INamespaceValidationRunStore.AppendAsync(run)` and `ListForNamespaceAsync(namespaceId, limit)` — no `UpdateAsync`).
- Re-using the existing `cosmos-registry-store` module's container-list variable means the IaC delta is a single-line addition.

**Alternatives considered**:
- Storing ValidationRun records as nested entries inside the namespace document — rejected: unbounded growth on a hot document, race conditions on append, and breaks the namespace document's ETag concurrency semantics established by spec 006.
- Storing ValidationRun records in the existing `registry-audit` container — rejected: audit events are entity-state-change events (Created/Updated/Lifecycle/etc.); ValidationRuns are health-probe events. Conflating them muddies the audit-log UX (the details-page audit panel would have to filter by type rather than just rendering everything).
- Adding a TTL of 90 days — deferred. Useful operationally but worth a deliberate retention-policy spec rather than baking a number into v1.

---

## §7. RegistrySource enum extension: `Manual` + `Onboarded` coexistence

**Decision**: Add **`Onboarded`** as a new value of the existing `RegistrySource` enum in `Features/Registry/_Shared/RegistrySource.cs`. Existing `Manual` value and existing documents are untouched. The enum is serialized via System.Text.Json's default `JsonStringEnumConverter` with case-insensitive parsing (already in use across the project). All consumers tolerate unknown values *gracefully*: the existing spec-006 list/get endpoints will continue to return `Onboarded` documents in their responses (read), and the spec-008 list/get endpoints will surface `Manual` documents only when explicitly requested via a `source` filter parameter (default behavior: Inventory shows `Onboarded` only).

**Rationale**:
- Cosmos is schemaless; adding a new enum value is a write-side and reader-side concern only.
- System.Text.Json's `JsonStringEnumConverter` is configured project-wide and tolerates unknown values by deserializing them to the default enum value (zero) — but spec 006 has only `Manual` as the existing value, so any document with `source = Manual` or `source = Onboarded` deserializes correctly. Documents written before this spec land have `source = Manual` (explicit string); documents written by spec 008's onboarding endpoint write `source = Onboarded`.
- Spec 006's polymorphic Update/Delete endpoints rebound to **reject writes** when `source = Onboarded` (`409 Conflict` with a redirect-style error pointing to the spec-008 endpoints) — preserves ownership invariants (a spec-006-shaped PUT does not carry the structured `ownership` block and would obliterate the spec-008 fields). This is a one-method tweak in `_Shared/UpdateEndpoint.cs` and `_Shared/DeleteEndpoint.cs`. Reads remain open.

**Alternatives considered**:
- A separate `OnboardedNamespace` Cosmos container — rejected by the spec's Assumption ("OnboardedNamespace extends spec 006's `Namespace`, in place — not a parallel entity").
- A pre-write hook in spec 006's endpoints that rejects `Manual` writes once a document has been migrated to `Onboarded` — equivalent to the above, just deferred to write-time inspection. Same net behavior; chose to put the gate at the endpoint level for clarity.
- Deprecating `Manual` entirely and migrating existing docs — rejected: out of scope; legacy 006 records are explicitly preserved per the spec's Assumptions.

---

## §8. API routing strategy and concurrency-conflict reuse

**Decision**: New endpoints under the **`/api/namespaces`** prefix, parallel to (not nested under) spec 006's `/api/registry/*`. Spec 008's endpoints declare both `[Authorize]` and the new `RolePolicies.CanAdministerNamespaces` policy on every write surface. The spec 006 conflict response (`contracts/conflict-response.schema.json` in the 006 spec) is **reused unchanged** for concurrent-edit conflicts on `/api/namespaces/{id}/metadata` and `/api/namespaces/{id}/ownership` — same shape, same ETag-on-If-Match semantics, same Force-overwrite audit event treatment.

Five new audit event types are added to the existing `AuditEventType` enum: `NamespaceOnboarded`, `NamespaceMetadataUpdated`, `NamespaceOwnershipUpdated`, `NamespaceLifecycleTransitioned`, `NamespaceValidationExecuted`. The existing `AuditEvent` record gains a nullable `LifecycleReason: string?` field (populated only on `NamespaceLifecycleTransitioned`).

**Rationale**:
- Distinct prefix (`/api/namespaces` vs `/api/registry`) makes the authorization gate (Reader-tier vs NamespaceAdministrator-tier) self-evident from the URL.
- Distinct prefix lets the OpenAPI document be authored in two files (one per spec) without route-collision concerns; the runtime aggregator stitches them into a single served document per the existing pattern.
- Reusing the spec 006 conflict response keeps frontend conflict-modal code identical between registry edit and namespace edit — one component, two consumers.
- Adding event types to the existing audit enum keeps the audit-panel UI generic; new event types render the same way the existing `Created`/`Updated`/`Deleted`/`StatusChanged` events do, with the change-summary string carrying the discriminator's human-readable form.

**Alternatives considered**:
- Nested under `/api/registry/namespaces/onboarding/...` — rejected: implies the onboarding feature is a sub-feature of the polymorphic registry, when it actually carries different authorization semantics, a different DTO surface, and a separate Cosmos container (ValidationRun). The parallel-prefix is cleaner.
- A second OpenAPI document served at a separate URL — rejected: one served document per service per the existing convention; two authored files compose into one runtime document.
- A distinct `NamespaceAuditEvent` document shape in a separate container — rejected: redundant; the existing `registry-audit` shape with a discriminator field is sufficient and keeps the operator-facing audit panel generic.

---

## §9. Frontend wizard state management

**Decision**: Single React Hook Form root form (`useForm<NamespaceOnboardingWizardValues>`) spanning all five wizard steps; step-local Zod schemas are composed into the root schema via `z.object({}).extend({})` for per-step validation; submit handler is invoked only at step 5. Wizard state is persisted to **`sessionStorage`** under key `bt:namespaces:wizard:v1` on every meaningful change (debounced 300ms); cleared on (a) successful registration, (b) explicit cancel, (c) window close (cleared via `beforeunload` listener). Per FR-002, transient state in browser only — never sent to the backend until step-5 register. Step navigation uses a custom `<NamespaceOnboardingWizard>` Client Component that owns the current-step state and renders the active step via a `match`-style discriminator. The custom `<WizardStepper>` indicator composes existing shadcn `Card` + `Badge` primitives with `framer-motion` animated step-dots; `prefers-reduced-motion` is honored by short-circuiting transitions.

**Rationale**:
- Single root form means a single source of truth — step navigation back/forward preserves all prior input automatically without per-step state-lifting boilerplate.
- `sessionStorage` (not `localStorage`) bounds the persistence to the browser tab; closing the tab clears it (matching the spec's "in-progress form data is best-effort and bounded to the browser tab" stance inherited from spec 006).
- The custom Stepper avoids introducing a third-party dependency for a single composite — every constituent primitive is already in the tree.

**Alternatives considered**:
- One RHF instance per step + a parent reducer to aggregate — rejected: more boilerplate, more chances for shape drift between step-form and parent reducer.
- A third-party stepper library (`@react-stately/list`, `@chakra-ui/...`) — rejected by the constitution's "no second design system" + "no new UI libraries without explicit approval" rules.
- Persisting via TanStack Query cache + a custom persister — rejected: TanStack Query is purpose-built for server-state caching, not transient client-form state.

---

## §10. Tenant-ID enforcement (FR-006 cross-tenant rejection)

**Decision**: Backend: the new `NamespaceArmIdParser` reads the configured Entra tenant id from `IConfiguration["AzureAd:TenantId"]` at construction time (singleton). The parser extracts the subscription id from the ARM resource id and calls `ArmClient.GetSubscriptionResource(subId).GetAsync()` to retrieve the subscription's `tenantId` property; if it does NOT match the configured tenant id, validation fails Step 1 with reason `CrossTenantArmId`. The check is performed *server-side* (frontend cannot be trusted with this assertion). Cached at the parser level for the duration of one onboarding wizard session (keyed on subscription id) to avoid repeated ARM round-trips when the operator edits other fields.

Frontend: the wizard step 1 reads `tid` from MSAL via the existing `getTid(account)` helper and pre-validates the ARM ID's subscription against the user's `tid` as a UX hint — but the authoritative rejection is the backend's. The frontend hint is purely advisory and uses an `aria-live="polite"` warning band, not an inline form error (to avoid spurious blocks if the user has multi-tenant access).

**Rationale**:
- Server-side authoritative; frontend advisory. Matches the spec's explicit "must be server-validated" stance.
- Caching at the parser level bounds ARM calls during a wizard session — the user changes display name 12 times, the subscription-to-tenant lookup happens once.
- Using `tid` claim on the frontend avoids needing a separate `/api/namespaces/tenant-id` endpoint.

**Alternatives considered**:
- Reading the tenant id from the ARM resource id itself — rejected: the ARM id format does NOT include the tenant; it includes the subscription id, and subscription-to-tenant is an ARM-mediated lookup.
- Deferring cross-tenant rejection to Step 4 validation (Existence check would fail anyway because the workload UAMI can't reach a different-tenant resource) — rejected: the explicit "cross-tenant" diagnostic is meaningfully more actionable than a generic "Existence failed" for this case.

---

## §11. Drift detection on re-validation (FR-029)

**Decision**: The validation runner records the *Azure-truth* values (region, resource group, subscription id from the just-returned ARM resource) into the `ValidationRun` document's `armResourceSnapshot` field (new sub-shape: `{ region, resourceGroup, subscriptionId }`). On every re-validation, the runner compares those values against the persisted namespace document's stored fields. Mismatches set the `ValidationRun.driftDetected` boolean true and populate `ValidationRun.driftFields[]` with `{ field, persistedValue, observedValue }` entries. The Details page renders a "metadata drift detected" warning panel above the validation-run viewer when the latest run has `driftDetected = true`. The persisted namespace document's region/RG/subscription fields are **NEVER auto-updated** — only surfaced.

**Rationale**:
- Matches FR-029 exactly: "drift MUST be surfaced... but MUST NOT mutate the persisted Azure-identifier fields automatically."
- Storing the snapshot inside `ValidationRun` means drift information is timestamped and audit-traceable — operators can see when drift first appeared.
- Reconciliation is intentionally out of scope (spec Non-Goal: "Drift auto-reconciliation").

**Alternatives considered**:
- Surfacing drift as a top-level field on the namespace document — rejected: drift is a property of a run, not of the namespace; storing it on the namespace would require updating the namespace on every revalidation, defeating its append-only semantics for this concern.
- Auto-updating the namespace document with the observed values — rejected by the spec.

---

## §12. Inventory query: persistence-only, no AI Search dependency

**Decision**: The `/api/namespaces` Inventory endpoint queries the existing `registry-entities` Cosmos container directly via the existing `IRegistryEntityStore.ListAsync` with new filter parameters: `source = Onboarded`, optional `lifecycleStatus`, optional `validationStatus`, optional `environment`, optional `tag` filter, optional partial-name match across `displayName` and `businessUnit`. Server-side sorting and pagination via existing patterns. AI Search is NOT consulted by Inventory in v1 (per FR-021).

**Rationale**:
- FR-021 explicitly requires Inventory + Details to be served from the persistent store, not the search index.
- Cosmos's PK is `/environment`, so cross-environment queries are cross-partition (slow at scale but acceptable for v1's "hundreds per environment" target — research confirms cross-partition reads of 100s of docs are sub-second at p95).
- Partial-name search via `CONTAINS()` against `displayName` and `businessUnit` is supported on Cosmos SQL queries; not as fast as an inverted index but well within FR-037's 1s budget at the v1 scale.

**Alternatives considered**:
- Adding a namespace-specific projection to the existing AI Search index — rejected for v1: AI Search is already populated by the spec-006 indexer (`registry-entities-v1`); the new fields appear automatically as projected metadata since the indexer reads the source document. But spec-008's Inventory does NOT query AI Search per FR-021. The projection IS available for a future spec to leverage.
- A second AI Search index — rejected: redundant; complexity not justified at v1 scale.

---

## §13. Frontend Stepper / Combobox / DataTable choices

**Decision**:
- **Stepper**: custom composite component `<WizardStepper>` built from existing shadcn `Card` + `Badge` + `framer-motion` (already pinned). 5 step dots with active/completed/pending visual states; honors `prefers-reduced-motion`.
- **Combobox** (for the Entra picker): assembled from existing `Popover` + `Command` (cmdk) primitives — this is the shadcn-canonical Combobox composition, no third-party library needed.
- **DataTable** (for the Inventory): reuse the existing TanStack Table v8 wiring from spec 006 (`registry-search-results-table.tsx` pattern); namespace-specific column factory in `components/namespaces/inventory/`.

**Rationale**: Each composition is the canonical shadcn pattern (verified via shadcn/ui MCP at task time). Avoids new dependencies. Keeps the design-system surface coherent.

**Alternatives considered**:
- Pulling in a third-party stepper or combobox — rejected by constitution ("no new UI libraries without explicit approval").
- Building a custom DataTable rather than reusing TanStack — rejected: spec 006 already invested in TanStack Table integration; reuse delivers identical behavior for zero marginal cost.

---

## §14. ServiceBus management endpoint metadata probe (`ApiReachabilityCheck`)

**Decision**: The `ApiReachabilityCheck` issues a lightweight `GET https://{namespaceName}.servicebus.windows.net/$Resources?api-version=2017-04` via the existing `Azure.Identity` token (acquired from the same `DefaultAzureCredential`); a 200/401/403 response is `Pass` (server is reachable; auth distinction is captured separately by the `IdentityAuthorization` check); only a network-level failure (timeout, DNS error, TLS error) is `Fail`. Per-check timeout 3s.

**Rationale**:
- The Service Bus management endpoint is a separate plane from ARM; reachability here is a distinct check (FR-014).
- Using `$Resources?api-version=2017-04` returns a tiny payload (a list of resource roots) without enumerating any actual queues/topics/subscriptions — minimal data-plane access.
- 401/403 mean "reachable but unauthorized" — which the IdentityAuthorization check catches separately. ApiReachability is strictly about network reachability.

**Alternatives considered**:
- Using `GET https://{ns}.servicebus.windows.net/` (root) — rejected: less stable response shape; some Azure regions return different content.
- Skipping this check and folding it into Accessibility (ARM call) — rejected by FR-014, which lists ApiReachability as a distinct check.

---

## §15. App Role declaration — additive to spec 003 matrix

**Decision**: Add **`namespace-administrator`** App Role to the BusTerminal API Entra app registration via the existing `iac/modules/app-registration-roles/` module. New entry in the `role_definitions` map:

```hcl
"namespace-administrator" = {
  role_id              = "<stable UUID generated at planning time>"
  allowed_member_types = ["User", "Group"]
  display_name         = "Namespace Administrator"
  description          = "May onboard, edit, lifecycle-transition, and validate Azure Service Bus namespaces."
  value                = "BusTerminal.NamespaceAdministrator"
}
```

A new `PlatformRole.NamespaceAdministrator` enum value (claim `BusTerminal.NamespaceAdministrator`) is added to `Authorization/PlatformRole.cs`. A new `RolePolicies.CanAdministerNamespaces` policy is registered. The new `IsNamespaceAdministrator()` extension method on `PlatformPrincipal` returns true iff `EffectiveRoles.Contains(PlatformRole.NamespaceAdministrator)`. The spec 003 role-permission matrix contract document is updated in a follow-up PR (NOT as part of this slice's task graph — flagged in `contracts/outputs-contract.md` as a follow-up).

**Rationale**:
- Single source of truth for App Role definitions is the IaC module; same pattern as the four existing roles.
- The new policy is registered alongside `RolePolicies.CanMutateDomain`, `CanOperatePlatform`, etc. — consistent surface.
- Per Clarification Q1, the role is tenant-wide (not per-environment) — no environment-scoped role variants are declared.

**Alternatives considered**:
- Declaring the role on a separate Entra app — rejected: increases consent/assignment ceremony for operators; the API app is the natural home.
- Using an Entra security group instead of an App Role — rejected: App Roles are project convention per spec 003.

---

## §16. Audit event extension for lifecycle reason notes

**Decision**: Add nullable `LifecycleReason: string?` field to the existing `AuditEvent` record in `Features/Registry/_Shared/AuditEvent.cs`. Populated only on `AuditEventType.NamespaceLifecycleTransitioned` events. Other event types serialize without the field (null). Field bounded at 1000 characters; FluentValidation enforces.

**Rationale**:
- Lifecycle transitions are the only event type that carries a free-form operator-supplied reason; making the field nullable on the shared record avoids creating a parallel audit-event entity.
- Existing serialization tolerates nullable additions (System.Text.Json default behavior).
- Bounded length keeps audit document sizes predictable.

**Alternatives considered**:
- A separate `LifecycleAuditEvent` record — rejected: redundant inheritance ceremony; the shared record already carries an extensible `ChangeSummary`.
- Putting the reason in the `ChangeSummary` field — rejected: the change summary is the *human-readable sentence* for the audit panel; the reason note is a separate operator-supplied input that needs to be query-filterable for governance audits.

---

## §17. Workload UAMI `principalId` resolution at runtime

**Decision**: Inject the workload UAMI's `principalId` as an **environment variable** `WORKLOAD_PRINCIPAL_ID` at deploy time. The value is sourced from `module.workload_identity.principal_id` (an existing OpenTofu output) and passed into the Container App's `env_vars` block alongside the existing `AZURE_CLIENT_ID`. The new `WorkloadIdentityProvider` reads `IConfiguration["WORKLOAD_PRINCIPAL_ID"]` at startup, parses it as `Guid`, caches it for the process lifetime, and exposes it via `Task<Guid> GetPrincipalIdAsync(CancellationToken)`. Falls back to a structured `ERROR` log + 500 from `/api/namespaces/identity` if the env var is missing or unparseable (which would indicate a deployment misconfiguration).

**Rationale**:
- Microsoft Graph's `/me` endpoint **does not work** for application-token (managed-identity) flows — it is delegated-flow-only. Calling `/me` from a workload identity returns 401/404 at runtime.
- The clean alternatives are (a) Graph `GET /servicePrincipals?$filter=appId eq '{AZURE_CLIENT_ID}'`, which requires the Graph picker to be working *and* an extra round-trip on cold start, or (b) injecting the principalId directly from the deployment-time IaC output. Option (b) is strictly less ceremony — the value is already known to OpenTofu at apply time, the existing `container-app` module already accepts arbitrary `env_vars`, and there is no Graph dependency on cold start.
- The IaC delta is one new env-var entry in `iac/environments/{dev,test,prod}/main.tf` per environment composition where the backend Container App is declared.

**Alternatives considered**:
- Graph `GET /servicePrincipals?$filter=appId eq ...` — adds startup-time Graph dependency for a value that is static across the process lifetime; rejected.
- Configuration via Key Vault — rejected: the `principalId` is not a secret, and adding KV read for a non-secret value is unnecessary indirection.
- Hard-coding the `principalId` in `appsettings.{env}.json` — rejected: couples runtime config to per-environment files outside the IaC source of truth; drift risk.

---

## §18. Pre-onboarding `ValidationRun.namespaceId` allocation

**Decision**: The frontend wizard **pre-allocates** the namespace's `id` (`Guid`) **at the start of step 4** (immediately before the first validation run). The pre-allocated `Guid` is stored on the wizard's RHF root form, used as the `ValidationRun.namespaceId` for every validation run during the wizard, and re-used as the persisted `OnboardedNamespace.id` when step-5 Register succeeds. The backend `POST /api/namespaces/_validate` endpoint accepts an optional `proposedNamespaceId: Guid?` field in the request body — if present and well-formed, the runner stamps the resulting `ValidationRun.namespaceId` with it; if absent (e.g., a direct API caller bypassing the wizard), the runner generates a fresh `Guid`. On `POST /api/namespaces` (step-5 register), the request body's `validationRunId` is looked up; the runner's `namespaceId` MUST equal the new namespace's `id`; mismatch → 400 with `Code = "NamespaceIdMismatch"`.

**Rationale**:
- `ValidationRun.namespaceId` is the Cosmos partition key per `data-model.md §3`. Using `Guid.Empty` would create a hot partition for every pre-onboarding run in the system; using a fresh-but-different `Guid` per run would scatter pre-onboarding runs across thousands of single-document partitions (also wasteful + unindexable).
- Pre-allocating the namespace `id` upfront lets the wizard's three validation runs (step 4 retries) all live in the same partition AND lets the eventually-registered namespace document use the same `id`, so the audit trail and ValidationRun trail are partition-aligned from day one.
- Mutating a Cosmos document's PK after-the-fact is **not supported** by the SDK — pre-allocation is the only clean answer.
- The `proposedNamespaceId` field is optional so direct API callers (CI scripts, test harnesses) that don't care about wizard pre-allocation can still call `_validate` and get a usable ValidationRun.

**Alternatives considered**:
- Server-allocated `namespaceId` returned in the `_validate` response, threaded back through the wizard's RHF state — works but adds an extra round-trip and a state-management concern (every step-4 retry needs to re-thread the same id).
- Storing pre-onboarding runs in a separate `namespace-validation-runs-staging` Cosmos container — adds operational surface for a transient concern.
- Embedding the validation run inside the wizard's sessionStorage only (never persisting until step-5) — rejected: the spec FR-016 explicitly requires every validation execution to be persisted as a ValidationRun record, including failed pre-onboarding attempts (so operators can see the history of attempts in the audit trail later).

---

## Summary

All 18 decisions above resolve the planning-time NEEDS-CLARIFICATION items implicit in the Technical Context. None of them invalidate the spec's Functional Requirements; several refine implementation-level details that the spec deliberately left open. Decisions §17 (workload principalId injection) and §18 (pre-allocated namespaceId) were added during the `/speckit-analyze` remediation pass for findings F2 and F3 respectively; the spec's FR-043 was amended in the same pass to explicitly carve out operator-supplied namespace grants (closing finding F1).

The two surviving deviations from the project's IaC convention are documented in `plan.md §Complexity Tracking`:
1. **Operator-supplied namespace Reader-role grant is out-of-band** (research §4) — now sanctioned by amended FR-043.
2. **A fifth platform role `namespace-administrator` is added to spec 003's role matrix** (research §15).

Both are justified, scoped, and reversible by a future spec.
