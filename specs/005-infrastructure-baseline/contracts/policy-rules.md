# Policy Rules (CI Gate)

**Feature**: `005-infrastructure-baseline` | **Date**: 2026-05-25

The binding rule set the custom CI policy gate enforces (FR-044). Implemented as bash + jq scripts under `iac/policies/`, run after `tofu show -json tfplan` and against `tofu show -json terraform.tfstate`. Each rule has a stable ID for reference in operator overrides.

A rule failure blocks the PR check unless an explicit per-rule allowlist entry exists in `iac/policies/allowlist.json` AND the PR description carries a justification (the gate doesn't parse the PR — reviewers enforce this rule).

---

## Rule catalog

### `BT-IAC-001` — Mandatory tag coverage

**Source FR**: FR-037, SC-010
**Asserts**: Every taggable resource in the plan/state has all of the following tags with non-empty values:

- `application = "BusTerminal"`
- `environment = <env_name>`
- `managed-by = "opentofu"`
- `cost-center` (any non-empty value)
- One of `owner` OR `team` (any non-empty value)

**Resource types skipped** (Azure resource types that don't accept tags): `Microsoft.Authorization/roleAssignments`, `Microsoft.Authorization/roleDefinitions`, `Microsoft.Network/privateDnsZones/virtualNetworkLinks` (the link itself), `Microsoft.KeyVault/vaults/secrets`, `Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments`.

**Failure output**: `BT-IAC-001 FAIL: <resource_address> is missing tag(s): <list>`

---

### `BT-IAC-002` — No public-by-default data services in production

**Source FR**: FR-031, SC-004, US3
**Applies to**: env compositions where `environment_name` starts with `prod`
**Asserts**: For each of these resource types, `public_network_access_enabled = false` (or equivalent provider property):

- `azurerm_cosmosdb_account` → `public_network_access_enabled`
- `azurerm_key_vault` → `public_network_access_enabled`
- `azurerm_search_service` → `public_network_access_enabled`
- `azurerm_servicebus_namespace` → `public_network_access_enabled`
- `azurerm_container_registry` → `public_network_access_enabled` (when SKU = Premium)
- `azurerm_storage_account` (if any) → `public_network_access_enabled`

**Allowlist key**: `BT-IAC-002:<env>:<resource_address>` (rare; should never be needed for prod).

**Failure output**: `BT-IAC-002 FAIL: <resource_address> in environment <env> has public_network_access_enabled = true`

---

### `BT-IAC-003` — Diagnostic settings coverage and shape

**Source FR**: FR-027, SC-006, Q5c
**Asserts**: For each of these resource types in the plan/state, there exists a corresponding `azurerm_monitor_diagnostic_setting` whose `target_resource_id` matches AND whose `enabled_log` block has `category_group = "allLogs"` AND that has NO `enabled_metric` block:

- `azurerm_cosmosdb_account`
- `azurerm_key_vault`
- `azurerm_search_service`
- `azurerm_servicebus_namespace`
- `azurerm_container_registry`
- `azurerm_container_app_environment`
- `azurerm_container_app` (only `allLogs`; AllMetrics block REMOVED per Q5c)
- `azurerm_log_analytics_workspace` (workspace can monitor itself for some diagnostics)
- `azurerm_application_insights`

**Allowlist key**: `BT-IAC-003:<resource_address>` (used for resources that Azure does not support diagnostic settings on, identified at task-implementation time).

**Failure outputs**:
- `BT-IAC-003 FAIL: <resource_address> has no diagnostic setting forwarding allLogs to a Log Analytics workspace`
- `BT-IAC-003 FAIL: <diag_resource_address> includes an enabled_metric block (Q5c forbids forwarding metrics to Log Analytics)`
- `BT-IAC-003 FAIL: <diag_resource_address> uses an individual log category instead of category_group = "allLogs"`

---

### `BT-IAC-004` — RBAC scope is not subscription-wide for workloads

**Source FR**: FR-033, FR-034
**Asserts**: For every `azurerm_role_assignment` AND `azurerm_cosmosdb_sql_role_assignment` in the plan/state:

- If the assignment's `principal_id` matches the workload UAMI (lookup by `principal_type = "ServicePrincipal"` and name matching `mi-${naming_prefix}-workload`), then the assignment's scope MUST NOT be a subscription-level scope (`/subscriptions/<sub-id>` exactly) or a management-group scope.
- If the assignment's role definition is one of the management-plane roles (`Owner`, `Contributor`, `User Access Administrator`, `Cosmos DB Account Contributor`, `Service Bus Namespace Owner`, `Search Service Contributor`, `Key Vault Administrator`), and the principal is a workload UAMI, FAIL.

**Allowlist entries (built-in)**:
- The pipeline UAMI's subscription-Contributor + condition-scoped RBAC Administrator (documented in `plan.md` Complexity Tracking) is explicitly allowed.

**Failure output**: `BT-IAC-004 FAIL: role assignment <resource_address> grants <role_name> to <principal_name> at <scope> — workload identities must not receive subscription-wide or management-plane grants`

---

### `BT-IAC-005` — No secret values in OpenTofu outputs

**Source FR**: FR-036, FR-041, FR-042, SC-005
**Asserts**: For every output in the plan/state:

- If the output's `sensitive` flag is `false` (i.e., the value will appear in plaintext `tofu output`), the resolved value MUST NOT match any of these patterns:
  - `AccountKey=` (storage account / cosmos key)
  - `SharedAccessSignature=`
  - `Endpoint=sb://.*SharedAccessKey=`
  - `eyJ` (likely a JWT)
  - PEM key headers (`-----BEGIN.*PRIVATE KEY-----`)
  - Long base64 blobs that decode to common cert/key formats
  - `InstrumentationKey=` outside of a value tagged `app_insights_connection_string_*` (which is the documented sensitive exception per Q1c and IS sensitive-flagged)

**Allowlist entries**: None permitted; secret-content patterns are absolute.

**Failure output**: `BT-IAC-005 FAIL: output <output_name> contains a secret-like value (<matched-pattern>) without being marked sensitive`

---

### `BT-IAC-006` — Provider versions pinned

**Source FR**: NFR-005, constitution AVM mandate
**Asserts**: The env composition's `.terraform.lock.hcl` exists, is committed, and matches what `tofu init -upgrade=false` resolves (no drift between local + CI).

**Allowlist entries**: None.

**Failure output**: `BT-IAC-006 FAIL: provider lockfile drift detected — expected <committed-hash>, got <resolved-hash>`

---

### `BT-IAC-007` — Stateful-resource destroys require explicit approval

**Source FR**: FR-045, SC-009, US7
**Applies to**: tfplan JSON only (not state)
**Asserts**: The plan contains NO `delete` or `destroy-replace` action targeting any of the following resource types:

- `azurerm_resource_group`
- `azurerm_log_analytics_workspace`
- `azurerm_application_insights`
- `azurerm_key_vault`
- `azurerm_key_vault_secret`
- `azurerm_container_registry`
- `azurerm_cosmosdb_account`
- `azurerm_cosmosdb_sql_database`
- `azurerm_user_assigned_identity` (workload UAMI; pipeline UAMI exempted via allowlist when intentional)
- `azurerm_container_app_environment`
- `azurerm_storage_account` (tfstate)

**Allowlist key**: `BT-IAC-007:<resource_address>` plus a required `justification` field in the PR description (reviewers enforce). For destructive changes, the CI gate prints a "REQUIRES MANUAL APPROVAL" banner and the workflow pauses on a manual-approval gate.

**Failure output**: `BT-IAC-007 FAIL: plan would <action> stateful resource <resource_address> (state would be lost). Manual reviewer approval required.`

---

## Rule execution

The orchestrator script `iac/policies/run-policies.sh` runs all rules in dependency order, accumulates failures, and emits:

1. **Exit code**: `0` for all-pass, `1` for any FAIL, `2` for setup errors (no tfplan provided, jq missing, etc.).
2. **Markdown summary** to stdout (captured by the CI job and posted as a PR comment via `gh pr comment`).
3. **JSON detail** to a file artifact (uploaded by the CI workflow for inspection).

The script accepts:
- `--plan <path-to-tfplan.json>` — tfplan to evaluate
- `--state <path-to-state.json>` — optional state-mode check (subset of rules)
- `--env <env-name>` — environment context (drives env-conditional rules)
- `--allowlist <path>` — defaults to `iac/policies/allowlist.json`

---

## Allowlist file format (`iac/policies/allowlist.json`)

```json
{
  "BT-IAC-001": [],
  "BT-IAC-002": [],
  "BT-IAC-003": [
    {
      "resource_address": "azurerm_log_analytics_workspace.this",
      "justification": "LAW does not support a diagnostic setting for its own log ingestion"
    }
  ],
  "BT-IAC-004": [
    {
      "principal_match": "mi-busterminal-pipeline-*",
      "role": "Contributor",
      "scope": "/subscriptions/*",
      "justification": "Documented Complexity Tracking exception in spec 005 plan.md"
    },
    {
      "principal_match": "mi-busterminal-pipeline-*",
      "role": "Role Based Access Control Administrator",
      "scope": "/subscriptions/*",
      "justification": "Documented Complexity Tracking exception; condition-scoped to a fixed role GUID allowlist"
    }
  ],
  "BT-IAC-005": [],
  "BT-IAC-006": [],
  "BT-IAC-007": []
}
```

Allowlist edits are themselves a PR change and require reviewer sign-off; the gate prints a warning when it consumes any allowlist entry so reviewers see the bypass even on otherwise-green checks.
