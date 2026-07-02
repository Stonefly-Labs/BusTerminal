# Dev environment parking (cost control)

The dev environment can be **parked** overnight/weekends so it stops billing
for resources that charge 24×7 even when idle, and **unparked** in a single
step when development resumes. Parking is dev-only — the `parked` variable is
not surfaced in the test/prod templates.

## Why parking instead of scale-down

| Resource | 24×7 behaviour | Parked |
| --- | --- | --- |
| AI Search (Basic, westus3) | ~$75/mo; Azure has **no pause** — deletion is the documented way to stop billing | **Destroyed** (service + `registry-entities-v1` index) |
| Service Bus namespace (Standard) | ~$10/mo base charge while it exists | **Destroyed** (namespace + `discovery-requested` queue + RBAC + diagnostics) |
| Indexer Container App | `min_replicas = 1` keeps one replica warm for the change feed | **Scaled to 0** |
| Backend / frontend apps | already scale to zero | unchanged |
| Cosmos DB (serverless), Key Vault, ACR, LAW/App Insights, CAE, UAMI + role assignments, storage, VNet | ~free when idle and/or stateful | **never parked** |

Both destroyed resources are safe to lose: the search index is a projection of
the canonical Cosmos store, and the discovery queue holds only re-triggerable
requests. Neither type is on the BT-IAC-007 stateful-destroy list, so the
policy gate passes a park plan cleanly.

## How the index survives

The park flow clears the `registry-entities-leases` container
(`iac/scripts/clear-indexer-leases.sh`) after the indexer is scaled to zero.
The indexer's `CosmosDBTrigger` has `StartFromBeginning = true`, so on unpark
it replays the entire change feed and re-projects every registry entity into
the freshly created (empty) index. No manual reindex step.

## Commands

Park / unpark from anywhere:

```bash
gh workflow run env-park-dev.yml -f mode=park
gh workflow run env-park-dev.yml -f mode=unpark
```

Or locally (plan + policy gate + prompt, like any apply):

```bash
./iac/scripts/apply-env.sh --env dev --park
./iac/scripts/apply-env.sh --env dev --unpark
# --park does NOT clear leases for you locally — follow with:
./iac/scripts/clear-indexer-leases.sh --env dev
```

A **nightly cron (04:00 UTC) parks automatically** as a forget-proof safety
net; it no-ops when already parked. Edit the `schedule` block in
`.github/workflows/env-park-dev.yml` to change the hour.

## Parked-state stickiness

`parked` defaults to `false`, but every pipeline that applies dev state
(`cd-dev.yml`, `iac-apply-dev.yml`, `iac-validate.yml`, `apply-env.sh`) reads
the live `parked` output back from state first — so merging to `main`
overnight does **not** unpark the environment. Only the park workflow and the
`--park/--unpark` flags transition the posture.

## Unpark expectations

- **~5–10 minutes** for the apply (AI Search Basic provisioning dominates).
- **A few more minutes of RBAC propagation** on the fresh resources: the
  workload UAMI's `Search Index Data Contributor` and Service Bus
  Sender/Receiver grants are recreated with the services, and Azure role
  propagation is eventually consistent. Early requests may 403; the indexer
  and API retry through it.
- The index backfill runs as fast as the change feed drains — typically well
  under a minute at current registry sizes.
- **westus3 capacity roulette:** AI Search lives in westus3 because eastus2
  was out of Basic capacity (2026-05-31). Each unpark re-requests capacity;
  if westus3 ever rejects with `InsufficientResourcesAvailable`, override
  `ai_search_location` in tfvars and unpark again.

## While parked

- Discovery requests fail (no Service Bus) — the publisher throws and the
  coalescer contains it (PR #116/#120); no wedged runs.
- Search-backed pages return errors (no index).
- The backend/frontend still wake on request for everything Cosmos-backed.
- Parked burn rate is a few dollars/month (ACR + Cosmos/LAW storage, pennies
  of everything else).
