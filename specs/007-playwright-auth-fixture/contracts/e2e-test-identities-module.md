# Contract: `iac/modules/e2e-test-identities` OpenTofu Module

**Module path**: `iac/modules/e2e-test-identities/`
**Files**: `main.tf`, `variables.tf`, `outputs.tf`, `versions.tf`, `README.md` (terraform-docs inject mode — matches every existing module)
**Caller**: `iac/environments/dev/main.tf`
**Providers**: `hashicorp/azuread ~> 3.1`, `hashicorp/azurerm` (versions per dev composition's existing constraints)

---

## Inputs (`variables.tf`)

| Variable | Type | Required | Description |
|---|---|---|---|
| `api_application_object_id` | `string` | yes | Object ID of the `BusTerminal API` app registration (where the four `BusTerminal.*` app roles live). Output of `app-registration-roles` module's parent app. Used to scope `azuread_app_role_assignment.resource_object_id`. |
| `role_object_ids` | `object({ reader = string, operator = string, admin = string })` | yes | Object IDs of the `BusTerminal.Reader / Operator / Admin` app roles. Output from `app-registration-roles` module. Used as `azuread_app_role_assignment.app_role_id`. |
| `key_vault_id` | `string` | yes | Resource ID of the dev Key Vault. Used to write the four `e2e-test-user-<persona>-password` secrets. |
| `tenant_default_domain` | `string` | yes | The `*.onmicrosoft.com` domain to use for UPN suffix (e.g., `busterminaldev.onmicrosoft.com`). |
| `unique_suffix` | `string` | yes | Short suffix (≤ 6 chars) used to deconflict UPNs/mail-nicknames across environments. Reuses the existing dev composition's `unique_suffix`. |
| `tags` | `map(string)` | no (default `{}`) | Tags applied to the KV secrets (Entra users do not accept tags). |

---

## Outputs (`outputs.tf`)

| Output | Type | Description |
|---|---|---|
| `personas` | `map(object({ upn = string, object_id = string, key_vault_secret_name = string }))` | Keyed by persona name (`reader`, `operator`, `admin`, `none`). Each entry has the UPN, Entra user object ID, and the KV secret name for the password. Consumed by the dev composition and surfaced via env composition outputs. |
| `key_vault_secret_ids` | `list(string)` | The four KV secret resource IDs. Used by the dev composition to assign `Key Vault Secrets User` RBAC scoped per-secret (not vault-wide) to the CI federated identity. |

---

## Behavior (`main.tf`)

For each persona in `{ reader, operator, admin, none }`:

1. **Generate a high-entropy password** via `random_password` (length ≥ 32, full character set). Marked `sensitive = true` throughout.
2. **Create the test user** with:
   - `user_principal_name = "e2e-${persona}-${var.unique_suffix}@${var.tenant_default_domain}"`
   - `display_name = "BusTerminal E2E Test User — ${title(persona)}"`
   - `mail_nickname = "e2e-${persona}-${var.unique_suffix}"`
   - `account_enabled = true`
   - `force_password_change = false`
   - `usage_location = "US"` (or another tenant-default)
   - `password = random_password.this[persona].result`
   - `lifecycle { ignore_changes = [password] }` — passwords are rotated out-of-band per R7
3. **Write the password to Key Vault** as `azurerm_key_vault_secret`:
   - `name = "e2e-test-user-${persona}-password"`
   - `value = random_password.this[persona].result`
   - `content_type = "text/plain — e2e test user password"`
   - `lifecycle { ignore_changes = [value] }` — KV secret values are also rotated out-of-band
4. **Assign the app role** for non-`none` personas via `azuread_app_role_assignment`:
   - `principal_object_id = azuread_user.this[persona].object_id`
   - `resource_object_id = var.api_application_object_id`
   - `app_role_id = var.role_object_ids[persona]`
   - Skipped entirely for `persona == "none"`.

---

## Invariants

- **`for_each` driven by a `local.personas` map**, not by a count — adding `developer` later is purely additive in one map entry.
- **No outputs expose the password**. Only the KV secret name; password lives in KV.
- **`ignore_changes = [password, value]` on user + KV-secret resources** — out-of-band rotation does not appear as drift in `tofu plan`.
- **`none` persona has no `azuread_app_role_assignment` entry**, by construction. A unit-test on the module (via `tofu plan -json` parsed in CI) asserts the `none` user appears with zero role assignments.
- **UPN naming makes the user obviously synthetic**. A separate one-line policy check in the project's `BT-IAC-*` rule set (added by this feature) asserts no `azuread_user` resource exists in any composition with a non-`e2e-` UPN prefix; this is a guardrail against accidentally adding real-user provisioning here.

---

## Caller-side wiring (in `iac/environments/dev/main.tf`)

```hcl
module "e2e_test_identities" {
  source = "../../modules/e2e-test-identities"

  api_application_object_id = module.app_registration_api.application_object_id
  role_object_ids = {
    reader   = module.app_registration_roles.role_ids["BusTerminal.Reader"]
    operator = module.app_registration_roles.role_ids["BusTerminal.Operator"]
    admin    = module.app_registration_roles.role_ids["BusTerminal.Admin"]
  }
  key_vault_id          = module.keyvault.id
  tenant_default_domain = var.tenant_default_domain
  unique_suffix         = var.unique_suffix
  tags                  = local.shared_tags
}

# Scope the CI federated identity to JUST these four secrets — not the whole vault.
resource "azurerm_role_assignment" "ci_reads_e2e_secrets" {
  for_each             = toset(module.e2e_test_identities.key_vault_secret_ids)
  scope                = each.value
  role_definition_name = "Key Vault Secrets User"
  principal_id         = module.federated_credential_ci.principal_id
}
```

Variable names referenced above (`module.app_registration_api`, `module.app_registration_roles`, `module.federated_credential_ci`) reflect existing modules; exact names are adapted at implement time to match the actual dev composition.

---

## CI / lint expectations

- `tofu fmt -check -recursive` passes.
- `tflint --recursive` passes.
- `checkov` (existing config) passes — no new exceptions required.
- `terraform-docs --output-check` passes — module README has the `<!-- BEGIN_TF_DOCS --> ... <!-- END_TF_DOCS -->` markers matching every existing module.
- A new entry in `BT-IAC-*` (whichever rule file handles allowlists for IAM-affecting resources) records the `azuread_app_role_assignment` additions as expected, with rationale.
