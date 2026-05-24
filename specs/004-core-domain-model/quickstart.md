# Quickstart — Core Domain Model

**Plan**: [plan.md](./plan.md) · **Spec**: [spec.md](./spec.md)

This runbook walks a developer or operator through the end-to-end validation of the canonical domain model: provisioning Cosmos DB, granting access, loading the fixture set, running the validation framework, traversing a producer→consumer relationship path, soft-deleting + restoring a topic, and confirming change-event log entries. Each step maps to one or more spec Success Criteria (SC-001 through SC-012).

> **Prerequisites**: Slices 001/002/003 already deployed. The user has `az login` against the BusTerminal dev tenant (per the project memory). For purely local runs, Docker Desktop or OrbStack with at least 4 GB allocated to containers.

---

## Path A — local-only (Cosmos Linux emulator)

This is the path a contributor without Azure access uses to run the integration tests and explore the model. It does NOT exercise the OpenTofu / dev-environment path.

### A.1 Start the emulator

```bash
docker compose up -d cosmos-emulator
# wait ~30s for the emulator to finish provisioning
docker compose logs cosmos-emulator | grep -i "Cosmos DB Emulator is ready"
```

The emulator exposes `https://localhost:8081`. The emulator's well-known master key (not a secret — public string per Microsoft's documentation) is wired in `appsettings.Development.json` under `Cosmos:LocalEmulatorKey`. Production / dev Azure environments do NOT use the key path; they use AAD-RBAC.

### A.2 Create the database and containers

```bash
dotnet run --project tools/load-fixtures -- \
  --endpoint https://localhost:8081 \
  --auth emulator-key \
  --create-database
```

This creates the `busterminal-canonical` database and the two containers (`resources` with `/resourceType` partition; `change-events` with `/resourceId` partition).

### A.3 Load the fixture set

```bash
dotnet run --project tools/load-fixtures -- \
  --endpoint https://localhost:8081 \
  --auth emulator-key \
  --fixtures api/BusTerminal.Api/Fixtures/canonical-fixtures.json
```

Expected output: `Loaded 30 resources, 24 relationships. Validation: 30 OK, 0 Error, 2 Warning, 5 Info.` The Warning and Info findings are expected — the fixture set deliberately includes a deprecated message contract version (Warning), several unknown-type extension surfaces (Info), and a soft-deleted-target relationship (Warning).

**Maps to SC-001** (fixture set materializes), **SC-008** (validation produces graded severity findings).

### A.4 Run the integration test suite

```bash
dotnet test api/BusTerminal.Api.Tests --filter "Category=Integration"
```

The integration tests run against the emulator and exercise CRUD, ETag conflict rejection, soft-delete + restore, change-event log queries, and round-trip serialization. All tests should pass green.

**Maps to SC-001, SC-002, SC-003, SC-004, SC-005, SC-008, SC-009, SC-012.**

---

## Path B — dev-environment (real Azure Cosmos via OpenTofu)

This is the path that exercises FR-024 (no secrets), FR-018 (Managed Identity), and the production-shape AAD-RBAC flow. It requires Azure access.

### B.1 Apply the OpenTofu changes

```bash
cd iac/environments/dev
tofu init  # if not already initialized
tofu plan -out tfplan
tofu apply tfplan
```

The plan should show:

- 1 new `azurerm_cosmosdb_account` (via the `cosmos-account` module wrapping the AVM)
- 1 new `azurerm_cosmosdb_sql_database` named `busterminal-canonical`
- 2 new `azurerm_cosmosdb_sql_container` (`resources`, `change-events`)
- 1 new `azurerm_cosmosdb_sql_role_assignment` granting the API UAMI the `Cosmos DB Built-in Data Contributor` role at the database scope

After apply, the Cosmos account is reachable at `https://<account-name>.documents.azure.com:443/`. **No keys are required and no keys are exposed.** The Container App talks to Cosmos via AAD using its UAMI (inherited from 003).

### B.2 Verify the secret-free posture

```bash
gitleaks detect --source . --no-git --redact
# Expected: no findings.

az cosmosdb keys list \
  --name <account-name> \
  --resource-group bt-dev-rg
# These keys exist but are NEVER used by the platform.
# Their presence is unavoidable — Cosmos auto-rotates them.
# Verify they appear nowhere in our config:
grep -r "<account-name>.*key" iac/ api/ web/ docs/ || echo "No key references found."
```

**Maps to SC-011** (no secrets in any persisted document — automated denylist scan in the integration tier covers documents; this manual check covers configuration).

### B.3 Load the fixture set into dev Cosmos

```bash
dotnet run --project tools/load-fixtures -- \
  --endpoint https://<account-name>.documents.azure.com:443/ \
  --auth aad \
  --fixtures api/BusTerminal.Api/Fixtures/canonical-fixtures.json
```

`--auth aad` uses `DefaultAzureCredential`. Locally that resolves to your `az login` identity. The dev environment grants your identity the same `Cosmos DB Built-in Data Contributor` role on the canonical database (per `iac/environments/dev/main.tf` developer-access block).

### B.4 Confirm end-to-end

Query Cosmos directly to confirm:

```bash
az cosmosdb sql query \
  --account-name <account-name> \
  --resource-group bt-dev-rg \
  --database-name busterminal-canonical \
  --container-name resources \
  --query-text "SELECT VALUE COUNT(1) FROM c WHERE c.resourceType = 'queue'"
# Expected: 1 (the fixture set includes one queue)
```

```bash
az cosmosdb sql query \
  --account-name <account-name> \
  --resource-group bt-dev-rg \
  --database-name busterminal-canonical \
  --container-name change-events \
  --query-text "SELECT VALUE COUNT(1) FROM c"
# Expected: at least 30 (one Created event per loaded fixture)
```

**Maps to SC-001, SC-012.**

---

## Smoke validation — manual operator runbook

Once the fixture set is loaded (via Path A or Path B), exercise these scenarios manually using the load-fixtures CLI's verb-style subcommands:

### Smoke 1 — ownership lookup

```bash
dotnet run --project tools/load-fixtures -- show-owner \
  --endpoint <endpoint> --auth <aad|emulator-key> \
  --resource-id <queue-id-from-fixture>
```

Expected: structured owner block with team id, technical contact, business contact, escalation, support, operational tier. **SC-002.**

### Smoke 2 — relationship traversal

```bash
dotnet run --project tools/load-fixtures -- traverse \
  --endpoint <endpoint> --auth <aad|emulator-key> \
  --from <producer-application-id> \
  --max-hops 4
```

Expected: a typed, directional, multi-hop traversal from the producer app through the topic and subscriptions to the consumer apps. **SC-003.**

### Smoke 3 — lifecycle transition

```bash
# legal: Active → Deprecated
dotnet run --project tools/load-fixtures -- transition \
  --endpoint <endpoint> --auth <aad|emulator-key> \
  --resource-id <topic-id> --to deprecated

# illegal: Active → Draft
dotnet run --project tools/load-fixtures -- transition \
  --endpoint <endpoint> --auth <aad|emulator-key> \
  --resource-id <topic-id> --to draft
# Expected: rejection with structured illegal-transition error
```

**SC-004.**

### Smoke 4 — soft-delete + restore

```bash
dotnet run --project tools/load-fixtures -- soft-delete \
  --endpoint <endpoint> --auth <aad|emulator-key> \
  --resource-id <topic-id>

dotnet run --project tools/load-fixtures -- show \
  --endpoint <endpoint> --auth <aad|emulator-key> \
  --resource-id <topic-id> --include-deleted
# Expected: returns the resource with isDeleted=true; relationships pointing at it still resolve

dotnet run --project tools/load-fixtures -- restore \
  --endpoint <endpoint> --auth <aad|emulator-key> \
  --resource-id <topic-id>
# Expected: resource returns to its prior lifecycle state
```

**SC-005.**

### Smoke 5 — extension round-trip

```bash
dotnet run --project tools/load-fixtures -- show \
  --endpoint <endpoint> --auth <aad|emulator-key> \
  --resource-id <queue-id> --format yaml
# Expected: the contoso:costCenter extension and the __indexable map survive round-trip in YAML
```

**SC-006.**

### Smoke 6 — environment multi-association

```bash
dotnet run --project tools/load-fixtures -- show \
  --endpoint <endpoint> --auth <aad|emulator-key> \
  --resource-id <queue-id>
# Expected: a single document with environments=[development, test, qa, staging, production, disasterRecovery]
```

**SC-007.**

### Smoke 7 — export and re-import

```bash
dotnet run --project tools/load-fixtures -- export \
  --endpoint <endpoint> --auth <aad|emulator-key> \
  --output /tmp/export.json

# wipe & re-load
dotnet run --project tools/load-fixtures -- truncate \
  --endpoint <endpoint> --auth <aad|emulator-key>

dotnet run --project tools/load-fixtures -- import \
  --endpoint <endpoint> --auth <aad|emulator-key> \
  --input /tmp/export.json --conflict-resolution reject
# Expected: identifiers, relationships, version lineage, ownership, lifecycle, extensions preserved byte-equivalently
```

**SC-009.**

### Smoke 8 — change-event log query

```bash
dotnet run --project tools/load-fixtures -- changelog \
  --endpoint <endpoint> --auth <aad|emulator-key> \
  --resource-id <queue-id-that-was-mutated>
# Expected: ordered sequence of events (Created → Updated → LifecycleTransitioned → SoftDeleted → Restored)
# with actor, timestamp, source system, concurrency tokens, and snapshot/diff per event
```

**SC-012.**

---

## Cleanup

### Path A — local emulator

```bash
docker compose down cosmos-emulator
```

### Path B — dev environment

The Cosmos account and containers persist. To wipe data without destroying infrastructure:

```bash
dotnet run --project tools/load-fixtures -- truncate \
  --endpoint <endpoint> --auth aad
```

To tear down infrastructure (rare — typically only when rebuilding the dev environment):

```bash
cd iac/environments/dev
tofu destroy -target=module.cosmos_canonical_store -target=module.cosmos_account
```

---

## Troubleshooting

| Symptom | Likely cause | Resolution |
|---|---|---|
| `401 Unauthorized` from Cosmos in dev environment | Your `az login` identity lacks the data-contributor role. | Confirm `iac/environments/dev/main.tf` includes a developer-access role assignment for your identity, or ask the platform owner to add one. |
| `412 Precondition Failed` on every write | You're holding a stale `ConcurrencyToken` and not retrying. | The Cosmos SDK's `IfMatch` semantics map to our `ConcurrencyConflictException` — re-read the resource and re-submit. (FR-025 / Q2.) |
| Fixture load reports unexpected validation errors | A schema change landed without updating the fixture file. | Run `dotnet test --filter Name~Schema` and review the diff; fix either the fixture or the schema. |
| Cosmos emulator container fails to start on macOS | Insufficient memory allocated to Docker/OrbStack. | Allocate at least 4 GB; the emulator is memory-hungry. |
| `Could not load file or assembly Microsoft.Azure.Cosmos` | Package wasn't restored. | `dotnet restore api/BusTerminal.Api`. |
| `Unknown resource type 'foo'` in logs | Persisted document has a `resourceType` not in the known registry. | Expected if a future slice extended the registry and was reverted, or if test fixtures include unknown-type placeholders. Per Q4, this is non-fatal and emits an Info finding. |

---

## What this slice does NOT exercise

- No REST API endpoints exist yet. API specs are downstream consumers.
- No UI exists yet. The UI exploration of the canonical model is a later slice.
- No search index is populated. The model's search-projection alignment (FR-014) is exercised by future search slices.
- No live broker connectivity. Fixture data is operator-authored, not synchronized from a live Azure Service Bus.

These are explicit non-goals per the spec and the plan's Constitution Check.
