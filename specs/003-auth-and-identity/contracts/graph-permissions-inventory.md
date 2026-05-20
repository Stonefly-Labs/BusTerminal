# Microsoft Graph Permissions Inventory

**Status**: Authoritative · **Slice**: 003-auth-and-identity · **Last reviewed**: 2026-05-19

This document enumerates every Microsoft Graph permission BusTerminal's backend app registration (`bt-<env>-api`) holds. FR-024 requires this inventory be the single source of truth for which Graph operations the platform can perform. Adding a permission to the IaC without adding it here — or vice versa — is a defect.

The minimum-necessary principle applies (Constitution Principle IV — Security by Default; Decision Priority #4). Permissions granted by this slice are deliberately narrow; future slices that need more must justify each addition.

---

## Granted Permissions

### `User.Read.All` *(Application)*

| Property | Value |
|---|---|
| Permission name | `User.Read.All` |
| Permission type | **Application** (app-only) |
| Granted by | Slice 003 (this slice) |
| Requires admin consent | **Yes** |
| Granted in IaC | `iac/modules/graph-permissions/` via `azuread_application_api_access` |
| Consent state, per environment | Tracked manually below |

**Rationale**: enables the backend to resolve any user object id to a `User` resource — supporting (a) the SC-009 self-resolve smoke operation, and (b) the near-term "translate caller `oid` to display name and email" use case that follow-up slices (ownership UI, audit displays) will need. App-only is chosen because the consumer is the backend service, not a user-context request path; the backend may need to resolve users other than the calling user (e.g., a workload-initiated audit summary).

**Where it's consumed**: `api/BusTerminal.Api/Infrastructure/Graph/IGraphClient` and its concrete implementation only. Searching the codebase for `IGraphClient` returns every consumer.

**Consent state by environment**:

| Environment | Admin consent granted | Consent date | Tenant admin |
|---|---|---|---|
| `dev` | _pending — see runbook_ | _to be recorded_ | _to be recorded_ |
| `test` | _not yet provisioned_ | — | — |
| `prod` | _not yet provisioned_ | — | — |

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
2. **Declare** the permission in IaC by adding it to `iac/modules/graph-permissions/` (the `role_ids` list inside `azuread_application_api_access`).
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
