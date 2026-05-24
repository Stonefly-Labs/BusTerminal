# Microsoft Graph permissions

**Status**: Authoritative runbook for Graph permission management.

This document is BusTerminal's durable home for everything Microsoft-Graph-related: the **current permissions inventory**, the **admin-consent procedure** for a new environment, and the **add-a-new-permission procedure** for future slices. The authoritative inventory file lives in the active spec (`specs/003-auth-and-identity/contracts/graph-permissions-inventory.md`); this doc mirrors it for operational use and points back to the spec for the binding rationale on each grant.

The platform-wide rules on identity and secrets — Managed Identity preference, OIDC federation, `DefaultAzureCredential`, MSAL — live in `identity-and-secrets.md`. Read that first if you're new; this file is the Graph-specific overlay.

---

## How BusTerminal accesses Microsoft Graph

| Concern | Choice |
|---|---|
| Authentication flow | **App-only** (client credentials with a Managed Identity / Entra-Identity credential, no signed-in user). FR-024. |
| Code surface | A single abstraction — `IGraphClient` in `api/BusTerminal.Api/Infrastructure/Graph/`. **The Graph SDK is consumed via this interface only.** `grep "IGraphClient"` returns every Graph caller. |
| Credential | `IAzureCredentialFactory.CreateCredential()` → `DefaultAzureCredential` (Managed Identity in deployed environments; developer identity locally). FR-018. |
| Permission grants | Declared in OpenTofu (`iac/modules/graph-permissions/`), consumed at runtime. |
| Admin consent | **Manual**, per environment, by a tenant administrator. Not automated. |
| Delegated flows | Not currently enabled (FR-025). The `IGraphClient` interface accommodates them via a future user-context credential without breaking the interface — see the comment at the top of `IGraphClient.cs`. |

---

## Current permissions inventory

The single source of truth is `specs/003-auth-and-identity/contracts/graph-permissions-inventory.md`. Snapshot as of slice 003:

### `User.Read.All` *(Application)*

| Property | Value |
|---|---|
| Permission name | `User.Read.All` |
| Permission UUID | `df021288-bdef-4463-88db-98f22de89214` |
| Permission type | **Application** (app-only) |
| Granted by | Slice 003 (auth-and-identity) |
| Requires admin consent | **Yes** |
| Declared in IaC at | `iac/modules/graph-permissions/main.tf` (composed by `iac/environments/<env>/main.tf`) |
| Consumed by | `api/BusTerminal.Api/Infrastructure/Graph/GraphClient.cs::ResolveUserAsync` |
| Sole entry point | `IGraphClient` — no other code is permitted to call Graph directly |

**Rationale**: enables the backend to resolve any tenant user object id to a `User` resource. Powers (a) the SC-009 self-resolve smoke surfaced on `GET /probe/developer`, and (b) the near-term "translate caller `oid` to display name / mail" needs that follow-up slices (ownership UI, audit displays) will rely on. App-only is required because the backend may resolve users other than the calling user (e.g., a workload-initiated audit summary).

### Out-of-scope (deliberately not granted)

| Permission | Type | Why deferred |
|---|---|---|
| `GroupMember.Read.All` | Application | No current capability needs group-based recommendations or group-claim mapping. Add only when a slice introduces group-driven role mapping. |
| `Directory.Read.All` | Application | Substantially broader consent surface than `User.Read.All`; no current use case justifies it. |
| `User.Read` | Delegated | Delegated flows are *supported* by `IGraphClient` (FR-025) but not enabled in this slice. No `/me`-shaped endpoint ships here. |
| `Mail.*` / `Calendars.*` / `Files.*` | Either | Not relevant to BusTerminal's domain. |

---

## Granting admin consent in a new environment

Admin consent for application permissions is a **one-time, manual, per-environment** step. It cannot be performed by `tofu apply` — application permissions require directory-write privileges (RoleManagement / AppRoleAssignment.ReadWrite.All) that the deployment pipeline identity should *not* hold. Keeping consent as a tenant-admin action also leaves a clean audit-log trail in the Entra directory.

### Prerequisites

- You are signed into Entra as a tenant admin for the target environment's tenant.
- `tofu apply` has already declared the permission on the target API app registration (the IaC step is the prerequisite for the consent dance — without it, the portal shows nothing to consent to).

### Portal path

1. Entra portal → **App registrations** → select `bt-<env>-api` (e.g. `bt-dev-api`).
2. **API permissions** → confirm `Microsoft Graph → User.Read.All (Application)` appears under "Configured permissions" with the status "Not granted for &lt;Tenant&gt;".
3. Click **`Grant admin consent for <Tenant>`**. Confirm.
4. The status flips to "Granted for &lt;Tenant&gt;" (green check).
5. Record the consent in the inventory: open `specs/003-auth-and-identity/contracts/graph-permissions-inventory.md` (or its durable successor), edit the **Consent state by environment** table — fill in the environment row with today's date and the granting admin's UPN.

### CLI path (preferred for scripting)

```powershell
# Authenticate as the tenant admin
az login --tenant <tenant-id>

# Grant admin consent for every "Configured permissions" entry on the app registration
az ad app permission admin-consent --id <bt-env-api-app-id>
```

The `app-id` is the API app registration's *application (client) id* — visible on the portal's Overview blade. For `bt-dev-api` it's the value of the `entra_api_client_id` tofu variable.

### Verification

The fastest end-to-end smoke is `GET /probe/developer` against the deployed API:

```powershell
$token = "Bearer <a token for a BusTerminal.Admin or BusTerminal.Developer>"
curl -H "Authorization: $token" https://ca-bt-<env>-api.<env-domain>.azurecontainerapps.io/probe/developer
```

The response body contains `graphResolvedDisplayName`. After consent succeeds, this field carries the caller's Entra display name; before consent it is `null` (the probe degrades gracefully — see FR-024 and the endpoint's structured-log "consent may not yet be granted" warning).

---

## Adding a new permission in a future slice

A future slice that needs additional Graph access **MUST** follow this procedure. PRs that skip any step are rejected in review.

1. **Justify** the new permission in the slice's `spec.md` under a "Graph permissions added by this slice" subsection. Cite (a) the operation(s) the permission enables, (b) why no narrower permission would suffice, and (c) the consent surface trade-off.
2. **Resolve the UUID** from the [Microsoft Graph permissions reference](https://learn.microsoft.com/graph/permissions-reference). Application-permission UUIDs differ from delegated-permission UUIDs even for permissions that share a display name.
3. **Declare** the permission in IaC: extend the `granted_application_permission_ids` list passed to `module "graph_permissions"` in `iac/environments/<env>/main.tf`. The list is the wire format; add a same-PR comment naming the permission so future readers don't have to look up UUIDs.
4. **Update the inventory** in the same PR: add a new section to `specs/003-auth-and-identity/contracts/graph-permissions-inventory.md` (or the durable successor doc) and to the snapshot in this file. **Rationale** is a required field.
5. **Restrict consumption** to the `IGraphClient` abstraction. Adding a direct `GraphServiceClient` or raw `HttpClient` Graph call elsewhere in the codebase is a defect — the abstraction is the *only* permitted entry point so the audit surface (`grep IGraphClient`) stays complete.
6. **Apply the IaC** and **request admin consent** in every target environment (`dev` first, then `test` / `prod` per the environment-rollout policy of the slice). Record consent date + granting admin in the inventory.
7. **Extend the smoke** — add a probe / integration test exercising the new permission against the dev tenant (gated on `BUSTERMINAL_GRAPH_INTEGRATION=1` per the existing pattern). The test is the durable proof that consent succeeded; a future operator can re-run it to diagnose drift.

### Anti-patterns (do not do these)

- **Granting consent via `tofu apply`.** Application-permission consent requires far broader directory privileges than the deployment pipeline should hold. Keep it manual.
- **Adding `azuread_service_principal_delegated_permission_grant` for app-only consent.** That resource applies only to delegated (user) consent, not application admin consent. Wrong tool.
- **Calling `az ad app permission admin-consent` from a Tofu `local-exec`.** Couples infra apply to local CLI installation and quietly broadens the pipeline's effective privilege.
- **Granting `Directory.Read.All` "because it covers everything".** Least privilege is the rule. Pick the narrowest permission that does the job.
- **Caching Graph responses without an explicit retention policy.** No caching layer is shipped today. The first slice that needs caching adds the policy (TTL, invalidation, PII handling) in its design.

---

## Removing a permission

If a future slice retires a Graph-using capability:

1. Remove every code path that uses the permission (`grep` for the `IGraphClient` method).
2. Remove the UUID from the `granted_application_permission_ids` list.
3. Move the inventory entry from "Granted Permissions" to "Out-of-Scope" with a note recording the removal slice.
4. **Revoke admin consent** in the Entra portal for each environment (otherwise the historical grant remains visible in audit logs forever — usually fine, but worth being deliberate).
5. Verify the smoke test no longer depends on the permission.

---

## Audit surface

A Graph permission grant on `bt-<env>-api`'s app registration is visible to any tenant admin via **Entra portal → App registrations → `bt-<env>-api` → API permissions**. The grants displayed there MUST match the inventory exactly. Drift is a defect — investigate in the tenant's audit logs (the Entra directory writes a `Update application` event whenever permissions change).

The runtime side of the audit surface is the `IGraphClient` abstraction: every Graph call originates from one method on one class. If a code review surfaces a direct `GraphServiceClient` or `HttpClient` Graph call elsewhere, that's the defect — fix it before merge.

---

## Cross-references

- `specs/003-auth-and-identity/contracts/graph-permissions-inventory.md` — binding inventory (source of truth)
- `specs/003-auth-and-identity/research.md` § 3, § 9 — SDK choice and admin-consent decision
- `specs/003-auth-and-identity/quickstart.md` § A.2.3 — operator quickstart for first-time consent
- `iac/modules/graph-permissions/README.md` — IaC module contract
- `docs/identity-and-secrets.md` — platform-wide identity model
- `api/BusTerminal.Api/Infrastructure/Graph/IGraphClient.cs` — interface + GraphUser projection
- [Microsoft Graph permissions reference](https://learn.microsoft.com/graph/permissions-reference) — canonical UUID source
