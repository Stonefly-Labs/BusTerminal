# Data Model — Auth and Identity (Phase 1)

**Plan**: [plan.md](./plan.md) · **Spec**: [spec.md](./spec.md) · **Research**: [research.md](./research.md)

This document captures the logical model for the platform-level identity constructs introduced (or formalized) by slice 003. **No messaging-domain entities are introduced.** The model is logical — concrete persistence is not part of this slice because no entity is persisted to BusTerminal-owned storage; every entity below is either an in-process value type (`PlatformPrincipal`, `OperationClass`), a reference to a Microsoft Entra ID object (`Workload Identity`, `Federated Credential`, `App Registration`, `Graph Permission Grant`), or a published contract (`Platform Role`, the role-permission matrix).

Naming is consistent across API, IaC, docs, and telemetry (Constitution Principle III).

---

## Entity Catalog

### 1. Platform Principal *(in-process value type, request-scoped)*

The normalized internal representation of an authenticated caller after token validation. Source of truth for every authorization decision and every audit log entry in the request pipeline. FR-008.

| Field | Type | Source | Notes |
|---|---|---|---|
| `ObjectId` | `Guid` | `oid` token claim | The caller's Entra ID object id. Stable across renames. |
| `TenantId` | `Guid` | `tid` token claim | The Entra tenant the caller authenticated against. |
| `CallerType` | `enum { Human, Workload }` | derived | `Workload` when `idtyp == "app"` or `aud` is `api://<api-app-id>` and `roles` claim contains an application-permission role; `Human` otherwise. |
| `DisplayName` | `string?` | `name` token claim (humans only) | Null for workloads. **Not** sent to telemetry by default (FR-033 / privacy). |
| `Username` | `string?` | `preferred_username` token claim (humans only) | UPN/email-shape. Null for workloads. **Not** in telemetry. |
| `EffectiveRoles` | `IReadOnlySet<PlatformRole>` | `roles` token claim | Parsed from the `roles` claim. Empty set is valid and indicates "signed in but no platform role" (FR-010). |
| `RawClaims` | `IReadOnlyDictionary<string,string[]>` | full token | Available for diagnostic logging only; never returned in API responses. |
| `CorrelationId` | `string` | `traceparent` request header | The W3C Trace Context correlation id flowing from the calling client. Used for joining auth events to the request trace. |

**Lifecycle**: constructed once per request by `PrincipalAccessor` after authentication, exposed via `IPlatformPrincipalAccessor`, disposed at end of request scope.

**Validation rules**:
- `ObjectId` and `TenantId` MUST be valid GUIDs (token validation already enforces this).
- If `CallerType == Human`, `DisplayName` and `Username` SHOULD be populated (Entra always emits them for org accounts in our tenant; missing values are logged as warnings but not fatal).
- `EffectiveRoles` is parsed strictly: unknown values in the `roles` claim are **dropped silently** at parse time and a single structured log line is written ("unknown role rejected"). This protects against future role-name churn breaking the API.

**State transitions**: none — the type is immutable per request.

**Relationships**:
- `EffectiveRoles` references `Platform Role` (below).
- `CallerType.Workload` instances correlate to a `Workload Identity` (below) by `ObjectId` (the workload MI's object id is the principal's `ObjectId`).

---

### 2. Platform Role *(published enum + Entra app role)*

One of the four enumerated platform-level roles. Implemented as Entra ID **app roles** on the API app registration (`bt-dev-api`). Surfaced in the access token's `roles` claim. Drives all role-aware authorization (FR-009) and role-aware UI (FR-006).

| Role | Value (`roles` claim) | Display Name (Entra) | Description |
|---|---|---|---|
| `Admin` | `BusTerminal.Admin` | BusTerminal Administrator | Full administrative access. Includes everything Operator, Reader, and Developer can do, plus `Administer`-class operations. |
| `Operator` | `BusTerminal.Operator` | BusTerminal Operator | Operational management access. Read + Mutate-Domain + Operate-Platform. |
| `Reader` | `BusTerminal.Reader` | BusTerminal Reader | Read-only access to platform and domain state. |
| `Developer` | `BusTerminal.Developer` | BusTerminal Developer | API/spec/developer-tooling access. Read + Developer-Tooling. |

**Allowed member types** (on each app role): `User, Application`. This makes each role assignable to a *user* (humans) **and** to a *service principal* (workload managed identities) — required by FR-022 (workload-to-API calls).

**Validation rules**:
- The four `value` strings above are stable and case-sensitive. Renaming requires a backwards-compatibility plan and an ADR (every assigned role would need to be reassigned).
- Role assignment to humans and to workloads is performed via `azuread_app_role_assignment` (workloads, via IaC) or via the Entra portal (humans, manual — FR-002a). The *initial* `BusTerminal.Admin` assignment is **not** in IaC (Q3).

**State transitions**: role *definitions* are versioned-as-code in IaC; role *assignments* are managed-in-place in Entra and observed only via audit. Removing a role definition is a breaking change to existing assignments and requires migration.

**Relationships**:
- Each role maps to one or more `Operation Class` values per the role-permission matrix (below).
- Assignments to humans/workloads connect a `Platform Role` to either an Entra user object id or a `Workload Identity`.

---

### 3. Operation Class *(published enum + authorization policy)*

One of the five enumerated operation classes that every protected operation is assigned to. FR-009a. The class is the *what* of an operation; the role is the *who*. Endpoints declare their operation class; the role-permission matrix translates class → required role(s).

| Operation Class | Policy Name | Description | Probe Endpoint (FR-009c) |
|---|---|---|---|
| `Read` | `CanRead` | GET-style queries against domain or platform state. | `GET /probe/read` |
| `MutateDomain` | `CanMutateDomain` | Create/update/delete of domain resources (introduced by later slices). | `POST /probe/mutate-domain` |
| `OperatePlatform` | `CanOperatePlatform` | Operational actions: trigger discovery, clear caches, retry failed jobs, etc. | `POST /probe/operate` |
| `Administer` | `CanAdminister` | Platform-wide configuration, role-assignment-adjacent operations, environment-level settings. | `POST /probe/administer` |
| `DeveloperTooling` | `CanUseDeveloperTooling` | OpenAPI surface, API explorer, developer diagnostic endpoints not gated as Read. | `GET /probe/developer` |

**Validation rules**:
- Every protected endpoint MUST be tagged with exactly one operation class via `.RequireAuthorization("CanXxx")`. Endpoints without an operation-class policy are caught by the default policy (`RequireAuthenticatedUser`) and produce an authorization warning event in telemetry (FR-032) until they are properly tagged.
- The role-permission matrix is the single source of truth for which roles satisfy which policy. It is captured in code in `RolePolicies.cs` and mirrored in `contracts/role-permission-matrix.md`.

**State transitions**: operation classes are stable; adding a new class is a spec change.

**Relationships**:
- Each `Operation Class` policy resolves a *set* of `Platform Role` values that satisfy it (the role-permission matrix). Specifically:
  - `Read` ← Reader, Developer, Operator, Admin
  - `MutateDomain` ← Operator, Admin
  - `OperatePlatform` ← Operator, Admin
  - `Administer` ← Admin
  - `DeveloperTooling` ← Developer, Admin

---

### 4. Workload Identity *(Entra ID managed identity, IaC-managed)*

A managed identity attached to a BusTerminal-hosted workload. Holder of (a) RBAC grants on downstream Azure resources and (b) Entra **app role assignments** that authorize the workload to call the BusTerminal API. FR-013, FR-014, FR-022.

| Field | Type | Source | Notes |
|---|---|---|---|
| `Name` | `string` | IaC | Convention: `mi-bt-<env>-<workload>` (e.g., `mi-bt-dev-workload`, `mi-bt-dev-pipeline`). |
| `Kind` | `enum { UserAssigned, SystemAssigned }` | IaC | **`UserAssigned` by default** (FR-014). `SystemAssigned` permitted only when user-assigned is infeasible. |
| `PrincipalId` | `Guid` | Azure | The MI's service-principal object id; the value carried as `Platform Principal.ObjectId` when the MI calls the API. |
| `ClientId` | `Guid` | Azure | The MI's application id; consumed by `DefaultAzureCredential` via `ManagedIdentityClientId` to disambiguate when multiple MIs are attached. |
| `Environment` | `string` | IaC | `dev` / `test` / `prod`. |
| `Workload` | `string` | IaC | Free-form workload label (e.g., `api`, `pipeline`, `discovery-job`, `event-fn-x`). |
| `AssignedAzureRoles` | list of `AzureRbacAssignment` | IaC | `(scope, role definition)` tuples. Includes data-plane roles on Cosmos / Search / Key Vault / Storage / Service Bus / OpenAI / App Config as those resources are introduced by later slices. |
| `AssignedApiAppRoles` | list of `PlatformRole` | IaC | The `BusTerminal.*` app roles this workload holds on `bt-dev-api`'s service principal (used by `azuread_app_role_assignment`). |

**Lifecycle**: created by `iac/modules/workload-identity/`; survives independent of the workload container's lifecycle (Container Apps revisions come and go; the MI does not).

**Validation rules**:
- `Name` MUST match the convention; CI checks the regex `^mi-bt-(dev|test|prod)-[a-z0-9-]+$`.
- A workload MUST hold at least one of `BusTerminal.Reader / Developer / Operator / Admin` if it calls the API; otherwise its calls are rejected with no silent-default-role assignment (FR-010).
- A workload MUST NOT carry a client secret (FR-016). Token acquisition uses Managed Identity, period.

**Relationships**:
- 1:N with `AzureRbacAssignment` (data-plane RBAC on downstream services).
- 1:N with `Federated Credential` (only for the pipeline MI, where GitHub OIDC federation is in play).
- 1:N with `PlatformRole` (via `azuread_app_role_assignment` to the API app registration's service principal).

---

### 5. Federated Credential *(Entra ID federated identity credential, IaC-managed)*

The trust relationship between an external identity provider (GitHub OIDC) and an Entra ID identity (the pipeline managed identity). Defined by an `issuer`, `audience`, and `subject` pattern that the inbound OIDC token's claims must match. FR-029, FR-030.

| Field | Type | Source | Notes |
|---|---|---|---|
| `ParentIdentity` | reference to `Workload Identity` | IaC | The MI that this credential federates *to*. In this slice: the pipeline MI. |
| `Issuer` | `string` | IaC | Fixed: `https://token.actions.githubusercontent.com` (for GitHub Actions). |
| `Audience` | `string` | IaC | Fixed: `api://AzureADTokenExchange` (Entra's required audience for federation). |
| `Subject` | `string` | IaC | The federation subject pattern. Two flavors used here: `repo:<org>/<repo>:ref:refs/heads/<branch>` and `repo:<org>/<repo>:environment:<env>`. |
| `Name` | `string` | IaC | Per-credential display name, used by Entra and shown in failure messages (FR-030). |
| `Description` | `string` | IaC | Human-readable description recorded in Entra. |

**Validation rules**:
- The `Subject` value MUST appear verbatim in `docs/identity-and-secrets.md` so pipeline failures reproducing a federation-drift symptom can be diagnosed in one step (FR-030).
- A federated credential MUST NOT use a wildcard `*` in the subject without an ADR-recorded justification — overly broad subjects expand the trust surface beyond a single repo/branch/environment.

**State transitions**: federated credentials are versioned-as-code. Adding a new credential is an additive IaC change; removing one is breaking (the dependent pipeline stops authenticating).

**Relationships**:
- N:1 with `Workload Identity` (one MI can carry multiple federated credentials for different subjects).

---

### 6. Graph Permission Grant *(Microsoft Graph application permission, IaC-declared + admin-consented)*

A Microsoft Graph application permission granted to the backend API app registration and consumed via the `IGraphClient` abstraction. FR-023, FR-024.

| Field | Type | Source | Notes |
|---|---|---|---|
| `Permission` | `string` | IaC | Graph permission value (e.g., `User.Read.All`). |
| `Type` | `enum { Application, Delegated }` | IaC | This slice grants only `Application` permissions (FR-025 — delegated supported by the abstraction but not enabled). |
| `RequiresAdminConsent` | `bool` | Graph reference | `User.Read.All` is `true`. Procedure documented in `docs/identity-graph-permissions.md`. |
| `GrantedTo` | reference to `App Registration` | IaC | The backend API app registration. |
| `Rationale` | `string` | inventory document | Why the permission is needed. Required field in `contracts/graph-permissions-inventory.md`. |

**Validation rules**:
- Every permission ever granted MUST appear in `contracts/graph-permissions-inventory.md` with a non-empty `Rationale`. New permissions added by future slices must update the inventory in the same PR.
- Application permissions MUST follow least-privilege — the spec's `User.Read.All`-only stance is the foundation; later slices that need more must justify each addition.

**State transitions**: a permission is grantable from the moment it is declared in IaC; it is *active* only after admin consent. The inventory document tracks consent state per environment.

**Relationships**:
- N:1 with `App Registration` (the backend API app registration is the single grant target in this slice).

---

### 7. App Registration *(Entra ID app registration, IaC-managed where possible)*

The Entra ID app registration backing a platform component. BusTerminal has two: the **backend API** (`bt-dev-api`) and the **frontend web** (`bt-dev-web`).

| Field | Type | Notes |
|---|---|---|
| `Name` | `string` | Convention: `bt-<env>-{api|web}`. |
| `Purpose` | `enum { Api, Web }` | The API app registration exposes scopes and app roles; the Web app registration is the SPA client. |
| `AppId` | `Guid` | The application (client) id. Used by MSAL config (`auth.clientId`) on the Web side and by `Microsoft.Identity.Web` (`AzureAd:ClientId`) on the API side. |
| `ObjectId` | `Guid` | The application's object id; required for IaC resource references. |
| `IdentifierUris` | list of `string` | The API's exposed audience. Convention: `api://<api-app-id>`. |
| `AppRoles` | list of `PlatformRole` | (API only) The four `BusTerminal.*` roles declared as `azuread_application_app_role` resources. |
| `Oauth2PermissionScopes` | list of `string` | (API only) The delegated scopes exposed to the Web client. Inherited from 002 (the `whoami` scope). |
| `RedirectUris` | list of `Uri` | (Web only) The MSAL redirect URIs (per environment). |
| `RequiredApiAccess` | list of `GraphPermissionGrant` | (API only) `User.Read.All` (application) on Microsoft Graph. |

**Lifecycle**: the app registrations themselves were created by slice 002; this slice extends them — by adding app roles and Graph permission requirements (API side) and by adding MSAL redirect URIs (Web side). Their *creation* is not re-litigated here; their *modification* is fully IaC-managed.

**Validation rules**:
- App role list MUST contain the four `BusTerminal.*` roles; removing any role is a breaking change.
- Redirect URIs MUST use HTTPS in any non-development environment (FR-031). `http://localhost:*` is permitted only in `dev` for the local Next.js dev server.
- `IdentifierUris` MUST resolve consistently across local and deployed environments so the same scope (`api://<api-app-id>/.default`) works in both.

**State transitions**: changes are additive-friendly. The IaC `lifecycle { ignore_changes = [app_role] }` on the API `azuread_application` resource preserves the ability to manage roles via separate resources without state thrash.

**Relationships**:
- 1:1 with the underlying `Workload Identity` (a service principal is automatically created per app registration; the API's SP is what `azuread_app_role_assignment` targets).
- 1:N with `Graph Permission Grant` (only the API side carries Graph grants in this slice).

---

## Naming Cross-Reference *(Constitution Principle III)*

| Concept | API (.NET) | IaC (HCL) | Docs / Telemetry |
|---|---|---|---|
| Platform Principal | `PlatformPrincipal` | — (constructed in-process) | `platform_principal.*` log fields |
| Platform Role | `PlatformRole.{Admin,Operator,Reader,Developer}` | `local.platform_roles` map (4 entries) | `BusTerminal.{Admin,Operator,Reader,Developer}` |
| Operation Class | `OperationClass.{Read,MutateDomain,OperatePlatform,Administer,DeveloperTooling}` | — | `operation_class.{read,mutate-domain,operate-platform,administer,developer-tooling}` |
| Workload Identity | `WorkloadIdentityConfig` (DI) | `module.workload_identity` | `workload_identity.name` |
| Federated Credential | — | `module.federated_credential` | `federated_credential.subject` |
| Graph Permission Grant | `IGraphClient` (consumes; doesn't model the grant directly) | `module.graph_permissions` | `graph_permission.value` |
| App Registration | `AzureAd:ClientId` config binding | `azuread_application.{api,web}` | `app_registration.purpose` |

Synonym drift between layers is a defect (Constitution Principle III). The role *value* string `BusTerminal.Admin` appears unchanged everywhere (token claim, code constant, IaC variable, doc).

---

## What This Slice Does NOT Model

- **Domain entities** (Namespace, Queue, Topic, Subscription, Rule, etc.) — deferred to the slices that introduce them.
- **Per-resource authorization** (a user can read namespace X but not namespace Y) — deferred. The platform-role mechanism is the foundation; per-resource ACLs layer on top in a later slice.
- **Audit log persistence** — auth events flow to App Insights / Log Analytics via the existing OTel pipeline (FR-032, FR-034). BusTerminal does not own an audit-log datastore in this slice.
- **User profile cache** — Graph `User.Read.All` is available but no caching layer is introduced here. The first slice that needs cached user metadata will add it.

These omissions are intentional and consistent with the spec's "platform mechanism, not domain authorization" framing.
