# Configuration Profile Schema

**Feature**: `005-infrastructure-baseline` | **Date**: 2026-05-25

The binding variable schema every env composition (`iac/environments/<env>/variables.tf`) MUST declare. New variables added by this slice are listed; existing variables (preserved from spec 002/004) are noted.

---

## Variables

### Existing (preserved unchanged)

| Variable | Type | Required | Notes |
|---|---|---|---|
| `subscription_id` | string | yes | Existing |
| `environment_name` | string | yes (default `"dev"` in dev composition) | Existing |
| `location` | string | yes (default `"eastus2"` in dev) | Existing |
| `naming_prefix` | string | yes (default `"bt-dev"` in dev) | Existing |
| `unique_suffix` | string | yes | Existing; 4–12 alphanumeric |
| `github_org_repo` | string | yes | Existing |
| `frontend_image` | string | yes | Existing |
| `backend_image` | string | yes | Existing |
| `frontend_min_replicas` | number | no (default `0`) | Existing |
| `frontend_max_replicas` | number | no (default `3`) | Existing |
| `backend_min_replicas` | number | no (default `0`) | Existing |
| `backend_max_replicas` | number | no (default `3`) | Existing |
| `entra_tenant_id` | string | yes | Existing |
| `entra_api_client_id` | string | yes | Existing |
| `entra_web_client_id` | string | yes | Existing |
| `tags` | map(string) | no (default `{}`) | Existing |
| `platform_role_ids` | object | yes | Existing |
| `probe_job_enabled` | bool | no (default `false`) | Existing |
| `canonical_db_name` | string | no (default `"busterminal-canonical"`) | Existing (spec 004) |
| `kv_operator_object_ids` | list(string) | no (default `[]`) | Existing (spec 003 retrofit) |

### NEW in this slice

| Variable | Type | Required | Default | Validation | Notes |
|---|---|---|---|---|---|
| `network_address_space` | list(string) | yes | dev: `["10.50.0.0/16"]` | All entries valid CIDR | Per research §10 |
| `subnet_integration_cidr` | string | yes | dev: `"10.50.0.0/23"` | `/23` or larger; inside `network_address_space` | CAE integration subnet |
| `subnet_private_endpoints_cidr` | string | yes | dev: `"10.50.2.0/24"` | `/24` recommended; inside `network_address_space`; non-overlapping with integration | |
| `data_services_public_access_enabled` | bool | yes | dev: `true`; test/prod: `false` | n/a | Q2c networking clarification: dev opts into public access until the destructive retrofit follow-up lands |
| `private_endpoints_enabled` | bool | yes | `true` (all envs) | n/a | Per Q2c, dev provisions PEs warm — kept-on by default |
| `ai_search_sku` | string | yes | dev: `"basic"`; test/prod: `"standard"` | one of `free`, `basic`, `standard`, `standard2`, `standard3` | Per research §4 |
| `service_bus_sku` | string | yes | dev: `"Standard"`; test/prod: `"Premium"` | one of `Standard`, `Premium` (Basic rejected) | Per research §3 |
| `service_bus_capacity` | number | conditional | n/a (required when `service_bus_sku = "Premium"`) | one of 1, 2, 4, 8, 16 | Premium-only |
| `key_vault_purge_protection_enabled` | bool | yes | dev: `false`; test/prod: `true` | n/a | FR-019 |
| `key_vault_soft_delete_retention_days` | number | yes | dev: `7`; test/prod: `90` | 7–90 | |
| `log_analytics_retention_days` | number | yes | `30` (all envs per Q5c) | 30–730 | Q5c clarification |

---

## Profile validation cross-cuts

These constraints span multiple variables and are enforced via `precondition` blocks in the env composition's root module:

1. `subnet_integration_cidr` AND `subnet_private_endpoints_cidr` MUST each be inside one of the `network_address_space` CIDRs. Computed via `cidrhost()` and CIDR-math helpers.
2. `subnet_integration_cidr` AND `subnet_private_endpoints_cidr` MUST NOT overlap.
3. `service_bus_sku = "Premium"` requires `service_bus_capacity != null`.
4. `service_bus_sku = "Standard"` AND `private_endpoints_enabled = true` is ACCEPTABLE (the SB PE is silently skipped; documented in research §3). The module-level validation makes this explicit so no operator is surprised.
5. If `data_services_public_access_enabled = false` AND `private_endpoints_enabled = false`, the env composition emits a `precondition` failure — workloads would be unable to reach any data service.

---

## `terraform.tfvars.example` templates

### Dev (`iac/environments/dev/terraform.tfvars.example`)

```hcl
subscription_id     = "08b37dc0-0011-4841-84c0-0349a5c65883"
environment_name    = "dev"
location            = "eastus2"
naming_prefix       = "bt-dev"
unique_suffix       = "chdev01"
github_org_repo     = "Stonefly-Labs/BusTerminal"

# Images supplied by the CD pipeline at apply time; placeholders here.
frontend_image = "acrbtdevchdev01.azurecr.io/busterminal/web:placeholder"
backend_image  = "acrbtdevchdev01.azurecr.io/busterminal/api:placeholder"

entra_tenant_id      = "596c1564-6e95-4c35-a80b-2dbe45a162f3"
entra_api_client_id  = "9fb329a3-7b5b-4fdf-a46a-71f7df1d6716"
entra_web_client_id  = "84ca372d-8d45-4527-967f-868a3336985b"

platform_role_ids = {
  admin     = "<uuid>"
  operator  = "<uuid>"
  reader    = "<uuid>"
  developer = "<uuid>"
}

# NEW in spec 005
network_address_space            = ["10.50.0.0/16"]
subnet_integration_cidr          = "10.50.0.0/23"
subnet_private_endpoints_cidr    = "10.50.2.0/24"
data_services_public_access_enabled = true     # dev opts in per Q2c
private_endpoints_enabled           = true     # warm PEs in dev
ai_search_sku                     = "basic"
service_bus_sku                   = "Standard"
# service_bus_capacity            = null       # not required for Standard
key_vault_purge_protection_enabled  = false
key_vault_soft_delete_retention_days = 7
log_analytics_retention_days        = 30
```

### Test (`iac/environments/test/terraform.tfvars.example`)

```hcl
subscription_id     = "<test-subscription-id>"
environment_name    = "test"
location            = "eastus2"
naming_prefix       = "bt-test"
unique_suffix       = "<unique-suffix>"
github_org_repo     = "Stonefly-Labs/BusTerminal"

frontend_image = "<test-acr>.azurecr.io/busterminal/web:placeholder"
backend_image  = "<test-acr>.azurecr.io/busterminal/api:placeholder"

entra_tenant_id      = "<test-tenant-id>"
entra_api_client_id  = "<test-api-client-id>"
entra_web_client_id  = "<test-web-client-id>"

platform_role_ids = { admin = "<uuid>", operator = "<uuid>", reader = "<uuid>", developer = "<uuid>" }

network_address_space            = ["10.51.0.0/16"]
subnet_integration_cidr          = "10.51.0.0/23"
subnet_private_endpoints_cidr    = "10.51.2.0/24"
data_services_public_access_enabled = false    # test ships with private-by-default
private_endpoints_enabled           = true
ai_search_sku                     = "standard"
service_bus_sku                   = "Premium"
service_bus_capacity              = 1
key_vault_purge_protection_enabled  = true
key_vault_soft_delete_retention_days = 90
log_analytics_retention_days        = 30
```

### Prod (`iac/environments/prod/terraform.tfvars.example`)

```hcl
subscription_id     = "<prod-subscription-id>"
environment_name    = "prod"
location            = "centralus"
naming_prefix       = "bt-prod"
unique_suffix       = "<unique-suffix>"
github_org_repo     = "Stonefly-Labs/BusTerminal"

frontend_image = "<prod-acr>.azurecr.io/busterminal/web:placeholder"
backend_image  = "<prod-acr>.azurecr.io/busterminal/api:placeholder"

entra_tenant_id      = "<prod-tenant-id>"
entra_api_client_id  = "<prod-api-client-id>"
entra_web_client_id  = "<prod-web-client-id>"

platform_role_ids = { admin = "<uuid>", operator = "<uuid>", reader = "<uuid>", developer = "<uuid>" }

network_address_space            = ["10.52.0.0/16"]
subnet_integration_cidr          = "10.52.0.0/23"
subnet_private_endpoints_cidr    = "10.52.2.0/24"
data_services_public_access_enabled = false    # prod is private-by-default
private_endpoints_enabled           = true
ai_search_sku                     = "standard"
service_bus_sku                   = "Premium"
service_bus_capacity              = 1
key_vault_purge_protection_enabled  = true
key_vault_soft_delete_retention_days = 90
log_analytics_retention_days        = 30        # operator override if compliance requires longer
```
