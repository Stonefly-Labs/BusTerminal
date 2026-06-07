# Quickstart: Running E2E Auth Tests Locally & Rotating Test-User Credentials

**Feature**: 007-playwright-auth-fixture
**Audience**: Contributors who need to run the authenticated E2E suite on their workstation; operators who need to rotate test-user credentials.

---

## Part A — Running E2E auth tests locally (first time, clean checkout)

**Target**: SC-003 — under 15 minutes from a clean checkout to a passing authenticated E2E run.

### Prerequisites

| Tool | Version | Already required? |
|---|---|---|
| Node.js | per `.nvmrc` | yes |
| pnpm | per `package.json` packageManager | yes |
| Azure CLI (`az`) | ≥ 2.60 | yes |
| OpenTofu | per `iac/.terraform-version` | yes (only needed if you'll also touch IaC) |
| Access to the BusTerminal dev Entra tenant | yes — your individual account must be a member | yes (any active contributor already has this) |
| `Key Vault Secrets User` RBAC on the four `e2e-test-user-*-password` secrets | yes — included in the dev-contributor role bundle | yes |
| Backend running locally (real Entra-validated mode, in-memory persistence) OR pointed at deployed dev API | yes | optional — local backend is the supported path |

### Steps

1. **Sign in to Azure CLI against the BusTerminal dev tenant.**
   ```bash
   az login --tenant <busterminal-dev-tenant-id>
   az account set --subscription <busterminal-dev-subscription-id>
   ```
   Tenant and subscription IDs are documented in the dev environment's `iac/environments/dev/README.md`.

2. **Install dependencies.**
   ```bash
   pnpm -C web install --frozen-lockfile
   pnpm -C web exec playwright install --with-deps
   ```

3. **Set the persona env vars from Key Vault** (UPNs + passwords). The simplest approach is a one-liner per persona; future improvement: a `pnpm run e2e:env` script wrapping this. For now:
   ```bash
   export KV_NAME=<dev-kv-name>   # see iac/environments/dev/README.md
   for P in reader operator admin none; do
     UPN=$(az ad user list --filter "startswith(userPrincipalName,'e2e-${P}-')" --query '[0].userPrincipalName' -o tsv)
     PWD=$(az keyvault secret show --vault-name "$KV_NAME" --name "e2e-test-user-${P}-password" --query value -o tsv)
     export "E2E_TEST_USER_$(echo $P | tr a-z A-Z)_UPN=$UPN"
     export "E2E_TEST_USER_$(echo $P | tr a-z A-Z)_PASSWORD=$PWD"
   done
   ```

4. **Start the backend with real-Entra config and in-memory persistence** (in a separate terminal):
   ```bash
   ASPNETCORE_ENVIRONMENT=Development \
   ASPNETCORE_URLS=http://localhost:8080 \
   AzureAd__TenantId=<dev-tenant-id> \
   AzureAd__ClientId=<dev-api-client-id> \
   AzureAd__Audience=api://<dev-api-client-id> \
   REGISTRY_PERSISTENCE=InMemory \
   dotnet run --project api/BusTerminal.Api
   ```
   Wait for the `Now listening on: http://localhost:8080` line.

5. **Run the E2E suite** (frontend dev server auto-starts via `playwright.config.ts` `webServer`):
   ```bash
   pnpm -C web test:e2e
   ```
   - First run: globalSetup performs four scripted sign-ins (~15–25 s per persona on a warm tenant). You'll see one Chromium window per persona briefly.
   - Subsequent runs: globalSetup detects valid cached storageState files under `web/tests/.auth/` and skips sign-in. Suite starts in seconds.

6. **Run a single previously-fixme'd spec to verify**:
   ```bash
   pnpm -C web exec playwright test tests/e2e/registry/create-browse.e2e.spec.ts
   ```
   Expected: the spec runs end-to-end and reports pass/fail based on its own assertions — no `test.fixme` skip.

### Expected first-time wall time

- Install + browser download: ~5 min on a cold cache (mostly Playwright browser binaries).
- KV / Entra env-var pull: < 30 s.
- First globalSetup (four personas serially): ~60–90 s.
- One spec run: < 30 s.

**Total: well under SC-003's 15-minute budget on a clean checkout.**

### Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| `globalSetup: persona <X>: AAD sign-in form not found` | Microsoft sign-in UI changed (rare cosmetic change) | File an issue tagged `auth-fixture/upstream-drift`; in the meantime, see `web/tests/auth/sign-in.ts` for the selectors and update if you can reproduce locally. |
| `globalSetup: persona <X>: token roles claim mismatch (expected [...], got [...])` | IaC drift — the persona's role grant was changed in the tenant | Re-run `tofu apply` on the dev composition or, if the role-grant change was deliberate, update `PersonaConfig.expectedRoleAssignments`. |
| `globalSetup: persona <X>: storageState wrote zero sessionStorage entries` | Playwright version regressed sessionStorage capture, or MSAL cacheLocation changed | Confirm `web/lib/auth/msal-config.ts` still sets `cacheLocation: "sessionStorage"` and Playwright is ≥ 1.41. File an issue if both check out. |
| `az keyvault secret show` returns 403 | Your account lacks the `Key Vault Secrets User` RBAC role on the dev KV | Ask a dev-environment owner to add you to the contributor role assignment. |
| Tests run but every authenticated nav redirects to sign-in | storageState file's `origin` doesn't match `baseURL` | Delete `web/tests/.auth/<persona>.json` and re-run; globalSetup will recapture against the current `baseURL`. |

---

## Part B — Rotating a test-user password

**When to rotate**:

- Routine cadence (recommended: every 90 days; not enforced).
- Suspected exposure (a contributor's `~/.zsh_history` leaked, a CI log accidentally captured a password, etc.).
- Account compromise notification from the Entra tenant.

**Target**: SC-006 — under 30 minutes from start to next CI run passing.

### Steps

1. **Sign in to Azure CLI** against the dev tenant with an identity that has `Key Vault Secrets Officer` on the four `e2e-test-user-*-password` secrets AND `User Administrator` (or equivalent) on the dev tenant (needed for `az ad user password reset`).

2. **Run the rotation script**:
   ```bash
   ./scripts/e2e-test-identities/rotate-password.sh <persona>
   ```
   where `<persona>` is one of `reader`, `operator`, `admin`, `none`.

   The script:
   - Generates a high-entropy password.
   - Calls `az ad user password reset` to set the new password on the test user.
   - Writes the new password to the corresponding KV secret.
   - Deletes any locally cached `web/tests/.auth/<persona>.json`.
   - Prints a single-line confirmation.

3. **Verify the rotation in CI**:
   - Open or update any open pull request to trigger CI.
   - The frontend E2E job's globalSetup pulls the new password from KV automatically — no workflow file changes needed.
   - Confirm the job passes.

4. **Verify locally** (optional but recommended for the operator):
   ```bash
   unset E2E_TEST_USER_<PERSONA>_PASSWORD
   # Re-export the var via the Part-A step-3 loop to pick up the new value.
   pnpm -C web test:e2e -- tests/e2e/registry/create-browse.e2e.spec.ts
   ```

### Failure modes & recovery

| Scenario | Recovery |
|---|---|
| Script fails after `az ad user password reset` but before `az keyvault secret set` writes the new value | Re-run the script. The next attempt will generate a fresh password and overwrite both. The old KV value is broken in the meantime; the suite will fail loudly until the script completes. |
| Script fails after KV write but before local cache invalidation | Manually `rm web/tests/.auth/<persona>.json`. CI is unaffected. |
| Rotation collides with an in-flight CI run | The in-flight run already holds the old password in step-scoped memory. globalSetup completed; storageState was captured. The run finishes against the old password's session unaffected. The next run picks up the new password. No human intervention needed. |
| You ran the script for the wrong persona | Re-run for the correct persona. The wrongly-rotated persona keeps working — its KV secret and Entra user agree on the new password, just with a fresh value you didn't intend. |

### What the rotation script never does

- It never prints the password to stdout/stderr (beyond the single-line confirmation).
- It never writes the password to a local file.
- It never updates Tofu state (the `azuread_user.password` and `azurerm_key_vault_secret.value` are under `lifecycle.ignore_changes` — `tofu apply` after rotation does not revert).

---

## Part C — Adding a new persona (e.g., `developer`)

Out of scope for v1 (see [research.md R3](./research.md#r3--persona-inventory)) but the steps are small and recorded here for the future operator:

1. Add `'developer'` to the `Persona` literal union in `web/tests/auth/personas.ts`.
2. Add a `PersonaConfig` entry for `developer` (UPN env var, password env var, `keyVaultSecretName: 'e2e-test-user-developer-password'`, `expectedRoleAssignments: ['BusTerminal.Developer']`, `storageStatePath: 'web/tests/.auth/developer.json'`).
3. In `iac/modules/e2e-test-identities/`, add `developer` to the `local.personas` map and add the `BusTerminal.Developer` role object ID to the caller's `role_object_ids` input.
4. `tofu apply` against the dev composition. This creates the user, writes the KV secret, and assigns the role.
5. Update `contracts/persona-config.schema.json`'s enum to include `developer`.
6. Add E2E specs that consume the new persona.

No existing personas, role grants, or KV secrets are touched.
