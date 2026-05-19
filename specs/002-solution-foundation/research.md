# Phase 0 Research: Solution Foundation

**Feature**: 002-solution-foundation
**Date**: 2026-05-16
**Sources consulted**: Microsoft Learn MCP (Azure, .NET, Entra ID, AVM, Functions on Container Apps), Tech Stack Reference (`speckit-artifacts/tech-stack.md`), Constitution (`.specify/memory/constitution.md`), Slice 001 design foundation.

Each decision below is recorded as: **Decision** / **Rationale** / **Alternatives considered**. Phase 0's job is to leave zero NEEDS CLARIFICATION markers in the Technical Context above; all twelve research topics below resolve to a concrete decision.

---

## 1. Frontend authentication library (Next.js 16 + Microsoft Entra ID)

**Decision**: **NextAuth.js v5 (Auth.js)** with the Microsoft Entra ID provider.

**Rationale**:
- Auth.js v5 is the canonical App Router-compatible auth library. It supports React Server Components (sessions readable in server components via `auth()`), middleware-based route protection, and the `signIn`/`signOut` server actions that match Next.js 16 conventions.
- Entra ID is a first-class provider (`@auth/core/providers/microsoft-entra-id`) — minimal custom code to wire up.
- Token-callback hooks let us attach the access token to `session.accessToken` for server-side `fetch` to the backend (the typed `api-client.ts` wrapper picks it up).
- Inherits the slice-001 design system's accessibility posture — sign-in pages are styled components, not framework chrome.

**Alternatives considered**:
- **MSAL React** (`@azure/msal-react`) — written for SPA/client-component model; works against App Router only with manual session bridging. Rejected because it forces a client-component shell around server-rendered pages, undoing the App Router's server-by-default benefit.
- **Hand-rolled OIDC flow** — unnecessary complexity for the foundation slice; reinvents what Auth.js already ships.
- **`next-auth` v4** — superseded by v5/Auth.js for App Router; no reason to start on a legacy line.

---

## 2. Backend authentication library (.NET 10 Minimal APIs + Microsoft Entra ID)

**Decision**: **`Microsoft.Identity.Web`** via `builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"))`, with endpoints protected by `.RequireAuthorization()` and a `RequireAuthenticatedUser()` global policy.

**Rationale**:
- This is the Microsoft-canonical path for an ASP.NET Core Web API protected by Entra ID (confirmed via MS Learn MCP — multiple authoritative docs reference exactly this pattern).
- Handles JWT signing-key rotation, multi-tenant configuration, and issuer/audience validation out of the box.
- Composes cleanly with Minimal APIs: `app.MapGet("/whoami", (HttpContext ctx) => ...).RequireAuthorization();` is the entire wiring.
- Library is actively maintained and required for any future "OBO" (on-behalf-of) flow if we later call Microsoft Graph from the backend.

**Alternatives considered**:
- **`Microsoft.AspNetCore.Authentication.JwtBearer` directly** — viable but forces us to hand-author Entra-specific config (metadata address, valid issuers, audience matching). `Microsoft.Identity.Web` wraps this with sane defaults. Rejected as needless boilerplate.
- **Custom token validation handler** — rejected outright per the MS Learn guidance: "Never implement your own token validation code."

---

## 3. .NET telemetry stack (logs + traces + metrics → Azure Monitor)

**Decision**: **`Azure.Monitor.OpenTelemetry.AspNetCore`** (the Azure Monitor distro for OpenTelemetry) registered via `builder.Services.AddOpenTelemetry().UseAzureMonitor();`. Serilog is configured as the logging framework and routed *into* the OTel logging pipeline via `Serilog.Sinks.OpenTelemetry` so that logs, traces, and metrics share a single exporter and correlation context.

The Application Insights connection string is provided via the `APPLICATIONINSIGHTS_CONNECTION_STRING` environment variable, which Container Apps injects as a Key Vault reference (managed-identity-backed).

**Rationale**:
- The Azure Monitor distro includes built-in instrumentation for ASP.NET Core, HttpClient, and SQL — exactly what this slice needs (and aligns with the constitution-mandated "OpenTelemetry for Azure Monitor").
- W3C Trace Context propagation is built into the underlying .NET `Activity` API, so inbound `traceparent` headers from the frontend automatically become the parent span on the backend with no extra config.
- Centralized exporter avoids the trap of "logs go to App Insights via Serilog, traces go via OTel" with two disjoint correlation models.

**Alternatives considered**:
- **Application Insights SDK (legacy)** — superseded by the OTel distro; constitution explicitly mandates OpenTelemetry for Azure Monitor.
- **Pure OpenTelemetry SDK + Azure Monitor exporter** — possible, but reimplements what the distro already wraps. Rejected as unnecessary configuration surface.
- **Serilog → App Insights sink directly (bypassing OTel)** — would split log correlation from trace correlation. Rejected.

---

## 4. Frontend telemetry (browser observability adapter + W3C trace propagation)

**Decision**: Reuse the **pluggable observability adapter** established by slice 001. Add an **Application Insights JavaScript SDK** adapter (`@microsoft/applicationinsights-web` + `@microsoft/applicationinsights-react-js`) that activates *only* when `NEXT_PUBLIC_APPINSIGHTS_CONNECTION_STRING` is present. The default in local dev remains the no-op adapter.

W3C Trace Context propagation is **always on**, regardless of which adapter is active (per FR-022 and constitutional mandate). A small `trace-context.ts` helper generates `traceparent` (and optionally `tracestate`) headers for every outbound `fetch` from the typed `api-client.ts`, derived from the active client-side `Activity` if one exists, or generated fresh otherwise.

**Rationale**:
- Honors the slice-001 contract (adapter is pluggable, no-op by default).
- Honors the constitutional mandate that trace context propagation is independent of the active adapter — a contributor running locally with the no-op adapter still emits valid `traceparent` headers that the backend will pick up.
- App Insights JS SDK is the canonical browser-side tool for the Azure Monitor stack.

**Alternatives considered**:
- **OpenTelemetry Web SDK directly + OTLP collector** — viable but adds a deployable collector to the foundation slice scope. Rejected for operational simplicity at this slice; the App Insights JS SDK exports straight to Application Insights via HTTPS.
- **No W3C propagation in the no-op adapter** — rejected because it violates the constitution and breaks end-to-end correlation as soon as anyone toggles telemetry on.

---

## 5. IaC modules: Azure Verified Modules vs. hand-authored

**Decision**: **AVM-Terraform-first**. For each Azure resource type, check whether an AVM Terraform module exists with adequate coverage; if yes, use it pinned to an explicit version. If no (or coverage is insufficient for our config), hand-author a thin module under `iac/modules/`. Document each deviation in the module's `README.md`.

**Concrete sourcing plan** (initial):
- Container Apps Environment → `Azure/avm-res-app-managedenvironment/azurerm`
- Container App → `Azure/avm-res-app-containerapp/azurerm`
- User-Assigned Managed Identity → `Azure/avm-res-managedidentity-userassignedidentity/azurerm`
- Key Vault → `Azure/avm-res-keyvault-vault/azurerm`
- Log Analytics Workspace → `Azure/avm-res-operationalinsights-workspace/azurerm`
- Application Insights → `Azure/avm-res-insights-component/azurerm`
- Container Registry → `Azure/avm-res-containerregistry-registry/azurerm`

(Exact module names confirmed at implementation time; planning assumes AVM coverage exists for all of the above based on the current AVM Terraform module catalog. Where coverage is missing or the module forces an unwanted abstraction, fall through to a hand-authored module.)

**Rationale**:
- Constitution mandates AVM-preferred sourcing with pinned versions.
- AVM modules carry WAF-aligned defaults (diagnostic settings, RBAC patterns, naming conventions) — work we'd otherwise reinvent.
- OpenTofu can consume Terraform Registry modules unchanged (it's a Terraform fork at the module-protocol level).

**Alternatives considered**:
- **Hand-author everything** — rejected. Reinvents WAF-aligned defaults and adds maintenance burden.
- **Bicep registry modules** — prohibited by constitution.
- **Pull-down AVMs into the repo and modify** — rejected. Forks the upstream module and complicates upgrades. Use AVMs by reference, hand-author only the gaps.

---

## 6. OpenTofu remote-state backend bootstrap

**Decision** (mandated by spec clarification 2026-05-16, Q4): A dedicated one-time `iac/platform-bootstrap/` OpenTofu module provisions:
- A dedicated resource group (e.g., `rg-busterminal-tfstate`)
- An Azure Storage Account with versioning + soft-delete enabled
- A blob container named `tfstate`
- A user-assigned managed identity for the GitHub Actions pipeline
- A federated identity credential on that managed identity, scoped to `repo:<org>/BusTerminal:environment:<env>` per environment
- RBAC role assignments granting the pipeline identity `Contributor` over the dev subscription scope and `Storage Blob Data Contributor` over the tfstate storage account

The bootstrap module's *own* state lives locally (run with `-state-out=local.tfstate` documented in its README) and is not committed. After bootstrap, every other environment's state lives in the storage account that bootstrap created. State is partitioned by environment via the `key` attribute on the AzureRM backend (`key = "envs/dev/terraform.tfstate"`, etc.).

**Documentation requirement** (FR-082b): The bootstrap module's README MUST describe the equivalent **manual `az` CLI procedure** so a developer who'd rather not run OpenTofu against their own subscription can achieve the same backend.

**Rationale**:
- Spec clarification explicitly chose this hybrid approach (module + manual fallback). Local dev workflows use local state and don't depend on the remote backend (FR-082).
- The "bootstrap is itself OpenTofu" path keeps the team in a single mental model rather than mixing imperative shell-out with declarative IaC.
- Partitioning by `key` keeps the storage account count to one across all environments — minimum operational surface for state.

**Alternatives considered**:
- **All-bash bootstrap script (no OpenTofu)** — simpler but inconsistent with the rest of the IaC. Rejected.
- **State in repo, gitignored** — viable for `dev` only but doesn't generalize to `test`/`prod` and creates a "single-developer footgun" anyway. Rejected.
- **Documented portal walkthrough only (no script)** — rejected because FR-082a explicitly requires the module.

---

## 7. GitHub Actions → Azure authentication

**Decision**: **OIDC workload identity federation** to a **user-assigned managed identity** per environment, configured via the `azure/login@v2` action with `auth-type: IDENTITY` and the workflow-level permission `id-token: write`.

Federation subject pattern: `repo:<org>/BusTerminal:environment:<envname>`. Each GitHub deployment environment (`dev`, eventually `test`, `prod`) maps to a distinct managed identity with environment-scoped permissions. Pull-request branches do NOT have deploy credentials — only `pull_request` triggers for read-only operations like `tofu validate`/`plan`.

**Rationale**:
- Constitution and FR-043 mandate OIDC federation with no static credentials.
- User-assigned managed identities (over Entra application registrations) are preferred per MS Learn guidance because they're simpler to manage in Azure-only scenarios and inherit Azure RBAC directly.
- Per-environment identities give us a natural least-privilege boundary — the `dev` deploy identity has no permissions in `test` or `prod`, which matches FR-100's environment isolation requirement.

**Alternatives considered**:
- **App registration with client secret in GitHub Secrets** — explicitly prohibited (FR-043, constitution).
- **App registration with OIDC federation** — workable; rejected in favor of user-assigned managed identity for the simpler management model.
- **Single identity across all environments** — violates FR-100 (environment isolation). Rejected.

---

## 8. Container build strategy

**Decision**:

- **Backend (.NET 10)**: multi-stage `Dockerfile` based on `mcr.microsoft.com/dotnet/sdk:10.0` for the build stage and `mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled` for the runtime stage. Chiseled images are minimal (≤ 50 MB final image), have no shell, and ship only the runtime — they pass the constitution's "secure by default" posture and shrink attack surface.
- **Frontend (Next.js 16)**: multi-stage `Dockerfile` using `node:22-alpine` for the build stage with `next build --turbopack` and `next.config.ts` set to `output: 'standalone'`. The final stage copies the standalone output and `.next/static` into a minimal `node:22-alpine` runtime image and runs `node server.js`.

Both Dockerfiles set non-root users, explicit `EXPOSE` ports, and standard `HEALTHCHECK` directives. Image labels include source commit SHA (passed as build arg) for telemetry correlation.

**Rationale**:
- Chiseled .NET images are the Microsoft-recommended runtime for production .NET on Linux containers — smaller, safer, faster cold-starts.
- Next.js standalone output is the canonical production deployment shape for App Router — bundles only the runtime dependencies, no `node_modules` bloat.
- Alpine base for the Next.js runtime is small and well-supported.

**Alternatives considered**:
- **Single-stage builds** — produces images with the SDK present in production. Rejected.
- **Distroless instead of chiseled** — distroless is Google's equivalent; chiseled is the Microsoft-published variant. We prefer chiseled because it's Microsoft-maintained and the Container Apps platform team validates compatibility.
- **`next start` from a full `node_modules`** — works but ships ~10× the image size. Rejected.

---

## 9. Health probe pattern for Container Apps

**Decision**: Use ASP.NET Core's built-in `HealthChecks` middleware, registering three distinct probe routes:

- `GET /healthz/live` → trivial liveness check (process is up, event loop responds). No dependency checks.
- `GET /healthz/ready` → readiness check that exercises lightweight dependencies (e.g., Entra ID metadata reachable, Key Vault reachable). Returns 503 when not ready.
- `GET /healthz/startup` → startup check that returns 200 only after initial config load and first JWT signing-key fetch complete. Container Apps uses this with a longer `initialDelay` to give the app time to warm up.

Container Apps probes are configured per workload to map to these endpoints with the matching `liveness`/`readiness`/`startup` probe `type`.

The frontend exposes equivalents via Next.js route handlers under `/healthz/{live,ready,startup}` returning JSON.

**Rationale**:
- Three distinct endpoints match Container Apps' three probe types (per the AVM `container-app` module schema), giving the platform precise signals.
- ASP.NET Core HealthChecks is the standard idiom; no custom infrastructure required.
- Startup probe specifically prevents premature liveness failures during cold start, which would otherwise loop-kill the container under scale-from-zero.

**Alternatives considered**:
- **Single `/healthz` endpoint** — works but loses the distinction Container Apps actually uses for different probe behaviors. Rejected.
- **Custom probe implementation** — unnecessary; the framework provides the right shape.

---

## 10. Local hot-reload + containerized variant

**Decision**:

- **Native local stack** (primary): `scripts/start-local.{ps1,sh}` spawns two child processes — `dotnet watch` for the backend and `pnpm dev` (Next.js dev server with turbopack) for the frontend. Both produce structured logs to the developer's terminal. Frontend reads `NEXT_PUBLIC_API_BASE_URL` (default `http://localhost:5000`) so it knows where to find the backend.
- **Containerized local stack** (optional, opt-in): `docker-compose.yml` at the repo root builds both Dockerfiles and runs them together with the same port mapping. `scripts/start-local-containers.sh` is a thin wrapper. This gives parity with the Container Apps runtime when debugging container-specific issues.

Neither local mode requires a real Entra ID tenant; the dev-time NextAuth.js config supports a "mock provider" fallback that returns a synthetic principal when the Entra config env vars are absent. The backend, when run with `ASPNETCORE_ENVIRONMENT=Development` and `AzureAd:TenantId=development`, similarly accepts an unsigned dev token shape rather than requiring real Entra metadata. This is gated to non-Production environments only.

**Rationale**:
- Two modes serve two purposes: native is fastest for inner-loop work; containerized is fastest for catching container-specific bugs before they hit Azure.
- The mock-auth dev fallback satisfies FR-012 (local dev MUST NOT require live Azure resources).

**Alternatives considered**:
- **`tilt` or `skaffold`** — heavier tooling than this slice needs. Rejected for operational simplicity.
- **Mandatory containerized local stack** — would slow inner-loop development significantly. Rejected.
- **Aspire** — viable but introduces additional framework dependency. The constitution allows "Aspire-compatible service defaults where useful" but does not require Aspire. Deferred to a later slice if/when its orchestration value justifies it.

---

## 11. Secret scanning + dependency scanning in CI

**Decision**:

- **Secret scanning**: `gitleaks` (GitHub Action `gitleaks/gitleaks-action@v2`) on every PR and on push to `main`. Custom `.gitleaks.toml` allows the documented patterns we *do* commit (e.g., placeholder values like `<your-tenant-id>` in docs).
- **Dependency vulnerability scanning**:
  - Backend: `dotnet list package --vulnerable --include-transitive` step in CI; fails the build on `High` or `Critical` findings.
  - Frontend: `pnpm audit --audit-level high` step in CI; fails the build on `high` or `critical` findings.
- **Container scanning**: Trivy (`aquasecurity/trivy-action`) on each built image in CI; fails on `HIGH` or `CRITICAL` vulnerabilities in the runtime image layers.

A documented (but not yet enforced) pre-commit hook for `gitleaks --staged` is shipped in `scripts/` so contributors can catch issues locally before pushing.

**Rationale**:
- Constitution requires secret scanning, SCA, and dependency hygiene — gitleaks + Trivy + native package vulnerability tools cover the three axes with minimum new tool surface.
- Failing on `High`+ matches the standard industry posture for foundation work; `Medium` can be raised in a future hardening slice.

**Alternatives considered**:
- **TruffleHog** instead of gitleaks — equivalent capability; gitleaks chosen for its simpler config and faster GitHub Action.
- **GitHub-native secret scanning only** — provides alerts but does not block merges by default. Rejected as the primary gate.
- **Snyk / Dependabot for SCA** — Dependabot is enabled separately for dependency *updates*; vulnerability *gating* remains in CI to keep the policy explicit and run reproducibly.

---

## 12. Future support: Azure Functions on Container Apps Environment

**Decision** (informational — no code in this slice): The constitution mandates that future event-driven processing use **Azure Functions on the Container Apps Environment** (the GA hosting model that runs containerized Functions inside an existing Container Apps Environment with KEDA-based scaling and full VNet integration). This slice does *not* deploy any Functions, but the design choices are made so adding them later is purely additive:

- The Container Apps Environment provisioned by `iac/modules/container-apps-env/` is the same environment Functions will deploy into.
- The managed identity and Key Vault references model is the same.
- The Log Analytics destination is the same.

No additional IaC scaffolding is added in this slice for Functions specifically; the patterns are documented in `docs/architecture.md` as future-state.

**Rationale**:
- Confirmed via MS Learn MCP that Functions on Container Apps is GA and supports the constitution's stated requirements (scale-to-zero via KEDA, secure networking, custom containers, unified environment with microservices/APIs/jobs).
- Adding Functions infra now would violate the YAGNI principle and pollute the slice scope. Documenting the path is sufficient.

**Alternatives considered**:
- **Provision an empty Functions placeholder now** — rejected; no consumer.
- **Flex Consumption plan** — also GA, but the constitution explicitly directs us to the Container Apps hosting model for unified environments. No deviation needed.
