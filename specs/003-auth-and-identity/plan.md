# Implementation Plan: Auth and Identity

**Branch**: `feature/003-auth-and-identity` | **Date**: 2026-05-19 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/003-auth-and-identity/spec.md`

---

## Summary

Elevate BusTerminal from "any signed-in Entra ID user can call anything" (the posture shipped in spec 002) to a coherent, role-aware, zero-trust identity foundation. The slice (1) replaces the NextAuth-backed frontend sign-in with **MSAL** (`@azure/msal-browser` + `@azure/msal-react`); (2) defines four platform roles (**BusTerminal.Admin / Operator / Reader / Developer**) as Entra ID **app roles** on the backend API app registration and binds them to a published role-permission matrix across five operation classes (Read / Mutate-Domain / Operate-Platform / Administer / Developer-Tooling); (3) enforces those roles in the backend via `Microsoft.Identity.Web`'s `roles` claim handling, with `Microsoft.Graph` app-only `User.Read.All` as the Graph foundation; (4) generalizes the slice-002 identity OpenTofu plumbing into reusable `workload-identity` and `federated-credential` modules and an `app-registration-roles` composition so adding a new workload is configuration not custom IAM; (5) sweeps the codebase for any remaining static credential paths and codifies a single `DefaultAzureCredential` abstraction usable identically in local dev and deployed environments.

The slice ships **probe endpoints only** — no domain functionality. The probes are the end-to-end test surface for the role-permission matrix (FR-009c). Domain authorization (per-namespace, per-queue, per-topic) is explicitly deferred to the slices that introduce those resources.

The technical approach below maps every spec FR to an approved choice in `speckit-artifacts/tech-stack.md`. **No new top-level technologies are introduced.** New libraries (`@azure/msal-browser`, `@azure/msal-react`, `Microsoft.Graph`) are direct extensions of the existing Entra-ID + Microsoft.Identity.Web foundation.

---

## Technical Context

**Language/Version**:
- Frontend: TypeScript (strict mode) on Node.js 22 LTS — inherited from 002
- Backend: C# 13 / .NET 10 (target framework `net10.0`) — inherited from 002
- IaC: OpenTofu ≥ 1.10 (HCL) — inherited from 002

**Primary Dependencies** (additions for this slice):

- **Frontend (new in 003)**:
  - `@azure/msal-browser` (latest 4.x) — browser MSAL with Authorization Code + PKCE
  - `@azure/msal-react` (latest 3.x) — React bindings (`MsalProvider`, `useMsal`, `useAccount`, `useIsAuthenticated`, `withMsal`)
- **Frontend (removed in 003)**: `next-auth` (currently in `web/lib/auth.ts`); the mock-tenant credentials provider is removed in favor of an MSAL-equivalent dev-mode pathway documented below.
- **Backend (new in 003)**:
  - `Microsoft.Graph` (latest 5.x for .NET) — Graph SDK v5 used via app-only client; sole consumer is the new `IGraphClient` abstraction
  - No new auth library — the existing `Microsoft.Identity.Web` package (already in 002) gains usage of role-based authorization policies and app-role-claim handling
- **IaC (new in 003)**:
  - `hashicorp/azuread` provider (added; complements the existing `hashicorp/azurerm`) — required for app registration / app role / federated credential / Graph permission resources. Version pinned alongside `azurerm`.
  - No new AVM modules required for this slice (AVM coverage of `azuread_*` resources is thin; modules here are hand-authored and live under `iac/modules/`)

**Storage**: N/A for this slice. No persistence is added. (Cosmos DB and AI Search remain unprovisioned until a domain slice introduces them.)

**Testing** (additions for this slice):
- Frontend: Vitest + RTL for MSAL config + token acquisition + role-aware affordance components; Playwright smoke for the sign-in → role-resolved-affordances → sign-out flow against the dev environment
- Backend: xUnit + `WebApplicationFactory` for role-policy authorization tests using a test-token mint flow (no real Entra calls in unit/integration tier); separate integration suite hits the dev environment with real tokens for the role probes
- IaC: `tofu validate` + `tofu plan` (existing CI), with additional `checkov` rules for `azuread_application` properties
- Secret scanning: `gitleaks` (existing CI) re-run on this slice's PRs to enforce SC-007

**Target Platform**: Linux containers on Azure Container Apps (inherited from 002). Developer OS: macOS / Windows / Linux supported via the existing prerequisites; PowerShell remains the primary shell.

**Project Type**: Web application (frontend + backend) plus infrastructure as code (no new top-level component types introduced).

**Performance Goals**:
- Sign-in → first authenticated UI render (SC-008 derivative): ≤ 2 seconds for the "no platform role" experience after Entra returns to the SPA
- Backend authorization decision latency added over inherited 002 path: ≤ 5 ms p95 (role-claim handling is in-process — this is effectively token-validation overhead only)
- Telemetry latency for authz failure events: within the documented telemetry latency window from spec 002 (≤ 2 minutes)
- Adding a new workload identity via the new IaC modules: ≤ 15 minutes of authoring (SC-005 derivative)

**Constraints**:
- **MSAL is the sole frontend authentication library.** Any NextAuth code shipped in 002 is removed, not coexisting.
- **App roles, not security groups.** Roles surface in the `roles` claim and are evaluated via `Microsoft.Identity.Web`'s built-in role-claim handling (no custom group-to-role mapping layer).
- **No internal-trust bypass.** Internal workload calls validate tokens identically to human calls (FR-012). No `X-Internal-Caller` headers, no shared secrets, no network-position trust.
- **Managed Identity everywhere** for Azure-service access (FR-013–FR-016). The list of services covered is enumerated in FR-015; this slice does not provision the unprovisioned ones (Cosmos / AI Search / Storage / OpenAI / Service Bus / App Config) but reserves the credential-acquisition path for them.
- **`User.Read.All` is the only Graph permission granted.** No `GroupMember.Read.All`, no `Directory.Read.All` (FR-024, Q5 clarification).
- **Initial `BusTerminal.Admin` role is granted manually by a tenant admin via the Entra portal** (FR-002a, Q3 clarification). Not in OpenTofu state.
- **W3C Trace Context propagation continues to be mandatory** on every UI-originated HTTP request, including the new MSAL-acquired calls (inherited constitution-bound constraint).
- **No PII in telemetry** beyond correlation ids and `oid` (object id) — the Platform Principal carries display name and email only for in-process audit-log structuring and never serialized to telemetry without an explicit future opt-in.
- **No secrets** in any source-controlled file or container image (FR-016, SC-007).

**Scale/Scope**:
- Dev environment: ≤ 10 internal users + ≤ 2 probe workloads, roles distributed by manual Entra portal assignment per FR-002a
- Backend code added: ~6 endpoints (5 role probes + 1 enriched `/whoami`), ~3 new infra classes (PlatformPrincipal, RolePolicies, GraphClient), well under 1,500 net LOC including tests
- Frontend code added: MSAL config + provider, ~3 hooks, ~5 role-aware affordance components, ~3 routes (sign-in landing / no-access / sign-out). Less than 1,500 net LOC.
- IaC: 3 new modules + role-assignment additions to the existing identity module. Net new HCL ~ 400 LOC.

---

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-evaluated after Phase 1 design (below).*

| Principle | Status | Evidence |
|-----------|--------|----------|
| **I. Azure-First Architecture** | ✅ Pass | Entra ID, app roles, Microsoft Graph, Managed Identity, federated credentials — every identity primitive is Azure-native. No multi-IDP abstraction is introduced. |
| **II. API-First Design** | ✅ Pass | Role probes are first-class API endpoints, documented in `contracts/`, callable by the frontend and by external automation. The role-aware UI consumes the same `whoami` + probe contracts — no UI-only backdoors. OpenAPI emission continues. |
| **III. Strong Domain Modeling** | ⚪ N/A | This slice still introduces no messaging-domain entities. The Platform Principal / Platform Role / Workload Identity / Federated Credential / Graph Permission Grant / App Registration concepts are *platform* entities and are documented in `data-model.md`. The constitution's domain-modeling principle binds when registry-domain slices begin; the platform-entity naming used here is consistent across API, IaC, docs, and telemetry. |
| **IV. Security by Default** | ✅ Pass — *strengthens* existing posture | App-role-based RBAC (FR-009), no static credentials (FR-013–FR-016), managed identity everywhere (FR-013), federated credentials for CI/CD (FR-029), no internal-trust bypass (FR-012), HTTPS required (FR-031), no token contents in logs (FR-033), explicit "no default role" stance (FR-010). The external-ingress deviation noted in spec 002's plan is unchanged by this slice — the *mitigation* (mandatory token validation at the edge) is strengthened, not relaxed. |
| **V. Operational Excellence** | ✅ Pass | Authz events flow into the existing OTel → App Insights → Log Analytics pipeline (FR-032, FR-034). Correlation ids propagate (constitution + 002 trace-context constraint). No silent retries — Graph permission failures and managed-identity propagation delays surface explicitly per edge cases. |
| **VI. Incremental Extensibility** | ✅ Pass | The five operation classes + role-permission matrix form a stable contract that domain slices bind to. New workload identities, new federated credentials, new Graph permissions are additive via the new IaC modules + permissions inventory document. Delegated Graph flows are *supported by the abstraction* but not enabled — extensibility preserved without scope creep. |
| **Modular Monolith First** | ✅ Pass | No service split. The Graph client is a class in the backend; the role policies are .NET authorization policies in the same process. Nothing in this slice forces decomposition. |
| **Container-Native** | ✅ Pass | All workloads remain containerized. The Graph SDK and MSAL are pure in-process libraries; no sidecars. Local containerized dev (`docker compose up`) continues to work with `DefaultAzureCredential` using shared volumes for the developer's `~/.azure` token cache. |
| **Async-First** | ⚪ N/A | Auth is request-scoped and synchronous-by-design. No async workflows are introduced. |
| **CI/CD Requirements** | ✅ Pass | Existing CI matrix (build, unit, integration, lint, format, gitleaks, dependency vuln scan, container scan, tofu validate/plan/tflint/checkov) continues. The slice adds a Playwright smoke specifically targeting MSAL sign-in. |
| **Testing Standards** | ✅ Pass | Role-policy unit tests use the standard `WebApplicationFactory` pattern with a stub token-issuance helper; integration tests hit the dev environment with real Entra tokens for the role probe matrix; frontend role-aware components have Vitest + RTL coverage and axe a11y. Tests assert observable behavior (status codes, response shapes, rendered affordances), not implementation detail. |
| **AI Tooling / MCP Usage** | ✅ Pass | Microsoft Learn MCP is the source of truth for `Microsoft.Identity.Web` and `Microsoft.Graph` patterns. context7 MCP is the source of truth for MSAL React APIs. Next.js MCP is consulted for App-Router-correct MSAL provider mounting. shadcn/ui MCP for any role-aware affordance primitives. |

**Gate decision**: PASS. No new deviations introduced. The slice strengthens Principle IV without adding violations to any other principle. Complexity Tracking carries no entries for this slice.

---

## Project Structure

### Documentation (this feature)

```text
specs/003-auth-and-identity/
├── plan.md                                 # This file
├── research.md                             # Phase 0 output (this run)
├── data-model.md                           # Phase 1 output (this run)
├── quickstart.md                           # Phase 1 output (this run)
├── contracts/                              # Phase 1 output (this run)
│   ├── whoami.openapi.yaml                 # /whoami extended with roles + caller type
│   ├── role-probes.openapi.yaml            # 5 probe endpoints — one per operation class
│   ├── role-permission-matrix.md           # The binding contract for all current/future endpoints
│   └── graph-permissions-inventory.md      # Initial Graph permissions + add-permissions procedure
└── tasks.md                                # NOT created here — /speckit-tasks output
```

### Source Code (repository root)

```text
/web/                                       # Next.js frontend — inherited from 002 + 001
  app/
    layout.tsx                              # Existing — MsalProvider injected at root
    page.tsx                                # Existing — unchanged
    (auth)/
      signin/page.tsx                       # REWRITTEN — MSAL redirect/popup entry, no NextAuth
      signout/page.tsx                      # REWRITTEN — MSAL logoutRedirect
      no-access/page.tsx                    # NEW — "no platform role assigned" experience (SC-008)
    (authenticated)/
      layout.tsx                            # MODIFIED — MSAL-authenticated guard + role context provider
      platform-status/page.tsx              # MODIFIED — shows effective roles in addition to identity
    api/auth/[...nextauth]/route.ts         # REMOVED — NextAuth handler deleted with the library
  components/
    auth/
      msal-provider.tsx                     # NEW — wraps app shell; reads MSAL config from env
      auth-guard.tsx                        # NEW — gate for authenticated-only routes
      role-guard.tsx                        # NEW — gate for role-required routes / affordances
      role-aware-button.tsx                 # NEW — disabled-or-hidden button variant for FR-006
      user-menu.tsx                         # MODIFIED — MSAL account + sign-out; shows effective roles
    layout/
      navigation-shell.tsx                  # MODIFIED — items filtered by effective roles
  hooks/
    use-current-user.ts                     # NEW — returns the active MSAL account + claims
    use-roles.ts                            # NEW — returns effective roles parsed from the `roles` claim
    use-has-role.ts                         # NEW — boolean check helper
    use-acquire-token.ts                    # NEW — silent-first, interactive-fallback token acquisition for the API scope
  lib/
    auth/
      msal-config.ts                        # NEW — MSAL PublicClientApplication config
      msal-instance.ts                      # NEW — singleton client export
      scopes.ts                             # NEW — known scopes (api://<api-app-id>/.default, etc.)
      claims.ts                             # NEW — typed claim accessors (oid, tid, roles, name, preferred_username)
    api-client.ts                           # REWRITTEN — uses MSAL acquireTokenSilent + traceparent + retry on 401
    auth.ts                                 # REMOVED — NextAuth config; superseded by the auth/ folder above

/api/                                       # .NET 10 backend — inherited from 002
  BusTerminal.Api/
    Program.cs                              # MODIFIED — registers role policies, Graph client, principal mapping
    Authorization/
      PlatformPrincipal.cs                  # NEW — FR-008 normalized principal record
      PlatformRole.cs                       # NEW — enum: Admin / Operator / Reader / Developer
      OperationClass.cs                     # NEW — enum: Read / MutateDomain / OperatePlatform / Administer / DeveloperTooling
      RolePolicies.cs                       # NEW — IServiceCollection extension binding each operation class to roles
      PrincipalAccessor.cs                  # NEW — IPlatformPrincipalAccessor → injectable into endpoints
      RolesClaimExtensions.cs               # NEW — read effective roles from JwtBearer principal
    Features/
      Identity/
        WhoAmIEndpoint.cs                   # MODIFIED — returns oid, tid, name, callerType, effectiveRoles, correlationId
      RoleProbes/                           # NEW — Phase-1 contract endpoints validating the matrix
        ReadProbeEndpoint.cs                # GET  /probe/read         (Operation class: Read)
        MutateDomainProbeEndpoint.cs        # POST /probe/mutate-domain (Operation class: MutateDomain)
        OperatePlatformProbeEndpoint.cs     # POST /probe/operate       (Operation class: OperatePlatform)
        AdministerProbeEndpoint.cs          # POST /probe/administer    (Operation class: Administer)
        DeveloperToolingProbeEndpoint.cs    # GET  /probe/developer     (Operation class: DeveloperTooling)
    Infrastructure/
      Authentication/
        AuthenticationExtensions.cs         # MODIFIED — adds role policies; mock handler now emits roles claim
        MockAuthenticationHandler.cs        # MODIFIED — accepts ?roles=... query param in Development for testing
      Graph/
        IGraphClient.cs                     # NEW — abstraction surface for app-only Graph calls
        GraphClient.cs                      # NEW — Microsoft.Graph SDK v5 impl using ManagedIdentityCredential + DefaultAzureCredential chain
        GraphClientExtensions.cs            # NEW — DI wiring
      Credentials/
        AzureCredentialFactory.cs           # NEW — single DefaultAzureCredential builder, used by Graph + future SDKs
  BusTerminal.Api.Tests/
    Unit/
      Authorization/
        RolePoliciesTests.cs                # NEW — each role × each operation class
        PlatformPrincipalMappingTests.cs    # NEW — claims → PlatformPrincipal
      Graph/
        GraphClientTests.cs                 # NEW — abstraction wiring tests; live Graph hit is in Integration tier
    Integration/
      RoleProbeEndpointTests.cs             # NEW — 5 endpoints × 4 roles × no-role = matrix coverage via WebApplicationFactory + mock handler
      WhoAmIEndpointTests.cs                # MODIFIED — asserts roles included in response

/iac/                                       # OpenTofu — inherited from 002
  modules/
    identity/                               # MODIFIED — gains app-role-assignment outputs
    workload-identity/                      # NEW — generalized: UAMI + optional federated credential + optional Azure-resource RBACs + optional API-app-role assignment
    federated-credential/                   # NEW — generalized GitHub OIDC federation block
    app-registration-roles/                 # NEW — declares the 4 platform app roles on the API app registration via the azuread provider
    graph-permissions/                      # NEW — declares User.Read.All app-only permission + records admin-consent requirement
  environments/
    dev/
      main.tf                               # MODIFIED — wires the new modules; adds probe-workload sample wiring
      providers.tf                          # MODIFIED — adds azuread provider with pinned version
      terraform.tfvars                      # MODIFIED — adds tenant_id, api_app_object_id, web_app_object_id refs

/docs/                                      # MODIFIED
  identity-and-secrets.md                   # REWRITTEN — supersedes 002's version; now the authoritative source
  identity-role-administration.md           # NEW — operator runbook: assigning roles, granting Admin (FR-002a)
  identity-graph-permissions.md             # NEW — permission inventory + procedure to add new permissions (FR-024)
  local-development.md                      # MODIFIED — DefaultAzureCredential walkthrough, az login tenant guidance

/CLAUDE.md                                  # MODIFIED — SPECKIT marker block repointed to this plan
```

**Structure Decision**:

This plan extends — does not relocate — the layout established by slices 001 and 002 (top-level `web/`, `api/`, `iac/`, `docs/`). Every new artifact lives inside the existing role-keyed directory it semantically belongs to. The only "new top-level domain" is the `/api/BusTerminal.Api/Authorization/` folder, introduced because authorization concerns now span more than a single feature slice and deserve a stable home alongside `Features/` and `Infrastructure/`. The placement keeps Vertical Slice Architecture intact for *feature* code while giving cross-cutting platform primitives (PlatformPrincipal, RolePolicies, OperationClass) a dedicated namespace.

The NextAuth route handler (`web/app/api/auth/[...nextauth]/route.ts`) is *deleted*, not stubbed. Removing it is part of the slice's "no two auth systems" rule (FR-003). The `web/middleware.ts` shipped by 002 will be reviewed and adapted to MSAL's session model in implementation (see research.md §1).

---

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

No new constitutional deviations introduced by this slice. The existing slice-002 deviation (backend external ingress with mandatory token validation) is unchanged and is not relitigated here; this slice in fact strengthens the *mitigation* by making per-request token validation role-aware and removing the "any signed-in user can hit anything" fallback.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|--------------------------------------|
| *(none)* | | |

---

## Phase 0 (research.md) — completed in this run

See [research.md](./research.md) for the resolved-decision record. Summary of the 10 research topics covered:

1. **Frontend MSAL package selection and config shape** → `@azure/msal-browser` 4.x + `@azure/msal-react` 3.x; PublicClientApplication with `auth.authority` = `https://login.microsoftonline.com/<tenant-id>`, `auth.clientId` = web app registration id, `cache.cacheLocation` = `sessionStorage` (per MS recommended SPA default), Authorization Code + PKCE only.
2. **Backend role-claim handling** → `Microsoft.Identity.Web`'s default role-claim mapping (`roles` claim → `ClaimTypes.Role`) consumed via `AuthorizationPolicy.RequireRole`. Policies are named per operation class (not per role) so endpoints declare *what they are*, not *who can call them*.
3. **App-only Graph client wiring** → `Microsoft.Graph` v5 with `Azure.Identity.ChainedTokenCredential` resolving Managed Identity (deployed) and Azure CLI/VS Code (local). Single `IGraphClient` abstraction is the only consumer.
4. **Mock-tenant role surfacing for local dev** → Extend the existing `MockAuthenticationHandler` (002) to accept a `roles` query parameter and project it into a `ClaimTypes.Role` collection. Keeps the "tenant=development" path consistent with production semantics without ever touching Microsoft.
5. **MSAL dev-mode local fallback** → Use an MSAL "no-IDP" approach is not possible — MSAL requires an issuer. Local dev signs into the real dev tenant; the mock-auth path is *backend-only*. Frontend always uses the dev tenant for sign-in even when the backend is running in mock mode.
6. **OpenTofu `azuread` provider** → Pinned ≥ 3.1 to gain stable `azuread_application_api_access` resource (preferred over the legacy `required_resource_access` block). App roles declared on the API app registration via `azuread_application.app_role`.
7. **App role assignment to users vs groups** → Confirmed: this slice grants app roles to *users* (and to *managed identities* for workload-to-API calls) via `azuread_app_role_assignment`. Group-claim mapping is documented as a deferred future path (Q2 clarification).
8. **Initial Admin assignment policy** → Per Q3 clarification: manual via Entra portal, documented in `docs/identity-role-administration.md`. Not in OpenTofu state. The IaC modules do declare the *role definitions* (so they exist on the API app registration), but no module assigns the initial `BusTerminal.Admin` role.
9. **Graph admin-consent procedure** → `User.Read.All` (application) requires admin consent. The IaC module records the grant *intent* (via `azuread_application_api_access`); the consent itself is a tenant-admin action and is documented in `docs/identity-graph-permissions.md`. This is the same pattern that 002 used for the API exposed-scope.
10. **Secret-scan baseline refresh** → `gitleaks` config reviewed; nothing in this slice introduces credential-shaped strings. The NextAuth `clientSecret` env-var reference in 002 is *removed* by deleting `web/lib/auth.ts`. A new gitleaks allowlist entry covers MSAL config keys (which are not secrets).

---

## Phase 1 — completed in this run

- **data-model.md** documents six platform constructs at logical-design granularity: Platform Principal, Platform Role, Operation Class, Workload Identity, Federated Credential, Graph Permission Grant, and App Registration. Lifecycle, identity rules, validation rules, and relationships are captured. No domain entities are introduced.
- **contracts/whoami.openapi.yaml** updates the `GET /whoami` contract inherited from 002 to include effective roles and caller type. The response shape is additive-only (existing 002 clients continue to parse it).
- **contracts/role-probes.openapi.yaml** declares the five probe endpoints, one per operation class, each annotated with the role(s) authorized to call it. These probes are the FR-009c end-to-end test surface for the matrix.
- **contracts/role-permission-matrix.md** is the published role-to-operation-class matrix (FR-009b). It is the contract every current and future endpoint binds against and is the source of truth referenced from the OpenAPI fragments.
- **contracts/graph-permissions-inventory.md** records the single `User.Read.All` (application) permission granted in this slice (FR-024) and the procedure for adding additional permissions in future slices.
- **quickstart.md** is the operator runbook: Entra app-registration changes (declare app roles, expose API scope, register graph permission), admin consent dance, initial Admin role assignment, MSAL frontend config, developer `az login` walkthrough, and the five-step smoke validation mapping to SC-001 through SC-009.

Agent context update: `CLAUDE.md`'s SPECKIT-marked block is repointed to this plan (`specs/003-auth-and-identity/plan.md`).

---

## Post-Phase-1 Constitution Re-Check

| Principle | Status After Design |
|-----------|---------------------|
| I. Azure-First | ✅ Confirmed — design uses Entra ID, Microsoft Graph, Managed Identity, and `azuread`/`azurerm` providers exclusively. |
| II. API-First | ✅ Confirmed — `/whoami` and the five probe endpoints are documented in `contracts/`; the role-permission matrix is the binding contract; UI consumes the same contract. |
| III. Strong Domain Modeling | ⚪ N/A — no messaging-domain entities. Platform entities use consistent terminology across API, IaC, docs, and telemetry (verified in data-model.md §Naming Cross-Reference). |
| IV. Security by Default | ✅ Strengthened — app-role RBAC + no static credentials + no internal-trust bypass + minimal Graph permission. The 002 external-ingress deviation is unchanged and unrelated to this slice. |
| V. Operational Excellence | ✅ Confirmed — authz events route through the OTel pipeline to App Insights; the no-role failure mode is explicitly logged with caller `oid` and required role(s); no silent retries. |
| VI. Incremental Extensibility | ✅ Confirmed — operation classes and matrix form the stable contract; new workloads compose existing modules; Graph permissions are additive via the documented procedure; delegated Graph flows are supported by the abstraction but not enabled. |
| Modular Monolith First | ✅ Confirmed — no service decomposition. |
| Container-Native | ✅ Confirmed — all workloads remain containerized; `DefaultAzureCredential` works in containers via mounted `~/.azure` or via injected Managed Identity. |
| Async-First | ⚪ N/A — auth remains request-scoped synchronous. |
| CI/CD Requirements | ✅ Confirmed — existing CI gates apply; the gitleaks pass continues to enforce SC-007. |
| Testing Standards | ✅ Confirmed — unit + integration + contract + smoke E2E coverage planned. |
| AI Tooling / MCP Usage | ✅ Confirmed — implementation tasks will cite Microsoft Learn MCP (Microsoft.Identity.Web, Microsoft.Graph), context7 MCP (MSAL React), Next.js MCP (App Router MSAL provider mounting), shadcn/ui MCP (role-aware affordance primitives). |

**No new violations introduced by Phase 1 design. Plan is ready for `/speckit-tasks`.**
