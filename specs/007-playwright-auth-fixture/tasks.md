---
description: "Task list for spec 007-playwright-auth-fixture"
---

# Tasks: Playwright MSAL Auth Fixture for E2E Tests

**Input**: Design documents from `/specs/007-playwright-auth-fixture/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

**Tests**: This feature does not introduce new test specs — the tests already exist as `test.fixme`-suspended cases in the E2E suite. The "test work" in each user story is **un-suspending existing specs and wiring them to the new fixture**. Per FR-004 and SC-001/SC-002 these adoption tasks are first-class deliverables.

**Organization**: Tasks are grouped by user story to enable independent implementation and validation of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Different file, no in-progress dependencies — safe to parallelize
- **[Story]**: User story this task serves (US1 / US2 / US3); omitted for Setup / Foundational / Polish
- Every task description includes the exact file path(s) it touches

## Path Conventions

This feature spans existing directories — paths below match the `Project Structure` section of [plan.md](./plan.md):

- Frontend tests: `web/tests/{auth,fixtures,e2e}/`
- IaC: `iac/modules/e2e-test-identities/` and `iac/environments/dev/`
- Operational tooling: `scripts/e2e-test-identities/`
- CI: `.github/workflows/ci.yml`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the directory skeleton and gitignore entry that every later task assumes exists.

- [ ] T001 [P] Add `web/tests/.auth/` to `web/.gitignore` (or repo-root `.gitignore` if the project tracks it there) so persona storageState files are never committed
- [ ] T002 [P] Create the `web/tests/auth/` directory with an empty `.gitkeep` so subsequent tasks can land files into a tracked path
- [ ] T003 [P] Create the `web/tests/fixtures/` directory with an empty `.gitkeep` (sibling of `web/tests/auth/`)
- [ ] T004 [P] Create the `scripts/e2e-test-identities/` directory with an empty `.gitkeep`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Provision the Entra test identities, Key Vault secrets, and per-secret RBAC; scaffold the persona / fixture TypeScript types. Nothing in any user story can run until this completes.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

### IaC: `e2e-test-identities` module + dev composition wiring

- [ ] T005 [P] Create `iac/modules/e2e-test-identities/versions.tf` declaring `required_version`, `hashicorp/azuread ~> 3.1`, and `hashicorp/azurerm` (version pin matching the dev composition's existing constraint)
- [ ] T006 [P] Create `iac/modules/e2e-test-identities/variables.tf` with the six inputs documented in `contracts/e2e-test-identities-module.md` (`api_application_object_id`, `role_object_ids`, `key_vault_id`, `tenant_default_domain`, `unique_suffix`, `tags`)
- [ ] T007 Create `iac/modules/e2e-test-identities/main.tf` per `contracts/e2e-test-identities-module.md`: `local.personas` map for `{reader,operator,admin,none}`; `random_password` per persona; `azuread_user` per persona (with `lifecycle { ignore_changes = [password] }`); `azurerm_key_vault_secret` per persona (with `lifecycle { ignore_changes = [value] }`); `azuread_app_role_assignment` for the three role-bearing personas only (skip `none`)
- [ ] T008 Create `iac/modules/e2e-test-identities/outputs.tf` exporting `personas` (map keyed by persona name with `upn`, `object_id`, `key_vault_secret_name`) and `key_vault_secret_ids` (list of the four KV secret resource IDs) — sensitive flags applied where appropriate
- [ ] T009 [P] Create `iac/modules/e2e-test-identities/README.md` with the standard `<!-- BEGIN_TF_DOCS -->` / `<!-- END_TF_DOCS -->` markers; run `terraform-docs -c iac/.terraform-docs.yml iac` locally to inject the generated section
- [ ] T010 Wire the module into `iac/environments/dev/main.tf`: `module "e2e_test_identities"` call passing the inputs from existing modules (`app_registration_api`, `app_registration_roles`, `keyvault`); add an `azurerm_role_assignment` `for_each` over `module.e2e_test_identities.key_vault_secret_ids` granting `Key Vault Secrets User` to the CI federated identity principal — scoped per-secret, NOT vault-wide (R5 / contracts/keyvault-secret-naming.md)
- [ ] T011 Add `personas` and `e2e_test_identity_kv_secret_ids` to `iac/environments/dev/outputs.tf` so downstream consumers (CI workflow, local quickstart) can resolve persona UPNs without an extra Graph lookup
- [ ] T012 Run `tofu fmt -recursive` then `tofu init` + `tofu validate` + `tofu plan` in `iac/environments/dev/`; verify the four `azuread_user`, four `azurerm_key_vault_secret`, three `azuread_app_role_assignment`, and four `azurerm_role_assignment` resources are planned with no destructive side effects on existing infrastructure
- [ ] T013 Run `tofu apply` against the dev composition (with appropriate approval); confirm the four UPNs (`e2e-{reader,operator,admin,none}-<suffix>@<tenant>`) appear in the tenant and the four secrets appear in the dev Key Vault
- [ ] T014 [P] Smoke-test KV access: `az keyvault secret show --vault-name <dev-kv> --name e2e-test-user-reader-password --query value -o tsv` returns a value (from a developer's local CLI session). This verifies the new secrets exist and basic RBAC works.

### Frontend scaffolding: persona enum + fixture skeleton

- [ ] T015 [P] Create `web/tests/auth/personas.ts` exporting the `Persona` literal type (`'reader' | 'operator' | 'admin' | 'none'`), the `PersonaConfig` interface matching `contracts/persona-config.schema.json`, and a `PERSONA_CONFIGS: Record<Persona, PersonaConfig>` constant pre-populated with all four persona records (KV secret names, env var names, expected role arrays per the schema's `examples`)
- [ ] T016 Create `web/tests/fixtures/auth.ts` exporting the `test` factory per `contracts/fixture-api.md`: extends `@playwright/test` with a `persona: Persona | undefined` option that resolves `storageState` to `web/tests/.auth/<persona>.json` when set, `undefined` otherwise; re-export `expect`
- [ ] T017 [P] Add a Vitest unit test at `web/tests/auth/__tests__/personas.config.test.ts` that validates each entry of `PERSONA_CONFIGS` against `contracts/persona-config.schema.json` using a JSON-schema validator (ajv is already not in deps — use `zod` instead since it's already in deps, encoding the schema as a Zod schema in the test file). Fails loudly if a new persona is added that drifts from the schema.

### CI federation pre-flight (PR-trigger compatibility)

- [ ] T017b Verify the GitHub OIDC federation surface supports PR-triggered workflows. The existing `module.workload_federation_environment` in `iac/environments/dev/main.tf:437` is scoped to subject `repo:${var.github_org_repo}:environment:${var.environment_name}` — that matches only when the GH Actions job declares `environment: dev`. CI's frontend job currently does not. Two acceptable resolutions; pick one at implement time and document the choice in `iac/environments/dev/README.md`: (a) **Add a second federated credential** alongside the existing one in `iac/environments/dev/main.tf` (a new `module "ci_pull_request_federation"` block) with `subject = "repo:${var.github_org_repo}:pull_request"` bound to a CI-dedicated user-assigned MI (create via `iac/modules/workload-identity` or `iac/modules/identity`) and grant that MI the four per-secret `Key Vault Secrets User` RBAC roles from T010 instead of the workload MI; OR (b) **Add `environment: dev` to the `frontend` job in `.github/workflows/ci.yml`** to make the subject claim resolve to the existing federation — note this places the job behind any deployment-protection rules attached to the `dev` environment, which may be undesirable for PR runs. Decision criterion: if `dev` environment has any required-reviewer or wait-timer protections, choose (a); otherwise (b) is simpler.

**Checkpoint**: Foundation ready — User Story 1 implementation can now begin.

---

## Phase 3: User Story 1 — Single-persona unblock (Priority: P1) 🎯 MVP

**Goal**: A platform engineer can run the registry and platform-status E2E specs against the dev tenant non-interactively. Eight of the twelve fixme'd cases (all the Reader-using ones) report real pass/fail.

**Independent Test**: Run `pnpm -C web exec playwright test tests/e2e/registry/create-browse.e2e.spec.ts` locally against the dev tenant after completing Setup + Foundational + Phase 3. The spec reaches the post-auth UI, runs its assertions, and reports pass/fail — no `test.fixme` skip, no interactive sign-in prompt.

### Sign-in driver + global setup (reader persona only)

- [ ] T018 [US1] Create `web/tests/auth/sign-in.ts` exporting a single `signInPersona(browser, persona, baseURL): Promise<void>` function that drives the Microsoft sign-in flow: navigate `baseURL`, AuthGuard redirects to `login.microsoftonline.com`, fill `#i0116` (UPN), advance, fill `#i0118` (password), submit, dismiss the "Stay signed in?" prompt (click `#idBtn_Back` for No or `#idSIButton9` for Yes — pick Yes so the session survives any in-flow refresh), and wait for the post-redirect navigation back to `baseURL`. Reads UPN/password from env vars per `PersonaConfig`. No persona-specific logic — the persona parameter only resolves which env vars to read.
- [ ] T019 [US1] Create `web/tests/auth/global-setup.ts` implementing Playwright's `globalSetup` signature. For Phase 3 it handles only the `reader` persona: launch Chromium, create a new context with `baseURL`, call `signInPersona(browser, 'reader', baseURL)`, then `context.storageState({ path: 'web/tests/.auth/reader.json' })`. After write, re-read the file and assert (a) `origins[].sessionStorage` is non-empty, (b) a `context.request.get('/whoami')` returns 200 with a `roles` claim that equals `PERSONA_CONFIGS.reader.expectedRoleAssignments` — fail loud per FR-014 with a persona-scoped diagnostic if either assertion fails. **Trace capture for this project is explicitly disabled** (`tracing.start({...})` is NOT called) so the sign-in form fill never lands in a trace artifact (FR-010).
- [ ] T020 [US1] Update `web/playwright.config.ts`: add `globalSetup: require.resolve('./tests/auth/global-setup.ts')`. Add an optional setup-only project named `auth-setup` (no `testMatch` needed for that name since the work happens in globalSetup itself). Keep the existing `chromium` / `firefox` / `webkit` projects' `testDir` and matcher as-is.
- [ ] T021 [US1] Add a `test:e2e:auth-only` convenience script to `web/package.json` (alongside existing `test:e2e`): runs only `tests/e2e/*.spec.ts` files (excluding `registry/`) so the auth round-trip can be validated quickly without the full registry sweep.

### Un-fixme the eight Reader-using specs (each touches a different file — all [P])

- [ ] T022 [P] [US1] Edit `web/tests/e2e/msal-sign-in-and-whoami.spec.ts`: switch the import to `from '@/tests/fixtures/auth'`; add `test.use({ persona: 'reader' })` at file scope; remove the `test.fixme(...)` wrapper from the sign-in-cycle case (leave the malformed-bearer 401 case unchanged — it is already live and persona-less); ensure the existing `traceparent` regex assertion (`^00-[0-9a-f]{32}-[0-9a-f]{16}-[0-9a-f]{2}$`) runs as part of the now-live test
- [ ] T023 [P] [US1] Edit `web/tests/e2e/platform-status.spec.ts`: import switch + `test.use({ persona: 'reader' })` + remove the `test.fixme(...)` wrapper
- [ ] T024 [P] [US1] Edit `web/tests/e2e/role-aware-affordances.spec.ts`: import switch + `test.use({ persona: 'reader' })` + remove the `test.fixme(...)` wrapper
- [ ] T025 [P] [US1] Edit `web/tests/e2e/registry/relationships-audit.e2e.spec.ts`: import switch + `test.use({ persona: 'reader' })` + remove the `test.fixme(...)` wrapper
- [ ] T026 [P] [US1] Edit `web/tests/e2e/registry/sc-010-time-to-find.e2e.spec.ts`: import switch + `test.use({ persona: 'reader' })` + remove the `test.fixme(...)` wrapper
- [ ] T027 [P] [US1] Edit `web/tests/e2e/registry/search.e2e.spec.ts`: import switch + `test.use({ persona: 'reader' })` + remove **both** `test.fixme(...)` wrappers (one for the typeahead case, one for the 503 case)
- [ ] T028 [P] [US1] Edit `web/tests/e2e/registry/unauthorized-state.e2e.spec.ts`: import switch + `test.use({ persona: 'reader' })` + remove the `test.fixme(...)` wrapper (the spec asserts a 401-from-API state on a page rendered in a Reader-authenticated context)

### Local validation

- [ ] T029 [US1] Export the four `E2E_TEST_USER_<PERSONA>_UPN` and four `E2E_TEST_USER_<PERSONA>_PASSWORD` env vars per quickstart.md Part A step 3 (only the `reader` pair is consumed at this phase, but exporting all four matches the future-state quickstart). Run `pnpm -C web test:e2e` against the dev tenant with the backend running locally (real Entra validation, in-memory persistence — see T034 / T035 for the CI-side change; local backend bring-up is documented in quickstart.md Part A step 4). Confirm all eight tasks T022–T028 specs pass. Capture the wall time for globalSetup as a sanity check against plan.md's ≤90s budget.

**Checkpoint**: User Story 1 fully functional. Eight of twelve fixme'd cases now run for real on a developer workstation; suite signal restored on the bulk of authenticated-route coverage. P1 alone is shippable value.

---

## Phase 4: User Story 2 — Multi-persona coverage (Priority: P2)

**Goal**: Role-conditional UI and no-access experience tests run as their intended personas. All twelve fixme'd cases are now live.

**Independent Test**: With four personas wired, run `pnpm -C web exec playwright test tests/e2e/no-access-experience.spec.ts` (uses `none`) and `pnpm -C web exec playwright test tests/e2e/registry/create-browse.e2e.spec.ts` (uses `operator`) against the dev tenant. Both must reach their target screens and pass their assertions. Then run `pnpm -C web test:e2e` (the full suite) and confirm twelve cases that were `test.fixme` are now live and pass.

### Extend globalSetup + fixture to all four personas

- [ ] T030 [US2] Edit `web/tests/auth/global-setup.ts`: replace the single-persona sign-in with a `for (const persona of personas) { ... }` loop over `Object.keys(PERSONA_CONFIGS) as Persona[]`. For each persona, perform the same sign-in + capture + post-write assertion pattern from T019. **Important**: run the personas serially (not in parallel) to keep Microsoft sign-in pacing conservative and avoid CAPTCHA / rate-limit risk in a sub-90s budget. For the `none` persona, the role-claim assertion expects an empty/absent `roles` claim — the helper must handle that case without treating "no claim" as "wrong claim."
- [ ] T031 [US2] Edit `web/tests/fixtures/auth.ts`: tighten the `persona` option's runtime validation to assert the value (if defined) is one of the four enum literals; emit a clear diagnostic if a test misspells the persona name. Pure additive — does not change the behavior for valid persona values.

### Un-fixme the four remaining cases (each touches a different file — all [P])

- [ ] T032 [P] [US2] Edit `web/tests/e2e/no-access-experience.spec.ts`: import switch + `test.use({ persona: 'none' })` + remove the `test.fixme(...)` wrapper; preserve the existing `Date.now()` post-redirect timestamp + `toBeVisible({ timeout: 2000 })` dual assertion exactly as authored
- [ ] T033 [P] [US2] Edit `web/tests/e2e/registry/create-browse.e2e.spec.ts`: import switch + `test.use({ persona: 'operator' })` + remove the `test.fixme(...)` wrapper
- [ ] T034 [P] [US2] Edit `web/tests/e2e/registry/delete-blocked.e2e.spec.ts`: import switch + `test.use({ persona: 'operator' })` + remove the `test.fixme(...)` wrapper
- [ ] T035 [P] [US2] Edit `web/tests/e2e/registry/edit-conflict.e2e.spec.ts`: import switch + `test.use({ persona: 'operator' })` + remove the `test.fixme(...)` wrapper

### Validation

- [ ] T036 [US2] Run `pnpm -C web test:e2e` against the dev tenant. Confirm: (a) all twelve previously-fixme'd cases run and pass; (b) running a Reader spec and an Operator spec in the same run produces no cross-session leakage (FR-006 — manually verify by inspecting that the Operator-using create-browse test successfully mutates registry state while the Reader-using role-aware-affordances test in the same run still observes Reader-only nav)
- [ ] T037 [US2] Add an inline comment in `web/tests/auth/personas.ts` next to the `admin` entry of `PERSONA_CONFIGS` documenting that the persona is provisioned-but-currently-unused — no test consumer in v1; reserved for forthcoming admin-scoped specs. The user-facing equivalent of this note is added to `web/tests/auth/README.md` in T046; this in-code comment is the version that's visible to a test author touching the file.

**Checkpoint**: All twelve fixme'd cases live. The complete authenticated suite runs on a developer workstation against the dev tenant. SC-001 satisfied locally; SC-002 satisfied by grep (`grep -r "MSAL E2E auth fixture" web/tests/ | grep test.fixme` returns nothing).

---

## Phase 5: User Story 3 — Authenticated E2E on every PR (Priority: P3)

**Goal**: The suite runs in CI on every pull request, pulling credentials from Key Vault via OIDC federation. A rotation procedure exists and is tested end-to-end.

**Independent Test**: Open a no-op PR after this phase completes. The frontend job runs the authenticated E2E suite to completion against the dev tenant. Inspect logs and artifacts — no credentials present. Then run `./scripts/e2e-test-identities/rotate-password.sh reader`; open another PR; confirm the next CI run still passes.

### Backend mode switch for the CI E2E job

- [ ] T038 [US3] Inspect `api/BusTerminal.Api/Program.cs` and the spec-006 registry persistence wiring. If a `REGISTRY_PERSISTENCE=InMemory` (or equivalent env-var-driven) switch already exists for selecting `InMemoryRegistryStores` over Cosmos, document the env var in this task's comment and skip implementation. Otherwise, add a thin env-var-driven DI selection that swaps `IRegistryEntityStore` / `IAuditEventStore` to the existing `InMemoryRegistryStores` implementations when `REGISTRY_PERSISTENCE=InMemory` is set. No new code paths; only DI selection.
- [ ] T039 [US3] Update the backend's `Program.cs` (or wherever the existing mock-auth handler is wired) to ensure the mock handler is only active when explicitly enabled via env var (e.g., `AzureAd__UseMockHandler=true`) and is **off by default** in non-mock env-var configurations. Required because CI for this feature must validate real tokens; leaving the mock handler implicit creates a footgun.

### CI workflow extension

- [ ] T040 [US3] Edit `.github/workflows/ci.yml` — `frontend` job: (a) add an `azure/login@v2` step using the existing GH OIDC federation pattern (mirror the auth shape from `cd-dev.yml` or `iac-apply-dev.yml`); resolve the federation per T017b's chosen option (either declare `environment: dev` on the job, or use the new CI-dedicated MI's client ID); (b) add a step that resolves persona UPNs from `terraform output` (or from a fixed naming convention with `unique_suffix` resolved from `vars.DEV_UNIQUE_SUFFIX`) and fetches the four passwords via `az keyvault secret show --vault-name ${{ vars.DEV_KV_NAME }} --name e2e-test-user-<persona>-password --query value -o tsv`, exporting each as a `>> $GITHUB_ENV` line (which masks them in subsequent step logs); (c) replace the existing "Start backend in background (mock auth)" step's env vars with real-Entra values (`AzureAd__TenantId=${{ vars.DEV_TENANT_ID }}`, `AzureAd__ClientId=${{ vars.DEV_API_CLIENT_ID }}`, `AzureAd__Audience=api://${{ vars.DEV_API_CLIENT_ID }}`) and add `REGISTRY_PERSISTENCE=InMemory` and `AzureAd__UseMockHandler=false`; (d) leave the backend-readiness probe, Playwright invocation, and the existing "Stop backend" / "Backend log tail" step untouched — those steps remain useful for debug visibility under the real-Entra config; the plan's "log artifacts no longer apply" language refers to the mock-handler-debugging purpose, not literal step removal. **All four CI variables** (`DEV_KV_NAME`, `DEV_UNIQUE_SUFFIX`, `DEV_TENANT_ID`, `DEV_API_CLIENT_ID`) **are configured as GitHub repository variables** (Settings → Secrets and variables → Actions → Variables tab) so they appear in the repo UI alongside the existing federation config — they are non-sensitive (tenant ID + client IDs + KV name + suffix are public-equivalent values) and intentionally NOT secrets.
- [ ] T041 [US3] Edit `.github/workflows/ci.yml` — `frontend` job: add a final post-Playwright step that scans `web/test-results/` and `web/playwright-report/` for any of the four persona passwords (via `printenv | grep E2E_TEST_USER` piped to a `grep -r -f -` over those directories); fail the job loudly if any match. Belt-and-suspenders for SC-005 ("no credential in artifacts"). This step runs `if: always()` so it executes even when the suite fails.
- [ ] T042 [US3] Verify in a follow-up PR: open a no-op PR; confirm the frontend CI job runs to completion, executes all twelve previously-fixme'd cases, and reports pass to the PR. Capture the wall-time delta vs. the pre-feature CI run.

### Rotation script + operational tooling

- [ ] T043 [US3] Create `scripts/e2e-test-identities/rotate-password.sh` per `contracts/keyvault-secret-naming.md` § "Rotation contract": `set -euo pipefail`; validate persona arg; generate 32-char password via `openssl rand -base64 32 | tr -d '/+=' | head -c 32`; `az ad user password reset --id <upn-derived-from-naming-convention-or-env> --password <new>`; `az keyvault secret set --vault-name <env-or-arg> --name e2e-test-user-<persona>-password --value <new>`; `rm -f web/tests/.auth/<persona>.json`; print single-line confirmation; **never echo the password to stdout/stderr**; `chmod +x` the file in the same commit
- [ ] T044 [US3] Add a `--dry-run` flag to `scripts/e2e-test-identities/rotate-password.sh` that prints the resolved UPN and KV secret name (no values, no Graph/KV writes) — useful for the validation step and for operators sanity-checking before pulling the trigger
- [ ] T045 [US3] Execute one rotation end-to-end against the `reader` persona: run the script; open a PR (or trigger a no-op CI run); confirm CI passes. This validates SC-006 (≤30 min for an operator to rotate and see CI green) and the deterministic-failure-on-collision property documented in research §R7

### Documentation

- [ ] T046 [P] [US3] Create `web/tests/auth/README.md` (concise — under one screen): the four personas and what each is for (including a short note that `admin` is provisioned-but-currently-unused — reserved for forthcoming admin-scoped specs, mirroring the T037 in-code comment); how to declare one with `test.use({ persona: ... })`; "don't do this" list (don't bypass the fixture; don't hand-craft MSAL state; don't commit the `.auth/` directory); pointer to `quickstart.md` for environment setup and to `contracts/fixture-api.md` for the formal contract
- [ ] T047 [P] [US3] Verify `quickstart.md` Part A by following it from a fresh clone (or as close as practical without a literal fresh clone — `pnpm -C web exec playwright uninstall && pnpm -C web exec playwright install --with-deps` simulates the first-time browser install) and time the steps. If any step takes longer than expected or has a gap, edit `quickstart.md` to fix it. Document the timing in a single line at the end of Part A's troubleshooting section.

**Checkpoint**: Suite runs on every PR; rotation is documented and validated; no credentials in artifacts. All three success criteria families (SC-001/SC-002, SC-004/SC-005, SC-006/SC-007) demonstrably satisfied.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Cross-references back to other specs that pointed at this work; loose-end cleanup.

- [ ] T048 [P] Update the spec-003 cross-references that point at "T093 in Phase 9 polish" — edit `specs/003-auth-and-identity/tasks.md` lines 128, 255, 256, 259 to add a `*(Closed by spec 007-playwright-auth-fixture.)*` annotation immediately after the existing `test.fixme` caveat in each. Do not change task-state or any other content
- [ ] T049 [P] Update the spec-006 cross-references that point at "T093" — same treatment: search `specs/006-service-bus-registry-core/tasks.md` for occurrences of "T093" and add the same closing annotation
- [ ] T050 Run a final grep to confirm zero residual `test.fixme` markers attributable to "MSAL E2E auth fixture": `grep -rn "test.fixme" web/tests/e2e/ | grep -v "malformed bearer"` returns no results. Document the result in a single-line confirmation appended to `specs/007-playwright-auth-fixture/checklists/requirements.md` under Notes
- [ ] T051 [P] Run the project's existing IaC policy gates (`iac/policies/run-policies.sh` + checkov + tfsec) against the dev composition to confirm the new module and KV role assignments raise no new findings; if any new finding emerges (most likely BT-IAC-001 around the `Key Vault Secrets User` assignments), add the rationale to `iac/policies/allowlist.json` with a short justification
- [ ] T052 [P] Document the FR-005 enforcement stance in `research.md` under R8: long-run token-expiry handling is **inherited from MSAL's silent-refresh behavior, not asserted by execution**. The suite's typical wall time is well under the access-token lifetime (~1h), so no test forces a refresh; the requirement is verified by behavior inheritance from `@azure/msal-browser`. If a future suite-time grows long enough that mid-run refresh becomes routinely exercised, the `traceparent` assertion in `msal-sign-in-and-whoami.spec.ts` will naturally cover it (refresh requests propagate `traceparent` exactly as any other UI-originated request). Add a one-line note to `web/tests/auth/README.md` (`Not asserted by tests`: long-run token refresh, see research.md R8 — inherited from MSAL behavior).

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (Phase 1)**: No dependencies. T001–T004 are all `[P]` and may proceed in parallel.
- **Foundational (Phase 2)**: Depends on Setup. T005–T009 are `[P]` (different files). T010 depends on T005–T009. T011 depends on T010. T012 depends on T011. T013 depends on T012 (real `tofu apply`). T014 depends on T013. T015–T017 are `[P]` with each other and with the IaC chain (frontend scaffolding does not depend on `tofu apply` having completed). **T017b** (CI federation pre-flight) is independent of the rest of Foundational and can land anywhere in Phase 2; it must complete before T040 (CI workflow extension in US3) — if option (a) is chosen, T017b's IaC change must be applied via `tofu apply` before T042's PR-trigger validation.
- **User Story 1 (Phase 3)**: Depends on Foundational complete. T018–T021 are sequential. T022–T028 are all `[P]` (different files), each depends on T020 (fixture wired into config). T029 depends on T022–T028.
- **User Story 2 (Phase 4)**: Depends on US1 complete (extends globalSetup and fixture). T030, T031 sequential then `[P]` with each other. T032–T035 are `[P]`, each depends on T030–T031. T036 depends on T032–T035. T037 only depends on T015 (`personas.ts` existing) — Foundational-era dependency, not cross-phase.
- **User Story 3 (Phase 5)**: Depends on US2 complete (CI needs all four personas wired). T038, T039 sequential. T040 depends on T038–T039. T041 depends on T040. T042 depends on T041. T043, T044 `[P]` with each other; T045 depends on both. T046, T047 are `[P]` with everything else in US3.
- **Polish (Phase 6)**: Depends on Phase 5 complete.

### Within each user story

- Sign-in driver / globalSetup before fixture wiring before spec-adoption
- Spec-adoption tasks are `[P]` with each other (different files)
- Local validation gate at the end of each story before moving to the next

### Parallel opportunities

- **Phase 1**: All four tasks `[P]` (T001–T004)
- **Phase 2 IaC files**: T005–T009 `[P]`
- **Phase 2 frontend scaffolding**: T015–T017 `[P]` with each other AND with the IaC chain
- **Phase 3 spec-adoption tasks**: T022–T028 `[P]` (seven specs, seven different files)
- **Phase 4 spec-adoption tasks**: T032–T035 `[P]` (four specs, four different files)
- **Phase 5 docs**: T046–T047 `[P]` with each other and with the CI/rotation chain
- **Phase 6 cross-references**: T048, T049, T051 `[P]` with each other (T050 sequential to validate the un-fixme result)

---

## Parallel Examples

### Foundational phase — IaC module files

```bash
# T005–T009 all touch different files in iac/modules/e2e-test-identities/
Task: "Create iac/modules/e2e-test-identities/versions.tf"
Task: "Create iac/modules/e2e-test-identities/variables.tf"
Task: "Create iac/modules/e2e-test-identities/main.tf"
Task: "Create iac/modules/e2e-test-identities/outputs.tf"
Task: "Create iac/modules/e2e-test-identities/README.md with terraform-docs markers"
```

### Phase 3 — un-fixme the seven Reader-using spec files in parallel

```bash
Task: "Un-fixme web/tests/e2e/msal-sign-in-and-whoami.spec.ts (sign-in cycle case)"
Task: "Un-fixme web/tests/e2e/platform-status.spec.ts"
Task: "Un-fixme web/tests/e2e/role-aware-affordances.spec.ts"
Task: "Un-fixme web/tests/e2e/registry/relationships-audit.e2e.spec.ts"
Task: "Un-fixme web/tests/e2e/registry/sc-010-time-to-find.e2e.spec.ts"
Task: "Un-fixme web/tests/e2e/registry/search.e2e.spec.ts (both cases)"
Task: "Un-fixme web/tests/e2e/registry/unauthorized-state.e2e.spec.ts"
```

---

## Implementation Strategy

### MVP first (User Story 1 only)

1. Complete Phase 1: Setup (≤ 1 hour — directory + gitignore plumbing)
2. Complete Phase 2: Foundational (≤ 1 day — IaC module + `tofu apply` against dev + frontend scaffolding)
3. Complete Phase 3: User Story 1 (≤ 1–2 days — sign-in driver + globalSetup for reader + un-fixme 8 specs + local validation)
4. **STOP and VALIDATE**: Eight of twelve fixme'd cases run for real locally. Demonstrably restores authenticated-route coverage for the majority of the suite.
5. Optional: ship US1 alone if team capacity is constrained. US2/US3 unblock the rest but US1 alone closes the bleeding.

### Incremental delivery

- After US1: suite signal restored for Reader-using flows.
- After US2: full twelve-case coverage locally; suite signal restored end-to-end.
- After US3: coverage runs on every PR; rotation tested; SC-005 (no-credentials-in-artifacts) demonstrably enforced.
- After Polish: cross-references closed; downstream specs (003, 006) annotated as closed.

### Parallel team strategy

- **One developer**: Phases sequentially — about 4–6 working days end-to-end.
- **Two developers**: After Phase 2 completes, Dev A drives US1 (eight spec un-fixmes) while Dev B drives the US3 backend mode switch + CI workflow draft (T038–T040). Dev A's US1 completion gates Dev B's CI validation (T042) but T038–T040 can be authored in parallel and rebased onto US1's branch when ready.
- **Three developers**: Add Dev C to handle docs (T046–T047) and the IaC policy/allowlist updates (T051) during the US1/US3 parallel period.

---

## Notes

- `[P]` tasks = different files, no dependencies on in-progress tasks
- `[Story]` label maps each task to its user story for traceability
- Each user story is independently completable and locally validatable
- No new test scaffolding is authored by this feature; the work in each story includes adopting the existing fixme'd specs to the new fixture per FR-004
- Commit after each task or each logical group (a story's spec-adoption batch can land as one commit)
- Stop at any story checkpoint to validate the story independently
- Avoid: editing the same spec file in two parallel tasks; introducing new test scaffolding (the fixme'd specs already exist and are the test surface this feature un-suspends)
