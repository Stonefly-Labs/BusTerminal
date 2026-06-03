# Quickstart — Service Bus Registry Core

**Plan**: [plan.md](./plan.md) · **Spec**: [spec.md](./spec.md) · **Date**: 2026-06-02

This guide is the operator walkthrough for spec 006: standing up the registry locally, deploying it to dev, registering the first entities, validating search lag, and exercising the conflict UX. It assumes spec 005's infrastructure baseline is applied and the spec-003 auth foundation works (sign-in against the real Entra dev tenant).

---

## 1. Prerequisites

- Spec 005 dev environment applied (Cosmos account, AI Search service, CAE, ACR, KV, App Insights, LAW, workload UAMI all reachable).
- `az login` against the BusTerminal dev tenant. Your developer identity is what `DefaultAzureCredential` resolves locally.
- Node ≥ 22.13, pnpm 11.x.
- .NET SDK 10.x.
- OpenTofu ≥ 1.11.
- `azure-functions-core-tools@4` (only required if you want to run the indexer locally — optional, see §4.2).

---

## 2. First-time setup checklist

```text
[ ] git checkout feature/006-service-bus-registry-core
[ ] cd web && pnpm install          # picks up @tanstack/react-query
[ ] cd api && dotnet restore        # picks up FluentValidation and (new) BusTerminal.Indexer project
[ ] cd iac && tofu init             # picks up the new azapi provider
[ ] az login                        # resolves DefaultAzureCredential for local API + indexer
```

---

## 3. Deploy infrastructure to dev

```text
cd iac/environments/dev
tofu plan -out=plan.tfplan
tofu apply plan.tfplan
```

The plan adds:

- 3 Cosmos containers (`registry-entities`, `registry-audit`, `registry-entities-leases`) on the existing `canonical` database.
- 1 AI Search index (`registry-entities-v1`) via the `azapi` provider.
- 1 Functions-for-CAE container app (`bt-dev-indexer-<suffix>`, `kind = "functionapp"`) with the workload UAMI bound and KV-secret-reference to the App Insights connection string (consistent with spec-005's Q1c hybrid pattern).
- 2 new role assignments: workload UAMI → AI Search service: `Search Index Data Reader` and `Search Index Data Contributor`.
- 1 diagnostic setting on the new container app (`allLogs`-only via the existing wrapper).

CI policy gates (BT-IAC-001..007) MUST pass before merge. Expect zero new allowlist entries.

After apply, capture the new outputs:

```text
tofu output cosmos_registry_entities_container_name
tofu output ai_search_registry_index_name
tofu output indexer_container_app_name
```

---

## 4. Run the API and frontend

### 4.1 Backend (`BusTerminal.Api`)

```text
cd api/BusTerminal.Api
dotnet run
```

The API now exposes the registry endpoints alongside the existing health/identity/role-probes surface:

- `GET    /api/registry?environment=dev`
- `POST   /api/registry`
- `GET    /api/registry/{id}`
- `PUT    /api/registry/{id}` (requires `If-Match`)
- `DELETE /api/registry/{id}` (requires `If-Match`)
- `GET    /api/registry/{id}/audit?limit=50`
- `GET    /api/registry/search?q=...&environment=...`

OpenAPI document available at `/openapi/v1.json`; the runtime document is asserted against [`contracts/registry-api.yaml`](./contracts/registry-api.yaml) by the contract test (`SharedSchemaContractTests`).

### 4.2 Indexer (`BusTerminal.Indexer`) — optional locally

The indexer runs in dev automatically as the `bt-dev-indexer-*` container app. Running it locally is only useful when debugging projection logic.

```text
cd api/BusTerminal.Indexer
func start
```

Local indexer auth resolves via `DefaultAzureCredential` (your `az login` identity). Your developer principal needs `Cosmos DB Built-in Data Contributor` on the `canonical` database AND `Search Index Data Contributor` on the AI Search service for local indexing to work. Per spec-005 dev posture both are granted to the developer-MI role at `iac/platform-bootstrap/`.

### 4.3 Frontend

```text
cd web
pnpm dev
```

Open `http://localhost:3000/registry`. You should see the empty-state landing page and an empty explorer tree.

---

## 5. Golden-path walkthrough — register the first namespace and child entities

Goal: validate Story 1 acceptance scenarios end-to-end (FR-001..FR-014). SC-001 is "under 10 minutes total, no documentation outside the in-app UI" — this walkthrough mirrors what an operator would do.

1. **Register a namespace**:
   - Click **New → Namespace** in the explorer.
   - Fill: `name = "orders-prod"`, `environment = "dev"`, `azureResourceId = "/subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.ServiceBus/namespaces/orders-prod"`, `owner = "payments-platform"`, `tags = [Tier=1]`.
   - Submit. Expect 201 + ETag header. Explorer refreshes; `orders-prod` appears under the `dev` environment node.

2. **Register a queue under it**:
   - Click **New → Queue** with `parent = orders-prod`.
   - Fill: `name = "orders-incoming"`, environment auto-inherits to `dev`.
   - Submit. Expect 201. Queue appears nested under `orders-prod`.

3. **Edit the queue**:
   - Open the queue's detail page. Click **Edit**.
   - Change description. Submit.
   - Expect 200, updated detail panel, advanced `updatedAtUtc`, new audit event in the **Audit** panel.

4. **Add a topic + subscription + rule**:
   - Same pattern. Verify the explorer tree and the relationships panels on each detail page.

5. **Delete the rule** (leaf): Expect 204; explorer removes it.
6. **Try to delete the subscription** (still has a rule? if you deleted the rule it's now leaf — delete succeeds): To exercise FR-009, register a second rule under another subscription, then attempt to delete that subscription — expect 409 with `code: "HasChildren"` and `childCount: 1`.

7. **Try to register a duplicate-named queue** under `orders-prod`: Expect 409 with `code: "DuplicateName"`.

8. **Try to register a queue with missing required fields**: Expect 400 with field-level error details (FR-016).

---

## 6. Search validation (Story 2)

Goal: validate FR-022..FR-026 + SC-002 + SC-005.

1. Wait 5 seconds after the last create (SC-005 budget).
2. Open `/registry/search` and type `orders`. Expect ranked results showing all four entities under `orders-prod` (namespace, queue, topic, subscription).
3. Apply filter: `entityType = Queue`. Expect only the queue.
4. Apply filter: `environment = test`. Expect zero results with the "no results for this query" empty state (NOT the "search unavailable" one).
5. Apply tag filter: `tagKey = tier`. Expect entities tagged with key `Tier` matched case-insensitively.
6. Open the browser network panel. Confirm every registry API request carries a `traceparent` header (FR-042 / SC-012).

---

## 7. Audit + relationships (Story 3)

1. Navigate from a topic detail page to one of its subscriptions (relationships panel link). Expect the subscription's detail page to load in < 500ms (SC-003 / FR-044).
2. From the subscription, drill into a rule.
3. On every detail page, check the **Audit** panel — expect the most recent create/update/delete events with actor identity, UTC timestamp, change summary.
4. Click into any audit event — expect the field-diff popover for `Updated`/`StatusChanged` events.

---

## 8. Conflict UX (FR-020)

1. Open the same queue's edit form in two browser tabs simultaneously.
2. In tab A: change `description`, submit. Expect 200.
3. In tab B (still showing the old version): change `owner`, submit.
4. Expect tab B to show the **Concurrency Conflict** modal with:
   - The current server state (rendered in a "current" column).
   - The fields you changed (`owner` only) — shown side-by-side with current values.
5. Choose **Discard my changes and refresh** — tab B form resets to current state. Toast: "Refreshed to current state".
6. Repeat steps 1-3. This time choose **Force overwrite** — tab B's PUT re-submits with `_overwriteAcknowledged: true`. Expect 200.
7. Check the audit panel — the latest `Updated` event has `wasForceOverwrite: true`.

---

## 9. Status transitions (FR-013a)

1. Open a queue detail page. Click **Mark as Deprecated**.
2. Expect the status badge to flip from **Active** to **Deprecated** with a visual distinction (color + icon + text — per FR-047 color-not-alone rule).
3. The entity remains fully visible in browse, search, and detail (FR-013a — Deprecated is NOT a hide-from-view mechanism).
4. Audit panel shows a `StatusChanged` event with `fieldChanges: [{ field: "status", before: "Active", after: "Deprecated" }]`.
5. Click **Reactivate** to transition back. Audit records the second `StatusChanged`.

---

## 10. CI gates to expect on the PR

| Gate | What it checks |
|---|---|
| `dotnet build` + `dotnet test` | Backend unit / integration / contract tests pass; new `BusTerminal.Indexer` project compiles. |
| `pnpm run lint` + `pnpm typecheck` + `pnpm test` | Frontend lint, TS strict, Vitest pass. |
| `pnpm test:e2e` | Playwright E2E flows: explorer, search, create, edit, conflict, delete-leaf, delete-blocked. |
| `pnpm test:a11y` | axe-playwright over the 6 new App Router segments — zero violations on dark and light themes. |
| `iac-validate.yml` | `tofu fmt -check -recursive`, `tofu validate` per env, `tofu plan` PR-comment, checkov + tfsec, BT-IAC-001..007 policy gates, `terraform-docs --output-check`. Expect all green; no allowlist additions. |
| `pnpm run test:contracts` | Shared-schema parity: Zod schemas in `web/lib/registry/schemas.ts` ↔ FluentValidation rules ↔ `contracts/registry-entity.schema.json`. |
| Runtime OpenAPI ↔ `contracts/registry-api.yaml` assertion | The API's generated OpenAPI document matches the human-authored YAML. |

---

## 11. Tech-stack updates (post-merge follow-up)

Update `speckit-artifacts/tech-stack.md` per research §21:

- **§1 Backend**: add row for FluentValidation 11.10.x (boundary HTTP request validation).
- **§2 Frontend**: add row for TanStack Query 5.x (server-state on interactive surfaces only).
- **§5 Data Platform**: annotate Cosmos row with the change-feed-lease-container requirement under managed-identity auth.
- **§6 Hosting**: add row for Native (v2) Functions on Container Apps (`Microsoft.App/containerApps` with `kind = functionapp`); mark legacy v1 (`Microsoft.Web/sites` proxy) as prohibited for new workloads.

---

## 12. Troubleshooting

| Symptom | Likely cause | Fix |
|---|---|---|
| `POST /api/registry` returns 401 | Token missing or expired | Re-sign-in in the frontend; for direct API calls, refresh the access token from MSAL. |
| `POST /api/registry` returns 400 with `code: "ParentNotFound"` | `parentId` references an entity in a different environment or a non-existent id | Verify the parent exists in the same environment partition. |
| Indexer is not picking up changes | Lease container missing or RBAC missing | Verify `registry-entities-leases` container exists (IaC); verify workload UAMI has `Cosmos DB Built-in Data Contributor` on the database. |
| Search returns empty for an entity that exists | Index lag exceeded SC-005 budget; check indexer logs | App Insights → Logs → search for `RegistryEntityIndexer` and the entity id; if logs show `errorCategory: aiSearchSchema`, the projection mapping has drifted from the index field set — see `contracts/indexer-events.md` §3. |
| AI Search returns 403 from the API | Workload UAMI does not have `Search Index Data Reader` | Verify the role assignment was applied (`az role assignment list --assignee <uami-principal-id>`). |
| Search returns 503 | AI Search service unavailable; check service health | Browse and detail remain functional (SC-011); the empty-state on `/registry/search` distinguishes "search unavailable" from "no results". |
| Conflict modal shows but force-overwrite returns 400 `ForceOverwriteWithoutConflict` | The intervening write was rolled back before the second PUT landed; no conflict exists | Discard the modal and re-submit normally. |
| `tofu plan` shows the `azapi` provider unauthorized | First-time provider use needs `az login` with the pipeline UAMI permissions | Confirm the pipeline UAMI is the one running the apply; locally `az login` your developer identity. |
