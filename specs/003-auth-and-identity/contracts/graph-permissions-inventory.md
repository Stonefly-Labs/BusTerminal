# Microsoft Graph Permissions Inventory

**Status**: Authoritative · **Slice**: 003-auth-and-identity · **Last reviewed**: 2026-05-19

This document enumerates every Microsoft Graph permission BusTerminal's backend app registration (`bt-<env>-api`) holds. FR-024 requires this inventory be the single source of truth for which Graph operations the platform can perform. Adding a permission to the IaC without adding it here — or vice versa — is a defect.

The minimum-necessary principle applies (Constitution Principle IV — Security by Default; Decision Priority #4). Permissions granted by this slice are deliberately narrow; future slices that need more must justify each addition.

---

## ⚠️ Runtime identity: grants must reach the workload managed identity

App-only Graph permissions are only effective for the **service principal that actually authenticates the Graph call**. BusTerminal's backend authenticates as the **workload user-assigned managed identity** (`mi-bt-<env>-workload`) via `DefaultAzureCredential`/`ManagedIdentityCredential` — **not** via client-credentials on the `bt-<env>-api` app registration.

Therefore each granted permission must exist in **two** places, and both are required:

1. **On the API app registration** — declared in `iac/modules/graph-permissions/` (`azuread_application_api_access`) and made effective by tenant **admin consent**. This is the documented/audited inventory surface and covers any future client-credentials flow.
2. **On the workload MI's service principal** — declared as a direct **app-role assignment** in `iac/modules/workload-identity/` (`assigned_graph_app_roles` → `azuread_app_role_assignment`). **This is what the running backend actually relies on.** Admin consent on the app registration does nothing for the MI.

Omitting (2) is invisible at the app-registration audit surface but causes Graph to return **403** at runtime (e.g. the spec 008 owner-picker returned 502, 2026-06-24, because the MI held no Graph app-roles despite app-registration consent being granted). When adding or removing a permission, update **both** IaC modules and this inventory in the same PR. Note: an MI app-role assignment changes the `roles` claim in newly-issued tokens only — the workload must obtain a fresh token (container restart or token-cache expiry) before the grant takes effect.

---

## Granted Permissions

### `User.Read.All` *(Application)*

| Property | Value |
|---|---|
| Permission name | `User.Read.All` |
| Permission type | **Application** (app-only) |
| Granted by | Slice 003 (this slice) |
| Requires admin consent | **Yes** (app registration) |
| Role id | `df021288-bdef-4463-88db-98f22de89214` |
| Granted in IaC | `iac/modules/graph-permissions/` (app registration) **and** `iac/modules/workload-identity/` `assigned_graph_app_roles` (workload MI) |
| Consent / assignment state, per environment | Tracked manually below |

**Rationale**: enables the backend to resolve any user object id to a `User` resource — supporting (a) the SC-009 self-resolve smoke operation, (b) the "translate caller `oid` to display name and email" use case, and (c) the spec 008 owner-picker (`GET /api/namespaces/_picker`) which searches users. App-only is chosen because the consumer is the backend service, not a user-context request path; the backend may need to resolve users other than the calling user.

**Where it's consumed**: `api/BusTerminal.Api/Infrastructure/Graph/` — `IGraphClient` and `IGraphPrincipalPicker` (`GraphPrincipalPicker`). Searching the codebase for those types returns every consumer.

**Consent / assignment state by environment** (see ⚠️ section above — both columns must be satisfied):

| Environment | App-reg admin consent | Workload-MI app-role assigned | Date | Tenant admin |
|---|---|---|---|---|
| `dev` | Granted 2026-06-14 | **Yes** — added 2026-06-24 (regression fix) | 2026-06-24 | _to be recorded_ |
| `test` | _not yet provisioned_ | via IaC `assigned_graph_app_roles` | — | — |
| `prod` | _not yet provisioned_ | via IaC `assigned_graph_app_roles` | — | — |

---

### `Group.Read.All` *(Application)*

| Property | Value |
|---|---|
| Permission name | `Group.Read.All` |
| Permission type | **Application** (app-only) |
| Granted by | Slice 008 (namespace onboarding — owner picker) |
| Requires admin consent | **Yes** (app registration) |
| Role id | `5b567255-7703-4780-807c-7be8301ae99b` |
| Granted in IaC | `iac/modules/graph-permissions/` (app registration) **and** `iac/modules/workload-identity/` `assigned_graph_app_roles` (workload MI) |
| Consent / assignment state, per environment | Tracked manually below |

**Rationale**: the spec 008 owner-picker (`GET /api/namespaces/_picker?includeGroups=true`) lets an operator pick a **group** as a namespace owner. Resolving/searching groups requires `Group.Read.All`. Narrower than `Directory.Read.All`; `GroupMember.Read.All` is insufficient because the picker reads group objects (display name/mail), not membership.

**Where it's consumed**: `api/BusTerminal.Api/Infrastructure/Graph/GraphPrincipalPicker` (`graph.Groups.GetAsync`).

**Consent / assignment state by environment**:

| Environment | App-reg admin consent | Workload-MI app-role assigned | Date | Tenant admin |
|---|---|---|---|---|
| `dev` | Granted 2026-06-14 | **Yes** — added 2026-06-24 (regression fix) | 2026-06-24 | _to be recorded_ |
| `test` | _not yet provisioned_ | via IaC `assigned_graph_app_roles` | — | — |
| `prod` | _not yet provisioned_ | via IaC `assigned_graph_app_roles` | — | — |

---

## Out-of-Scope Permissions (Documented for Clarity)

These permissions are commonly considered for identity-aware platforms and are **explicitly not** granted by this slice. They appear here so future slice authors don't waste time re-litigating the decision and so reviewers can challenge any unjustified expansion.

| Permission | Type | Why deferred |
|---|---|---|
| `GroupMember.Read.All` | Application | Required only when BusTerminal needs group-driven recommendations or group-based role mapping. No current capability needs it. Add via a follow-up slice if/when group-claim-mapped roles are introduced (Q2 clarification deferred this). |
| `Directory.Read.All` | Application | Substantially broader than `User.Read.All`; covers groups, devices, app registrations, directory roles. No current use case justifies the additional consent surface. |
| `User.Read` | Delegated | Delegated flows are *supported* by `IGraphClient` (FR-025) but not *enabled* in this slice. No `/me`-shaped endpoint ships here. |
| `Mail.*` / `Calendars.*` / `Files.*` | Either | Not relevant to BusTerminal's domain. |

---

## Procedure to Add a New Graph Permission

A future slice that needs additional Graph access MUST follow this procedure:

1. **Justify** the permission in the new slice's spec under a "Graph permissions added by this slice" subsection. Cite the specific operation(s) the permission enables and explain why no narrower permission would suffice.
2. **Declare** the permission in IaC in **both** places (see the ⚠️ section): (a) `iac/modules/graph-permissions/` (the `role_ids` list inside `azuread_application_api_access`, on the app registration) **and** (b) each env's `assigned_graph_app_roles` map on `module.workload_identity` (the direct app-role assignment on the workload MI that the running backend actually uses).
3. **Update this inventory** in the same PR — add a new "Granted Permissions" section entry with rationale, consumer pointer, and per-environment consent state. **PRs that change IaC without updating this document MUST be rejected in review.**
4. **Obtain admin consent** in each target environment via the Entra portal or `az ad app permission admin-consent` (procedure in `docs/identity-graph-permissions.md`). Record the consent date and the granting tenant admin in this inventory.
5. **Restrict consumption** to the `IGraphClient` abstraction. Adding a direct `GraphServiceClient` or `HttpClient` Graph call elsewhere in the codebase is a defect.
6. **Verify** by extending the slice's smoke or integration tests to exercise the new permission against the dev environment.

---

## Procedure to Remove a Graph Permission

Removing a Graph permission requires:

1. Removing every code path that uses the permission. `grep` for the corresponding `IGraphClient` method call.
2. Removing the IaC declaration.
3. Updating this inventory (move the entry from "Granted Permissions" to "Out-of-Scope Permissions" with a note about the removal slice).
4. Revoking admin consent in the Entra portal for each environment.
5. Verifying the smoke test no longer depends on the permission.

---

## Audit Surface

A Graph permission grant on the backend's app registration is visible to any tenant admin via Entra portal → App registrations → `bt-<env>-api` → API permissions. The grants displayed there MUST exactly match the "Granted Permissions" section above; any drift is a defect to be investigated in the tenant's audit log.
