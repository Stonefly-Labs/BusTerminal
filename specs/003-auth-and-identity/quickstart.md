# Quickstart: Auth and Identity

**Feature**: 003-auth-and-identity
**Audience**: an operator or platform engineer responsible for (a) preparing a new BusTerminal environment for role-aware authentication and (b) onboarding new developers to the local stack.
**Success criteria**: SC-001 (role grant ≤ 5 min end-to-end), SC-004 (developer local-Azure access via `az login` only), SC-008 (no-role experience ≤ 2 s), SC-009 (Graph self-resolve first-call success).

This is the slice-level quickstart. On implementation it will be split and promoted into the `docs/` runbook set: `docs/identity-role-administration.md`, `docs/identity-graph-permissions.md`, the rewritten `docs/identity-and-secrets.md`, and the updated `docs/local-development.md`.

---

## Part A — Operator: prepare an environment

For a brand-new environment, do the **infrastructure** steps first (Part A.1), then the **tenant** steps (Part A.2). For the existing `dev` environment, the infrastructure is already provisioned; jump to Part A.2.

### A.1 Infrastructure (OpenTofu)

The slice extends the existing OpenTofu composition with three new modules and an `azuread` provider declaration.

```powershell
cd iac/environments/dev
tofu init -upgrade        # picks up the new azuread provider pin
tofu plan -out=tfplan
# Review the plan. New resources you should see:
#   azuread_application_app_role.bt_admin
#   azuread_application_app_role.bt_operator
#   azuread_application_app_role.bt_reader
#   azuread_application_app_role.bt_developer
#   azuread_application_api_access.api_graph_user_read_all
#   azuread_app_role_assignment.workload_to_api_<role>  (per workload that needs API access)
tofu apply tfplan
```

What the plan does:

1. Declares the **four BusTerminal app roles** on the API app registration (`bt-dev-api`).
2. Declares **`User.Read.All`** as an application permission on the API app registration.
3. Assigns one or more API app roles to each **workload managed identity** that needs to call the API (per `workload-identity` module instances).
4. Reuses the existing **pipeline federated credential** (002) — no new federation is introduced.

The plan does **not**:

- Assign the initial `BusTerminal.Admin` role to any human (manual portal step — see A.2.2).
- Grant admin consent for the Graph permission (manual portal step — see A.2.3).
- Mutate the `bt-dev-api` app registration's other properties (the parent resource has `lifecycle { ignore_changes = [app_role] }` so roles are independently managed).

### A.2 Tenant configuration (manual, one-time per environment)

> **Privilege required**: Microsoft Entra ID **tenant administrator** for the BusTerminal tenant (`596c1564-6e95-4c35-a80b-2dbe45a162f3` for `dev`).

#### A.2.1 Verify the app roles are visible

Open Entra portal → App registrations → `bt-dev-api` → **App roles**. You should see four entries:

- `BusTerminal Administrator` (value `BusTerminal.Admin`)
- `BusTerminal Operator` (value `BusTerminal.Operator`)
- `BusTerminal Reader` (value `BusTerminal.Reader`)
- `BusTerminal Developer` (value `BusTerminal.Developer`)

Each role's "Allowed member types" should be `Users/Groups + Applications` (this lets both humans and managed identities receive role assignments).

#### A.2.2 Grant the initial `BusTerminal.Admin` to a founding operator (FR-002a)

> **This step is intentionally manual.** It is the only Admin assignment performed outside IaC, and is audit-logged in Entra's directory logs.

1. Entra portal → **Enterprise applications** → select `bt-dev-api` (you want the service-principal entry, not the app registration).
2. **Users and groups** → **Add user/group**.
3. Pick the founding operator's user account.
4. Select role: **`BusTerminal Administrator`**.
5. **Assign**.
6. Record the assignment (date, who, which env) in the operator runbook handover doc.

The newly-assigned Admin can now sign into the BusTerminal UI and grant any further role assignments via the same Entra UI. Roles propagate within ~1–3 minutes for fresh tokens (existing tokens converge at their natural expiry — typically ≤ 1 hour).

#### A.2.3 Grant admin consent for `User.Read.All` (FR-024)

1. Entra portal → App registrations → `bt-dev-api` → **API permissions**.
2. Verify `Microsoft Graph → User.Read.All (Application)` appears under "Configured permissions". (If not, re-run `tofu apply`.)
3. Click **`Grant admin consent for <Tenant>`**. Confirm.
4. Verify the permission's status reads **Granted for <Tenant>**.
5. Record the consent date and granting admin in `contracts/graph-permissions-inventory.md` (the file lives in the spec; the rewritten `docs/identity-graph-permissions.md` will replace it as the durable home).

CLI alternative:

```powershell
az ad app permission admin-consent --id <bt-dev-api-app-id>
```

#### A.2.4 Verify MSAL redirect URIs on the web app registration

1. Entra portal → App registrations → `bt-dev-web` → **Authentication** → **Single-page application** platform.
2. Confirm the redirect URIs include both the dev FQDN (`https://ca-bt-dev-web.<env-domain>.azurecontainerapps.io`) and `http://localhost:3000` (for local dev).
3. Confirm **Implicit grant and hybrid flows** has *no* boxes ticked — MSAL uses Authorization Code + PKCE only (FR-003).
4. Confirm "Allow public client flows" is **No** (this is a web/SPA registration, not a public client).

> Note: 002 registered `http://localhost:3000/api/auth/callback/microsoft-entra-id` as a *web* redirect URI for NextAuth. This slice **removes that URI** and replaces it with `http://localhost:3000` registered as a *Single-page application* redirect. The change is part of the IaC delta.

---

## Part B — Operator: grant a role to a teammate (SC-001 target)

This is the recurring action: an Admin granting a role to another human. Target: ≤ 5 minutes end-to-end.

1. Entra portal → **Enterprise applications** → `bt-dev-api` → **Users and groups** → **Add user/group**.
2. Select the user (search by display name or UPN).
3. Select the role: **`BusTerminal Administrator` / `Operator` / `Reader` / `Developer`** (exactly one).
4. **Assign**.
5. Tell the teammate to sign out of BusTerminal and back in. Their new token will carry the role claim. If they're already signed in, MSAL will silently refresh within the token's natural lifetime (≤ 1 hour) — they don't need to do anything if they can wait.

A user may hold multiple roles; effective permissions are the union (FR-011).

To **remove** a role: same path, select the user's existing role entry, **Remove**. Same propagation rules apply.

---

## Part C — Developer: run the local stack with real Azure dependencies (SC-004 target)

The frontend's MSAL config still signs in to the **dev** tenant for local development — there is no longer a frontend mock provider (FR-018, research §5). The backend continues to support a `tenant=development` mock mode for integration tests and curl-style probing.

### C.1 First-time setup (extending the slice-002 quickstart)

> **Authoritative developer-onboarding reference**: `docs/local-development.md` § 0 lists
> the prerequisite Entra-account + role-grant + `az login` steps that the
> `scripts/start-local.{sh,ps1}` launcher validates on every run. This section
> mirrors them for operators preparing developer access.

After completing the slice-002 prerequisites (Part 1–2 of `specs/002-solution-foundation/quickstart.md`):

```powershell
# Sign into Azure with your developer Entra account (in the BusTerminal dev tenant)
az login --tenant 596c1564-6e95-4c35-a80b-2dbe45a162f3
az account set --subscription 08b37dc0-0011-4841-84c0-0349a5c65883

# Verify your identity resolved
az ad signed-in-user show --query '{displayName:displayName, oid:id, upn:userPrincipalName}'
```

### C.2 Have an Admin grant you the `BusTerminal.Developer` role

Follow **Part B** above with yourself as the teammate. (You'll need to do this once per environment your local backend points at.) The `BusTerminal.Developer` role grants `Read` + `DeveloperTooling` operation classes — sufficient for exercising the API surface without risk of mutating shared state. If you also need to exercise `MutateDomain` / `OperatePlatform` operations locally, request `BusTerminal.Operator`; if you need `Administer`, request `BusTerminal.Admin`.

### C.3 Configure local environment files

```powershell
# Frontend MSAL config — replaces the slice-002 NextAuth env vars
Copy-Item web/.env.local.example web/.env.local
# Edit web/.env.local — fill in:
#   NEXT_PUBLIC_AZURE_AD_TENANT_ID=596c1564-6e95-4c35-a80b-2dbe45a162f3
#   NEXT_PUBLIC_AZURE_AD_CLIENT_ID=84ca372d-8d45-4527-967f-868a3336985b   (bt-dev-web)
#   NEXT_PUBLIC_API_SCOPE=api://9fb329a3-7b5b-4fdf-a46a-71f7df1d6716/.default
#   NEXT_PUBLIC_API_BASE_URL=http://localhost:5000

# Backend appsettings — points at the same tenant and api app registration
Copy-Item api/BusTerminal.Api/appsettings.Development.json.example `
          api/BusTerminal.Api/appsettings.Development.json
# Edit api/BusTerminal.Api/appsettings.Development.json — fill in:
#   "AzureAd": {
#     "Instance": "https://login.microsoftonline.com/",
#     "TenantId": "596c1564-6e95-4c35-a80b-2dbe45a162f3",
#     "ClientId": "9fb329a3-7b5b-4fdf-a46a-71f7df1d6716",
#     "Audience": "api://9fb329a3-7b5b-4fdf-a46a-71f7df1d6716"
#   }
```

`.env.local` and `appsettings.Development.json` are gitignored. No client secret is required — the SPA uses Authorization Code + PKCE; the backend validates tokens issued by Entra (no client credentials on the API side); Azure-service access uses `DefaultAzureCredential` resolving to your `az login` identity (Key Vault, future Cosmos / Search / etc.).

### C.4 Start the stack

```powershell
pwsh scripts/start-local.ps1
```

Open `http://localhost:3000`. MSAL redirects you to Entra; you sign in with your dev-tenant Entra account; you land back on `/platform-status`. The page calls `/whoami` and shows:

- Your identity (display name, oid, tenant id, callerType=Human)
- Your effective roles (e.g., `["BusTerminal.Developer"]`)
- The W3C trace correlation id

Smoke the role-permission matrix:

```powershell
# Get a token via your local frontend's MSAL flow, copy it from the network tab,
# OR mint one via the mock path:
$token = "Bearer <paste-token-here>"

# These should succeed for Developer:
curl -H "Authorization: $token" http://localhost:5000/probe/read
curl -H "Authorization: $token" http://localhost:5000/probe/developer
# These should return 403:
curl -X POST -H "Authorization: $token" -H "Content-Type: application/json" `
     -d '{"message":"hi"}' http://localhost:5000/probe/mutate-domain
curl -X POST -H "Authorization: $token" -H "Content-Type: application/json" `
     -d '{"message":"hi"}' http://localhost:5000/probe/administer
curl -X POST -H "Authorization: $token" http://localhost:5000/probe/operate
```

Or, with the backend in mock mode and the mock-roles header (no real Entra round-trip needed):

```powershell
# AZURE_AD_TENANT_ID=development on the backend
curl -H "X-Mock-Roles: BusTerminal.Developer" http://localhost:5000/probe/read         # 200
curl -H "X-Mock-Roles: BusTerminal.Developer" http://localhost:5000/probe/developer    # 200
curl -H "X-Mock-Roles: BusTerminal.Reader" http://localhost:5000/probe/developer       # 403
curl -H "X-Mock-Roles: BusTerminal.Admin" -X POST -H "Content-Type: application/json" `
     -d '{"message":"hi"}' http://localhost:5000/probe/administer                      # 200
curl http://localhost:5000/probe/read                                                  # 401 (no token)
```

---

## Part D — Operator: verify the slice's success criteria

This is the smoke checklist for declaring the slice done in a freshly-provisioned environment.

### SC-001 — role grant ≤ 5 min

Time yourself walking through **Part B** with a fresh teammate. If you exceed 5 minutes the runbook needs improvement, not the implementation.

### SC-002 — zero static credentials

```powershell
# In CI: gitleaks --redact --no-banner --report-format=json
# Locally:
gitleaks detect --redact --no-banner --source=.
```

Expect zero findings. Inspect each workload's auth path:

```powershell
# Backend → Key Vault: ManagedIdentity (DefaultAzureCredential)
# Pipeline → Azure: OIDC federation
# Frontend → API: MSAL-acquired user token (no client secret on the SPA side)
# (Future) Backend → Cosmos / Search / Storage / Service Bus / OpenAI / App Config:
#   DefaultAzureCredential → ManagedIdentity (config-only changes per new SDK)
```

### SC-003 — workload → API authenticated via MI

```powershell
# Stand up a probe Container Apps Job (one-off) that uses the existing workload MI
# (mi-bt-dev-workload). The job's only step:
#   az account get-access-token --resource api://9fb329a3-7b5b-4fdf-a46a-71f7df1d6716
#   curl -H "Authorization: Bearer <token>" https://ca-bt-dev-api.<env>/probe/read
# Expect: 200 from /probe/read (the workload MI was granted BusTerminal.Reader in IaC).
# Expect: 401 from a second job that does not present any token.
```

### SC-004 — developer local Azure access via `az login` only

Run **Part C** on a clean machine. The backend authenticates to Key Vault via your `az`-resolved identity. No `.env` file contains an Azure secret. No code path branches between local and deployed for credential acquisition.

### SC-005 — adding a workload is module-composition

Confirm that adding a new `module "workload_identity" "discovery_worker" { ... }` block to `iac/environments/dev/main.tf` is sufficient to bring up a new MI with the desired RBAC and BusTerminal app-role assignments — no inline `azurerm_user_assigned_identity` or `azurerm_role_assignment` blocks introduced.

### SC-006 — authz failures visible in telemetry

Trigger a 403 on `/probe/administer` from a non-Admin caller. Within 2 minutes (App Insights telemetry latency, inherited from 002):

```kql
// In the Log Analytics workspace:
AppRequests
| where Url endswith "/probe/administer"
| where ResultCode == "403"
| project TimeGenerated, OperationId, Properties.required_role, Properties.caller_oid
| order by TimeGenerated desc
```

Should return at least one row with the caller's `oid` and the required role(s).

### SC-007 — secret-scan zero findings

Confirmed by SC-002 above.

### SC-008 — no-role experience ≤ 2 s

Have an Admin assign **no** role to a fresh user, then have that user sign in via `http://localhost:3000` (or the dev FQDN). Time from MSAL redirect-back to the no-access page rendering. Should be ≤ 2 s. Page content:

- Friendly headline ("You don't have access to BusTerminal yet")
- The user's display name
- The `oid` they can share with their admin to request a role
- A "sign out" affordance

API behavior: `/whoami` returns `200` with `effectiveRoles: []`. Any role-gated endpoint returns `403`.

### SC-009 — Graph self-resolve first-call success

After admin-consenting `User.Read.All`:

```powershell
# Hit a developer-tooling probe that internally calls IGraphClient to resolve
# the calling user's profile. Expect 200 on the first invocation post-deploy.
$token = "Bearer <a token for an Admin or Developer>"
curl -H "Authorization: $token" https://ca-bt-dev-api.<env>/probe/developer
```

The probe's response includes the `displayName` resolved via Graph for the caller's `oid` — proof the app-only Graph flow works end-to-end without any client secret.

### W3C Trace Context — `traceparent` propagation (constitution)

Every UI-originated HTTP request must carry a W3C `traceparent` header so the
frontend trace correlates with the backend OpenTelemetry trace in Azure
Monitor. This is **non-optional** regardless of observability adapter
configuration (constitution + slice 001).

Manual verification (recommended after any change to MSAL token acquisition,
the API client, or the layout's `/whoami` fetch):

1. With the local stack running (`pwsh scripts/start-local.ps1`), sign in
   and reach `/platform-status`.
2. Open the browser **DevTools → Network** tab.
3. Click the `whoami` request. Under **Request Headers**, find
   `traceparent`. The value must match the regex
   `^00-[0-9a-f]{32}-[0-9a-f]{16}-[0-9a-f]{2}$` (version-trace-id-span-id-flags).
4. Optional: also confirm any subsequent `/probe/*` requests carry their own
   `traceparent`. Each request gets a fresh span id; the trace id stays
   stable across the page session.
5. In Application Insights' end-to-end transaction view, the browser request
   and the backend request must appear as **one** correlated trace.

Automated coverage: the Playwright spec at
`web/tests/e2e/msal-sign-in-and-whoami.spec.ts` (T093 / T096) asserts the
header presence and shape on `/whoami` after the MSAL fixture lands. Until
then, the manual check above is the durable signal.

---

## Troubleshooting cheat sheet

| Symptom | Likely cause | Fix |
|---|---|---|
| `AADSTS50105: The signed in user is not assigned to a role for the application` | User signed in with a tenant that recognizes them but they hold no BusTerminal role | Part B — assign a role. |
| `AADSTS65001: The user or administrator has not consented to use the application` (frontend) | The web app registration's API permission for the API's `access_as_user` scope was never admin-consented | Entra portal → `bt-dev-web` → API permissions → Grant admin consent. |
| Frontend stuck in redirect loop after sign-in | MSAL redirect URI mismatch between web app registration and `NEXT_PUBLIC` env | Compare the web app registration's "Single-page application" redirect URIs with `window.location.origin` of the loading page. |
| `/whoami` returns 401 in deployed env, 200 locally | Backend `Audience` claim mismatch | Confirm `AzureAd:Audience` matches the API app registration's `IdentifierUris` value. |
| `IGraphClient` call fails with "Insufficient privileges" | Admin consent not granted on `User.Read.All` for this environment | A.2.3 above. |
| Pipeline deploy fails with `AADSTS70021: No matching federated identity record found` | GitHub OIDC subject claim drifted from the federated credential's accepted subject (FR-030) | Read the error — it prints the expected subject. Either update the federated credential to match, or update the GitHub workflow's environment/branch to match the existing federated credential. |
| `tofu apply` fails with `application_api_access` reporting "permission not found" | The Graph permission name is misspelled or refers to a deprecated permission | Confirm against the Microsoft Graph permissions reference. Permission names are case-sensitive. |
| Local backend cannot read Key Vault: `DefaultAzureCredential failed to retrieve a token` | Not signed in with `az login`, or signed into the wrong tenant | `az login --tenant 596c1564-...` then `az account set --subscription 08b37dc0-...`. |

---

## Definition of done for this slice

A reasonable smoke for this slice is: a fresh teammate, following this document alone, can in **under 30 minutes total** (1) be granted `BusTerminal.Developer`, (2) sign into BusTerminal locally and see their role(s), (3) hit all five role probes and observe correct allow/deny behavior, and (4) make one Graph call via the dev-tooling probe that resolves their own profile. That validates SC-001, SC-003 (analog), SC-004, SC-008 (negative case via a non-roled account), and SC-009 in a single linear flow.
