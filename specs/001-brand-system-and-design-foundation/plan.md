# Implementation Plan: Brand System and Design Foundation

**Branch**: `feature/001-brand-system-and-design-foundation` | **Date**: 2026-05-14 | **Spec**: [`spec.md`](./spec.md)

**Input**: Feature specification at [`specs/001-brand-system-and-design-foundation/spec.md`](./spec.md)

**Authoritative inputs cited by this plan**:

- [BusTerminal Constitution v1.0.0](../../.specify/memory/constitution.md)
- [Tech Stack Reference](../../speckit-artifacts/tech-stack.md)
- [Spec 001 Source Artifact](../../speckit-artifacts/001-brand-system-and-design-foundation.md)
- [Phase 0 Research](./research.md)
- [Phase 1 Data Model](./data-model.md)
- [Phase 1 Contracts](./contracts/)
- [Phase 1 Quickstart](./quickstart.md)

## Summary

Establish the BusTerminal frontend foundation as a Next.js 16.x App Router application under `web/` containing: a complete design-token system with dark/light themes, a project-owned shadcn/ui primitive library, BusTerminal-specific domain composites, a TanStack Table foundation, a React Hook Form + Zod form foundation, a Recharts-based chart primitive layer, an i18n-ready string surface with locale-aware formatting, a pluggable observability adapter (no-op default + Application Insights browser adapter), mandatory W3C Trace Context propagation on every UI-originated HTTP request, an operational Storybook component documentation system, a published soft initial-JS bundle target, a brand asset pipeline with required human originality/licensing review for AI-assisted assets, and the full lint/test/a11y/e2e/bundle-analyzer quality-gate stack. The deliverable unblocks all future feature specs by providing a complete, accessible, performant, observable, RTL-safe, dark-mode-primary UI kit that human contributors and coding agents can compose without authoring bespoke styling.

## Technical Context

**Language / Version**: TypeScript (strict mode); Node.js 20.x LTS for build / tooling; targeting ES2022 output via the Next.js compiler.

**Primary Dependencies** (frozen by [tech-stack.md §2](../../speckit-artifacts/tech-stack.md)):

- **Framework**: Next.js 16.x (App Router only; Pages Router prohibited)
- **UI runtime**: React (Server Components default, Client Components scoped narrowly)
- **Styling**: Tailwind CSS v4.x (no CSS-in-JS), CSS variables for token theming
- **Component foundation**: shadcn/ui — project-owned source in `components/ui/`
- **Icons**: lucide-react
- **Tables**: TanStack Table
- **Forms**: React Hook Form
- **Validation**: Zod
- **Charts**: Recharts (wrapped in a thin chart primitive layer)
- **Animation**: Framer Motion (sparingly; respects `prefers-reduced-motion`)
- **Theme management**: next-themes (class strategy, inline anti-FOUC script)
- **Class utilities**: clsx, tailwind-merge, class-variance-authority
- **Component docs**: Storybook 8.x with `@storybook/nextjs`, `addon-a11y`, `addon-themes`, `addon-interactions`, `addon-viewport`, RTL toggle (see [Research R1](./research.md#r1-component-documentation-system--storybook-vs-equivalent))
- **Web Vitals**: `web-vitals`
- **Observability adapter**: hand-rolled `ObservabilityAdapter` interface (see [contracts/observability-adapter.ts](./contracts/observability-adapter.ts)); the AI adapter dynamically imports `@microsoft/applicationinsights-web` when the connection string is configured
- **Locale formatting**: native `Intl.*` (no polyfill needed)
- **Package manager**: pnpm (lockfile committed)

**Storage**: N/A. This is a frontend-only foundation; backend persistence is owned by future specs. Client-side persistence used: `localStorage` for theme preference (via `next-themes`) and column-visibility preferences for the table foundation (scoped key prefix `bt:foundation:*`).

**Testing** (see [Research R6](./research.md#r6-testing-strategy)):

- Vitest + React Testing Library for component / unit
- `vitest-axe` for component-level a11y assertions
- `@storybook/test-runner` + `axe-playwright` for story-level a11y (FR-027 / SC-005)
- Playwright for E2E and cross-browser smoke (Chromium, Firefox, WebKit) (FR-035a / SC-018)
- `@next/bundle-analyzer` plus a shell-size diff script for the soft bundle target (FR-035e / SC-020)

**Target Platform**:

- Browser matrix per [FR-035a](./spec.md): last two major versions of evergreen Chrome, Edge, Firefox, Safari (desktop), plus iPadOS Safari and Android Chrome.
- Modern CSS permitted without polyfills: CSS nesting, `:has()`, `color-mix()`, `@property`, container queries, OKLCH, CSS logical properties (FR-035b).
- IE, legacy non-Chromium Edge, and older embedded webviews are out of scope.

**Project Type**: Single Next.js 16.x web application (App Router) under `web/` at repo root. Backend services and infrastructure modules are out of scope for this spec and will live alongside this app in future specs.

**Performance Goals**:

- Core Web Vitals "Good" on a representative composed screen on a mid-range laptop over broadband: **LCP ≤ 2.5s**, **INP ≤ 200ms**, **CLS ≤ 0.1** (FR-035d / SC-019).
- **Soft initial-JS bundle target for the application shell: ≤ 180 KB gzipped First Load JS for the authenticated root route**, with a hard alert at **200 KB gzipped** and a CI-published regression alert path via `@next/bundle-analyzer` + a shell-size diff script. The measured baseline at foundation handoff is recorded in `web/docs/performance-budget.md` (FR-035e / SC-020). Decision and methodology: [Research R2](./research.md#r2-soft-initial-js-bundle-target-for-the-application-shell-fr-035e--sc-020).

**Constraints**:

- WCAG 2.2 AA minimum across every primitive and composite (FR-027 / SC-002 / SC-005).
- RTL-safe by construction: CSS logical properties only, ESLint rule enforces no physical-direction utilities outside token files (FR-022b / FR-022d / SC-011 / SC-012).
- Externalized strings: no hardcoded user-facing strings inside primitive or composite source (FR-022a / SC-012).
- W3C Trace Context propagation on every UI-originated HTTP request — applies regardless of whether `NEXT_PUBLIC_APPINSIGHTS_CONNECTION_STRING` is configured (FR-039 / SC-014).
- No PII in trace attributes, error payloads, or Web Vitals events by default — only correlation IDs (FR-041 / Constitution §IV).
- No secrets in client code. AI connection string is delivered via `NEXT_PUBLIC_APPINSIGHTS_CONNECTION_STRING` and is the only telemetry environment variable accepted in v1.
- No CSS-in-JS, no second design system, no additional UI libraries beyond the tech stack reference §2 without explicit approval (Constitution §Technology Standards).

**Scale / Scope**:

- 27 primitives required by [FR-013](./spec.md) (Button, Input, Textarea, Select, Checkbox, Radio Group, Switch, Label, Form, Dialog, Sheet (canonical name; the side-overlay primitive — "Drawer" is reserved for the app-shell composition that uses Sheet), Dropdown Menu, Context Menu, Command, Tabs, Card, Badge, Alert, Toast, Tooltip, Popover, Separator, Skeleton, Table foundation, Breadcrumb, Scroll Area, Resizable Panels).
- 13 domain composites required by [FR-028](./spec.md) (Namespace card, Queue row/card, Topic row/card, Subscription row/card, Dead-letter status indicator, Message count indicator, Health summary indicator, Discovery job status, Entity relationship badge, Environment badge, Azure resource link, Metadata key-value panel, Topology mini-map placeholder).
- One representative composed screen wired in the application shell to validate all P1 / P2 acceptance scenarios end-to-end.
- Storybook surface: every primitive and composite has stories covering principal states, variants, dark + light, and a passing a11y check (FR-030 / SC-005).
- One brand asset set: full wordmark, compact glyph, favicon set, social preview, repo banner — each in dark + light variants, each AI-assisted asset accompanied by a `REVIEW.md` (FR-002 / FR-002a / FR-002b / SC-017).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Gates derived from [`.specify/memory/constitution.md`](../../.specify/memory/constitution.md):

### Core Principles

| # | Principle | Applicability | Compliance |
|---|---|---|---|
| I | **Azure-First Architecture** | Frontend consumes Azure-hosted APIs and emits telemetry to Azure Monitor. | ✅ Application Insights browser adapter is the primary observability target; W3C Trace Context propagates to Azure OpenTelemetry backends. No multi-cloud abstraction is introduced. |
| II | **API-First Design** | Frontend will consume documented backend contracts. | ✅ The `http/client.ts` wrapper is built around contract-typed responses; no UI-only backdoor patterns are introduced. No backend is implemented in this spec, so no API contracts are produced — only the consumer-side scaffolding. |
| III | **Strong Domain Modeling** | Domain composites must use canonical Service Bus terminology. | ✅ Namespace, Queue, Topic, Subscription, Dead-Letter, Discovery Job, Environment, Topology are named consistently with the constitution's domain vocabulary (see [data-model.md](./data-model.md)). |
| IV | **Security by Default** | No secrets in client code; least-privilege; no PII in telemetry. | ✅ The only client env var accepted is `NEXT_PUBLIC_APPINSIGHTS_CONNECTION_STRING` (and AI connection strings are not secrets — they are publishable instrumentation keys). PII is excluded by the adapter interface shape (FR-041). |
| V | **Operational Excellence** | Full observability hook points are required. | ✅ Error boundary, route-change spans, Web Vitals, W3C Trace Context propagation, and adapter-based forwarding are foundational deliverables (FR-036 – FR-041). |
| VI | **Incremental Extensibility** | No premature lock-in. | ✅ The observability adapter interface, the i18n string surface, the design-token system, and the iconography mapping module all preserve future swap points. |

### Technology Standards

| Standard | Applicability | Compliance |
|---|---|---|
| **Backend (.NET 10, Minimal APIs, Vertical Slice)** | Out of scope for this frontend-only foundation. | N/A — no backend code is produced. |
| **Frontend (Next.js 16.x App Router, RSC default, TypeScript strict, Tailwind v4, shadcn/ui, lucide-react, TanStack Table, RHF+Zod, Recharts, Framer Motion, next-themes, clsx/tailwind-merge/CVA)** | Direct mandate for this spec. | ✅ Every tech choice in the spec and plan maps to the tech stack reference §2. No additional UI library is introduced. |
| **Data Platform (Cosmos DB, Azure AI Search)** | Out of scope. | N/A. |
| **Hosting and Infrastructure (Container Apps, ACR, Key Vault, OpenTofu, AVM)** | Out of scope for this foundation; deployment is owned by [Spec 002 — Solution Foundation](../../speckit-artifacts/002-solution-foundation.md). | N/A here; this plan does not author IaC. |
| **Identity (Entra ID, Managed Identity, RBAC)** | Out of scope for this foundation; consumed by [Spec 003 — Auth & Identity](../../speckit-artifacts/003-auth-and-identity.md). | N/A here. |

### Architecture Standards

| Standard | Compliance |
|---|---|
| **Modular Monolith First** | ✅ Frontend ships as a single Next.js application; no premature decomposition. |
| **Container-Native** | ✅ Local dev runs in a containerized devcontainer; production hosting on Azure Container Apps is owned by Spec 002. |
| **Async-First Thinking** | ✅ Web Vitals capture, route-change spans, and the AI SDK initialization are all non-blocking. |
| **AI-Assisted Capability Enablement** | ✅ The MCP usage guidance shipped in `web/docs/agentic-coding.md` distinguishes coding-agent development-time tooling from runtime dependencies (FR-032). |

### Engineering Workflow & Quality

| Standard | Compliance |
|---|---|
| **Spec-Driven Development** | ✅ This plan is the output of `/speckit-plan` against the clarified spec. |
| **CI gates (build, unit, lint, format, security scan, dep vuln scan)** | ✅ `pnpm lint && pnpm typecheck && pnpm test && pnpm test:storybook && pnpm test:e2e && pnpm analyze` runs locally and in CI (SC-009). |
| **Testing strategy (unit, integration, contract, UI, E2E)** | ✅ See Technical Context → Testing. |
| **AI Tooling / MCP usage** | ✅ Documented as development-time only (FR-032). |

### Scope & Decision Framework

| Standard | Compliance |
|---|---|
| **Non-goals not driving design** | ✅ Foundation produces no business UI, no backend, no auth logic, no persistence, no IaC. |
| **Decision Priorities ordered (Op-Simplicity → Dev-Productivity → Maintainability → Security → Observability → Extensibility → Performance → Cost)** | ✅ All R1 – R10 decisions in [research.md](./research.md) call out their priority alignment. The bundle target balances Performance against Operational Simplicity by setting a pragmatic soft / hard threshold rather than an aggressive aspirational number. |
| **Open-source commitments** | ✅ The brand asset review pipeline (FR-002a / FR-002b), plain-SVG requirement, and recorded `REVIEW.md` artifacts are auditable by community contributors. |

**Result**: **PASS**. No violations require justification in the Complexity Tracking section.

## Project Structure

### Documentation (this feature)

```text
specs/001-brand-system-and-design-foundation/
├── spec.md                # Clarified feature spec (source of truth)
├── plan.md                # This file (/speckit-plan output)
├── research.md            # Phase 0 research (R1 – R10)
├── data-model.md          # Phase 1 conceptual data model
├── quickstart.md          # Phase 1 contributor / agent quickstart
├── contracts/             # Phase 1 TypeScript interface contracts
│   ├── observability-adapter.ts
│   ├── theme-provider.ts
│   ├── design-tokens.ts
│   ├── i18n-strings.ts
│   ├── table-foundation.ts
│   ├── form-foundation.ts
│   └── brand-asset-review.md
└── tasks.md               # Phase 2 output (/speckit-tasks — NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
web/                                  # Next.js 16.x App Router application
├── app/
│   ├── (app)/                        # Authenticated operational shell route group
│   │   ├── layout.tsx                # App shell (sidebar + top bar + page container + footer)
│   │   ├── page.tsx                  # Foundation demo screen (representative composed screen)
│   │   ├── error.tsx                 # Top-level error boundary surface
│   │   ├── loading.tsx               # Top-level loading surface (skeleton-based)
│   │   └── _showcase/                # Foundation showcase pages used for SC-001 / SC-007 / SC-019
│   ├── (marketing)/                  # Placeholder route group for future marketing pages
│   ├── api/                          # Reserved; no routes in this spec
│   ├── layout.tsx                    # Root layout (Theme provider, observability provider, html lang/dir)
│   ├── providers.tsx                 # Client Components: ThemeProvider + ObservabilityProvider + WebVitalsBeacon
│   ├── opengraph-image.tsx           # Social preview (consumes brand asset)
│   └── icon.tsx / apple-icon.tsx     # Favicon configuration consuming brand assets
├── components/
│   ├── ui/                           # shadcn/ui primitives (project-owned source) — FR-013
│   ├── app-shell/                    # Sidebar, top bar, page header, footer, breadcrumbs, drawer chrome — FR-010 / FR-011
│   ├── data-display/                 # Card, key-value panel, code block, JSON viewer, status pill, stat panel
│   ├── data-table/                   # TanStack Table foundation — FR-015 / FR-016
│   ├── forms/                        # React Hook Form + Zod field composites — FR-017 / FR-018
│   ├── feedback/                     # Toast surface, alerts, empty state, error state, retry — FR-019 / FR-020
│   ├── navigation/                   # Command palette, breadcrumbs, pagination
│   ├── charts/                       # Recharts wrappers — FR-029 / Research R10
│   └── domain/                       # Service Bus domain composites — FR-028
├── lib/
│   ├── design-system/                # cn(), variants helper, token utilities
│   ├── i18n/
│   │   ├── strings/en.ts             # Centralized English string surface — FR-022a
│   │   ├── format.ts                 # Intl-based formatters — FR-022c
│   │   └── direction.ts              # dir helper + locale → dir mapping (RTL-safe wiring)
│   ├── observability/
│   │   ├── adapter.ts                # ObservabilityAdapter interface — FR-040
│   │   ├── noop-adapter.ts           # Default no-op adapter — FR-040
│   │   ├── app-insights-adapter.ts   # Dynamic-import wrapper for @microsoft/applicationinsights-web
│   │   ├── web-vitals.ts             # web-vitals → adapter wiring — FR-037 / SC-016
│   │   ├── error-boundary.tsx        # Top-level error boundary forwarder — FR-036 / SC-015
│   │   └── route-change.ts           # App Router navigation span emitter — FR-038
│   ├── http/
│   │   ├── trace-context.ts          # W3C Trace Context generator/parser — FR-039
│   │   └── client.ts                 # fetch wrapper enforcing traceparent injection
│   ├── iconography/
│   │   └── domain-icons.ts           # Service Bus domain → lucide-react icon mapping
│   ├── utils/                        # General utilities (no business logic)
│   └── validation/                   # Reusable Zod helpers for forms
├── hooks/
│   ├── use-toast.ts
│   ├── use-confirm.ts
│   ├── use-reduced-motion.ts
│   └── use-bounding-locale.ts
├── styles/
│   ├── globals.css                   # Tailwind v4 directives, base layer
│   ├── tokens.css                    # CSS variables — design tokens (light + dark) — FR-004 / FR-005
│   └── typography.css                # Typography scale variables — FR-008
├── brand/                            # SVG sources + REVIEW.md per AI-assisted asset — FR-002a / FR-002b
│   ├── wordmark/
│   │   ├── wordmark-dark.svg
│   │   ├── wordmark-light.svg
│   │   └── REVIEW.md
│   ├── glyph/
│   │   ├── glyph-dark.svg
│   │   ├── glyph-light.svg
│   │   └── REVIEW.md
│   └── README.md                     # Brand pipeline overview
├── public/
│   ├── brand/                        # Exported PNG / OG / repo banner — FR-002
│   ├── favicons/                     # Multi-size favicon set
│   └── og/                           # Social preview image
├── .storybook/
│   ├── main.ts                       # Storybook framework + addons config
│   ├── preview.tsx                   # Global decorators: ThemeProvider, RTL toggle, viewport defaults
│   └── theme.ts                      # Brand-aligned Storybook UI theming
├── stories/                          # Storybook MDX pages (brand, tokens, accessibility, contribution)
│   ├── 00-introduction.mdx
│   ├── 01-brand.mdx
│   ├── 02-design-tokens.mdx
│   ├── 03-typography.mdx
│   ├── 04-iconography.mdx
│   ├── 05-accessibility.mdx
│   ├── 06-theming.mdx
│   ├── 07-contribution.mdx
│   ├── 08-agentic-coding.mdx
│   └── 09-browser-support.mdx
├── tests/
│   ├── e2e/                          # Playwright specs (composed screen, cross-browser smoke)
│   ├── a11y/                         # Story-runner-based axe assertions
│   └── unit/                         # Component / hook tests (Vitest + RTL + vitest-axe)
├── docs/
│   ├── performance-budget.md         # FR-035e / SC-020 — bundle target + measured baseline
│   ├── performance-budget.json       # Machine-readable baseline (input to the diff script)
│   ├── agentic-coding.md             # MCP usage as dev-time tooling — FR-032
│   ├── browser-support.md            # FR-035c
│   ├── theming.md
│   ├── accessibility.md
│   └── contributing-frontend.md
├── scripts/
│   ├── bundle-diff.mjs               # Compares analyze output against the recorded baseline
│   └── brand-export.mjs              # Helper for stage-3 brand variant exports (Research R3)
├── eslint.config.ts                  # Lint rules incl. no-physical-direction-utilities — SC-012
├── next.config.ts                    # @next/bundle-analyzer wiring
├── postcss.config.mjs                # Tailwind v4
├── tailwind.config.ts                # Token references (re-export of CSS variables)
├── tsconfig.json                     # strict
├── package.json
└── pnpm-lock.yaml

docs/
└── adr/                              # Architectural Decision Records (placeholder; created at first ADR)
```

**Structure Decision**: Single Next.js 16.x web application rooted at `web/`. This is the only top-level application directory introduced by this spec. Backend services and infrastructure modules will be added by later specs as sibling top-level directories (e.g., `services/`, `infrastructure/`). The frontend foundation has no backend dependency in v1, so no cross-tree coupling is introduced.

The chosen layout is a direct expression of the structure recommended in the source artifact §10.2 and the tech-stack reference, expanded with concrete directories for the brand pipeline (FR-002a / FR-002b), observability adapter (FR-040), W3C Trace Context propagation (FR-039), Storybook documentation system ([Research R1](./research.md#r1-component-documentation-system--storybook-vs-equivalent)), and the bundle-size regression alert path ([Research R2](./research.md#r2-soft-initial-js-bundle-target-for-the-application-shell-fr-035e--sc-020)).

## Implementation Phases

The plan organizes the work into six dependency-ordered phases. Each phase is **independently shippable** in the sense that the foundation remains buildable and demoable at the end of each phase — early phases produce visible Storybook output even before all primitives exist.

### Phase A — Repository scaffolding, tokens, theming, lint, brand placeholder

**Goal**: A buildable Next.js 16.x application with the design-token system, Tailwind v4 setup, theme provider with flash-free first paint, ESLint rule enforcing logical properties, Storybook initialized, brand placeholder SVG committed, and CI scripts in place.

**Key deliverables**:

- `web/` Next.js 16.x App Router project bootstrapped with TypeScript strict, pnpm, RSC default.
- Tailwind v4 configured; `styles/tokens.css` and `styles/typography.css` define the full CSS-variable token surface (FR-004 / FR-005 / FR-007 / FR-008).
- `next-themes` wired with the class strategy + inline anti-FOUC script (FR-006 / SC-004).
- ESLint config including the no-physical-direction-utilities rule (FR-022b / SC-012).
- Storybook 8.x initialized with the addons listed in [Research R1](./research.md#r1-component-documentation-system--storybook-vs-equivalent); `00-introduction`, `01-brand`, `02-design-tokens` MDX pages.
- Brand **placeholder** mark and wordmark committed to `web/brand/` and exported into `web/public/` so favicons / OG / repo branding render in dev (Research R3 placeholder).
- `pnpm` scripts: `dev`, `build`, `start`, `lint`, `typecheck`, `analyze`.
- CI: lint + typecheck + build + Storybook build run on every PR.

**Parallel track started in Phase A**: Brand asset pipeline Stage 1 (concept exploration); does not gate Phase A completion.

### Phase B — shadcn/ui primitive library

**Goal**: All 27 primitives required by [FR-013](./spec.md) committed, themed, accessible, story-covered, and unit-tested.

**Key deliverables**:

- `pnpm dlx shadcn@latest init`, then add each primitive (Research R8).
- Each primitive is themed against the design tokens (zero hardcoded colors / spacings — SC-003).
- Each primitive has: a default state story, all-variants story, all-states story, both-themes story, and a `vitest-axe`-clean unit test (FR-027 / SC-005).
- Class utilities (`cn`, variants helper) in `lib/design-system/` (FR-014 / FR-034).
- Resizable Panels, Command palette, Scroll Area, Tooltip and Popover (which need Radix wiring) are covered in this phase.

**Parallel track active in Phase B**: Brand asset pipeline Stages 1 → 2 (concept → vector authoring).

### Phase C — Layout chrome, navigation, data table, forms, feedback surfaces, charts

**Goal**: The application shell, navigation chrome, table foundation, form foundation, feedback surfaces, and chart wrappers are complete and composed in a representative demo screen.

**Key deliverables**:

- App shell (sidebar + top bar + page container + footer) composed in `app/(app)/layout.tsx` (FR-010).
- Sheet primitive + the app-shell drawer composition pattern that uses Sheet, split / resizable panels, tabs (FR-011 / FR-012).
- Breadcrumb composite, command-palette overlay, pagination (FR-013 + Source artifact §11.2 Navigation).
- TanStack Table foundation: type-safe columns, sorting, filtering, column visibility, sticky headers, keyboard navigation, row actions, bulk select, pagination, empty/loading/error states, responsive overflow (FR-015 / FR-016).
- React Hook Form + Zod form foundation: schema-driven validation, accessible required indication, inline error display, submit-pending state, destructive-action confirmation pattern, long-running progress feedback (FR-017 / FR-018).
- Feedback primitives: skeleton patterns (FR-019), empty/error/inline-validation/alert/toast surfaces (FR-020).
- Recharts chart primitive layer (Research R10): `<ChartContainer>`, `<ChartLine>`, `<ChartBar>`, `<ChartArea>` — token-themed, reduced-motion-aware, color-and-text semantic state.
- Representative composed demo screen rendered at `/` validating SC-001 (composable scaffold from foundation primitives only — `pnpm audit:tokens && pnpm audit:strings && pnpm audit:directions` report zero violations).

**Parallel track active in Phase C**: Brand asset pipeline Stages 2 → 3 (vector authoring → variant export).

### Phase D — Domain composites, iconography, i18n surface

**Goal**: BusTerminal Service Bus domain composites, the iconography mapping module, and the i18n string surface + locale-aware formatters are complete.

**Key deliverables**:

- 13 domain composites in `components/domain/` (FR-028).
- `lib/iconography/domain-icons.ts` mapping all required domain icons (FR-021 / FR-022 / Research R9).
- `lib/i18n/strings/en.ts` with the foundation's user-facing strings (FR-022a / SC-012).
- `lib/i18n/format.ts` providing `formatDate`, `formatTime`, `formatRelativeTime`, `formatDuration`, `formatNumber`, `formatBytes` (FR-022c).
- `dir="rtl"` Storybook toggle exercises every primitive and composite without breakage (FR-022d / SC-011).

**Parallel track approaching gate in Phase D**: Brand asset pipeline Stages 3 → 4 (variant export → human originality / licensing review). Stage 4 `REVIEW.md` artifacts must land before Phase F.

### Phase E — Observability, performance budget, cross-browser smoke

**Goal**: All FR-036 – FR-041 observability requirements, the bundle-size regression alert path, the Core Web Vitals checks, and the cross-browser smoke are operational.

**Key deliverables**:

- `lib/observability/adapter.ts` interface, `noop-adapter.ts`, `app-insights-adapter.ts` (dynamic-import) (FR-040 / SC-013).
- Top-level error boundary forwarding rendering errors + React component stack through the adapter (FR-036 / SC-015).
- `web-vitals` capture wired to the adapter (FR-037 / SC-016).
- Route-change span emitter for App Router navigations (FR-038).
- `lib/http/trace-context.ts` + `lib/http/client.ts` enforcing `traceparent` (+ `tracestate` when present) on every UI-originated HTTP request, **regardless of adapter config** (FR-039 / SC-014).
- PII hygiene confirmed via adapter interface shape (FR-041).
- `@next/bundle-analyzer` wired in `next.config.ts`. `scripts/bundle-diff.mjs` runs on PRs and posts size delta; baseline recorded in `web/docs/performance-budget.json` and described in `web/docs/performance-budget.md` (FR-035e / SC-020 / Research R2).
- Playwright cross-browser smoke (Chromium, Firefox, WebKit) targeting the representative composed screen (FR-035a / SC-018).
- Playwright + Lighthouse worker probe records LCP / INP / CLS against the budget (FR-035d / SC-019).

### Phase F — Brand asset finalization, documentation, handoff

**Goal**: Final brand assets land, documentation is complete, and the foundation is declared handoff-ready.

**Key deliverables**:

- Brand asset pipeline Stage 5: final SVG sources committed in `web/brand/`, exported variants in `web/public/`, `REVIEW.md` accompanying each AI-assisted asset (FR-002 / FR-002a / FR-002b / SC-017). Placeholder is replaced; import paths unchanged.
- Storybook MDX pages complete: `01-brand.mdx`, `02-design-tokens.mdx`, `03-typography.mdx`, `04-iconography.mdx`, `05-accessibility.mdx`, `06-theming.mdx`, `07-contribution.mdx`, `08-agentic-coding.mdx`, `09-browser-support.mdx` (FR-030 / FR-031).
- `web/docs/agentic-coding.md` documents MCP usage as development-time tooling only (FR-032).
- `web/docs/browser-support.md` publishes the matrix (FR-035c).
- `web/docs/performance-budget.md` publishes the measured baseline + 180 KB soft / 200 KB hard thresholds + the regression alert path (FR-035e / SC-020).
- All success criteria validated end-to-end; foundation declared complete.

## Phase 2 Planning Approach (forward-look — produced by `/speckit-tasks`)

`/speckit-plan` stops at the end of Phase F design. Phase 2 (task generation) will:

- Convert each phase deliverable into one or more ordered tasks in `tasks.md`.
- Identify which tasks can run in parallel and which depend on prior tasks.
- Surface the brand asset pipeline as a parallel sub-stream with explicit dependencies on the `REVIEW.md` artifact before any Stage 5 commit task can run (per [Research R3](./research.md#r3-ai-assisted-brand-asset-production-and-required-human-review-fr-002a--fr-002b)).
- Establish quality gates at the end of each phase: Storybook build + a11y, Vitest + RTL + axe, Playwright cross-browser smoke, bundle-size diff (per [Research R6](./research.md#r6-testing-strategy) and [R2](./research.md#r2-soft-initial-js-bundle-target-for-the-application-shell-fr-035e--sc-020)).

## Post-Design Constitution Re-Check

After Phase 1 design artifacts (data-model.md, contracts/, quickstart.md) were authored:

- No new technology was introduced.
- No new violation surfaced.
- All design artifacts respect the Decision Priorities ordering and the technology standards.

**Result**: **PASS** (re-check). No entries are added to Complexity Tracking.

## Complexity Tracking

> *Fill ONLY if Constitution Check has violations that must be justified.*

**No violations.** The plan adopts the tech stack reference verbatim, introduces no new dependencies beyond `@next/bundle-analyzer` (which is a Next.js-first-party tool), `web-vitals` (a Google-published reference library named in the Web Vitals capture requirement), `@microsoft/applicationinsights-web` (the explicit AI adapter target named by FR-040), and the standard Storybook 8.x addons named in Research R1. No CSS-in-JS, no second design system, no alternative component library, no heavy chart suite, no graph/topology library, no drag-and-drop library, no rich-text editor, no code editor component — all in compliance with the constitutional frontend standards and the spec's stated assumptions.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| _(none)_ | _(none)_ | _(none)_ |
