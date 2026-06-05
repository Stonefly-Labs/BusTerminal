# Spec 006 — Perf Validation Results (T128)

**Status**: ⏳ Pending operator capture. Fill in the rows below after `tofu apply` and a smoke-traffic run against the dev environment.

This file is the `T128` artifact — the recorded SC-002/SC-003/SC-004/SC-005 p95 metrics captured from App Insights against the dev environment under representative load (per spec.md §Assumptions: "a few hundred concurrent operators and registry sizes in the tens of thousands of entities per environment").

Capture each metric **after** the registry has been populated with the quickstart §5 seed data and at least one smoke-traffic pass (CRUD + browse + search + audit) has been driven through the UI and API. Use the App Insights dashboards (or `kusto` queries below) to read the p95 line.

## Budgets

| Metric | Budget | Source |
|---|---|---|
| Search request p95 | < 1s | SC-002 / FR-043 |
| Detail page load p95 | < 500ms | SC-003 / FR-044 |
| CRUD API p95 | < 1s | SC-004 / FR-045 |
| Indexer Cosmos→AI Search lag p95 | < 5s | SC-005 / FR-025 |

## Capture template

| Metric | Date captured | p95 (ms) | Window | Result |
|---|---|---|---|---|
| Search p95 | YYYY-MM-DD | — | last 24h | ⏳ |
| Detail p95 | YYYY-MM-DD | — | last 24h | ⏳ |
| CRUD p95 | YYYY-MM-DD | — | last 24h | ⏳ |
| Index lag p95 | YYYY-MM-DD | — | last 24h | ⏳ |

Result column legend: ✅ within budget · ⚠️ within budget but >80% · ❌ over budget.

## Suggested KQL queries

Run these against the BusTerminal dev Application Insights resource.

### Search p95 (SC-002)

```kusto
requests
| where cloud_RoleName == "BusTerminal.Api"
| where name endswith "/api/registry/search"
| where timestamp > ago(24h)
| summarize p95 = percentile(duration, 95)
```

### Detail p95 (SC-003)

```kusto
requests
| where cloud_RoleName == "BusTerminal.Api"
| where name matches regex @"^GET /api/registry/[^/]+$"
| where timestamp > ago(24h)
| summarize p95 = percentile(duration, 95)
```

### CRUD p95 (SC-004)

```kusto
requests
| where cloud_RoleName == "BusTerminal.Api"
| where name has "/api/registry"
| where name in ("POST /api/registry", "PUT /api/registry/{id}", "DELETE /api/registry/{id}")
| where timestamp > ago(24h)
| summarize p95 = percentile(duration, 95)
```

### Indexer lag p95 (SC-005)

The indexer Function emits a custom `RegistryIndexerLag` metric (per spec 006 indexer telemetry — emitted from `RegistryEntityIndexer` on every successful upsert with the delta between the source document's `updatedAtUtc` and the AI Search write completion).

```kusto
customMetrics
| where cloud_RoleName == "busterminal-indexer"
| where name == "RegistryIndexerLag"
| where timestamp > ago(24h)
| summarize p95 = percentile(value, 95)
```

## Notes

- A short capture window (e.g. last 24h) is acceptable for the v1 milestone; future ops-hardening can extend to a rolling 7-day window.
- If a metric is over budget, do **not** silently bump the budget. Open an issue, attribute the regression, then either tune the implementation or amend the spec with a documented exception.
