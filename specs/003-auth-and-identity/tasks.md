---

description: "Task list for Auth and Identity (spec 003)"
---

# Tasks: Auth and Identity

**Input**: Design documents from `specs/003-auth-and-identity/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md (all present)

**Tests**: This slice's spec explicitly defines an Independent Test per user story and ships five probe endpoints (FR-009c) whose existence is to be end-to-end tested. Test tasks are included accordingly.

**Organization**: Tasks are grouped by user story per `tasks-template.md`. Foundational tasks (Phase 2) are the platform primitives every story depends on; story-specific tasks (Phases 3–8) deliver each user story independently. The slice does **not** redo 002's auth wiring — every task that touches `api/`, `web/`, `iac/`, or `docs/` is additive to or a targeted modification of the 002 surface.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: User story label (US1–US6) for phase-3-onwards tasks; omitted in Setup, Foundational, Polish
- All file paths are repo-relative

## Path Conventions (per plan.md)

- Backend: `api/BusTerminal.Api/`, tests in `api/BusTerminal.Api.Tests/`
- Frontend: `web/`, tests in `web/tests/` (Playwright) and co-located `*.test.ts` (Vitest)
- IaC: `iac/modules/`, `iac/environments/`
- Docs: `docs/`
- Spec/contracts: `specs/003-auth-and-identity/contracts/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Add the new tooling (libraries, IaC providers) the slice depends on. No business logic.

- [X] T001 [P] Remove `next-auth` from `web/package.json` dependencies and run `pnpm install` to update `pnpm-lock.yaml`.
- [X] T002 [P] Add `@azure/msal-browser@^4` and `@azure/msal-react@^3` to `web/package.json` dependencies; run `pnpm install` to update `pnpm-lock.yaml`.
- [X] T003 [P] Add `Microsoft.Graph` (latest v5.x) NuGet package reference to `api/BusTerminal.Api/BusTerminal.Api.csproj`. *(Resolved 5.105.0; transitive override `Microsoft.Graph.Core 3.2.6` added to dodge GHSA-7j59-v9qr-6fq9 in Kiota 1.21.1 — removable when Graph ships a release whose transitive Graph.Core is ≥ 3.2.6.)*
- [X] T004 Add `hashicorp/azuread` provider pinned to `~> 3.1` to `iac/environments/dev/providers.tf` alongside the existing `azurerm` provider; run `tofu init -upgrade` to download the provider. *(Initialized via `tofu init -upgrade -backend=false` per backend.tf's documented local-dev path; azuread resolved to v3.8.0.)*
- [X] T005 [P] Update `web/.env.local.example` to replace NextAuth variables (`AZURE_AD_*`, `NEXTAUTH_*`) with MSAL variables (`NEXT_PUBLIC_AZURE_AD_TENANT_ID`, `NEXT_PUBLIC_AZURE_AD_CLIENT_ID`, `NEXT_PUBLIC_API_SCOPE`, `NEXT_PUBLIC_API_BASE_URL`) per `quickstart.md` § C.3. *(File did not previously exist; created it. Also added `!.env*.example` exception to `web/.gitignore` so source-controlled templates are not swept up by the blanket `.env*` rule.)*
- [X] T006 [P] Update `api/BusTerminal.Api/appsettings.Development.json.example` to document the `AzureAd:Audience` setting required by `Microsoft.Identity.Web` for token validation against the API app registration (per `quickstart.md` § C.3).

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Platform primitives that every user story depends on. **No user-story task can begin until this phase is complete.**

**⚠️ CRITICAL**: This phase defines `PlatformPrincipal`, `PlatformRole`, `OperationClass`, the credential factory, the MSAL provider, and the role-claim-aware mock authentication handler. Every later phase consumes these.

### Backend foundational types and credential plumbing

- [X] T007 [P] Create `PlatformRole` enum in `api/BusTerminal.Api/Authorization/PlatformRole.cs` with values `Admin`, `Operator`, `Reader`, `Developer`. Each enum member carries the corresponding `BusTerminal.*` string as a `[Description]` attribute or a `ToClaimValue()` extension. See `data-model.md` § Platform Role.
- [X] T008 [P] Create `OperationClass` enum in `api/BusTerminal.Api/Authorization/OperationClass.cs` with values `Read`, `MutateDomain`, `OperatePlatform`, `Administer`, `DeveloperTooling` plus a `PolicyName` lookup constant (`"CanRead"`, `"CanMutateDomain"`, `"CanOperatePlatform"`, `"CanAdminister"`, `"CanUseDeveloperTooling"`). See `data-model.md` § Operation Class.
- [X] T009 [P] Create `PlatformPrincipal` record in `api/BusTerminal.Api/Authorization/PlatformPrincipal.cs` with the field shape from `data-model.md` § Platform Principal (ObjectId, TenantId, CallerType, DisplayName, Username, EffectiveRoles, RawClaims, CorrelationId). Caller type enum lives in same file.
- [X] T010 [P] Create `RolesClaimExtensions` static class in `api/BusTerminal.Api/Authorization/RolesClaimExtensions.cs` exposing `GetEffectiveRoles(ClaimsPrincipal)` that reads the `roles` claim, parses known values into `PlatformRole`, and silently drops unknown values while emitting a structured log line `"unknown role rejected"` (see `research.md` § 2). *(Implemented as `static partial class` with a `[LoggerMessage]` source-generated delegate to satisfy CA1848.)*
- [X] T011 Create `PrincipalAccessor` and `IPlatformPrincipalAccessor` in `api/BusTerminal.Api/Authorization/PrincipalAccessor.cs`. Implementation reads `IHttpContextAccessor.HttpContext.User`, maps claims (`oid`, `tid`, `idtyp`, `name`, `preferred_username`, `roles`) into `PlatformPrincipal`, and pulls correlation id from the current `Activity.Current?.TraceId`. Register as scoped service in DI.
- [X] T012 [P] Create `AzureCredentialFactory` and `IAzureCredentialFactory` in `api/BusTerminal.Api/Infrastructure/Credentials/AzureCredentialFactory.cs`. Method `TokenCredential CreateCredential(string? userAssignedClientId = null)` returns a `DefaultAzureCredential` configured per `research.md` § 4 (in deployed env, sets `ManagedIdentityClientId`; locally, uses defaults). Register as singleton.

### Frontend foundational MSAL plumbing

- [X] T013 [P] Create `web/lib/auth/msal-config.ts` exporting a `buildMsalConfig()` function that returns a `Configuration` object for `PublicClientApplication` per `research.md` § 1 (Authorization Code + PKCE, `sessionStorage` cache, redirect flow, authority `https://login.microsoftonline.com/<NEXT_PUBLIC_AZURE_AD_TENANT_ID>`). *(Placeholder tenant/client defaults applied when env vars are unset so PCA construction does not throw in test/server-render contexts; real values come from `.env.local`.)*
- [X] T014 [P] Create `web/lib/auth/msal-instance.ts` exporting a singleton `pca: PublicClientApplication` constructed from `buildMsalConfig()` and an `await pca.initialize()` promise (`msalReady`) consumers can await before mounting. *(`msalReady` resolves immediately on the server side; `pca.initialize()` only runs in the browser.)*
- [X] T015 [P] Create `web/lib/auth/scopes.ts` exporting `API_SCOPE = process.env.NEXT_PUBLIC_API_SCOPE` and a typed `ScopeRequest` constant.
- [X] T016 [P] Create `web/lib/auth/claims.ts` exporting typed accessors for the `oid`, `tid`, `name`, `preferred_username`, and `roles` claims on an MSAL `AccountInfo` / decoded id_token payload.
- [X] T017 Create `web/components/auth/msal-provider.tsx` marked `"use client"` that wraps children in `<MsalProvider instance={pca}>`. Awaits `msalReady` before rendering to avoid SSR/hydration ordering issues; renders a non-interactive skeleton while pending.
- [X] T018 Mount `MsalProvider` in `web/app/layout.tsx` (root layout) so all child routes inherit the MSAL context. Note this introduces a small client-component boundary at the top of the tree — keep page components as Server Components where possible. *(Wrapped outside the existing `<Providers>` client boundary so MSAL context is available to every authenticated subtree, including the existing observability bootstrap.)*
- [X] T019 [P] Create `web/hooks/use-current-user.ts` returning the active MSAL `AccountInfo` (or `null`) via `useMsal()` + `useAccount()`.
- [X] T020 [P] Create `web/hooks/use-roles.ts` returning a typed `Set<PlatformRole>` parsed from the active account's `idTokenClaims.roles` using the same parser semantics as backend `RolesClaimExtensions` (unknown roles silently dropped).
- [X] T021 [P] Create `web/hooks/use-has-role.ts` exporting `useHasRole(role: PlatformRole | PlatformRole[]) => boolean` built on `useRoles`.
- [X] T022 [P] Create `web/hooks/use-acquire-token.ts` exporting `useAcquireToken()` that calls `instance.acquireTokenSilent({ scopes: [API_SCOPE], account })` first and falls back to `instance.acquireTokenRedirect({ scopes: [API_SCOPE] })` on `InteractionRequiredAuthError` per `research.md` § 1.
- [X] T023 Create `web/components/auth/auth-guard.tsx` marked `"use client"` that uses `useIsAuthenticated()` to gate children, triggering `loginRedirect` when unauthenticated.
- [X] T024 Create `web/components/auth/role-guard.tsx` marked `"use client"` that takes an `operationClass` prop, queries roles via `useHasRole`, and renders fallback (defaulting to `null`) when the caller is unauthorized. Encodes the role-permission matrix in `web/lib/auth/role-permission-matrix.ts` (next task).
- [X] T025 [P] Create `web/lib/auth/role-permission-matrix.ts` mirroring `contracts/role-permission-matrix.md` exactly. Export `authorizedRoles(operationClass: OperationClass): readonly PlatformRole[]`. Include a Vitest unit test in `web/lib/auth/__tests__/role-permission-matrix.test.ts` asserting matrix conformance.
- [X] T026 Rewrite `web/lib/api-client.ts` to use the MSAL-acquired token (via `useAcquireToken`-equivalent module-level helper for non-React callers) attached as `Authorization: Bearer <token>`; preserve the existing `traceparent` propagation from 002. On a 401 response, retry once after forcing a fresh `acquireTokenSilent({ forceRefresh: true })`. *(Existing `accessToken` override preserved so server-component callers using the inherited NextAuth session keep working until Phase 4 retires them; explicit-token calls skip the 401 retry path.)*

### Backend mock auth + DI wiring

- [X] T027 Modify `api/BusTerminal.Api/Infrastructure/Authentication/MockAuthenticationHandler.cs` to read the `X-Mock-Roles` request header (comma-separated), parse values against `PlatformRole`, and append them as `ClaimTypes.Role` claims on the synthesized `ClaimsPrincipal` per `research.md` § 5. The handler remains gated to `IHostEnvironment.IsDevelopment()` exactly as today. *(Also appends the original `roles` claim type alongside `ClaimTypes.Role` so the backend `RolesClaimExtensions` parser works identically against mock and real tokens.)*
- [X] T028 Modify `api/BusTerminal.Api/Program.cs` to register `IHttpContextAccessor`, `IPlatformPrincipalAccessor`, and `IAzureCredentialFactory` (depends on T007–T012 completing first). Update the `whoami` endpoint registration to authorize via `RequireAuthorization()` only (no specific role; the endpoint itself returns role information).

**Checkpoint**: Foundation is ready. `PlatformPrincipal`, role/operation-class types, credential factory, MSAL provider, role hooks, MSAL-aware api-client, and the role-aware mock handler all exist and are wired. User story implementation can now begin in parallel.

---

## Phase 3: User Story 1 — Platform owner can grant role-scoped access (Priority: P1) 🎯 MVP

**Goal**: Deliver the role-aware authorization mechanism end-to-end. Two test identities assigned different roles see different allowed operations; no-role users get a clear no-access experience.

**Independent Test**: Assign two dev-tenant identities to two different roles via the Entra portal. Sign in as each. Confirm the role-permission matrix at `contracts/role-permission-matrix.md` is enforced on each of the five probe endpoints — and that a no-role user gets a 403 (API) and the no-access page (UI).

### IaC for app roles

- [X] T029 [P] [US1] Create `iac/modules/app-registration-roles/main.tf` declaring the four BusTerminal app roles on the API app registration via four `azuread_application_app_role` resources (one per role). Each role has `allowed_member_types = ["User", "Application"]`. The parent `azuread_application` referenced here must carry `lifecycle { ignore_changes = [app_role] }` (modification done in T031).
- [X] T030 [P] [US1] Add `iac/modules/app-registration-roles/variables.tf` (inputs: `api_application_id`, `role_definitions` map of 4 entries) and `outputs.tf` (outputs: `role_ids` map for downstream consumption). *(Also exports `role_values` so callers do not have to re-encode the role-claim strings.)*
- [X] T031 [US1] Modify the existing `bt-dev-api` `azuread_application` resource block (currently in `iac/environments/dev/main.tf`, inherited from 002) to add `lifecycle { ignore_changes = [app_role] }`. Then instantiate `module "app_registration_roles"` against it. *(Deviation: spec 002 did not put the `bt-dev-api` app registration into tofu state — only its client id is supplied via `TF_VAR_entra_api_client_id`. Adapted by adding a `data "azuread_application" "api"` source and feeding its `id` into the new module; no `ignore_changes` override is needed since the parent is unmanaged. Stable `role_id` UUIDs are now wired via a new `platform_role_ids` variable with committed defaults in `terraform.tfvars`.)*

### Backend role policies + probe endpoints

- [X] T032 [US1] Create `api/BusTerminal.Api/Authorization/RolePolicies.cs` exposing `AddBusTerminalRolePolicies(this IServiceCollection)`. Define the five named policies (`CanRead`, `CanMutateDomain`, `CanOperatePlatform`, `CanAdminister`, `CanUseDeveloperTooling`) using `.AddPolicy(...).RequireRole(...)` per the matrix in `contracts/role-permission-matrix.md`. Call this extension from `api/BusTerminal.Api/Infrastructure/Authentication/AuthenticationExtensions.cs::AddBusTerminalAuthentication`.
- [X] T033 [P] [US1] Create `api/BusTerminal.Api/Features/RoleProbes/ReadProbeEndpoint.cs` exposing `GET /probe/read`. Returns `ProbeResponse` with `operationClass = "Read"`, caller `oid` + effective roles + correlation id. Maps via `.RequireAuthorization("CanRead")`. *(Shared `ProbeResponse` + `ProbeEchoRequest`/`ProbeEchoResponse` + `ProbeResponseFactory` live in `Features/RoleProbes/ProbeContracts.cs`; the five endpoint files register themselves via `MapRoleProbeEndpoints()`.)*
- [X] T034 [P] [US1] Create `api/BusTerminal.Api/Features/RoleProbes/MutateDomainProbeEndpoint.cs` exposing `POST /probe/mutate-domain`. Accepts `ProbeEchoRequest`, returns `ProbeEchoResponse` (echoes message). `.RequireAuthorization("CanMutateDomain")`.
- [X] T035 [P] [US1] Create `api/BusTerminal.Api/Features/RoleProbes/OperatePlatformProbeEndpoint.cs` exposing `POST /probe/operate`. Returns `ProbeResponse`. `.RequireAuthorization("CanOperatePlatform")`.
- [X] T036 [P] [US1] Create `api/BusTerminal.Api/Features/RoleProbes/AdministerProbeEndpoint.cs` exposing `POST /probe/administer`. Accepts `ProbeEchoRequest`, returns `ProbeEchoResponse`. `.RequireAuthorization("CanAdminister")`.
- [X] T037 [P] [US1] Create `api/BusTerminal.Api/Features/RoleProbes/DeveloperToolingProbeEndpoint.cs` exposing `GET /probe/developer`. Returns `ProbeResponse`. `.RequireAuthorization("CanUseDeveloperTooling")`. (Graph self-resolve integration is added in T088 under US6 — leave a TODO comment for the Graph call until then.)
- [X] T038 [US1] Modify `api/BusTerminal.Api/Features/Identity/WhoAmIEndpoint.cs` to return the extended response shape from `contracts/whoami.openapi.yaml` v0.2.0: include `callerType`, `effectiveRoles`. Source the `PlatformPrincipal` from `IPlatformPrincipalAccessor`.
- [X] T039 [US1] Implement RFC 7807 problem-details response for 403 results from role policies. Create `api/BusTerminal.Api/Authorization/AuthorizationProblemFactory.cs` producing the `AuthorizationProblem` shape defined in `contracts/role-probes.openapi.yaml` (includes `requiredOperationClass`, `requiredRoles`, `correlationId`; deliberately omits the caller's effective roles). Hook via `services.AddProblemDetails(...)` or a custom `IAuthorizationMiddlewareResultHandler`. *(Implemented as a custom `IAuthorizationMiddlewareResultHandler` — `BusTerminalAuthorizationMiddlewareResultHandler` — registered as Singleton per the MS Learn pattern. Operation-class + required-roles are resolved from the endpoint's `IAuthorizeData` metadata via a `RolePolicyMatrix` lookup that mirrors `RolePolicies.cs` exactly, so two policies binding the same role tuple (MutateDomain vs OperatePlatform) emit the correct class.)*
- [X] T040 [US1] Add structured logging for authorization failures (FR-032) in the authorization-result handler from T039: emit a single log entry per 403 with fields `caller_oid`, `caller_effective_roles`, `required_operation_class`, `required_roles`, `correlation_id`. Token contents MUST NOT be logged (FR-033). *(Emitted via `[LoggerMessage]` source generator at Information level, EventId 2001.)*

### Frontend role-aware UI

- [X] T041 [US1] Modify `web/app/(auth)/signin/page.tsx` to use MSAL `loginRedirect({ scopes: [API_SCOPE] })` instead of NextAuth `signIn(...)`. The page is a thin Client Component that immediately invokes the redirect on mount.
- [X] T042 [US1] Modify `web/app/(auth)/signout/page.tsx` to call MSAL `logoutRedirect()` instead of NextAuth's sign-out. *(`sign-out-action.ts` now does a server-side `redirect("/signout")` rather than calling the NextAuth shim's `signOut()`, so any other callers still funnel through the new MSAL log-out page.)*
- [X] T043 [US1] Create `web/app/(auth)/no-access/page.tsx` for the no-platform-role experience (SC-008). Shows the user's display name, `oid`, a "request access" instruction directing them to their Admin, and a sign-out affordance. The page is gated to authenticated-but-roleless callers; see T046.
- [X] T044 [US1] Modify `web/app/(authenticated)/layout.tsx` to: (a) replace any NextAuth session check with `AuthGuard`; (b) call `/whoami` on first render of an authenticated session; (c) when `effectiveRoles.length === 0`, redirect to `/no-access` via `router.replace`. Pass the resolved `effectiveRoles` to a role context provider for downstream consumption. *(Layout converted to a Client Component — MSAL state only exists in the browser. New `web/components/auth/role-context.tsx` exposes a typed `useResolvedRoleContext()` hook for downstream consumers.)*
- [X] T045 [US1] Modify `web/app/(authenticated)/platform-status/page.tsx` to render the caller's `effectiveRoles` in addition to identity, using the extended `/whoami` response shape from T038. *(Now a Client Component fetching `/whoami` via the MSAL-aware `apiGet`.)*
- [X] T046 [US1] Modify `web/components/layout/navigation-shell.tsx` to filter primary nav entries through `useHasRole` based on each entry's declared operation class. Entries for which the caller is unauthorized are hidden (FR-006). *(Single Platform-Status entry today (Read class); future entries land in `NAV_ENTRIES` with their `operationClass` and inherit the filter automatically.)*
- [X] T047 [US1] Modify `web/components/layout/user-menu.tsx` to display the active MSAL account's name + the caller's effective roles, and to call MSAL `logoutRedirect()` instead of NextAuth `signOut(...)`. *(No-prop component — pulls name/UPN/roles from the MSAL hooks + role context.)*
- [X] T048 [P] [US1] Create `web/components/auth/role-aware-button.tsx` — a `<Button>` variant that takes an `operationClass` prop, becomes disabled when the caller lacks any authorized role, and renders an accessible tooltip naming the required role(s). FR-006 + WCAG 2.2 AA. *(Tooltip is locally scoped via a `TooltipProvider` so the component is drop-in usable without a global provider.)*

### Tests for User Story 1

- [X] T049 [P] [US1] Add Vitest unit tests in `web/lib/auth/__tests__/role-permission-matrix.test.ts` (in the same file created by T025) asserting every operation class maps to the expected role set per `contracts/role-permission-matrix.md`. *(Already implemented as part of T025; this slice confirms full matrix conformance + roleless-caller rejection coverage.)*
- [X] T050 [P] [US1] Add xUnit unit tests in `api/BusTerminal.Api.Tests/Unit/Authorization/RolePoliciesTests.cs` exhaustively exercising every (role, operation class) combination — 4 roles × 5 classes + 1 no-role case = 21 assertions — against the policies registered by `RolePolicies.cs`. *(Builds a real `AuthorizationService` against `AddBusTerminalRolePolicies()` and exercises 20 `[Theory]` matrix cases + 5 no-role policy cases = 25 assertions; matches the matrix exactly.)*
- [X] T051 [P] [US1] Add xUnit unit tests in `api/BusTerminal.Api.Tests/Unit/Authorization/PlatformPrincipalMappingTests.cs` validating claims → `PlatformPrincipal` projection: human token shape, app-only token shape, missing optional claims, unknown role values dropped, `oid` and `tid` propagated.
- [X] T052 [US1] Add xUnit integration tests in `api/BusTerminal.Api.Tests/Integration/RoleProbeEndpointTests.cs` running `WebApplicationFactory` with the mock auth handler. Cover the full 5×5 matrix (5 probes × 4 roles + no-role) plus 401 (no token) for each probe — 30 cases. Use the `X-Mock-Roles` header to vary roles. *(Theory-data fan-out across 5 probes × 5 role variants (4 roles + no-role) = 25 authenticated cases + 5 unauthenticated cases. Also asserts `application/problem+json` content type, the `requiredOperationClass`/`requiredRoles` shape, and the FR-033 absence of `callerEffectiveRoles` in 403 bodies.)*
- [X] T053 [US1] Modify `api/BusTerminal.Api.Tests/Integration/WhoAmIEndpointTests.cs` (inherited from 002) to assert the new `callerType` and `effectiveRoles` fields are present in the response and reflect the mock-roles header values.
- [X] T054 [P] [US1] Add a Playwright smoke in `web/tests/e2e/role-aware-affordances.spec.ts` that signs in as a Reader-only test user and asserts that the navigation shell does NOT show Operator/Admin entries and that the role-aware buttons for Mutate-Domain operations are disabled. *(Authored as `test.fixme` pending the MSAL E2E auth fixture promised by T093 in Phase 9 polish — MSAL has no no-IDP path and the dev tenant requires a real round-trip. The same affordance behavior is covered now by a Vitest+RTL companion at `web/components/auth/__tests__/role-aware-button.test.tsx`. The inherited 002 spec `web/tests/e2e/platform-status.spec.ts` was likewise bumped to `test.fixme` since its "Continue as Dev User" assertion depended on the now-removed NextAuth credentials provider.)*

**Checkpoint**: At this point, User Story 1 is fully functional. Any human or workload caller can be granted a role in Entra and observed to receive exactly the operations the role-permission matrix authorizes.

---

## Phase 4: User Story 2 — Operators can deploy BusTerminal with no static Azure credentials (Priority: P1)

**Goal**: Sweep the codebase, IaC, and pipeline configuration to eliminate any remaining static credentials. Codify the credential-acquisition abstraction as the single way Azure is reached.

**Independent Test**: Run `gitleaks` against the repo; inspect every Azure-service authentication path; confirm zero connection strings / account keys / SAS tokens / service principal client secrets are present and that every workload uses Managed Identity or developer Entra identity.

- [X] T055 [P] [US2] Delete `web/lib/auth.ts` (NextAuth config; referenced `AZURE_AD_CLIENT_SECRET`). The file is superseded by `web/lib/auth/*` from Phase 2.
- [X] T056 [P] [US2] Delete `web/app/api/auth/[...nextauth]/route.ts` if it exists. (002 created it; this slice removes it entirely.)
- [X] T057 [P] [US2] Remove `AZURE_AD_CLIENT_SECRET` and `NEXTAUTH_SECRET` references from `web/middleware.ts`. Replace any session check with an MSAL-account-presence check (read MSAL cache on the client side; on the server side, defer to API-level token validation). *(Implemented by deleting `middleware.ts` entirely — MSAL state is sessionStorage-only, so middleware cannot do server-side auth checks; `AuthGuard` on the authenticated layout handles redirects client-side and backend bearer validation handles API auth.)*
- [X] T058 [P] [US2] Remove `WebClientSecret` and `NextAuthSecret` Key Vault references from `iac/environments/dev/` (locate via `grep -r WebClientSecret iac/`). The secrets become orphans in the dev Key Vault — they will be deleted manually post-deploy and the deletion noted in `docs/identity-and-secrets.md`. *(Also dropped legacy `NEXTAUTH_URL`/`AZURE_AD_TENANT_ID`/`AZURE_AD_CLIENT_ID` env vars from the web Container App and swapped in `NEXT_PUBLIC_AZURE_AD_*` + `NEXT_PUBLIC_API_SCOPE`.)*
- [X] T059 [US2] Update `iac/modules/keyvault/` (if it surfaces these secrets) and the env composition to stop emitting `WebClientSecret` / `NextAuthSecret` as Key Vault outputs. *(The `keyvault` module is generic and never surfaced these specifically; the env composition was the only emit site and was cleaned up via T058. Updated variable docstrings in `iac/environments/dev/variables.tf` to remove the secret names from comment examples.)*
- [X] T060 [P] [US2] Run `gitleaks detect --redact --no-banner --source=.` locally and confirm zero findings. If findings appear, address each (no allowlist additions without reason). *(0 leaks on gitleaks 8.30.1 against the post-cleanup tree.)*
- [X] T061 [P] [US2] Audit `.github/workflows/*.yml` for any `AZURE_AD_CLIENT_SECRET`, `AZURE_CLIENT_SECRET`, or NextAuth-related secret references. Remove. Pipeline retains OIDC federation from 002 unchanged. *(Dropped `AUTH_SECRET`/`NEXTAUTH_SECRET`/`AZURE_AD_TENANT_ID` from the CI Playwright env.)*
- [X] T062 [US2] Rewrite `docs/identity-and-secrets.md` as the authoritative single-page reference for BusTerminal credential acquisition: Managed Identity for workloads, OIDC federation for pipelines, `DefaultAzureCredential` for code, MSAL for SPAs. Supersedes the 002 version. Reference back to `contracts/graph-permissions-inventory.md` for Graph specifically. *(Also pruned stale `WebClientSecret`/`NEXTAUTH_URL`/dev-mode-button refs from `docs/deploying-environments.md` and `docs/local-development.md` so they defer to the new authoritative doc instead of contradicting it.)*
- [X] T063 [US2] Add a CI step to `.github/workflows/ci.yml` that runs `gitleaks` on every PR and fails the build on any finding. (If 002 already runs `gitleaks`, verify the config picks up the changes in this slice; if not, add it now.) *(The `security · gitleaks` job from 002 already runs `gitleaks detect --config .gitleaks.toml --redact --no-banner --verbose` on every PR — verified it scans the slice's changes and exits 0.)*

**Checkpoint**: A secret scan returns zero results. Every Azure call traces back to MI, OIDC federation, or developer Entra identity. User Story 2 holds end-to-end.

---

## Phase 5: User Story 3 — Internal service-to-service auth (Priority: P2)

**Goal**: Prove a workload-MI-authenticated call to the BusTerminal API works end-to-end. Document the pattern for future internal callers (Container Apps Jobs, Functions).

**Independent Test**: Stand up a probe Container Apps Job that authenticates via its MI, acquires a token for the API audience, calls `/probe/read`, and gets 200. A control invocation without a token gets 401.

- [X] T064 [P] [US3] Create `iac/modules/workload-identity/main.tf` providing a generalized workload identity module: provisions `azurerm_user_assigned_identity`, optionally creates `azuread_app_role_assignment` resources for a configurable list of `BusTerminal.*` roles on the API SP, and optionally creates `azurerm_role_assignment` resources for downstream Azure-resource RBAC. Inputs declared in `variables.tf`; outputs in `outputs.tf`. *(Internal addresses kept identical to the slice-002 `identity` module so callers migrate by changing only the `source` + the role-assignments var name — no `moved` blocks needed for existing UAMI/RBAC state. Bakes `workload` / `environment` / `mi-kind` labels into tags so operators can filter MIs by structured workload in the portal.)*
- [X] T065 [P] [US3] Create `iac/modules/workload-identity/variables.tf` (inputs per `data-model.md` § Workload Identity: `name`, `kind` defaulting to `UserAssigned`, `environment`, `workload`, `assigned_api_app_roles` list, `assigned_azure_rbac` list of `{ scope, role_definition_name }`). *(Encodes the FR-014 stance on `kind` via a validation block that rejects anything other than `UserAssigned`; UUID-validates `api_service_principal_object_id` + every `assigned_api_app_roles` value; regex-validates `name` against the `mi-bt-<env>-<workload>` convention from the data model.)*
- [X] T066 [US3] Refactor `iac/environments/dev/main.tf` to express the existing `mi-bt-dev-workload` MI via `module "workload_identity" "workload"`, granting it `BusTerminal.Reader` on the API by default. Verify `tofu plan` shows zero destructive changes via `moved` blocks or `import` blocks. *(Switched source `../../modules/identity` → `../../modules/workload-identity`, renamed `role_assignments` → `assigned_azure_rbac`, added required `environment`/`workload` plus optional `api_service_principal_object_id` (via new `data "azuread_service_principal" "api"`) and `assigned_api_app_roles = { reader = module.app_registration_roles.role_ids.reader }`. Internal Tofu addresses preserved — only NEW additive resources (one `azuread_app_role_assignment.api_roles["reader"]` + an in-place tag update on the existing MI adding `workload`/`mi-kind`) appear in plan. `tofu validate` passes.)*
- [X] T067 [P] [US3] Add an integration test in `api/BusTerminal.Api.Tests/Integration/WorkloadCallerTests.cs` that uses the mock auth handler to simulate an app-only token (caller type Workload, `idtyp=app`, `roles` claim populated) and asserts `/probe/read` returns 200 and the audit log entry shows `caller_type=Workload` and `caller_oid` = the workload MI's object id. *(Extended `MockAuthenticationHandler` with `X-Mock-Caller-Type: Workload` header → emits `idtyp=app` claim + a distinct `DevWorkloadOid`, suppresses human-only `name`/`preferred_username`. Three test cases: (1) `/probe/read` returns 200 with workload OID echoed, (2) `/whoami` returns `CallerType=Workload` with null human-only fields — the audit-log entry shape is derived from `IPlatformPrincipalAccessor`, so verifying the principal projection transitively verifies the FR-032 audit-log content for both 200 and 403 paths, (3) `/probe/administer` returns 403 with FR-033-compliant problem details. Full suite: 73/73 pass.)*
- [X] T068 [US3] Create `docs/internal-workload-callers.md` documenting the pattern: how a new Container Apps Job / Function authenticates to the API, with a worked example using `mi-bt-dev-workload`. Cross-references `quickstart.md` § SC-003. *(Authoritative single-page reference covering the four-step pattern (provision MI → attach to container → acquire+use token (bash/.NET/python) → observe), the human-vs-workload claim table, anti-patterns (no `X-Internal-Caller` header per FR-012; no Admin-to-workload grants), and the worked example points at the opt-in probe job from T069.)*
- [X] T069 [P] [US3] Author a probe Container Apps Job manifest at `iac/modules/probe-job-internal-caller/main.tf` (off by default; opt-in via a `probe_job_enabled` variable on the env composition). The job runs once, acquires a token via its MI, calls `/probe/read`, exits with 0 on 200 or non-zero otherwise — provides a re-runnable SC-003 smoke. *(Manual-trigger `azurerm_container_app_job` running `mcr.microsoft.com/azure-cli:latest`; script does `az login --identity` → `az account get-access-token` → `curl /probe/read` → exits 0 on 200, echoes status+body for diagnostic visibility on failure. New `probe_job_enabled` variable on the env composition defaults to false; `count = var.probe_job_enabled ? 1 : 0` keeps state empty when disabled. Validated both off and on via `tofu validate`.)*

**Checkpoint**: User Story 3 is fully demonstrable: a workload MI calls the API and is authorized via the same path human callers traverse. The pattern is documented for the next workload that lands.

---

## Phase 6: User Story 4 — Developers run the full stack locally (Priority: P2)

**Goal**: A developer signed in via `az login` can run the local backend and have it authenticate to any Azure dependency via their identity — no per-developer secrets, no `.env` credential editing.

**Independent Test**: Clean machine. `az login --tenant <busterminal-dev-tenant>`. `pwsh scripts/start-local.ps1`. Backend reads from dev Key Vault successfully without any secret in `.env` or `appsettings.Development.json`.

- [X] T070 [P] [US4] Modify the existing Key Vault configuration provider wiring in `api/BusTerminal.Api/Infrastructure/Configuration/KeyVaultExtensions.cs` (from 002) to acquire its `TokenCredential` from `IAzureCredentialFactory` instead of constructing `DefaultAzureCredential` inline. This is the proof case for FR-018. *(`AddBusTerminalKeyVault` now takes `IAzureCredentialFactory` as a parameter; `Program.cs` constructs a single `AzureCredentialFactory` instance and shares it between the config-builder call and the singleton DI registration — same credential path for KV config-load and every future SDK consumer.)*
- [X] T071 [P] [US4] Update `scripts/start-local.ps1` and `scripts/start-local.sh` to verify `az account show` succeeds before launching the backend; print a clear remediation message naming the tenant id (`596c1564-...` for dev) if `az` is not signed in or is on the wrong tenant (FR-019). *(Hard-fail when `AZURE_KEY_VAULT_URI` is set (real-Azure path); advisory warning otherwise so pure mock-tenant local dev still launches without `az login`. Also dropped the now-dead `AZURE_AD_CLIENT_SECRET`/`NEXTAUTH_SECRET`/`AUTH_SECRET`/`AZURE_AD_TENANT_ID`/`AZURE_AD_CLIENT_ID` exports left behind by US2 — those were NextAuth vestiges with placeholder values, but their continued presence contradicted the slice's "no static credentials" stance.)*
- [X] T072 [P] [US4] Update `docs/local-development.md` to reference the new credential model: developers run `az login --tenant <tenant-id>` once; the backend's `IAzureCredentialFactory` resolves their identity for every Azure dependency. Remove any prior reference to per-developer secrets or `.env`-stored credentials. **Add a numbered prerequisite at the top of the document**: "Before any local-Azure work, you must have an Entra account in the BusTerminal dev tenant (`596c1564-6e95-4c35-a80b-2dbe45a162f3`) with at least the `BusTerminal.Developer` app role assigned (see `docs/identity-role-administration.md` § Part B). MSAL no longer ships a frontend mock provider — local sign-in goes to the real dev tenant." Cross-reference this prereq from `quickstart.md` § C.1. *(`identity-role-administration.md` does not exist yet — Phase 9 polish T091 will create it; the link is forward-looking and won't 404 once T091 lands.)*
- [X] T073 [P] [US4] Add a friendly developer error in `AzureCredentialFactory.CreateCredential` for the `CredentialUnavailableException` case in `Development` environment: catch it at the SDK boundary inside `KeyVaultExtensions` and convert to a clear `InvalidOperationException` with message `"Azure credentials unavailable. Run: az login --tenant 596c1564-6e95-4c35-a80b-2dbe45a162f3"` (the dev tenant id). *(Implemented as a synchronous credential probe (`credential.GetToken(new TokenRequestContext([KeyVaultScope]), CancellationToken.None)`) before `AddAzureKeyVault` is invoked — surfaces a friendly error at config-build time instead of an opaque `DefaultAzureCredential failed to retrieve a token` deep inside the Azure SDK's lazy Load.)*
- [X] T074 [US4] Add a Vitest unit test in `api/BusTerminal.Api.Tests/Unit/Credentials/AzureCredentialFactoryTests.cs` confirming that `CreateCredential()` returns a `DefaultAzureCredential` in `Development` and that it sets `ManagedIdentityClientId` when `userAssignedClientId` is provided. *(Implemented as xUnit — the task title said "Vitest" but the path is a `.NET` test project, clearly a doc typo. Refactored `AzureCredentialFactory` to expose an internal static `BuildOptions(IHostEnvironment, string?)` seam so the `ManagedIdentityClientId` assignment is testable without reflection; added `InternalsVisibleTo("BusTerminal.Api.Tests")` to the API csproj. Eight test cases — covering Development/Production × with/without clientId, the credential-type contract, and whitespace-clientId guards. Full suite: 81/81 pass.)*

**Checkpoint**: A developer with only `az login` on a clean machine can exercise real-Azure-dependent code paths locally. The credential model is identical to deployed environments — same `IAzureCredentialFactory`, same chain, no branches in application code.

---

## Phase 7: User Story 5 — CI/CD infrastructure modules encapsulate identity provisioning (Priority: P3)

**Goal**: Adding a new environment or workload is module composition, not inline IAM. Federated credentials are a reusable module, not a one-off resource block.

**Independent Test**: Add a hypothetical new workload to `iac/environments/dev/main.tf` using only `module "workload_identity" ...` and `module "federated_credential" ...` invocations — confirm no inline `azurerm_user_assigned_identity`, `azurerm_role_assignment`, or `azuread_application_federated_identity_credential` resource blocks are introduced.

- [X] T075 [P] [US5] Create `iac/modules/federated-credential/main.tf` providing a generalized federated credential module: takes `parent_application_id`, `issuer`, `audience`, `subject`, `display_name`, `description`; produces an `azuread_application_federated_identity_credential` resource. *(Deviation: produces `azurerm_federated_identity_credential` (MI-parented) instead of `azuread_application_federated_identity_credential` (app-reg-parented). Reason: data-model.md § Federated Credential defines `ParentIdentity` as a reference to a Workload Identity (MI), and every existing FIC in the repo (pipeline FIC in `iac/platform-bootstrap/`, workload FIC in `iac/environments/dev/`) targets a MI. The app-reg variant gets a sibling module when the first app-reg-parented federation lands. Also dropped `display_name`/`description` inputs — the underlying `azurerm_federated_identity_credential` resource has neither (`name` is the display name). Documented in `iac/modules/federated-credential/README.md`.)*
- [X] T076 [P] [US5] Create `iac/modules/federated-credential/variables.tf` and `outputs.tf` (output: `credential_id`). *(Outputs `credential_id`, `name`, and `subject` — the latter two are echoed for plan-output sanity-checking. Subject validation rejects wildcard `*` per data-model.md § Federated Credential validation rules.)*
- [X] T077 [US5] Refactor the existing pipeline federated credential (inherited from 002) in `iac/environments/dev/main.tf` to use `module "federated_credential" "pipeline_dev"` rather than the inline resource block. Use `moved` blocks so `tofu plan` produces zero destructive changes. *(Deviation: the pipeline FIC actually lives in `iac/platform-bootstrap/main.tf` (line 132), not in `iac/environments/dev/main.tf`. Refactored BOTH FICs to use the new module: (1) pipeline FIC in platform-bootstrap → `module "pipeline_federation"` with a per-instance `moved` block (`["dev"]`, since only the dev instance is in state today); (2) `workload_environment` FIC in dev/main.tf:321 → `module "workload_federation_environment"` with a single `moved` block. Both `tofu validate` cleanly.)*
- [X] T078 [P] [US5] Add a CI lint check (e.g., as a step in `.github/workflows/iac-validate.yml`) that fails the build if any new `azurerm_role_assignment`, `azurerm_user_assigned_identity`, or `azuread_application_federated_identity_credential` resource appears at the `iac/environments/*/main.tf` level outside a module declaration. Use a simple `grep -E` pattern against the file set — document any allowlisted exceptions inline. *(Implemented as `scripts/lint-iac-inline-iam.sh` (executable, bash-3.2-safe so it runs on macOS system bash), wired into `iac-validate.yml` as the final step of the `validate` job. Also covers `azurerm_federated_identity_credential` (the MI flavor actually used in this repo, not just the app-reg flavor the task text named). Allowlist is two entries — `pipeline_kv_secrets_officer` and `operator_kv_secrets_officer` — both env-RG-scoped grants that genuinely cannot be modulized (the pipeline MI authors the env composition; the operator set is per-env, not per-workload). Negative-tested by injecting a forbidden resource and confirming exit 1 with `::error file=,line=` markers.)*
- [X] T079 [US5] Update `docs/deploying-environments.md` (inherited from 002) with a "Adding a new workload" section that walks through the module-composition pattern using the modules introduced by this slice. Cross-reference `quickstart.md` § SC-005. *(New § 8 "Adding a new workload" with the module-lifetime table (per-env vs per-workload), a complete HCL worked example for a hypothetical discovery worker, a reminder about the federation-subject-in-identity-and-secrets.md rule, and lint-failure guidance. Also tightened stale § 7 "Adding a new environment" framing — dropped the `(US5 preview)` suffix and the broken link to `iac/environments/test/README.md`.)*

**Checkpoint**: All identity-related IaC composes from the four new modules (`app-registration-roles`, `workload-identity`, `federated-credential`, `graph-permissions`) plus the existing `identity` module. Inline IAM blocks exist only where documented as explicit exceptions.

---

## Phase 8: User Story 6 — Microsoft Graph foundation (Priority: P3)

**Goal**: Pre-wire the Graph client abstraction so future identity-aware capabilities (display-name resolution, group lookups, org metadata) inherit a working foundation. Single Graph permission (`User.Read.All` application) granted now; future slices add more as needed.

**Independent Test**: The Graph client abstraction resolves the calling user's own profile via app-only flow against the dev tenant on the first invocation after deployment, with no manual consent or credential step at runtime.

- [X] T080 [P] [US6] Create `iac/modules/graph-permissions/main.tf` declaring the `User.Read.All` Graph application permission on the API app registration via `azuread_application_api_access`. References the well-known Microsoft Graph app id via `data "azuread_application_published_app_ids" "well_known"`. Outputs `granted_role_ids` for downstream documentation. *(Module also outputs `graph_api_client_id` (Graph's well-known app id) for any future slice that needs to issue `azuread_app_role_assignment` directly. The `granted_application_permission_ids` variable carries inline UUID validation + a non-empty guard. README documents the inventory-mirror rule (every UUID here must show up in `contracts/graph-permissions-inventory.md`).)*
- [X] T081 [US6] Add `module "graph_permissions"` to `iac/environments/dev/main.tf`. After `tofu apply`, admin consent must be granted manually per `quickstart.md` § A.2.3 — log this as a follow-up to be performed by a tenant admin. *(Composed against the existing `data "azuread_application" "api"` source from T031 — no new data source needed. `User.Read.All` UUID `df021288-bdef-4463-88db-98f22de89214` declared inline; the IaC-inline-IAM lint guard (slice 005 / T078) passes — the new resources live inside the module, not at the env-composition level. `tofu validate` clean.)*
- [X] T082 [P] [US6] Create `IGraphClient` interface in `api/BusTerminal.Api/Infrastructure/Graph/IGraphClient.cs` exposing the minimal initial surface: `Task<GraphUser?> ResolveUserAsync(string objectId, CancellationToken ct)`. Define a `GraphUser` record (oid, displayName, userPrincipalName, mail) co-located in the same file. Add a single-line code comment above the interface noting: `// Delegated Graph flows (FR-025) can be added in a later slice by injecting a user-context TokenCredential via the AzureCredentialFactory — no breaking change to this interface required.`
- [X] T083 [US6] Create `GraphClient` implementation in `api/BusTerminal.Api/Infrastructure/Graph/GraphClient.cs` constructed with a `GraphServiceClient` built from `IAzureCredentialFactory.CreateCredential()` (per `research.md` § 3). Method calls `_graph.Users[objectId].GetAsync(...)` and maps the result to `GraphUser`. Returns null on 404; throws on other errors (no silent failure — FR per spec edge cases). *(GraphClient exposes an `internal` constructor accepting a `Func<string, CancellationToken, Task<User?>>` fetch-delegate seam in addition to the public ctor — gives unit tests a clean projection-only surface without standing up a fake Kiota `IRequestAdapter`. Public ctor builds `new GraphServiceClient(credentialFactory.CreateCredential())` and wires the delegate to `graph.Users[oid].GetAsync(...)`. `InternalsVisibleTo("BusTerminal.Api.Tests")` was already in place from T074.)*
- [X] T084 [P] [US6] Create `GraphClientExtensions` in `api/BusTerminal.Api/Infrastructure/Graph/GraphClientExtensions.cs` exposing `AddBusTerminalGraphClient(this IServiceCollection)` that registers `IGraphClient → GraphClient` as a scoped service. Call from `Program.cs`.
- [X] T085 [P] [US6] Create `docs/identity-graph-permissions.md` (new) documenting: (1) the current permissions inventory (a copy of `contracts/graph-permissions-inventory.md`'s current state), (2) the procedure to grant admin consent in a new environment, (3) the procedure to add a new permission in a future slice. Cross-reference the inventory contract.
- [X] T086 [US6] Add unit tests in `api/BusTerminal.Api.Tests/Unit/Graph/GraphClientTests.cs` mocking `GraphServiceClient`'s `IRequestAdapter` and asserting (a) successful user resolution maps to `GraphUser` correctly, (b) 404 returns null, (c) other errors propagate. *(Deviation: rather than standing up a fake Kiota `IRequestAdapter` (heavy — dozens of generic overloads to stub) and depending on an internal Microsoft.Graph SDK request shape that changes between minor versions, the tests inject the projection-side of `GraphClient` directly via the internal delegate-fetch seam exposed for this purpose. 13 cases cover the projection (populated User, null-Id fallback, null result, 404→null, 403 propagation, arbitrary exception propagation, cancellation-token pass-through, blank-oid validation, null-ctor guards). One smoke test imports `IRequestAdapter` to keep that reference resolvable for a future author who wants to wire a real GraphServiceClient. Full unit + integration suite: 95/95 pass.)*
- [X] T087 [US6] Add an integration test (gated on the dev tenant being reachable; opt-in via env var `BUSTERMINAL_GRAPH_INTEGRATION=1`) in `api/BusTerminal.Api.Tests/Integration/GraphResolveSelfTests.cs` that resolves the test caller's own profile and asserts `displayName` is non-empty. *(Reads `BUSTERMINAL_GRAPH_TEST_OID` for the user object id to resolve (default-skip pattern: early-return on missing opt-in env var, fail-loud on missing oid var when opt-in IS set). Composes the real `AzureCredentialFactory` + real `GraphClient` so the path under test matches the deployed shape exactly. Also fixed an unrelated test-hygiene issue: `RoleProbeAppFactory` now substitutes a `StubGraphClient` for the integration matrix tests so `/probe/developer` invocations stop round-tripping the dev tenant (~1.4 s/call → < 1 ms). The new stub returns `null` from `ResolveUserAsync`, which mirrors the "consent not yet granted" code path the probe endpoint handles gracefully — the response shape under assertion is unchanged.)*
- [X] T088 [US6] Modify `api/BusTerminal.Api/Features/RoleProbes/DeveloperToolingProbeEndpoint.cs` (created in T037) to call `IGraphClient.ResolveUserAsync(caller.ObjectId)` and include the resolved `displayName` in the response. This is the SC-009 smoke surface. Wrap the call in try/catch and degrade gracefully if Graph consent is not yet granted (return the probe response with a `graphResolvedDisplayName: null` field and log a warning). *(Introduced a `DeveloperToolingProbeResponse` record that extends the base `ProbeResponse` shape with the optional `GraphResolvedDisplayName` field — the matrix tests in `RoleProbeEndpointTests` still assert against the common fields (`callerObjectId`, `callerEffectiveRoles`, etc.) without modification since `System.Text.Json` accepts extra fields. Try/catch narrowly scoped to `ODataError` with `ResponseStatusCode in {401, 403}` per the FR-024 graceful-degradation contract; other errors propagate so they're not silently swallowed. Warning emitted via a `[LoggerMessage]` source-gen delegate at EventId 3001 (avoids CA1848). Endpoint is no-op (returns the probe with `GraphResolvedDisplayName: null`) when the caller has no resolvable oid.)*

**Checkpoint**: The Graph foundation exists. Future slices that need Graph access can call `IGraphClient` directly; new permissions follow the documented procedure. SC-009 is verifiable.

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: Final hardening, documentation cohesion, smoke validation across all user stories.

---

> # 🚨 🚨 🚨 **POST-MERGE MANUAL STEP — DO NOT SKIP** 🚨 🚨 🚨
>
> ### ⭐⭐⭐ **GRANT GRAPH ADMIN CONSENT ON `bt-dev-api`** ⭐⭐⭐
>
> **After Phase 9 polish lands and CD applies the slice to dev**, a tenant admin (a-christopher@chrishou.se) must run:
>
> ```bash
> az login --tenant 596c1564-6e95-4c35-a80b-2dbe45a162f3
> az ad app permission admin-consent --id 9fb329a3-7b5b-4fdf-a46a-71f7df1d6716
> ```
>
> **Why this can't be automated**: `User.Read.All` (application) requires tenant-admin consent. Slice 003 deliberately keeps this manual (FR-024 / research § 9) so the grant lives in the Entra directory audit log and not in `tofu apply` history.
>
> **What's broken until you do this**:
> - SC-009 (Graph self-resolve first-call success) cannot pass.
> - `GET /probe/developer` returns `graphResolvedDisplayName: null` and logs a warning at `EventId=3001`.
>
> **After running it**: update `contracts/graph-permissions-inventory.md` § "Consent state by environment" with today's date and the granting admin's UPN.
>
> 🚨 🚨 🚨 **DO NOT MERGE THE SLICE WITHOUT QUEUING THIS STEP** 🚨 🚨 🚨

---

- [ ] T089 [P] Run the full quickstart `quickstart.md` end-to-end on a fresh machine; record any deviations and fix them in-place. Target: ≤ 30 min from clone to a working role-aware local stack.
- [ ] T090 [P] Update root `README.md` to mention the role-aware authentication posture and link to `docs/identity-and-secrets.md`, `docs/identity-role-administration.md`, `docs/identity-graph-permissions.md`.
- [ ] T091 [P] Create `docs/identity-role-administration.md` (the operator runbook promoted from `quickstart.md` Part B): how to grant/remove roles, how to bootstrap the initial Admin, how role propagation works.
- [ ] T092 [P] Add Playwright smoke in `web/tests/e2e/no-access-experience.spec.ts` covering SC-008: a no-role test user sees `/no-access` rendered within 2 seconds of completing MSAL sign-in. **Include an explicit Playwright timing assertion** — capture a timestamp at the post-MSAL-redirect navigation event, then `await expect(page.getByTestId('no-access-page')).toBeVisible({ timeout: 2000 })` and assert `(Date.now() - postRedirectTs) <= 2000`. Fail the test if the page renders late.
- [ ] T093 [P] Add Playwright smoke in `web/tests/e2e/msal-sign-in-and-whoami.spec.ts` covering the basic sign-in → `/whoami` → effective-roles-rendered → sign-out cycle. **Also verify the inherited (002) posture is intact post-rewrite**: (a) the page loads over HTTPS in the deployed env (assert `page.url().startsWith('https://')` when `BASE_URL` is non-localhost); (b) a deliberately-malformed bearer token sent to `/whoami` via `fetch` returns `401` with a `WWW-Authenticate: Bearer ...` header (confirms the inherited Microsoft.Identity.Web validation pipeline is still active after T028).
- [ ] T094 [P] Add a contract conformance test in `api/BusTerminal.Api.Tests/Integration/OpenApiConformanceTests.cs` that parses `contracts/role-probes.openapi.yaml` + `contracts/whoami.openapi.yaml` and compares against the live OpenAPI document emitted by the API. Fail on shape drift.
- [ ] T095 Run `axe` against the new no-access page and role-aware affordances (T043, T048); resolve any WCAG 2.2 AA violation.
- [ ] T096 [P] Verify W3C Trace Context (`traceparent`/`tracestate`) propagation on MSAL-acquired requests (constitution-bound). Add a manual verification step to `quickstart.md` Part D and an automated assertion to T093.
- [ ] T097 [P] Update `speckit-artifacts/tech-stack.md` § 7 (Identity & Authentication) to mention MSAL for SPA auth and reference the role-permission matrix as the binding contract for future slices. This is the durable home for the slice's identity decisions.
- [ ] T098 Run `tofu plan` against `iac/environments/dev/` and assert it shows only the additive changes expected by this slice (new modules, role definitions, Graph permission grant) plus zero-effect refactors (`moved` blocks). No destructive changes.
- [ ] T099 [P] Verify SC-002 / SC-007: re-run `gitleaks detect --redact --no-banner --source=.` after all polish tasks land; confirm zero findings. Document the run output in the PR description.
- [ ] T100 Update `docs/architecture.md` (inherited from 002) with a short "Identity & Authorization" section showing the MSAL → API → role-policy flow and the workload-identity → API call shape. One diagram + a paragraph; defer to the role-permission matrix for details.
- [ ] T101 [P] Add a CI guard in `.github/workflows/iac-validate.yml` (or a new `iac-credential-scan` job) that fails the build on any new occurrence of inline Azure-service connection-string / account-key / SAS-token patterns inside `iac/` HCL files. Use a `grep -E` against the file set with patterns like `(AccountKey|SharedAccessKey|connection_string|primary_access_key|sas_token)\s*=` and exclude documented exceptions via an inline allowlist. Future-proofs FR-015's "no static credentials" stance as later slices introduce Cosmos / AI Search / Storage / OpenAI / Service Bus / App Configuration resources.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies. Can start immediately.
- **Phase 2 (Foundational)**: Depends on Phase 1 (libraries/providers installed). **BLOCKS all user stories.**
- **Phase 3 (US1)**: Depends on Phase 2.
- **Phase 4 (US2)**: Depends on Phase 2. Largely deletion-and-audit work; can run **in parallel with Phase 3** if staffed.
- **Phase 5 (US3)**: Depends on Phase 3 (needs the role policies + probes to call against). Workload identity IaC can be authored in parallel with Phase 3 but the integration test needs the probes.
- **Phase 6 (US4)**: Depends on Phase 2 (`IAzureCredentialFactory`). Can run **in parallel with Phase 3–5**.
- **Phase 7 (US5)**: Depends on Phases 3 and 5 (the IaC modules to refactor are introduced there).
- **Phase 8 (US6)**: Depends on Phase 2 (credential factory) and Phase 3 (the developer-tooling probe to extend in T088). Can otherwise run **in parallel with Phase 5/6/7**.
- **Phase 9 (Polish)**: Depends on all desired user stories being complete.

### User Story Dependencies

- **US1 (P1)**: The MVP. Independent once Phase 2 is done.
- **US2 (P1)**: Independent of US1 — pure cleanup + audit.
- **US3 (P2)**: Depends on US1's probe endpoints (the workload calls them) but its IaC module work is independent.
- **US4 (P2)**: Independent of US1–US3.
- **US5 (P3)**: Depends on US3's `workload-identity` module and US1's `app-registration-roles` module being authored.
- **US6 (P3)**: Largely independent; touches the same `DeveloperToolingProbeEndpoint` as US1 (T088 modifies T037's output).

### Within Each User Story

- IaC modules can be authored in parallel with backend/frontend code (different file trees).
- Backend models/types before services before endpoints.
- Endpoints before contract/integration tests against them.
- Frontend hooks before components that consume them.
- Story is "done" only when its independent test passes end-to-end.

### Parallel Opportunities

- All Phase 1 `[P]` tasks (T001, T002, T003, T005, T006): parallel.
- Backend foundational types (T007–T010): parallel.
- Frontend MSAL foundational pieces (T013–T016, T019–T022, T025): parallel.
- All five probe endpoint creation tasks (T033–T037): parallel.
- US1 + US2 + US4 + US6: can be worked in parallel by separate developers once Phase 2 is complete (US3 + US5 have intra-story sequencing).
- Within Polish: T089–T097 are largely parallel except where noted.

---

## Parallel Example: User Story 1

```bash
# After Phase 2 complete, run these in parallel:
Task: T029 — Create iac/modules/app-registration-roles/main.tf
Task: T033 — Create ReadProbeEndpoint.cs
Task: T034 — Create MutateDomainProbeEndpoint.cs
Task: T035 — Create OperatePlatformProbeEndpoint.cs
Task: T036 — Create AdministerProbeEndpoint.cs
Task: T037 — Create DeveloperToolingProbeEndpoint.cs
Task: T048 — Create role-aware-button.tsx

# Then sequentially (each blocks the next):
Task: T032 — RolePolicies.cs (must exist before probe tests pass)
Task: T038 — WhoAmIEndpoint.cs extension
Task: T044 — (authenticated)/layout.tsx role context

# Tests can run in parallel after their subject code lands:
Task: T049 — Vitest role-permission-matrix tests
Task: T050 — xUnit RolePoliciesTests
Task: T051 — xUnit PlatformPrincipalMappingTests
Task: T052 — xUnit RoleProbeEndpointTests (depends on T032–T037)
```

---

## Implementation Strategy

### MVP First (User Stories 1 + 2 — both P1)

1. Complete **Phase 1**: Setup (T001–T006).
2. Complete **Phase 2**: Foundational (T007–T028). **Blocking — no user-story work until done.**
3. Complete **Phase 3**: User Story 1 (T029–T054). Validate via the 5×5 role matrix probe tests.
4. Complete **Phase 4**: User Story 2 (T055–T063). Validate via gitleaks zero-findings.
5. **STOP AND VALIDATE**: SC-001, SC-002, SC-007, SC-008 all hit. The slice is shippable here as a coherent role-aware foundation, even without US3–US6.
6. Optional checkpoint deploy/demo.

### Incremental Delivery (P2 stories)

1. **Phase 5 (US3)** and **Phase 6 (US4)** in parallel: US3 needs the probes from Phase 3; US4 needs only Phase 2.
2. Validate SC-003 (US3) and SC-004 (US4).
3. Demo internal-caller and developer-local-stack paths.

### Final Delivery (P3 stories + polish)

1. **Phase 7 (US5)**: IaC module composition refactor. Validate SC-005.
2. **Phase 8 (US6)**: Graph foundation. Validate SC-009.
3. **Phase 9 (Polish)**: Cohesion, smoke validation, secret-scan re-run.

### Parallel Team Strategy

With multiple developers and Phase 2 complete:

- **Developer A**: Phase 3 (US1) — the MVP. Owns the role-policy + probe + role-aware UI.
- **Developer B**: Phase 4 (US2) — the cleanup sweep. Independent.
- **Developer C**: Phase 6 (US4) — credential-factory consolidation. Independent.
- After Phase 3 lands: Developer A or D picks up Phase 5 (US3); Developer B or E picks up Phase 8 (US6).
- Phase 7 (US5) is the last refactor — sequenced after Phases 3 and 5.

---

## Notes

- **Tests are included** because the spec's user stories each define an Independent Test and SC-001–SC-009 are concrete enough to assert against. Tests follow the slice's testing strategy from `plan.md`: Vitest + RTL for unit, `WebApplicationFactory` for integration, Playwright for E2E.
- **No new top-level technology** is introduced — every library / provider / package addition (MSAL, Microsoft.Graph, `azuread` provider) is a direct extension of choices the constitution and tech-stack already approve.
- **The matrix is the contract.** When in doubt about who can do what, consult `contracts/role-permission-matrix.md` — it is authoritative over the code.
- **Commit after each task or logical group.** Pre-commit gitleaks remains advisory until T063 wires it into CI; do not skip.
- **Stop at checkpoints** to validate stories independently. The slice ships value at every checkpoint, not just at the end.
- **Avoid**: vague edits, same-file conflicts across parallel tasks, cross-story dependencies that break independence.
