# `test` environment composition (spec 005 template)

**Status**: Template only — NOT applied by spec 005.

This directory ships as part of spec 005's "independently-deployable dev/test/prod" deliverable (User Story 5). The composition is validation-checked in CI (`tofu init -backend=false && tofu validate`) but is never planned or applied as part of this slice's scope (Q1c env scope = dev only).

When an operator is ready to stand `test` up:

1. Follow `specs/005-infrastructure-baseline/quickstart.md` §B end-to-end.
2. The stand-up extends `iac/platform-bootstrap` to add the `test` env (creates the test pipeline managed identity + federated credential, applies the bootstrap RBAC-Admin grant with the spec-005 GUID extension already live).
3. `cd iac/environments/test`, copy `terraform.tfvars.example` to `terraform.tfvars`, fill in real values, then `tofu init -backend-config="key=envs/test/terraform.tfstate"` and `tofu plan` / `apply`.

## Test posture (defaults)

The variable defaults in `variables.tf` are tuned for a test environment:

| Concern | Default | Variable |
|---|---|---|
| Region | `eastus2` | `location` |
| VNet | `10.51.0.0/16` | `network_address_space` |
| Data-services public access | **off** | `data_services_public_access_enabled` |
| Private endpoints | on | `private_endpoints_enabled` |
| AI Search SKU | `standard` (S1) | `ai_search_sku` |
| Service Bus SKU | `Premium`, 1 MU | `service_bus_sku`, `service_bus_capacity` |
| KV purge protection | on | `key_vault_purge_protection_enabled` |
| KV soft-delete | 90 days | `key_vault_soft_delete_retention_days` |
| LAW retention | 30 days | `log_analytics_retention_days` |
| Backend ingress | external (`true`) | `backend_external_ingress` (T134) |

Test mirrors dev's posture for the backend ingress (`backend_external_ingress = true`) so the test workloads are reachable from outside the CAE during stand-up and parity-of-debugging. Flip to `false` via `terraform.tfvars` once the test workloads are stable and external ingress is no longer needed.

## Diverges from dev

- `data_services_public_access_enabled = false` by default (dev opts in per Q2c).
- `ai_search_sku = "standard"`, `service_bus_sku = "Premium"` (dev defaults to cost-optimized basic / Standard).
- `key_vault_purge_protection_enabled = true`, `key_vault_soft_delete_retention_days = 90` (FR-019 hardening; wired into the keyvault module by US7 / T122).
- VNet is `10.51.0.0/16` (dev is `10.50.0.0/16`).
- Backend state lives at `envs/test/terraform.tfstate` (dev is `envs/dev/terraform.tfstate`).
- No `import {}` adoptions or `moved {}` blocks — the test composition has no pre-existing state.

## Destructive-change manual approval

The CI policy gate (BT-IAC-007, US6) emits a `REQUIRES MANUAL APPROVAL` banner if a plan against this env shows a `delete` action against any stateful resource (see `iac/policies/run-policies.sh` and `iac-validate.yml`). Approve only after confirming the destroy is intentional.
