# Phase 0 Research: Playwright MSAL Auth Fixture

**Feature**: 007-playwright-auth-fixture
**Date**: 2026-06-07
**Status**: Complete — all `/speckit-clarify` decisions validated against current docs and the existing codebase. No unresolved NEEDS CLARIFICATION items.

This document records the research that backs each architectural decision in [`plan.md`](./plan.md). For each topic: the decision, the rationale, and the alternatives considered with reasons for rejection.

---

## R1 — Credential acquisition flow

**Decision**: Playwright `globalSetup` performs a one-time scripted browser sign-in per persona against the real Microsoft sign-in surface (`login.microsoftonline.com`). The resulting browser `storageState` (cookies + localStorage + sessionStorage) is persisted to a gitignored file per persona. Each test that requests a persona loads that storageState into its browser context — no live IdP round-trip per test.

**Rationale**:

1. **Exercises the real MSAL flow.** Because the persisted state is whatever a real signed-in user's browser holds, the persona's session shape stays automatically in sync with `@azure/msal-browser` v4's storage layout across upgrades. Token injection (option C in clarify) would have hard-coded that storage layout into fixture code and broken on every `msal-browser` minor.
2. **Aligned with current Entra posture.** ROPC username/password (option B) has been progressively restricted by Microsoft for tenants that enforce conditional access. The dev tenant currently permits ROPC for synthetic users, but relying on it ties the test pipeline to a flow Microsoft has signalled intent to further restrict. The browser-automation path is the Microsoft-documented direction for SPA E2E.
3. **Preserves W3C Trace Context propagation.** Because the page is rendered exactly as a real user would render it (just with seeded storage), the existing `traceparent` propagation behavior is unchanged. The previously-suspended assertion in `web/tests/e2e/msal-sign-in-and-whoami.spec.ts` activates as-is once the fixture is in place — there is no fixture-side code path that bypasses the application's HTTP client.

**Alternatives considered**:

- **ROPC (username/password grant)** — Rejected. Tenant-restriction risk; doesn't exercise the real MSAL state-write path; would force hand-assembly of MSAL's session structure.
- **Token injection (skip IdP entirely)** — Rejected. Highest coupling to internal MSAL storage shape; diverges most from a real user's session; brittle across `msal-browser` upgrades.

---

## R2 — sessionStorage capture in `storageState`

**Decision**: Use Playwright's `BrowserContext.storageState({ path })` to persist persona state. This captures cookies, localStorage, **and** sessionStorage. The project's `@playwright/test ^1.60` satisfies the ≥ 1.41 requirement.

**Rationale**: `web/lib/auth/msal-config.ts:21` configures MSAL with `cacheLocation: "sessionStorage"` (Microsoft's recommended default for SPAs). If storageState did not capture sessionStorage, the persisted file would contain none of MSAL's session data and the seeded browser would still prompt for sign-in. Playwright's `BrowserContext.storageState` returns `origins[].sessionStorage` as of v1.41, persisted alongside `localStorage` under the same `origins` entry. Loading the file via `context: { storageState: 'path/to/file.json' }` (or `test.use({ storageState })`) rehydrates both stores.

**Verification path** (executed at implement time):

1. Run globalSetup once.
2. Inspect the per-persona JSON: `origins[0].sessionStorage` must contain MSAL keys (typically `msal.<clientId>-<tenantId>-<homeAccountId>` style records — names vary by MSAL version; presence is what matters, not the literal names).
3. Load the state into a fresh context and assert `AuthGuard` does not redirect.

**Alternatives considered**:

- **Change MSAL `cacheLocation` to `localStorage` in tests** — Rejected. Would diverge test behavior from production, violating "tests assert observable behavior, not implementation detail" and the project's no-mock-IdP posture.
- **Capture sessionStorage manually via `page.evaluate(() => sessionStorage)` and seed on every page open with `addInitScript`** — Rejected. Required only for Playwright < 1.41; we're on ^1.60 so the storageState path is direct and simpler.

---

## R3 — Persona inventory

**Decision**: v1 ships four personas — `reader`, `operator`, `admin`, `none` (zero-role). The role catalog (`specs/003-auth-and-identity/contracts/role-permission-matrix.md`) has a fifth role, `Developer`, which is the only role authorized for the `DeveloperTooling` operation class.

**Rationale for excluding `developer` in v1**: None of the twelve currently-fixme'd test cases exercise `DeveloperTooling`-gated UI. Adding a fifth persona now would inflate the IaC surface (extra user, extra role grant, extra KV secret, extra globalSetup pass) without unblocking any test. The persona model is enum-extensible — adding `developer` later is one line in `web/tests/auth/personas.ts`, one `azuread_user` block, one `azuread_app_role_assignment` block, and one `azurerm_key_vault_secret` block.

**Rationale for `none` (zero-role)**: The `no-access-experience.spec.ts` and `unauthorized-state.e2e.spec.ts` specs need a user that is authenticated but holds no `BusTerminal.*` app role. This is **not** the same as an unauthenticated user — the auth wall must admit them, but the role catalog must reject them from every operation class. Provisioned as an `azuread_user` with zero `azuread_app_role_assignment` records.

**Alternatives considered**:

- **All five roles in v1** — Rejected. Speculative scope expansion for no current test consumer.
- **Two personas (logged-in + zero-role) and assert via API mocking for role variations** — Rejected. Mocking the role boundary defeats the point of the suite catching real role-config regressions; the spec is explicit about real Entra tokens.

**Documented v1.1 extension**: Add `developer` persona when the first `DeveloperTooling`-gated UI surface ships an E2E spec.

---

## R4 — Test-identity provisioning (IaC)

**Decision**: A new `iac/modules/e2e-test-identities` OpenTofu module declares the four test users (`azuread_user`), assigns each to the appropriate `BusTerminal.*` app role on the API app registration (`azuread_app_role_assignment`), and writes each user's initial password to Key Vault as a secret (`azurerm_key_vault_secret`). Wired into `iac/environments/dev/main.tf`.

**Rationale**:

1. **Reproducibility.** New dev environments (or a rebuild of the existing one) reproduce the entire test-identity surface from code. Manual setup drifts the moment the tenant is rebuilt.
2. **Constitution-aligned.** OpenTofu is the required IaC tool (tech-stack §6). The `hashicorp/azuread ~> 3.1` provider is already in use (`iac/modules/app-registration-roles/main.tf:7`), so no new provider is introduced.
3. **Naming makes synthetic identities obvious.** UPN pattern `e2e-<persona>-<unique_suffix>@<tenant-default-domain>`; display name pattern `BusTerminal E2E Test User — <Persona>`; `mail_nickname` consistent. Anyone auditing the tenant sees these are test fixtures, not real people (FR-008).

**Password lifecycle**: `azuread_user.password` is set on **create only**. The `hashicorp/azuread` provider does not re-apply `password` on subsequent `tofu apply` runs unless the resource is recreated (which would break role assignments and KV secret references). This is by-design: it lets passwords be rotated out-of-band without IaC state churn. The rotation procedure (R7) handles ongoing password lifecycle.

**Alternatives considered**:

- **Out-of-band creation, IaC references only via data sources** — Rejected per `/speckit-clarify` Q2 (the answer chose IaC ownership for reproducibility).
- **Hybrid: users out-of-band, role grants in IaC** — Rejected for the same reason; splitting ownership across two boundaries doubles operational surface for marginal benefit.
- **Use AAD groups + `azuread_group_member` to carry role assignments** — Rejected for v1. Group-mediated role assignment adds an indirection that yields no benefit here (four users, fixed mapping, no admin-portal management needed). Worth reconsidering only if the persona count grows or if the same role grants need to be reused outside of test contexts.

---

## R5 — CI credential pathway (KV access at run time)

**Decision**: Extend the existing GitHub Actions OIDC federation pattern. The CI workflow uses `azure/login@v2` with a federated identity to acquire a short-lived Entra token, then pulls each persona's password from Key Vault via `az keyvault secret show` into ephemeral step-scoped env vars consumed by `globalSetup`.

**Rationale**:

1. **No long-lived secrets in GH Actions.** Tech-stack §7 mandates OIDC federation for pipeline-to-Azure auth. The CD/IaC workflows already use this pattern; this feature reuses it rather than introducing GH Action repository secrets.
2. **Least privilege.** The federated identity gets `get` on the four specific KV secrets only — not `list`, not other secrets. Scoped via KV access policy or RBAC role assignment (`Key Vault Secrets User` on the four secrets specifically if KV is RBAC-mode, else a tight access policy).
3. **Secret hygiene.** Passwords reach the runner as step-scoped env vars (visible only to `globalSetup`), are never written to disk by Playwright, are never logged (`globalSetup` will explicitly redact the form-fill step from any captured trace), and the federated token itself is one-hour-lifetime. No persistence between runs.

**KV access scope**: Existing dev KV; four new secrets named `e2e-test-user-<persona>-password` (canonical names in `contracts/keyvault-secret-naming.md`). The CI federated identity gets `Key Vault Secrets User` role on those four secrets only (not the whole vault).

**Alternatives considered**:

- **GH Actions repository secrets** — Rejected explicitly (spec FR-009 forbids; constitution requires Managed-Identity-preferred).
- **Pre-fetch passwords during a separate `bootstrap` workflow into encrypted environment files** — Rejected. More moving parts, secret-at-rest concerns, no benefit over per-run KV fetch.
- **Use a single persona-shared shared password** — Rejected. Per-persona credentials so a compromise of one persona's password doesn't affect the others; also enables per-persona rotation without disrupting other personas.

---

## R6 — Backend mode for the E2E CI job

**Decision**: Replace the current mock-auth backend mode in `.github/workflows/ci.yml:67` with **real Entra token validation** against the dev tenant. The backend continues to run as an in-CI process (not as the deployed dev API) but is configured with `AzureAd__TenantId = <dev tenant id>` and `AzureAd__ClientId = <dev API app reg id>`, and runs with **in-memory persistence** (no Cosmos dependency for the E2E job).

**Rationale**:

1. **The spec rules out mock IdP.** The fixture must seed real tokens; the backend must validate them. Running the backend with the mock handler would silently accept anything and produce false E2E confidence.
2. **In-memory persistence keeps the CI graph simple.** Spec 006 already ships `InMemoryRegistryStores` (used by the existing backend test suite). Wiring the E2E backend to those stores via env-var-driven DI selection means no Cosmos emulator dependency, no DB cleanup between specs, and no cross-test state leakage at the persistence layer.
3. **Avoids hitting deployed dev from PR CI.** Running PR CI against the deployed dev API would mix test state with whatever the live dev environment holds, would create cross-PR interference, and would couple PR pass/fail to dev's deployment health. The local-backend-with-real-tokens model is both isolated and realistic.

**Alternatives considered**:

- **Point CI at deployed dev API** — Rejected (cross-PR interference, state coupling).
- **Keep mock backend in CI; assert fewer end-to-end properties** — Rejected. Defeats the spec.
- **Spin up Cosmos emulator for the E2E job too** — Rejected for v1. The E2E specs are happy-path UI flows; persistence semantics are unit-tested elsewhere. Reconsider if any E2E becomes meaningfully load-bearing for persistence correctness.

---

## R7 — Password rotation lifecycle

**Decision**: Rotation is an **out-of-band procedure**, not a `tofu apply` flow. A new `scripts/e2e-test-identities/rotate-password.sh` script accepts a persona name, generates a high-entropy password, calls Microsoft Graph (`az ad user password reset --id <upn> --password <new>`) to set the new password on the test user, writes the new password to the corresponding Key Vault secret (`az keyvault secret set --vault-name <kv> --name e2e-test-user-<persona>-password --value <new>`), and prints a single-line confirmation. The script never writes the password to stdout/stderr beyond the confirmation and never persists it to a local file.

**Rationale**:

1. **`azuread_user.password` is create-only in the `hashicorp/azuread` provider.** Re-applying via Tofu would either no-op (if the resource isn't recreated) or destroy and recreate the user (which would break role assignments and require re-grant). Neither is the right shape for ongoing credential rotation.
2. **Operator-friendly.** A single-purpose bash script with `set -euo pipefail` and one input (persona name) is faster and less error-prone than a Tofu workflow. Recovery from a failed mid-rotation is just "re-run the script."
3. **Cache invalidation.** The script also deletes any local `web/tests/.auth/<persona>.json` cached storageState so the next local run forces a fresh globalSetup sign-in. CI is unaffected — it always starts from no cache.

**Edge case — rotation collides with an in-flight CI run** (spec edge case): The in-flight run already holds the old password in step-scoped env memory. globalSetup completed; storageState is captured; subsequent tests use storageState. The in-flight run completes against the old password's session unaffected. The next run pulls the rotated password and is unaffected. **Failure mode is deterministic**: no in-flight run breaks; no future run inherits stale credentials. Documented in `quickstart.md`.

**Alternatives considered**:

- **Tofu-managed rotation** — Rejected (provider semantics, see above).
- **Time-bound passwords with auto-rotation in KV** — Rejected for v1. KV's auto-rotation is for KV-native secret rotation (e.g., RBAC keys); reaching back into Entra to set the user's password is out of band regardless.

---

## R8 — Concurrent worker contention on a single persona

**Decision**: storageState files are **read-only by workers**. Playwright opens each new browser context with `storageState: <path>` which reads the JSON, hydrates the context, and never writes back. Multiple workers requesting the same persona simultaneously each read the same file and operate on independent in-memory copies of the session. No file lock, no contention.

**Rationale**: The MSAL session captured at globalSetup time contains an access token + refresh token. The access token is bearer-by-value (cloneable). When a worker's session refreshes silently mid-run, the refresh happens against the per-context MSAL instance — there is no shared writable state between workers. The original storageState file remains valid until either (a) the refresh token itself expires (typically 14 days) or (b) the test user's password is rotated.

In CI, `playwright.config.ts:20` pins `workers: 1` anyway, so concurrent same-persona reads only occur during local parallel runs. Locally, the four-persona × N-workers case is provably safe by the read-only-file argument.

**Alternatives considered**:

- **Per-worker storageState file** — Rejected. Multiplies globalSetup cost by worker count for no observable benefit.
- **Mutex on the storageState file** — Rejected. Solves a non-problem; serializes for no reason.

---

## R9 — Documentation surface

**Decision**: Two documentation deliverables:

1. **`quickstart.md`** (this feature) — "Running E2E auth tests locally": prerequisites, `az login` step, one-time persona-cache priming, command to run a single fixme'd-now-live spec, and a brief "expected first-time delay" note (globalSetup sign-in is ~15-25 s per persona on a warm tenant). Also includes the rotation procedure.
2. **`web/tests/auth/README.md`** (alongside the fixture code) — concise reference for test authors: how to declare a persona, what each persona is for, how to add a new persona, and a "don't do this" list (don't bypass the fixture; don't hand-craft MSAL state; don't commit the `.auth/` directory).

The quickstart is the contributor onboarding surface (SC-003 covers this — "under 15 minutes from clean checkout"). The fixture README is the day-to-day reference.

**Rationale**: Splitting the two avoids cluttering the spec-level quickstart with day-to-day test-authoring detail and avoids hiding the rotation procedure inside a test-suite README where ops engineers won't find it.

---

## R10 — What this fixture explicitly does NOT do

Documented here so future contributors don't mistake gaps for omissions:

- **Does not authenticate the test runner itself for direct API calls.** Per `/speckit-clarify` Q3 (FR-017), scope is browser session only. Tests that want runner-side API calls use a separate (not-yet-built) helper.
- **Does not exercise MFA, conditional access, B2C, or B2B flows.** Spec out-of-scope; dev tenant test users do not have MFA or CA policies applied to them. The IaC module's `azuread_user` block will explicitly note this expectation in a comment so a future tenant-policy change doesn't silently break the suite.
- **Does not test the sign-out flow programmatically.** The msal sign-in spec's sign-out path runs as part of the un-fixme'd case using the real MSAL sign-out, but the fixture itself is acquire-only — there's no "fixture-driven sign-out" helper. Tests that need to assert sign-out behavior do it via the in-page UI.
- **Does not generate or rotate the API app registration's roles.** Those are owned by spec 003's `app-registration-roles` module and are reused as-is here.

---

## Open follow-ups (not blocking implementation)

- **Add `developer` persona** if and when a `DeveloperTooling`-gated UI surface ships an E2E spec. Tracked as documented v1.1 extension in R3.
- **AAD groups for role mediation** if persona count grows beyond five or if the same role grants need reuse outside test contexts. Tracked in R4 alternatives.
- **Direct-to-API auth helper for runner-side calls** if/when tests start needing API-driven arrange/teardown that isn't worth driving through the page. Out of scope here per FR-017.
