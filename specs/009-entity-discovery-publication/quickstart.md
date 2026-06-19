# Quickstart: Entity Discovery and Publication

**Spec**: [spec.md](./spec.md) · **Plan**: [plan.md](./plan.md) · **Data Model**: [data-model.md](./data-model.md) · **Contracts**: [contracts/openapi.yaml](./contracts/openapi.yaml)

This document walks a developer (or an LLM coding agent) through running discovery end-to-end on a local box and against the dev environment, plus the smoke tests that prove US1–US4 from the spec. It does **not** repeat the platform-wide setup (Cosmos emulator, Service Bus emulator, `az login`, MSAL configuration) — those are documented in the project root `README.md` and in each prior spec's quickstart.

---

## Prerequisites

- The platform-wide local dev environment is up (per the project README): API, Indexer, web, Cosmos emulator, Service Bus emulator.
- You are signed in via `az login` against the BusTerminal dev tenant.
- You have at least one registered namespace (Spec 008) pointing at a real Azure Service Bus namespace you can read. The dev-environment runbook (`iac/runbooks/grant-namespace-reader.md`) grants the workload UAMI the `Reader` role on that namespace.

If you're starting from a blank dev box, run the existing `make dev-up` (or PowerShell equivalent) to bring up the local stack, then onboard a namespace via the existing Spec 008 UI flow.

---

## Spec 009 — Local-stack additions

The platform-wide setup script (`make dev-up`) is extended by Spec 009 to also:

1. **Create the new Cosmos containers** in the emulator: `discovery-runs` (PK `/namespaceId`) and `discovery-locks` (PK `/namespaceId`).
2. **Create the internal Service Bus queue** `discovery-requested` (emulator preview supports queues; rule discovery against the emulator is limited — see "Limitations of the local stack" below).
3. **Extend the AI Search emulator-equivalent** (the local AI Search dev cluster) with the four new fields documented in [data-model.md §2.1](./data-model.md#21-registry-entities-v1-extend-existing-schema).

These are wired into the existing `make dev-up` script as additive steps.

---

## US1 walkthrough: discover a registered namespace

**Goal**: confirm the foundational happy path — trigger discovery via the API, watch the worker drain the queue, see entities show up in the catalog.

### Step 1 — Identify a registered namespace

```bash
# List your registered namespaces (Spec 008 endpoint)
curl -sS -H "Authorization: Bearer $(az account get-access-token --resource api://busterminal-api --query accessToken -o tsv)" \
  http://localhost:8080/api/namespaces | jq '.items[] | {id, displayName, azureNamespaceFqdn}'
```

Pick a namespace ID (looks like `ns_01HKXP...`). Note the underlying Azure namespace FQDN so you can sanity-check what discovery finds.

### Step 2 — Trigger discovery

```bash
NAMESPACE_ID=ns_01HKXP...
TOKEN=$(az account get-access-token --resource api://busterminal-api --query accessToken -o tsv)

curl -sS -X POST \
  -H "Authorization: Bearer $TOKEN" \
  -H "traceparent: $(./scripts/new-traceparent.sh)" \
  "http://localhost:8080/api/namespaces/$NAMESPACE_ID/discover" | jq
```

Expected response (HTTP 202):

```json
{
  "discoveryRunId": "dr_01HZAB...",
  "namespaceId": "ns_01HKXP...",
  "status": "Queued",
  "coalescedFromExisting": false,
  "startedUtc": "2026-06-17T14:32:11.123Z"
}
```

If you re-run the same `curl` immediately, you should see `coalescedFromExisting: true` and the same `discoveryRunId` — that's FR-003 in action.

### Step 3 — Watch the worker logs

```bash
# Tail Indexer container logs (or `func start` output if running with the Functions Core Tools)
docker logs -f busterminal-indexer 2>&1 | grep -E "discovery\.(run|fetch|persist)"
```

Expected log sequence (abridged):

```
INFO discovery.run.started runId=dr_01HZAB... namespaceId=ns_01HKXP...
INFO discovery.fetch.queues count=3 duration_ms=312
INFO discovery.fetch.topics count=2 duration_ms=287
INFO discovery.fetch.subscriptions count=4 duration_ms=910
INFO discovery.fetch.rules count=6 duration_ms=722
INFO discovery.persist.batch size=15 ru_consumed=42.1
INFO discovery.run.completed runId=dr_01HZAB... status=Succeeded duration_ms=2890 new=15 updated=0 missing=0
```

### Step 4 — Fetch the run summary

```bash
curl -sS -H "Authorization: Bearer $TOKEN" \
  "http://localhost:8080/api/discovery-runs/dr_01HZAB...?namespaceId=$NAMESPACE_ID" | jq
```

Validate: `status: "Succeeded"`, counts non-zero, `failure: null`, `durationMs` < `300000` (5 min).

### Step 5 — Search the catalog

```bash
curl -sS -H "Authorization: Bearer $TOKEN" \
  "http://localhost:8080/api/entities?namespaceId=$NAMESPACE_ID" | jq '.items[] | {name, entityType, lifecycleStatus, lastSeenUtc}'
```

Every entity returned should have `lifecycleStatus: "Active"` and a recent `lastSeenUtc`.

### Step 6 — Browser smoke

Open `http://localhost:3000/namespaces/$NAMESPACE_ID` and confirm:
- The new "Discover" button is visible (you have the role).
- The "Last discovery" panel shows the run from steps above.
- The "Entity counts" tiles match what step 5 returned.

Then open `http://localhost:3000/registry/search?namespaceId=$NAMESPACE_ID` and confirm entities appear in the search results table.

---

## US1 re-run — change detection

**Goal**: confirm new / updated / missing classification.

1. In the Azure portal (or via `az servicebus`), create a new queue in the underlying namespace, change the TTL on an existing topic, and delete a subscription.
2. Re-trigger discovery via the same `POST` as Step 2 above.
3. After the run completes, fetch the run summary (Step 4). Validate `newCount: 1, updatedCount: 1, missingCount: 1`.
4. Fetch the deleted subscription's entity by ID — its `lifecycleStatus` should now be `Missing`.

---

## US2 walkthrough: search and detail

1. Browser → `http://localhost:3000/registry/search`.
2. Apply filter `entityType = Topic`, `status = Active`. Verify only active topics appear.
3. Click any row. Verify the detail page shows:
   - Two distinct sections: "Azure Metadata" (Azure-sourced) and "Registry Metadata" (curated).
   - A "Discovery Information" panel with `firstDiscoveredUtc`, `lastSeenUtc`, lifecycle status badge.

---

## US3 walkthrough: discovery history

1. Browser → `http://localhost:3000/namespaces/$NAMESPACE_ID/discovery-runs`.
2. Verify runs are listed in reverse chronological order.
3. Engineer a failure: revoke the workload UAMI's `Reader` grant on the Azure namespace (`az role assignment delete ...`), trigger discovery, wait for it to fail.
4. Restore the role grant and trigger discovery again (succeeds).
5. In the history view, confirm the failed run shows status `Failed` and the failure detail includes `category: "Authz"` and a non-PII operator-safe message.
6. Confirm the entity catalog from US2 still shows the previously-discovered entities (failed run did not nuke them — FR-021 / SC-006).

---

## US4 walkthrough: curate metadata

1. Browser → entity detail for any active entity you own.
2. Click "Edit" → add a description, a tag, a documentation link, contact info.
3. Save. Verify the entity detail reflects the edits.
4. Click "Manage associations" → add a `Producer`-role association for some service, save.
5. Re-trigger discovery (which will refresh `azureSourced.*`).
6. Re-open the entity detail. Verify:
   - Every curated field you added is still present.
   - `azureSourced.*` reflects current Azure state.
   - `lastSeenUtc` updated.

---

## Limitations of the local stack

- **Service Bus emulator** has limited rule discovery support; deeply-nested SQL filter expressions may not round-trip identically. For rule-coverage testing, prefer the dev Azure namespace (covered by the runbook).
- **AI Search dev cluster** (single small node) may show slightly different scoring than production. Filter/facet/sort behavior is identical.
- **Cosmos emulator** is RU-unbounded — load tests for SC-005 (5,000+ subscriptions) must run against the dev Cosmos account, not the emulator.

---

## Useful one-liners

| Task | Command |
|---|---|
| Issue a fresh `traceparent` for manual API calls | `./scripts/new-traceparent.sh` (existing) |
| Reset the discovery lock for a namespace (debug only) | `dotnet run --project tools/DiscoveryLockReset -- --endpoint $COSMOS_ENDPOINT --namespace-id $NS_ID` |
| Inspect the discovery lock without modifying it | `dotnet run --project tools/DiscoveryLockReset -- --endpoint $COSMOS_ENDPOINT --namespace-id $NS_ID --read-only` |
| Drain the local `discovery-requested` queue | Service Bus emulator's REST API or VS Code extension |
| Re-run AI Search projection backfill | `iac/scripts/rebuild-search-index.sh dev` |
| Tail discovery telemetry to the console | `dotnet run --project tools/DiscoveryTelemetryTail` |
| Forward discovery telemetry to a local OTLP collector | `dotnet run --project tools/DiscoveryTelemetryTail -- --mode otlp --otlp-endpoint http://localhost:4317` |

---

## Acceptance criteria mapping

The walkthrough above demonstrates every numbered acceptance criterion in [spec.md §Acceptance Criteria](./spec.md#measurable-outcomes):

| AC | Demonstrated by |
|---|---|
| 1, 2–5 | US1 Step 5 (entities visible per type) |
| 6 | US1 Step 4 (run persisted) + US1 Step 5 (catalog populated) |
| 7 | US3 Step 1 |
| 8, 9, 10 | US1 re-run |
| 11 | US4 Step 6 |
| 12 | US2 Step 2 |
| 13 | US2 Step 3 |
| 14 | US3 Steps 3–5 |
| 15 | US1 Step 2 second invocation (`coalescedFromExisting: true`) and idempotency |
| 16, 17 | US3 Steps 3–5 (failure leaves catalog intact) |
| 17 | US3 Step 6 |
| 18 | US4 Step 4 |
| 19 | US1 Step 6 (namespace overview shows hierarchy via counts; entity detail shows parent links) |
| 20 | US1 Step 5 (catalog matches Azure state) |

---

**Phase 1 quickstart status**: ✅ Complete. End-to-end developer walkthrough covers all four user stories and maps onto every spec acceptance criterion.
