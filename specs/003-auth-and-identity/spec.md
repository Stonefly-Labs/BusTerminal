# Feature Specification: Auth and Identity

**Feature Branch**: `feature/003-auth-and-identity`

**Created**: 2026-05-19

**Status**: Draft

**Input**: Source artifact: `speckit-artifacts/003-auth-and-identity.md`

---

## Overview

This slice elevates BusTerminal's identity posture from "any signed-in Entra ID user can call anything" (the authorization stance shipped in spec 002) to a coherent, role-aware, zero-trust identity foundation that every later domain slice will inherit. It standardizes how *humans*, *workloads*, *infrastructure*, and *automation* authenticate; defines the platform's authorization roles and how they are enforced; eliminates remaining static credentials; and lays the groundwork for future Microsoft Graph integration.

When this slice is complete, every later feature can assume: human identity, workload identity, authorization boundaries, token validation, and managed-identity access to Azure services are all already in place and consistent.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Platform owner can grant role-scoped access to BusTerminal (Priority: P1)

A platform owner needs to give a teammate access to BusTerminal at a specific privilege level — read-only for a stakeholder, operator for an SRE, full admin for a fellow owner, developer for someone exercising the public API surface. Today every authenticated user can call every endpoint; the platform owner has no way to express "this person can read but not change."

**Why this priority**: Without role-aware authorization, BusTerminal cannot be safely opened beyond its own engineering team. RBAC is the prerequisite for every domain capability that follows, because domain endpoints will need a role to bind their authorization checks against. This is the most consequential gap that 003 closes.

**Independent Test**: Assign two different test identities to two different roles. Sign in as each. Confirm that each identity can only invoke the API operations its role permits and is rejected (with a clear, auditable failure) from operations its role does not permit. Verifiable without any new domain endpoints — the same protected endpoints from spec 002 plus a small set of role-gated probe endpoints are sufficient.

**Acceptance Scenarios**:

1. **Given** a user assigned the read-only role, **When** they invoke a read-only operation, **Then** the request succeeds and the response identifies the caller's effective role.
2. **Given** a user assigned the read-only role, **When** they invoke an operation that requires the operator or admin role, **Then** the request is rejected with an authorization failure, the failure is logged with the caller's identity and the required role, and no state is changed.
3. **Given** a user assigned the admin role, **When** they invoke any role-gated operation, **Then** the request succeeds and the response indicates the caller's effective role.
4. **Given** a signed-in Entra ID user with *no* platform role assigned, **When** they invoke any role-gated operation, **Then** the request is rejected and the user is presented with a clear "you do not have access to BusTerminal" message in the UI and a structured authorization error from the API.
5. **Given** the UI renders for an authenticated user, **When** the user has the read-only role, **Then** UI affordances for state-changing operations are visually unavailable (disabled or hidden) rather than appearing actionable and failing on click.

---

### User Story 2 — Operators can deploy BusTerminal with no static Azure credentials anywhere in the system (Priority: P1)

An operator (or a security reviewer) audits a deployed BusTerminal environment. Every workload, every CI/CD pipeline step, and every Azure resource integration is expected to authenticate via managed identity or federated identity — no connection strings with secrets, no service principal passwords, no access keys, no SAS tokens stored in configuration. The platform must hold this property end-to-end, not just for the resources spec 002 happened to wire up.

**Why this priority**: Spec 002 shipped this property for the resources it provisioned (Key Vault, ACR pull, Container Apps execution identity, pipeline OIDC). Spec 003 is the slice that **standardizes the pattern** across every Azure resource BusTerminal will integrate with — Cosmos DB, AI Search, Storage, OpenAI, Service Bus, App Configuration — so that adding a new dependency in a later slice is a config and grant change, not a credential-management redesign. P1 because security debt accumulates fast once shortcuts ship.

**Independent Test**: Run a secret scanner across the repository, container images, pipeline configuration, and resource configuration. Zero credential-shaped findings. For each Azure service BusTerminal authenticates to, inspect the workload's authentication path and confirm a managed identity (workload, pipeline, or developer) is the principal — not a stored secret.

**Acceptance Scenarios**:

1. **Given** the deployed dev environment, **When** the workload reads from or writes to any integrated Azure service, **Then** authentication is performed via the workload's managed identity and the call succeeds without any client secret or access key being present in configuration.
2. **Given** the CI/CD pipeline, **When** it authenticates to Azure for any reason (Tofu plan/apply, image push, deployment, smoke tests), **Then** it uses workload identity federation and no client secret is present in any pipeline secret, repo variable, or environment variable.
3. **Given** a new Azure resource type is added to the platform later, **When** the engineer follows the documented pattern, **Then** the only credential-related work is: provision a managed identity (or reuse the workload identity), grant the appropriate RBAC role on the resource, and consume the resource via a credential-free SDK call.
4. **Given** the deployed environment, **When** a secret scanner runs against the repository and pipeline configuration, **Then** zero credential-shaped values are found.

---

### User Story 3 — Internal service-to-service calls are authenticated, even inside the private boundary (Priority: P2)

A platform engineer adds a new background worker that calls the BusTerminal API. The worker authenticates with its own managed identity, acquires a token for the API, and the API validates the token and authorizes the call on the same code path that human callers traverse. There is no "internal bypass" header, no shared secret, no anonymous trust based on network position.

**Why this priority**: BusTerminal's architecture will accumulate more workloads over time (Container Apps Jobs for discovery, containerized Azure Functions for event-driven processing — both explicitly enumerated in `CLAUDE.md`). If the first internal caller is allowed to bypass authentication "because it's inside the VNet," every subsequent caller will too, and the zero-trust posture is gone before it ships. P2 because no internal caller exists yet in the current slice — but the **pattern** must land here so that the first internal caller built on top of it inherits it for free.

**Independent Test**: Stand up a probe workload (Container App Job or scripted client) inside the environment that authenticates via its managed identity, acquires a token for the BusTerminal API audience, and successfully calls a protected endpoint. A second probe that does *not* present a valid token receives a 401 from the same endpoint.

**Acceptance Scenarios**:

1. **Given** a workload with a managed identity granted a role on the BusTerminal API, **When** the workload acquires a token for the API and calls a protected endpoint, **Then** the API validates the token, resolves the workload's role, and processes the call.
2. **Given** a workload calling the API without a valid token, **When** the request reaches the API, **Then** the API rejects it regardless of network position.
3. **Given** any internal caller, **When** it calls the API, **Then** the audit/log trail shows the caller's identity (object id and role) — not just a generic "internal" marker.

---

### User Story 4 — Developers can run the full stack locally against real Azure services without managing personal secrets (Priority: P2)

A developer working on a slice that depends on Cosmos DB, AI Search, Key Vault, or any other Azure dependency starts the local stack. The local backend, executing under the developer's own Entra ID identity (via Azure CLI / VS / VS Code sign-in), reads and writes Azure resources without anyone provisioning a personal client secret, copying a connection string, or pasting an access key into a `.env` file. Tokens for the local backend are acquired via the same identity provider as the deployed environment.

**Why this priority**: P2 because local development already works for the resources spec 002 wired up, but a coherent `DefaultAzureCredential` convention is needed before later slices start adding Cosmos / Search / OpenAI integrations and inventing per-developer credential workarounds. This is also the principal mechanism by which the "no static credentials" promise of US2 holds up *for developers*, not just for deployed environments.

**Independent Test**: A developer with Azure CLI sign-in on a clean machine clones the repo, runs the local startup command, and the backend successfully authenticates to a representative Azure dependency (e.g., Key Vault) using their personal identity. No `.env` file contains an Azure secret. No code path branches between "local" and "deployed" for credential acquisition.

**Acceptance Scenarios**:

1. **Given** a developer signed into Azure via CLI/VS/VS Code, **When** they start the local backend, **Then** the backend authenticates to Azure dependencies via the developer's identity and operations the developer's identity is authorized for succeed.
2. **Given** a developer is *not* signed into Azure, **When** they start the local backend against a dependency that requires Azure auth, **Then** the failure message clearly states which sign-in command will resolve the problem.
3. **Given** the codebase, **When** an engineer inspects how credentials are acquired for any Azure service, **Then** the acquisition path is the same single abstraction (the `DefaultAzureCredential` chain) regardless of environment.

---

### User Story 5 — CI/CD infrastructure modules encapsulate identity provisioning so adding a new environment or workload is configuration, not custom IAM (Priority: P3)

An infrastructure engineer adds a new environment (e.g., `test`), or a new workload type (e.g., a Container Apps Job). They reuse existing OpenTofu modules to provision the workload's managed identity, federated credentials (for pipeline access), and the RBAC role assignments the workload needs — without writing custom IAM resource blocks inline in the environment composition.

**Why this priority**: P3 because the foundation already has *some* identity provisioning in OpenTofu (the pipeline MI and federation in spec 002). This slice promotes that one-off into reusable modules so the same pattern works the second, third, and Nth time. Required for sustainable growth; not required to ship the first useful domain slice.

**Independent Test**: Add a new workload definition to the `dev` environment composition that depends only on the existing identity modules — no new `azurerm_*identity*` or `azurerm_role_assignment` resource blocks are authored inline. The new workload comes up with the correct managed identity and the correct RBAC, and the same module would work in a different environment with only variable changes.

**Acceptance Scenarios**:

1. **Given** the existing OpenTofu module set, **When** an engineer adds a new workload to an environment, **Then** the workload's identity and its required RBAC assignments are expressible using the existing modules.
2. **Given** an OpenTofu module that provisions identity resources, **When** it is instantiated against a different environment, **Then** the only inputs that change are environment-scoped variables.
3. **Given** the OpenTofu codebase, **When** an engineer searches for inline RBAC role assignments or federated credential definitions outside of identity modules, **Then** they find none (or each one is documented as an explicit, justified exception).

---

### User Story 6 — Microsoft Graph foundation is in place for future identity-aware capabilities (Priority: P3)

A future BusTerminal capability needs to resolve a user object id to a display name, or enumerate the members of a group to drive a recommendation, or look up an organizational metadata field. The Graph client foundation, app-only Graph permissions, and the workload's RBAC posture are already wired up — the future slice just calls into the Graph client abstraction. No identity, app-registration, or consent work is needed at that point.

**Why this priority**: P3 because no current capability *requires* Graph access. But the artifact explicitly lists future Graph-driven workflows (user/group lookup, RBAC automation, tenant metadata inspection). Pre-wiring the foundation here means future slices land cleanly; deferring would cause the next slice that needs Graph to re-open identity-architecture decisions.

**Independent Test**: The Graph client abstraction can resolve the calling user's own profile via app-only flow against a real tenant. The Graph permissions are visible in the backend app registration, are minimum-necessary, and are documented in the inventory.

**Acceptance Scenarios**:

1. **Given** the deployed backend's managed identity has been granted app-only Graph permissions, **When** code invokes the Graph client abstraction for a "resolve user" operation, **Then** the call succeeds via managed-identity-acquired token (no client secret).
2. **Given** the documentation, **When** an engineer wants to add a new Graph operation in a future slice, **Then** they can find: the Graph client abstraction's entry point, the existing granted permissions, and the documented procedure to request additional permissions.

---

### Edge Cases

- **Signed-in Entra ID user with no platform role**: User authenticates successfully but has not been granted any BusTerminal role. The platform MUST present a clear "no access" UI and refuse API calls — never silently grant a default role.
- **Token validation failure modes**: Expired token, wrong audience, wrong issuer, missing required claim, signature failure. All MUST produce structured authorization failures, MUST be logged without leaking the token itself, and MUST NOT be conflated with "user has insufficient role."
- **Role changes mid-session**: A user's role assignment is changed in Entra ID. The platform MUST converge to the new role within a documented token-lifetime window; it MUST NOT cache role assignments beyond the token's natural lifetime in a way that would defeat the change.
- **Multiple roles on one user**: A user assigned both reader and operator. The platform MUST compute effective permissions as the union, with the most-permissive role determining what operations are allowed.
- **Managed identity not yet propagated**: A workload is provisioned, but its RBAC role assignment on a downstream resource has not yet propagated through Entra ID (typical 1–3 minute window). The platform MUST surface a clear, recoverable error and retry conservatively rather than masking it.
- **Local developer authenticated to wrong tenant**: Developer's `az login` is against a tenant other than the BusTerminal Entra tenant. Failure MUST identify the tenant mismatch explicitly so the developer can switch contexts.
- **CI/CD federated credential drift**: The GitHub OIDC subject claim (branch, environment, ref) no longer matches the federated credential's accepted subject. Pipeline failure MUST clearly identify the mismatch and the expected subject pattern.
- **Graph permissions revoked at the tenant level**: Tenant admin removes consented Graph permissions. Graph operations MUST fail visibly and the operational dashboard MUST surface the failure — not retry silently.

---

## Requirements *(mandatory)*

### Functional Requirements

#### Identity Provider & Account Types

- **FR-001**: The platform MUST use Microsoft Entra ID as the sole identity provider for human authentication.
- **FR-002**: The platform MUST accept only organizational Microsoft accounts. Personal Microsoft accounts, social identity providers, local user databases, and username/password mechanisms MUST NOT be supported.

#### Frontend Authentication

- **FR-003**: The frontend MUST authenticate users via Microsoft Entra ID using the Authorization Code flow with PKCE. The implicit flow MUST NOT be used.
- **FR-004**: The frontend MUST acquire access tokens for the BusTerminal backend API on demand and attach them as bearer credentials to API requests.
- **FR-005**: The frontend MUST surface the signed-in user's identity (display name and account) and provide an explicit sign-out affordance.
- **FR-006**: The frontend MUST render role-aware UI: affordances for operations the current user is not authorized to perform MUST NOT appear as actionable controls.

#### Backend Authentication & Authorization

- **FR-007**: The backend API MUST validate every inbound request's bearer token: issuer, audience, signature, expiry, and required claims. Validation failures MUST produce a structured authorization error and MUST be logged with the failure reason (never the token itself).
- **FR-008**: The backend MUST normalize incoming token claims into a single internal "platform principal" representation that downstream code uses for authorization decisions, audit logging, and Graph-related lookups.
- **FR-009**: The backend MUST enforce role-based authorization on every protected operation. The four initial platform roles are: **BusTerminal.Admin** (full administrative access), **BusTerminal.Operator** (operational management access), **BusTerminal.Reader** (read-only access), **BusTerminal.Developer** (API/spec/developer-tooling access).
- **FR-010**: The backend MUST reject role-gated requests from authenticated users who hold no applicable platform role. Lack of any role MUST NOT be treated as a default "reader" assignment.
- **FR-011**: When a user holds multiple platform roles, the backend MUST compute effective permissions as the union (most permissive role wins for any given operation).
- **FR-012**: The backend MUST validate tokens from internal workload callers identically to tokens from human callers. There MUST NOT be a "trusted internal" bypass based on network position, source header, or shared secret.

#### Managed Identity & Azure Service Authentication

- **FR-013**: Every Azure-hosted BusTerminal workload MUST authenticate to Azure services via managed identity.
- **FR-014**: The platform MUST use user-assigned managed identities by default; system-assigned managed identities are permitted only when user-assigned is infeasible.
- **FR-015**: The following Azure services MUST be accessed exclusively via managed identity: Cosmos DB, Azure AI Search, Key Vault, Azure Storage, Azure OpenAI, Azure Service Bus, App Configuration. Log Analytics and Application Insights MUST be accessed via Entra ID (managed identity or workload identity).
- **FR-016**: No Azure service connection string, account key, SAS token, or service principal client secret MUST be present in source code, configuration files, container images, or pipeline configuration.

#### Local Development Authentication

- **FR-017**: Local backend development MUST acquire Azure credentials via the standard credential chain (Azure CLI, Visual Studio, VS Code, or interactive developer login), with no per-developer secret provisioning required.
- **FR-018**: The credential-acquisition abstraction MUST be the same single mechanism for local and deployed environments. Environment-specific credential branches in application code MUST NOT exist.
- **FR-019**: When credential acquisition fails locally, the error MUST clearly identify which sign-in command resolves the problem and which tenant the developer must be signed into.

#### Token Acquisition Patterns

- **FR-020**: Frontend-to-backend token acquisition MUST use the frontend's user-context authentication library.
- **FR-021**: Backend-to-Azure-service token acquisition MUST use the credential chain abstraction.
- **FR-022**: Backend-to-internal-API token acquisition MUST use the calling workload's managed identity.

#### Microsoft Graph Foundation

- **FR-023**: The backend MUST include a Graph client abstraction capable of executing app-only Graph operations using the workload's managed identity. The abstraction MUST be the sole entry point for Graph access in the codebase.
- **FR-024**: Graph permissions granted to the backend app registration MUST be the minimum required for currently-planned operations and MUST be enumerated in the documentation deliverables.
- **FR-025**: Delegated Graph flows MUST be supported by the abstraction but MUST NOT be enabled or consented to in this slice unless a specific operation in this slice requires them.

#### Infrastructure as Code

- **FR-026**: All identity-related Azure resources (app registrations metadata where possible, managed identities, federated credentials, RBAC role assignments, Key Vault access policies/RBAC) MUST be provisioned and managed via OpenTofu modules.
- **FR-027**: OpenTofu modules MUST encapsulate identity provisioning so that adding a new workload or environment does not require authoring inline RBAC or federated-credential resource blocks.
- **FR-028**: Inline credential definitions in OpenTofu (literal secrets, embedded keys) MUST NOT exist.

#### CI/CD Authentication

- **FR-029**: CI/CD pipelines MUST authenticate to Azure via workload identity federation (GitHub OIDC or Azure DevOps workload federation, depending on the platform in use). Stored secrets and publish profiles MUST NOT be used.
- **FR-030**: Federated credential subject claims (branch, environment, ref) MUST be explicit and documented; pipelines failing due to federation drift MUST surface the expected subject pattern in their error output.

#### Security & Observability

- **FR-031**: All endpoints (frontend and backend) MUST require HTTPS. Plaintext HTTP MUST be refused.
- **FR-032**: Authentication and authorization events MUST be logged, including: sign-in successes and failures, token validation failures, authorization failures (with required role and caller's effective roles), and any detected privilege-escalation attempts.
- **FR-033**: Token contents (raw JWTs, refresh tokens, ID tokens) MUST NOT be logged in any form.
- **FR-034**: Authorization failures MUST be visible in the operational telemetry store (the solution's Log Analytics workspace) within the documented telemetry latency window.

### Key Entities

- **Platform Principal**: The normalized internal representation of an authenticated caller. Carries: caller object id, tenant id, caller type (human / workload), display name (for humans), effective platform roles, raw claims (for diagnostic use), and the correlation id of the originating request. Single source of truth for authorization decisions and audit logging.
- **Platform Role**: One of the four enumerated platform-level roles (Admin, Operator, Reader, Developer). Granted to a principal via Entra ID and surfaced in the access token in a documented claim location. Drives authorization checks and role-aware UI rendering.
- **Workload Identity**: A managed identity (user-assigned by default) attached to a BusTerminal-hosted workload — backend API, Container Apps Job, Container Apps Function, or pipeline runner. Holder of RBAC grants on downstream Azure resources and of platform-role assignments for internal API calls.
- **Federated Credential**: The trust relationship between a pipeline or external identity provider (e.g., GitHub OIDC issuer) and an Entra ID identity (typically the pipeline managed identity). Defined by a subject pattern that must match the OIDC token's `sub` claim at request time.
- **Graph Permission Grant**: A Microsoft Graph application permission granted (and admin-consented) to the backend app registration. Enumerated in the permissions inventory; consumed exclusively via the Graph client abstraction.
- **App Registration**: The Entra ID app registration backing the platform — one for the backend API (exposes scopes and role definitions) and one for the frontend client (registers the SPA/web client and redirect URIs). Identity for the platform itself.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A platform owner can grant a teammate one of the four platform roles end-to-end (assignment in Entra ID through visible effect in the BusTerminal UI) in **under 5 minutes**, following documentation alone, without engineering assistance.
- **SC-002**: For every Azure service BusTerminal integrates with in the deployed environment, **100%** of authentication uses managed identity (workload, pipeline, or developer). **Zero** connection strings, account keys, SAS tokens, or service principal client secrets exist in the repository, container images, pipeline configuration, or runtime configuration.
- **SC-003**: An internal workload (probe Container Apps Job) can authenticate to the BusTerminal API via its managed identity, present the resulting token, be authorized for its role, and successfully invoke a protected endpoint — verifiable in **a single end-to-end test** that does not rely on any shared secret.
- **SC-004**: A new developer, following the local-development documentation alone, can start the local stack and execute a real-Azure-dependent operation against the dev environment's Key Vault using **only** their Azure CLI sign-in — no per-developer secret provisioning, no `.env` credential editing.
- **SC-005**: Adding a new BusTerminal workload (a new API, job, or function) to the existing OpenTofu environment composition requires **no new inline IAM resource blocks** — only invocations of existing identity modules with new inputs.
- **SC-006**: An authorization failure (wrong role, missing role, expired token) for any protected operation appears in the centralized telemetry store, queryable by caller identity and correlation id, within the **documented telemetry latency window** (consistent with spec 002's observability commitments).
- **SC-007**: A secret-scanning tool run against the repository at any point during or after this slice's implementation produces **zero credential-shaped findings**.
- **SC-008**: A signed-in Entra ID user with no assigned platform role sees a clear "no access" experience in the UI **within 2 seconds** of completing sign-in, with **no** silent default-role assignment.
- **SC-009**: The backend's Graph client abstraction can resolve the calling user's own profile (an app-only operation) against the dev tenant on the **first invocation after deployment**, with no manual consent or credential step required at runtime.

---

## Assumptions

- **MSAL replaces any interim frontend auth library shipped in spec 002**: Spec 002's "platform-status page successfully calls the backend's `whoami` endpoint and renders the user's identity" implied a working frontend sign-in flow. The 003 source artifact prescribes MSAL (`@azure/msal-browser`, `@azure/msal-react`) as the standardized frontend authentication library. This slice consolidates onto MSAL even if 002 used a different library (e.g., NextAuth). If an alternative is preferred, it must be raised before `/speckit-plan` (see Clarifications below).
- **Role assignments are granted via Entra ID app roles defined on the backend API app registration**: This is the most common Microsoft-native pattern and integrates cleanly with `Microsoft.Identity.Web`'s role-claim handling. Group-claim mapping is a documented alternative for future use but is not the default for this slice.
- **Local developer identities will be granted appropriate platform roles in the dev Entra environment**: Developers' personal Entra accounts must hold at least the `BusTerminal.Developer` role to exercise role-gated endpoints locally against deployed dev resources. Role-assignment procedure is part of the documentation deliverables.
- **The existing `mi-bt-dev-workload` user-assigned managed identity is the workload identity onto which downstream RBAC grants will be added**: New resource integrations (Cosmos, Search, etc.) will grant data-plane roles to this identity rather than introducing a per-resource workload identity. Per-workload identities may be introduced later for blast-radius reduction; not required now.
- **The existing pipeline managed identity (`mi-busterminal-pipeline-dev`) and its GitHub OIDC federation are the basis for all CI/CD Azure authentication in this slice**: New pipeline operations layer onto this identity; no new federated identities are introduced unless explicitly justified.
- **Microsoft Graph integration is foundational only in this slice**: No user-visible Graph-driven feature ships. The abstraction, the app-only permissions inventory, and a single self-resolving smoke operation are sufficient to declare the foundation complete. Specific Graph-powered capabilities ship in later slices that explicitly require them.
- **Authorization happens at the API; UI role-awareness is presentational only**: Hidden or disabled UI controls are a UX affordance, never a security boundary. The backend remains the sole authority on what each principal may do.
- **Personal Microsoft accounts, social IDPs, and local accounts remain explicitly out of scope**, consistent with the source artifact.
- **No domain-specific authorization (per-resource ownership, per-namespace ACLs, per-environment scoping) ships in this slice**: That work belongs to the domain slices that introduce those resources. This slice provides the platform-role mechanism; it does not pre-design domain-resource authorization.

---

## Out of Scope

- Per-resource (per-namespace, per-queue, per-topic) authorization — deferred to domain slices that introduce those resources.
- Multi-tenant SaaS architecture or tenant-isolation primitives (Constitution Non-Goals).
- User self-service registration, password management, or any local credential storage.
- Delegated Microsoft Graph flows that require admin-consented permissions beyond what app-only foundation operations require.
- A CLI authentication experience (listed as a secondary/future goal in the artifact; not in MVP scope).
- Custom claims providers, token-exchange flows, or third-party IDP federation.
- Service principal client-secret-based authentication for any purpose.

---

## Dependencies

- **Spec 002 (Solution Foundation)** is a hard prerequisite: this slice extends the Entra ID authentication, `whoami` round-trip, Key Vault MI access, ACR MI pull, pipeline OIDC federation, and OpenTofu module structure that 002 delivered. The platform's two Entra app registrations (`bt-dev-api`, `bt-dev-web`) and the workload/pipeline managed identities described in `project_dev_environment.md` are the substrate this slice builds on.
- **Spec 001 (Brand System and Design Foundation)** is a soft prerequisite for the role-aware UI affordances (US1, FR-006) — the design tokens, primitives, and dark-mode-first patterns from 001 are consumed by the role-gated UI states. No new design system work is in scope here.
- **Microsoft Entra ID tenant administrator access** is required to grant app-only Graph permissions and to assign initial platform roles (BusTerminal.Admin) to founding operators. This is an operational dependency, not a code dependency.

---

## Clarifications

The following items are flagged for `/speckit-clarify` before `/speckit-plan`:

- [NEEDS CLARIFICATION: Does spec 002 currently use MSAL on the frontend, or did it ship with NextAuth (or another library)? The 003 artifact prescribes MSAL, but the existing dev environment notes reference a `NextAuthSecret` — confirming this drives whether 003 is a "configure MSAL" task or a "replace NextAuth with MSAL" task.]
- [NEEDS CLARIFICATION: Are the four platform roles delivered as Entra ID **app roles** (defined on the backend API app registration, surfaced in the `roles` claim) or as **Entra ID security groups** (surfaced in a `groups` claim and mapped to roles inside the API)? App roles is the default assumption; confirm before plan.]
- [NEEDS CLARIFICATION: What is the policy for granting the initial `BusTerminal.Admin` role? Manual assignment by tenant admin only, or is there a documented procedure (e.g., a bootstrap script invoked once per environment)?]
