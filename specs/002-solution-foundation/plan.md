# Implementation Plan: Solution Foundation

**Branch**: `feature/002-solution-foundation` | **Date**: 2026-05-16 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/002-solution-foundation/spec.md`

---

## Summary

Establish the deployable application foundation for BusTerminal: a Next.js 16 frontend shell (consuming the slice-001 design system), a .NET 10 ASP.NET Core Minimal API backend, deployed to Azure Container Apps in a `dev` environment provisioned end-to-end via OpenTofu. Identity flows through Microsoft Entra ID (interactive sign-in for users, managed identity for workloads, OIDC workload-identity federation for the pipeline). Observability flows through OpenTelemetry into Application Insights + Log Analytics, with W3C Trace Context propagating end-to-end. The slice ships a working authenticated `whoami` round-trip, a navigation shell, and a CI/CD pipeline that deploys it all without a human running `az` by hand.

`test` and `prod` are scaffolded as parameter-file templates and folder structure only — the *pattern* is proven, the resources are not provisioned in this slice.

The technical approach below maps every spec FR to an approved choice in `speckit-artifacts/tech-stack.md`. No new technologies are introduced.

---

## Technical Context

**Language/Version**:
- Frontend: TypeScript (strict mode) on Node.js 22 LTS
- Backend: C# 13 / .NET 10 (target framework `net10.0`)
- IaC: OpenTofu ≥ 1.10 (HCL)

**Primary Dependencies**:
- Frontend (inherited from slice 001 where applicable): Next.js 16, React 19, Tailwind CSS v4, `shadcn/ui` (project-owned in `web/components/ui`), `next-themes`, `@tanstack/react-query`, `react-hook-form`, `zod`, `lucide-react`, `clsx`, `tailwind-merge`, `class-variance-authority`. Added by this slice: **NextAuth.js v5 (Auth.js)** with the **Microsoft Entra ID** provider for App Router sign-in, plus the App Insights **JavaScript SDK** wired behind the existing pluggable observability adapter from slice 001.
- Backend: `Microsoft.Identity.Web` (Entra ID JWT validation for Minimal APIs), `Azure.Monitor.OpenTelemetry.AspNetCore` (Azure Monitor distro for OpenTelemetry — ASP.NET Core + HttpClient instrumentation), `Serilog.AspNetCore` + `Serilog.Sinks.OpenTelemetry` (structured logging, routed through the OTel pipeline so logs/traces/metrics share one exporter), `Microsoft.AspNetCore.OpenApi` (built-in .NET 10 OpenAPI generation), `Azure.Identity` (managed identity / DefaultAzureCredential), `Microsoft.Extensions.Configuration.AzureKeyVault` (Key Vault references).
- IaC: AzureRM provider for OpenTofu, pinned. Modules sourced from **Azure Verified Modules (AVM)** Terraform registry where coverage exists; hand-authored where it doesn't. All module versions pinned.

**Storage**: N/A in this slice. The foundation does not provision Cosmos DB or AI Search; those land in a later slice. OpenTofu state is stored in an Azure Storage Account provisioned by a one-time `platform-bootstrap` module (see FR-082a) — local developer workflows use local state.

**Testing**:
- Frontend: Vitest + React Testing Library (component) — inherited from slice 001; Playwright (smoke E2E covering sign-in + platform-status); axe (a11y gate)
- Backend: xUnit + `WebApplicationFactory` for integration tests against the in-memory test host; FluentAssertions for assertion ergonomics
- IaC: `tofu validate` + `tofu plan` in CI; `tflint` for static analysis; `checkov` for security scan
- Pipeline: `gitleaks` for secret scanning on every PR

**Target Platform**:
- Runtime: Linux containers on Azure Container Apps (Consumption plan, scale-to-zero permitted)
- Dev shells: PowerShell (primary, per CLAUDE.md) and bash (secondary)
- Developer OS: macOS, Windows, Linux all supported via the documented prerequisites

**Project Type**: Web application (frontend + backend) plus infrastructure as code.

**Performance Goals**:
- Frontend Core Web Vitals "Good": LCP ≤ 2.5s, INP ≤ 200ms, CLS ≤ 0.1 (constitution-bound)
- Backend p95 latency on `/whoami` ≤ 200ms warm; cold-start budget ≤ 5s (Container Apps scale-from-zero on a small-memory instance)
- Pipeline: full no-infrastructure-change deploy in ≤ 20 minutes (SC-002)
- Telemetry latency: requests visible in Application Insights within 2 minutes (SC-003)

**Constraints**:
- All Azure diagnostic logs route to the solution's single Log Analytics Workspace (constitution-bound)
- W3C Trace Context (`traceparent`/`tracestate`) on every UI-originated HTTP request (constitution-bound)
- No PII in default telemetry; only correlation identifiers propagate (FR-073)
- No secrets in source, container images, IaC variable defaults, or pipeline configuration (FR-051, gitleaks-enforced)
- Pipeline → Azure auth via OIDC workload identity federation only — no client secrets (FR-043)
- Workload → Azure auth via managed identity only (FR-042)
- OpenTofu only; Bicep prohibited without an ADR (constitution-bound)
- Backend ingress is external-but-token-protected (per spec clarification 2026-05-16, Q2)

**Scale/Scope**:
- `dev` environment sized for foundation use: ~10 internal users, < 10 RPS, single region (East US 2 unless project default overrides)
- Container Apps: `minReplicas=0`, `maxReplicas=3` for both workloads in `dev` (cost-optimized; tunable per environment)
- AVM modules will provision a single resource group per environment, isolating state and identities

---

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-evaluated after Phase 1 design (below).*

| Principle | Status | Evidence |
|-----------|--------|----------|
| **I. Azure-First Architecture** | ✅ Pass | Hosting (Container Apps), identity (Entra ID), secrets (Key Vault), observability (Azure Monitor + App Insights + Log Analytics), CR (ACR) are all Azure-native. AVM-first IaC. |
| **II. API-First Design** | ✅ Pass | Backend exposes OpenAPI via .NET 10's built-in `Microsoft.AspNetCore.OpenApi`; `/whoami` is documented in `contracts/`. UI calls the same public contract — no UI-only backdoors. |
| **III. Strong Domain Modeling** | ⚪ N/A | This slice introduces no domain entities (foundation only). The constraint applies starting from the registry-domain slices. |
| **IV. Security by Default** | ⚠️ Pass with documented deviation | Managed identities for workloads (FR-042), OIDC federation for pipeline (FR-043), Key Vault references for secrets (FR-050), no embedded credentials (FR-051), Entra ID enforced for users (FR-040), secret scanning in CI (FR-052). **Documented deviation:** Principle IV says "private networking MUST be preferred wherever feasible". Per spec clarification 2026-05-16 (Q2), the backend has external ingress with mandatory Entra-token validation. See Complexity Tracking below for justification. |
| **V. Operational Excellence** | ✅ Pass | Structured logging (Serilog → OTel), distributed tracing (OpenTelemetry + Azure Monitor distro), metrics, three health endpoints per workload (FR-063), correlation IDs end-to-end, dashboards via Azure Monitor workbooks, no silent retries (telemetry surfaces failures). |
| **VI. Incremental Extensibility** | ✅ Pass | Vertical slice arch for backend, App Router for frontend, modular OpenTofu, env scaffolding for `test`/`prod`, pluggable observability adapter inherited from slice 001, frontend→backend supports both server- and client-side calling patterns. |
| **Modular Monolith First** | ✅ Pass | One backend process, one frontend process. No premature decomposition. |
| **Container-Native** | ✅ Pass | Both workloads ship as containers; local containerized dev supported alongside native run. |
| **Async-First** | ⚪ N/A | No async workflows in this slice. Pattern preserved for later (Container Apps Jobs + Functions on Container Apps documented as future support). |
| **CI/CD Requirements** | ✅ Pass | Pipeline includes build, unit tests, lint, format, secret scan (gitleaks), dependency scan (`dotnet list package --vulnerable`, `pnpm audit`), container scan (Trivy), `tofu validate`/`plan`, `tflint`, `checkov`. |
| **Testing Standards** | ✅ Pass | Unit (Vitest, xUnit), integration (`WebApplicationFactory`), contract (OpenAPI schema diff), UI component (Vitest + RTL), E2E smoke (Playwright). |
| **AI Tooling / MCP Usage** | ✅ Pass | All planning decisions grounded in MS Learn MCP; future implementation tasks will cite Next.js MCP, shadcn/ui MCP, context7 MCP where their domains apply. |

**Gate decision**: PASS. One documented deviation (backend external ingress) is tracked in Complexity Tracking. All other principles satisfied or N/A.

---

## Project Structure

### Documentation (this feature)

```text
specs/002-solution-foundation/
├── plan.md              # This file (/speckit-plan output)
├── research.md          # Phase 0 output (this run)
├── data-model.md        # Phase 1 output (this run)
├── quickstart.md        # Phase 1 output (this run)
├── contracts/           # Phase 1 output (this run)
│   └── whoami.openapi.yaml
└── tasks.md             # NOT created here — /speckit-tasks output
```

### Source Code (repository root)

```text
/web/                       # Next.js frontend (established by slice 001; extended here)
  app/
    layout.tsx              # Existing — extended with NavigationShell wrapper
    page.tsx                # Existing — redirects to /platform-status when authenticated
    (auth)/
      signin/
        page.tsx            # NEW — Entra ID sign-in entry
      signout/
        page.tsx            # NEW — sign-out confirmation
    (authenticated)/
      layout.tsx            # NEW — requires session; renders NavigationShell
      platform-status/
        page.tsx            # NEW — calls backend /whoami, displays identity + correlation id
    api/
      auth/[...nextauth]/
        route.ts            # NEW — NextAuth.js handler
  components/
    layout/
      navigation-shell.tsx  # NEW — header (logo, theme toggle, user menu) + sidebar placeholder
      user-menu.tsx         # NEW — signed-in user dropdown with sign-out
    ui/                     # Existing — slice-001 shadcn primitives
  lib/
    auth.ts                 # NEW — NextAuth.js config (Entra ID provider, token callback)
    api-client.ts           # NEW — typed fetch wrapper attaching token + traceparent
    telemetry/              # Existing — slice-001 observability adapter; AI adapter added here
      ai-adapter.ts         # NEW — App Insights JS SDK adapter implementation
      trace-context.ts      # NEW — outbound traceparent/tracestate header generator
  Dockerfile                # NEW — multi-stage Next.js standalone build

/api/                       # NEW — .NET 10 backend
  BusTerminal.Api/
    Program.cs              # Minimal API host, auth + OTel + health setup
    Features/               # Vertical slice arch
      Identity/
        WhoAmIEndpoint.cs   # GET /whoami — returns calling principal + correlation id
      Health/
        HealthEndpoints.cs  # /healthz/live, /healthz/ready, /healthz/startup
    Infrastructure/
      Authentication/
        AuthenticationExtensions.cs  # AddMicrosoftIdentityWebApi wiring
      Observability/
        OpenTelemetryExtensions.cs   # Azure Monitor distro wiring
      Configuration/
        KeyVaultExtensions.cs        # Key Vault reference wiring
    appsettings.json
    appsettings.Development.json     # Local-only, gitignored secrets blank
  BusTerminal.Api.Tests/
    Integration/
      WhoAmIEndpointTests.cs
      HealthEndpointTests.cs
    Unit/
  BusTerminal.sln
  Dockerfile                # NEW — multi-stage chiseled .NET 10 runtime

/iac/                       # NEW — OpenTofu infrastructure
  platform-bootstrap/       # One-time module — see FR-082a
    main.tf                 # State storage account, federated identity, RBAC
    variables.tf
    outputs.tf
    README.md               # Step-by-step "how to use this module" + manual fallback
  modules/                  # Reusable composables
    identity/               # Managed identities + role assignments
    container-apps-env/     # Container Apps Environment + Log Analytics binding
    container-app/          # A single container app (frontend or backend)
    monitoring/             # App Insights + Log Analytics + diagnostic settings
    keyvault/               # Key Vault + access policies via RBAC
    container-registry/     # ACR + AcrPull assignments
  environments/
    dev/
      main.tf               # Wires modules for dev
      backend.tf            # Remote state backend config (dev partition)
      terraform.tfvars      # Dev-specific values (NO secrets)
      variables.tf
    test/
      backend.tf            # Template only — pattern proven
      terraform.tfvars.example
      README.md             # "How to provision this environment"
    prod/
      backend.tf
      terraform.tfvars.example
      README.md
  README.md                 # IaC overview + execution order

/.github/
  workflows/
    ci.yml                  # NEW — on every PR: build, test, lint, scan
    cd-dev.yml              # NEW — on main: build images, push to ACR, tofu apply dev, deploy revisions
    iac-validate.yml        # NEW — tofu fmt/validate/plan on IaC changes
  dependabot.yml            # NEW — dependency update PRs

/scripts/
  bootstrap.ps1             # NEW — install prereqs check + register dev creds
  bootstrap.sh              # NEW — bash variant
  start-local.ps1           # NEW — runs frontend + backend concurrently
  start-local.sh
  start-local-containers.sh # NEW — docker compose up of containerized variant
  bootstrap-platform.ps1    # NEW — runs the platform-bootstrap tofu module
  bootstrap-platform-manual.md  # NEW — az CLI walkthrough alternative

/docs/
  local-development.md      # NEW — onboarding guide (SC-001 target)
  deploying-environments.md # NEW — how to deploy / add a new environment (SC-006 target)
  observability.md          # NEW — what's logged, how to trace a request
  identity-and-secrets.md   # NEW — Entra setup, Key Vault references, federated creds
  architecture.md           # NEW — high-level diagram + decision references

/docker-compose.yml         # NEW — optional containerized local stack
```

**Structure Decision**:

The slice-001 work already established `web/` at the repository root for the Next.js frontend (with brand tokens, shadcn primitives, Vitest, Playwright, axe, Storybook). This plan **inherits** that layout rather than relocating it to `/src/frontend` as the source artifact suggested — moving working slice-001 code would be churn for no benefit and would force every existing developer to re-learn the layout.

The backend lands at `/api/` (NOT `/src/backend`) to mirror the established convention: each workload lives in a top-level directory named after its role. Infrastructure goes to `/iac/` (NOT `/infrastructure/opentofu/`) for the same reason — top-level, role-named, no extra nesting since OpenTofu is the only IaC tool.

This deviates from the source artifact's `/src/frontend`, `/src/backend`, `/infrastructure/opentofu/` layout. The deviation is *intentional* and consistent with constitutional Decision Priority #1 (operational simplicity): a flatter, role-keyed layout is easier to navigate than a deeper, category-keyed one, and slice 001's `web/` location is already documented in CLAUDE.md.

---

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|--------------------------------------|
| Backend has external public ingress (deviation from Principle IV's "private networking preferred wherever feasible") | The foundation slice does not provision a virtual network or private DNS. Adding VNet integration, private endpoints, and internal DNS for the Container Apps Environment more than doubles the IaC surface for this slice and pushes the bootstrap chicken-and-egg further (private networking requires a private DNS resolver to be reachable from the pipeline, which is itself outside Azure). The mitigation in place — mandatory Entra ID token validation on every request (FR-062, FR-032) — provides authentication at the network edge before any application logic runs. Decision Priority #1 (operational simplicity) and #2 (developer productivity) outweigh defense-in-depth at this slice. | Internal-only ingress was the *recommended* answer to spec clarification Q2 and was explicitly rejected by the user in favor of external-with-token. A later slice (when a domain API surface exists and may carry sensitive data) can flip the backend's `ingress.external` to `false` and add VNet integration as a focused, isolated change without re-architecting anything in this slice. The design supports the flip (FR-062 mandates the backend's ingress posture be configurable per environment). |

No other constitutional deviations.

---

## Phase 0 (research.md) — completed in this run

See [research.md](./research.md) for the resolved-decision record. Summary of the 12 research topics covered:

1. Frontend auth library for Next.js 16 App Router + Entra ID → **NextAuth.js v5 (Auth.js)** with the Microsoft Entra ID provider.
2. Backend auth library for .NET 10 Minimal APIs + Entra ID → **Microsoft.Identity.Web** via `AddMicrosoftIdentityWebApi`.
3. .NET telemetry stack → **`Azure.Monitor.OpenTelemetry.AspNetCore`** (the Azure Monitor distro for OpenTelemetry) + Serilog routed through the OTel logging pipeline.
4. Frontend telemetry → existing slice-001 pluggable adapter; **App Insights JS SDK** activated when the connection-string env var is set. W3C Trace Context propagation is *always* on (FR-022).
5. IaC modules → **Azure Verified Modules (Terraform variant)** preferred where coverage exists, hand-authored where it doesn't; all versions pinned.
6. OpenTofu state backend → one-time `platform-bootstrap` module provisioning a dedicated storage account + federated identity, **with documented manual `az` CLI alternative** (spec clarification Q4).
7. GitHub Actions → Azure auth → **OIDC workload identity federation** to a per-environment user-assigned managed identity. No client secrets anywhere.
8. Container build → multi-stage `Dockerfile`s: chiseled .NET 10 runtime for backend; Next.js standalone output for frontend.
9. Health probe pattern → ASP.NET Core `HealthChecks` with three distinct route names (`/healthz/live`, `/healthz/ready`, `/healthz/startup`) mapped to Container Apps `liveness`/`readiness`/`startup` probe types respectively.
10. Local hot-reload → `dotnet watch` + `next dev` run concurrently via `scripts/start-local.*`; container-based variant via `docker compose up`. No reverse-proxy needed locally (frontend reads backend base URL from env).
11. Secret scanning → **gitleaks** in CI on every PR; pre-commit hook documented but not enforced.
12. Azure Functions on Container Apps Environment → confirmed GA hosting model (no functions ship in this slice; documented for future support per FR future-considerations).

---

## Phase 1 — completed in this run

- **data-model.md**: documents the five platform constructs from the spec (Environment, Workload, Identity Configuration, Pipeline Run, Telemetry Stream) at logical-design granularity. No domain entities (this slice has none).
- **contracts/whoami.openapi.yaml**: OpenAPI 3.1 fragment defining the `GET /whoami` endpoint that the frontend's platform-status page calls. This is the single public surface this slice ships.
- **quickstart.md**: developer onboarding plus first deployment, sized to meet SC-001 (under 30 min, local) and SC-002 (under 20 min, pipeline).

Agent context update: `CLAUDE.md`'s SPECKIT-marked block now points to this plan (`specs/002-solution-foundation/plan.md`).

---

## Post-Phase-1 Constitution Re-Check

| Principle | Status After Design |
|-----------|---------------------|
| I. Azure-First | ✅ Confirmed — design uses Azure-native services exclusively. |
| II. API-First | ✅ Confirmed — `contracts/whoami.openapi.yaml` is the public contract; the frontend platform-status page consumes it; no UI-only backdoor. |
| III. Strong Domain Modeling | ⚪ N/A — confirmed no domain entities introduced. |
| IV. Security by Default | ⚠️ Confirmed — one documented deviation (Complexity Tracking) only. All other security FRs intact. |
| V. Operational Excellence | ✅ Confirmed — three health endpoints, structured logging, OTel traces, App Insights/Log Analytics destination, correlation IDs end-to-end. |
| VI. Incremental Extensibility | ✅ Confirmed — env scaffolding for `test`/`prod` is in place (folder + backend.tf templates); IaC is modular; the pluggable observability adapter supports future sinks; the backend can flip to internal ingress in a later slice without breaking the design. |

No new violations introduced by Phase 1 design. Plan is ready for `/speckit-tasks`.
