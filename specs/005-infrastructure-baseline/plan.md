# Implementation Plan: Infrastructure Baseline

**Branch**: `005-infrastructure-baseline` | **Date**: 2026-05-25 | **Spec**: [`spec.md`](./spec.md)

**Input**: Feature specification from `/specs/005-infrastructure-baseline/spec.md`

## Summary

Establish the complete BusTerminal infrastructure baseline in OpenTofu: networking (VNet, subnets, private DNS zones, private endpoints), data services (Cosmos DB already present from spec 004 + Azure AI Search + Service Bus namespace), observability (Log Analytics + workspace-based Application Insights — both already present from spec 002 but need diagnostic-coverage extension), identity (workload UAMI + per-environment pipeline UAMIs), forward-looking least-privilege RBAC (Cosmos Data Contributor, KV Secrets User, Service Bus Sender/Receiver, Search Index Data Contributor, Monitoring Metrics Publisher), consistent tagging, `allLogs`-only diagnostic settings with a 30-day default LA retention tf-var, and CI gates (`tofu fmt`/`validate`/`plan` + checkov/tfsec + a policy gate that blocks public-by-default prod data services / missing diagnostics / missing tags / excessive RBAC). All eight clarifications from `spec.md` apply: dev only is physically applied; existing 002 resources are selectively retrofitted via import + additive diagnostics/RBAC; Service Bus is namespace-only; App Insights ingestion is hybrid (backend AAD via managed identity, browser via connection string surfaced through KV → Container Apps secret reference); dev gets the full VNet + private endpoints with public access on; workload RBAC is forward-looking with an explicit role enumeration; deployment identities are per-environment; diagnostics are `allLogs` only (no metrics to LA) with 30-day retention exposed as a tf-var.

## Technical Context

**Language/Version**: OpenTofu ≥ 1.11 (`required_version = ">= 1.11.0"` matches the existing `iac/platform-bootstrap/main.tf` pin). HCL2.

**Primary Dependencies**:
- Providers (existing pins from `iac/platform-bootstrap/main.tf`, to be reused by env compositions): `hashicorp/azurerm ~> 4.0`, `hashicorp/azuread ~> 3.0`, `hashicorp/time ~> 0.12`. New: `hashicorp/random ~> 3.6` (already transitively present via AVMs) for naming-suffix generation only if needed.
- Azure Verified Modules (AVMs), pinned per the constitution: `Azure/avm-res-storage-storageaccount/azurerm 0.6.3` (already used for tfstate), `Azure/avm-res-managedidentity-userassignedidentity/azurerm 0.3.3` (already used for pipeline MIs). New AVMs evaluated in `research.md`: `Azure/avm-res-network-virtualnetwork/azurerm`, `Azure/avm-res-network-privatednszone/azurerm`, `Azure/avm-res-network-privateendpoint/azurerm`, `Azure/avm-res-search-searchservice/azurerm`, `Azure/avm-res-servicebus-namespace/azurerm`, `Azure/avm-res-keyvault-vault/azurerm` (if existing hand-rolled `iac/modules/keyvault` is replaced — decision in research), `Azure/avm-res-documentdb-databaseaccount/azurerm` (if existing hand-rolled `iac/modules/cosmos-account` is replaced — decision in research).
- Hand-authored modules retained: `naming/` (new), `role-assignments/` (new), `diagnostic-settings/` (new — wraps `azurerm_monitor_diagnostic_setting` for the `allLogs`-only convention), plus all existing `iac/modules/*` (workload-identity, container-apps-env, container-app, container-registry, monitoring, federated-credential, identity, keyvault, cosmos-account, cosmos-canonical-store, app-registration-roles, graph-permissions, probe-job-internal-caller).

**Storage**:
- Application data plane: Azure Cosmos DB SQL (already provisioned by spec 004's `cosmos-canonical-store` module; this baseline retains it, adopts it into the formalized module layout, and adds a private endpoint).
- Search index: Azure AI Search (new). SKU per environment — `basic` for dev (per NFR-003 cost awareness), `standard` for prod template (production-compatible topology preserved per NFR-003).
- Internal messaging: Azure Service Bus namespace (new). SKU `Standard` for dev (cheapest SKU that supports topics + sessions + dead-letter); `Premium` reserved for the prod template because Premium is required for private endpoints (FR-024 caveat: "where the chosen SKU supports it"). Decision recorded in `research.md`.
- Secrets: Azure Key Vault (existing dev KV adopted via import; new private endpoint added in dev — warm per the Q2c networking clarification).
- Remote state: existing Azure Storage backend (`btstatech0001`, container `tfstate`, key `envs/dev/terraform.tfstate`). Test/prod state keys (`envs/test/...`, `envs/prod/...`) are pre-allocated in the same backend; per-env tofu workspaces are NOT used (per-env backend `key` overrides instead).

**Testing**:
- `tofu fmt -check -recursive` — formatting gate
- `tofu validate` per environment composition — syntactic + provider-validation gate
- `tofu plan` per environment composition — change-preview gate; CI posts plan summary to PR
- `checkov --framework terraform` + `tfsec` — static security scanning gate
- Custom policy script (bash, runs against `tofu show -json tfplan`) — asserts: every taggable resource has the mandatory tag set; no production data service has `public_network_access_enabled = true`; every supported resource has a diagnostic setting forwarding `allLogs`; no role assignment targets a subscription-wide scope except the documented pipeline-MI exception. This gate produces a structured JSON output the CI job parses to mark the PR check.
- `terraform-docs` (or `tofu-docs`) regenerates module READMEs as a CI-time formatting gate.
- No infra-runtime tests in this slice (no application-layer assertions beyond the existing spec-002 smoke that runs after CD).

**Target Platform**: Azure subscription `08b37dc0-0011-4841-84c0-0349a5c65883` (existing BusTerminal dev tenant). Primary region `eastus2` for dev (matches existing). Region for test/prod is parameterized via `var.location` per environment composition; templates default test to `eastus2` and prod to `centralus` (research will confirm Azure AI Search + Service Bus Premium availability in `centralus`).

**Project Type**: Infrastructure-as-code (single OpenTofu workspace with module library + per-environment compositions). No application source code modified by this slice (frontend/backend container images are consumed as-is from the existing CD flow).

**Performance Goals**: SC-001 — full baseline applies cleanly into an empty resource group in under 60 minutes of unattended runtime. SC-007 — adding a new environment takes under 30 minutes of operator configuration work. No application-runtime performance targets in this slice (those are owned by the runtime specs).

**Constraints**:
- Constitution: OpenTofu only (no Bicep/ARM/Pulumi); AVMs preferred with pinned versions; managed identity preferred over secrets; private networking preferred; all diagnostics route to the LAW.
- Spec 005 clarifications: dev only (test/prod templates only); selective retrofit (no destructive changes to existing 002 resources); Service Bus namespace only; App Insights hybrid ingestion with connection string in KV; full dev network topology with public-access toggle for data services; forward-looking workload RBAC role list (enumerated in FR-033); per-env deployment MI; `allLogs` only diagnostics; 30-day LA retention as tf-var.
- 002 carryover: live dev URL must NOT change; Entra app-registration redirect URIs must NOT need updating; tfstate backend must NOT be recreated.
- 004 carryover: existing Cosmos DB account + canonical-store database + workload `Cosmos DB Built-in Data Contributor` assignment must NOT be replaced; the spec 005 module layout adopts them via `import` only.

**Scale/Scope**:
- Net new Azure resources in dev: ~25 (VNet, 2 subnets, 4 private DNS zones, 3–4 private endpoints, AI Search service, Service Bus namespace, ~5 new role assignments, ~5 new diagnostic settings, KV PE + DNS A record, ~1 new tofu module composition glue).
- Net new modules in the IaC tree: 5 (`naming`, `networking`, `ai-search`, `service-bus`, `diagnostic-settings`; possibly `role-assignments` if research recommends extracting the role-grant pattern into a shared module). Existing 14 modules retained.
- Net new environment definitions: 2 (`test/`, `prod/`) as templates only — provider config, backend config, tfvars.example, variables, outputs, and main.tf that composes the shared modules.
- Lines of HCL added: estimated ~1500–2500 across modules + env templates + tests.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Constitution version: 1.0.0 (`.specify/memory/constitution.md`, ratified 2026-05-14).

### Principle I — Azure-First Architecture

**Gate**: ✅ PASS. Every resource provisioned by this slice is an Azure-native PaaS resource. No multi-cloud abstraction layers introduced. Networking and identity decisions deepen Azure integration (private endpoints + private DNS, managed identity + RBAC).

### Principle II — API-First Design

**Gate**: ✅ N/A. This slice ships no APIs. It produces infrastructure outputs consumed by later application specs; output stability is a contract surface and is governed by `contracts/` in Phase 1.

### Principle III — Strong Domain Modeling

**Gate**: ✅ N/A for runtime entities. Infrastructure modules ARE the domain of this spec. Module naming aligns with capability boundaries (FR-002) — naming, networking, container-apps, cosmos-db, ai-search, key-vault, service-bus, observability, managed-identity, role-assignments, diagnostic-settings — and matches the entity vocabulary the spec defines (Container Apps Environment, Cosmos DB Account, etc.).

### Principle IV — Security by Default

**Gate**: ✅ PASS. Spec 005 explicitly mandates:
- Private networking is preferred (FR-031: public access disabled by default; dev opts in via explicit toggle; test/prod templates default private).
- Least-privilege RBAC (FR-033 enumerates the exact roles).
- Managed identity preferred over secrets for every service-to-service path (FR-014, FR-016, FR-023, FR-033, and the FR-028 App Insights hybrid clarification).
- Microsoft Entra ID for platform identity (existing).
- Secrets never in source / state / outputs (FR-019, FR-021, FR-036, FR-041, FR-042).
- Supply chain: provider/module versions pinned in `versions.tf` per module and in the per-env composition. Lockfile (`.terraform.lock.hcl`) committed.

One subscription-wide RBAC grant exists today (`pipeline_subscription_contributor` + scoped `pipeline_role_admin` in `iac/platform-bootstrap/main.tf`). Spec FR-034 says "subscription-wide or cross-environment privileges are prohibited unless an explicit, documented exception applies." This is logged in **Complexity Tracking** below.

### Principle V — Operational Excellence

**Gate**: ✅ PASS with one Q5c-driven trade-off recorded and one explicit deferral logged. All Azure resources route logs to the LAW (constitution mandate). The Q5c clarification specifies `allLogs` only — no metrics forwarded to LA, since platform metrics are free in Azure Monitor's native metric store and metric ingestion into LA is billed. This satisfies "structured logging + diagnostic correlation + metrics" because metrics remain queryable via Azure Monitor's metrics explorer and via App Insights metrics — they are not lost, just not duplicated into the LA workspace. Health endpoints are an app concern (already in spec 002).

**Explicit deferral**: The constitution's §V "Operational dashboards" requirement is **deferred to a future ops-hardening spec** by this slice's Out of Scope section. Rationale per Decision Priorities §1 (Operational Simplicity) + §3 (Maintainability): spec 005 ships the *underlying observability resources* (LAW, App Insights, diagnostic-settings convention) that any future dashboard work consumes; building dashboards before downstream specs have produced telemetry to visualize would yield empty dashboards and churn. The deferral is recorded here so future PR review verifies the dashboard spec lands; no ADR is needed because the constitution's text scopes "All services MUST provide" to runtime application services, not to the infrastructure baseline itself.

### Principle VI — Incremental Extensibility

**Gate**: ✅ PASS. Module-level boundaries (FR-002, FR-004) and configuration-as-data (FR-006: per-env configuration profile) preserve the ability to add new environments, new regions, and new resources without architectural rewrites. The test/prod templates exercise the same module set as dev with only configuration deltas.

### Technology Standards (Constitution §Technology Standards)

| Standard | Compliance |
|---|---|
| OpenTofu required; Bicep prohibited | ✅ Spec FR-001 mandates OpenTofu only; out-of-scope explicitly excludes Bicep/ARM/Pulumi. |
| Azure Verified Modules preferred, versions pinned | ✅ Planned use of AVM for storage (already), MI (already), network, private DNS, private endpoint, search, service bus. Hand-authored modules used only where AVM coverage is insufficient or the existing module already encodes project-specific behavior (workload-identity composes UAMI + RBAC + app-role assignments; container-app encodes the KV-secret-reference pattern). Decisions recorded in `research.md`. |
| Container Apps + ACR for hosting | ✅ Reused from spec 002; not modified by this slice. |
| Key Vault for secrets | ✅ Existing dev KV retained; new private endpoint provisioned (warm in dev). |
| Observability via Azure Monitor + App Insights + OTel for Azure Monitor | ✅ Existing dev resources retained; diagnostic settings extended to every new resource. |
| Managed identity preferred over secrets | ✅ FR-014, FR-016, FR-023, FR-033, FR-028 (App Insights hybrid). The only exception is the App Insights ingestion key in the connection string surfaced to the browser, which is excluded from SC-003 by the Q1c clarification because it is write-only (telemetry-submission, no data read/write). |
| Microsoft Entra ID identity | ✅ Existing pipeline + workload MIs retained; per-env deployment MI mandated by Q4c. |
| Reproducible deployments + environment parity | ✅ Spec FR-005, FR-006, FR-007 + US5. |
| Modules versioned and reviewed like application code | ✅ CI gates (FR-043, FR-044, FR-045). |

### Engineering Workflow & Quality Standards

| Standard | Compliance |
|---|---|
| Spec-driven development | ✅ `/speckit-specify` → `/speckit-clarify` → `/speckit-plan` (this artifact) → `/speckit-tasks` → `/speckit-implement`. |
| CI gates (build, unit, lint, format, security, dependency scan) | ✅ FR-043 (local), FR-044 (CI), with `checkov` + `tfsec` for IaC security and the custom policy gate. |
| Trunk-based with feature branches | ✅ On `005-infrastructure-baseline` branch. |
| Open-source community readiness (reproducible local dev, contributor-friendly, transparent ADRs) | ✅ `quickstart.md` (Phase 1 output) documents the local apply path; module READMEs document inputs/outputs/examples; the subscription-wide pipeline-MI grant is documented as an ADR-track exception in Complexity Tracking. |

### Result: ✅ PASS (with one documented exception under Complexity Tracking)

Phase 0 may proceed.

## Project Structure

### Documentation (this feature)

```text
specs/005-infrastructure-baseline/
├── plan.md                           # This file
├── research.md                       # Phase 0 output — AVM/SKU/layout decisions with rationale
├── data-model.md                     # Phase 1 output — config-profile schema + resource topology graph
├── quickstart.md                     # Phase 1 output — dev apply walkthrough
├── contracts/                        # Phase 1 output — module input/output surfaces + emitted env outputs
│   ├── module-contracts.md           # Per-module input/output schema
│   ├── outputs-contract.md           # Per-env emitted outputs (what later specs consume)
│   ├── config-profile-schema.md      # Per-env configuration profile (var schema)
│   └── policy-rules.md               # Custom policy gate rule set (Q5c diagnostics, FR-031 public access, FR-033 RBAC, FR-037 tags)
├── checklists/
│   └── requirements.md               # Existing — already passes
└── tasks.md                          # Phase 2 output — NOT created by /speckit-plan
```

### Source Code (repository root)

```text
iac/
├── platform-bootstrap/               # Existing; extended by this slice
│   ├── main.tf                       # ADD: per-env pipeline MIs for test/prod (already for_each-driven);
│   │                                 # EXTEND: RBAC-Admin condition GUID list to include new workload roles
│   │                                 # (Cosmos Data Contributor, SB Sender/Receiver, Search Index Data
│   │                                 # Contributor, Monitoring Metrics Publisher)
│   ├── variables.tf                  # ADD: var.environments list now includes "test" and "prod" entries
│   └── outputs.tf                    # Existing
├── environments/
│   ├── dev/                          # Existing; extended by this slice
│   │   ├── main.tf                   # ADD: networking module composition; ai-search module composition;
│   │   │                             # service-bus module composition; diagnostic-settings module composition
│   │   │                             # for new resources; private-endpoint compositions for Cosmos, KV
│   │   │                             # (new), AI Search, SB (where SKU supports it); workload RBAC for the
│   │   │                             # new data services per FR-033 enumeration; REMOVE the existing
│   │   │                             # AllMetrics diagnostic settings on the Container Apps (per Q5c)
│   │   ├── variables.tf              # ADD: log_analytics_retention_days, network_address_space,
│   │   │                             # data_services_public_access_enabled, ai_search_sku, sb_sku
│   │   ├── outputs.tf                # ADD: all the FR-035 outputs (resource IDs, MI principal IDs,
│   │   │                             # endpoints, Application Insights resource id, AI Search endpoint,
│   │   │                             # Service Bus namespace FQDN, VNet id, subnet ids, PE FQDNs)
│   │   ├── providers.tf, backend.tf, terraform.tfvars  # Existing
│   ├── test/                         # NEW — template only, not applied in this slice
│   │   ├── main.tf, variables.tf, outputs.tf, providers.tf, backend.tf, terraform.tfvars.example
│   └── prod/                         # NEW — template only, not applied in this slice
│       ├── main.tf, variables.tf, outputs.tf, providers.tf, backend.tf, terraform.tfvars.example
└── modules/
    ├── naming/                       # NEW — central naming convention. Inputs: env, prefix, suffix,
    │                                 # resource_type; output: canonical name.
    ├── networking/                   # NEW — VNet + subnets (integration, private-endpoint) + private
    │                                 # DNS zones + VNet-zone links. AVM-backed (avm-res-network-*).
    ├── ai-search/                    # NEW — AI Search service + diagnostic settings hook + private-endpoint
    │                                 # subresource binding. AVM-backed.
    ├── service-bus/                  # NEW — Service Bus namespace + diagnostic settings hook + private-
    │                                 # endpoint subresource binding (SKU-conditional). AVM-backed.
    ├── diagnostic-settings/          # NEW — thin wrapper enforcing the `allLogs`-only convention from
    │                                 # Q5c. Inputs: target_resource_id, log_analytics_workspace_id, name.
    │                                 # Renders only `enabled_log { category_group = "allLogs" }` blocks —
    │                                 # explicitly omits `enabled_metric` blocks.
    ├── private-endpoint/             # NEW — wrapper around avm-res-network-privateendpoint that takes a
    │                                 # target resource id, the subresource name, the private-endpoint
    │                                 # subnet id, and the private DNS zone id, and produces a fully-wired
    │                                 # PE. Encapsulates the Cosmos vs KV vs Search vs SB subresource
    │                                 # name nuances.
    ├── role-assignments/             # NEW (optional — research decides) — abstracts the per-workload
    │                                 # role-grant set. Probably overkill; default-rejected unless research
    │                                 # surfaces a strong reason.
    ├── workload-identity/            # Existing — EXTEND: accept the new role-assignment set (Cosmos via
    │                                 # azurerm_cosmosdb_sql_role_assignment, SB via azurerm_role_assignment,
    │                                 # Search via azurerm_role_assignment, App Insights Monitoring Metrics
    │                                 # Publisher via azurerm_role_assignment).
    ├── container-apps-env/           # Existing — adopt as-is (no destructive change; VNet integration is
    │                                 # the deferred-retrofit follow-up's scope).
    ├── container-app/                # Existing — adopt as-is (KV-secret-reference pattern already
    │                                 # implements Q1c).
    ├── container-registry/           # Existing — adopt as-is.
    ├── cosmos-account/               # Existing — EXTEND: accept an optional private-endpoint binding;
    │                                 # add diagnostic-settings hook (currently uses an inline
    │                                 # azurerm_monitor_diagnostic_setting — refactor to use the new
    │                                 # diagnostic-settings module).
    ├── cosmos-canonical-store/       # Existing — adopt as-is.
    ├── keyvault/                     # Existing — EXTEND: accept an optional private-endpoint binding;
    │                                 # keep diagnostic settings routed through the new module.
    ├── monitoring/                   # Existing — retain (App Insights + LAW). The retention is already
    │                                 # 30d ✓. Convert `retention_in_days` from a hardcoded 30 to an input
    │                                 # variable defaulted to 30 (matches Q5c tf-var requirement).
    ├── federated-credential/         # Existing — adopt as-is.
    ├── identity/                     # Existing — adopt as-is.
    ├── app-registration-roles/       # Existing — adopt as-is.
    ├── graph-permissions/            # Existing — adopt as-is.
    └── probe-job-internal-caller/    # Existing — adopt as-is.

iac/policies/                          # NEW — custom policy-gate scripts (bash + jq) and rule
│                                      # definitions. Consumed by the CI workflow that runs after
│                                      # `tofu show -json tfplan`.
│   ├── check-tags.sh
│   ├── check-public-access.sh
│   ├── check-diagnostics.sh
│   ├── check-rbac-scope.sh
│   └── run-policies.sh                # Orchestrator — runs all checks, produces JSON result for CI.
iac/scripts/                           # NEW — operator-facing helper scripts (rare; most workflows are
│                                      # `tofu` direct)
│   └── apply-env.sh                   # Wrapper that runs fmt/validate/plan/policy/apply against a given
│                                      # env composition with the right backend key.

.github/workflows/
├── iac-validate.yml                   # NEW (or extend existing) — runs fmt/validate/plan + checkov +
│                                      # tfsec + the custom policy gate on every PR touching iac/
└── iac-apply-dev.yml                  # NEW (or extend the existing cd-dev.yml) — applies dev on merge
                                       # to main; gated by manual approval for destructive changes.
```

**Structure Decision**: The existing `iac/` tree (not `infra/opentofu/` as the source artifact proposed) is the canonical location. This is the explicit deferral the spec called out in Assumptions ("the implementation will converge on one canonical location, but the spec does not mandate which — the plan phase resolves this"). Rationale:

1. The dev environment is live and its state is keyed to the `iac/environments/dev/` composition. Renaming to `infra/opentofu/` would force a state-move operation and rewrite every CD workflow.
2. The existing 14 modules under `iac/modules/` are already shaped by capability and are well-tested in CI.
3. Spec FR-002 mandates capability-boundary modules, not a specific directory location.

The spec's `infra/opentofu/` proposal is treated as advisory; we extend `iac/` in place and document the chosen layout in `research.md`.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|---|---|---|
| **Pipeline managed identity holds subscription-wide `Contributor` and (scope-conditioned) `Role Based Access Control Administrator`** (existing in `iac/platform-bootstrap/main.tf` lines 158–218). This violates FR-034 ("subscription-wide or cross-environment privileges are prohibited unless an explicit, documented exception applies"). | The pipeline MI needs to create resources in arbitrary RGs the per-env composition might add (today: the env RG and the bootstrap RG; tomorrow potentially: per-env DNS zones in a shared zone RG, per-env Log Analytics workspaces in a shared workspace RG, future shared registries). Subscription-Contributor is the simplest grant that survives every composition shape. The RBAC-Admin grant is condition-scoped to a fixed allowlist of role GUIDs (today: AcrPull, KV Secrets User, KV Secrets Officer; spec 005 will extend the list to include Cosmos Data Contributor, SB Data Sender, SB Data Receiver, Search Index Data Contributor, Monitoring Metrics Publisher), so the privilege-escalation surface is bounded. | Per-RG scoping requires every new env composition to add a role assignment in the bootstrap stack before its first apply, doubling the number of state mutations per environment standup and creating a "bootstrap-before-bootstrap" ordering problem (the bootstrap stack would need to know about env RGs that haven't been created yet). The condition-scoped RBAC-Admin grant prevents the worst tail risk (the pipeline assigning itself Owner). This is recorded here as the constitution-mandated explicit, documented exception per Compliance Review (constitution §Governance). A future spec slice may introduce an ADR (`docs/adr/`) once the ADR location is established (per constitution Sync Impact Report TODO). |

---

## Post-Design Constitution Re-Check

*Performed after Phase 0 (`research.md`) and Phase 1 (`data-model.md`, `contracts/`, `quickstart.md`).*

Re-evaluating each principle against the concrete decisions captured in the Phase-0 and Phase-1 artifacts:

- **I. Azure-First Architecture** — ✅ Still PASS. The AVMs selected (`avm-res-network-virtualnetwork`, `avm-res-network-privatednszone`, `avm-res-search-searchservice`, `avm-res-servicebus-namespace`) are all Azure-native; no multi-cloud abstraction introduced.

- **II. API-First Design** — ✅ N/A unchanged. `contracts/outputs-contract.md` formalizes the infrastructure-output surface that downstream specs consume — analogous to an API contract for IaC. Output names are stable and validated by `BT-IAC-005`.

- **III. Strong Domain Modeling** — ✅ Strengthened. `data-model.md` captures the resource-topology graph explicitly and `contracts/module-contracts.md` defines each module's input/output shape. Naming convention is centralized in the new `iac/modules/naming/` module.

- **IV. Security by Default** — ✅ Still PASS, plus one refinement surfaced. Research §6 confirmed the App Insights ingestion-key path: the connection string IS stored as a KV secret + surfaced via Container Apps secret reference (matching Q1c), and App Insights local-auth remains enabled because the JavaScript SDK doesn't support Entra ingestion. This is documented; not a new exception. The pipeline-MI subscription-Contributor grant is the only remaining Complexity Tracking exception.

- **V. Operational Excellence** — ✅ Still PASS. The new `iac/modules/diagnostic-settings/` module enforces the `allLogs`-only + no-metrics Q5c convention by construction (the module physically cannot accept a metric block). The `BT-IAC-003` policy rule enforces it at CI time. Combined, these prevent silent drift.

- **VI. Incremental Extensibility** — ✅ Strengthened. The test/prod composition templates (Section §18 of research, `contracts/config-profile-schema.md` §B/§C) exercise every module against new environment inputs in CI's validate step — without applying — proving the modules don't carry dev-only assumptions.

### Technology Standards re-check

| Standard | Compliance after design |
|---|---|
| OpenTofu only, no Bicep/ARM/Pulumi | ✅ confirmed |
| AVM preferred + version-pinned | ✅ confirmed: networking, private DNS, AI Search, Service Bus, ACR (where retrofit needed), storage (existing) all use AVMs with explicit version pins per research §2 |
| Managed identity preferred over secrets | ✅ confirmed: all data-plane access in `data-model.md` §2 uses workload UAMI + RBAC; App Insights connection string is the documented exception (per Q1c clarification + research §6) |
| Diagnostics route to LAW | ✅ confirmed via `iac/modules/diagnostic-settings/` + `BT-IAC-003` |
| Provider/module versions pinned | ✅ confirmed: research §13 enumerates the pin set; `BT-IAC-006` enforces |

### Result: ✅ PASS

No new Complexity Tracking entries needed beyond the existing pipeline-MI subscription-Contributor exception. Phase 0 and Phase 1 artifacts are coherent with the constitution.

---

## Artifact Index (post-`/speckit-plan`)

| Artifact | Purpose | Status |
|---|---|---|
| [`plan.md`](./plan.md) | This file — Technical Context, Constitution Check, Project Structure, Complexity Tracking | ✅ produced |
| [`research.md`](./research.md) | Phase 0 — 20 numbered decisions (AVM picks, SKU choices, layout, conventions, RBAC GUIDs, AppInsights auth, network sizing) | ✅ produced |
| [`data-model.md`](./data-model.md) | Phase 1 — configuration profile schema + resource topology graph + lifecycle/replacement rules + state-management contract | ✅ produced |
| [`contracts/module-contracts.md`](./contracts/module-contracts.md) | Phase 1 — per-module input/output schemas for the 6 new and 4 extended modules | ✅ produced |
| [`contracts/outputs-contract.md`](./contracts/outputs-contract.md) | Phase 1 — env-composition output surface downstream specs consume | ✅ produced |
| [`contracts/config-profile-schema.md`](./contracts/config-profile-schema.md) | Phase 1 — env-composition variable schema + dev/test/prod tfvars examples | ✅ produced |
| [`contracts/policy-rules.md`](./contracts/policy-rules.md) | Phase 1 — 7 CI policy-gate rules (`BT-IAC-001` through `BT-IAC-007`) + allowlist format | ✅ produced |
| [`quickstart.md`](./quickstart.md) | Phase 1 — operator walkthrough: apply against dev, stand up test/prod, CI workflows, troubleshooting | ✅ produced |
| `tasks.md` | Phase 2 output — NOT created by `/speckit-plan`; produced by `/speckit-tasks` | ⏳ pending |
| `CLAUDE.md` SPECKIT-block reference | Updated to point at this plan | ✅ updated |
