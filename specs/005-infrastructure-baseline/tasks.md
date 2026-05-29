# Tasks: Infrastructure Baseline

**Input**: Design documents from `/specs/005-infrastructure-baseline/`

**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, contracts/ ✅, quickstart.md ✅

**Tests**: NOT requested by this spec. Validation is via `tofu validate`, `tofu plan`, `checkov`, `tfsec`, and the custom policy-gate scripts (`iac/policies/*.sh`) — implemented under User Story 6. No unit/integration test tasks are generated.

**Organization**: Tasks are grouped by user story so each story can be implemented, validated, and (where applicable) applied independently.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (US1–US7)
- All file paths are absolute-from-repo-root (e.g., `iac/modules/naming/main.tf`)

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Repo-level scaffolding required by every later task.

- [X] T001 Create directory `iac/modules/naming/` with empty `main.tf`, `variables.tf`, `outputs.tf`, `versions.tf`, `README.md`
- [X] T002 [P] Create directory `iac/modules/networking/` with empty `main.tf`, `variables.tf`, `outputs.tf`, `versions.tf`, `README.md`
- [X] T003 [P] Create directory `iac/modules/ai-search/` with empty `main.tf`, `variables.tf`, `outputs.tf`, `versions.tf`, `README.md`
- [X] T004 [P] Create directory `iac/modules/service-bus/` with empty `main.tf`, `variables.tf`, `outputs.tf`, `versions.tf`, `README.md`
- [X] T005 [P] Create directory `iac/modules/diagnostic-settings/` with empty `main.tf`, `variables.tf`, `outputs.tf`, `versions.tf`, `README.md`
- [X] T006 [P] Create directory `iac/modules/private-endpoint/` with empty `main.tf`, `variables.tf`, `outputs.tf`, `versions.tf`, `README.md`
- [X] T007 [P] Create directory `iac/policies/` (will hold bash policy-gate scripts in US6)
- [X] T008 [P] Create directory `iac/scripts/` (will hold the `apply-env.sh` operator helper in US6)
- [X] T009 [P] Create directory `iac/environments/test/` (will hold the test composition template in US5)
- [X] T010 [P] Create directory `iac/environments/prod/` (will hold the prod composition template in US5)
- [X] T011 Update root `.gitignore` to ignore `**/tfplan`, `**/tfplan.json`, and `iac/environments/*/.terraform/` (verify entries; add if missing)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core building blocks every user story depends on. NO user-story phase may start until Phase 2 completes.

**⚠️ CRITICAL**: T012–T028 block every later task.

### Naming module (used by every env composition)

- [X] T012 Implement `iac/modules/naming/variables.tf` declaring `environment_name`, `naming_prefix`, `unique_suffix` with the regex validations from `contracts/module-contracts.md` §`naming`
- [X] T013 Implement `iac/modules/naming/outputs.tf` emitting every derived name from `data-model.md` §1.2 (`resource_group_name`, `log_analytics_workspace_name`, `application_insights_name`, `key_vault_name`, `container_registry_name`, `container_apps_env_name`, `cosmos_account_name`, `ai_search_name`, `service_bus_name`, `vnet_name`, `workload_uami_name`)
- [X] T014 Implement `iac/modules/naming/main.tf` with `locals { ... }` computing every name (hyphens stripped for ACR per `data-model.md` §1.2)
- [X] T015 Implement `iac/modules/naming/versions.tf` pinning `terraform >= 1.11.0` and no provider requirements (naming is pure HCL)
- [X] T016 Document `iac/modules/naming/README.md` with inputs/outputs table and one usage example

### Diagnostic-settings module (used by every resource that forwards logs)

- [X] T017 Implement `iac/modules/diagnostic-settings/variables.tf` declaring `name`, `target_resource_id`, `log_analytics_workspace_id` per `contracts/module-contracts.md` §`diagnostic-settings`
- [X] T018 Implement `iac/modules/diagnostic-settings/main.tf` rendering exactly one `azurerm_monitor_diagnostic_setting` with `enabled_log { category_group = "allLogs" }` and NO `enabled_metric` block (per Q5c + `research.md` §7)
- [X] T019 Implement `iac/modules/diagnostic-settings/outputs.tf` emitting `id`
- [X] T020 Implement `iac/modules/diagnostic-settings/versions.tf` pinning `hashicorp/azurerm ~> 4.0`
- [X] T021 Document `iac/modules/diagnostic-settings/README.md` with the Q5c rationale comment and a usage example. Add a paragraph confirming FR-047 compliance: the `allLogs` category group on Azure PaaS resource diagnostics does NOT include application payloads — only resource-level operations/audit logs — so no PII leaks via diagnostic-settings by construction. Cite the per-service log-category reference (https://learn.microsoft.com/azure/azure-monitor/essentials/resource-logs-categories) for reviewers.

### Private-endpoint wrapper module (used by KV, Cosmos, AI Search, SB, ACR)

- [X] T022 Implement `iac/modules/private-endpoint/variables.tf` declaring `name`, `resource_group_name`, `location`, `subnet_id`, `target_resource_id`, `subresource_name`, `private_dns_zone_id`, `tags` per `contracts/module-contracts.md` §`private-endpoint`
- [X] T023 Implement `iac/modules/private-endpoint/main.tf` with `azurerm_private_endpoint.this` + `private_dns_zone_group` block binding the supplied zone
- [X] T024 Implement `iac/modules/private-endpoint/outputs.tf` emitting `id`, `private_ip_address` (from `network_interface[0].ip_configuration[0]`), and `fqdn` derived from target name + zone
- [X] T025 Implement `iac/modules/private-endpoint/versions.tf` pinning `hashicorp/azurerm ~> 4.0`
- [X] T026 Document `iac/modules/private-endpoint/README.md` with the per-service `subresource_name` reference table (`vault`, `Sql`, `searchService`, `namespace`, `registry`, `blob`)

### New env-composition variables (used by every later wiring task)

- [X] T027 Extend `iac/environments/dev/variables.tf` to add the 11 NEW variables from `contracts/config-profile-schema.md` (`network_address_space`, `subnet_integration_cidr`, `subnet_private_endpoints_cidr`, `data_services_public_access_enabled`, `private_endpoints_enabled`, `ai_search_sku`, `service_bus_sku`, `service_bus_capacity`, `key_vault_purge_protection_enabled`, `key_vault_soft_delete_retention_days`, `log_analytics_retention_days`) with the dev defaults specified in the schema and the validation blocks from §1.1
- [X] T028 Extend `iac/environments/dev/providers.tf` to add the three new provider requirements (`hashicorp/random ~> 3.6`, `azure/azapi ~> 2.4`, `azure/modtm ~> 0.3`) per `research.md` §13; set `enable_telemetry = false` for modtm

**Checkpoint**: Foundation ready — Phase 3+ may now begin.

---

## Phase 3: User Story 1 — Provision the complete BusTerminal platform baseline (Priority: P1) 🎯 MVP

**Goal**: A `tofu apply` against `iac/environments/dev/` (selective retrofit per Q2c) brings the dev environment to the full FR-002 / FR-005-onward topology: VNet + subnets + private DNS zones, AI Search, Service Bus namespace, Cosmos PE (warm), KV PE (warm), Search PE (warm), additional RBAC, and every documented output emitted.

**Independent Test**: From `iac/environments/dev/`, run `tofu init && tofu plan -var-file=terraform.tfvars -out=tfplan`. Plan shows ~25 adds (per `plan.md` §Scale/Scope), zero destroys against any stateful resource (per `data-model.md` §3), and zero changes outside the documented scope. After `tofu apply`, `tofu output -json` resolves every key in `contracts/outputs-contract.md`.

### Networking module (US1 — builds the VNet/subnets/DNS zones every PE consumes)

- [X] T029 [US1] Implement `iac/modules/networking/variables.tf` per `contracts/module-contracts.md` §`networking` (`vnet_name`, `resource_group_name`, `location`, `address_space`, `subnet_integration_cidr`, `subnet_private_endpoints_cidr`, `private_dns_zones`, `tags`) with the three preconditions from the contract
- [X] T030 [US1] Implement `iac/modules/networking/main.tf` calling `Azure/avm-res-network-virtualnetwork/azurerm v0.16.0` for the VNet + two subnets (CAE integration delegated to `Microsoft.App/environments`; PE subnet)
- [X] T031 [US1] Extend `iac/modules/networking/main.tf` to instantiate `Azure/avm-res-network-privatednszone/azurerm v0.4.2` once per zone in `var.private_dns_zones` via `for_each`, each linked to the new VNet
- [X] T032 [US1] Implement `iac/modules/networking/outputs.tf` emitting `vnet_id`, `subnet_integration_id`, `subnet_private_endpoints_id`, `private_dns_zone_ids` (map keyed by zone name)
- [X] T033 [US1] Implement `iac/modules/networking/versions.tf` pinning the two AVMs + `hashicorp/azurerm ~> 4.0` + the new provider trio (T028)
- [X] T034 [US1] Document `iac/modules/networking/README.md` with the dev/test/prod CIDR table from `research.md` §10

### AI Search module (US1)

- [X] T035 [US1] Implement `iac/modules/ai-search/variables.tf` per `contracts/module-contracts.md` §`ai-search` including the SKU validation rejecting `free` when public access disabled or PE set
- [X] T036 [US1] Implement `iac/modules/ai-search/main.tf` calling `Azure/avm-res-search-searchservice/azurerm v0.2.0` with system-assigned identity disabled (workload UAMI handles access via RBAC, not identity-on-search)
- [X] T037 [US1] Extend `iac/modules/ai-search/main.tf` to instantiate `module.diagnostic-settings` (T018) targeting the new search service
- [X] T038 [US1] Extend `iac/modules/ai-search/main.tf` to conditionally instantiate `module.private-endpoint` (T023) when `var.private_endpoint_subnet_id != null`, with `subresource_name = "searchService"`
- [X] T039 [US1] Extend `iac/modules/ai-search/main.tf` to emit `azurerm_role_assignment` granting `Search Index Data Contributor` (role definition GUID `8ebe5a00-799e-43f5-93ac-243d3dce84a7`) to `var.workload_principal_id` scoped to the search service
- [X] T040 [US1] Implement `iac/modules/ai-search/outputs.tf` emitting `id`, `endpoint` (`https://<name>.search.windows.net`), `private_endpoint_id`
- [X] T041 [US1] Implement `iac/modules/ai-search/versions.tf` pinning the AVM + `hashicorp/azurerm ~> 4.0`
- [X] T042 [US1] Document `iac/modules/ai-search/README.md` with the SKU table from `research.md` §4

### Service Bus module (US1)

- [X] T043 [US1] Implement `iac/modules/service-bus/variables.tf` per `contracts/module-contracts.md` §`service-bus` including the four preconditions (Basic rejected; Standard+PE→ERROR; Premium-without-capacity→ERROR)
- [X] T044 [US1] Implement `iac/modules/service-bus/main.tf` calling `Azure/avm-res-servicebus-namespace/azurerm` (pin to latest 0.x at task time and record in `iac/modules/service-bus/versions.tf`) — namespace only, NO topics/queues per FR-022
- [X] T045 [US1] Extend `iac/modules/service-bus/main.tf` to instantiate `module.diagnostic-settings` targeting the new namespace
- [X] T046 [US1] Extend `iac/modules/service-bus/main.tf` to conditionally instantiate `module.private-endpoint` when `var.private_endpoint_subnet_id != null` AND `var.sku == "Premium"`, with `subresource_name = "namespace"`
- [X] T047 [US1] Extend `iac/modules/service-bus/main.tf` to emit two `azurerm_role_assignment` blocks granting `Azure Service Bus Data Sender` (GUID `69a216fc-b8fb-44d8-bc22-1f3c2cd27a39`) and `Azure Service Bus Data Receiver` (GUID `4f6d3b9b-027b-4f4c-9142-0e5a2a2247e0`) to `var.workload_principal_id` scoped to the namespace
- [X] T048 [US1] Implement `iac/modules/service-bus/outputs.tf` emitting `id`, `name`, `fqdn` (`<name>.servicebus.windows.net`), `private_endpoint_id`
- [X] T049 [US1] Implement `iac/modules/service-bus/versions.tf` pinning the resolved AVM version + `hashicorp/azurerm ~> 4.0`
- [X] T050 [US1] Document `iac/modules/service-bus/README.md` with the dev=Standard / test+prod=Premium SKU rationale from `research.md` §3

### Extend existing modules to accept PE inputs (US1)

- [X] T051 [P] [US1] Extend `iac/modules/cosmos-account/variables.tf` adding `private_endpoint_subnet_id` (string, default null), `private_dns_zone_id` (string, default null), `public_network_access_enabled` (bool, default true) per `contracts/module-contracts.md` §`cosmos-account (extended)`
- [X] T052 [US1] Extend `iac/modules/cosmos-account/main.tf` to conditionally instantiate `module.private-endpoint` when `var.private_endpoint_subnet_id != null` with `subresource_name = "Sql"`, and propagate `public_network_access_enabled` to the existing `azurerm_cosmosdb_account` resource
- [X] T053 [US1] Extend `iac/modules/cosmos-account/outputs.tf` adding `private_endpoint_id` (null when PE disabled)
- [X] T054 [P] [US1] Extend `iac/modules/keyvault/variables.tf` adding `private_endpoint_subnet_id`, `private_dns_zone_id`, `public_network_access_enabled` per `contracts/module-contracts.md` §`keyvault (extended)`
- [X] T055 [US1] Extend `iac/modules/keyvault/main.tf` to conditionally instantiate `module.private-endpoint` when `var.private_endpoint_subnet_id != null` with `subresource_name = "vault"`, and propagate `public_network_access_enabled`
- [X] T056 [US1] Extend `iac/modules/keyvault/outputs.tf` adding `private_endpoint_id`
- [X] T057 [P] [US1] Extend `iac/modules/container-registry/variables.tf` adding `private_endpoint_subnet_id`, `private_dns_zone_id` per `contracts/module-contracts.md` §`container-registry (extended)`
- [X] T058 [US1] Extend `iac/modules/container-registry/main.tf` to conditionally instantiate `module.private-endpoint` when `var.private_endpoint_subnet_id != null` with `subresource_name = "registry"`

### Dev env composition wiring (US1)

- [X] T059 [US1] Extend `iac/environments/dev/main.tf` to instantiate `module.naming` with `environment_name`, `naming_prefix`, `unique_suffix` (consume its outputs in every downstream module call)
- [X] T060 [US1] Extend `iac/environments/dev/main.tf` to instantiate `module.networking` with the dev CIDRs from `terraform.tfvars` and the seven private DNS zones from `research.md` §11 (`privatelink.vaultcore.azure.net`, `privatelink.documents.azure.com`, `privatelink.search.windows.net`, `privatelink.servicebus.windows.net`, `privatelink.azurecr.io`)
- [X] T061 [US1] Extend `iac/environments/dev/main.tf` to instantiate `module.ai-search` passing the workload UAMI principal ID + (conditional on `var.private_endpoints_enabled`) the PE subnet and the search private DNS zone ID
- [X] T062 [US1] Extend `iac/environments/dev/main.tf` to instantiate `module.service-bus` with `sku = var.service_bus_sku`. The env composition MUST conditionally null the PE inputs based on SKU support: `private_endpoint_subnet_id = (var.service_bus_sku == "Premium" && var.private_endpoints_enabled) ? module.networking.subnet_private_endpoints_id : null` and the matching `private_dns_zone_id` likewise. Reason: the `service-bus` module precondition rejects `sku=Standard` + non-null PE inputs (per `contracts/module-contracts.md` §service-bus), so the env composition must do the SKU-aware nulling rather than passing the PE inputs unconditionally. Dev defaults to Standard, so dev gets no SB PE; test/prod templates default to Premium and DO get one.
- [X] T063 [US1] Extend `iac/environments/dev/main.tf` to wire the new PE inputs into the existing `module.cosmos_account`, `module.keyvault` calls (warm in dev per Q2c — `private_endpoints_enabled` toggles, public access stays on per `data_services_public_access_enabled`)
- [X] T064 [US1] Extend `iac/environments/dev/main.tf` to add an `import {}` block for the existing dev Container Registry adoption if not already imported, and wire its PE input to null in dev (PE deferred to test/prod template per research §2)
- [X] T065 [US1] Update `iac/environments/dev/terraform.tfvars` with the new variables' dev values (use the example from `contracts/config-profile-schema.md` §Dev as the reference)
- [X] T066 [US1] Extend `iac/environments/dev/outputs.tf` to declare every output in `contracts/outputs-contract.md` — Resource identifiers, Networking, Compute, Data services, Secrets, Container Registry, Observability, Identity sections
- [X] T067 [US1] Mark `application_insights_connection_string` output as `sensitive = true` and source it from the App Insights resource (consumed only by the KV secret materialization — see US4)
- [X] T068 [US1] Create `iac/environments/dev/terraform.tfvars.example` as a redacted copy of `terraform.tfvars` (no real subscription IDs, no real client IDs — use the template from `contracts/config-profile-schema.md` §Dev with placeholders)

**Checkpoint**: User Story 1 fully testable — `tofu plan` against dev produces the expected adds + zero stateful destroys; `tofu apply` brings the full topology up; every documented output is emitted.

---

## Phase 4: User Story 2 — Workloads authenticate via managed identity with least-privilege RBAC (Priority: P1)

**Goal**: After apply, every workload-to-data-plane access path uses the workload UAMI's role assignments (no SAS, no account keys, no admin keys, no KV-secret-via-account-key). Backend App Insights ingestion uses AAD via `APPLICATIONINSIGHTS_AUTHENTICATION_STRING`; browser uses the ingestion key from KV per Q1c.

**Independent Test**: `az role assignment list --assignee <workload-uami-principal-id>` returns exactly the FR-033 enumeration scoped per-resource. `tofu output -json` (US1) shows no secret-content per the FR-036 patterns from `contracts/policy-rules.md` §`BT-IAC-005`. Container Apps backend log shows OTel exporter authenticating to App Insights via AAD (no `InstrumentationKey=` errors).

### Workload identity extensions (US2)

- [X] T069 [US2] Extend `iac/modules/workload-identity/variables.tf` to accept the extended `assigned_azure_rbac` map shape from `contracts/module-contracts.md` §`workload-identity (extended)` (existing module already supports arbitrary map; verify and document)
- [X] T070 [US2] Extend `iac/modules/workload-identity/README.md` to enumerate the FR-033 forward-looking role set AND document the **role-assignment split convention** explicitly: data-service workload roles (`Search Index Data Contributor`, `Azure Service Bus Data Sender`, `Azure Service Bus Data Receiver`) are emitted by the data-service modules themselves (T039, T047). Pass ONLY non-data-service roles (`AcrPull`, `Key Vault Secrets User`, `Monitoring Metrics Publisher`) via this module's `assigned_azure_rbac` map. Listing data-service roles in BOTH places would create duplicate `azurerm_role_assignment` resources and fail apply. Add a warning callout in the README plus a worked example showing the correct split for spec 005's set.
- [X] T071 [US2] Extend `iac/environments/dev/main.tf` to pass the FR-033 workload role set into `module.workload_identity`'s `assigned_azure_rbac` input: `acr-pull` (existing), `kv-secrets-user` (existing), `sb-data-sender` (scope: SB namespace), `sb-data-receiver` (scope: SB namespace), `search-index-data-contributor` (scope: AI Search), `monitoring-metrics-publisher` (scope: App Insights resource ID). Note: SB Sender/Receiver + Search Index Data Contributor are emitted by their own modules (T039, T047); this task wires Monitoring Metrics Publisher
- [X] T072 [US2] Verify the existing `azurerm_cosmosdb_sql_role_assignment.workload_data_contributor` in `iac/environments/dev/main.tf` remains in place (already-granted per `research.md` §5); no change needed but call out in PR description that adoption is intentional
- [X] T073 [US2] Extend `iac/modules/cosmos-canonical-store/outputs.tf` to emit `canonical_database_role_scope` (the exact scope path for `azurerm_cosmosdb_sql_role_assignment` consumers) per `research.md` §15 + `contracts/outputs-contract.md`

### Backend AAD ingestion for App Insights (US2 — implements Q1c hybrid auth)

- [X] T074 [US2] Extend `iac/environments/dev/main.tf` `module.container_app` invocation for the backend Container App to add env var `APPLICATIONINSIGHTS_AUTHENTICATION_STRING = "Authorization=AAD;ClientId=${module.workload_identity.uami_client_id}"` per `research.md` §6
- [X] T075 [US2] Verify the existing backend `APPLICATIONINSIGHTS_CONNECTION_STRING` env var continues to flow from the KV secret reference (unchanged from spec 002); browser `NEXT_PUBLIC_APPINSIGHTS_CONNECTION_STRING` also unchanged per Q1c
- [X] T076 [US2] Extend `iac/modules/monitoring/variables.tf` to add `local_authentication_disabled` (bool, default `false`) per `contracts/module-contracts.md` §`monitoring (extended)`; thread it into `azurerm_application_insights.this.local_authentication_disabled`
- [X] T077 [US2] Document in `iac/modules/monitoring/README.md` why `local_authentication_disabled` MUST remain `false` (browser SDK does not support AAD ingestion per Microsoft Learn — `research.md` §6)

### Pipeline RBAC-Admin condition extension (US2 — for the platform-bootstrap stack)

- [X] T078 [US2] Extend `iac/platform-bootstrap/main.tf` `azurerm_role_assignment.pipeline_role_admin` condition to include the four new role GUIDs from `research.md` §12: `69a216fc-b8fb-44d8-bc22-1f3c2cd27a39` (SB Data Sender), `4f6d3b9b-027b-4f4c-9142-0e5a2a2247e0` (SB Data Receiver), `8ebe5a00-799e-43f5-93ac-243d3dce84a7` (Search Index Data Contributor), `3913510d-42f4-4e42-8a64-420c390055eb` (Monitoring Metrics Publisher). Keep comment header documenting each GUID
- [X] T079 [US2] Apply `iac/platform-bootstrap/` (manual operator step — call out in PR description that this must run before the dev env apply that introduces the new role assignments). Confirm the new condition is live with `az role assignment list --assignee <pipeline-mi-principal-id> --include-conditions`. **Applied 2026-05-26**: role assignment replaced (id `a140b4e3-...`); az verification confirms all 7 GUIDs (3 original + 4 spec-005) live in the scope `/subscriptions/08b37dc0-.../`.

> ⚠️ **PRE-APPLY GATE** — T079 (and T078 that authors the condition extension) MUST be applied before ANY `tofu apply` against `iac/environments/dev/` that includes a new role assignment from the FR-033 set. If you attempt to dry-run / partial-apply US1's wiring before T078+T079 land, the pipeline-MI will hit `AuthorizationFailed` on the new role-assignment writes. Recommended sequence: complete T078 → apply platform-bootstrap (T079) → continue US1+US2 wiring → run the full dev apply at T127. The MVP validation in T127 depends on T079 having already run.

**Checkpoint**: User Story 2 fully testable — workload UAMI holds exactly the FR-033 set; backend `dotnet` process authenticates to App Insights via AAD with no fallback to ingestion key; no secret-like values in outputs. **Bootstrap RBAC-Admin condition is live and verified** (T079).

---

## Phase 5: User Story 3 — Production data services are private by default (Priority: P1)

**Goal**: Test/prod composition templates ship with `data_services_public_access_enabled = false` AND `private_endpoints_enabled = true`. Dev's warm PEs exercise the same wiring with public access toggled on per Q2c. The CI policy gate (BT-IAC-002) blocks public-by-default prod data services.

**Independent Test**: `tofu validate` against `iac/environments/prod/` (US5 delivers the template; this story validates the per-env defaults). `iac/policies/check-public-access.sh` on a prod tfplan with a deliberately-toggled `public_network_access_enabled = true` returns exit code 1.

> ⚠️ Many of the PE wiring tasks already landed under US1 because the modules+composition wire them in one pass. This phase enforces the *defaults* and *policy posture* that make prod private-by-default.

- [X] T080 [US3] Verify (and adjust if needed) `iac/environments/dev/variables.tf` (from T027) has `data_services_public_access_enabled` default `true`, `private_endpoints_enabled` default `true` per Q2c
- [X] T081 [US3] Verify the dev composition (T063) passes `var.data_services_public_access_enabled` into `module.cosmos_account.public_network_access_enabled`, `module.keyvault.public_network_access_enabled`, `module.ai_search.public_network_access_enabled`, `module.service_bus.public_network_access_enabled` — wiring the per-env toggle FR-031
- [X] T082 [US3] Verify each data-service module instantiation in dev composition passes `private_dns_zone_id = var.private_endpoints_enabled ? module.networking.private_dns_zone_ids["<zone-name>"] : null` and `private_endpoint_subnet_id = var.private_endpoints_enabled ? module.networking.subnet_private_endpoints_id : null`. **Refined per commit `1d27ea5`**: KV / Cosmos / AI Search use a plan-time-knowable bool toggle (`private_endpoint_enabled = var.private_endpoints_enabled`) with unconditional subnet+zone IDs so module preconditions evaluate cleanly at plan time. Service Bus retains the ternary form because of the SKU compound condition. Functional behavior identical: `private_endpoints_enabled = false` → no PE on any data service.
- [X] T083 [US3] Add documentation comment in `iac/environments/dev/terraform.tfvars` explaining the Q2c trade-off (dev opts into public access until destructive retrofit) and pointing to `spec.md` §Clarifications

**Checkpoint**: User Story 3's posture is enforced by configuration defaults and by the BT-IAC-002 policy gate (implemented in US6). Test/prod templates (US5) inherit the private-by-default defaults.

---

## Phase 6: User Story 4 — Diagnostics + telemetry route to one observability workspace per env (Priority: P2)

**Goal**: Every supported platform resource forwards `allLogs` to the env's LAW via the new `diagnostic-settings` module (T017–T021). NO `enabled_metric` block exists on any diagnostic setting (Q5c). LAW retention is a per-env tf-var defaulting to 30 days.

**Independent Test**: After apply, `az monitor diagnostic-settings list --resource <each-supported-resource-id>` shows one setting per resource with `logs[].categoryGroup = "allLogs"` and zero entries in `metrics[]`. `terraform.tfvars` `log_analytics_retention_days = 30` is reflected in `az monitor log-analytics workspace show`.

### Refactor existing inline diagnostic settings to use the new module (US4)

- [X] T084 [US4] Refactor the existing `azurerm_monitor_diagnostic_setting` blocks at `iac/environments/dev/main.tf` (the Container App API + Web diagnostic settings currently containing `enabled_metric { category = "AllMetrics" }` blocks per `data-model.md` §2.2) to use `module.diagnostic-settings` (T017) — this REMOVES the `enabled_metric` blocks per Q5c
- [X] T085 [US4] Refactor any inline `azurerm_monitor_diagnostic_setting` in `iac/modules/cosmos-account/main.tf`, `iac/modules/keyvault/main.tf`, `iac/modules/container-apps-env/main.tf`, `iac/modules/container-registry/main.tf`, `iac/modules/monitoring/main.tf` to delegate to the `diagnostic-settings` module (one module call per resource per `contracts/policy-rules.md` §`BT-IAC-003`). **Implemented as**: cosmos-account had a true inline `azurerm_monitor_diagnostic_setting` → replaced with `module.diagnostics` + `moved` block. keyvault / container-apps-env / container-registry / monitoring use the AVM `diagnostic_settings` input (not inline); switched `metric_categories = []` to drop the AVM-generated metric block while keeping the AVM-emitted setting at its existing state address (zero state churn). Both shapes are BT-IAC-003 compliant.
- [X] T086 [US4] Add `module.diagnostic-settings` calls in dev composition for every NEW resource introduced by US1: AI Search service (handled inside `module.ai-search` per T037), Service Bus namespace (handled inside `module.service-bus` per T045), and App Insights itself (target `azurerm_application_insights.this`)

### LAW retention as tf-var (US4)

- [X] T087 [US4] Confirm `iac/modules/monitoring/variables.tf` has `retention_in_days` variable (per `research.md` §8 it already does); ensure the default is `30`
- [X] T088 [US4] Extend `iac/environments/dev/main.tf` `module.monitoring` invocation to pass `retention_in_days = var.log_analytics_retention_days` (T027 added the env-level var)

### Output wiring (US4)

- [X] T089 [US4] Extend `iac/environments/dev/outputs.tf` (US1's T066) to include `log_analytics_workspace_id`, `log_analytics_workspace_customer_id`, `application_insights_id`, `application_insights_app_id`, `application_insights_name`, `app_insights_connection_string_secret_uri` per `contracts/outputs-contract.md` §Observability + §Secrets

### KV secret materialization for App Insights connection string (US4 — implements Q1c)

- [X] T090 [US4] Verify `iac/environments/dev/main.tf` already provisions `azurerm_key_vault_secret.app_insights_connection_string` referencing `azurerm_application_insights.this.connection_string` (existing per spec 002 lines 127–145 per `research.md` §6). If absent, add it with `content_type = "application/x-azure-monitor-connection-string"` and a far-future `expiration_date` matching the existing CKV_AZURE_41 convention
- [X] T091 [US4] Verify the existing Container Apps `secret` blocks reference the KV secret URI and the workload UAMI holds `Key Vault Secrets User` on the vault (existing per spec 002); document in PR description that no change is needed if already in place

**Checkpoint**: User Story 4 fully testable — every supported resource shows up in the LAW; queries against `AzureDiagnostics` return rows for all expected resources; `AzureMetrics` table is empty for the resources affected by T084 (per Q5c); App Insights data is queryable end-to-end via both AAD (backend) and ingestion-key (browser) paths.

---

## Phase 7: User Story 5 — Independently-deployable dev/test/prod from the shared module set (Priority: P2)

**Goal**: `iac/environments/test/` and `iac/environments/prod/` exist as composition templates that pass `tofu validate` against the shared modules. They are NOT applied in this slice (Q1c env scope = dev only). Per-env tfstate keys are scoped (`envs/test/...`, `envs/prod/...`) so cross-env writes are impossible by construction.

**Independent Test**: From `iac/environments/test/`, `tofu init -backend=false && tofu validate` exits 0. Same for `prod/`. `iac/environments/test/backend.tf` references `key = "envs/test/terraform.tfstate"`. Operator runs the "B. Stand up test or prod" path in `quickstart.md` and the path completes without code changes.

### Test environment composition template (US5)

- [X] T092 [US5] Create `iac/environments/test/providers.tf` matching the dev composition's provider block, with `subscription_id` parameterized (no hardcoded value)
- [X] T093 [P] [US5] Create `iac/environments/test/backend.tf` with `azurerm` backend pointing to `btstatech0001` / `tfstate` container / `key = "envs/test/terraform.tfstate"` per `data-model.md` §4
- [X] T094 [P] [US5] Create `iac/environments/test/variables.tf` as a copy of the dev `variables.tf` with test-tier defaults from `contracts/config-profile-schema.md` §Test: `network_address_space = ["10.51.0.0/16"]`, `data_services_public_access_enabled = false`, `ai_search_sku = "standard"`, `service_bus_sku = "Premium"`, `service_bus_capacity = 1`, `key_vault_purge_protection_enabled = true`, `key_vault_soft_delete_retention_days = 90`
- [X] T095 [US5] Create `iac/environments/test/main.tf` as a copy of the dev composition (post-US1–US4) with the same module call graph; ensure no hardcoded dev-only values bled through. **Dev-only `import {}` and `moved {}` blocks intentionally omitted** — test has no pre-existing state to adopt or refactor.
- [X] T096 [US5] Create `iac/environments/test/outputs.tf` mirroring the dev outputs file (same key set per `contracts/outputs-contract.md`)
- [X] T097 [US5] Create `iac/environments/test/terraform.tfvars.example` per `contracts/config-profile-schema.md` §Test (no real secrets; placeholders for subscription_id, entra IDs, etc.)

### Prod environment composition template (US5)

- [X] T098 [P] [US5] Create `iac/environments/prod/providers.tf` matching the test providers.tf, with `subscription_id` parameterized
- [X] T099 [P] [US5] Create `iac/environments/prod/backend.tf` with `key = "envs/prod/terraform.tfstate"`
- [X] T100 [P] [US5] Create `iac/environments/prod/variables.tf` with prod-tier defaults from `contracts/config-profile-schema.md` §Prod: `location = "centralus"` (per `research.md` §17), `network_address_space = ["10.52.0.0/16"]`, all the prod-hardening defaults
- [X] T101 [US5] Create `iac/environments/prod/main.tf` as a copy of the test composition (which itself derives from dev); validate no test-only assumptions bled through
- [X] T102 [US5] Create `iac/environments/prod/outputs.tf` mirroring the test outputs file
- [X] T103 [US5] Create `iac/environments/prod/terraform.tfvars.example` per `contracts/config-profile-schema.md` §Prod

### Cross-env validation (US5)

- [X] T104 [US5] Verify from repo root: `cd iac/environments/test && tofu init -backend=false && tofu validate` exits 0. **Validated 2026-05-28**: `Success! The configuration is valid` (one upstream-AVM `local_authentication_disabled` deprecation warning — identical to dev, not actionable here).
- [X] T105 [US5] Verify from repo root: `cd iac/environments/prod && tofu init -backend=false && tofu validate` exits 0. **Validated 2026-05-28**: `Success! The configuration is valid` (same upstream-AVM deprecation warning as test/dev).
- [X] T106 [US5] Document in `iac/environments/test/README.md` and `iac/environments/prod/README.md` (new files): "Template only — NOT applied by spec 005. See `specs/005-infrastructure-baseline/quickstart.md` §B for the stand-up procedure when an operator is ready."
- [X] T134 [US5] Enforce FR-010 in the prod template: ensure the backend Container App's ingress defaults to **internal** (`external_enabled = false`) in `iac/environments/prod/main.tf` (and matching tfvars override in `iac/environments/prod/terraform.tfvars.example`). Add an env-level variable `backend_external_ingress` (bool, default `false` in prod; default `true` in dev/test to preserve current dev behavior) in each env's `variables.tf`, and thread it through the existing `module.container_app` invocation for the backend. Test composition keeps backend external (test mirrors dev's posture for parity-of-debugging) unless an operator overrides via tfvars.

**Checkpoint**: User Story 5 fully testable — both env templates validate; their backend keys are env-scoped; prod template defaults backend ingress to internal per FR-010; `quickstart.md` §B walkthrough is executable end-to-end.

---

## Phase 8: User Story 6 — Local + CI validation gates (Priority: P3)

**Goal**: Contributors run `tofu fmt`, `tofu validate`, `tofu plan` locally. CI re-runs these plus `checkov`, `tfsec`, and the seven custom policy rules from `contracts/policy-rules.md` (BT-IAC-001 through BT-IAC-007). Destructive changes to stateful resources surface as a "REQUIRES MANUAL APPROVAL" banner.

**Independent Test**: Run `bash iac/policies/run-policies.sh --plan <tfplan.json> --env dev` against a tfplan with: a resource missing tags (BT-IAC-001 FAIL), a prod resource with public access on (BT-IAC-002 FAIL), a resource with no diagnostic setting (BT-IAC-003 FAIL), a workload role assignment at subscription scope (BT-IAC-004 FAIL), a plaintext output containing `AccountKey=` (BT-IAC-005 FAIL), a stateful destroy (BT-IAC-007 FAIL). Each rule prints the expected failure message and the script exits 1.

### Policy gate scripts (US6 — bash + jq per research §16)

- [X] T107 [P] [US6] Implement `iac/policies/check-tags.sh` per `contracts/policy-rules.md` §`BT-IAC-001` — parses tfplan JSON (passed via `--plan`); asserts every taggable resource has the 5 mandatory tags; skips the documented untaggable types
- [X] T108 [P] [US6] Implement `iac/policies/check-public-access.sh` per `contracts/policy-rules.md` §`BT-IAC-002` — env-conditional (only fires when `--env` starts with `prod`); checks the documented resource types' `public_network_access_enabled` properties
- [X] T109 [P] [US6] Implement `iac/policies/check-diagnostics.sh` per `contracts/policy-rules.md` §`BT-IAC-003` — for each supported resource type, asserts a corresponding `azurerm_monitor_diagnostic_setting` exists with `category_group = "allLogs"` and NO `enabled_metric` block
- [X] T110 [P] [US6] Implement `iac/policies/check-rbac-scope.sh` per `contracts/policy-rules.md` §`BT-IAC-004` — asserts no `azurerm_role_assignment` or `azurerm_cosmosdb_sql_role_assignment` with workload-UAMI principal has subscription-level or management-plane scope; honors the pipeline-MI allowlist entries
- [X] T111 [P] [US6] Implement `iac/policies/check-outputs-no-secrets.sh` per `contracts/policy-rules.md` §`BT-IAC-005` — for each output, asserts (when not `sensitive`) the value doesn't match the documented secret patterns; asserts `application_insights_connection_string` IS marked sensitive
- [X] T112 [P] [US6] Implement `iac/policies/check-stateful-destroys.sh` per `contracts/policy-rules.md` §`BT-IAC-007` — operates on tfplan JSON only; asserts NO `delete` or `destroy-replace` action targets the documented stateful resource types; emits "REQUIRES MANUAL APPROVAL" banner when triggered
- [X] T113 [US6] Implement `iac/policies/run-policies.sh` orchestrator — accepts `--plan`, `--state`, `--env`, `--allowlist` (default `iac/policies/allowlist.json`), runs all checks in order, accumulates failures, exits 0/1/2 per `contracts/policy-rules.md` §`Rule execution`; emits Markdown summary to stdout + JSON detail to a file
- [X] T114 [US6] Implement `iac/policies/check-lockfile.sh` per `contracts/policy-rules.md` §`BT-IAC-006` — asserts `.terraform.lock.hcl` is committed and matches what `tofu init -upgrade=false` resolves (no drift)
- [X] T115 [US6] Create `iac/policies/allowlist.json` per the template at `contracts/policy-rules.md` §`Allowlist file format` — includes the pipeline-MI BT-IAC-004 exceptions documented in `plan.md` §`Complexity Tracking`
- [X] T116 [P] [US6] Document `iac/policies/README.md` describing each rule's purpose, exit codes, the allowlist format, and the "edits require justification + reviewer sign-off" convention

### Operator helper (US6)

- [X] T117 [US6] Implement `iac/scripts/apply-env.sh` wrapper per `plan.md` §Project Structure: runs fmt → validate → plan → policies → apply against a supplied env composition with the right backend key

### CI workflow integration (US6)

- [X] T118 [US6] Extend (do NOT recreate) `.github/workflows/iac-validate.yml` per `quickstart.md` §C — for each env composition (`dev`, `test`, `prod`): `tofu init -backend=false && tofu validate`; for dev only: pipeline-UAMI login → `tofu init` (with backend) → `tofu plan` → `tofu show -json tfplan > tfplan.json` → `bash iac/policies/run-policies.sh --plan tfplan.json --env dev` → `checkov` → `tfsec` → post plan summary to PR via `gh pr comment`
- [X] T119 [US6] Add a "REQUIRES MANUAL APPROVAL" check job in `iac-validate.yml` that fails the run when `BT-IAC-007` returns a destructive change (per `contracts/policy-rules.md` §`BT-IAC-007` + `quickstart.md` §C)
- [X] T120 [US6] Create `.github/workflows/iac-apply-dev.yml` per `quickstart.md` §C — triggers on push to `main` touching `iac/environments/dev/**` / `iac/modules/**` / `iac/platform-bootstrap/**`; re-runs validate's plan; on approval, `tofu apply tfplan`; posts apply summary

**Checkpoint**: User Story 6 fully testable — local script invocation passes/fails as expected per rule; CI on a synthetic PR with deliberate violations posts the BT-IAC-NNN failure summary and blocks the merge.

---

## Phase 9: User Story 7 — Re-applies are safe; stateful resources protected (Priority: P3)

**Goal**: After the dev baseline is applied, an unrelated module edit produces a tfplan with zero `delete` actions on the stateful resource list in `data-model.md` §3. Production KV has `purge_protection_enabled = true`. The state storage account has `versioning_enabled = true`.

**Independent Test**: From `iac/environments/dev/`, simulate a tag-value-change PR (`tags.cost-center = "platform-2"`) and confirm `tofu plan` shows zero destroys against any stateful resource. From `iac/environments/prod/` template, `tofu validate` against tfvars-example shows `key_vault_purge_protection_enabled = true` and the plan would set `purge_protection_enabled = true` on the KV.

- [ ] T121 [US7] Verify each stateful resource module already has `lifecycle { prevent_destroy = true }` where appropriate (Cosmos DB account, KV, ACR, LAW, App Insights, tfstate storage) — add the block where missing. Note: this requires balancing intentional replacements (rare) against accidental destroys (the BT-IAC-007 policy gate is the primary defense; `prevent_destroy` is belt-and-suspenders)
- [ ] T122 [US7] Verify `iac/environments/dev/variables.tf` `key_vault_purge_protection_enabled` defaults to `false` (dev) AND `iac/environments/{test,prod}/variables.tf` defaults to `true`; thread the variable into `module.keyvault` per FR-019
- [ ] T123 [US7] Verify `iac/platform-bootstrap/main.tf` storage account block has `versioning_enabled = true` and 30-day soft-delete retention per `data-model.md` §4 (already present per research §13; this task confirms)
- [ ] T124 [US7] Document the destructive-change manual approval gate in `iac/environments/dev/README.md` (new file, per US5/T106 convention) referencing the BT-IAC-007 rule and the iac-apply-dev.yml approval step

**Checkpoint**: User Story 7 fully validated — `prevent_destroy` is in place on stateful resources, prod KV purge protection is on by default in the prod template, and the BT-IAC-007 gate (from US6) blocks accidental destroys at plan time.

---

## Phase 10: Polish & Cross-Cutting Concerns

**Purpose**: Final formatting, documentation regeneration, end-to-end validation against `quickstart.md`, and tech-stack documentation updates.

- [ ] T125 [P] Run `tofu fmt -recursive iac/` from the repo root; commit any formatting changes
- [ ] T126 [P] Run `terraform-docs markdown table iac/modules/<each-module>/` and commit the regenerated module READMEs (or wire `terraform-docs` as a CI formatting gate per `plan.md` §Testing)
- [ ] T127 Verify the `iac/environments/dev/` apply matches `quickstart.md` §A end-to-end: §A.1 login, §A.2 init, §A.3 plan (zero stateful destroys), §A.4 policy gate (all pass), §A.5 apply, §A.6 validate outputs + live dev URL still works + MSAL sign-in still works. **Capture wall-clock duration** of `tofu apply` and record it in the PR description; the SC-001 budget is 60 minutes for an empty-RG apply (this slice is an incremental retrofit, so the budget here is informational — but the timing data establishes a baseline for SC-001's later validation when a fresh env stands up via the test/prod templates, which is SC-007's 30-minute budget for an incremental env addition).
- [ ] T128 Confirm Container Apps service-bus FQDN / AI Search endpoint / Cosmos endpoint outputs resolve correctly via `tofu output -json` and match the format documented in `contracts/outputs-contract.md`
- [ ] T129 Confirm the BT-IAC-003-adjacent invariant from `quickstart.md` §D "Browser telemetry stops appearing" — App Insights `local_authentication_disabled` is `false`; browser SDK ingestion continues to work
- [ ] T130 Update `speckit-artifacts/tech-stack.md` to reflect any durable new conventions this slice introduces (the `allLogs`-only diagnostic convention, the new policy-gate rule IDs, the new AVM pins) per CLAUDE.md §"When You're Unsure"
- [ ] T131 [P] Verify the `.terraform.lock.hcl` files in `iac/environments/dev/`, `iac/environments/test/`, `iac/environments/prod/`, `iac/platform-bootstrap/`, and each new module directory are committed per `research.md` §13
- [ ] T132 Capture and resolve any `checkov` / `tfsec` findings introduced by this slice; either fix the finding or add an explicit skip with justification in the resource code
- [ ] T133 Run `/speckit-analyze` after `tasks.md` lands to verify cross-artifact consistency (spec ↔ plan ↔ tasks); resolve any CRITICAL/HIGH findings before `/speckit-implement`
- [ ] T135 Author `specs/005-infrastructure-baseline/production-hardening.md` per FR-046 — an audit-grade reference enumerating every production hardening switch this slice introduces, separated from local/dev conveniences. Required sections: (a) Network posture (data-services public access disabled, PEs live, DNS zones linked); (b) Secrets posture (KV purge protection ON, soft-delete retention 90d, RBAC authorization); (c) State posture (storage account versioning + soft-delete, lifecycle.prevent_destroy on stateful resources); (d) Identity posture (workload UAMI role enumeration per FR-033, deployment-MI per-env federation per FR-032); (e) Observability posture (allLogs forwarded, retention default, AAD ingestion for backend); (f) CI posture (BT-IAC-001 through BT-IAC-007 active, BT-IAC-002 enforced for prod, BT-IAC-007 manual approval gate). For each item, list the controlling tf-var, the prod default, and the override mechanism. Cross-reference `contracts/policy-rules.md` and `quickstart.md` §C.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1, T001–T011)**: No dependencies — can start immediately. T001 must precede T002–T006 only if shell-completion ordering matters; in practice they're parallel.
- **Foundational (Phase 2, T012–T028)**: Depends on Setup completion. BLOCKS every user story phase.
  - Naming module (T012–T016) and Diagnostic-settings module (T017–T021) and Private-endpoint module (T022–T026) are mutually independent — three parallel tracks.
  - T027 + T028 (env-composition variables/providers) gate every wiring task in US1.
- **User Story 1 (Phase 3, T029–T068)**: Depends on Phase 2. Internal ordering: networking module (T029–T034) before AI-Search/Service-Bus/extended-module wiring (T035–T058) because PE wrappers consume DNS zone IDs; env-composition wiring (T059–T068) comes after every module is in place.
- **User Story 2 (Phase 4, T069–T079)**: Depends on Phase 2 + the workload-identity module shape (existing, no US1 dependency). T071 references AI-Search and Service-Bus module instantiations (US1) — sequence after US1 in practice, but the role-assignment additions are themselves independent of US1's exact resource creation.
- **User Story 3 (Phase 5, T080–T083)**: Depends on US1 (the resources whose toggles are being enforced).
- **User Story 4 (Phase 6, T084–T091)**: Depends on Phase 2's `diagnostic-settings` module (T017–T021) and US1's new resources (for the diagnostic targets).
- **User Story 5 (Phase 7, T092–T106)**: Depends on US1+US2+US3+US4 (templates copy the post-merge dev composition). Internally: test template (T092–T097) and prod template (T098–T103) are mutually independent — parallel.
- **User Story 6 (Phase 8, T107–T120)**: Policy scripts (T107–T116) are mutually independent — parallel. CI workflow integration (T118–T120) depends on the scripts existing.
- **User Story 7 (Phase 9, T121–T124)**: Depends on US1 (the resources being protected exist) + US5 (the prod template that asserts purge-protection).
- **Polish (Phase 10, T125–T133)**: Depends on every prior phase.

### MVP definition

**MVP = Phase 1 + Phase 2 + Phase 3 (US1) + Phase 4 (US2) + Phase 5 (US3)**. After MVP, dev has the full topology, identity-based access, and the private-by-default posture (warm in dev). US4–US7 add observability polish, multi-env templates, CI gates, and re-apply safety — all valuable, none blocking the dev environment from operating.

### Parallel opportunities (selected)

- **Phase 2**: T012–T016 (naming) ‖ T017–T021 (diagnostics) ‖ T022–T026 (PE wrapper) — three independent module tracks.
- **Phase 3**: T029–T034 (networking) is sequential within itself; T035–T042 (AI Search) ‖ T043–T050 (Service Bus) ‖ T051–T058 (existing-module extensions) once networking is in place.
- **Phase 7**: T092–T097 (test template) ‖ T098–T103 (prod template) — entirely independent.
- **Phase 8**: T107–T112 (six policy-rule scripts) — entirely independent; can land as six parallel PRs if helpful.

### Within each story

- Module variables → module main → module outputs → module versions → module README (small dependency chain inside a single module).
- Module implementation → env-composition wiring → env-composition outputs (composition consumes module).
- Composition wiring → policy gate scripts (gates assert against the composition's plan).

---

## Parallel Example: User Story 1 module track

```bash
# Once Phase 2 (foundational modules) is complete, three module tracks run in parallel:

# Track A — AI Search module
Task: T035 Implement iac/modules/ai-search/variables.tf
Task: T036 Implement iac/modules/ai-search/main.tf (AVM call)
Task: T037 Add diagnostic-settings call
Task: T038 Add conditional PE wrapper call
Task: T039 Add Search Index Data Contributor role assignment
Task: T040 Implement iac/modules/ai-search/outputs.tf
Task: T041 Implement iac/modules/ai-search/versions.tf
Task: T042 Document iac/modules/ai-search/README.md

# Track B — Service Bus module
Task: T043 Implement iac/modules/service-bus/variables.tf
Task: T044 Implement iac/modules/service-bus/main.tf (AVM call, namespace only)
# ... T045–T050

# Track C — Extend existing modules with PE inputs
Task: T051 Extend iac/modules/cosmos-account/variables.tf
Task: T054 Extend iac/modules/keyvault/variables.tf
Task: T057 Extend iac/modules/container-registry/variables.tf
# (then their respective main.tf + outputs.tf changes)
```

---

## Implementation Strategy

### MVP first (US1 + US2 + US3, the three P1s)

1. Complete Phase 1 (Setup, T001–T011)
2. Complete Phase 2 (Foundational, T012–T028)
3. Complete Phase 3 (US1, T029–T068)
4. Complete Phase 4 (US2, T069–T079) including the platform-bootstrap apply (T079)
5. Complete Phase 5 (US3, T080–T083)
6. **STOP and VALIDATE**: full dev `tofu apply` per `quickstart.md` §A; zero destroys on stateful resources; live dev URL still works; backend telemetry visible in App Insights via AAD; workload UAMI holds the FR-033 enumeration only.

After MVP, dev is fully on the new baseline and ready for downstream specs to consume the new outputs.

### Incremental delivery beyond MVP

7. Complete Phase 6 (US4, T084–T091) — refactor diagnostics; the Q5c invariant is now enforced by the module shape AND the (US6) policy gate.
8. Complete Phase 7 (US5, T092–T106) — test/prod templates ship; operators can stand them up via `quickstart.md` §B.
9. Complete Phase 8 (US6, T107–T120) — CI gates and policy scripts land; future infra PRs are validated automatically.
10. Complete Phase 9 (US7, T121–T124) — final stateful-destroy guards.
11. Complete Phase 10 (Polish, T125–T133).

### Parallel team strategy

Once Phase 2 is complete, three developers can work in parallel:

- **Developer A**: US1 networking + service-bus + ai-search modules + env composition wiring (longest critical path)
- **Developer B**: US2 RBAC extensions + platform-bootstrap condition update (depends on US1's role-assignment scopes but the GUIDs are knowable from research §12)
- **Developer C**: US6 policy gate scripts (no dependency on US1 resource shape; scripts work off resource *types*, not specific instances)

Then US3 (small), US4 (refactor + new diag), US5 (template copy), US7 (small) can land in any order once their dependencies are in.

---

## Notes

- [P] tasks = different files, no dependencies on other incomplete [P] tasks in the same group.
- [Story] label maps task to its user story for traceability and selective rollback.
- Tests are NOT included per the spec's validation approach (CI gates + policy scripts under US6 replace traditional test tasks for IaC).
- Commit per task or per small task group; the existing speckit auto-commit hooks will prompt.
- Stop at any checkpoint to validate the story independently before moving on.
- Avoid: mixing US1 module work with US6 policy script work in a single commit (separate concerns, separate review surfaces).
- When in doubt about a destructive plan-time change, consult `data-model.md` §3 (stateful resource list) and `quickstart.md` §D (troubleshooting).

---

## Task count summary

| Phase | Task range | Count |
|---|---|---|
| Phase 1 — Setup | T001–T011 | 11 |
| Phase 2 — Foundational | T012–T028 | 17 |
| Phase 3 — US1 (P1, MVP) | T029–T068 | 40 |
| Phase 4 — US2 (P1) | T069–T079 | 11 |
| Phase 5 — US3 (P1) | T080–T083 | 4 |
| Phase 6 — US4 (P2) | T084–T091 | 8 |
| Phase 7 — US5 (P2) | T092–T106 + T134 | 16 |
| Phase 8 — US6 (P3) | T107–T120 | 14 |
| Phase 9 — US7 (P3) | T121–T124 | 4 |
| Phase 10 — Polish | T125–T133 + T135 | 10 |
| **Total** | **T001–T135** | **135** |

> Note: T134 and T135 were appended out-of-sequence after `/speckit-analyze` flagged coverage gaps for FR-010 (prod backend ingress) and FR-046 (production hardening doc). They're logically grouped with US5 and Polish respectively despite the out-of-order IDs.

Independent test criteria for each user story are written into the goal/test paragraph at the head of each phase. Suggested MVP scope = US1 + US2 + US3 (the three P1 stories) per the Implementation Strategy section above.
