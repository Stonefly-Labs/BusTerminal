# Production hardening — Spec 005 reference

**Feature**: `005-infrastructure-baseline` · **Date**: 2026-05-30 · **Authority**: FR-046

This document enumerates every production hardening switch this slice
introduces, with the controlling Tofu variable, prod default, override
mechanism, and where the switch is enforced. **It is intentionally separate
from local/development conveniences** so a reviewer can answer "what is
the production posture?" without traversing spec text, plan rationale, and
module READMEs.

When a future spec adds a hardening switch, append it here.

**Cross-references**:
- Tofu surface — `iac/environments/prod/variables.tf` (defaults), `iac/environments/prod/terraform.tfvars.example` (operator-facing override schema)
- Policy gates — `specs/005-infrastructure-baseline/contracts/policy-rules.md`
- Apply path — `specs/005-infrastructure-baseline/quickstart.md` § C ("Stand up prod")
- Stateful resource registry — `specs/005-infrastructure-baseline/data-model.md` § 3

---

## A. Network posture

Public network exposure is disabled on every data service in prod; private
endpoints carry all data-plane traffic; private DNS zones resolve
`privatelink.*` FQDNs from inside the VNet.

| Switch | Prod default | Tofu variable / location | Override mechanism | Enforcement |
|---|---|---|---|---|
| Data-service public network access | **`false`** (KV, Cosmos, AI Search, Service Bus Premium, ACR Premium) | `var.data_services_public_access_enabled` → `iac/environments/prod/variables.tf:195` | Set `data_services_public_access_enabled = true` in `terraform.tfvars` (discouraged; trips BT-IAC-002) | **BT-IAC-002** policy gate fails the prod plan when any of the gated resources have public access enabled. |
| Private endpoints provisioned | **`true`** | `var.private_endpoints_enabled` → `iac/environments/prod/variables.tf:201` | Set `private_endpoints_enabled = false` in `terraform.tfvars` | Module-level preconditions on `cosmos-account`, `keyvault`, `ai-search`, `service-bus`, `container-registry` reject `public_network_access_enabled = false` without a private endpoint. |
| `privatelink.*` DNS zones linked to env VNet | **All 6 zones** (vaultcore, documents, search, servicebus, azurecr, blob — blob reserved for future storage PE) | `module.networking.private_dns_zones` → `iac/environments/prod/main.tf` (env composition) | Edit composition `private_dns_zone_names` list | Networking module precondition: every requested PE-bearing service must have a matching zone. |
| Backend Container App ingress | **Internal-only** (`external_enabled = false`) | `var.backend_external_ingress` → `iac/environments/prod/variables.tf:272` (FR-010) | Set `backend_external_ingress = true` in `terraform.tfvars` | Wired through `module.backend_app` ingress block; no policy gate (operational decision per FR-010). |
| Frontend Container App ingress | External (public) | env composition (no tf-var; intentional) | n/a — frontend is user-facing | Confirmed at plan review time. |
| Container Apps Environment subnet | **VNet-integrated** via `subnet_integration_cidr` (warm in dev, threaded through all envs) | `var.subnet_integration_cidr` → `iac/environments/prod/variables.tf` | Re-CIDR via tfvars; precondition rejects subnets smaller than `/23` (CAE minimum) | Module-level precondition in `iac/modules/networking/main.tf`. |

**Compensating note — telemetry ingestion.** Application Insights browser
ingestion remains over public endpoints because the App Insights JavaScript
SDK does not support Microsoft Entra ingestion auth (research §6). AMPLS
(Azure Monitor Private Link Scope) is intentionally deferred per
research §14; backend ingestion uses AAD over the public endpoint.

---

## B. Secrets posture

Key Vault is the sole secret materialization surface; managed identity is
the authoritative service-to-service credential.

| Switch | Prod default | Tofu variable / location | Override mechanism | Enforcement |
|---|---|---|---|---|
| KV purge protection | **`true`** | `var.key_vault_purge_protection_enabled` → `iac/environments/prod/variables.tf:240` (FR-019) | Set to `false` in `terraform.tfvars` (Azure rejects flipping back to `false` on an in-state vault) | Threaded into `module.keyvault.purge_protection_enabled` (US7 / T122). |
| KV soft-delete retention | **`90` days** (Azure max) | `var.key_vault_soft_delete_retention_days` → `iac/environments/prod/variables.tf:246` | Override to 7–89 in `terraform.tfvars` (range-validated 7–90) | Variable validation; threaded into `module.keyvault.soft_delete_retention_days`. |
| KV authorization model | **RBAC-only** (no access policies) | Module constant — `module.keyvault` enables `enable_rbac_authorization = true` always | n/a — constitutional (FR-015) | Constitutional; no per-env knob. |
| KV public network access | Tied to **`data_services_public_access_enabled`** (prod: `false`) | See § A | See § A | See § A (BT-IAC-002). |
| App Insights connection string materialization | **KV-only** (`azurerm_key_vault_secret.app_insights_connection_string`) | Composition: `iac/environments/prod/main.tf` | Composition-level | Containers consume via Container Apps `secretref` → KV; never as plaintext. **FR-036** + **BT-IAC-005** (no inline credentials) gate this. |
| Embedded credentials in HCL | **Prohibited** | n/a | n/a — defect to allow | **BT-IAC-005** policy gate; `scripts/lint-iac-inline-credentials.sh` source-HCL scan. |
| Inline IAM in env compositions | **Prohibited** (must compose from `iac/modules/`) | n/a | Allowlist entry with rationale in script | **BT-IAC-004**; `scripts/lint-iac-inline-iam.sh`. |

---

## C. State posture

State storage is durable, versioned, and recoverable from accidental
mutation.

| Switch | Prod default | Tofu variable / location | Override mechanism | Enforcement |
|---|---|---|---|---|
| Tfstate storage account versioning | **`true`** | `iac/platform-bootstrap/main.tf` storage account `blob_properties.versioning_enabled` | Composition-level | Verified by T123. |
| Tfstate blob soft-delete retention | **`30` days** | `iac/platform-bootstrap/main.tf` `delete_retention_policy.days` / `container_delete_retention_policy.days` | Composition-level | Verified by T123. |
| Tfstate storage account replication | **`ZRS`** (zone-redundant) | `iac/platform-bootstrap/main.tf` `account_replication_type` | Composition-level | Documented in `.checkov.yaml` skip rationale for CKV_AZURE_206. |
| Tfstate auth model | **OIDC federated, AAD-only** | `iac/environments/prod/backend.tf`: `use_oidc = true` + `use_azuread_auth = true` | Composition-level (changing breaks CI) | Shared access keys are disabled on the SA (`shared_access_key_enabled = false`). |
| `lifecycle.prevent_destroy` on directly-owned stateful resources | **`true`** on env RG, `azurerm_cosmosdb_account.this`, `azurerm_cosmosdb_sql_database.canonical`, `azurerm_key_vault_secret.app_insights_connection_string` | Inline `lifecycle {}` blocks | Remove the block in a PR (rejects via BT-IAC-007 + approval gate) | **Belt-and-suspenders** to BT-IAC-007. AVM-wrapped stateful resources (KV, ACR, LAW, App Insights, ACE, workload UAMI, tfstate SA) rely on BT-IAC-007 alone — enumerated in `iac/environments/dev/README.md` § "AVM-wrapped stateful resources". |
| Stateful destroy manual approval | Required for every PR plan touching a stateful resource | Composition-level | "Re-run failed jobs" on the `iac-stateful-change-approval` job after reviewer approval | **BT-IAC-007** policy gate writes `requires-manual-approval.flag`; the dedicated approval-gate job fails until a maintainer re-runs it (`.github/workflows/iac-validate.yml`). |

---

## D. Identity posture

Workload UAMIs hold the strict, forward-looking RBAC enumeration from
FR-033; the deployment MI is per-environment and condition-scoped to four
specific role GUIDs.

| Switch | Prod default | Tofu variable / location | Override mechanism | Enforcement |
|---|---|---|---|---|
| Workload UAMI role assignments (backend) | `Cosmos DB Built-in Data Contributor` (Cosmos account / canonical db scope) · `Key Vault Secrets User` (env KV scope) · `Azure Service Bus Data Sender` + `Data Receiver` (SB namespace scope) · `Search Index Data Contributor` (Search service scope) · `Monitoring Metrics Publisher` (App Insights scope) | `module.workload_identity.assigned_azure_rbac` + per-module data-service role assignments | Add a role outside this set in a new spec with justification | **BT-IAC-001** policy gate compares against the FR-033 allowlist; any addition outside it fails the gate. **FR-033** is the binding text. |
| Workload UAMI role assignments (frontend) | `Key Vault Secrets User` (env KV) · `Monitoring Metrics Publisher` (App Insights) | Same as above | Same as above | Same as above. |
| Deployment MI federation subject | **`repo:Stonefly-Labs/BusTerminal:environment:prod`** (per-env) | `iac/platform-bootstrap` `module.pipeline_federation_prod` | Bootstrap-only | **FR-032** — wildcards (`*`) in subject are rejected by the `federated-credential` module. |
| Deployment MI cross-env access | **Prohibited** | Bootstrap module's RBAC-Admin condition is scoped to a specific set of role GUIDs and env RGs | Modify the bootstrap module's condition (high-friction; reviewer scrutiny) | **FR-034** + bootstrap RBAC-Admin condition. |
| System-assigned identities on data services | **Disabled** (workload UAMI handles all outbound auth) | Module defaults in `iac/modules/ai-search`, `iac/modules/service-bus`, `iac/modules/cosmos-account` | n/a — flip in a new spec with rationale | `.checkov.yaml` skip rationale for CKV_AZURE_207 / CKV_AZURE_202. |

---

## E. Observability posture

Diagnostics flow to a single LAW per env; backend ingestion uses AAD;
no PII propagates in `allLogs` payloads (FR-047).

| Switch | Prod default | Tofu variable / location | Override mechanism | Enforcement |
|---|---|---|---|---|
| Diagnostic log forwarding | **`category_group = "allLogs"`** on every resource that supports diagnostic settings — no `enabled_metric` block | `module "<r>_diagnostics"` instances of `iac/modules/diagnostic-settings` | n/a — module shape forbids metrics | **BT-IAC-003** policy gate fails the plan on any `azurerm_monitor_diagnostic_setting` carrying an `enabled_metric` block. Originated in Q5c. |
| Log Analytics Workspace retention | **`30` days** (Azure minimum interactive) | `var.log_analytics_retention_days` → `iac/environments/prod/variables.tf:257` | Operators MAY raise per compliance — Azure range 30–730 | Variable validation; threaded into `module.monitoring.retention_in_days`. |
| App Insights ingestion auth (backend) | **AAD** via `APPLICATIONINSIGHTS_AUTHENTICATION_STRING = "Authorization=AAD;ClientId=<workload-uami-client-id>"` | Container Apps env config (composition-level) | n/a — backend defaults to AAD | Workload UAMI receives `Monitoring Metrics Publisher` (FR-033). |
| App Insights `local_authentication_disabled` | **`false`** (MUST remain `false` per Q1c — JS SDK does not support AAD ingestion) | `var.local_authentication_disabled` → `iac/modules/monitoring/variables.tf:39` | Override at composition (would break browser telemetry — see `iac/modules/monitoring/README.md` § Local authentication) | Documented module-level constraint; live verification by T129. |
| PII in `allLogs` categories | **None** (resource-level logs only; application payloads belong to OTel exporters) | Module-level guarantee (`iac/modules/diagnostic-settings/README.md` § FR-047 compliance) | n/a — controlled at exporter layer | FR-047 — application telemetry suppresses PII in app-layer OTel config, separate from this module. |
| Diagnostic destinations | **Single env LAW** (`module.monitoring.log_analytics_workspace_id`) | Composition wiring | Composition-level | Constitutional: all Azure services must route to the solution LAW. |

---

## F. CI posture

Every IaC change passes through the same gauntlet of formatting, validation,
static security scans, policy gates, and (for stateful destroys) a manual
approval job.

| Gate | Prod-relevant behavior | Workflow / file | Override mechanism |
|---|---|---|---|
| `tofu fmt -check -recursive` | Fails on unformatted HCL | `.github/workflows/iac-validate.yml` | Run `tofu fmt -recursive iac/` locally; commit. |
| `tofu validate` | Fails on syntactic / provider validation errors | Same | Fix in code. |
| `tofu plan` (env-scoped, PR comment) | Posts plan summary to PR; output artifact feeds policy + approval gates | Same | n/a — required. |
| `tflint --recursive` | Fails on AVM rule violations | Same | Fix in code; `.tflint.hcl` controls rules. |
| `checkov` (source HCL + tfplan.json) | Fails on misconfiguration findings | Same | Add a skip with rationale in `iac/.checkov.yaml` (reviewer scrutiny). |
| `tfsec` | Fails on misconfiguration findings | Same | n/a — current state has zero findings. |
| `terraform-docs --output-check` | Fails on module-README drift | Same | Locally: `terraform-docs -c iac/.terraform-docs.yml iac` and commit. |
| **BT-IAC-001** — workload UAMI RBAC allowlist (FR-033) | Fails on any role-assignment to the workload UAMI outside the FR-033 enumeration | `iac/policies/run-policies.sh` → `check-rbac.sh` | Add role to FR-033 via a new spec; update allowlist with PR rationale. |
| **BT-IAC-002** — private-by-default for prod | Fails when prod plan provisions a data service with `public_network_access_enabled = true` | Same → `check-public-access.sh` | Override via `terraform.tfvars` is discouraged; gate is hard-fail in prod. |
| **BT-IAC-003** — `allLogs`-only diagnostics | Fails on any `azurerm_monitor_diagnostic_setting` carrying `enabled_metric` | Same → `check-diagnostics.sh` | Use the `iac/modules/diagnostic-settings` wrapper; never inline. |
| **BT-IAC-004** — no inline IAM in env compositions | Fails on inline `azurerm_role_assignment` / `azurerm_user_assigned_identity` / FIC resources in `iac/environments/*/main.tf` | `.github/workflows/iac-validate.yml` → `scripts/lint-iac-inline-iam.sh` | Use `iac/modules/{identity,workload-identity,federated-credential}/` instead. |
| **BT-IAC-005** — no inline credentials | Fails on inline connection strings / account keys / SAS tokens in HCL | Same → `scripts/lint-iac-inline-credentials.sh` | Materialize via KV; reference via Container Apps `secretref`. |
| **BT-IAC-006** — `.terraform.lock.hcl` drift | Fails when CI's `tofu init` produces a lockfile diff that isn't committed | `iac/policies/run-policies.sh` → `check-lockfile.sh` | Commit the lockfile change explicitly. |
| **BT-IAC-007** — stateful destroy manual approval | Writes `requires-manual-approval.flag` when a stateful resource is destroyed; the `iac-stateful-change-approval` job fails until a maintainer re-runs it | `.github/workflows/iac-validate.yml` → `check-stateful-destroys.sh` + approval job | "Re-run failed jobs" by a maintainer after PR description documents the rationale; allowlist entry in `iac/policies/allowlist.json` if recurring. |
| Apply path | Prod apply is **manual** (no auto-apply on push to main) — operators run `iac-apply-prod.yml` (or equivalent) after PR merge | `.github/workflows/iac-apply-dev.yml` is dev-only; prod ships with no auto-apply per spec 005 Q1c | Future ops slice may add a prod apply workflow; until then, prod stand-up uses `quickstart.md` § B. |

---

## How to use this document

- **Reviewing a prod-targeting PR.** Walk this document section-by-section and confirm the PR doesn't relax any column-2 default without an explicit override in the operator-supplied `terraform.tfvars` AND a PR-description rationale.
- **Onboarding a new env (test, future).** Use this document as the prod baseline; deviations for test (e.g., shorter LAW retention, smaller SB capacity) belong in the test composition's `variables.tf` defaults, not as overrides at apply time.
- **Auditing**. Every column-2 default has a single authoritative location (column 3). For "is X enforced in prod?", check the enforcement column. For "how did this get set?", check the variable location.
- **Future hardening.** When a future spec adds a switch (e.g., AMPLS, CMK on Cosmos, double encryption on Service Bus), append a row to the appropriate section and update `tech-stack.md` § 6 if the convention is durable.
