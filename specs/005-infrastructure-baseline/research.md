# Phase 0 — Research: Infrastructure Baseline

**Feature**: `005-infrastructure-baseline` | **Date**: 2026-05-25 | **Spec**: [`spec.md`](./spec.md) | **Plan**: [`plan.md`](./plan.md)

This document resolves the technical-context unknowns and best-practice questions raised in `plan.md`. Each entry follows: **Decision → Rationale → Alternatives considered → Source/citation**.

---

## 1. OpenTofu directory layout — `iac/` vs `infra/opentofu/`

**Decision**: Keep the existing `iac/` tree as the canonical location. Extend it with the five new modules (`naming`, `networking`, `ai-search`, `service-bus`, `diagnostic-settings`, plus optional `private-endpoint` and `role-assignments`) and the two new env compositions (`environments/test/`, `environments/prod/`). Do NOT move existing modules or rename `iac/` to `infra/opentofu/`.

**Rationale**:
- The dev environment is live with state keyed to `iac/environments/dev/`. Renaming the directory forces a state-move operation across every resource (Tofu's `moved` blocks help but each requires per-resource enumeration), and would force a rewrite of the cd-dev.yml workflow's working directory.
- The existing 14 modules under `iac/modules/` are tested, working, and shaped by capability — exactly what spec FR-002 mandates.
- The spec's source artifact proposed `infra/opentofu/` as advisory; spec.md Assumptions explicitly defers the choice to the plan phase.

**Alternatives considered**:
- *Rename to `infra/opentofu/`*: Pure cost with no operational benefit. State-move is non-zero risk; cd-dev.yml + every doc reference would need updating. Rejected.
- *Adopt a layered `infra/{network,data,compute,observability}/` decomposition per AWS-style stack pattern*: Adds an unnecessary indirection. The existing per-env composition is already a coherent stack; multi-stack composition adds dependency-graph complexity and `terraform_remote_state` data sources without operational benefit at the current scale.

---

## 2. AVM versus hand-authored modules

**Decision**: Use AVM for net-new resources where a published module covers what we need; keep existing hand-authored modules in place to avoid destructive churn. Specifically:

| Resource | AVM module | Pin | Hand-authored? |
|---|---|---|---|
| Virtual network + subnets | `Azure/avm-res-network-virtualnetwork/azurerm` | `0.16.0` | No (use AVM via thin wrapper `iac/modules/networking/`) |
| Private DNS zone | `Azure/avm-res-network-privatednszone/azurerm` | `0.4.2` | No (use AVM, called per-zone from `iac/modules/networking/`) |
| Private endpoint | Inlined via the per-service AVM module's built-in `private_endpoints` parameter when available; otherwise `azurerm_private_endpoint` resource directly | n/a | Partial — thin `iac/modules/private-endpoint/` wrapper for services whose AVM lacks the parameter |
| Azure AI Search | `Azure/avm-res-search-searchservice/azurerm` | `0.2.0` | No (use AVM via `iac/modules/ai-search/`) |
| Service Bus namespace | `Azure/avm-res-servicebus-namespace/azurerm` | latest 0.x (research pin via `tofu init` against the latest tag at task time; record exact pin in `iac/modules/service-bus/versions.tf`) | No (use AVM via `iac/modules/service-bus/`) |
| Log Analytics workspace | (already provisioned by existing `iac/modules/monitoring/`) | n/a | Yes (retained; one input added: `retention_in_days` already exists as a variable in `monitoring/variables.tf` — currently hardcoded 30 at the env layer; surface as a tf-var per Q5c) |
| Application Insights | (already provisioned by existing `iac/modules/monitoring/`) | n/a | Yes (retained; one input added: `disable_local_auth = false` explicitly — see §6) |
| Key Vault | (already provisioned by existing `iac/modules/keyvault/`) | n/a | Yes (retained; add an optional `private_endpoint` block input for the per-env PE provisioning, even though the dev PE is "warm" — per Q2c networking clarification) |
| Container Apps Environment | (already provisioned by existing `iac/modules/container-apps-env/`) | n/a | Yes (retained; no VNet integration added in this slice per Q2c selective-retrofit; the integration subnet is sized correctly so the future retrofit is a single attribute flip) |
| Container App | (already provisioned by existing `iac/modules/container-app/`) | n/a | Yes (retained; no changes — the KV-secret-reference pattern already implements Q1c hybrid-auth) |
| Container Registry | (already provisioned by existing `iac/modules/container-registry/`) | n/a | Yes (retained; add optional `private_endpoint` block input — not provisioned in dev because the existing ACR is Premium and works fine on public access, but the input is exposed for the test/prod templates) |
| Cosmos DB account | (already provisioned by existing `iac/modules/cosmos-account/`) | n/a | Yes (retained; add optional `private_endpoint` block input — provisioned in dev as a warm PE) |
| User-assigned MI (pipeline) | `Azure/avm-res-managedidentity-userassignedidentity/azurerm` | `0.3.3` (already pinned in `iac/platform-bootstrap/main.tf`) | No (existing) |
| Workload UAMI + RBAC + app-role assignments | (existing `iac/modules/workload-identity/`) | n/a | Yes (retained; extend to accept the new RBAC role set from Q3c) |
| Storage account (tfstate) | `Azure/avm-res-storage-storageaccount/azurerm` | `0.6.3` (already pinned) | No (existing) |

**Rationale**: AVM coverage is good for net-new resources but the existing hand-authored modules encode project-specific patterns (Container Apps + KV secret references, app-role-grant federation, Cosmos canonical-store DB+containers) that an AVM swap would either lose or force a large refactor for. The constitution explicitly permits AVM deviations when "the existing module already encodes project-specific behavior" — captured in `plan.md` Constitution Check.

**Alternatives considered**:
- *Greenfield-replace every existing module with AVM*: Forces a re-state of every dev resource. Massive risk for negligible benefit; rejected per Q2c selective-retrofit clarification.
- *Skip AVM entirely and hand-author networking/Search/SB modules*: Rejects the constitution's "AVM preferred" guidance with no offsetting benefit. The networking + private-DNS-zone + Search + SB AVMs are mature and well-maintained. Rejected.

**Source**: Azure Verified Modules catalog (https://azure.github.io/Azure-Verified-Modules/indexes/terraform/tf-resource-modules/), specific AVM versions cross-referenced against the AI/ML landing-zone pattern AVM (`terraform-azurerm-avm-ptn-aiml-landing-zone` v0.10.0) which is a well-tested composition of the same modules we'll consume.

---

## 3. Service Bus SKU — namespace-only, but Premium for private endpoints

**Decision**: Use **Standard SKU** for the dev Service Bus namespace; specify **Premium SKU** in the `prod` template. Both `test` and `prod` templates carry a `var.sb_sku` input defaulting to `"Premium"`; dev's tfvars overrides to `"Standard"`. The `iac/modules/service-bus/` module accepts the SKU as an input and conditionally creates a private endpoint only when `sku = "Premium"` AND `private_endpoint_enabled = true`.

**Rationale**:
- Spec FR-024 explicitly carves out the SKU caveat: "where the chosen SKU supports it". Microsoft Learn documentation (https://learn.microsoft.com/azure/service-bus-messaging/private-link-service) confirms private endpoints require Premium tier; Standard tier supports neither private endpoints nor VNet service-endpoint rules.
- Cost: Standard SKU is ~$10/mo flat; Premium SKU minimum is ~$667/mo for 1 messaging unit. Dev does not need Premium's resource isolation, customer-managed-key encryption, or partitioning-at-namespace-creation features.
- Q2c says dev gets the full network topology with public access on. For Service Bus, "the full network topology" means: namespace exists, RBAC is wired (no SAS), diagnostic settings forward `allLogs`, but **no private endpoint** because the SKU doesn't support it. This is a documented exception to the "PEs in dev are warm" pattern — covered in the spec's FR-024 SKU caveat.
- Standard tier supports topics/subscriptions and managed-identity auth via Azure RBAC — sufficient for everything downstream specs will need before they have a reason to upgrade to Premium.

**Alternatives considered**:
- *Premium in dev for parity*: Cost-prohibitive (~$667/mo for dev) and unnecessary for the messaging volume any near-term spec will exercise. Rejected per NFR-003.
- *Basic SKU in dev*: Doesn't support topics/subscriptions. Rejected because future specs may need topics (the spec doesn't ship them but the namespace SKU must support them being added).

**Source**: https://learn.microsoft.com/azure/service-bus-messaging/service-bus-premium-messaging#network-security; https://learn.microsoft.com/azure/service-bus-messaging/private-link-service.

---

## 4. Azure AI Search SKU

**Decision**: Use **`basic` SKU** for dev, **`standard`** (S1) for test, **`standard`** (S1) for prod template. SKU is parameterized via `var.ai_search_sku` per env composition.

**Rationale**:
- `basic` (and above) supports private endpoints, AAD/RBAC authentication, and the index/document features any near-term spec will need. `free` SKU does NOT support private endpoints or RBAC.
- Production-compatible topology preserved per NFR-003: same module shape, same RBAC pattern, same diagnostic wiring; only the SKU differs.
- Standard (S1) is the minimum production-grade SKU. Higher tiers (S2, S3) reserve capacity for high-throughput scenarios that this baseline doesn't justify pre-spec-008 (search projection).

**Alternatives considered**:
- *`free` in dev*: No private endpoints + no RBAC; would force public + admin-key access. Violates FR-016. Rejected.
- *`standard` (S1) in dev*: Roughly 4x cost of `basic`. Rejected as premature.

---

## 5. Cosmos DB capacity model

**Decision**: Keep the existing dev Cosmos DB account provisioned by spec 004 (capacity model already decided there). For the test/prod templates, document `var.cosmos_offer_type` (default `"Standard"`) and rely on the canonical-store module's per-container throughput settings. **This slice does not change Cosmos DB capacity provisioning** — it adopts the existing account and only adds a private endpoint, additional RBAC grants, and a diagnostic settings binding (already in place via the existing `iac/modules/cosmos-account/`).

**Rationale**:
- The Cosmos DB workload-identity Data Contributor grant from Q3c is **already in place** in the dev composition (lines 432–451 of `iac/environments/dev/main.tf` from spec 004). No change needed for dev.
- Capacity model questions (serverless vs. autoscale vs. provisioned throughput) are owned by spec 004 — outside this slice.
- The only Cosmos-specific change in this slice is adding the private endpoint (warm in dev per Q2c) and confirming the diagnostic setting routes `allLogs` only (already does via the existing `iac/modules/cosmos-account/`'s `log_analytics_workspace_id` parameter — verify category list at task time).

**Alternatives considered**:
- *Reprovision Cosmos under a new module shape*: Would destroy the canonical store. Rejected per Q2c selective retrofit and spec 004 dependency.

---

## 6. Application Insights ingestion auth — hybrid (AAD for backend, connection-string for browser)

**Decision**: Configure the App Insights resource with `local_authentication_disabled = false` (i.e., local auth REMAINS enabled). Configure the backend Container App with `APPLICATIONINSIGHTS_AUTHENTICATION_STRING = "Authorization=AAD;ClientId=<workload-MI-client-id>"` plus the existing `APPLICATIONINSIGHTS_CONNECTION_STRING` (sourced from KV via the existing Container Apps secret reference). Grant the workload UAMI the `Monitoring Metrics Publisher` role scoped to the App Insights resource. The frontend Container App's NEXT_PUBLIC_APPINSIGHTS_CONNECTION_STRING continues to flow from KV → Container Apps secret → env var → browser, unchanged. The connection string also continues to be needed by the backend's .NET OpenTelemetry exporter to identify the target resource.

**Rationale**:
- Microsoft Learn explicitly lists the **Application Insights JavaScript SDK as unsupported** for Microsoft Entra authentication (https://learn.microsoft.com/azure/azure-monitor/app/azure-ad-authentication#unsupported-scenarios). Disabling local auth would break browser telemetry. This is the exact contradiction Q1c resolved.
- Microsoft Learn confirms the `Monitoring Metrics Publisher` role applies to **all** telemetry despite its name, and the AAD-backed config uses `APPLICATIONINSIGHTS_AUTHENTICATION_STRING` ALONGSIDE the connection string (not as a replacement). Token audience is `https://monitor.azure.com`.
- The existing `iac/environments/dev/main.tf` already stores the conn string in KV and surfaces it via Container Apps secret reference (lines 127–145, 244–250, 282–288). The Q1c clarification is satisfied by these existing patterns + adding the `Monitoring Metrics Publisher` role assignment + setting `APPLICATIONINSIGHTS_AUTHENTICATION_STRING` on the backend.

**Alternatives considered**:
- *Disable local auth entirely*: Breaks browser ingestion (per MS Learn). Rejected.
- *Connection string as plain (non-KV) output*: Conflicts with the user's explicit Q1c follow-up instruction to keep the KV-secret-reference path. The browser exposure happens at the Container App env-var injection step, not at the IaC output layer. Rejected.

**Source**: https://learn.microsoft.com/azure/azure-monitor/app/azure-ad-authentication; https://learn.microsoft.com/azure/azure-functions/configure-monitoring#enable-application-insights-integration (which documents the `APPLICATIONINSIGHTS_AUTHENTICATION_STRING` format).

---

## 7. Diagnostic-settings convention — `allLogs` only, no metrics

**Decision**: Implement a `iac/modules/diagnostic-settings/` thin wrapper around `azurerm_monitor_diagnostic_setting` that:
- Accepts `target_resource_id`, `log_analytics_workspace_id`, `name`
- Emits exactly one `enabled_log { category_group = "allLogs" }` block
- Emits **NO** `enabled_metric` block (Q5c clarification: metrics remain in Azure Monitor's native metric store)

Refactor existing inline `azurerm_monitor_diagnostic_setting` blocks (lines 298–316 of `iac/environments/dev/main.tf`) to use this module and **remove the `enabled_metric { category = "AllMetrics" }` blocks** — they violate Q5c.

**Rationale**:
- The Q5c clarification is explicit. Removing the existing `AllMetrics` forwarding is a small change with one user-visible consequence: any Log Analytics query previously joining against `AzureMetrics` from these resources will return no rows after the change. None of the existing CI smoke tests rely on this data; impact is zero for the active toolchain.
- A centralized wrapper module guarantees the convention propagates uniformly across every new diagnostic setting added by this slice (and by every subsequent slice).

**Alternatives considered**:
- *Per-resource `azurerm_monitor_diagnostic_setting` blocks*: Allows drift — easy to forget the `category_group = "allLogs"` convention or accidentally include an `enabled_metric` block. Rejected.
- *Keep the existing `AllMetrics` forwarding for backward compatibility*: Directly contradicts Q5c. Rejected.

---

## 8. Log Analytics retention — 30-day default exposed as tf-var

**Decision**: Existing `iac/modules/monitoring/variables.tf` already exposes `retention_in_days` with a default of 30. Surface this through the env composition by adding `variable "log_analytics_retention_days"` (default `30`, validation `>= 30 && <= 730` per Azure constraints) to `iac/environments/dev/variables.tf` and propagating it to `module.monitoring`. Carry the same variable into the test/prod template `variables.tf`. Per Q5c, all three envs default to 30 — operators override per env if compliance requires longer.

**Rationale**:
- Q5c is explicit. The existing module already has the parameter; the work is just plumbing it through the env composition.
- Azure Log Analytics minimum retention is 30 days (free); maximum interactive retention is 730 days. Beyond that requires archive tier configuration — out of scope.

**Alternatives considered**:
- *Hardcode 30 in the env composition (no var)*: Q5c explicitly requires the value to be a tf-var. Rejected.

---

## 9. Per-environment deployment managed identity model

**Decision**: Leverage the existing `iac/platform-bootstrap/main.tf` `for_each = toset(var.environments)` shape — adding test and prod is a tfvars change. To stand up test/prod identities:

1. Extend `iac/platform-bootstrap/variables.tf` so `var.environments` accepts `["dev", "test", "prod"]` (it already does — only the tfvars needs updating when test/prod operators come online; this slice does NOT change the tfvars).
2. Extend the RBAC-Admin condition in `iac/platform-bootstrap/main.tf` (line 211–216) to include the new role GUIDs the workload identity now receives per Q3c:
   - Cosmos DB Built-in Data Contributor — `00000000-0000-0000-0000-000000000002` (data-plane role, granted via `azurerm_cosmosdb_sql_role_assignment`; subscription RBAC-Admin condition does NOT govern this path — so it is not added to the condition list).
   - Azure Service Bus Data Sender — `69a216fc-b8fb-44d8-bc22-1f3c2cd27a39`
   - Azure Service Bus Data Receiver — `4f6d3b9b-027b-4f4c-9142-0e5a2a2247e0`
   - Search Index Data Contributor — `8ebe5a00-799e-43f5-93ac-243d3dce84a7`
   - Monitoring Metrics Publisher — `3913510d-42f4-4e42-8a64-420c390055eb`

3. Per-env federated credential subjects already use the `repo:${var.github_org_repo}:environment:${each.key}` pattern.

**Rationale**:
- The existing shape already implements Q4c (per-env identity via `for_each` over env names). The slice's work is extending the RBAC-Admin condition GUID allowlist so the pipeline MI can assign the new workload roles during apply.
- This is the only Q4c-relevant change in the bootstrap stack. The new env identities themselves don't get provisioned in this slice (Q1c env scope = dev only); they'll be provisioned by a follow-up slice when test/prod are stood up.

**Alternatives considered**:
- *Move per-env MIs out of bootstrap into per-env compositions*: Cleaner separation, but the bootstrap is the right place because pipeline MIs need to exist before the per-env composition runs (the composition is what the pipeline MI is running). Rejected.
- *Don't pre-extend the RBAC-Admin condition; add roles ad-hoc when test/prod stand up*: Defeats the Q3c forward-looking principle and forces a bootstrap change in the test/prod standup slice. Rejected.

---

## 10. Network address space and subnet sizing

**Decision**: Allocate `10.50.0.0/16` to dev, `10.51.0.0/16` to test, `10.52.0.0/16` to prod. Subnet layout per environment:

| Subnet | CIDR | Purpose | Notes |
|---|---|---|---|
| `snet-cae-integration` | `10.5x.0.0/23` (512 IPs) | Container Apps Environment integration subnet (for the future retrofit) | Container Apps Environment requires a `/23` minimum for Consumption-only and `/27` for Workload Profiles. The `/23` is the safer choice and matches AVM patterns. Delegated to `Microsoft.App/environments`. |
| `snet-private-endpoints` | `10.5x.2.0/24` (256 IPs) | All private endpoints for data services | Generous room for future PE additions. Service endpoints disabled (PEs only). |
| `snet-jumpbox` (reserved, not deployed) | `10.5x.3.0/27` (32 IPs) | Future Bastion/jumpbox subnet | Reserved CIDR only — no resources provisioned in this slice. |

The remaining CIDR space (`10.5x.4.0/22` and beyond) is reserved for future subnets without re-IP'ing existing resources.

**Rationale**:
- `/16` per env gives ample room for growth without IP exhaustion.
- The `/23` integration subnet matches Azure Container Apps Environment minimum. The current dev CAE is NOT VNet-integrated; once the deferred retrofit lands, the integration subnet will already be sized correctly.
- The `/24` PE subnet is 256 IPs — enough for ~20 private endpoints today and unlimited expansion via the reserved IP space.
- The `10.50.x.x` private range avoids common defaults (`10.0.x.x`, `10.1.x.x`) that may collide with existing on-prem ranges if BusTerminal is later peered with corporate networks.

**Alternatives considered**:
- *IPAM allocation via the AVM IPAM pool pattern*: Adds complexity (Azure IPAM is a separate service). Static allocation is fine at this scale; rejected as premature.
- *Tighter subnet sizing*: A `/26` PE subnet (64 IPs) would suffice today, but the `/24` costs nothing extra and prevents a future re-IP when more services land. Rejected.

---

## 11. Private DNS zones

**Decision**: Provision one private DNS zone per service type in dev (and per-env-template for test/prod), all linked to the env's VNet:

| Service | Private DNS Zone | PE Sub-resource |
|---|---|---|
| Key Vault | `privatelink.vaultcore.azure.net` | `vault` |
| Cosmos DB SQL | `privatelink.documents.azure.com` | `Sql` |
| Azure AI Search | `privatelink.search.windows.net` | `searchService` |
| Service Bus (Premium only) | `privatelink.servicebus.windows.net` | `namespace` |
| Container Registry | `privatelink.azurecr.io` | `registry` (not provisioned in dev; carried in test/prod template) |
| Storage (tfstate, if PE-ing in future) | `privatelink.blob.core.windows.net` | `blob` (carried in test/prod template only) |
| Application Insights / Log Analytics (Azure Monitor Private Link Scope path is more involved) | `privatelink.monitor.azure.com` + several others | (deferred — see §14) |

In dev, the Service Bus zone IS provisioned and linked to the VNet but NO PE is created (Standard SKU). When the SKU is upgraded to Premium later, the zone is already in place.

**Rationale**:
- Each Azure PaaS service has a specific privatelink zone name; using the wrong one breaks resolution.
- VNet-zone link is a one-time per-env operation; doing it in this slice means future PE additions are a single resource change.

**Source**: https://learn.microsoft.com/azure/private-link/private-endpoint-dns (canonical mapping).

---

## 12. Pipeline RBAC-Admin condition extension

**Decision**: Extend the existing condition in `iac/platform-bootstrap/main.tf` (lines 205–217) to include:

```
@Request[Microsoft.Authorization/roleAssignments:RoleDefinitionId] ForAnyOfAnyValues:GuidEquals {
  7f951dda-4ed3-4680-a7ca-43fe172d538d,  # AcrPull (existing)
  4633458b-17de-408a-b874-0445c86b69e6,  # Key Vault Secrets User (existing)
  b86a8fe4-44ce-4948-aee5-eccb2c155cd7,  # Key Vault Secrets Officer (existing)
  69a216fc-b8fb-44d8-bc22-1f3c2cd27a39,  # Azure Service Bus Data Sender (new)
  4f6d3b9b-027b-4f4c-9142-0e5a2a2247e0,  # Azure Service Bus Data Receiver (new)
  8ebe5a00-799e-43f5-93ac-243d3dce84a7,  # Search Index Data Contributor (new)
  3913510d-42f4-4e42-8a64-420c390055eb   # Monitoring Metrics Publisher (new)
}
```

Cosmos DB Data Contributor is NOT added — it's granted via `azurerm_cosmosdb_sql_role_assignment` (Cosmos-native RBAC), not `azurerm_role_assignment` (Azure RBAC), so the subscription RBAC-Admin condition does NOT govern it. The pipeline MI's existing subscription-Contributor grant is what permits the Cosmos data-plane assignment.

**Rationale**:
- Forward-looking RBAC per Q3c requires the pipeline to be able to assign these roles during apply.
- The condition list is the privilege-escalation guardrail (preventing the pipeline from assigning Owner). Extending it deliberately, with each role enumerated and commented, preserves that guardrail.

---

## 13. Provider versions and lockfile

**Decision**:
- `hashicorp/azurerm ~> 4.0` (already pinned in `iac/platform-bootstrap/main.tf`; new env compositions match)
- `hashicorp/azuread ~> 3.0` (already pinned)
- `hashicorp/time ~> 0.12` (already pinned)
- `hashicorp/random ~> 3.6` (new; AVM modules require it)
- `azure/azapi ~> 2.4` (new; AVM modules require it for some resources)
- `azure/modtm ~> 0.3` (new; AVM telemetry — `enable_telemetry = false` everywhere, so it's a no-op but the provider must resolve)

Commit `.terraform.lock.hcl` per env composition AND per module. CI fails if the lockfile changes during `tofu init` without an explicit commit.

**Rationale**: Pinning matches the constitution mandate and prevents non-deterministic plans across contributors and CI.

---

## 14. Azure Monitor Private Link Scope (AMPLS) — deferred

**Decision**: Do NOT provision an Azure Monitor Private Link Scope in this slice. Telemetry ingestion to App Insights and Log Analytics continues over public endpoints (with AAD auth for backend per §6, ingestion key for browser).

**Rationale**:
- AMPLS adds significant complexity (multiple private DNS zones, scope resource, separate "ingestion mode" decisions) and isn't required to meet spec FR-027 / NFR-001.
- Browser ingestion would still need a public path because the JavaScript SDK doesn't support Microsoft Entra auth — so AMPLS would only protect backend ingestion. The cost/complexity trade-off doesn't justify it at this slice.
- A future ops-hardening slice can add AMPLS once a stronger reason (compliance audit, network-egress policy) materializes.

**Alternatives considered**:
- *Provision AMPLS for backend ingestion only*: Cost/complexity not justified at this stage. Rejected.

---

## 15. Workload identity Cosmos DB role grant for the canonical-store database

**Decision**: The existing `azurerm_cosmosdb_sql_role_assignment.workload_data_contributor` (line 432) is exactly Q3c's required state. No change needed in the dev composition for this. **However**, the role is currently scoped to a per-database path constructed inline (line 429). Refactor this scope-build into a small helper local in `iac/modules/cosmos-canonical-store/` so the path-construction trap (ARM vs data-plane path form — see the existing comment at lines 422–427) doesn't get re-rediscovered by a future spec.

**Rationale**: The trap is real (HTTP 400 from the Cosmos provider on the ARM path form) and the existing comment is buried in env composition. Surfacing it as a module output (`canonical_database_role_scope`) prevents repeats.

---

## 16. CI policy gate scripts — bash + jq, run against `tofu show -json tfplan`

**Decision**: Implement the custom policy gate (FR-044) as bash + jq scripts under `iac/policies/`:

- `check-tags.sh`: parses tfplan JSON; asserts every taggable resource type has all 5 mandatory tags (`application`, `environment`, `managed-by`, `cost-center`, plus one of `owner` or `team`).
- `check-public-access.sh`: asserts no resource in a "prod" env composition has `public_network_access_enabled = true` (or equivalent per-service property) UNLESS the resource is explicitly listed in `iac/policies/allowed-public-prod.json` (which is empty by default and changes require PR review).
- `check-diagnostics.sh`: asserts every supported platform resource has a corresponding `azurerm_monitor_diagnostic_setting` with `category_group = "allLogs"` and NO `enabled_metric` block.
- `check-rbac-scope.sh`: asserts no `azurerm_role_assignment` has a subscription-level scope UNLESS its principal_id matches the documented pipeline-MI exception (Complexity Tracking entry in plan.md).
- `run-policies.sh`: orchestrator; runs all checks, exits non-zero on any failure, prints findings as a Markdown table for CI comment.

Use bash + jq (already available on GitHub-hosted runners) rather than adopting a new policy tool (OPA/Conftest, Checkov custom rules) — keep the dependency surface small.

**Rationale**:
- Operational simplicity (Decision Priorities §1). bash + jq is already used elsewhere in the repo; OPA/Conftest is more powerful but introduces a new toolchain and a new language (Rego) for one slice's worth of checks.
- The checks themselves are simple boolean assertions over JSON; jq handles them cleanly.
- If the rule set grows complex enough to warrant Rego (e.g., 20+ rules with cross-resource constraints), a later spec can refactor.

**Alternatives considered**:
- *Checkov custom checks*: Checkov is already in the toolchain (FR-044). But Checkov custom checks are Python, scoped per resource type, and harder to express "this resource type must have a sibling diagnostic-settings resource" cross-cuts. Built-in Checkov rules are reused for what they cover; custom rules live in the bash gate. Rejected as the primary mechanism.
- *Open Policy Agent (Conftest)*: Rejected — new toolchain for one slice's needs.

---

## 17. Region selection

**Decision**: Dev stays in `eastus2` (existing). Test template defaults to `eastus2` (cheapest path; sub-region resilience is not in scope). Prod template defaults to `centralus` to provide regional diversity from dev/test (mitigates correlated-region failure for the canonical state store and platform). All region choices are tf-vars; operators override per-env if needed.

**Rationale**:
- `eastus2` and `centralus` both support every Azure service this baseline provisions (verified via the Azure Service Availability table; AI Search, Service Bus Premium, Cosmos DB, all available in both).
- Spec NFR-005 explicitly requires region-parameterized modules — already true of the existing modules.

**Alternatives considered**:
- *All envs in `eastus2`*: Higher correlated-failure risk for prod. Rejected for the prod template default.
- *Multi-region prod (active-active)*: Out of scope (Cosmos DB geo-replication, Service Bus Geo-DR, AMPLS — all add complexity). Rejected per spec Out of Scope ("Alerts, dashboards, SLOs, or runbook automation; only the underlying observability resources..." implies single-region operability is fine).

---

## 18. Test/prod composition templates — structure and apply gating

**Decision**: Create `iac/environments/test/` and `iac/environments/prod/` directories containing:

- `main.tf` — calls the same modules as dev with env-appropriate inputs
- `variables.tf` — copy of dev's, with sensible defaults for env-tier-appropriate SKUs (Premium SB, Standard S1 Search) and prod-hardening defaults (public access OFF on data services, purge protection ON for KV)
- `outputs.tf` — same shape as dev's
- `providers.tf` — same providers as dev with env-specific subscription ID
- `backend.tf` — same storage account, different `key` (`envs/test/terraform.tfstate`, `envs/prod/terraform.tfstate`)
- `terraform.tfvars.example` — env-appropriate sample values (no real secrets, no real subscription IDs)

CI workflow runs `tofu init && tofu validate` against both test and prod compositions on every PR but does NOT run `tofu plan` against them (they have no state yet; plan would just be a create-everything-from-empty which adds noise without value). Validation-only is the Q1c "templates are verifiable" guarantee.

**Rationale**:
- The directory-template-only approach satisfies the Q1c clarification cleanly.
- Validation in CI catches syntax/provider-config errors without needing real Azure state.
- When operators do stand up test/prod, the path is: pre-extend `iac/platform-bootstrap/var.environments` (already supports it), populate the new env's tfvars, run `tofu init -backend-config="key=envs/test/terraform.tfstate"`, then `tofu plan`/`apply`.

---

## 19. Local development apply path

**Decision**: Document a `quickstart.md` operator path:
1. `az login` (developer Entra account)
2. `cd iac/environments/dev`
3. `tofu init` (uses lockfile pin)
4. `tofu plan -var-file=terraform.tfvars`
5. (review plan; expect zero destroys against any stateful resource)
6. `tofu apply` (manual confirmation)

Local apply uses the developer's `az` principal, NOT the pipeline MI. Per the existing operator KV grant pattern, the developer must already hold the `KV Secrets Officer` role on the env KV (via the `kv_operator_object_ids` mechanism) AND the pipeline-MI's subscription-Contributor / RBAC-Admin grants must be in place (already true for dev).

**Rationale**: Local development convenience is required by NFR / open-source community readiness; the existing `kv_operator_object_ids` mechanism already supports developer-local applies in dev.

**Alternatives considered**:
- *Force all applies through the pipeline*: Slower iteration loop for IaC work. Rejected; local dev applies are explicitly supported by the existing pattern.

---

## 20. Out-of-scope confirmations (deliberate non-decisions)

These items were considered and explicitly DEFERRED to a later slice:

- Container Apps Environment VNet integration (per Q2c selective retrofit deferral)
- Key Vault private endpoint **activation** (PE is provisioned warm in dev; switching the workload to resolve it requires DNS path testing — deferred along with CAE VNet integration)
- Azure Monitor Private Link Scope (per §14)
- Cosmos DB capacity model changes (per §5)
- Service Bus topics/queues (per spec Q3 from /specify clarifications)
- Multi-region prod active-active (per §17)
- Service-endpoint policies, NSG rules, route tables, VNet peering (no inter-VNet traffic in this baseline)
- Container Registry private endpoint in dev (per §2 — exposed as input but not provisioned in dev)
- Geo-DR (Cosmos / Service Bus / Storage) — explicitly Out of Scope per spec

---

## Summary of net-new vs adopted-in-place

| Net-new in this slice (dev) | Adopted-in-place (no change) |
|---|---|
| `iac/modules/naming/` | `iac/modules/keyvault/` |
| `iac/modules/networking/` (VNet + subnets + private DNS zones + zone-VNet links) | `iac/modules/monitoring/` (one new tf-var for retention) |
| `iac/modules/ai-search/` | `iac/modules/container-apps-env/` |
| `iac/modules/service-bus/` | `iac/modules/container-app/` |
| `iac/modules/diagnostic-settings/` | `iac/modules/container-registry/` |
| `iac/modules/private-endpoint/` (wrapper) | `iac/modules/cosmos-account/` (one optional PE input added) |
| `iac/environments/test/` (template) | `iac/modules/cosmos-canonical-store/` |
| `iac/environments/prod/` (template) | `iac/modules/workload-identity/` (extend RBAC inputs) |
| `iac/policies/` (CI policy gate scripts) | `iac/modules/federated-credential/` |
| `iac/scripts/apply-env.sh` (operator helper) | `iac/modules/identity/`, `iac/modules/app-registration-roles/`, `iac/modules/graph-permissions/`, `iac/modules/probe-job-internal-caller/` |
| New env-level resources in dev: VNet, subnets, private DNS zones, AI Search, Service Bus, Cosmos PE, KV PE (warm), Search PE, additional RBAC (per Q3c list), `Monitoring Metrics Publisher` for workload UAMI | `iac/platform-bootstrap/` (one condition-list extension only) |
| Removed: `enabled_metric { category = "AllMetrics" }` blocks on Container Apps diagnostics (per Q5c) | All existing dev resources (no destroys planned) |

---

*All NEEDS CLARIFICATION items are resolved. Phase 1 may proceed.*
