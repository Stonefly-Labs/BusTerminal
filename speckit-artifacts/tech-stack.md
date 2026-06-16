# BusTerminal Tech Stack Reference

**Status**: Draft v1.1
**Last Updated**: 2026-06-04
**Authority**: Derived from the [BusTerminal Constitution v1.0.0](../.specify/memory/constitution.md). Deviations require an Architectural Decision Record (ADR).
**Companion artifacts**: [`busterminal-constitution.md`](./busterminal-constitution.md), [`001-brand-system-and-design-foundation.md`](./001-brand-system-and-design-foundation.md)

This document is a single-page reference of the technologies, frameworks, libraries, and infrastructure choices that govern BusTerminal work. It exists to be cited from future spec prompts, plan reviews, and agentic-coding workflows so that stack decisions don't have to be re-explained or re-litigated per task.

If a technology is **not** listed here, it is not approved by default. Introducing a new dependency requires justification against the Constitution's Decision Priorities and (for material additions) an ADR.

---

## 1. Backend

| Area | Standard | Notes |
|---|---|---|
| Runtime / Language | **.NET 10**, **C#** | Modern C# features encouraged: primary constructors, collection expressions, required members, pattern matching, records, file-scoped namespaces, typed results, async streams, nullable reference types. |
| Web framework | **ASP.NET Core** | |
| API style | **Minimal APIs preferred** | Controllers are used only when Minimal APIs are infeasible. |
| Architecture | **Vertical Slice Architecture preferred** | Horizontal layering is discouraged. |
| Dependency Injection | **Built-in .NET DI container** | Third-party containers only with documented justification. |
| API contracts | **OpenAPI** | Generated and maintained for every public API surface (Constitution Principle II — API-First). |
| HTTP request validation | **FluentValidation 11.10.x** | Boundary validation on API endpoints (HTTP request DTOs). Complements the spec-004 `ValidationEngine` (canonical-domain validation at write time). Invoked manually from Minimal-API handlers — `AddFluentValidationAutoValidation` is MVC-only. Introduced by spec 006. |
| Azure ARM management plane | **`Azure.ResourceManager.ServiceBus` 1.x** | Authenticated via `DefaultAzureCredential` (workload UAMI). Used by spec 008's namespace-onboarding validation runner for Existence / Accessibility / RequiredPermissions / IdentityAuthorization / ApiReachability checks. Authoritative source for cross-tenant guard via subscription→tenant resolution. **No connection strings or SAS tokens** — FR-017 / FR-033 / SC-007 forbid them across the namespace-onboarding surface. |
| State | **Stateless services where practical** | |

**Cross-cutting rules**

- Favor composition over inheritance.
- Minimize hidden magic; prefer explicitness over convention-heavy frameworks.
- Separate domain logic from infrastructure concerns.
- Emphasize testability — domain logic must be exercisable without infrastructure.
- Unnecessary abstraction layers and framework complexity are prohibited.

---

## 2. Frontend

| Area | Standard | Notes |
|---|---|---|
| Framework | **Next.js 16.x** | App Router only. Pages Router is prohibited. |
| React model | **React Server Components by default** | Client Components are explicit, scoped narrowly, and only used where interactivity, browser APIs, local state, or event handlers are required. |
| Language | **TypeScript, strict mode** | |
| Styling | **Tailwind CSS v4.x** | No CSS-in-JS. No second design system. Tokens live in Tailwind/theme config and CSS variables. |
| Component foundation | **shadcn/ui** | Project-owned source code, not a black-box dependency. Generated components live in `components/ui` and are themed, reviewed, and adapted to BusTerminal standards. |
| Icons | **lucide-react** | Single iconography family across the product. |
| Data tables | **TanStack Table** | All product tables route through the foundation's table primitives — no raw or per-feature tables. |
| Forms | **React Hook Form** | Default form engine for all product forms. |
| Validation | **Zod** | Default schema/validation library. |
| Server state / data fetching | **TanStack Query 5.x** | Used for interactive surfaces (search-box debounced typeahead, create/edit form mutations with conflict UX, audit-panel client-side refresh). RSC + `fetch` on the server remains the default for read-only routes. Devtools is dev-only. Introduced by spec 006. |
| Charts | **Recharts** | Standard charts (line, bar, area, donut, small trends). Topology/graph visualization libraries are deferred to a dedicated future decision. |
| Animation | **Framer Motion** | Used sparingly; motion must clarify state, not provide theatre. Respect `prefers-reduced-motion`. |
| Theme management | **next-themes** (or lightweight equivalent) | Dark and light themes as first-class peers. Dark is the primary operational experience. |
| Class utilities | **clsx**, **tailwind-merge**, **class-variance-authority** | Provide the `cn()`-style class merging utility and variant definition utility used project-wide. |
| Component docs | **Storybook or equivalent** | Decision deferred to implementation plan, but a working docs system is required. |
| Browser baseline | Last **two major versions** of evergreen Chrome, Edge, Firefox, Safari on desktop + iPadOS Safari and Android Chrome | IE, legacy non-Chromium Edge, and older embedded webviews are out of scope. Modern CSS (`:has()`, `color-mix()`, `@property`, container queries, OKLCH, CSS nesting) is permitted without polyfills. |
| Logical layout | **CSS logical properties only** | No hardcoded `left`/`right`. RTL-safe by construction even though v1 content is English-only. |
| i18n posture (v1) | **English content, i18n-ready foundation** | User-facing strings externalized, locale-aware date/number/duration formatting, full translation pipeline deferred to a later spec. |
| Performance budget | **Core Web Vitals "Good"**: LCP ≤ 2.5s, INP ≤ 200ms, CLS ≤ 0.1 | Plus a documented soft initial-JS bundle target for the application shell. |

**Cross-cutting rules**

- The UI is a product feature, not a thin client. It must be information-dense, fast, accessible, dark-mode complete, keyboard-friendly, and visually consistent.
- Strongly type API interactions.
- Prefer composition over wrapper-heavy abstractions.
- No additional UI libraries (alternative component libraries, CSS-in-JS, heavy chart suites, graph/topology libraries, drag-and-drop, rich-text editors, code editors) without explicit approval.

---

## 3. Accessibility

| Area | Standard |
|---|---|
| Compliance target | **WCAG 2.2 AA minimum**; prefer AAA where practical |
| Keyboard | Full keyboard operability, visible focus, logical tab order, no traps |
| Screen reader | Semantic HTML first; ARIA only to fill semantic gaps |
| Motion | Respect `prefers-reduced-motion`; motion clarifies state, not theatre |
| Color | Never rely on color alone for meaning — icons and text accompany semantic states |
| Tooling | Automated axe (or equivalent) checks gate the foundation's CI |

---

## 4. Frontend Observability

| Area | Standard |
|---|---|
| Adapter pattern | **Pluggable observability adapter**. Default = no-op for OSS contributors; Application Insights browser adapter activated via env-gated connection string. |
| Error reporting | Top-level error boundary forwards unhandled rendering errors (with React component stack) through the adapter. User sees an on-brand, accessible error surface. |
| Web Vitals | LCP, INP, CLS, TTFB, FCP captured per page load and forwarded through the adapter. |
| Tracing | Route navigations emit trace spans. **W3C Trace Context** (`traceparent`/`tracestate`) headers propagate on **every** UI-originated HTTP request — required regardless of adapter configuration, so frontend and backend correlate end-to-end. |
| Privacy | PII is **not** captured in trace attributes, error payloads, or Web Vitals events by default. Only correlation identifiers propagate unless an explicit opt-in is added by a future spec. |

---

## 5. Data Platform

| Area | Standard | Notes |
|---|---|---|
| Metadata storage | **Azure Cosmos DB** | Denormalization is acceptable where beneficial. The Cosmos **change feed** (latest-version mode) is the supported event source for indexing/projection pipelines; **lease containers must be IaC-provisioned** under managed-identity auth (the change-feed trigger cannot create lease containers when running under MI because container creation is a management-plane operation). |
| Search / discovery | **Azure AI Search** | Searchability is a first-class concern. |
| Schema evolution | Backward-compatible migrations preferred | Breaking schema changes require an ADR. |
| Partitioning | Must preserve future scalability | |
| Lineage | Metadata lineage must remain traceable | |

---

## 6. Hosting & Infrastructure

| Area | Standard | Notes |
|---|---|---|
| Application hosting | **Azure Container Apps** | All services containerized. |
| Image registry | **Azure Container Registry** | |
| Secrets | **Azure Key Vault** | Secrets must never be committed to source. Managed identity preferred over secret-based auth (Constitution Principle IV). |
| Background jobs | **Azure Container Apps Jobs** | |
| Event-driven processing | **Containerized Azure Functions** on the Container Apps Environment | Use the **newest native Azure Functions for Container Apps hosting** — the **v2 native model**: a single `Microsoft.App/containerApps` resource with `kind = "functionapp"`. The legacy **v1 model** (the `Microsoft.Web/sites` proxy with a hidden backing container app) is **prohibited for new workloads**; existing v1 workloads must migrate per [`learn.microsoft.com/azure/container-apps/migrate-functions`](https://learn.microsoft.com/azure/container-apps/migrate-functions). |
| Observability backbone | **Azure Monitor** + **Application Insights** + **OpenTelemetry for Azure Monitor** | All Azure services must route diagnostic logs to the solution's Log Analytics Workspace. |
| Infrastructure-as-Code | **OpenTofu** (required) | **Bicep is prohibited** unless approved by ADR-recorded exception. |
| IaC modules | **Azure Verified Modules (AVM) preferred** | Versions pinned explicitly. Deviations documented in spec or ADR. Exceptions allowed when AVM lacks coverage, blocks secure networking, or introduces unreasonable complexity. |
| IaC diagnostics convention | **`allLogs`-only diagnostic forwarding** via the project's `iac/modules/diagnostic-settings` wrapper. | Every Azure resource emits a single `enabled_log` block with `category_group = "allLogs"` and **no `enabled_metric` block**. Metrics stay in Azure Monitor's native metric store. Forwarding `AllMetrics` to LAW is prohibited (BT-IAC-003). Originated in spec 005 (Q5c). |
| IaC policy gates | **BT-IAC-001..007** custom rules (`iac/policies/run-policies.sh`) plus checkov + tfsec. | Gate enumeration: BT-IAC-001 (workload UAMI role-assignment allowlist per FR-033) · BT-IAC-002 (private-by-default for prod) · BT-IAC-003 (`allLogs`-only diagnostics) · BT-IAC-004 (no inline IAM in env compositions) · BT-IAC-005 (no inline credentials) · BT-IAC-006 (`.terraform.lock.hcl` drift) · BT-IAC-007 (stateful-destroy manual approval). Allowlist with per-skip rationale lives in `iac/policies/allowlist.json`. |
| IaC module docs | **`terraform-docs` inject mode** is the CI formatting gate. | Each `iac/modules/<m>/README.md` carries `<!-- BEGIN_TF_DOCS -->` / `<!-- END_TF_DOCS -->` markers; `iac/.terraform-docs.yml` drives recursive injection. CI fails on drift (`fail-on-diff: true`). Locally regenerate with `terraform-docs -c iac/.terraform-docs.yml iac`. |
| IaC env posture (Q2c) | **Dev**: `data_services_public_access_enabled = true` + `private_endpoints_enabled = true` (warm PEs, public-on). **Test/prod**: public access disabled by default. | Originated in spec 005. Net effect: dev keeps CI-runner reachability; test/prod enforce private-by-default via BT-IAC-002. |
| Stateful resource protection | `lifecycle { prevent_destroy = true }` on directly-owned stateful resources; BT-IAC-007 manual-approval gate is the primary CI defense; AVM-wrapped stateful resources documented in `iac/environments/dev/README.md`. | Stateful resource list lives in `specs/005-infrastructure-baseline/data-model.md` §3. |
| Environment parity | Strongly preferred | Reproducible deployments mandatory. |
| Networking defaults | Secure-by-default; private networking preferred where feasible | Per-env `private_endpoints_enabled` toggle. Networking module provisions VNet + integration subnet + PE subnet + per-service `privatelink.*` DNS zones; the project's `private-endpoint` wrapper binds PEs to the right subresource per service. |

---

## 7. Identity & Authentication

| Area | Standard |
|---|---|
| Identity platform | **Microsoft Entra ID** |
| Human sign-in (SPA) | **MSAL** (`@azure/msal-browser` 4.x + `@azure/msal-react` 3.x), Authorization Code + PKCE. No client secret. (FR-003 / slice 003) |
| Backend token validation | **Microsoft.Identity.Web** JWT bearer on the API; rejects unauthenticated calls with `401 + WWW-Authenticate: Bearer`. |
| Service-to-service auth | **Managed Identity preferred over secrets** — user-assigned managed identity (UAMI) per workload; same UAMI is used for workload→API calls (no internal-trust bypass). (FR-012) |
| Pipeline-to-Azure auth | **OIDC federated credentials** (GitHub Actions → short-lived JWT exchanged for an Entra token). No long-lived secrets. (FR-015) |
| Backend Azure-SDK credentials | **`DefaultAzureCredential`** resolved through a single factory (`IAzureCredentialFactory`). Never construct inline. (FR-018) |
| Access control | **RBAC, least-privilege.** Backend authorization is role-based via app roles on the API app registration. The role-permission matrix at `specs/003-auth-and-identity/contracts/role-permission-matrix.md` is the **binding contract** mapping `BusTerminal.Reader` / `BusTerminal.Developer` / `BusTerminal.Operator` / `BusTerminal.Admin` to the five operation classes (`Read`, `MutateDomain`, `OperatePlatform`, `Administer`, `DeveloperTooling`). Future protected endpoints **must** declare an operation class and align with this matrix; the matrix is the authoritative source if the implementation (`api/BusTerminal.Api/Authorization/RolePolicies.cs`) ever drifts. |
| Microsoft Graph access | App-only flow via `IGraphClient`; permissions declared in `iac/modules/graph-permissions/`; admin consent is **manual** and out-of-band per environment. Permission inventory: `docs/identity-graph-permissions.md`. (FR-024) |
| Embedded credentials | **Prohibited.** No client secrets, no SAS tokens, no connection strings with embedded keys in source or in container env. CI runs `gitleaks` on every change. |
| Local development | `az login` once against the BusTerminal dev tenant; `DefaultAzureCredential` resolves the developer's `AzureCliCredential`. MSAL signs in to the **real** dev tenant — no frontend mock provider (FR-018, removed by slice 003). |

---

## 8. Engineering Workflow & Quality

| Area | Standard |
|---|---|
| Spec-driven development | All significant functionality begins with a spec (Constitution requires it). |
| CI gates | Build, unit tests, lint, format, security scanning, dependency vulnerability scanning. Main branch continuously deployable. |
| IaC CI gates | `tofu fmt -check -recursive` · `tofu validate` · `tofu plan` (PR comment) · `tflint --recursive` · `checkov` (source + plan frameworks) · `tfsec` · custom **BT-IAC-001..007** policy rules · `terraform-docs --output-check` (module README drift) · `iac-stateful-change-approval` manual-approval job (BT-IAC-007). |
| Testing strategy | Unit, integration, contract, UI component, and end-to-end smoke. Tests assert observable behavior, not implementation detail. |
| E2E | **Playwright** |
| Component tests | **Vitest** + **React Testing Library**; **axe** for accessibility |
| Source control | Trunk-based with feature branches per spec (`feature/<NNN>-<slug>` convention currently in use). |

---

## 9. AI Tooling / MCP Servers (Development-Time Only)

MCP servers are **engineering workflow standards, not runtime requirements**. The product MUST NOT depend on any MCP server being reachable at runtime.

| Server | Purpose |
|---|---|
| **Next.js MCP** | Framework conventions, routing, rendering, caching, app architecture |
| **shadcn/ui MCP** | Component installation, registry usage, component patterns |
| **Microsoft Learn MCP** | Azure and Microsoft platform guidance |
| **context7 MCP** | Current library documentation and examples |

Spec/plan/task language must use phrasing like *"Coding agents must consult…"*, *"Frontend implementation workflows should use…"*, *"Agentic development should reference…"* — and must avoid phrasing like *"BusTerminal integrates with MCP servers"* or *"The application depends on MCP servers"*.

---

## 10. Architecture Constraints

| Constraint | Notes |
|---|---|
| **Modular monolith first** | Microservices only when justified by scaling, ownership, deployment independence, or operational isolation. Premature decomposition is prohibited. |
| **Container-native** | All services run in containers, support local containerized dev, reproducible builds, CI/CD-friendly. |
| **Async-first thinking** | Especially for discovery, indexing, topology scanning, telemetry ingestion, AI enrichment. |
| **AI features must be explainable** | Semantic search, AI-assisted discovery, topology summarization, documentation generation, operational insights, governance recommendations — opaque "black box" AI behavior is prohibited in user-facing capabilities. |
| **Open-source community readiness** | Reproducible local dev, contributor-friendly onboarding, transparent ADRs, public issue tracking, automated validation. |

---

## 11. Non-Goals (Foundational Phase)

The following are explicitly **out of scope** at the foundational phase and must not shape current architectural decisions:

- Full enterprise ESB functionality
- Message brokering
- Message transport replacement
- Runtime traffic interception
- Full multi-cloud abstraction layers
- Multi-tenant SaaS architecture
- Complex workflow orchestration
- Legacy on-premise broker management

They may be revisited in later phases but must not drive foundational design.

---

## 12. Decision Priorities (in order)

When evaluating a technical decision or trade-off, prefer in this order:

1. Operational simplicity
2. Developer productivity
3. Maintainability
4. Security
5. Observability
6. Extensibility
7. Performance
8. Cost efficiency

Premature optimization is prohibited. Pragmatic engineering is favored over theoretical purity.

---

## 13. How to Reference This Document

When writing a future spec, plan, or task prompt, prefer phrasing like:

> *"Use the BusTerminal tech stack as defined in `speckit-artifacts/tech-stack.md` — specifically section <N>."*

This keeps prompts short and ensures coding agents inherit the full set of constraints, including the non-obvious ones (OpenTofu over Bicep, Minimal APIs over Controllers, dark mode primary, W3C Trace Context propagation, RTL-safe by construction, MCP servers as dev-time tools, etc.).

When deviating from this document:

1. Justify the deviation against the Decision Priorities (section 12).
2. Record an ADR under `docs/adr/` (location pending — see Constitution Sync Impact Report TODO).
3. Update this document if the deviation becomes the new standard.
