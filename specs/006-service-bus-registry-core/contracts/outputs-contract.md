# Incremental IaC Outputs Contract — Spec 006

**Feature**: `006-service-bus-registry-core` | **Date**: 2026-06-02

This document records the IaC outputs introduced or extended by spec 006. The spec-005 [`outputs-contract.md`](../../005-infrastructure-baseline/contracts/outputs-contract.md) remains the authoritative surface for everything it covers; this document is **additive only**.

**Rule**: no output may contain a secret value (consistent with spec-005 FR-036).

---

## New outputs added by `iac/environments/dev/main.tf`

### Registry Cosmos containers

| Output | Type | Sensitive | Notes |
|---|---|---|---|
| `cosmos_registry_entities_container_name` | string | no | `"registry-entities"` echo. |
| `cosmos_registry_entities_container_id`   | string | no | Full container resource id. |
| `cosmos_registry_audit_container_name`    | string | no | `"registry-audit"` echo. |
| `cosmos_registry_audit_container_id`      | string | no | |
| `cosmos_registry_leases_container_name`   | string | no | `"registry-entities-leases"` echo. |
| `cosmos_registry_leases_container_id`     | string | no | |

### AI Search index

| Output | Type | Sensitive | Notes |
|---|---|---|---|
| `ai_search_registry_index_name` | string | no | `"registry-entities-v1"`. Used by API and indexer to bind the search client. |
| `ai_search_registry_index_id`   | string | no | Full azapi-managed resource id. |

### Indexer Function container

| Output | Type | Sensitive | Notes |
|---|---|---|---|
| `indexer_container_app_id`    | string | no | `azurerm_container_app` id for the `kind=functionapp` container. |
| `indexer_container_app_fqdn`  | string | no | Internal FQDN on the CAE default domain. No public ingress is configured in this slice (the indexer has no inbound HTTP surface; CAE health probes use the CAE-internal address). |
| `indexer_container_app_name`  | string | no | Resource name for Azure CLI follow-up commands. |

---

## Outputs consumed (not redeclared) from spec 005

These outputs from spec 005 are inputs to the spec-006 modules; their definitions remain in spec 005 and are referenced by id.

| Spec-005 output | Consumed by | Purpose |
|---|---|---|
| `cosmos_account_id` | `cosmos-registry-store`, `functions-container-app` | Container creation; indexer env var (`Cosmos__accountEndpoint`) |
| `cosmos_account_endpoint` | API (env var), indexer (env var) | SDK endpoint binding |
| `cosmos_canonical_database_name` | `cosmos-registry-store` | Target database for new containers |
| `ai_search_id` | `ai-search-index`, RBAC role assignments | Index parent scope; role-assignment scope |
| `ai_search_endpoint` | API (env var), indexer (env var) | SDK endpoint binding |
| `container_apps_environment_id` | `functions-container-app` | Hosting target |
| `container_registry_login_server` | `functions-container-app` | Image pull |
| `workload_uami_id` | `functions-container-app`, RBAC role assignments | Identity binding + role assignment principal |
| `workload_uami_client_id` | `functions-container-app` (env var) | `Cosmos__clientId` for identity-based change-feed binding |
| `application_insights_id` | `functions-container-app` | App Insights binding |
| `application_insights_connection_string_secret_uri` | `functions-container-app` | Hybrid AI ingestion (browser-mode connection string for OTel SDK init); same pattern as spec-005 Q1c |
| `log_analytics_workspace_id` | `iac/modules/diagnostic-settings` | LAW destination for diagnostics on the new container app |

---

## RBAC additions

| Identity | Role | Scope | Source |
|---|---|---|---|
| `workload_uami_principal_id` | `Search Index Data Reader` | `ai_search_id` (service scope) | NEW. Allowlisted by spec-005 FR-033. |
| `workload_uami_principal_id` | `Search Index Data Contributor` | `ai_search_id` (service scope) | NEW. Allowlisted by spec-005 FR-033. Granted to enable indexer ingestion. (The API uses Reader scope; the indexer uses Contributor. They share the UAMI — granting both at the service scope is acceptable per least privilege in this slice; future RBAC tightening can split identities.) |

No new role GUID is added to the pipeline-MI RBAC-Admin allowlist condition; both GUIDs are already permitted (per [[project_spec005_bootstrap_gate]] memory: bootstrap gate cleared 2026-05-26).

---

## Diagnostic settings

The new Functions container app (`indexer_container_app_id`) gets a per-resource diagnostic setting via `iac/modules/diagnostic-settings`, conforming to the spec-005 BT-IAC-003 `allLogs`-only convention.

The new Cosmos containers and the new AI Search index do NOT need per-resource diagnostic settings — the Cosmos account and the AI Search service already forward `allLogs` to LAW (spec 005).

---

## CI policy gate coverage

All seven BT-IAC-001..007 rules from spec 005 apply to the new modules and resources without allowlist additions:

- BT-IAC-001 (UAMI role assignment allowlist) — green; both new role GUIDs already on the allowlist.
- BT-IAC-002 (private-by-default for prod) — dev composition keeps `data_services_public_access_enabled = true` (per spec-005 Q2c); the test/prod templates inherit private-by-default.
- BT-IAC-003 (`allLogs`-only diagnostics) — green; new container app uses the wrapper.
- BT-IAC-004 (no inline IAM in env compositions) — green; role assignments declared in `iac/environments/dev/main.tf` via the existing `workload-identity` extension pattern (not inline `azurerm_role_assignment`).
- BT-IAC-005 (no inline credentials) — green; managed identity end-to-end.
- BT-IAC-006 (lockfile drift) — green on next CI run; new providers (`Azure/azapi`) added to `versions.tf`.
- BT-IAC-007 (stateful-destroy manual approval) — green; new Cosmos containers added with `lifecycle { prevent_destroy = true }` consistent with spec-005's stateful-resource convention.
