# Research — Auth and Identity (Phase 0)

**Plan**: [plan.md](./plan.md) · **Spec**: [spec.md](./spec.md)

This document records the decisions made before implementation begins. Each section captures (1) the decision, (2) the rationale (with primary sources where they affect API surface), and (3) alternatives considered. All clarifications surfaced by `/speckit-clarify` are also recorded here as resolved.

The five clarifications resolved on 2026-05-19 are repeated up-front for reference:

| Clarification | Resolution |
|---|---|
| Frontend auth library | **MSAL** (`@azure/msal-browser` + `@azure/msal-react`); NextAuth is removed by this slice |
| Role delivery mechanism | **Entra ID app roles** on the backend API app registration, surfaced in the `roles` claim |
| Initial `BusTerminal.Admin` bootstrap | **Manual assignment** via Entra portal by a tenant admin; documented in operator runbook; **not** in OpenTofu state |
| Role boundary definition | **Role-permission matrix** of five operation classes × four roles, published as a binding contract |
| Initial Graph permissions | **`User.Read.All` (application) only** |

---

## 1. Frontend MSAL package selection and configuration shape

**Decision**: Use `@azure/msal-browser` 4.x and `@azure/msal-react` 3.x. Initialize a single `PublicClientApplication` instance at module scope in `web/lib/auth/msal-instance.ts` and pass it to `<MsalProvider instance={pca}>` at the App Router root layout. Use **Authorization Code + PKCE only** (the only flow MSAL supports for SPAs in current versions). Cache location: `sessionStorage` (per the MS-recommended default for SPAs in environments where cross-tab session sharing is not required). Interactive method: **redirect** (`loginRedirect` / `acquireTokenRedirect`) — not popup — because BusTerminal operators frequently run with strict popup blockers and the redirect flow is more reliable across browsers.

**Rationale**:
- MSAL React requires `MsalProvider` at the root and forbids calling interactive APIs (`loginRedirect`, `acquireTokenRedirect`, `loginPopup`, `acquireTokenPopup`, `handleRedirectPromise`, `ssoSilent`, `logout`) outside its context. ([MSAL React FAQ — Authentication](https://learn.microsoft.com/entra/msal/javascript/react/faq#authentication))
- The recommended token acquisition pattern is `acquireTokenSilent` first, falling back to `acquireTokenRedirect` only when silent fails. ([SPA: Acquire a token to call an API](https://learn.microsoft.com/entra/identity-platform/scenario-spa-acquire-token))
- A single `PublicClientApplication` instance must be reused across the page lifetime — MSAL specifically calls this out. ([MSAL React Getting Started](https://learn.microsoft.com/entra/msal/javascript/react/getting-started))
- For App Router specifically, `MsalProvider` must mount inside a **client component** boundary because it relies on React context. The root `app/layout.tsx` will mark a small `MsalProvider` wrapper as `"use client"`; the rest of the layout (and authenticated pages) stay as Server Components where possible. The authenticated user's identity reaches Server Components only via API calls (the backend's `/whoami`) because MSAL's session lives in browser storage.

**Alternatives considered**:
- `next-auth` v5 (already in 002): rejected because the source artifact explicitly prescribes MSAL and because NextAuth's session abstractions add a layer that obscures the underlying token flow — this slice is *about* making token flows explicit and standard.
- `@azure/msal-node` for server-side rendering with token-on-the-server: rejected. The operational complexity of brokering tokens through a Next.js Route Handler is not justified for a SPA-shaped product. The frontend remains a browser-side authenticated client.
- Popup mode (`loginPopup`): rejected as the default. Popup is supported by `@azure/msal-browser` but BusTerminal's operator users typically run hardened browsers and the redirect path is more predictable. (Popup may be revisited later if user testing prefers it.)

---

## 2. Backend role-claim handling and policy naming

**Decision**: Continue using `Microsoft.Identity.Web`'s `AddMicrosoftIdentityWebApi` (already wired in 002 by `AuthenticationExtensions.cs`). Add **five named authorization policies — one per operation class** (`CanRead`, `CanMutateDomain`, `CanOperatePlatform`, `CanAdminister`, `CanUseDeveloperTooling`) — built via `AddAuthorizationBuilder().AddPolicy(...).RequireRole(...)`. Endpoints declare *what they are* by attaching the operation-class policy via `.RequireAuthorization("CanMutateDomain")`. The roles → operation classes mapping (FR-009b) lives inside `RolePolicies.cs`, in one place, as a single source of truth.

**Rationale**:
- `Microsoft.Identity.Web` maps the `roles` claim from app-role-assignment tokens to `ClaimTypes.Role` automatically. `RequireRole("BusTerminal.Admin")` works without custom handlers. ([Implement RBAC in ASP.NET Core](https://learn.microsoft.com/entra/identity-platform/howto-implement-rbac-for-apps#implement-rbac-in-aspnet-core); [Microsoft.Identity.Web Authorization](https://learn.microsoft.com/entra/msidweb/authentication/authorization))
- Policies named **per operation class** (not per role) are stable across role-permission-matrix evolution. If the matrix is later loosened (e.g., Developer gains MutateDomain), only `RolePolicies.cs` changes — no endpoint is touched. Endpoints declare intent ("this mutates a domain resource"), not eligibility ("only Admin or Operator allowed"). This aligns with the spec's framing (FR-009a defines operation classes; FR-009b defines the mapping).
- The `RequireRole` overload accepts multiple roles ANDed via separate `.RequireRole` calls or ORed by passing a parameter array; the union semantics for multi-role users (FR-011) come for free because ASP.NET Core's `RequireRole` succeeds if the principal has *any* of the listed roles.

**Alternatives considered**:
- Policies named per role (`AdminOnly`, `OperatorOnly`, `ReaderOnly`): rejected. Forces endpoints to spell out the role union themselves and means every matrix change touches every endpoint.
- Custom `AuthorizationHandler` per operation class: rejected. The built-in `RequireRole` covers every case in the spec; a custom handler is overhead with no behavioral benefit.
- Putting the matrix in `appsettings.json`: rejected. The matrix is a *contract* — changing it is a spec change, not a config change. Code is the appropriate home; the inventory document (`contracts/role-permission-matrix.md`) is the human-readable mirror.

---

## 3. Microsoft.Graph SDK app-only client wiring

**Decision**: Use the `Microsoft.Graph` SDK v5 directly, wrapped by an in-house `IGraphClient` abstraction. The Graph SDK constructor takes an `Azure.Core.TokenCredential` — we pass a single `DefaultAzureCredential` instance produced by the new `AzureCredentialFactory`. **Do not** introduce `Microsoft.Identity.Web.GraphServiceClient`: it is convenient but adds an extra layer between our DI and the Graph SDK, and we already control credential acquisition via the factory. The abstraction (`IGraphClient`) is the sole entry point for Graph access in the codebase (FR-023).

**Rationale**:
- Microsoft's own tutorial for "Access Microsoft Graph from a secured .NET app as the app" uses `ChainedTokenCredential` / `ManagedIdentityCredential` / `EnvironmentCredential` passed directly into `GraphServiceClient`. The `Microsoft.Identity.Web.GraphServiceClient` helper is an *option*, not a requirement. ([Tutorial: Access Microsoft Graph from a secured .NET app as the app](https://learn.microsoft.com/azure/app-service/scenario-secure-app-access-microsoft-graph-as-app#call-microsoft-graph))
- App-only auth requires the application permission (`User.Read.All`) and admin consent — both wired in the IaC and documented in the runbook. ([Build .NET apps with Microsoft Graph and app-only authentication](https://learn.microsoft.com/graph/tutorials/dotnet-app-only-authentication))
- Keeping `IGraphClient` as the only entry point makes future permission additions trivial to audit (`grep "IGraphClient"` returns every Graph caller).

**Alternatives considered**:
- `Microsoft.Identity.Web.GraphServiceClient`: rejected for the reason above. Reconsider if a later slice needs delegated Graph calls *and* on-behalf-of token exchange, where Microsoft.Identity.Web's helper carries its weight.
- Direct `HttpClient` calls to the Graph REST endpoints: rejected. The SDK provides typed responses, paging, and retries — re-implementing is waste.
- Per-call credential acquisition: rejected. One `TokenCredential` instance is shared (it does its own caching internally per the Azure SDK token-credential contract).

---

## 4. `DefaultAzureCredential` vs explicit credential per environment

**Decision**: Use **`DefaultAzureCredential`** behind the `AzureCredentialFactory` abstraction. The factory exposes `TokenCredential CreateCredential(string? userAssignedClientId = null)`. In deployed environments it constructs a `DefaultAzureCredential` with `ManagedIdentityClientId` set to our workload identity client id. In `Development` it constructs a `DefaultAzureCredential` with defaults so `AzureCliCredential` / `VisualStudioCodeCredential` resolve the developer's identity.

**Rationale**:
- `DefaultAzureCredential` chains Environment → WorkloadIdentity → ManagedIdentity → VSCode → AzureCli → AzurePowerShell → DeveloperCli credentials — exactly the developer-friendly + production-ready chain we need (FR-017, FR-018). ([Credential chains in the Azure Identity library for .NET](https://learn.microsoft.com/dotnet/azure/sdk/authentication/credential-chains))
- MS Learn explicitly recommends *replacing* `DefaultAzureCredential` with a specific `TokenCredential` once an app is deployed, for debuggability and performance reasons. We honor that guidance via the factory's deployment-environment branch, which sets `ManagedIdentityClientId` so the chain short-circuits to managed identity in production-shaped environments — fast, deterministic, and traceable.
- **One** credential path for all SDKs (Graph today; Cosmos, AI Search, Key Vault, etc. tomorrow). FR-018's "same single mechanism for local and deployed environments" is satisfied by `AzureCredentialFactory` being that mechanism.

**Alternatives considered**:
- `ChainedTokenCredential` built explicitly per environment: viable but more code. The factory abstraction gives us the same control with less boilerplate.
- `ManagedIdentityCredential` only (deployed) + `AzureCliCredential` only (dev) selected by env var: rejected. It works but reduces the developer-environment flexibility — a developer signed in via VS Code (but not `az`) would fail unnecessarily.

---

## 5. Mock-tenant local development & role surfacing

**Decision**: Extend the existing `MockAuthenticationHandler` (002) to accept an `X-Mock-Roles` request header (comma-separated values) and project them into `ClaimTypes.Role` collection on the synthesized `ClaimsPrincipal`. The mock path remains gated to `IHostEnvironment.IsDevelopment()` exactly as it is today (the `DevelopmentTenantSentinel = "development"` check). Local frontends still sign in to the real dev tenant; the mock path is **backend-only** and is used by integration tests and by curl-style probing of the API without round-tripping MSAL.

**Rationale**:
- Eliminates the need for a real Entra round-trip in unit/integration tests while preserving production-equivalent code paths (claims → `ClaimsPrincipal` → role policy). The same `RequireAuthorization("CanMutateDomain")` line executes against both a mock and a real token.
- Headers (not query parameters) keep the mock parameter out of URLs and logs. The header is stripped in production by virtue of the handler not being registered when the tenant id is not the development sentinel.
- Why backend-only mock: MSAL cannot synthesize a token without an issuer. Asking developers to maintain a fake OIDC issuer locally is overkill; the dev tenant is cheap to sign in to.

**Alternatives considered**:
- Frontend mock provider (akin to 002's NextAuth `Credentials` provider for "Dev User"): rejected. Reintroduces a frontend code branch that diverges from production behavior — which is exactly the FR-018 anti-pattern.
- A separate `Microsoft.AspNetCore.Authentication.Test` package and synthetic JWT signing: rejected. Overkill for our needs and adds a key-management problem.

---

## 6. OpenTofu `azuread` provider — app role declarations and assignments

**Decision**:
- Pin `hashicorp/azuread` provider to **`~> 3.1`** (latest stable major as of 2026-05).
- Declare the four BusTerminal platform roles on the **API app registration** (`bt-dev-api`) using the **separate `azuread_application_app_role` resource** (one per role) rather than inline `app_role` blocks. The parent `azuread_application` carries a `lifecycle { ignore_changes = [app_role] }` so the two resources don't fight.
- Declare the Graph `User.Read.All` application permission using the modern `azuread_application_api_access` resource against the API app registration's MI (this is the **backend** that consumes Graph, not the web app registration).
- Assign workload identities (managed identities) to API app roles using `azuread_app_role_assignment`. **Do not** assign the initial `BusTerminal.Admin` to any user via Tofu (per Q3 — manual portal action).

**Rationale**:
- The separate `azuread_application_app_role` resource pattern is the provider's recommended approach for "manage app roles independently of the application" and is the only pattern that scales as the role list grows. ([azuread provider — `application_app_role`](https://github.com/hashicorp/terraform-provider-azuread/blob/main/docs/resources/application_app_role.md))
- `azuread_application_api_access` is the modern replacement for the legacy `required_resource_access` block on `azuread_application` and is friendlier to compose. ([azuread provider — `application_api_access`](https://github.com/hashicorp/terraform-provider-azuread/blob/main/docs/resources/application_api_access.md))
- `azuread_app_role_assignment` works equally well for users, groups, and managed identities (the assignment principal is identified by object id). ([azuread provider — `app_role_assignment`](https://github.com/hashicorp/terraform-provider-azuread/blob/main/docs/resources/app_role_assignment.md))
- **Why role definitions in Tofu but Admin assignment manual (Q3 clarification)**: the role *definitions* are reproducible infrastructure-as-code (every environment needs the same four roles). The *Admin* role *assignment* is a privileged grant best left as an explicit, audit-loggable tenant-admin action. Putting it in Tofu would couple platform-role ownership to whoever can run `tofu apply` — exactly what we want to avoid.

**Alternatives considered**:
- Inline `app_role` blocks on `azuread_application`: works but creates merge conflicts on shared app registrations and makes per-role lifecycle management (e.g., disabling a deprecated role) harder.
- Legacy `required_resource_access` blocks for Graph permissions: rejected. The new `azuread_application_api_access` resource is provider-preferred and reads more cleanly.
- Tofu-managed `BusTerminal.Admin` assignment to a configured object id at environment creation: rejected per Q3. Documented as a future option if multi-environment Admin churn becomes painful.

---

## 7. Federated identity credentials — GitHub OIDC

**Decision**: Use `azuread_application_federated_identity_credential` (stable resource, available in 3.x) to declare the GitHub OIDC trust on the pipeline managed identity. Reuse the existing `mi-busterminal-pipeline-dev` identity (introduced by 002). Subject claim pattern continues to be `repo:<org>/<repo>:ref:refs/heads/<branch>` for branch-protected deployments and `repo:<org>/<repo>:environment:<environment>` for environment-protected deployments. Both subjects are documented in `docs/identity-and-secrets.md`.

**Rationale**:
- The existing 002 pipeline already uses OIDC federation; this slice generalizes its provisioning into a reusable module (`iac/modules/federated-credential/`) rather than introducing a new identity primitive.
- The newer `azuread_application_flexible_federated_identity_credential` (with claims-matching expressions) is available but is not necessary for our straightforward `sub`-claim match. Sticking with the simpler resource keeps the module pinnable and the configuration self-documenting.

**Alternatives considered**:
- A user-managed certificate uploaded as an application credential: rejected. Reintroduces a secret-rotation problem federation eliminates.
- Per-repo or per-branch managed identities: rejected as over-segmentation. One pipeline MI per environment is the unit of trust isolation we need.

---

## 8. Initial Admin role bootstrap — operational procedure

**Decision** (records the Q3 clarification): The IaC defines the role *types* (`BusTerminal.Admin`, `.Operator`, `.Reader`, `.Developer`); a tenant administrator **manually** assigns the initial `BusTerminal.Admin` role to a designated founding operator's object id via the Entra portal. The procedure is captured in `docs/identity-role-administration.md` (new in this slice) and exercised in `quickstart.md`.

**Rationale**:
- Q3 — keeps the privileged grant auditable in Entra's directory logs; decouples platform-role ownership from infra-state ownership; one-time per environment.
- Once an Admin exists, the same Admin can grant the other roles via the portal or via a future automation surface — but **that surface is not in this slice**.

**Alternatives considered**:
- Tofu module that assigns Admin to a configured object id on environment creation: rejected for the reasons above. Documented as a future option.
- Bootstrap PowerShell/Bash script: rejected. Adds a code path that's used once per environment and rots between executions.

---

## 9. Graph admin-consent workflow

**Decision**: Declare the `User.Read.All` (application) permission in OpenTofu via `azuread_application_api_access`. Admin consent is **not** automated — it's a tenant-admin action performed once per environment via the Entra portal (or `az ad app permission admin-consent` for the CLI-savvy). The procedure is documented in `docs/identity-graph-permissions.md` (new in this slice). The `contracts/graph-permissions-inventory.md` file is the source-of-truth list — every permission ever granted must appear there with a rationale.

**Rationale**:
- Mirrors 002's approach to the API app registration's exposed scope: declare the requirement in IaC, perform the consent dance manually, document both.
- `User.Read.All` requires admin consent (it's an application permission). The Microsoft Graph permission reference confirms this. ([Microsoft Graph permissions reference — User.Read.All](https://learn.microsoft.com/graph/permissions-reference#userreadall))

**Alternatives considered**:
- `azuread_service_principal_delegated_permission_grant` for granting consent in IaC: it exists but applies only to delegated (user) consent, not application admin consent. Not applicable here.
- Calling `az ad app permission admin-consent` from a Tofu `null_resource` `local-exec`: rejected as anti-pattern (couples infra to local CLI installation, and admin-consent privilege is far broader than what Tofu should hold).

---

## 10. Secret-scan baseline refresh & verification path for SC-007

**Decision**: Re-run `gitleaks` (already configured by 002) on this slice's PR and verify zero credential-shaped findings. Add **deletion** of `web/lib/auth.ts` (which referenced `AZURE_AD_CLIENT_SECRET`) to the change set, then verify the gitleaks config does not need new allowlist rules (MSAL config keys like `clientId` are not credential-shaped and require no allowlist entry).

**Rationale**:
- `gitleaks` already exists in 002's CI pipeline as the SC-007 enforcement mechanism. No new tool is needed.
- Deleting `web/lib/auth.ts` removes the only file in the repository that referenced `AZURE_AD_CLIENT_SECRET`. Removing the env var from documentation and pipeline configuration completes the cleanup. The `gitleaks` re-run is the proof.

**Alternatives considered**:
- Adding `trufflehog` or `detect-secrets` as a second scanner: rejected. One scanner is enough for SC-007's "zero findings" gate; adding a second adds maintenance without reducing risk.

---

## Cross-Reference

| Spec Functional Requirement | Resolved by Section |
|---|---|
| FR-001, FR-002 (Entra-only, work accounts) | Inherited from 002; no change |
| FR-002a, FR-002b (Initial Admin bootstrap) | §8 |
| FR-003 (MSAL, PKCE, no implicit) | §1 |
| FR-004 (Frontend acquires API tokens) | §1 |
| FR-005 (Identity display + sign-out) | §1 |
| FR-006 (Role-aware UI) | §1, §2 |
| FR-007 (Backend token validation) | §2 (Microsoft.Identity.Web does this by default) |
| FR-008 (PlatformPrincipal) | §2 |
| FR-009, FR-009a, FR-009b, FR-009c (Roles + matrix + probes) | §2 |
| FR-010 (No silent default role) | §2 (policies all `.RequireRole` — absence fails) |
| FR-011 (Multi-role union) | §2 (`RequireRole` semantics) |
| FR-012 (No internal trust bypass) | §2 (same path for workload tokens) |
| FR-013–FR-016 (MI everywhere, no static creds) | §3, §4 |
| FR-017–FR-019 (Local dev credential chain) | §4, §5 |
| FR-020 (MSAL token acquisition) | §1 |
| FR-021 (Backend → Azure via credential chain) | §4 |
| FR-022 (Workload → API via MI) | §6 |
| FR-023 (Graph abstraction) | §3 |
| FR-024 (Graph permissions minimal + enumerated) | §3, §9 |
| FR-025 (Delegated Graph supported but unused) | §3 |
| FR-026–FR-028 (Identity via OpenTofu, no inline creds) | §6, §7 |
| FR-029, FR-030 (Pipeline federation, subject documentation) | §7 |
| FR-031–FR-034 (HTTPS, authz logging, no token contents, telemetry) | Inherited from 002 |
| SC-007 (Zero credential findings) | §10 |

All `NEEDS CLARIFICATION` items from the spec are resolved. Phase 1 design proceeds.
