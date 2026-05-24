# BusTerminal — Identity and Secrets

**Status**: Authoritative · **Slice**: 003-auth-and-identity · **Supersedes**: any 002-era version of this document

This document is the single page that answers "how does anything in BusTerminal acquire credentials?" Every Azure call traces back to exactly one of the four mechanisms below. If you find code that doesn't, it's a defect — file an issue and fix it.

The rule, in one sentence: **BusTerminal has zero static credentials at rest or in transit.** No client secrets in source, no client secrets in container env, no SAS tokens, no connection strings with embedded keys, no service-principal-with-secret federation. Constitution Principle IV (Security by Default).

---

## The four credential mechanisms

| # | Mechanism | When | Where it appears |
|---|---|---|---|
| 1 | **User-Assigned Managed Identity** | Workload-to-Azure (Container App pulling from Key Vault, calling Microsoft Graph, etc.) | `iac/modules/workload-identity/`, every Container App's `identity` block |
| 2 | **OIDC federated credential** | Pipeline-to-Azure (GitHub Actions → Azure via short-lived JWT) | `iac/platform-bootstrap/`, `azuread_application_federated_identity_credential` |
| 3 | **`DefaultAzureCredential`** | Backend code reaching any Azure service (Key Vault, Graph, future Cosmos/AI Search/Service Bus) | `api/BusTerminal.Api/Infrastructure/Credentials/AzureCredentialFactory.cs` |
| 4 | **MSAL (Authorization Code + PKCE)** | Browser SPA signing the user in and acquiring an access token for the backend API | `web/lib/auth/`, `@azure/msal-browser`, `@azure/msal-react` |

Anything else — shared passwords, NextAuth secrets, AD client secrets, account keys, SAS tokens — is **prohibited**. The 002-era `WebClientSecret` and `NextAuthSecret` in Key Vault were removed by spec 003. The Key Vault entries are now orphans and should be deleted manually post-deploy (`az keyvault secret delete --vault-name kv-bt-dev-chdev01 --name WebClientSecret` etc.). Record the deletion in the dev-environment change log when done.

---

## 1. Managed Identity (workload-to-Azure)

Every Container App, Container Apps Job, and Function in BusTerminal runs under a **user-assigned managed identity** (UAMI). The UAMI is the workload's identity for every Azure call the workload makes — Key Vault pulls, Graph calls, future Cosmos / AI Search / Service Bus / OpenAI access.

- **IaC entrypoint**: `module.workload_identity` in `iac/environments/<env>/main.tf`. The module provisions the UAMI, attaches optional API-app-role assignments (so the workload can call the BusTerminal backend), and optionally grants Azure-resource RBAC roles (`Key Vault Secrets User` on the env KV, etc.).
- **Container wiring**: every Container App's `identity { type = "UserAssigned"; identity_ids = [<uami_id>] }` block points at the UAMI. The container reads `AZURE_CLIENT_ID` (the UAMI's client id) and `DefaultAzureCredential` picks it up automatically.
- **Why UAMI and not system-assigned**: lifecycle. The UAMI's role assignments and federations survive container redeploys; system-assigned identities are tied to the resource lifecycle.

Workload-to-API authentication (FR-012) — the backend's `/probe/read`, future domain endpoints — uses the **same UAMI** to acquire a token for the API audience (`api://<api-app>/.default`). The backend validates the token identically to a human-user token; there is no internal-trust bypass.

## 2. OIDC federated credentials (pipeline-to-Azure)

GitHub Actions deploys BusTerminal without ever holding a long-lived Azure secret. The pipeline service principal (`mi-busterminal-pipeline-<env>`) has a **federated credential** declaring trust in tokens issued by GitHub's OIDC IdP for the repo + environment. At job time, GitHub mints a short-lived JWT, the `azure/login@v2` action exchanges it for an Azure access token, and OpenTofu/AzCLI use it.

- **IaC entrypoint**: `iac/modules/federated-credential/` (added in spec 003 user story 5). The module is generic — supply `issuer`, `subject`, `audience`, and an Entra app/MI to federate to.
- **Why federation and not a stored secret**: there is nothing to rotate, nothing to leak, nothing to commit by accident. The token GitHub mints lives ~15 minutes and is scoped to one repo+environment.
- **Granting Graph rights to the pipeline MI**: required for the IaC `data.azuread_application.api` lookup at plan time and for `azuread_application_app_role` writes at apply time. The two role grants (`Application.Read.All` and `Application.ReadWrite.OwnedBy`) plus making the MI an *owner* of `bt-<env>-api` are out-of-band Azure admin actions documented in `quickstart.md` § Part A.

## 3. `DefaultAzureCredential` (backend code)

The backend resolves its Azure credential through one factory: `IAzureCredentialFactory.CreateCredential()` (`api/BusTerminal.Api/Infrastructure/Credentials/AzureCredentialFactory.cs`). The factory returns a `DefaultAzureCredential` that resolves identity in this order:

1. **In a deployed Container App**: `ManagedIdentityCredential` finds the UAMI via `AZURE_CLIENT_ID` and exchanges it for an Entra token.
2. **In local dev**: `AzureCliCredential` picks up the developer's `az login` session. Developers must run `az login --tenant 596c1564-6e95-4c35-a80b-2dbe45a162f3` once (the BusTerminal dev tenant) before launching the backend.

Every Azure SDK client constructed in the backend takes a `TokenCredential` from this factory — `SecretClient`, `GraphServiceClient`, future `CosmosClient`/`SearchClient`/`ServiceBusClient`. **Do not** construct `DefaultAzureCredential` inline. The factory is the single seam so we can swap in a deterministic credential for tests and so the developer-friendly "you need to `az login`" error message lands consistently.

## 4. MSAL (browser SPA)

The frontend authenticates users with **MSAL** (`@azure/msal-browser` 4.x + `@azure/msal-react` 3.x) using Authorization Code + PKCE. No client secret exists; the SPA is a *public client* per the Entra terminology.

- **MSAL config**: `web/lib/auth/msal-config.ts` reads three `NEXT_PUBLIC_*` env vars:
  - `NEXT_PUBLIC_AZURE_AD_TENANT_ID` — the BusTerminal Entra tenant id
  - `NEXT_PUBLIC_AZURE_AD_CLIENT_ID` — the `bt-<env>-web` app registration id
  - `NEXT_PUBLIC_API_SCOPE` — `api://<api-app>/.default`, the scope MSAL requests for backend calls
- **Token acquisition**: `web/hooks/use-acquire-token.ts` calls `acquireTokenSilent` first, falls back to `acquireTokenRedirect` if silent fails.
- **Why public client**: SPAs cannot safely hold a secret. PKCE replaces the secret with a per-request challenge. This is the Microsoft-recommended SPA pattern.

`NEXT_PUBLIC_*` values are public Entra identifiers — not secrets — and are safe to inline into the client bundle. They are documented in `web/.env.local.example`.

---

## What changed in spec 003

Spec 002 shipped the foundation slice with a NextAuth-backed sign-in. That carried a confidential-client model with `AZURE_AD_CLIENT_SECRET` and `NEXTAUTH_SECRET` env vars sourced from Key Vault. Spec 003 replaced the entire frontend auth stack with MSAL and **deleted** every reference to those secrets:

- `web/lib/auth.ts` (NextAuth config) — deleted
- `web/app/api/auth/[...nextauth]/route.ts` (NextAuth handler) — deleted
- `web/middleware.ts` (session-based redirect) — deleted; `AuthGuard` handles redirects client-side
- `iac/environments/dev/main.tf` — `secret_env_vars` block no longer includes `AZURE_AD_CLIENT_SECRET` or `NEXTAUTH_SECRET`; `key_vault_secrets` no longer maps `web-client-secret` or `nextauth-secret`
- `.github/workflows/ci.yml` — Playwright step no longer sets `AUTH_SECRET` / `NEXTAUTH_SECRET` / `AZURE_AD_TENANT_ID`

The Key Vault entries `WebClientSecret` and `NextAuthSecret` are now **orphans** — provisioned by 002 but referenced by nothing in the current code. Operators with `Key Vault Secrets Officer` on the env KV should delete them manually (`az keyvault secret delete ...`) the next time they're touching that vault. Tofu state will not re-create them.

---

## Cross-references

- **Microsoft Graph permissions inventory**: `specs/003-auth-and-identity/contracts/graph-permissions-inventory.md` is authoritative for which Graph permissions exist and on which environments they are consented.
- **Role-to-operation matrix**: `specs/003-auth-and-identity/contracts/role-permission-matrix.md` is the contract every backend endpoint binds against.
- **Role administration runbook**: `docs/identity-role-administration.md` (planned for Phase 9 polish, T091) — how to grant or revoke a `BusTerminal.*` role on a teammate.
- **Local development**: `docs/local-development.md` — `az login` walkthrough and the `IAzureCredentialFactory` flow.
- **Deploying environments**: `docs/deploying-environments.md` — provisioning a new environment end-to-end.
- **Constitution**: `.specify/memory/constitution.md` § IV (Security by Default).

---

## How to add a new credential path

Don't, unless it's one of the four mechanisms above. If you need:

- a workload to reach Azure → add it to `module.workload_identity` in the env composition; grant the minimum Azure RBAC role you need on the target resource.
- a workload to reach the BusTerminal API → grant it a `BusTerminal.*` app role via the `workload-identity` module's `assigned_api_app_roles` input.
- a new Graph permission → see `contracts/graph-permissions-inventory.md` § "Adding a new permission".
- a new pipeline (e.g., test-env CD) to deploy to Azure → add a federated credential block scoped to that environment.

Anything that looks like "we'll just put a secret in Key Vault and reference it from the container env" is a regression. Raise it with the maintainers before writing it.
