# Contract: Key Vault Secret Naming & Rotation

**Vault**: existing dev Key Vault (managed by `iac/modules/keyvault/`)
**Owner**: `iac/modules/e2e-test-identities` (create); `scripts/e2e-test-identities/rotate-password.sh` (update)

---

## Canonical secret names

| Persona | KV Secret Name | Notes |
|---|---|---|
| `reader` | `e2e-test-user-reader-password` | Required |
| `operator` | `e2e-test-user-operator-password` | Required |
| `admin` | `e2e-test-user-admin-password` | Required |
| `none` | `e2e-test-user-none-password` | Required — the zero-role persona is still password-protected |

**Pattern**: `e2e-test-user-<persona>-password` where `<persona>` matches the `Persona` enum literal exactly. New personas added in the future MUST follow this pattern.

---

## Content type & metadata

| Field | Value |
|---|---|
| `content_type` | `text/plain — e2e test user password` |
| `tags` | `{ owner = "spec-007-playwright-auth-fixture", purpose = "e2e-fixture", persona = "<persona>" }` |
| `expiration_date` | Optional. If set by the rotation operator, CI emits a warning when within 30 days of expiry. |
| `enabled` | `true` (default; disabling breaks the suite) |

---

## Access control

- **Create**: `iac/modules/e2e-test-identities` (Tofu apply identity — already has KV-secret-write per existing module patterns).
- **Read (CI)**: scoped per-secret `Key Vault Secrets User` RBAC role assignment to the CI federated identity. Granted by the caller-side wiring in `iac/environments/dev/main.tf` (see [`e2e-test-identities-module.md`](./e2e-test-identities-module.md)).
- **Read (local)**: contributors with dev-tenant access have `Key Vault Secrets User` on the vault via existing dev-tenant role assignments. Documented in `quickstart.md`.
- **Update**: the rotation script's invoker. Operators with `Key Vault Secrets Officer` on the four secrets only. This is a small enough scope that the existing dev-environment owners suffice.

---

## Rotation contract

`scripts/e2e-test-identities/rotate-password.sh <persona>` performs:

1. **Validate inputs**: `<persona>` is one of `reader | operator | admin | none`. `az account show` succeeds. KV name resolved from a known env var or argument.
2. **Generate new password**: 32 chars, full character set, OS-provided RNG (`openssl rand` or equivalent).
3. **Reset the Entra user's password**: `az ad user password reset --id <upn> --password <new>`. The UPN is resolved by looking up the persona's `upn` from the module's `personas` output (or via a known naming convention if the operator runs this without Tofu state access).
4. **Write the new KV secret value**: `az keyvault secret set --vault-name <kv> --name e2e-test-user-<persona>-password --value <new>`.
5. **Invalidate local storageState cache**: `rm -f web/tests/.auth/<persona>.json`.
6. **Print confirmation**: single line `Rotated e2e-test-user-<persona>-password (Entra + KV). Next CI run will pick up the new password automatically. Local: run \`pnpm test:e2e\` to recapture.`

The script MUST NOT:

- Print the password to stdout/stderr.
- Write the password to any local file.
- Update Tofu state (the `password` and KV-secret `value` fields are under `ignore_changes` so subsequent `tofu apply` runs do not revert).

---

## Failure modes

| Scenario | Detected by | Fixture behavior |
|---|---|---|
| KV secret missing | globalSetup `az keyvault secret show` fails | globalSetup fails loudly with persona-scoped error; quickstart pointer in error message. |
| KV secret read-permission denied | same | same, with hint to re-check federated-credential role assignment |
| Entra user password out-of-sync with KV (mid-rotation collision) | globalSetup sign-in fails at credential entry | globalSetup retries once after 5 s; if still failing, fails loudly with rotation-collision diagnostic. |
| KV secret expired (`expiration_date` reached) | globalSetup `az keyvault secret show` returns enabled=false | same; pointer to rotation procedure |

---

## Why per-secret RBAC (not vault-wide)

Constitution IV — Security by Default; least-privilege RBAC. The CI federated identity reads the four persona passwords and nothing else. If the CI workflow is later compromised, the blast radius is "four test-user passwords in the dev tenant," not "the entire dev KV."

This also means adding a fifth persona later adds one more `azurerm_role_assignment` — it does not broaden access to already-existing secrets.
