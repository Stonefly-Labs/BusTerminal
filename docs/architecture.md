# BusTerminal — Architecture Overview

**Status**: Living document. Updated per slice; latest update: slice 003 (auth-and-identity).

This document is a thin, durable architectural map of BusTerminal. The constitution (`.specify/memory/constitution.md`) is the governing source of design principles; the tech-stack reference (`speckit-artifacts/tech-stack.md`) is the source of approved technologies. This document sketches *how the pieces fit together* and links to the deep-dive docs.

---

## Component map

| Component | Where | Talks to |
|---|---|---|
| **Web SPA** (Next.js 16, App Router, RSC) | `web/` | `Web API` (bearer-authenticated REST) |
| **Web API** (.NET 10, ASP.NET Core Minimal APIs) | `api/BusTerminal.Api/` | Entra ID (token validation), Microsoft Graph (`IGraphClient`), Azure Key Vault, future Cosmos / AI Search / Service Bus |
| **Container Apps Jobs** (background) | future | Same Azure dependencies as the API |
| **Container Apps Functions** (event-driven) | future | Same |
| **Infrastructure** (OpenTofu) | `iac/` | Azure Resource Manager, Microsoft Graph (for app-registration / role / federated-credential writes) |

All compute is Azure Container Apps hosted under a single ACA Environment per environment (`dev`, `test`, `prod`). All Azure SDK access from compute uses `DefaultAzureCredential` resolving to the workload's user-assigned managed identity (UAMI).

---

## Identity & Authorization

This section was added by slice 003 (`specs/003-auth-and-identity`). The authoritative reference for the rules summarized here is the binding role-permission matrix at [`specs/003-auth-and-identity/contracts/role-permission-matrix.md`](../specs/003-auth-and-identity/contracts/role-permission-matrix.md).

```
┌─────────────────┐     1. MSAL Auth-Code + PKCE      ┌──────────────┐
│  Browser SPA    │ ─────────────────────────────────▶│   Entra ID   │
│  @azure/msal-*  │ ◀────── id_token + access_token ──│   (tenant)   │
└────────┬────────┘                                   └──────┬───────┘
         │                                                   │
         │ 2. Authorization: Bearer <access_token>           │ 4. JWKS metadata
         │    traceparent: 00-<trace>-<span>-01              │
         ▼                                                   │
┌─────────────────┐     3. ValidateBearerToken          ┌────▼───────┐
│  ASP.NET Core   │ ◀────────────────────────────────── │  Microsoft │
│  Minimal API    │       (Microsoft.Identity.Web)      │  Identity  │
│                 │ ─── 5. roles claim → policies ────▶ │  metadata  │
│  Authorization  │     (CanRead, CanMutateDomain,      └────────────┘
│  policies       │      CanOperatePlatform,
│                 │      CanAdminister,
│                 │      CanUseDeveloperTooling)
└────────┬────────┘
         │
         │ 6. IAzureCredentialFactory.CreateCredential()
         │    DefaultAzureCredential (UAMI in cloud, az-cli locally)
         ▼
┌─────────────────────────────────────────────────────────────────────┐
│  Azure resources: Key Vault • Microsoft Graph (User.Read.All) •     │
│  future Cosmos DB / AI Search / Service Bus                         │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────┐     A. UAMI client-credentials      ┌──────────────┐
│  Workload (CA   │ ─────────────────────────────────▶  │   Entra ID   │
│  Job / Function │ ◀──── access_token (api scope) ──── │              │
│  / Internal CA) │                                     └──────────────┘
└────────┬────────┘
         │
         │ B. Authorization: Bearer <access_token>   (FR-012)
         │    traceparent: <propagated or new>
         ▼
┌─────────────────┐
│  ASP.NET Core   │  ← same validation + policy pipeline as the SPA flow.
│  Minimal API    │    There is no internal-trust bypass — workloads
└─────────────────┘    are first-class principals (callerType=Workload).
```

**Flow A** is human sign-in. The SPA authenticates the user via MSAL (Authorization Code + PKCE — no client secret); on every API call it attaches the access token plus a W3C `traceparent` so the backend trace correlates with the browser-originated span. The backend validates the token via `Microsoft.Identity.Web`, projects the `roles` claim into a `PlatformPrincipal.EffectiveRoles` set, and authorization policies map operation classes to roles per the matrix.

**Flow B** is workload-to-API. Background jobs, functions, and other internal callers acquire an access token using their **own** UAMI's client-credentials grant against the API's scope (`api://<api-app>/.default`). The backend authenticates them identically to a human caller — same Microsoft.Identity.Web pipeline, same policies — the only differences are the resolved `callerType` (`Workload`) and the assigned app role (the UAMI is granted exactly the role its operation requires).

**Outbound Azure access** (Key Vault, Graph, future Cosmos / Search / Service Bus) is always brokered through `IAzureCredentialFactory.CreateCredential()` returning `DefaultAzureCredential`. In a deployed Container App the credential chain resolves to `ManagedIdentityCredential` against the UAMI; in local dev it resolves to `AzureCliCredential` against the developer's `az login` session. **No service-principal-with-secret, no SAS token, no connection-string-with-key.**

For the full breakdown of credential mechanisms, role administration, and Graph permissions:

- [`identity-and-secrets.md`](./identity-and-secrets.md) — the four credential mechanisms (UAMI, OIDC federation, `DefaultAzureCredential`, MSAL).
- [`identity-role-administration.md`](./identity-role-administration.md) — operator runbook for granting / removing roles and bootstrapping the first Admin.
- [`identity-graph-permissions.md`](./identity-graph-permissions.md) — Microsoft Graph permission inventory + admin-consent procedure.
- [`internal-workload-callers.md`](./internal-workload-callers.md) — how internal workloads acquire tokens and how the API treats them.

---

## Cross-cutting concerns

- **Observability** — all backend telemetry (logs, traces, metrics) ships via OpenTelemetry → Azure Monitor / Application Insights. All Azure resources route diagnostic logs to the solution's Log Analytics Workspace. Frontend uses a pluggable observability adapter with W3C Trace Context propagation enforced on every UI-originated HTTP request, regardless of adapter configuration.
- **Secrets** — only Azure Key Vault. No secrets in source, no secrets in container env. The backend resolves Key Vault secrets at boot via `DefaultAzureCredential`. CI runs `gitleaks` on every change.
- **CI/CD** — GitHub Actions with OIDC federation to Azure. No long-lived deployment credentials.

---

## Where to read next

- [Constitution](../.specify/memory/constitution.md) — governing principles. Architectural deviations require an ADR.
- [Tech Stack Reference](../speckit-artifacts/tech-stack.md) — approved technology and library matrix.
- Active spec at [`specs/`](../specs/) (per [`.specify/feature.json`](../.specify/feature.json)) — the in-progress slice.
