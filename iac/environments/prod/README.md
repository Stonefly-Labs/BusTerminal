# `prod` environment composition (spec 005 template)

**Status**: Template only — NOT applied by spec 005.

This directory ships as part of spec 005's "independently-deployable dev/test/prod" deliverable (User Story 5). The composition is validation-checked in CI (`tofu init -backend=false && tofu validate`) but is never planned or applied as part of this slice's scope (Q1c env scope = dev only).

When an operator is ready to stand `prod` up:

1. Follow `specs/005-infrastructure-baseline/quickstart.md` §B end-to-end.
2. The stand-up extends `iac/platform-bootstrap` to add the `prod` env (creates the prod pipeline managed identity + federated credential, applies the bootstrap RBAC-Admin grant with the spec-005 GUID extension already live).
3. `cd iac/environments/prod`, copy `terraform.tfvars.example` to `terraform.tfvars`, fill in real values, then `tofu init -backend-config="key=envs/prod/terraform.tfstate"` and `tofu plan` / `apply`.

## Prod posture (defaults)

The variable defaults in `variables.tf` are tuned for a production environment:

| Concern | Default | Variable |
|---|---|---|
| Region | **`centralus`** (regional diversity from dev/test) | `location` |
| VNet | `10.52.0.0/16` | `network_address_space` |
| Data-services public access | **off** | `data_services_public_access_enabled` |
| Private endpoints | on | `private_endpoints_enabled` |
| AI Search SKU | `standard` (S1) | `ai_search_sku` |
| Service Bus SKU | `Premium`, 1 MU | `service_bus_sku`, `service_bus_capacity` |
| KV purge protection | **on** (FR-019) | `key_vault_purge_protection_enabled` |
| KV soft-delete | **90 days** (FR-019) | `key_vault_soft_delete_retention_days` |
| LAW retention | 30 days (raise per compliance) | `log_analytics_retention_days` |
| **Backend ingress** | **internal (`false`)** (FR-010) | `backend_external_ingress` (T134) |

The backend Container App's `ingress.external_enabled` defaults to `false` in prod per **FR-010** — the backend is not exposed to the public internet by default. Do not flip this to `true` without a documented justification: the CI policy gate (US6) expects prod to keep the backend off the public internet.

## Diverges from dev / test

- Region is `centralus` (research §17 — regional diversity from dev/test's `eastus2`).
- VNet is `10.52.0.0/16`.
- Backend ingress defaults to internal (`false`); test/dev default external (`true`).
- All hardening defaults match test (private-by-default, Premium SB, Standard Search, KV purge protection ON, 90-day soft-delete).
- Backend state lives at `envs/prod/terraform.tfstate`.
- No `import {}` adoptions or `moved {}` blocks — the prod composition has no pre-existing state.

## Destructive-change manual approval

The CI policy gate (BT-IAC-007, US6) emits a `REQUIRES MANUAL APPROVAL` banner if a plan against this env shows a `delete` action against any stateful resource (see `iac/policies/run-policies.sh` and `iac-validate.yml`). For prod, this gate is non-negotiable — approve only after confirming the destroy is intentional and coordinated.
