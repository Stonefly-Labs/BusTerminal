# Implementation Plan: Playwright MSAL Auth Fixture for E2E Tests

**Branch**: `007-playwright-auth-fixture` | **Date**: 2026-06-07 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/007-playwright-auth-fixture/spec.md`

## Summary

Deliver a Playwright auth fixture and the supporting Entra-tenant, Key Vault, and CI plumbing that lets every `test.fixme`-suspended authenticated E2E spec (twelve cases across eleven files) run non-interactively on every pull request.

**Approach** (derived from `/speckit-clarify` decisions, validated in [research.md](./research.md)):

- **Acquisition flow**: Playwright `globalSetup` performs a one-time scripted browser sign-in per persona against the real Microsoft sign-in surface, then persists the resulting browser `storageState` (cookies + localStorage + sessionStorage — Playwright ≥ 1.41 captures sessionStorage, which is required because `@azure/msal-browser` is configured with `cacheLocation: "sessionStorage"`).
- **Personas**: four for v1 — `reader`, `operator`, `admin`, `none` (zero-role). The role catalog has a fifth (`Developer`); a `developer` persona is a documented v1.1 extension, not in current scope.
- **Provisioning**: a new `iac/modules/e2e-test-identities` OpenTofu module declares the test users via `azuread_user`, assigns the role grants via `azuread_app_role_assignment`, and writes each user's initial password to Key Vault as a secret. Subsequent rotations are handled by a documented out-of-band script (Tofu manages identity lifecycle; passwords are not re-applied by `tofu apply`).
- **CI credentials**: existing GitHub Actions OIDC federation (already used for `iac-apply-dev`, `cd-dev`) is extended with a federated credential scoped to the CI workflow; the workflow uses `azure/login@v2` to acquire a short-lived Entra token, then pulls the persona passwords from Key Vault into ephemeral env vars for `globalSetup` only.
- **Scope discipline**: fixture covers browser session only. Backend API calls follow the in-page MSAL flow. CI backend runs with **real** Entra token validation against the same dev tenant (replacing the current mock auth handler in `ci.yml:67`) and uses in-memory persistence so the suite has no Cosmos dependency.
- **Token renewal**: the in-page MSAL instance handles silent refresh during a run; the fixture re-acquires only when a persisted storageState becomes unusable between runs.

## Technical Context

**Language/Version**: TypeScript (strict), Node.js (per `.nvmrc`); HCL (OpenTofu ≥ pinned `.terraform-version`); shell (bash for the rotation script, PowerShell wrappers already exist for speckit scripts).

**Primary Dependencies**: `@playwright/test ^1.60` (already installed; storageState sessionStorage support is ≥ 1.41), `@azure/msal-browser ^4` / `@azure/msal-react ^3` (already installed; not modified by this feature), `hashicorp/azuread ~> 3.1` (already in use), `hashicorp/azurerm` (already in use), `azure/login@v2` (already used in iac-apply-dev / cd-dev workflows).

**Storage**: Test-user passwords in Azure Key Vault (existing dev KV — see `iac/modules/keyvault/`). Per-persona `storageState.json` files written to a gitignored `web/tests/.auth/` directory at globalSetup time; ephemeral within a run (recreated each CI run; cached across local runs as long as the storageState remains valid).

**Testing**: Playwright E2E suite (`web/tests/e2e/**/*.spec.ts`). The fixture itself is exercised by un-fixme-ing the twelve suspended cases enumerated in spec Assumptions. No unit tests for the fixture — its contract is observable via those E2E tests; testing the fixture directly with mocks would re-create the brittleness it's designed to eliminate.

**Target Platform**: Linux/macOS dev workstations; Ubuntu-latest GitHub Actions runners. Browser matrix per existing `playwright.config.ts`: Chromium, Firefox, WebKit at 1366×768.

**Project Type**: Web application (existing) — frontend in `web/`, backend in `api/`, infra in `iac/`. No new top-level project.

**Performance Goals**: globalSetup total wall time ≤ 90 s for four personas (one scripted sign-in apiece, run serially to avoid Microsoft sign-in rate-limit/CAPTCHA risk). Per-test overhead from loading storageState: negligible (<200 ms per context). Suite wall time impact: < +2 minutes on top of the existing E2E job.

**Constraints**:
- **MSAL cacheLocation is `sessionStorage`** (`web/lib/auth/msal-config.ts:21`). storageState **must** include sessionStorage; this constrains Playwright to ≥ 1.41 (project is on ^1.60, satisfied).
- **No secret in CI logs / artifacts / traces** (FR-010, SC-005). All bearer tokens are session-scoped; passwords reach the runner via Key Vault only; logging guards on the auth fixture and globalSetup must scrub URL parameters and form bodies. Playwright trace capture for the globalSetup project disabled.
- **Constitution IV (Security by Default)**: Managed Identity preferred — CI's federated identity is the only durable credential; the persona passwords are pulled from KV per-run and never persisted in env files committed to git.
- **Constitution V (Operational Excellence)**: W3C Trace Context propagation (`traceparent` / `tracestate`) on every UI-originated HTTP request — fixture is non-invasive on the page's HTTP client behavior, so existing propagation is preserved; the previously-suspended `traceparent` assertion in `msal-sign-in-and-whoami.spec.ts` lights up once the fixture is in place.
- **Tech-stack §7 (Identity & Auth)**: human SPA sign-in is MSAL Authorization Code + PKCE; backend validates with Microsoft.Identity.Web; OIDC federated credentials for pipeline-to-Azure. This feature aligns — it does not introduce a new auth pattern, it just non-interactively replays the SPA sign-in.

**Scale/Scope**:
- 4 personas; 1 dev tenant; 1 dev environment.
- 11 spec files / 12 fixme'd test cases un-suspended.
- 1 new IaC module (`e2e-test-identities`); 1 new Playwright globalSetup project; 1 fixture file (`web/tests/fixtures/auth.ts`); 1 rotation script; CI workflow extension.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Compliance | Notes |
|---|---|---|
| I — Azure-First Architecture | ✅ PASS | Real Entra dev tenant; KV for secrets; OIDC federation; no abstraction layer introduced. |
| II — API-First Design | ✅ N/A | Feature does not introduce new API surface. Fixture's TypeScript signature is documented as a contract under `contracts/` but it's an internal test utility, not a product API. |
| III — Strong Domain Modeling | ✅ N/A | No messaging-domain entities introduced. Personas reuse the canonical `BusTerminal.{Reader,Developer,Operator,Admin}` role catalog from spec 003. |
| IV — Security by Default | ✅ PASS | Managed-identity-first (OIDC federation, no long-lived secrets in CI); KV holds passwords; no embedded credentials; gitleaks gate on every change already covers prevention. Test users are obviously synthetic (`e2e-reader-<unique-suffix>@…onmicrosoft.com`) and dev-tenant-only. |
| V — Operational Excellence | ✅ PASS | Trace Context propagation preserved (FR-007); fixture does not bypass app's HTTP client; previously-suspended `traceparent` assertion in `msal-sign-in-and-whoami.spec.ts` activates. globalSetup emits structured log lines for sign-in lifecycle (without credentials). |
| VI — Incremental Extensibility | ✅ PASS | Persona model is enum-extensible (adding `developer` later is a one-line addition + one IaC user + one role grant + one storageState file). Fixture does not lock the project to Playwright-specific patterns; storageState is JSON. |

**Technology Standards**:
- OpenTofu (not Bicep) — new IaC module ✅.
- Playwright (constitution-bound E2E choice) ✅.
- KV for secrets ✅.
- AVM preference: `azuread_user` and `azuread_app_role_assignment` are core provider resources; no AVM equivalent exists. ✅ no deviation.
- E2E suite runs in CI ✅.

**No Constitution violations. No Complexity Tracking entries required.**

## Project Structure

### Documentation (this feature)

```text
specs/007-playwright-auth-fixture/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output (persona / identity / state-artifact / rotation-lifecycle)
├── quickstart.md        # Phase 1 output (local-dev quickstart + rotation procedure)
├── contracts/
│   ├── fixture-api.md           # TypeScript contract for the persona fixture
│   ├── persona-config.schema.json # JSON Schema for the persona-config file
│   ├── storage-state-shape.md   # Documentation of what we persist and why
│   ├── e2e-test-identities-module.md # IaC module input/output contract
│   └── keyvault-secret-naming.md # Canonical KV secret names + rotation contract
├── checklists/
│   └── requirements.md  # From /speckit-specify, updated by /speckit-clarify
└── tasks.md             # Phase 2 output (/speckit-tasks command — NOT created here)
```

### Source Code (repository root)

```text
web/
├── lib/auth/
│   └── msal-config.ts            # EXISTING — read-only reference; cacheLocation: sessionStorage drives storageState shape
├── tests/
│   ├── .auth/                    # NEW — gitignored; per-persona storageState.json files (one per persona)
│   ├── auth/
│   │   ├── global-setup.ts       # NEW — Playwright globalSetup: scripted sign-in per persona, persists storageState
│   │   ├── personas.ts           # NEW — persona enum + per-persona config (KV secret name, role grant expectations)
│   │   └── sign-in.ts            # NEW — pure helper: drives Microsoft sign-in form for one persona
│   ├── fixtures/
│   │   └── auth.ts               # NEW — `test` factory extending @playwright/test with a `persona` option
│   └── e2e/
│       ├── msal-sign-in-and-whoami.spec.ts  # MODIFIED — remove test.fixme on the sign-in-cycle case; adopt fixture
│       ├── platform-status.spec.ts          # MODIFIED — same
│       ├── no-access-experience.spec.ts     # MODIFIED — same; uses `none` persona
│       ├── role-aware-affordances.spec.ts   # MODIFIED — same; uses `reader` persona
│       └── registry/
│           ├── create-browse.e2e.spec.ts    # MODIFIED — adopt fixture
│           ├── delete-blocked.e2e.spec.ts   # MODIFIED — adopt fixture
│           ├── edit-conflict.e2e.spec.ts    # MODIFIED — adopt fixture
│           ├── relationships-audit.e2e.spec.ts  # MODIFIED
│           ├── sc-010-time-to-find.e2e.spec.ts  # MODIFIED
│           ├── search.e2e.spec.ts           # MODIFIED (both fixme'd cases)
│           └── unauthorized-state.e2e.spec.ts   # MODIFIED
├── playwright.config.ts          # MODIFIED — add globalSetup, add `setup` project that owns auth, gate test projects on it
├── package.json                  # MODIFIED — add `test:e2e:auth-setup` script (optional convenience)
└── .gitignore                    # MODIFIED — add `web/tests/.auth/`

iac/
├── modules/
│   └── e2e-test-identities/      # NEW — provisions the four test users + role assignments + KV password secrets
│       ├── main.tf
│       ├── variables.tf
│       ├── outputs.tf
│       ├── versions.tf
│       └── README.md             # terraform-docs inject mode (matches every existing module)
└── environments/dev/
    └── main.tf                   # MODIFIED — wire the new module into the dev composition

scripts/
└── e2e-test-identities/
    └── rotate-password.sh        # NEW — bash script: az ad user password reset → write KV secret → revoke old storageState cache

.github/workflows/
└── ci.yml                        # MODIFIED — frontend job: add KV-fetch step (using federated identity), replace mock backend env vars with real-Entra config, point backend at in-memory store

CLAUDE.md                         # MODIFIED — plan reference between SPECKIT markers
```

**Structure Decision**: This feature spans frontend tests (`web/tests/`), IaC (`iac/modules/`), CI (`.github/workflows/`), and operational tooling (`scripts/`). No new top-level project; all additions slot into existing directories using existing conventions (Playwright test layout under `web/tests/`, IaC module shape per `iac/modules/<name>/{main,variables,outputs,versions,README}`, terraform-docs inject mode, OIDC federation pattern already established by `iac-apply-dev.yml`).

## Complexity Tracking

> No Constitution Check violations. This section is intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| (none) | | |
