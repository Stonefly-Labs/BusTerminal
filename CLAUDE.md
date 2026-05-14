<!-- SPECKIT START -->
For additional context about technologies to be used, project structure,
shell commands, and other important information, read the current plan at
`specs/001-brand-system-and-design-foundation/plan.md`.
<!-- SPECKIT END -->

# BusTerminal — Project Context

BusTerminal is an open-source operational registry, discovery, governance, and observability platform for **Azure Service Bus** messaging infrastructure. It is the authoritative source for messaging topology — namespaces, queues, topics, subscriptions, rules, forwarding relationships, ownership metadata, contracts/schemas, and dependency graphs — built for the engineers, architects, and operators who run Azure messaging at scale.

The architecture is Azure-first but preserves optionality to extend to other messaging systems later (Constitution Principle VI — Incremental Extensibility).

---

## Authoritative Documents

Read these in priority order when you need project context:

1. **Constitution** — `.specify/memory/constitution.md` — governing principles, technology standards, and engineering workflow. **All decisions must align with this.** Deviations require an ADR.
2. **Tech Stack Reference** — `speckit-artifacts/tech-stack.md` — single-page consolidation of every approved technology, library, and infrastructure choice. **Cite this in prompts** rather than restating the stack.
3. **Active feature spec** — `.specify/feature.json` points to the current spec under `specs/`. The current spec is the source of truth for the feature you're working on.
4. **Source artifacts** — `speckit-artifacts/` holds the foundational input documents (constitution draft, brand foundation, future foundational specs) used by spec-kit.

---

## How We Work

- **Spec-Driven Development.** All significant functionality starts with a spec. The spec-kit flow is: `/speckit-specify` → `/speckit-clarify` → `/speckit-plan` → `/speckit-tasks` → `/speckit-implement`.
- **Feature branches**: `feature/<NNN>-<slug>` (e.g., `feature/001-brand-system-and-design-foundation`).
- **Spec directories**: `specs/<NNN>-<slug>/` with sequential numbering. The branch name and the spec directory name are independent.
- **One feature per branch.** Don't blend feature work into the foundation branch or another feature's branch.
- **Auto-commit hooks** are configured via speckit extensions and prompt before committing rather than acting silently.
- **PR review verifies constitution compliance.** Architectural deviations without an ADR are treated as defects.
- **Platform**: Windows + PowerShell is the primary dev shell; Bash is also available.
- **Planning**: When performing planning tasks, including speckit-plan, referecne /speckit-artifacts/tech-stack.md.

---

## Tech Stack — Quick Reference

The full matrix is in `speckit-artifacts/tech-stack.md`. The highlights:

**Backend** — .NET 10, C#, ASP.NET Core, **Minimal APIs preferred** (Controllers only when Minimal APIs are infeasible), Vertical Slice Architecture, OpenAPI for every public surface, built-in DI container.

**Frontend** — Next.js 16.x (**App Router only**), React Server Components by default, TypeScript strict, Tailwind CSS v4.x, **shadcn/ui as project-owned source** (not a black-box dependency), lucide-react, TanStack Table, React Hook Form + Zod, Recharts, Framer Motion (sparingly), next-themes. Dark mode is the primary operational experience.

**Data** — Azure Cosmos DB (metadata), Azure AI Search (discovery/indexing).

**Hosting** — Azure Container Apps + ACR; Container Apps Jobs for background; **containerized Azure Functions on the Container Apps Environment** for event-driven processing (use the newest native Azure Functions for Container Apps hosting).

**Infrastructure-as-Code** — **OpenTofu**. Azure Verified Modules preferred with pinned versions.

**Identity** — Microsoft Entra ID; **Managed Identity preferred over secrets**; RBAC least-privilege.

**Secrets** — Azure Key Vault. **Never** commit secrets.

**Observability** — Azure Monitor + Application Insights + OpenTelemetry for Azure Monitor. All Azure services route diagnostic logs to the solution's Log Analytics Workspace. Frontend uses a pluggable observability adapter (no-op default + AI browser adapter via env-gated connection string). **W3C Trace Context (`traceparent`/`tracestate`) propagation on every UI-originated HTTP request is mandatory** regardless of adapter configuration.

**Testing** — Playwright (E2E), Vitest + React Testing Library (component), axe (a11y) — runnable locally and in CI.

**Accessibility** — WCAG 2.2 AA minimum, AAA where practical. Semantic HTML first; ARIA only to fill semantic gaps. Respect `prefers-reduced-motion`. Never rely on color alone for meaning.

**Browser support** — Last **two major versions** of Chrome/Edge/Firefox/Safari (desktop) + iPadOS Safari + Android Chrome.

**Performance** — Core Web Vitals "Good": LCP ≤ 2.5s, INP ≤ 200ms, CLS ≤ 0.1 on a representative composed screen.

**i18n (v1)** — English content, RTL-safe foundation (CSS logical properties only), externalized strings, locale-aware date/number/duration formatting. Translation pipeline deferred.

---

## Easily-Forgotten Rules

These are the rules most likely to bite if forgotten — keep them top of mind.

- **OpenTofu**, not Bicep. (Bicep requires an ADR exception.)
- **Minimal APIs**, not Controllers. (Controllers require justification.)
- **App Router only**, not Pages Router. (Pages Router is prohibited.)
- **No CSS-in-JS.** Tailwind v4 + CSS variables only.
- **No second design system.** shadcn/ui (project-owned) is the baseline.
- **No additional UI libraries** without explicit approval — no alternative component libraries, heavy chart suites, graph/topology libraries, drag-and-drop libraries, rich-text editors, or code-editor components.
- **MCP servers are dev-time only.** Next.js MCP, shadcn/ui MCP, Microsoft Learn MCP, context7 MCP are workflow aids for humans and agents. The **product** must NEVER depend on an MCP server at runtime. Use phrasing like *"Coding agents must consult…"*, not *"BusTerminal integrates with…"*.
- **Managed Identity preferred over secrets** for service-to-service auth.
- **W3C Trace Context propagation is mandatory** on UI-originated HTTP requests so frontend traces correlate with backend OpenTelemetry traces in Azure Monitor.
- **No PII in telemetry by default.** Only correlation identifiers propagate unless an explicit opt-in is added by a future spec.
- **CSS logical properties only.** No hardcoded `left`/`right`. RTL-safe by construction even though v1 content is English-only.
- **Dark mode is primary**, light mode is a fully-supported peer (not a skin).
- **All Azure diagnostic logs** must route to the solution's Log Analytics Workspace.

---

## Decision Priorities (when trading off)

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

## Non-Goals (Foundational Phase)

Out of scope at the foundational phase and **must not drive current design**:

- Full enterprise ESB functionality
- Message brokering / transport replacement
- Runtime traffic interception
- Full multi-cloud abstraction layers
- Multi-tenant SaaS architecture
- Complex workflow orchestration
- Legacy on-premise broker management

These may be revisited in later phases but are not foundational requirements.

---

## When You're Unsure

- If a tech choice isn't in `speckit-artifacts/tech-stack.md`, it isn't approved by default — justify against the Decision Priorities and capture an ADR for material additions.
- If the spec conflicts with the constitution, the **constitution wins**.
- If the tech-stack reference conflicts with the constitution, the **constitution wins** (and `tech-stack.md` is out of date — fix it).
- If a feature spec adds a new durable rule (like the brand foundation's W3C Trace Context requirement), **update `tech-stack.md`** so future features inherit it.
