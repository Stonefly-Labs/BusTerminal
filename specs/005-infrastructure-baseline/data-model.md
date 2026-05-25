# Phase 1 — Data Model: Infrastructure Baseline

**Feature**: `005-infrastructure-baseline` | **Date**: 2026-05-25 | **Spec**: [`spec.md`](./spec.md) | **Plan**: [`plan.md`](./plan.md) | **Research**: [`research.md`](./research.md)

For an infrastructure spec, "data model" captures two things:
1. The **per-environment configuration profile** — the input schema operators populate to stand up an environment.
2. The **resource topology graph** — the entities produced by an apply and the relationships between them.

These are the contract surfaces every downstream module composition and operator workflow consumes. Both are language-agnostic and serializable; the OpenTofu module input/output schemas in `contracts/module-contracts.md` are the executable refinement of this data model.

---

## 1. Configuration Profile (per environment)

Every environment produces its resource set by populating one Configuration Profile. The profile is HCL today (env composition `terraform.tfvars`) but the shape is language-independent.

### 1.1 Fields

| Field | Type | Required? | Default | Validation |
|---|---|---|---|---|
| `environment_name` | string | yes | n/a | one of `dev`, `test`, `prod` |
| `subscription_id` | string (uuid) | yes | n/a | well-formed UUID |
| `location` | string | yes | n/a | a supported Azure region (e.g., `eastus2`, `centralus`) |
| `naming_prefix` | string | yes | n/a | matches regex `^bt-[a-z0-9]{2,8}$` (e.g., `bt-dev`, `bt-test`, `bt-prod`) |
| `unique_suffix` | string | yes | n/a | 4–12 lowercase alphanumeric, used for globally-unique names (KV, ACR, Cosmos, storage) |
| `tags` | map(string) | no | `{}` | merged onto the slice's mandatory tag set |
| `network_address_space` | list(string) | yes | n/a | one or more CIDR blocks; per research §10, defaults: dev=`10.50.0.0/16`, test=`10.51.0.0/16`, prod=`10.52.0.0/16` |
| `subnet_integration_cidr` | string | yes | n/a | `/23` minimum (Container Apps Environment requirement) inside `network_address_space` |
| `subnet_private_endpoints_cidr` | string | yes | n/a | `/24` recommended; inside `network_address_space` |
| `data_services_public_access_enabled` | bool | yes | `false` (dev override: `true` until retrofit) | When `false`, all data services have `public_network_access_enabled = false` |
| `private_endpoints_enabled` | bool | yes | `true` (every env, including dev — "warm in dev") | When `false`, skips PE provisioning entirely (test for the "skip PEs" alternative path) |
| `ai_search_sku` | string | yes | `basic` for dev, `standard` for test/prod | one of `free`, `basic`, `standard`, `standard2`, `standard3` |
| `service_bus_sku` | string | yes | `Standard` for dev, `Premium` for test/prod | one of `Basic`, `Standard`, `Premium` |
| `service_bus_capacity` | number | required when sku=`Premium` | n/a | one of 1, 2, 4, 8, 16 (messaging units) |
| `cosmos_offer_type` | string | yes | `Standard` | one of `Standard` |
| `key_vault_purge_protection_enabled` | bool | yes | `false` for dev, `true` for test/prod | enabled in prod per FR-019 |
| `key_vault_soft_delete_retention_days` | number | yes | `7` for dev, `90` for prod | `7`–`90` |
| `log_analytics_retention_days` | number | yes | `30` everywhere per Q5c | `30`–`730` (Azure constraint) |
| `frontend_image` | string | yes | n/a | fully-qualified container image |
| `backend_image` | string | yes | n/a | fully-qualified container image |
| `entra_tenant_id` | string (uuid) | yes | n/a | the Entra tenant ID |
| `entra_api_client_id` | string (uuid) | yes | n/a | backend API app registration client ID |
| `entra_web_client_id` | string (uuid) | yes | n/a | frontend SPA app registration client ID |
| `kv_operator_object_ids` | list(string) | no | `[]` | Entra object IDs that get standing `Key Vault Secrets Officer` access |
| `github_org_repo` | string | yes | n/a | `<org>/<repo>` for federated-credential subject |
| `platform_role_ids` | object | yes | n/a | UUIDs for the four BusTerminal app roles (admin, operator, reader, developer) |
| `probe_job_enabled` | bool | no | `false` | toggle for the internal-caller probe (existing) |
| `canonical_db_name` | string | no | `busterminal-canonical` | Cosmos SQL database name |

### 1.2 Per-environment derived values

These are NOT operator-supplied; they are derived deterministically from the profile inputs:

- `resource_group_name` = `"rg-${naming_prefix}"`
- `log_analytics_workspace_name` = `"log-${naming_prefix}"`
- `application_insights_name` = `"appi-${naming_prefix}"`
- `key_vault_name` = `"kv-${naming_prefix}-${unique_suffix}"`
- `container_registry_name` = `"acr${naming_prefix}${unique_suffix}"` (hyphens stripped)
- `container_apps_env_name` = `"cae-${naming_prefix}"`
- `cosmos_account_name` = `"cosmos-${naming_prefix}-${unique_suffix}"`
- `ai_search_name` = `"srch-${naming_prefix}-${unique_suffix}"`
- `service_bus_name` = `"sbns-${naming_prefix}-${unique_suffix}"`
- `vnet_name` = `"vnet-${naming_prefix}"`
- `workload_uami_name` = `"mi-${naming_prefix}-workload"`
- `pipeline_uami_name` (in bootstrap stack) = `"mi-busterminal-pipeline-${environment_name}"`
- `mandatory_tags` = `{ application = "BusTerminal", environment = environment_name, managed-by = "opentofu", cost-center = "platform", owner = "platform-team" }` merged with operator-supplied `tags`

### 1.3 Configuration relationships

- `naming_prefix` AND `unique_suffix` together must yield names that pass Azure's per-resource-type uniqueness + character-class rules. The `naming` module enforces this at plan time.
- `subnet_integration_cidr` AND `subnet_private_endpoints_cidr` must both be inside `network_address_space` AND must not overlap. The `networking` module enforces with a `precondition`.
- `service_bus_sku = "Premium"` is required for `private_endpoints_enabled = true` to provision a Service Bus PE. The `service-bus` module enforces with a precondition.

---

## 2. Resource Topology (per environment)

The resource graph an apply produces. Edges are typed by Azure provider semantics (parent-child, dependency, role-assignment, PE-target).

```
ResourceGroup (rg-<naming_prefix>)
├── VirtualNetwork (vnet-<naming_prefix>)
│   ├── Subnet (snet-cae-integration)               [delegated to Microsoft.App/environments; UNUSED in dev pending retrofit]
│   └── Subnet (snet-private-endpoints)
├── PrivateDnsZone (privatelink.vaultcore.azure.net)         ← linked to vnet
├── PrivateDnsZone (privatelink.documents.azure.com)         ← linked to vnet
├── PrivateDnsZone (privatelink.search.windows.net)          ← linked to vnet
├── PrivateDnsZone (privatelink.servicebus.windows.net)      ← linked to vnet (Premium-only; provisioned in dev anyway for future Premium upgrade)
├── PrivateDnsZone (privatelink.azurecr.io)                  ← linked to vnet (PE not provisioned in dev)
│
├── LogAnalyticsWorkspace (log-<naming_prefix>)
│   └── ApplicationInsights (appi-<naming_prefix>)           [workspace-bound; local_authentication_disabled = false]
│
├── KeyVault (kv-<naming_prefix>-<unique_suffix>)
│   ├── RoleAssignment: workload UAMI → Key Vault Secrets User (scope: vault)
│   ├── RoleAssignment: pipeline UAMI → Key Vault Secrets Officer (scope: env RG)
│   ├── RoleAssignment[s]: <each kv_operator_object_id> → Key Vault Secrets Officer (scope: vault)
│   ├── Secret: ApplicationInsightsConnectionString          [content: appi.connection_string; never an output]
│   ├── PrivateEndpoint (warm in dev; load-bearing in test/prod)
│   │   └── PrivateDnsRecord in privatelink.vaultcore.azure.net
│   └── DiagnosticSetting (allLogs → LAW)
│
├── ContainerRegistry (acr<naming_prefix><unique_suffix>)    [existing, adopted]
│   ├── RoleAssignment: workload UAMI → AcrPull (scope: registry)
│   ├── PrivateEndpoint (test/prod template only — not in dev)
│   └── DiagnosticSetting (allLogs → LAW)
│
├── CosmosDbAccount (cosmos-<naming_prefix>-<unique_suffix>) [existing, adopted; AAD-only data plane]
│   ├── SqlDatabase (canonical_db_name)
│   │   └── (containers — owned by cosmos-canonical-store module per spec 004)
│   ├── CosmosSqlRoleAssignment: workload UAMI → Cosmos DB Built-in Data Contributor (scope: database)
│   ├── CosmosSqlRoleAssignment: developer/pipeline principal → Cosmos DB Built-in Data Contributor (scope: database)
│   ├── PrivateEndpoint (warm in dev; load-bearing in test/prod)
│   │   └── PrivateDnsRecord in privatelink.documents.azure.com
│   └── DiagnosticSetting (allLogs → LAW)
│
├── AiSearchService (srch-<naming_prefix>-<unique_suffix>)
│   ├── RoleAssignment: workload UAMI → Search Index Data Contributor (scope: service)
│   ├── PrivateEndpoint (warm in dev; load-bearing in test/prod)
│   │   └── PrivateDnsRecord in privatelink.search.windows.net
│   └── DiagnosticSetting (allLogs → LAW)
│
├── ServiceBusNamespace (sbns-<naming_prefix>-<unique_suffix>)
│   ├── RoleAssignment: workload UAMI → Azure Service Bus Data Sender (scope: namespace)
│   ├── RoleAssignment: workload UAMI → Azure Service Bus Data Receiver (scope: namespace)
│   ├── PrivateEndpoint (Premium SKU only; NOT in dev; in test/prod template)
│   │   └── PrivateDnsRecord in privatelink.servicebus.windows.net
│   └── DiagnosticSetting (allLogs → LAW)
│
├── ContainerAppsEnvironment (cae-<naming_prefix>)           [existing, adopted; NOT VNet-integrated in this slice]
│   └── DiagnosticSetting (allLogs → LAW)                    [existing]
│
├── ContainerApp (ca-<naming_prefix>-api)                    [existing, adopted]
│   ├── ManagedIdentity ref: workload UAMI
│   ├── ContainerAppSecret: appinsights-connection-string → KV secret URI
│   ├── EnvVar: APPLICATIONINSIGHTS_CONNECTION_STRING ← secret ref
│   ├── EnvVar: APPLICATIONINSIGHTS_AUTHENTICATION_STRING = "Authorization=AAD;ClientId=${workload UAMI client_id}"  [NEW per research §6]
│   └── DiagnosticSetting (allLogs → LAW)                    [REMOVE the AllMetrics block that exists today per Q5c]
│
├── ContainerApp (ca-<naming_prefix>-web)                    [existing, adopted]
│   ├── ManagedIdentity ref: workload UAMI
│   ├── ContainerAppSecret: appinsights-connection-string → KV secret URI
│   ├── EnvVar: NEXT_PUBLIC_APPINSIGHTS_CONNECTION_STRING ← secret ref  [browser ingestion; local auth on AppInsights stays enabled]
│   └── DiagnosticSetting (allLogs → LAW)                    [REMOVE AllMetrics per Q5c]
│
├── UserAssignedIdentity (mi-<naming_prefix>-workload)       [existing, adopted; new role assignments added]
│   ├── RoleAssignment: → Monitoring Metrics Publisher (scope: appi-<naming_prefix>)  [NEW per research §6]
│   └── (all the data-plane role grants enumerated above)
│
└── AppRegistrationRoles + GraphPermissions + WorkloadFederation + (optional) ProbeJob  [existing, adopted unchanged]
```

### 2.1 Cross-stack edges (env composition → platform-bootstrap)

- `pipeline UAMI (in mi-busterminal-pipeline-<env>)` → `Subscription` Contributor (subscription scope; documented Complexity-Tracking exception)
- `pipeline UAMI` → `Role Based Access Control Administrator` (subscription scope; condition-scoped to the role GUID allowlist enumerated in research §12)
- `pipeline UAMI` → `Storage Blob Data Contributor` (scope: tfstate storage account)
- `pipeline UAMI` ⇋ `GitHub OIDC` (federated credential subject: `repo:${github_org_repo}:environment:${env}`)

These edges are defined in `iac/platform-bootstrap/` and are not re-created per env composition; they cross-reference via Azure resource IDs only.

### 2.2 Edges removed in this slice

- `ContainerApp (api) → AllMetrics diagnostic-setting` — removed per Q5c
- `ContainerApp (web) → AllMetrics diagnostic-setting` — removed per Q5c

### 2.3 Edges deliberately NOT created

- `ContainerAppsEnvironment → snet-cae-integration` — deferred per Q2c selective retrofit
- `ContainerApp (api) (web) → KV via PE` — workloads continue to resolve KV via public endpoint until retrofit
- `ContainerApp (api) → Cosmos via PE`, `→ AI Search via PE`, `→ Service Bus via PE` — same reason
- `Workload UAMI → Cosmos DB Owner` / `Search Service Contributor` / `Service Bus Namespace Owner` / `Key Vault Administrator` — explicitly forbidden by FR-033

---

## 3. Lifecycle and replacement semantics

The graph above is the *steady state*. The following resources MUST NOT be destroyed/recreated by any apply against an existing healthy environment (FR-045, US7, SC-009):

| Resource | Reason |
|---|---|
| `azurerm_resource_group.this` | All env state lives in it |
| `azurerm_log_analytics_workspace.this` | Holds all forwarded diagnostics; retention reset on recreate |
| `azurerm_application_insights.this` | Holds workspace-bound telemetry config |
| `azurerm_key_vault.this` | Holds secrets; purge-protection prevents quick recreate in prod |
| `azurerm_key_vault_secret.app_insights_connection_string` | Replacing the KV secret breaks the Container Apps secret reference (Container Apps caches the reference at startup) |
| `azurerm_container_registry.this` | Holds images |
| `azurerm_cosmosdb_account.this` | Holds the canonical store data |
| `azurerm_cosmosdb_sql_database.canonical` | Holds the canonical store |
| `azurerm_user_assigned_identity.workload` | Replacing breaks every role assignment + the Container Apps managed-identity binding |
| `azurerm_container_app_environment.this` | Replacing breaks every Container App + their FQDNs (Entra app-registration redirect URIs!) |
| Anything in `iac/platform-bootstrap/` | Replacing the tfstate storage account or pipeline MIs is a separate incident |

Net-new resources in this slice (VNet, subnets, DNS zones, PEs, AI Search, Service Bus, new role assignments) are created fresh and have no prior-state collisions.

---

## 4. State management

- **Backend**: `azurerm` (existing storage account `btstatech0001` in RG `rg-busterminal-tfstate`)
- **Container**: `tfstate`
- **Key per env**: `envs/<env>/terraform.tfstate` (dev key `envs/dev/terraform.tfstate` already in use)
- **Access**: only the pipeline UAMIs (per env) and a small set of human operators (via `Storage Blob Data Contributor` on the storage account) — set in bootstrap, not re-asserted per env composition
- **Locking**: Azure storage blob lease (default azurerm backend behavior)
- **Versioning**: enabled on the storage account (`versioning_enabled = true` per `iac/platform-bootstrap/main.tf`); 30-day soft-delete retention. Recoverable from accidental state corruption.

---

## 5. Validation rules summarized

(Mirror of the Functional Requirements but as a single reference for downstream module-contract authoring.)

- Every taggable resource carries the mandatory tag set (FR-037).
- Every supported platform resource has a diagnostic setting forwarding `allLogs` to the env LAW (FR-027, Q5c).
- No `enabled_metric` block on any diagnostic setting (Q5c).
- Public network access on data services follows the env's `data_services_public_access_enabled` toggle; default is `false` (FR-031).
- Workload UAMI receives only the roles enumerated in FR-033; pipeline UAMI is condition-scoped to its allowlist.
- No secret value appears in OpenTofu outputs (FR-036).
- KV secrets have an explicit `expiration_date` (per existing CKV_AZURE_41 convention; far-future for the App Insights connection string per existing comment).
- Network CIDR ranges don't overlap and integration subnet is `/23` minimum.
- Per-env state backends do not cross-reference each other.
