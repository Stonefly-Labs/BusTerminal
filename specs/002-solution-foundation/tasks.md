---

description: "Task list for feature 002-solution-foundation"
---

# Tasks: Solution Foundation

**Input**: Design documents from `specs/002-solution-foundation/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/whoami.openapi.yaml, quickstart.md (all present)

**Tests**: This slice DOES include test tasks. The constitution mandates unit/integration/contract/UI/E2E coverage, and the foundation is the substrate every later slice will trust — getting the harness in place now (not TDD-first, but alongside implementation) is a deliberate cost saved later.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3, US4, US5)
- Include exact file paths in descriptions

## Path Conventions

Per `plan.md` § Project Structure:

- Frontend: `web/` (inherited from slice 001, extended here)
- Backend: `api/BusTerminal.Api/`
- Backend tests: `api/BusTerminal.Api.Tests/`
- IaC: `iac/`
- CI/CD: `.github/workflows/`
- Dev scripts: `scripts/`
- Docs: `docs/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and the top-level layout decided in `plan.md`.

- [X] T001 Create the new top-level directory skeleton at the repository root (`api/`, `iac/`, `.github/workflows/`, `scripts/`, `docs/`). Add a `.gitkeep` only where the directory would otherwise be empty until later tasks populate it.
- [X] T002 [P] Create the .NET solution and backend project: `dotnet new sln -o api -n BusTerminal` then `dotnet new webapi --use-minimal-apis --auth None --framework net10.0 -o api/BusTerminal.Api -n BusTerminal.Api` and add to the solution. Set `<Nullable>enable</Nullable>` and `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` in the `.csproj`. (Note: .NET 10's `dotnet new sln` produces the modern `BusTerminal.slnx` XML format rather than legacy `.sln` — substitute the `.slnx` path everywhere later tasks reference `BusTerminal.sln`.)
- [X] T003 [P] Create the backend test project: `dotnet new xunit -o api/BusTerminal.Api.Tests -n BusTerminal.Api.Tests --framework net10.0`, add `Microsoft.AspNetCore.Mvc.Testing` and `FluentAssertions` packages, reference `BusTerminal.Api`, and add to the solution. (Note: FluentAssertions pinned to `7.2.0` — the last MIT-licensed release; v8+ switched to a paid commercial license. Re-evaluate if a license is procured.)
- [X] T004 [P] Add `.editorconfig` at repo root with `dotnet_diagnostic.*` rules aligning .NET style with the existing TypeScript style; ensure C# uses 4-space indent and file-scoped namespaces.
- [X] T005 [P] Add `dotnet-tools.json` under `api/` pinning the `dotnet format` and `dotnet outdated` versions; commit to enable `dotnet tool restore` deterministically. (Manifest written to `api/dotnet-tools.json`; `dotnet tool restore` from `api/` resolves both tools.)
- [X] T006 [P] Initialize the `iac/` OpenTofu layout: create `iac/.terraform-version` pinning the OpenTofu CLI version; add `iac/providers.tf` declaring the `azurerm` and `azuread` providers with explicit version constraints (`~> 4.0` and `~> 3.0` respectively).
- [X] T007 [P] Create `iac/.tflint.hcl` with the `terraform_required_version`, `terraform_required_providers`, and `azurerm` rule sets enabled.
- [X] T008 [P] Add `scripts/bootstrap.ps1` and `scripts/bootstrap.sh` that verify the documented prerequisite versions (`.NET 10`, Node 22, pnpm, OpenTofu 1.10+, Azure CLI 2.60+, Docker, PowerShell 7.4+) and print actionable gaps. No installs — just a verification script.
- [X] T009 [P] Add `.gitleaks.toml` at repo root with the default ruleset plus an allowlist for documented placeholder patterns (e.g., `<your-tenant-id>`, `00000000-0000-0000-0000-000000000000`).
- [X] T010 Update the root `README.md` to reference the new top-level directories (`api/`, `iac/`, `scripts/`, `docs/`) and link to `specs/002-solution-foundation/quickstart.md` for setup.

**Checkpoint**: Repo structure exists; both languages can build empty projects; tooling versions are pinned.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The cross-cutting infrastructure every user story depends on. No user story can be tested end-to-end until this phase is complete.

**⚠️ CRITICAL**: No user story work in Phases 3–7 can begin until this phase is complete.

### Backend foundation (Minimal API host + auth + telemetry + health)

- [X] T011 Add backend NuGet packages to `api/BusTerminal.Api/BusTerminal.Api.csproj`: `Microsoft.Identity.Web`, `Azure.Monitor.OpenTelemetry.AspNetCore`, `Azure.Identity`, `Microsoft.Extensions.Configuration.AzureKeyVault`, `Microsoft.AspNetCore.OpenApi`, `Serilog.AspNetCore`, `Serilog.Sinks.OpenTelemetry`, `AspNetCore.HealthChecks.UriHealthCheck`. Pin every package version explicitly.
- [X] T012 Implement `api/BusTerminal.Api/Infrastructure/Authentication/AuthenticationExtensions.cs` exposing `IServiceCollection.AddBusTerminalAuthentication(IConfiguration)` that wires `AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddMicrosoftIdentityWebApi(config.GetSection("AzureAd"))` and adds a global `AuthorizationBuilder().AddPolicy("RequireAuthenticatedUser", p => p.RequireAuthenticatedUser())`.
- [X] T013 Implement `api/BusTerminal.Api/Infrastructure/Observability/OpenTelemetryExtensions.cs` exposing `IServiceCollection.AddBusTerminalTelemetry(IConfiguration, IHostEnvironment)` that calls `AddOpenTelemetry().UseAzureMonitor()` only when the App Insights connection string is present; otherwise registers a no-op exporter so local dev produces console output. Wire Serilog through the OTel logger pipeline.
- [X] T014 Implement `api/BusTerminal.Api/Infrastructure/Configuration/KeyVaultExtensions.cs` exposing `IConfigurationBuilder.AddBusTerminalKeyVault(IHostEnvironment)` that, when the `AZURE_KEY_VAULT_URI` env var is set, adds Key Vault as a configuration source using `DefaultAzureCredential` (managed identity in Azure, developer credential locally).
- [X] T015 Implement `api/BusTerminal.Api/Features/Health/HealthEndpoints.cs` registering three distinct routes — `GET /healthz/live` (trivial 200), `GET /healthz/ready` (checks Entra metadata + Key Vault if configured), `GET /healthz/startup` (returns 503 until first successful Entra metadata fetch completes). Each endpoint returns a small JSON body with status + check details for telemetry friendliness.
- [X] T016 Wire `api/BusTerminal.Api/Program.cs` to call `AddBusTerminalAuthentication`, `AddBusTerminalTelemetry`, `AddBusTerminalKeyVault`, register the health endpoints, enable OpenAPI generation via `app.MapOpenApi()`, and apply `app.UseAuthentication()` + `app.UseAuthorization()`. Bind to port `8080` by default.
- [X] T017 Add `api/BusTerminal.Api/appsettings.json` with placeholders for `AzureAd:Instance`, `AzureAd:TenantId`, `AzureAd:ClientId`, `AzureAd:Audience`, plus log-level config. NO real values; documented as required env-injected at runtime.
- [X] T018 Add `api/BusTerminal.Api/appsettings.Development.json.example` with `AzureAd:TenantId=development` (the mock-auth sentinel value per `research.md` § 10) and add `appsettings.Development.json` to `.gitignore`.
- [X] T019 Implement `api/BusTerminal.Api/Infrastructure/Authentication/MockAuthenticationHandler.cs` — a development-only handler activated when `AzureAd:TenantId == "development"` that synthesizes a fixed development principal (oid `00000000-0000-0000-0000-000000000001`, display name `Dev User`). Gated on `IHostEnvironment.IsDevelopment()` — MUST throw on Production.
- [X] T020 [P] Add integration test `api/BusTerminal.Api.Tests/Integration/HealthEndpointTests.cs` covering live/ready/startup happy paths and the "ready returns 503 before startup completes" sequence using `WebApplicationFactory<Program>`.

### Frontend foundation (auth + telemetry adapter + typed API client)

- [X] T021 Update `web/package.json` to add the auth and telemetry dependencies: `next-auth@^5`, `@auth/core`, `@microsoft/applicationinsights-web`, `@microsoft/applicationinsights-react-js`. Pin major versions.
- [X] T022 Implement `web/lib/auth.ts` — NextAuth.js v5 configuration with the Microsoft Entra ID provider, reading `AZURE_AD_CLIENT_ID`, `AZURE_AD_CLIENT_SECRET` (from Key Vault in prod), `AZURE_AD_TENANT_ID`, and `NEXTAUTH_SECRET` from environment. Implements the `jwt`/`session` callbacks to expose `session.accessToken` for server-component fetches. Supports the documented mock-auth fallback (returns a synthetic session when `AZURE_AD_TENANT_ID === 'development'`).
- [X] T023 Implement `web/app/api/auth/[...nextauth]/route.ts` — the App Router catch-all handler delegating to the Auth.js handlers exported by `lib/auth.ts`.
- [X] T024 Implement `web/middleware.ts` (or extend it if slice 001 already created one) to redirect unauthenticated requests for routes under `(authenticated)/` to `/signin` and preserve the original URL as `callbackUrl`.
- [X] T025 Implement `web/lib/telemetry/trace-context.ts` exposing `generateTraceparent()` and `getOrCreateActiveTraceContext()` — produces W3C-compliant `traceparent` (16-byte trace ID, 8-byte span ID, sample flag) and optional `tracestate` for every outbound fetch. Active **regardless** of the active observability adapter (per FR-022).
- [X] T026 Implement `web/lib/telemetry/ai-adapter.ts` — an Application Insights JS SDK adapter implementing the slice-001 observability adapter interface. Activates only when `process.env.NEXT_PUBLIC_APPINSIGHTS_CONNECTION_STRING` is present. Registers route-change tracking via a `usePageView` hook.
- [X] T027 Update `web/lib/telemetry/index.ts` (or equivalent slice-001 adapter selector) to pick the AI adapter when the connection string is set, falling back to the existing no-op adapter otherwise.
- [X] T028 Implement `web/lib/api-client.ts` — a typed `fetch` wrapper that: (a) reads `NEXT_PUBLIC_API_BASE_URL`, (b) attaches `Authorization: Bearer <session.accessToken>` from the current session, (c) attaches `traceparent`/`tracestate` from `trace-context.ts`, (d) handles 401 by triggering re-auth, (e) returns a discriminated union (`{ ok: true, data } | { ok: false, error }`) so callers handle errors without throwing.
- [X] T029 [P] Add Vitest test `web/lib/telemetry/__tests__/trace-context.test.ts` asserting the produced `traceparent` matches the W3C regex and is unique across invocations.
- [X] T030 [P] Add Vitest test `web/lib/__tests__/api-client.test.ts` mocking `fetch` to assert that (a) Authorization header is attached, (b) `traceparent` is attached, (c) 401 surfaces as `{ ok: false, error: 'unauthenticated' }`.

### IaC foundation (modules + bootstrap)

- [X] T031 Create `iac/platform-bootstrap/main.tf` provisioning: a dedicated resource group (`rg-busterminal-tfstate`), an Azure Storage Account with versioning + soft-delete + HTTPS-only + TLS1.2 minimum, a blob container `tfstate`, a user-assigned managed identity per environment passed in as input, and a federated identity credential on each with the subject template `repo:<org>/BusTerminal:environment:<env>`. Use AVM where coverage exists (`Azure/avm-res-storage-storageaccount/azurerm`, `Azure/avm-res-managedidentity-userassignedidentity/azurerm`) with pinned versions.
- [X] T032 Create `iac/platform-bootstrap/variables.tf` declaring `github_org_repo` (string), `environments` (set of strings, default `["dev"]`), `location` (default `eastus2`), `subscription_id`.
- [X] T033 Create `iac/platform-bootstrap/outputs.tf` emitting the values needed as GitHub repository variables: per-environment `AZURE_CLIENT_ID`, plus shared `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `TFSTATE_STORAGE_ACCOUNT_NAME`, `TFSTATE_RESOURCE_GROUP`, `TFSTATE_CONTAINER_NAME`.
- [X] T034 Write `iac/platform-bootstrap/README.md` documenting how to run the module (`tofu init -backend=false && tofu apply -var-file=...`) plus the equivalent manual `az` CLI walkthrough required by FR-082b. Include the GitHub repository-variable mapping table.
- [X] T035 [P] Create `iac/modules/identity/` (workload-MI provisioning + role-assignment helpers) consuming the AVM user-assigned-identity module.
- [X] T036 [P] Create `iac/modules/monitoring/` (Log Analytics Workspace + Application Insights connected to the workspace + a `key_vault_secret` resource that exposes the App Insights connection string for workload consumption).
- [X] T037 [P] Create `iac/modules/keyvault/` (Key Vault using RBAC authorization, no access policies, diagnostic settings to the LAW from `monitoring`).
- [X] T038 [P] Create `iac/modules/container-registry/` (ACR with admin disabled, diagnostic settings to LAW, an output for the login server).
- [X] T039 [P] Create `iac/modules/container-apps-env/` (Container Apps Environment bound to the LAW from `monitoring`, no VNet for this slice, diagnostic settings enabled).
- [X] T040 [P] Create `iac/modules/container-app/` (single Container App composing image, env vars, secrets (Key Vault refs), managed identity, scale rules, three health probes — liveness/readiness/startup — and a configurable `ingress_external` boolean defaulting `false` so a later slice can flip to internal).

**Checkpoint**: All cross-cutting infrastructure is in place. User stories can now proceed in parallel.

---

## Phase 3: User Story 1 — New developer can run the whole solution locally on first day (Priority: P1) 🎯 MVP

**Goal**: A new engineer clones the repo, runs the documented bootstrap, and gets the frontend + backend running locally with a working authenticated `/whoami` round-trip in under 30 minutes (SC-001).

**Independent Test**: On a fresh machine with the documented prerequisites installed, executing `scripts/start-local.ps1` produces a local frontend (port 3000) and local backend (port 5000) that authenticate via the mock-auth fallback and complete the platform-status round-trip; the developer sees their identity and a correlation ID rendered.

### Implementation for User Story 1

- [X] T041 [US1] Implement `api/BusTerminal.Api/Features/Identity/WhoAmIEndpoint.cs` — a Minimal API endpoint `MapGet("/whoami", [authorize] (HttpContext ctx) => ...)` that returns the `WhoAmIResponse` shape from `contracts/whoami.openapi.yaml`: principal (oid, displayName, tenantId, preferredUsername), correlation (traceId from `Activity.Current`, spanId, receivedTraceparent), server (environment from `IHostEnvironment.EnvironmentName`, revision from `CONTAINER_APP_REVISION` env var or `"local"`, serverTimeUtc).
- [X] T042 [P] [US1] Add integration test `api/BusTerminal.Api.Tests/Integration/WhoAmIEndpointTests.cs` covering: (a) unauthenticated request → 401 + `WWW-Authenticate` header, (b) authenticated request (mock auth) → 200 with the expected principal shape, (c) traceparent echo round-trips correctly.
- [X] T043 [P] [US1] Implement `web/components/layout/navigation-shell.tsx` — header containing the product logo (using slice-001 brand tokens), the theme toggle (consuming the existing `next-themes` hook), and a placeholder user-menu slot; a sidebar region with a "navigation will live here" stub. Server component by default.
- [X] T044 [P] [US1] Implement `web/components/layout/user-menu.tsx` — client component showing the signed-in user's display name with a dropdown providing "Sign out" (calls the Auth.js `signOut` server action).
- [X] T045 [US1] Implement `web/app/(authenticated)/layout.tsx` — a layout that fetches the current session server-side, redirects to `/signin` if unauthenticated, and renders `<NavigationShell><UserMenu user={session.user} />{children}</NavigationShell>`.
- [X] T046 [US1] Implement `web/app/(authenticated)/platform-status/page.tsx` — a server component that calls the backend `/whoami` via `api-client.ts` (with the session's accessToken + generated traceparent) and renders: an "identity" card (oid, displayName, tenantId), a "correlation" card (traceId, spanId, receivedTraceparent), and a "server" card (environment, revision, serverTimeUtc). All components use slice-001 shadcn primitives. Includes an accessible loading skeleton and an error state.
- [X] T047 [P] [US1] Implement `web/app/(auth)/signin/page.tsx` — the sign-in entry that triggers Auth.js `signIn('microsoft-entra-id', { callbackUrl })` (or the dev-mode "Continue as Dev User" button when mock auth is active).
- [X] T048 [P] [US1] Implement `web/app/(auth)/signout/page.tsx` — a confirmation page calling `signOut({ callbackUrl: '/' })`.
- [X] T049 [US1] Update `web/app/page.tsx` to redirect to `/(authenticated)/platform-status` when a session exists, otherwise to `/signin`.
- [X] T050 [P] [US1] Add Playwright E2E test `web/tests/e2e/platform-status.spec.ts` that: (a) starts at `/`, (b) is redirected to `/signin`, (c) signs in via the dev-mode button, (d) lands on `/platform-status`, (e) sees the identity card with the dev user's display name, (f) sees a correlation card with a non-empty traceId. Configured to use the local backend on port 5000.
- [X] T051 [P] [US1] Add axe accessibility test `web/tests/a11y/platform-status.spec.ts` running against `/platform-status` and asserting zero violations of WCAG 2.2 AA rules.
- [X] T052 [P] [US1] Add `web/components/layout/__tests__/navigation-shell.test.tsx` (Vitest + RTL) asserting: rendered logo, theme-toggle present and toggles, user-menu slot renders children.
- [X] T053 [US1] Implement `scripts/start-local.ps1` (and `.sh`) that spawns `dotnet watch run --project api/BusTerminal.Api` and `pnpm --filter web dev` concurrently, captures interleaved structured-log output, traps Ctrl-C to stop both cleanly.
- [X] T054 [P] [US1] Add `api/BusTerminal.Api/Dockerfile` — multi-stage build using `mcr.microsoft.com/dotnet/sdk:10.0` for build, `mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled` for runtime. Non-root user, `EXPOSE 8080`, no `HEALTHCHECK` (Container Apps probes handle that).
- [X] T055 [P] [US1] Add `web/Dockerfile` — multi-stage build using `node:22-alpine` for build (`pnpm install --frozen-lockfile && pnpm build`), `node:22-alpine` for runtime. Set `output: 'standalone'` in `web/next.config.ts` if not already present.
- [X] T056 [US1] Add `docker-compose.yml` at the repo root composing both Dockerfiles with the same port mapping as native dev. Environment variables for mock-auth are pre-filled.
- [X] T057 [P] [US1] Add `scripts/start-local-containers.sh` (and `.ps1`) — thin wrapper calling `docker compose up --build`.
- [X] T058 [P] [US1] Add `scripts/test-all.ps1` (and `.sh`) running: `pnpm --filter web test`, `pnpm --filter web test:a11y`, `dotnet test api/BusTerminal.sln`, `tofu -chdir=iac/environments/dev validate`. Exit non-zero on any failure.
- [X] T059 [US1] Write `docs/local-development.md` — promotes the local-dev sections of `quickstart.md` into the canonical onboarding doc. Includes troubleshooting table from `quickstart.md` § 10.
- [ ] T060 [US1] Manually run the end-to-end onboarding on a clean working tree (delete `node_modules`, `bin/`, `obj/`, restart machine, run `bootstrap` → `start-local`) and confirm the elapsed time meets SC-001 (under 30 minutes). Capture timing in `docs/local-development.md` as a benchmark. _(Deferred: requires manual operator on a clean machine.)_

**Checkpoint**: A new developer can clone, bootstrap, and run the full stack locally with a working `/whoami` round-trip. US1 is independently testable and demonstrable.

---

## Phase 4: User Story 2 — Frontend and backend deploy automatically to a real Azure environment (Priority: P1)

**Goal**: A merge to `main` triggers a pipeline that builds images, runs OpenTofu against `dev`, deploys new revisions, and verifies health — without manual `az` commands. Completes in under 20 minutes for a no-infra-change deploy (SC-002).

**Independent Test**: From a clean Azure subscription with the bootstrap module already run, a single commit to `main` results in `frontend.<env>.azurecontainerapps.io` rendering the platform-status page after the user signs in through a real Entra ID tenant.

### Implementation for User Story 2

- [ ] T061 [US2] Compose `iac/environments/dev/main.tf` — wires the `monitoring`, `keyvault`, `container-registry`, `identity`, `container-apps-env`, and two `container-app` module invocations (frontend + backend). Frontend has `ingress_external = true`; backend has `ingress_external = true` with the documented external-but-token-protected posture (per spec clarification Q2). Reads image tags from variables so the pipeline can swap them per deploy.
- [ ] T062 [US2] Add `iac/environments/dev/backend.tf` configuring the `azurerm` remote-state backend with `key = "envs/dev/terraform.tfstate"` and the storage account name from bootstrap. NO secrets — uses managed-identity auth via the pipeline's federated credential.
- [ ] T063 [US2] Add `iac/environments/dev/variables.tf` and `iac/environments/dev/terraform.tfvars` with non-secret defaults: `environment_name = "dev"`, `location = "eastus2"`, `naming_prefix = "bt-dev"`, `frontend_min_replicas = 0`, `frontend_max_replicas = 3`, same for backend.
- [ ] T064 [US2] Compose the per-resource diagnostic settings in `iac/environments/dev/main.tf` so every Azure resource provisioned routes logs + AllMetrics to the LAW (FR-072). Uses the AVM modules' built-in diagnostic-settings parameters where possible; falls back to discrete `azurerm_monitor_diagnostic_setting` resources for those without.
- [ ] T065 [US2] Create `iac/environments/dev/outputs.tf` emitting the deployed frontend and backend FQDNs, the ACR login server, and the Application Insights connection string Key Vault secret URI.
- [ ] T066 [US2] Add `.github/workflows/ci.yml` — runs on every PR: `actions/checkout`, `setup-dotnet@v4` (10.0), `setup-node@v4` (22), `pnpm/action-setup`, then in parallel jobs: backend build+test, frontend build+test+a11y, `tofu validate` + `tflint` + `checkov`. Annotate the PR with results.
- [ ] T067 [US2] Add `.github/workflows/cd-dev.yml` — runs on push to `main` and matches the federation subject `repo:<org>/BusTerminal:environment:dev` via `environment: dev`. Steps: (1) `azure/login@v2` with OIDC, (2) build + push both images to ACR tagged `${{ github.sha }}` and `latest`, (3) `tofu apply -auto-approve` against `iac/environments/dev/` passing the new image tags as `-var` arguments, (4) wait for revisions to become healthy (poll `/healthz/ready` until 200 or 5-minute timeout), (5) post-deploy smoke step.
- [ ] T068 [P] [US2] Add `.github/workflows/iac-validate.yml` — runs on PRs that touch `iac/**`: `tofu fmt -check`, `tofu validate`, `tflint`, `checkov`, and `tofu plan -no-color` posted as a PR comment for review.
- [ ] T069 [P] [US2] Add `.github/dependabot.yml` enabling weekly updates for: `nuget` in `api/`, `npm` in `web/` (or `pnpm` via the docker ecosystem if dependabot lacks pnpm support — fall back to manually updating `pnpm-lock.yaml`), `terraform` in `iac/`, `github-actions` in `.github/workflows/`.
- [ ] T070 [US2] Add `scripts/bootstrap-platform.ps1` (and `.sh`) that wraps the `iac/platform-bootstrap/` module invocation with input prompts and `az login` verification.
- [ ] T071 [P] [US2] Add `scripts/bootstrap-platform-manual.md` — the equivalent manual `az` CLI walkthrough required by FR-082b. Cover: resource group create, storage account create, container create, identity create, federated credential add, RBAC role assignments.
- [ ] T072 [US2] Add a post-deploy smoke job to `cd-dev.yml` that calls the deployed `/whoami` endpoint using a CI-time token acquired via the pipeline's managed identity OBO flow; asserts 200 + non-empty principal + correlation block matches the OpenAPI contract.
- [ ] T073 [P] [US2] Add a contract-validation job in `ci.yml` that runs `redocly lint specs/002-solution-foundation/contracts/whoami.openapi.yaml` and diffs the live OpenAPI emitted by the backend (`/openapi/v1.json`) against the spec file in CI — fails on breaking changes.
- [ ] T074 [US2] Write `docs/deploying-environments.md` documenting the pipeline trigger model, the federated-credential scoping, the smoke-step expectations, and the rollback procedure (revert PR + re-run pipeline; Container Apps preserves previous revision until new one healthy).
- [ ] T075 [US2] Validate the end-to-end deploy from a clean state: bootstrap → push to main → observe a healthy `dev` deploy with a working sign-in + `/whoami` call against the real Entra tenant. Document the achieved deploy time (SC-002 target: < 20 min for no-infra change).

**Checkpoint**: A fresh commit to `main` produces a deployed environment without manual intervention. US2 is independently verifiable.

---

## Phase 5: User Story 3 — Operator can observe a deployed environment (Priority: P2)

**Goal**: An operator can find any request in the central telemetry by correlation ID and see logs/traces from both workloads end-to-end. Telemetry visible within 2 minutes (SC-003); zero PII in default telemetry (FR-073).

**Independent Test**: After triggering a request through the deployed frontend, the operator pastes the correlation ID from the platform-status page into Application Insights Transaction Search and sees the request span from frontend dependency through backend request.

### Implementation for User Story 3

- [ ] T076 [P] [US3] Verify diagnostic settings on every provisioned resource by adding a `iac/environments/dev/checks.tf` (or `null_resource` with `local-exec`) that runs an `az` query asserting every resource group's resources have a diagnostic setting pointing at the LAW. Or, more idiomatically, add a checkov custom check.
- [ ] T077 [P] [US3] Add an Application Insights workbook resource in `iac/modules/monitoring/workbook.tf` providing a "Platform Status" view: request volume, request latency p50/p95/p99, error rate, top 10 slowest dependencies, recent failures with correlation ID drill-through.
- [ ] T078 [P] [US3] Add an integration test `api/BusTerminal.Api.Tests/Integration/TelemetryTests.cs` that uses `WebApplicationFactory<Program>` with an in-memory `Activity` listener to assert: an inbound `traceparent` becomes the parent of the server span; the response correlation block's `traceId` matches `Activity.Current?.TraceId`.
- [ ] T079 [P] [US3] Add a frontend Vitest test `web/lib/__tests__/api-client.trace.test.ts` mocking `fetch` and asserting the outbound `traceparent` is W3C-compliant for both authenticated and unauthenticated calls.
- [ ] T080 [P] [US3] Write `docs/observability.md` documenting: starter KQL queries (find by correlation ID, top errors, slow dependencies), the workbook location, the LAW name per environment, and the documented telemetry-latency window (≤ 2 min).
- [ ] T081 [US3] Run a deliberate "find this request" exercise post-deploy: copy the correlation ID from a live platform-status page, paste into App Insights Transaction Search within 2 minutes, capture a screenshot for `docs/observability.md` as the canonical example.
- [ ] T082 [P] [US3] Add a PII-scrub guard test `api/BusTerminal.Api.Tests/Integration/TelemetryPiiTests.cs` that captures emitted span attributes during a `/whoami` call and asserts no field with the principal's display name or UPN appears as a span attribute (response body is allowed; span attributes are not).

**Checkpoint**: Telemetry is queryable, correlation works end-to-end, and the PII posture is automatically tested.

---

## Phase 6: User Story 4 — Security reviewer can verify the platform is "secure by default" (Priority: P2)

**Goal**: A security reviewer can confirm zero secrets in git, managed identities for all workload→Azure calls, OIDC federation for the pipeline, Entra-enforced sign-in for users, and automated gates that prevent regressions.

**Independent Test**: An external auditor runs `gitleaks detect` over the repo and gets zero findings; inspects each deployed workload and confirms managed identity; inspects pipeline auth and confirms federated credential.

### Implementation for User Story 4

- [ ] T083 [P] [US4] Add `.github/workflows/security.yml` — runs on PRs + on a weekly cron: `gitleaks/gitleaks-action@v2` with `.gitleaks.toml`, `aquasecurity/trivy-action` against built images, `dotnet list package --vulnerable --include-transitive` (failing on High+), `pnpm audit --audit-level high` (failing on High+).
- [ ] T084 [P] [US4] Add a pre-commit hook stub in `scripts/install-pre-commit-hooks.sh` that installs a `pre-commit` config running `gitleaks --staged` locally. Documented but not required.
- [ ] T085 [US4] Write `docs/identity-and-secrets.md` covering: Entra app registration one-time setup steps (with screenshots/CLI commands), workload managed-identity grants (Key Vault Secrets User, etc.), pipeline federated credential setup, secret-rotation procedure, and the documented "no app roles, authentication-only" posture inherited from spec clarification Q5.
- [ ] T086 [P] [US4] Add `api/BusTerminal.Api.Tests/Integration/AuthZTests.cs` asserting: unauthenticated calls to `/whoami` return 401 with no token-introspection details in the body, expired tokens return 401 with a generic `invalid_token` `WWW-Authenticate` challenge, wrong-audience tokens return 401.
- [ ] T087 [P] [US4] Add an OpenTofu check (using `precondition` blocks or `checkov` custom rules) that fails the plan if any `azurerm_role_assignment` grants `Owner` or `Contributor` to a workload identity (must be narrowly scoped — `Key Vault Secrets User` etc.).
- [ ] T088 [P] [US4] Add a CI step that scans built container images with Trivy for SBOM generation (`trivy image --format cyclonedx --output sbom.json <image>`) and uploads the SBOMs as build artifacts. Constitution compliance for supply-chain hygiene.
- [ ] T089 [US4] Confirm and document the security audit summary: run gitleaks + Trivy + the test suite against `main`, record the results in `docs/identity-and-secrets.md` § "Audit Evidence" so a future reviewer can re-run the same commands and see the same evidence.

**Checkpoint**: Security gates exist and pass; deviations require explicit changes (no hidden bypasses). The platform meets all FR-040 – FR-052 requirements.

---

## Phase 7: User Story 5 — Infrastructure engineer can stand up additional environments reproducibly (Priority: P3)

**Goal**: Adding a new environment (e.g., `test`) is a documentation-and-configuration exercise, not an architecture exercise. Target SC-006: under 1 working day for an engineer who hasn't touched the foundation.

**Independent Test**: A fresh engineer follows `docs/deploying-environments.md` to add a `test` environment; their first attempt produces a working environment without touching any file in `iac/modules/`.

### Implementation for User Story 5

- [ ] T090 [US5] Create `iac/environments/test/backend.tf` (template) — partition `key = "envs/test/terraform.tfstate"`, same storage account as dev. Documented as "copy and adjust per environment".
- [ ] T091 [P] [US5] Create `iac/environments/test/main.tf.example` — references the same modules dev does, with environment-specific variables. NOT a working `.tf` (the actual `main.tf` is created by the engineer adding the env, per the documented process), so adding `test` later cannot accidentally pre-provision resources.
- [ ] T092 [P] [US5] Create `iac/environments/test/terraform.tfvars.example` listing all variables an engineer must provide (location, naming_prefix, scaling defaults).
- [ ] T093 [P] [US5] Create `iac/environments/test/README.md` describing the steps to activate this environment: rename the `.example` files, run the bootstrap module with `-environments '["test"]'` to add the new pipeline identity + federated credential, set GitHub environment variables, create `cd-test.yml`.
- [ ] T094 [US5] Repeat the above three (T090–T093) for `iac/environments/prod/` — the pattern is identical; the existence of two `.example` siblings is what makes the pattern unambiguous to a new engineer.
- [ ] T095 [P] [US5] Add the second-environment recipe to `docs/deploying-environments.md` § "Adding a new environment" — pulls from `iac/environments/test/README.md` so they don't drift.
- [ ] T096 [US5] Perform a dry-run of adding `test`: follow the documented procedure to the point of `tofu plan` (do not apply). Verify the plan succeeds and produces a sensible set of new resources. Capture any documentation gaps and fix them. Do NOT apply (SC-006 measures the documentation, not actual provisioning of test).

**Checkpoint**: The multi-environment pattern is proven via dry-run; future environments are a documentation exercise.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Anything that didn't fit in a single user story; final verification.

- [ ] T097 [P] Write `docs/architecture.md` — high-level diagram (text + ASCII or `mermaid`) of the deployed `dev` environment showing browser → frontend → backend → Entra ID / Key Vault / Application Insights flows, plus the Container Apps Environment boundary and the future-state placement of Functions on Container Apps.
- [ ] T098 [P] Update `speckit-artifacts/tech-stack.md` to add any durable platform-foundation rules discovered during this slice (e.g., "Container App ingress posture is configurable per workload via `ingress_external` parameter; default `false`; flipping to `true` requires a documented justification") — only if any new durable rule emerged.
- [ ] T099 [P] Update the root `README.md` with a "Quick links" section pointing to `docs/local-development.md`, `docs/deploying-environments.md`, `docs/observability.md`, `docs/identity-and-secrets.md`, `docs/architecture.md`, and the spec/plan/tasks for this slice.
- [ ] T100 [P] Add `CHANGELOG.md` (or extend if exists) with a `## 002-solution-foundation` heading summarizing the slice's deliverables in human-readable terms for project newcomers.
- [ ] T101 Run `scripts/test-all.ps1` (or `.sh`) against `main` and confirm a clean pass.
- [ ] T102 Run the complete `quickstart.md` end-to-end one final time against a clean working tree and a clean Azure subscription; resolve any gaps before declaring the slice done.
- [ ] T103 [P] Capture screenshots for the docs: platform-status page (signed in), App Insights Transaction Search showing a correlated request, the Container Apps revision health view. Save under `docs/images/`.
- [ ] T104 Tag a `v0.2.0-foundation` release once all acceptance criteria in spec.md § Acceptance Criteria are independently verified.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately.
- **Foundational (Phase 2)**: Depends on Setup completion — **BLOCKS all user stories**.
- **User Stories (Phase 3+)**:
  - US1 and US2 (both P1) depend only on Foundational. US2 can technically start after the IaC modules in T035–T040 are done; US1 can start after the backend foundation in T011–T020.
  - US3 (P2) depends on Foundational; benefits from US2 being far enough along that a deployed environment exists to inspect.
  - US4 (P2) depends on Foundational; benefits from US2 to verify deployed-side claims.
  - US5 (P3) depends on the `iac/environments/dev/` composition from US2 (T061–T065) as the reference to copy.
- **Polish (Phase 8)**: Depends on all desired user stories being complete.

### User Story Dependencies (within Phases 3–7)

- **US1**: Independent — needs Foundational only.
- **US2**: Independent — needs Foundational only; reuses US1's `/whoami` endpoint and platform-status page (same code, just deployed).
- **US3**: Depends on Foundational. Acceptance test improves when US2 has shipped a real deployed environment to inspect.
- **US4**: Depends on Foundational. Acceptance test improves when US2 has shipped a real deployed environment to audit.
- **US5**: Depends on Phase 4 (US2) tasks T061–T065 specifically, since those are the modules to clone.

### Within Each User Story

- Models / configuration before services.
- Services before endpoints / pages.
- Endpoints / pages before tests-that-call-them.
- Manual validation step last (e.g., T060, T075, T081, T089, T096).

### Parallel Opportunities

- All Setup tasks marked `[P]` (T002–T009) run in parallel after T001.
- All Foundational `[P]` tasks within the same subsection (backend, frontend, IaC) run in parallel; between-subsection independence is even better.
- Once Foundational completes, all US1 + US2 work can run in parallel (different developers).
- US3 / US4 tasks marked `[P]` run in parallel within their respective phases.

---

## Parallel Example: Phase 2 Foundational

```bash
# Backend foundation (all can start once T011 packages are added):
Task: T012 (auth wiring) in api/BusTerminal.Api/Infrastructure/Authentication/AuthenticationExtensions.cs
Task: T013 (telemetry wiring) in api/BusTerminal.Api/Infrastructure/Observability/OpenTelemetryExtensions.cs
Task: T014 (Key Vault wiring) in api/BusTerminal.Api/Infrastructure/Configuration/KeyVaultExtensions.cs
Task: T015 (health endpoints) in api/BusTerminal.Api/Features/Health/HealthEndpoints.cs

# Frontend foundation (all can start once T021 packages are added):
Task: T022 (auth config) in web/lib/auth.ts
Task: T025 (trace context) in web/lib/telemetry/trace-context.ts
Task: T026 (AI adapter) in web/lib/telemetry/ai-adapter.ts
Task: T028 (api-client) in web/lib/api-client.ts

# IaC modules (all independent, can start immediately after T030):
Task: T035 (identity module) in iac/modules/identity/
Task: T036 (monitoring module) in iac/modules/monitoring/
Task: T037 (keyvault module) in iac/modules/keyvault/
Task: T038 (container-registry module) in iac/modules/container-registry/
Task: T039 (container-apps-env module) in iac/modules/container-apps-env/
Task: T040 (container-app module) in iac/modules/container-app/
```

---

## Implementation Strategy

### MVP First (User Story 1 only)

1. Complete **Phase 1: Setup** (T001–T010).
2. Complete **Phase 2: Foundational** (T011–T040) — this is the largest single batch; cannot be skipped.
3. Complete **Phase 3: US1** (T041–T060) — frontend nav shell + `/whoami` endpoint + local dev scripts.
4. **STOP and VALIDATE**: A new developer can clone, bootstrap, and run the local stack to a working platform-status page. Onboarding time ≤ 30 min (SC-001).
5. Demo this slice as the MVP foundation; ship.

### Incremental Delivery (recommended)

1. **Setup + Foundational** → foundation ready (no user-visible artifact).
2. **+ US1** → working local stack with end-to-end `/whoami` flow → demo (MVP!).
3. **+ US2** → automated cloud deploy → demo of a real deployed environment.
4. **+ US3** → operators have a correlated-telemetry story → demo telemetry queries.
5. **+ US4** → security audit can be re-run on demand → demo gitleaks/Trivy reports.
6. **+ US5** → multi-environment pattern proven via dry-run → demo the docs and `tofu plan` output.
7. **Polish** → docs + screenshots + release tag.

Each increment is independently testable and adds platform value. Stopping at any of the seven increments leaves the slice in a coherent, demonstrable state.

### Parallel Team Strategy

After the Foundational phase, with three developers:

- **Developer A**: US1 (local-dev focus) → owns the developer experience.
- **Developer B**: US2 (cloud-deploy focus) → owns the pipeline and IaC composition.
- **Developer C**: US3 + US4 (operability + security focus) → owns observability dashboards and the security workflow.

US5 is small enough to be picked up by whoever finishes their slice first.

---

## Notes

- `[P]` tasks edit different files and have no dependencies on other incomplete tasks in their phase.
- `[Story]` labels enable traceability — if a user story is descoped, its `[USx]`-tagged tasks come with it as a single unit.
- Tests are not TDD-first in this slice (the foundation already has confidence from spec + clarification rounds); they are written alongside implementation, before merging the related PR.
- Each task is sized to be completable in a single short session and ends with a verifiable artifact (a file, a passing test, a documented benchmark).
- Avoid: vague tasks, same-file conflicts across `[P]`-tagged tasks, cross-story dependencies that break independent testability.
- Commit after each task or logical group (the auto-commit hook will prompt).
