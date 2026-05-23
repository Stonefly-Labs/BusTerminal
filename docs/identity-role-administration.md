# BusTerminal — Role Administration Runbook

**Status**: Authoritative · **Slice**: 003-auth-and-identity

This is the operator's runbook for **role administration** in BusTerminal: how to bootstrap the first `BusTerminal.Admin` in a new environment, how to grant or remove a role on an existing teammate, and how roles propagate through the system.

The platform-wide rules for *how* identity and credentials work (Managed Identity, OIDC federation, `DefaultAzureCredential`, MSAL) live in [`identity-and-secrets.md`](./identity-and-secrets.md). The Microsoft Graph permission inventory and admin-consent procedure live in [`identity-graph-permissions.md`](./identity-graph-permissions.md). This document is the **role-grant** overlay — read those first if you're new.

The binding contract for *which role can do what* is [`specs/003-auth-and-identity/contracts/role-permission-matrix.md`](../specs/003-auth-and-identity/contracts/role-permission-matrix.md). If anything below contradicts that matrix, the matrix wins.

---

## The roles

BusTerminal defines exactly four platform roles on the API app registration (`bt-<env>-api`). They are mutually independent — a user may hold zero, one, or several; effective permissions are the union (FR-011).

| Role (Entra display name) | Value (token claim) | What it grants |
|---|---|---|
| `BusTerminal Administrator` | `BusTerminal.Admin` | Read · MutateDomain · OperatePlatform · Administer · DeveloperTooling (all operation classes) |
| `BusTerminal Operator` | `BusTerminal.Operator` | Read · MutateDomain · OperatePlatform |
| `BusTerminal Reader` | `BusTerminal.Reader` | Read |
| `BusTerminal Developer` | `BusTerminal.Developer` | Read · DeveloperTooling |

A caller with **no role** can authenticate but cannot invoke any role-gated endpoint (FR-010) — they land on `/no-access` in the UI.

---

## Part A — Bootstrap the first `BusTerminal.Admin` in a new environment

This is a **one-time-per-environment** action, performed by a Microsoft Entra ID **tenant administrator** for the BusTerminal tenant. It is the **only Admin assignment performed outside IaC** (FR-002a), and is audit-logged in Entra's directory logs.

**Prerequisites**: `tofu apply` for the target environment has succeeded — the API app registration exists and its four app roles are visible.

**Verify the roles first** (Entra portal → **App registrations** → `bt-<env>-api` → **App roles**):

- `BusTerminal Administrator` (value `BusTerminal.Admin`)
- `BusTerminal Operator` (value `BusTerminal.Operator`)
- `BusTerminal Reader` (value `BusTerminal.Reader`)
- `BusTerminal Developer` (value `BusTerminal.Developer`)

Each role's *Allowed member types* must be `Users/Groups + Applications` so both humans and managed identities can be assigned.

**Grant the founding Admin**:

1. Entra portal → **Enterprise applications** → select `bt-<env>-api` (the **service-principal** entry, not the app registration).
2. **Users and groups** → **Add user/group**.
3. Pick the founding operator's user account.
4. Select role: **`BusTerminal Administrator`**.
5. **Assign**.
6. Record the assignment (date, who, which environment) in the operator-runbook handover doc.

The newly-assigned Admin can now sign into BusTerminal and grant every subsequent role assignment via the procedure in **Part B**.

---

## Part B — Grant a role to a teammate (SC-001 target: ≤ 5 minutes)

This is the **recurring action**. Anyone holding `BusTerminal.Admin` can perform it from the Entra portal — no IaC change, no PR, no deploy.

1. Entra portal → **Enterprise applications** → `bt-<env>-api` → **Users and groups** → **Add user/group**.
2. Select the user (search by display name or UPN).
3. Select **exactly one** role: `BusTerminal Administrator`, `BusTerminal Operator`, `BusTerminal Reader`, or `BusTerminal Developer`.
4. **Assign**.
5. Tell the teammate to sign out of BusTerminal and sign back in. The new token will carry the role claim immediately. If they prefer to wait, MSAL refreshes silently within the token's natural lifetime (≤ 1 hour).

A user may hold **multiple roles**; the effective permission set is the union (FR-011). Repeat the procedure to grant additional roles on the same user.

### Removing a role

Same path. **Users and groups** → select the user's existing role entry → **Remove**. The same propagation rules apply: a fresh sign-in flushes the claim immediately; an existing token keeps the role until its natural expiry (≤ 1 hour).

> **There is no role-removal "kill switch"** that revokes existing tokens mid-lifetime. If a teammate departs unexpectedly and the propagation window is unacceptable, **disable the user account** in Entra ID — that invalidates new sign-ins and (depending on the conditional-access policy) terminates active sessions on next token refresh.

---

## Part C — How role propagation works

| Event | When the new claim takes effect |
|---|---|
| User signs in fresh | Immediately — the role appears in the access token Entra mints. |
| User holds an existing valid token | At the next silent refresh (MSAL refreshes ahead of expiry; effective convergence ≤ 1 hour). |
| User is signed in to multiple tabs / devices | Each session converges independently on its own refresh cadence. |

Tokens carry the role as a `roles` claim. The backend reads it through `Microsoft.Identity.Web` and projects it into a `PlatformPrincipal.EffectiveRoles` set; authorization policies (`CanRead`, `CanMutateDomain`, etc.) check the set against the role-permission matrix at request time. The mapping lives in `api/BusTerminal.Api/Authorization/RolePolicies.cs` and **must match** [`role-permission-matrix.md`](../specs/003-auth-and-identity/contracts/role-permission-matrix.md).

There is **no role cache** in the backend beyond the access-token lifetime. The token *is* the cache; refreshing the token refreshes the role set.

---

## Part D — Smoke-test a fresh grant

After granting a role and signing back in, the user can validate the grant against the role probes:

```powershell
$token = "Bearer <copy from the network tab on /platform-status>"

# Always 200 (every role grants Read)
curl -H "Authorization: $token" https://<api-host>/probe/read

# 200 for Developer/Admin, 403 otherwise
curl -H "Authorization: $token" https://<api-host>/probe/developer

# 200 for Operator/Admin, 403 otherwise
curl -X POST -H "Authorization: $token" -H "Content-Type: application/json" `
     -d '{"message":"hi"}' https://<api-host>/probe/mutate-domain

# 200 for Admin only, 403 otherwise
curl -X POST -H "Authorization: $token" -H "Content-Type: application/json" `
     -d '{"message":"hi"}' https://<api-host>/probe/administer
```

Each probe response includes `callerObjectId`, `callerEffectiveRoles`, and the W3C `traceparent` correlation id. If the role isn't in `callerEffectiveRoles`, the token is stale — sign out and back in.

---

## Cross-references

- [`identity-and-secrets.md`](./identity-and-secrets.md) — the four credential mechanisms (UAMI, OIDC federation, `DefaultAzureCredential`, MSAL).
- [`identity-graph-permissions.md`](./identity-graph-permissions.md) — Microsoft Graph permission inventory and admin-consent procedure.
- [`local-development.md`](./local-development.md) — developer-onboarding prerequisites (including the `BusTerminal.Developer` role grant for local work).
- [`specs/003-auth-and-identity/contracts/role-permission-matrix.md`](../specs/003-auth-and-identity/contracts/role-permission-matrix.md) — the binding role↔operation-class contract.
- [`specs/003-auth-and-identity/quickstart.md`](../specs/003-auth-and-identity/quickstart.md) — the original walkthrough this runbook was promoted from; retains end-to-end environment-bring-up context.
