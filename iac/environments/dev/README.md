# `dev` environment composition

**Status**: Live. This is the only env composition that spec 005 actually applies (Q1c env scope = dev only). Test and prod ship as templates and are validation-checked in CI but not planned/applied here.

The live coordinates (subscription, RGs, KV, ACR, ACE FQDNs, Entra app IDs) are tracked in the project memory file `project_dev_environment.md` rather than restated here.

## Dev posture (defaults)

| Concern | Default | Variable |
|---|---|---|
| Region | `eastus2` | `location` |
| VNet | `10.50.0.0/16` | `network_address_space` |
| Data-services public access | **on** (Q2c — until destructive retrofit) | `data_services_public_access_enabled` |
| Private endpoints | on (warm) | `private_endpoints_enabled` |
| AI Search SKU | `basic` | `ai_search_sku` |
| Service Bus SKU | `Standard` (no PE) | `service_bus_sku` |
| KV purge protection | **on** (Azure constraint; see below) | `key_vault_purge_protection_enabled` |
| KV soft-delete | **90 days** (Azure constraint; see below) | `key_vault_soft_delete_retention_days` |
| LAW retention | 30 days | `log_analytics_retention_days` |
| Backend ingress | external (`true`) | `backend_external_ingress` |

### Dev KV purge protection note

The dev KV was provisioned pre-spec-005 with `purge_protection_enabled = true` (the prior module hardcoded it). **Azure does not permit disabling purge protection once enabled**, so dev's `terraform.tfvars` keeps the value at `true` even though the variable default in `variables.tf` is `false` (per the spec's per-env table for fresh dev environments). Changing the tfvars value to `false` would produce an apply-time rejection from the Azure provider — not a destroy/replace. To truly drop purge protection on dev, the KV would need to be destroyed and recreated, which is a separate, deliberate operation gated by BT-IAC-007.

## Destructive-change manual approval gate

Spec 005 / User Story 7 protects the stateful resources in this env from accidental destroy/replace via two layered defenses:

### Layer 1 — `BT-IAC-007` policy gate (primary)

Defined in `contracts/policy-rules.md` §`BT-IAC-007` and implemented by `iac/policies/check-stateful-destroys.sh` + `iac/policies/run-policies.sh`. The CI workflow `.github/workflows/iac-validate.yml` runs the gate on every PR and emits a **`REQUIRES MANUAL APPROVAL`** banner if the plan contains a `delete` (or destroy-replace) action against any resource in the stateful list. `.github/workflows/iac-apply-dev.yml` blocks the apply behind a manual approval step when the gate fires.

To intentionally proceed with a destructive change:

1. Confirm the destroy is intentional and coordinated.
2. Approve the workflow run in the GitHub Actions UI (the apply job is gated on a `production`-style approval environment).
3. Document the rationale in the PR description.

### Layer 2 — `lifecycle { prevent_destroy = true }` (belt-and-suspenders)

The following resources carry `lifecycle { prevent_destroy = true }` in code, so Tofu refuses to plan a `destroy` against them at the planning step (before the policy gate even runs):

| Resource address | File |
|---|---|
| `azurerm_resource_group.this` | `iac/environments/dev/main.tf` |
| `module.cosmos_account.azurerm_cosmosdb_account.this` | `iac/modules/cosmos-account/main.tf` |
| `module.cosmos_canonical_store.azurerm_cosmosdb_sql_database.canonical` | `iac/modules/cosmos-canonical-store/main.tf` |
| `module.monitoring.azurerm_key_vault_secret.app_insights_connection_string[0]` | `iac/modules/monitoring/main.tf` |

### AVM-wrapped stateful resources — relies on Layer 1 only

`lifecycle` blocks cannot be added to a resource that lives inside a child module we don't own. The following stateful resources are owned by Azure Verified Modules and therefore depend on the `BT-IAC-007` gate as their **only** protection:

| Resource | Owning module |
|---|---|
| `azurerm_key_vault.this` | `Azure/avm-res-keyvault-vault/azurerm` (via `iac/modules/keyvault`) |
| `azurerm_container_registry.this` | `Azure/avm-res-containerregistry-registry/azurerm` (via `iac/modules/container-registry`) |
| `azurerm_log_analytics_workspace.this` | `Azure/avm-res-operationalinsights-workspace/azurerm` (via `iac/modules/monitoring`) |
| `azurerm_application_insights.this` | `Azure/avm-res-insights-component/azurerm` (via `iac/modules/monitoring`) |
| `azurerm_user_assigned_identity.workload` | `Azure/avm-res-managedidentity-userassignedidentity/azurerm` (via `iac/modules/identity` / `iac/modules/workload-identity`) |
| `azurerm_container_app_environment.this` | `Azure/avm-res-app-managedenvironment/azurerm` (via `iac/modules/container-apps-env`) |
| `azurerm_storage_account.this` (tfstate) | `Azure/avm-res-storage-storageaccount/azurerm` (via `iac/platform-bootstrap`) |

The `BT-IAC-007` script operates on the planned-resource list directly, so it catches destroy/replace on AVM-owned children regardless of whether the wrapper module exposes a `lifecycle` knob. If/when an AVM release adds a `prevent_destroy` passthrough, this list can shrink.

### Bypassing the layers (intentional teardown)

To intentionally destroy a stateful resource:

1. Open a PR that removes the `lifecycle { prevent_destroy = true }` block from the target resource AND documents the rationale.
2. The PR's plan will still trip `BT-IAC-007`; approve the manual gate in CI with the documented rationale referenced.
3. After the destroy lands, restore the `lifecycle` block in a follow-up PR if the resource is being recreated.

This two-step path is intentional: it forces both a code review (PR for the `lifecycle` removal) and a human approval (the CI gate) before any stateful resource is destroyed.
