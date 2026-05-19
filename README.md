# BusTerminal

**An open-source operational registry, discovery, governance, and
observability platform for Azure Service Bus messaging infrastructure.**

BusTerminal is the authoritative source for messaging topology —
namespaces, queues, topics, subscriptions, rules, forwarding
relationships, ownership metadata, contracts / schemas, and dependency
graphs — built for the engineers, architects, and operators who run
Azure Service Bus at scale.

The architecture is Azure-first but preserves optionality to extend to
other messaging systems in later phases (see the constitution,
Principle VI — Incremental Extensibility).

> **Status**: pre-1.0. The frontend foundation (spec 001) is the first
> in-progress slice. The product is not yet feature-complete.

---

## What's in this repo today

| Path | What lives here |
|---|---|
| [`web/`](./web) | Next.js 16.x App Router frontend foundation — design tokens, themed shadcn/ui primitives, TanStack Table foundation, RHF + Zod form foundation, Recharts wrappers, i18n surface, observability adapter with W3C Trace Context propagation, brand assets. |
| [`api/`](./api) | .NET 10 ASP.NET Core Minimal API backend (`BusTerminal.Api`) and its xUnit test project (`BusTerminal.Api.Tests`). Solution file: `BusTerminal.slnx`. |
| [`iac/`](./iac) | OpenTofu infrastructure-as-code — platform bootstrap, reusable modules, and per-environment compositions (`dev`, `test`, `prod`). |
| [`.github/workflows/`](./.github/workflows) | GitHub Actions pipelines — CI on PRs and CD on `main`. |
| [`scripts/`](./scripts) | Developer and platform helper scripts (`bootstrap.{ps1,sh}` for prerequisite checks; local-dev and platform-bootstrap helpers as they land). |
| [`docs/`](./docs) | Onboarding, deployment, observability, identity, and architecture documentation. |
| [`specs/`](./specs) | Spec-Driven Development feature specs. Spec 002 (solution foundation) is the active feature. |
| [`speckit-artifacts/`](./speckit-artifacts) | Foundational input documents — the constitution draft, brand foundation, tech-stack reference, and future foundational specs. |
| [`.specify/`](./.specify) | Spec-Driven Development tooling — the constitution, templates, hooks, and the active-feature pointer. |
| [`CLAUDE.md`](./CLAUDE.md) | Project context for coding agents. |

For the canonical onboarding walkthrough — local dev, first deploy,
and prerequisite versions — see
[`specs/002-solution-foundation/quickstart.md`](./specs/002-solution-foundation/quickstart.md).

---

## Authoritative documents

Read these in priority order when you need context:

1. **[Constitution](./.specify/memory/constitution.md)** — governing
   principles, technology standards, and engineering workflow. **All
   decisions must align with this.** Deviations require an ADR.
2. **[Tech Stack Reference](./speckit-artifacts/tech-stack.md)** —
   single-page consolidation of every approved technology, library,
   and infrastructure choice.
3. **[Active feature spec](./.specify/feature.json)** — points to the
   current spec under `specs/`. Today: spec 002 — Solution Foundation.
4. **[Source artifacts](./speckit-artifacts/)** — foundational input
   documents.

---

## Technology stack (highlights)

The full matrix is in [tech-stack.md](./speckit-artifacts/tech-stack.md).
Topline:

- **Backend** — .NET 10, C#, ASP.NET Core, Minimal APIs preferred,
  Vertical Slice Architecture, OpenAPI on every public surface.
- **Frontend** — Next.js 16.x (App Router only), React Server
  Components by default, TypeScript strict, Tailwind CSS v4.x,
  shadcn/ui as project-owned source, lucide-react, TanStack Table,
  React Hook Form + Zod, Recharts, Framer Motion (sparingly),
  next-themes. Dark mode is the primary operational experience.
- **Data** — Azure Cosmos DB (metadata), Azure AI Search (discovery /
  indexing).
- **Hosting** — Azure Container Apps + ACR; Container Apps Jobs for
  background; containerized Azure Functions on the Container Apps
  Environment for event-driven processing.
- **Infrastructure-as-Code** — OpenTofu (not Bicep). Azure Verified
  Modules preferred with pinned versions.
- **Identity** — Microsoft Entra ID; Managed Identity preferred over
  secrets; RBAC least-privilege.
- **Secrets** — Azure Key Vault. Never commit secrets.
- **Observability** — Azure Monitor + Application Insights +
  OpenTelemetry. All Azure services route diagnostic logs to the
  solution's Log Analytics Workspace. Frontend uses a pluggable
  observability adapter (no-op default + Application Insights browser
  adapter via env-gated connection string). **W3C Trace Context
  propagation on every UI-originated HTTP request is mandatory**
  regardless of adapter configuration.
- **Testing** — Playwright (E2E), Vitest + React Testing Library
  (component), axe (a11y) — runnable locally and in CI.

---

## How we work

- **Spec-Driven Development.** All significant functionality starts
  with a spec. The spec-kit flow is `/speckit-specify` →
  `/speckit-clarify` → `/speckit-plan` → `/speckit-tasks` →
  `/speckit-implement`.
- **Feature branches**: `feature/<NNN>-<slug>` (e.g.,
  `feature/001-brand-system-and-design-foundation`).
- **Spec directories**: `specs/<NNN>-<slug>/` with sequential
  numbering. The branch name and the spec directory name are
  independent.
- **One feature per branch.** No blending feature work into the
  foundation branch or another feature's branch.
- **PR review verifies constitution compliance.** Architectural
  deviations without an ADR are treated as defects.
- **Windows + PowerShell** is the primary dev shell; Bash is also
  available.

---

## Quickstart (frontend)

The frontend foundation lives under `web/`.

```bash
# Install
pnpm install

# Run the app at http://localhost:3000
pnpm -C web dev

# Run the foundation Storybook at http://localhost:6006
pnpm -C web storybook

# Run the full local quality gate
pnpm -C web lint
pnpm -C web typecheck
pnpm -C web audit:tokens
pnpm -C web audit:strings
pnpm -C web audit:directions
pnpm -C web test
pnpm -C web test:storybook
pnpm -C web test:e2e
pnpm -C web analyze
```

A more thorough walkthrough — including the backend, IaC, and first
deploy — lives in
[`specs/002-solution-foundation/quickstart.md`](./specs/002-solution-foundation/quickstart.md).
For the original frontend-only foundation walkthrough, see
[`specs/001-brand-system-and-design-foundation/quickstart.md`](./specs/001-brand-system-and-design-foundation/quickstart.md).

For agentic-coding rules and MCP server conventions, see
[`web/docs/agentic-coding.md`](./web/docs/agentic-coding.md) and
[`CLAUDE.md`](./CLAUDE.md).

---

## Non-goals (foundational phase)

Out of scope and **must not drive current design**:

- Full enterprise ESB functionality
- Message brokering / transport replacement
- Runtime traffic interception
- Full multi-cloud abstraction layers
- Multi-tenant SaaS architecture
- Complex workflow orchestration
- Legacy on-premise broker management

These may be revisited in later phases but are not foundational
requirements.

---

## License

Open-source. The licensing decision and `LICENSE` file are tracked
under spec 001's brand asset originality / licensing review pipeline
(FR-002a / FR-002b).
