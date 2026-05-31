# Module Contracts: Infrastructure Baseline

**Feature**: `005-infrastructure-baseline` | **Date**: 2026-05-25

Per-module input/output schema for the NEW and EXTENDED OpenTofu modules this slice ships. Each contract is the binding interface downstream env compositions consume; breaking changes after the slice ships require a major-version bump on the module folder and a coordinated env-composition update.

Conventions:
- All modules carry `tags` (map(string), required) and merge it onto every taggable child resource.
- All modules carry `name` (string, required) — the module is responsible for the resource's full name; no internal prefixing.
- Provider blocks are NOT declared inside modules; modules inherit from the env composition.
- Diagnostic settings on resources are routed through the new `diagnostic-settings` module (never inline `azurerm_monitor_diagnostic_setting` blocks in env compositions or feature modules).

---

## NEW MODULES

### `iac/modules/naming/`

**Purpose**: Central naming convention. Single source of truth for the `<prefix>-<resource-type>[-<suffix>]` pattern.

**Inputs**:
| Name | Type | Required | Description |
|---|---|---|---|
| `environment_name` | string | yes | `dev` / `test` / `prod` |
| `naming_prefix` | string | yes | e.g., `bt-dev` |
| `unique_suffix` | string | yes | 4–12 lowercase alphanumeric for globally-unique resources |

**Outputs**:
| Name | Type | Description |
|---|---|---|
| `resource_group_name` | string | `rg-<prefix>` |
| `log_analytics_workspace_name` | string | `log-<prefix>` |
| `application_insights_name` | string | `appi-<prefix>` |
| `key_vault_name` | string | `kv-<prefix>-<suffix>` |
| `container_registry_name` | string | `acr<prefix><suffix>` (hyphens stripped) |
| `container_apps_env_name` | string | `cae-<prefix>` |
| `cosmos_account_name` | string | `cosmos-<prefix>-<suffix>` |
| `ai_search_name` | string | `srch-<prefix>-<suffix>` |
| `service_bus_name` | string | `sbns-<prefix>-<suffix>` |
| `vnet_name` | string | `vnet-<prefix>` |
| `workload_uami_name` | string | `mi-<prefix>-workload` |

**Validation**:
- `naming_prefix` must match `^bt-[a-z0-9]{2,8}$`
- `unique_suffix` must match `^[a-z0-9]{4,12}$`

---

### `iac/modules/networking/`

**Purpose**: VNet + subnets + private DNS zones + zone-VNet links. Wraps `Azure/avm-res-network-virtualnetwork/azurerm v0.16.0` and `Azure/avm-res-network-privatednszone/azurerm v0.4.2`.

**Inputs**:
| Name | Type | Required | Default | Description |
|---|---|---|---|---|
| `vnet_name` | string | yes | n/a | VNet name from `naming` module |
| `resource_group_name` | string | yes | n/a | Env RG |
| `location` | string | yes | n/a | Azure region |
| `address_space` | list(string) | yes | n/a | e.g., `["10.50.0.0/16"]` |
| `subnet_integration_cidr` | string | yes | n/a | `/23` min; for CAE integration |
| `subnet_private_endpoints_cidr` | string | yes | n/a | `/24` recommended |
| `private_dns_zones` | list(string) | yes | n/a | e.g., `["privatelink.vaultcore.azure.net", ...]` |
| `tags` | map(string) | yes | n/a | merged onto every child |

**Outputs**:
| Name | Type | Description |
|---|---|---|
| `vnet_id` | string | Resource ID of the VNet |
| `subnet_integration_id` | string | Resource ID of the CAE integration subnet |
| `subnet_private_endpoints_id` | string | Resource ID of the PE subnet |
| `private_dns_zone_ids` | map(string) | Map keyed by zone name → zone resource ID |

**Validation** (precondition):
- `subnet_integration_cidr` and `subnet_private_endpoints_cidr` must be inside `address_space`
- The two subnet CIDRs must not overlap
- `subnet_integration_cidr` must be `/23` or larger (Container Apps Environment minimum)

---

### `iac/modules/ai-search/`

**Purpose**: Azure AI Search service + optional private endpoint + diagnostic settings. Wraps `Azure/avm-res-search-searchservice/azurerm v0.2.0`.

**Inputs**:
| Name | Type | Required | Default | Description |
|---|---|---|---|---|
| `name` | string | yes | n/a | Search service name |
| `resource_group_name` | string | yes | n/a | Env RG |
| `location` | string | yes | n/a | Azure region |
| `sku` | string | yes | n/a | `free`, `basic`, `standard`, `standard2`, `standard3` |
| `public_network_access_enabled` | bool | yes | n/a | Per-env toggle (FR-031) |
| `log_analytics_workspace_id` | string | yes | n/a | For diagnostics |
| `workload_principal_id` | string | yes | n/a | Workload UAMI principal ID for `Search Index Data Contributor` grant |
| `private_endpoint_subnet_id` | string | no | `null` | When set, provision a PE |
| `private_dns_zone_id` | string | no | `null` | Required when `private_endpoint_subnet_id` is set |
| `tags` | map(string) | yes | n/a | |

**Outputs**:
| Name | Type | Description |
|---|---|---|
| `id` | string | Search service resource ID |
| `endpoint` | string | Search service endpoint URL (e.g., `https://<name>.search.windows.net`) |
| `private_endpoint_id` | string | PE resource ID (null when PE disabled) |

**Validation**: `sku = "free"` is rejected if either public access is disabled OR `private_endpoint_subnet_id` is set (free SKU supports neither).

---

### `iac/modules/service-bus/`

**Purpose**: Service Bus namespace + optional private endpoint + diagnostic settings + workload RBAC. Wraps `Azure/avm-res-servicebus-namespace/azurerm` (latest 0.x).

**Inputs**:
| Name | Type | Required | Default | Description |
|---|---|---|---|---|
| `name` | string | yes | n/a | Namespace name |
| `resource_group_name` | string | yes | n/a | Env RG |
| `location` | string | yes | n/a | Azure region |
| `sku` | string | yes | n/a | `Basic` / `Standard` / `Premium` |
| `capacity` | number | conditional | n/a | Required when `sku = "Premium"` (1, 2, 4, 8, 16) |
| `public_network_access_enabled` | bool | yes | n/a | Per-env toggle |
| `log_analytics_workspace_id` | string | yes | n/a | For diagnostics |
| `workload_principal_id` | string | yes | n/a | Workload UAMI for Sender + Receiver grants |
| `private_endpoint_subnet_id` | string | no | `null` | Premium-only |
| `private_dns_zone_id` | string | no | `null` | Required when `private_endpoint_subnet_id` is set |
| `tags` | map(string) | yes | n/a | |

**Outputs**:
| Name | Type | Description |
|---|---|---|
| `id` | string | Namespace resource ID |
| `name` | string | Namespace name (echo for convenience) |
| `fqdn` | string | `<name>.servicebus.windows.net` |
| `private_endpoint_id` | string | PE resource ID (null when PE disabled or SKU isn't Premium) |

**Validation** (precondition):
- `sku = "Basic"` REJECTED — Basic doesn't support topics/subscriptions
- `sku = "Standard"` AND `private_endpoint_subnet_id != null` → ERROR (Standard doesn't support PEs; PE silently skipped is a footgun)
- `sku = "Premium"` AND `capacity == null` → ERROR

---

### `iac/modules/diagnostic-settings/`

**Purpose**: Enforce the `allLogs`-only + no-metrics convention (Q5c). Thin wrapper around `azurerm_monitor_diagnostic_setting`.

**Inputs**:
| Name | Type | Required | Default | Description |
|---|---|---|---|---|
| `name` | string | yes | n/a | Diagnostic setting name |
| `target_resource_id` | string | yes | n/a | The resource being monitored |
| `log_analytics_workspace_id` | string | yes | n/a | Destination LAW |

**Outputs**: `id` (string)

**Renders**:
```hcl
resource "azurerm_monitor_diagnostic_setting" "this" {
  name                       = var.name
  target_resource_id         = var.target_resource_id
  log_analytics_workspace_id = var.log_analytics_workspace_id

  enabled_log {
    category_group = "allLogs"
  }

  # No enabled_metric block — Q5c clarification: metrics stay in Azure Monitor's native store.
}
```

**Validation**: none (the module is the validation — it cannot accept a metric configuration).

---

### `iac/modules/private-endpoint/`

**Purpose**: Reusable PE wrapper that handles per-service subresource-name + DNS-zone-group binding. Wraps `azurerm_private_endpoint`.

**Inputs**:
| Name | Type | Required | Default | Description |
|---|---|---|---|---|
| `name` | string | yes | n/a | PE name |
| `resource_group_name` | string | yes | n/a | Env RG |
| `location` | string | yes | n/a | Azure region |
| `subnet_id` | string | yes | n/a | PE subnet |
| `target_resource_id` | string | yes | n/a | Target service resource ID |
| `subresource_name` | string | yes | n/a | One of `vault`, `Sql`, `searchService`, `namespace`, `registry`, `blob` |
| `private_dns_zone_id` | string | yes | n/a | Zone to register the A record in |
| `tags` | map(string) | yes | n/a | |

**Outputs**: `id` (string), `private_ip_address` (string), `fqdn` (string — derived from the target name + zone)

---

## EXTENDED MODULES (existing; new inputs added — backward-compatible defaults)

### `iac/modules/monitoring/` (extended)

**Added input**:
| Name | Type | Required | Default | Description |
|---|---|---|---|---|
| `local_authentication_disabled` | bool | no | `false` | Forwarded to `azurerm_application_insights.local_authentication_disabled`. MUST stay `false` per research §6 (browser SDK doesn't support AAD ingestion). |

(The existing `retention_in_days` input remains; the env composition now passes it from a new env-level tf-var per Q5c.)

---

### `iac/modules/keyvault/` (extended)

**Added inputs**:
| Name | Type | Required | Default | Description |
|---|---|---|---|---|
| `private_endpoint_subnet_id` | string | no | `null` | When set, provision a PE for this vault |
| `private_dns_zone_id` | string | no | `null` | Required when `private_endpoint_subnet_id` is set |
| `public_network_access_enabled` | bool | no | `true` (preserves dev behavior) | Per-env toggle |

**Added output**: `private_endpoint_id` (string, null when PE disabled)

---

### `iac/modules/cosmos-account/` (extended)

**Added inputs**:
| Name | Type | Required | Default | Description |
|---|---|---|---|---|
| `private_endpoint_subnet_id` | string | no | `null` | When set, provision a PE bound to the `Sql` subresource |
| `private_dns_zone_id` | string | no | `null` | Required when `private_endpoint_subnet_id` is set |
| `public_network_access_enabled` | bool | no | `true` (preserves dev behavior) | Per-env toggle |

**Added output**: `private_endpoint_id` (string, null when PE disabled)

---

### `iac/modules/container-registry/` (extended)

**Added inputs**:
| Name | Type | Required | Default | Description |
|---|---|---|---|---|
| `private_endpoint_subnet_id` | string | no | `null` | When set, provision a PE (Premium SKU required) |
| `private_dns_zone_id` | string | no | `null` | Required when `private_endpoint_subnet_id` is set |

(Existing `public_network_access_enabled` input already supports per-env toggling.)

---

### `iac/modules/workload-identity/` (extended)

**Added input**:
| Name | Type | Required | Default | Description |
|---|---|---|---|---|
| `assigned_azure_rbac` | map(object({ role_definition_name, scope })) | yes | (existing — extended set) | Now expected to include `cosmos-data-contributor` (note: granted via Cosmos SQL role assignment, not Azure RBAC — handled at the env-composition level), `sb-data-sender`, `sb-data-receiver`, `search-index-data-contributor`, `monitoring-metrics-publisher`. Existing `acr-pull` and `kv-secrets-user` remain. |

(The contract is "the module accepts an arbitrary map of role assignments"; the env composition supplies the Q3c-mandated set. The module already supports this shape today — no module-level code change required beyond a docs/README update.)

---

## ENV COMPOSITION outputs contract

See [`outputs-contract.md`](./outputs-contract.md).

---

## Compatibility

- All new modules ship at `v0.x.0` (pre-1.0 per AVM precedent).
- All extended modules' added inputs have defaults that preserve existing behavior — no `terraform.tfvars` changes required for the existing dev composition beyond what this slice itself requires.
- Module `versions.tf` files declare provider requirements; the env composition's `.terraform.lock.hcl` is the binding source of truth for resolved versions.
