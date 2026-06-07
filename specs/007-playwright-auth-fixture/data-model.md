# Phase 1 Data Model: Playwright MSAL Auth Fixture

**Feature**: 007-playwright-auth-fixture
**Date**: 2026-06-07

This document defines the entities the fixture introduces and the relationships between them. These are **internal test-tooling entities** — there is no product API surface in this feature.

---

## 1. Persona (enum, compile-time)

A symbolic handle used by a test to declare its required authentication identity. The persona is the sole interface between a test and the fixture.

| Field | Type | Notes |
|---|---|---|
| `name` | `'reader' \| 'operator' \| 'admin' \| 'none'` | Literal union. Extensible (e.g., `'developer'`) without breaking change. |

**Invariants**:

- A persona name maps to exactly one Test Identity.
- A persona name maps to exactly one Storage State Artifact.
- A test that omits the persona option falls back to no fixture (storageState absent); this is intentional so unauthenticated specs (e.g., a sign-in landing-page assertion) coexist with persona-annotated specs.

**Encoded at**: `web/tests/auth/personas.ts`

---

## 2. PersonaConfig (per-persona constant record)

Static configuration that resolves a Persona to the runtime data needed to acquire a session.

| Field | Type | Source | Notes |
|---|---|---|---|
| `persona` | `Persona` | inline | Discriminator |
| `upn` | `string` | env / globalSetup input | The test user's UPN (e.g., `e2e-reader-<suffix>@<tenant>.onmicrosoft.com`) |
| `keyVaultSecretName` | `string` | inline | Canonical KV secret name (see [`contracts/keyvault-secret-naming.md`](./contracts/keyvault-secret-naming.md)) |
| `expectedRoleAssignments` | `readonly string[]` | inline | Sanity-check list (e.g., `['BusTerminal.Reader']`); empty for `none` |
| `storageStatePath` | `string` | derived | Always `web/tests/.auth/<persona>.json` |

**Invariants**:

- `keyVaultSecretName` follows the pattern `e2e-test-user-<persona>-password`.
- `expectedRoleAssignments` is the **assertion** at globalSetup time after sign-in completes, not a configuration input — i.e., the fixture verifies the persona actually carries the expected roles in its access token, failing loud if the IaC drifted (FR-014).
- `none` persona has `expectedRoleAssignments: []` and the fixture asserts the access token's `roles` claim is empty/absent.

---

## 3. Test Identity (Entra-tenant entity, IaC-managed)

A synthetic Entra user managed by the new `e2e-test-identities` OpenTofu module. One Test Identity per Persona (except `none`, which is also a Test Identity — authenticated but role-grant-empty).

| Field | Type | Source | Notes |
|---|---|---|---|
| `user_principal_name` | `string` | `azuread_user.user_principal_name` | `e2e-<persona>-<unique_suffix>@<verified_domain>` |
| `display_name` | `string` | `azuread_user.display_name` | `BusTerminal E2E Test User — <Persona>` |
| `mail_nickname` | `string` | `azuread_user.mail_nickname` | `e2e-<persona>-<unique_suffix>` |
| `object_id` | `string` | `azuread_user.object_id` (output) | Used downstream by `azuread_app_role_assignment` |
| `initial_password` | `string` (sensitive) | `azuread_user.password` | Set on create only; rotation is out-of-band per R7 |
| `account_enabled` | `bool` | `azuread_user.account_enabled` | Always `true` |
| `force_password_change` | `bool` | `azuread_user.force_password_change` | Always `false` — synthetic identity, no interactive first-sign-in needed |
| `usage_location` | `string` | `azuread_user.usage_location` | Set to tenant default (typically `US`) |

**Invariants**:

- UPN, display name, and mail nickname collectively make the synthetic nature obvious (FR-008).
- `force_password_change = false` — otherwise the scripted sign-in would land on the "you must change your password" interstitial.
- One `azuread_app_role_assignment` per `expectedRoleAssignment` on the persona; zero such records for `none`.

**State transitions** (test identity lifecycle):

```
[absent] ──tofu apply──> [active, role-assigned]
   ▲                              │
   │                              ├─ rotate-password.sh ─> [active, role-assigned, password rotated]
   │                              │
   └──── (manual decommission) ───┘
```

The `[active, role-assigned, password rotated]` state is functionally identical to `[active, role-assigned]` from the fixture's perspective — only the credential in KV changes.

---

## 4. Key Vault Secret (per-persona password storage)

| Field | Type | Source | Notes |
|---|---|---|---|
| `name` | `string` | `azurerm_key_vault_secret.name` | `e2e-test-user-<persona>-password` |
| `value` | `string` (sensitive) | `azurerm_key_vault_secret.value` | Written from `azuread_user.password` output at create; updated out-of-band by `rotate-password.sh` |
| `content_type` | `string` | `azurerm_key_vault_secret.content_type` | `"text/plain — e2e test user password"` |
| `expiration_date` | `string` (optional) | `azurerm_key_vault_secret.expiration_date` | Optional; if set, surface in CI as a warning when within 30 days |

**Access control**:

- The CI federated identity (existing pattern) gets `Key Vault Secrets User` RBAC role **scoped to these four secrets only**, not the whole vault.
- Developers using the suite locally get the same RBAC via their `az login` identity (assumed pre-existing for any contributor with dev-tenant access — documented in `quickstart.md`).

**Invariants**:

- Secret names match the persona enum 1:1.
- Secret values never leave Key Vault except into step-scoped env vars at globalSetup time.

---

## 5. Storage State Artifact (per-persona JSON file)

The file Playwright writes from `context.storageState({ path })` and reads via `test.use({ storageState })`.

| Field | Type | Source | Notes |
|---|---|---|---|
| `path` | `string` | derived | Always `web/tests/.auth/<persona>.json` |
| `origins[].origin` | `string` | Playwright | The app's origin (e.g., `http://localhost:3000` for CI, custom for local) |
| `origins[].localStorage[]` | `Array<{name,value}>` | Playwright | Typically empty for MSAL-default config |
| `origins[].sessionStorage[]` | `Array<{name,value}>` | Playwright ≥ 1.41 | **Must contain MSAL session records** — this is what makes the seeded context an authenticated context |
| `cookies[]` | `Array<Cookie>` | Playwright | Likely empty for the SPA; included for completeness |

**Invariants**:

- The directory `web/tests/.auth/` is gitignored.
- Files are recreated by `globalSetup` on every CI run.
- Files are reused across local runs as long as their underlying MSAL refresh material remains valid; `rotate-password.sh` deletes the local file for the affected persona so the next local run forces a fresh capture.
- A file with origin mismatching the test's `baseURL` is invalid — globalSetup is keyed by `baseURL`, and a CI-captured file should never be reused locally (handled by always regenerating in CI and by the rotation script).

**Lifetime**:

```
[absent] ──globalSetup signs in──> [valid, fresh]
                                       │
                                       ├─ time passes, refresh-tokens still valid ──> [valid, used by tests]
                                       │
                                       ├─ refresh-token expired or password rotated ──> [invalid]
                                       │
                                       └─ next globalSetup detects load failure ──> [absent]
                                                                                       │
                                                                                       ▼
                                                                              (loop back to sign-in)
```

---

## 6. CI Federated Identity Scope Extension

| Field | Type | Source | Notes |
|---|---|---|---|
| `federated_subject` | `string` | `azuread_application_federated_identity_credential.subject` | `repo:<owner>/<repo>:pull_request` and `repo:<owner>/<repo>:ref:refs/heads/main` |
| `audience` | `string` | same | `api://AzureADTokenExchange` |
| `kv_role_assignment_scope` | `string` | `azurerm_role_assignment.scope` | KV secret resource IDs (four scoped assignments, one per persona secret) |

**Invariants**:

- The federated subject already exists for `iac-apply-dev.yml` / `cd-dev.yml`. The CI workflow either reuses an existing federated subject or adds a dedicated `repo:.../...:pull_request` subject if the existing one is workflow-specific.
- The KV role assignment is **scoped per-secret**, not vault-wide. Adding a fifth persona later adds a fifth assignment; it does not broaden existing access.

---

## 7. Persona-Annotated Test (consumer-side contract)

The test author's view of the fixture. Encoded in `web/tests/fixtures/auth.ts`.

| Field | Type | Notes |
|---|---|---|
| `persona` | `Persona \| undefined` | A test option, settable via `test.use({ persona: 'reader' })` |

**Semantics**:

- When `persona` is set, the test's browser context is created with `storageState: getStorageStatePath(persona)`. The `AuthGuard` admits the user immediately on first navigation.
- When `persona` is unset, the test runs without seeded storageState — equivalent to a fresh, unauthenticated browser. Used by specs that test the pre-auth UX (e.g., the malformed-bearer 401 case in `msal-sign-in-and-whoami.spec.ts` that is already live).
- The `persona` option flows through `test.describe(...)` blocks naturally; setting it at file scope is the canonical pattern.

---

## Relationship summary

```
Persona ─1:1─> PersonaConfig ─1:1─> Test Identity (IaC) ─1:N─> AppRoleAssignment
                                            │
                                            └─1:1─> KeyVault Secret
                                            
Persona ─1:1─> Storage State Artifact (regenerated from sign-in using KeyVault Secret value)

PersonaAnnotatedTest ─*:1─> Persona ─selects─> Storage State Artifact
```
