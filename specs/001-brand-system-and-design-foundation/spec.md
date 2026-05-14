# Feature Specification: Brand System and Design Foundation

**Feature Branch**: `feature/001-brand-system-and-design-foundation`

**Created**: 2026-05-14

**Status**: Draft

**Input**: User description: "Let's get our first slice built out, 001-brand-system-and-design-foundation, you can find the deets in @speckit-artifacts/001-brand-system-and-design-foundation.md"

**Source Artifact**: [`speckit-artifacts/001-brand-system-and-design-foundation.md`](../../speckit-artifacts/001-brand-system-and-design-foundation.md)

## Overview

BusTerminal needs a working, opinionated brand and design foundation in place **before** any feature workstream begins. This spec produces the visual identity, design tokens, accessibility baseline, themed UI primitives, layout chrome, data-table and form foundations, and the documentation system that future feature specs will consume rather than redefine.

The foundation is a product feature in its own right: it determines whether the product feels operationally trustworthy at first impression, whether feature work can move quickly and consistently, and whether agentic coding workflows can generate aligned UI without supervision drift.

## Clarifications

### Session 2026-05-14

- Q: What is the localization / i18n scope for v1 of the foundation? → A: English-only content for v1, **but** the foundation is built RTL-safe (CSS logical properties everywhere, no hardcoded `left`/`right`), user-facing strings are externalized (not embedded in component source), and locale-aware formatting is used for dates, times, relative times, and numbers from day one. Translation pipelines and additional locales are deferred to a future spec.
- Q: What is the frontend observability scope for the foundation? → A: The foundation ships the full observability **hook points** (error boundaries, Web Vitals capture, route-change traces, correlation-ID propagation) **and** an Application Insights browser adapter, with the AI connection string gated by an environment variable (no-op when unset) so OSS contributors are not forced into Azure tooling. **W3C Trace Context propagation (`traceparent`/`tracestate` headers on UI-originated HTTP requests) is a hard requirement**, so frontend traces correlate end-to-end with backend traces emitted via OpenTelemetry to Azure Monitor.
- Q: How will brand assets (logo, wordmark, favicon, social preview) be produced for v1? → A: AI-assisted generation (icon/wordmark generators) with manual refinement and final hand-cleanup; source SVG is authored/committed to the repo and redistributable under the project's open-source license. Human review for originality and any licensing/attribution implications is required before any generated asset is committed.
- Q: What is the browser support matrix for the foundation? → A: The **last two major versions** of evergreen desktop browsers (Chrome, Edge, Firefox, Safari) and the last two major versions of iPadOS Safari and Android Chrome. Internet Explorer, legacy non-Chromium Edge, and embedded webviews older than the supported window are explicitly out of scope. This baseline permits modern CSS (CSS nesting, `:has()`, `color-mix()`, `@property`, container queries, OKLCH) without polyfills.
- Q: What are the frontend performance budgets the foundation must meet? → A: Adopt the **Core Web Vitals "Good" thresholds** on a representative composed screen on a mid-range laptop over broadband — **LCP ≤ 2.5s**, **INP ≤ 200ms**, **CLS ≤ 0.1** — plus a documented **soft initial-JS bundle target for the application shell** that feature specs can reference. The soft target's specific value is set by the implementation plan based on the measured shell size after Next.js 16 + shadcn/ui + theme provider + observability hook points are in place; the foundation must publish the actual number and a budget regression alert path.

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Feature Developers Build From a Ready Foundation (Priority: P1)

A developer (human contributor or agentic coding agent) starts work on the first BusTerminal feature spec. They open the project, discover the available primitives and tokens, and assemble an operational screen — navigation chrome, page header, data table, detail panel, form, toasts — entirely from the foundation library, without authoring any one-off styling, color values, or layout chrome.

**Why this priority**: Every downstream feature spec depends on this. If the foundation isn't usable as a kit, feature work fragments into bespoke implementations and the design system never coalesces. This is the primary value proposition of the spec.

**Independent Test**: A reviewer (or coding agent) can scaffold a representative operational page — sidebar + header + data table with sortable/filterable columns + entity detail drawer + a validated form + a toast — using only foundation primitives and tokens. No new colors, fonts, spacings, or component patterns are introduced. The result renders correctly in both dark and light modes.

**Acceptance Scenarios**:

1. **Given** the foundation is installed in the repository, **When** a developer creates a new route under the application area, **Then** they can compose the page using only the published primitives (layout shell, navigation, page header, data table, form, dialog/drawer, toast, badge, card) without writing custom CSS or hardcoded color/spacing values.
2. **Given** a developer adds a new data table for an entity list, **When** they use the data-table foundation, **Then** sorting, filtering, column visibility, pagination, sticky headers, keyboard navigation, and empty/loading/error states all work without additional implementation.
3. **Given** a developer adds a new form, **When** they use the form foundation, **Then** validation, error display, required-field indicators, submit-disabled-while-pending, and accessible labelling all behave consistently with other forms in the product.
4. **Given** a coding agent is asked to build a screen, **When** it consults the foundation documentation, **Then** the documentation states which primitive to use for each common pattern, with usage examples and the rules for composing rather than wrapping.

---

### User Story 2 — Operators Experience a Polished, Operationally Trustworthy UI (Priority: P1)

A Service Bus operator opens BusTerminal for the first time. The product looks like modern cloud infrastructure tooling — dense but readable, dark-mode by default, with strong typography for technical identifiers, restrained motion, and clear semantic color signals. Nothing about the UI feels playful, consumer-grade, or like a generic admin template.

**Why this priority**: First-impression credibility is non-recoverable. If operators look at the product and read "weekend side project" or "consumer SaaS," they will not trust it with production messaging topology. This priority is co-equal with developer enablement (P1).

**Independent Test**: A reviewer who is not part of the project opens the foundation Storybook (or equivalent showcase) and confirms: the product reads as infrastructure tooling, dark and light themes are both polished and complete, monospace renders technical identifiers, semantic states (success/warning/error/info) are immediately recognizable, and there is no visual overload, gratuitous animation, or branding placeholder.

**Acceptance Scenarios**:

1. **Given** the application loads with no user preference set, **When** the page first renders, **Then** the system theme preference is honored, and there is no visible flash of unstyled content or wrong-theme flash during hydration.
2. **Given** the user toggles between dark and light themes, **When** the theme changes, **Then** every primitive, semantic color, focus ring, chart color, and typographic style remains legible and on-brand in both modes, and the preference persists across reloads.
3. **Given** a screen displays large amounts of messaging metadata, **When** the operator scans it, **Then** entity identifiers (queue/topic/namespace names, correlation IDs, connection strings) are rendered in a monospace family with clear visual separation from prose content.
4. **Given** the operator triggers an error, warning, success, or informational state, **When** the corresponding component renders, **Then** the semantic intent is conveyed by both color and a non-color affordance (icon or text), and contrast meets WCAG AA in both themes.

---

### User Story 3 — Accessibility Is Enforced From the Foundation, Not Retrofitted (Priority: P2)

A keyboard-only or assistive-technology user navigates the product, opens a data table, sorts a column, opens a detail drawer, fills out a form, and dismisses a confirmation dialog — entirely without a mouse, with screen-reader announcements that make sense, with visible focus throughout, and respecting their reduced-motion preference.

**Why this priority**: Accessibility is a foundational requirement per the source artifact and the BusTerminal Constitution. Retrofitting accessibility after feature work has begun is significantly more expensive than enforcing it in the primitives. P2 rather than P1 only because the bulk of accessibility behavior is unlocked once primitives are correct in P1 — this story validates that the enforcement actually holds.

**Independent Test**: An auditor runs automated accessibility scans against the Storybook stories and a representative composed screen, plus a manual keyboard-only and screen-reader walkthrough. Zero WCAG 2.2 AA failures; all interactive primitives are operable by keyboard with visible focus; reduced-motion preferences eliminate non-essential animation; no critical information is conveyed by color alone.

**Acceptance Scenarios**:

1. **Given** the user navigates only with the keyboard, **When** they tab through any primitive (button, input, select, checkbox, switch, table row, dialog, drawer, command palette, menu), **Then** focus order is logical, focus is always visible, there are no keyboard traps, and Escape closes overlay surfaces predictably.
2. **Given** a screen reader is active, **When** the user encounters any primitive, **Then** it announces with a meaningful name, role, state, and (where appropriate) value, using semantic HTML first and ARIA only where semantic HTML is insufficient.
3. **Given** the user has `prefers-reduced-motion` set, **When** components animate, **Then** non-essential motion is removed or reduced to opacity/instant transitions; state-clarifying motion remains but is minimized.
4. **Given** the user has a color-vision deficiency, **When** they view semantic states (success/warning/error/info, dead-letter indicator, health summary, environment badge), **Then** meaning is preserved via icon and/or text in addition to color.
5. **Given** any automated accessibility checker runs against a foundation primitive's published states, **When** the check completes, **Then** zero WCAG 2.2 AA violations are reported.

---

### User Story 4 — Brand, Tokens, and Components Are Discoverable and Documented (Priority: P2)

A new contributor or coding agent needs to answer: "What is the brand voice? What colors are sanctioned? Which component should I use for X? How do I theme it? What are the accessibility constraints?" — and finds those answers in one place, alongside live examples of every primitive in every state, in both themes.

**Why this priority**: A design system that exists only as code is not a design system; it is a private library. The discoverability layer is what makes adoption scale across human contributors and coding agents. P2 because it builds on P1 (primitives must exist before they can be documented).

**Independent Test**: A contributor unfamiliar with the project opens the documentation and Storybook, and within ten minutes can answer: which primitive to use for each common pattern, which design tokens to reference for color/spacing/typography, how to add a new variant correctly, what the accessibility expectations are, and how to validate their work before opening a pull request.

**Acceptance Scenarios**:

1. **Given** a contributor opens the component documentation, **When** they look up any primitive, **Then** they see its purpose, anatomy, props/variants, usage rules ("do this"/"don't do this"), accessibility notes, and live examples across states and themes.
2. **Given** a contributor needs a color, spacing value, radius, elevation, or motion duration, **When** they consult the token documentation, **Then** every centrally defined token is listed with its name, value, theme variants, and intended usage; no hardcoded values are sanctioned.
3. **Given** Storybook (or the chosen equivalent) is running, **When** a contributor browses it, **Then** every primitive has stories covering its principal states, variants, dark/light modes, and accessibility validation results.

---

### User Story 5 — Domain-Aware Components Exist for Service Bus Concepts (Priority: P3)

A feature developer building the first Service Bus screen needs to render a namespace, a queue, a topic, a subscription, a dead-letter indicator, a message count, and an environment badge — and finds purpose-built composite components that already know how to render these concepts consistently, instead of having to assemble them from scratch each time.

**Why this priority**: This shifts where consistency is enforced. Without these composites, every feature spec will reinvent how a "queue row" looks, eroding the design system. P3 because the underlying primitives in P1 are sufficient to unblock initial feature work; the domain composites are an accelerator and consistency guarantee on top.

**Independent Test**: A reviewer can render every listed domain composite (namespace card, queue row/card, topic row/card, subscription row/card, dead-letter status indicator, message count indicator, health summary indicator, discovery job status, entity relationship badge, environment badge, Azure resource link, metadata key-value panel, topology mini-map placeholder) in Storybook with representative data, and confirm visual and behavioral consistency across them.

**Acceptance Scenarios**:

1. **Given** a feature developer needs to render a queue in a list, **When** they reach for a domain composite, **Then** a published "Queue row/card" exists with documented props, states (active/idle/error/dead-lettered), and accessibility behavior.
2. **Given** the operator views any screen showing entities from multiple environments, **When** entity rows render, **Then** environment context is conveyed consistently via the environment badge composite.
3. **Given** a topology view is not yet implemented, **When** a developer needs a placeholder, **Then** a topology mini-map placeholder composite exists so feature specs can stub the slot without authoring bespoke placeholder UI.

---

### Edge Cases

- **Theme flash on first paint**: The user reloads the page while on dark mode in a system that prefers light — they must not see a light-themed flash during hydration before the stored preference applies.
- **Switching themes mid-flight**: An operator with a dialog, drawer, toast, and chart open simultaneously toggles themes — every surface must repaint cleanly without leaked dark/light values, broken focus rings, or stale chart colors.
- **Long entity names / wide content**: A queue or namespace name longer than the column or card can hold — composites must truncate predictably with hover/focus disclosure rather than overflowing or wrapping into adjacent cells.
- **Reduced motion + chart updates**: A user with reduced motion enabled watches a chart whose data updates — transitions must be removed or shortened without breaking comprehension of the data change.
- **Color-vision-deficient operator + semantic states**: A user with deuteranopia or protanopia views a dashboard mixing success/warning/error states — meaning must remain unambiguous via iconography and labels.
- **Very wide and very narrow viewports**: A 4K operations workstation and a 13" laptop both open the same screen — density and layout must adapt without hiding critical information on the wide screen or producing a single-column mobile compromise on the narrow desktop.
- **Mobile/tablet read access**: An operator opens BusTerminal from a tablet or phone for read-only triage — the layout must remain usable for read operations even though wide-desktop is the primary target.
- **Agentic generation drift**: A coding agent generates UI that wraps shadcn/ui primitives in feature-specific abstractions instead of composing them — the documentation and conventions must make the correct pattern obvious enough that this drift does not occur.
- **RTL document direction**: A contributor sets `dir="rtl"` on the document root to validate future-locale readiness — every primitive must render without clipping, overlap, broken anchoring of menus/popovers, mis-flipped chevrons, or mis-aligned focus rings, even though no translated content is shipped in v1.
- **Non-English locale formatting on first load**: A user's browser locale is, e.g., `de-DE` or `ja-JP` — dates, relative times, durations, and large counts rendered by the foundation must format per that locale even though the surrounding content is English.

## Requirements *(mandatory)*

### Functional Requirements

> **Numbering note**: Sub-numbered IDs (e.g., FR-002a/b, FR-022a–d, FR-035a–f) group related requirements that share a topical heading. FR-035 itself is the **scope-boundary** marker and is intentionally placed under its own "Scope Boundaries" heading near the end of this section, after the topical FR-035a–f sub-numbers. This ordering reflects topical grouping rather than strictly numeric sequence.

**Identity and Brand**

- **FR-001**: The product MUST present a single, consistent product name (`BusTerminal`) across the UI, documentation, generated artifacts, and any GitHub/social surfaces.
- **FR-002**: The brand MUST be delivered as a working logo system that includes full wordmark, compact mark, favicon, dark/light variants, and SVG-first assets that render cleanly from 16px through large-format use.
- **FR-002a**: Brand assets MAY be AI-assisted in their initial generation but MUST be manually refined, hand-cleaned, and reviewed by a human contributor before commit. The committed SVG sources MUST be authored as plain, readable SVG (no opaque rasters, no embedded base64 binaries) so the assets can be edited and reproduced in source control.
- **FR-002b**: All committed brand assets MUST be redistributable under the project's open-source license. A licensing/originality review MUST be recorded for any AI-assisted asset before it is committed (e.g., as a brief note in the asset's source folder or in the implementation pull request), confirming that no third-party trademark, copyrighted likeness, or restricted training-data artifact is reproduced.
- **FR-003**: The visual identity MUST communicate the brand traits documented in the source artifact (technical, reliable, modern, precise, efficient, operational, open) and MUST avoid the listed anti-patterns (playful, consumer-social, corporate-template, skeuomorphic, glassmorphism, visual overload).

**Design Tokens and Theming**

- **FR-004**: All color, typography, spacing, radius, elevation, motion-duration, border, layout, breakpoint, z-index, focus-ring, and data-visualization values MUST be expressed as centrally defined design tokens; feature code MUST NOT introduce hardcoded values.
- **FR-005**: The token system MUST support a dark theme and a light theme as first-class, complete sets — every primitive MUST be legible, on-brand, and accessible in both modes.
- **FR-006**: Theme selection MUST respect the user's system preference on first load, MUST be user-overridable, MUST persist the user's choice across reloads, and MUST NOT produce a flash of incorrect theme during hydration.
- **FR-007**: Semantic intent (success, warning, error, info, disabled, interactive/hover/focus) MUST be expressed through dedicated semantic tokens that meet WCAG AA contrast minimums in both themes.

**Typography**

- **FR-008**: A documented typography scale MUST cover display, H1–H6, body, caption, label, table, and monospace variants, with tokens for each.
- **FR-009**: Monospace typography MUST be applied to technical identifiers (queue names, topic names, subscription names, namespace names, correlation IDs, connection strings, JSON payloads, CLI snippets, message metadata) wherever they appear.

**Layout and Navigation Chrome**

- **FR-010**: A shared application shell MUST provide left-rail navigation, top bar, page container, section container, breadcrumb navigation, and footer primitives that future feature routes compose rather than reimplement.
- **FR-011**: Resizable and split panels, drawer surfaces, and tabbed layouts MUST be available as primitives suitable for operational, multi-panel workflows.
- **FR-012**: The layout system MUST optimize for wide-desktop operational use, support tablet, and remain usable for read-only access on mobile, without hiding critical information on wide screens.

**Primitive Component Library**

- **FR-013**: The following primitives MUST be available, themed, owned by the project, and ready for composition: Button, Input, Textarea, Select, Checkbox, Radio Group, Switch, Label, Form, Dialog, **Sheet** (the side-overlay primitive — colloquially referred to as "Drawer" in some contexts; the canonical primitive name is `Sheet`, matching shadcn/ui; "drawer" is reserved for the app-shell composition pattern that uses `Sheet`), Dropdown Menu, Context Menu, Command (palette), Tabs, Card, Badge, Alert, Toast, Tooltip, Popover, Separator, Skeleton, Table foundation, Breadcrumb, Scroll Area, Resizable Panels.
- **FR-014**: All primitives MUST be composable; the system MUST favor composition over wrapper-heavy abstractions, and a documented `cn()`-style class-merging utility plus a variant-definition utility MUST be available for project-wide use.

**Data Tables**

- **FR-015**: A shared data-table foundation MUST support, at minimum: type-safe column definitions, sorting, filtering, column visibility toggling, sticky headers, keyboard navigation, row actions, multi-select/bulk actions, pagination or virtualization (chosen per dataset size), empty/loading/error states, and responsive overflow handling.
- **FR-016**: Raw unstyled tables and one-off per-feature table implementations MUST NOT be used in product UI; all tables MUST consume the foundation's table primitives.

**Forms**

- **FR-017**: A shared form foundation MUST integrate schema-driven validation, accessible required-field indication, accessible inline error display, submit-pending state that prevents duplicate submission, and a documented pattern for destructive-action confirmation.
- **FR-018**: Long-running form operations MUST provide progress or clear feedback rather than appearing frozen.

**Feedback Surfaces**

- **FR-019**: Loading states MUST prefer skeleton primitives over indeterminate spinners; skeletons MUST preserve layout stability so that content arrival does not shift the page.
- **FR-020**: Empty states, error states, retry affordances, inline validation messages, alerts, and toasts MUST be available as documented primitives with consistent voice and visual treatment.

**Iconography**

- **FR-021**: A single iconography family MUST be standardized across the product, with consistent stroke widths and small-size readability, used in both themes.
- **FR-022**: Domain-specific iconography MUST be specified for: queues, topics, subscriptions, dead-letter queues, message flows, topology relationships, namespace health, relay/routing, discovery operations, and topology mapping — even if some are initially fulfilled by curated selections from the standard family.

**Internationalization Readiness**

- **FR-022a**: All user-facing strings rendered by foundation primitives and domain composites MUST be sourced from a centralized string surface; strings MUST NOT be hardcoded inside component source. (Translation pipeline itself is deferred — the surface exists so a future spec can swap implementations without rewriting components.)
- **FR-022b**: All foundation layout, spacing, padding, border, and positioning MUST use CSS logical properties (e.g., `padding-inline-start`, `margin-inline-end`, `inset-inline-start`); the physical `left`/`right`/`text-align: left`/`text-align: right` forms MUST NOT be used where a logical equivalent exists.
- **FR-022c**: Locale-aware formatting helpers MUST be provided for dates, times, relative times, durations, and numbers (counts, sizes, byte units), and foundation primitives and domain composites MUST use these helpers wherever such values are rendered. The active locale defaults to the user's browser locale.
- **FR-022d**: The foundation MUST support `dir="rtl"` at the document root without visual breakage in any primitive: directional affordances (chevrons, arrows, drawer entry side, table sort indicators, focus rings, dropdown anchoring) MUST flip correctly, and no element MUST clip, overlap, or escape its container. Full RTL UX polish and translated content for v1 are explicitly deferred.

**Accessibility**

- **FR-023**: All interactive primitives MUST be fully keyboard operable with visible focus states, logical tab order, and no keyboard traps.
- **FR-024**: All primitives MUST use semantic HTML first and ARIA only to fill semantic gaps; every interactive primitive MUST announce a meaningful name, role, state, and (where applicable) value to assistive technology.
- **FR-025**: `prefers-reduced-motion` MUST be respected; motion MUST be used to clarify state, not for theatre.
- **FR-026**: No critical information may be conveyed by color alone; semantic states MUST also be expressed via icon or text.
- **FR-027**: All primitives and their representative composed states MUST pass automated WCAG 2.2 AA accessibility checks as part of the foundation's test suite.

**Domain Composites**

- **FR-028**: BusTerminal-specific composite components MUST exist for: namespace card, queue row/card, topic row/card, subscription row/card, dead-letter status indicator, message count indicator, health summary indicator, discovery job status, entity relationship badge, environment badge, Azure resource link, metadata key-value panel, and a topology mini-map placeholder.

**Charts and Visualization**

- **FR-029**: A standard charting capability MUST be available for line, bar, area, and small-trend visualizations, themed to the brand and accessible in both modes. Topology/graph-specific visualization libraries are out of scope for this spec and require a future architectural decision.

**Documentation and Discoverability**

- **FR-030**: A component documentation system (Storybook or equivalent) MUST be operational and MUST include coverage of states, variants, dark/light modes, responsive behavior, and accessibility validation for every primitive and domain composite.
- **FR-031**: Authoritative documentation MUST exist for: design tokens, component usage and composition rules, accessibility guidance, theming guidance, frontend contribution rules, and agentic-coding implementation guidance.
- **FR-032**: The documentation MUST distinguish coding-agent development-time tooling (MCP servers used by humans and agents while building) from runtime application dependencies — MCP servers MUST NOT be described as runtime integrations of the product.

**Quality Gates**

- **FR-033**: Frontend linting, formatting, automated accessibility validation, component-test patterns, and end-to-end test scaffolding MUST be established as part of this foundation and MUST be runnable locally and in CI.
- **FR-034**: A `cn()`-style class-merging utility, a variant utility, formatting utilities, theme utilities, and any required accessibility utilities MUST be published as shared foundation utilities.

**Performance Budgets**

- **FR-035d**: A representative composed screen (sidebar + top bar + page header + data table + drawer + form + toast) MUST meet the Core Web Vitals "Good" thresholds on a mid-range laptop over broadband: **LCP ≤ 2.5s**, **INP ≤ 200ms**, **CLS ≤ 0.1**.
- **FR-035e**: The foundation MUST publish a documented soft initial-JS bundle target for the application shell (measured after Next.js 16 + shadcn/ui + theme provider + observability hook points are wired in) and MUST expose a means (e.g., bundle-analyzer report, CI summary) for contributors to detect regressions against that target.
- **FR-035f**: Foundation primitives MUST favor patterns that protect Web Vitals: skeletons that preserve layout stability (already covered by FR-019), font-loading strategies that avoid CLS, deferral of non-critical client JavaScript, and use of React Server Components by default per the Constitution's frontend standards.

**Browser Support**

- **FR-035a**: The foundation MUST officially support the **last two major versions** of evergreen desktop browsers (Chrome, Edge, Firefox, Safari) and the **last two major versions** of iPadOS Safari and Android Chrome. Internet Explorer, legacy non-Chromium Edge, and embedded webviews older than this window are explicitly out of scope.
- **FR-035b**: The foundation MAY use modern CSS features available across the support matrix without polyfills, including: CSS nesting, `:has()`, `color-mix()`, `@property`, container queries, OKLCH/OKLab color spaces, and CSS logical properties. Use of a feature outside the cross-browser support window MUST be gated by progressive enhancement (e.g., `@supports`) or avoided.
- **FR-035c**: The foundation MUST publish its browser support matrix in the documentation system so contributors and operators can verify environment compatibility before reporting issues.

**Frontend Observability**

- **FR-036**: A top-level error boundary MUST wrap the application shell and MUST forward unhandled rendering errors (and their React component stack) to the observability adapter; the user-facing error surface MUST remain on-brand and accessible.
- **FR-037**: Core Web Vitals (LCP, INP, CLS, TTFB, FCP) MUST be captured on every page load and forwarded to the observability adapter.
- **FR-038**: Route navigations within the application MUST emit a trace span that records the source route, destination route, and navigation duration, and MUST forward to the observability adapter.
- **FR-039**: Every UI-originated HTTP request to a BusTerminal backend API MUST carry W3C Trace Context headers (`traceparent`, and `tracestate` where present), so frontend operations correlate end-to-end with backend OpenTelemetry traces emitted to Azure Monitor. This propagation is a hard requirement and applies regardless of whether the Application Insights connection string is configured locally.
- **FR-040**: The foundation MUST ship an observability **adapter interface** plus two implementations: a **no-op adapter** (default when no provider is configured, used for OSS contributors and local development) and an **Application Insights browser adapter** activated when the connection string is supplied via environment variable. Swapping adapters MUST NOT require changes to feature code.
- **FR-041**: The observability surface MUST honor user privacy: PII MUST NOT be captured in trace attributes, error payloads, or Web Vitals events by default; correlation identifiers MUST be the only identifiers propagated unless an explicit opt-in surface is added by a future spec.

**Scope Boundaries**

- **FR-035**: This spec MUST NOT implement feature-specific business UI (discovery workflows, queue/topic management screens, governance workflows, environment management, production topology visualization, authentication business logic, backend APIs). Future feature specs MUST consume the artifacts produced here rather than redefine them.

### Key Entities *(conceptual, not data-model)*

- **Design Token**: A named, themeable value (color, spacing, radius, typography, elevation, motion, breakpoint, z-index, focus-ring, chart color) that MUST be referenced by name and MUST NOT be hardcoded. Tokens have dark and light variants where appropriate.
- **Theme**: A complete, named binding from the design-token system to concrete values for a viewing mode (dark, light). Themes are first-class peers, not skins layered over a default.
- **UI Primitive**: A foundation-owned, reusable, accessible component (Button, Input, Dialog, Table, etc.). Primitives compose; they do not get wrapped per feature.
- **Domain Composite**: A higher-order, BusTerminal-aware component (Queue row, Namespace card, Dead-letter indicator, Environment badge, etc.) composed from primitives and dedicated to a domain concept.
- **Layout Region**: A standardized chrome surface (app shell, sidebar, top bar, page container, section container, drawer, split/resizable panel) that defines where content lives across features.
- **Documentation Entry**: A canonical reference for a token, primitive, composite, or convention — including anatomy, states, accessibility behavior, usage rules, and live examples — accessible through the component documentation system.
- **Brand Asset**: An identity artifact (logo, wordmark, favicon, social preview, repo branding) delivered in production-ready formats with dark/light variants.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A new operational screen (sidebar + top bar + page header + sortable/filterable data table + entity detail drawer + validated form + toast) can be assembled from the published foundation using only its primitives, domain composites, and design tokens. **Zero** new color, spacing, typography, motion, or chrome values are introduced — verified by `pnpm audit:tokens && pnpm audit:strings && pnpm audit:directions` reporting zero violations on the assembled screen's source. (The earlier "under one development hour" wording was eliminated as arbitrary; the audit gate is the objective, reproducible measure.)
- **SC-002**: One hundred percent (100%) of foundation primitives and domain composites pass automated WCAG 2.2 AA checks in their published states across both themes.
- **SC-003**: Every UI value rendered by the foundation derives from a named design token; an audit of the foundation source reports zero hardcoded color, spacing, radius, elevation, or motion-duration literals outside the token definitions.
- **SC-004**: Theme switching (dark ↔ light) and first-load theme resolution complete with no visible flash of incorrect theme on a representative consumer-grade workstation.
- **SC-005**: Every primitive listed in FR-013 and every domain composite listed in FR-028 is present in the component documentation system with stories covering principal states, variants, both themes, and a passing accessibility check.
- **SC-006**: A contributor unfamiliar with the project can identify the correct primitive for ten representative UI tasks (list of entities, edit form, confirmation, destructive confirmation, transient notification, empty state, loading state, navigation chrome, modal workflow, drawer workflow) in under fifteen minutes by consulting only the published documentation.
- **SC-007**: A keyboard-only walkthrough of a representative composed screen completes every interaction (navigate, sort, filter, open detail, edit, save, dismiss) without using a pointing device, with visible focus throughout and no keyboard traps.
- **SC-008**: A reduced-motion preference removes or reduces all non-essential motion across the foundation; a manual review of the published stories under reduced-motion shows no theatrical transitions remain.
- **SC-009**: Foundation linting, formatting, accessibility validation, and component-test commands all run successfully locally and in CI on a clean checkout.
- **SC-010**: Operator-relevant primary content (entity names, identifiers, counts, statuses) remains visible on a 13" laptop viewport at the default zoom without requiring horizontal scroll on a representative composed screen.
- **SC-011**: Every foundation primitive and domain composite renders without visual breakage under `dir="rtl"` — a manual review against the published stories shows correctly mirrored directional affordances, no clipped or overlapping elements, and intact focus rings.
- **SC-012**: An audit of the foundation source reports zero hardcoded user-facing strings inside primitive or composite component source (all strings flow through the centralized string surface), and zero uses of physical `left`/`right` positioning properties where a logical equivalent exists.
- **SC-013**: With no Application Insights connection string configured, the foundation runs cleanly with the no-op observability adapter active — no console errors, no failed network calls to telemetry endpoints, and no degraded UX.
- **SC-014**: With the Application Insights connection string configured, an end-to-end test confirms that a UI-originated API request appears in Azure Monitor as a single distributed trace spanning the browser span and the backend span — verified by matching `traceparent` correlation, with no orphaned spans on either side.
- **SC-015**: A simulated unhandled rendering error is forwarded to the active observability adapter with the React component stack attached, while the user sees an on-brand, accessible error surface rather than a blank page or framework crash screen.
- **SC-016**: Core Web Vitals (LCP, INP, CLS, TTFB, FCP) are captured and emitted for every page load and exposed through the adapter interface — confirmed by inspecting the no-op adapter's debug pipeline in a local run.
- **SC-017**: All committed brand assets (logo wordmark, glyph mark, favicon set, social preview, repo branding) are plain, hand-readable SVG sources in the repository, render cleanly across the 16px–512px range, ship in dark and light variants, and carry a recorded human originality/licensing review for any AI-assisted asset.
- **SC-018**: A representative composed screen (sidebar + top bar + page header + data table + drawer + form + toast) renders correctly without visual regressions across the published support matrix — the latest two major versions of Chrome, Edge, Firefox, Safari (desktop), iPadOS Safari, and Android Chrome — verified through cross-browser smoke checks.
- **SC-019**: On a representative composed screen, measured on a mid-range laptop over broadband, the foundation meets the Core Web Vitals "Good" thresholds: **LCP ≤ 2.5s**, **INP ≤ 200ms**, **CLS ≤ 0.1**.
- **SC-020**: The application shell's initial JavaScript bundle size is published in the documentation system, baseline-captured at foundation handoff, and exposed through a contributor-visible reporting path (e.g., bundle-analyzer output in CI) so regressions are detectable in feature pull requests.

## Assumptions

- **Constitutional stack constraints**: The frontend stack is mandated by the BusTerminal Constitution and the source artifact (Next.js 16.x with App Router, TypeScript strict mode, React Server Components by default, Tailwind CSS v4.x, shadcn/ui as the primitive baseline, lucide-react icons, TanStack Table, React Hook Form + Zod, Recharts for standard charts, Framer Motion used sparingly, next-themes or equivalent, class-variance-authority/clsx/tailwind-merge). This spec assumes those choices as inputs rather than re-deciding them.
- **MCP servers are development-time tools, not runtime dependencies**: Next.js, shadcn/ui, Microsoft Learn, and context7 MCP servers are workflow aids for human and agentic contributors. The product itself does not depend on them.
- **Primary target is desktop operational use**: Tablet is a secondary target; mobile is read-only/limited. The foundation does not optimize for mobile-first workflows.
- **Dark mode is the primary operational experience**, with light mode a fully-supported peer — both themes are complete, not asymmetric.
- **shadcn/ui components, once generated, are project-owned source code** and will be reviewed, themed, and adapted to BusTerminal standards rather than treated as a black-box dependency.
- **Open-source community readiness**: The foundation must look and feel professional and community-friendly without leaning into vendor or enterprise-template aesthetics, and must support future SaaS evolution without a redesign.
- **No new UI libraries beyond the constitutional list will be introduced** without justification (specifically: no second design system, no CSS-in-JS, no alternative component library, no heavy chart suite, no graph/topology visualization library yet, no drag-and-drop, no rich text editor, no code editor component).
- **Logo and brand visual assets** may be produced or finalized during the implementation phase; the deliverable is a working set of brand assets, not a marketing-finalized logo selection.
- **Brand asset production pathway**: Assets are AI-assisted in initial generation and manually refined and hand-cleaned before commit. Committed SVG is plain/readable (no opaque rasters, no base64-embedded binaries), authored to be editable in source control, and redistributable under the project's open-source license. A human-recorded originality/licensing review is required for any AI-assisted asset before it lands on `main`. A future spec may commission a designer-led refresh; the foundation's job is to produce a working, on-brand set without gating on external delivery.
- **Tagline selection** is deferred; this spec does not lock in a final tagline.
- **Localization scope for v1**: User-facing content is English-only. The foundation is built RTL-safe (CSS logical properties throughout) with externalized strings and locale-aware date/number/duration formatting from day one, so translation and additional locales can be added by a future spec without rewriting primitives. The browser's locale governs date/number formatting by default.
- **Frontend observability scope**: The foundation ships observability hook points (top-level error boundary, Web Vitals capture, route-change traces, correlation-ID propagation) behind a swappable adapter interface, with a no-op adapter as the default and an Application Insights adapter activated when its connection string is supplied via environment variable. **W3C Trace Context propagation on UI-originated HTTP requests is required regardless of adapter choice**, so frontend traces correlate end-to-end with backend OpenTelemetry traces emitted to Azure Monitor (per Constitution Principle V — Operational Excellence and the Hosting and Infrastructure standards).
- **Backend trace-context expectations**: This spec assumes BusTerminal backend services accept and propagate W3C Trace Context (`traceparent`, `tracestate`) headers via their OpenTelemetry instrumentation. The handshake on the backend side is owned by backend specs.
- **Browser support matrix**: The supported runtime baseline is the last two major versions of evergreen desktop browsers (Chrome, Edge, Firefox, Safari) plus the last two major versions of iPadOS Safari and Android Chrome. This allows the foundation to adopt modern CSS (CSS nesting, `:has()`, `color-mix()`, `@property`, container queries, OKLCH) without polyfills. The matrix is published in the documentation system and is the testing target for cross-browser smoke checks.
- **Performance budgets**: The foundation adopts the Core Web Vitals "Good" thresholds (LCP ≤ 2.5s, INP ≤ 200ms, CLS ≤ 0.1) on a representative composed screen, plus a documented soft initial-JS bundle target for the application shell whose actual value is set by the implementation plan after the shell is wired up. This aligns with the Constitution's decision priorities (which place performance below operational simplicity but above premature optimization) and with FR-037's Web Vitals capture.

## Dependencies

- **BusTerminal Constitution v1.0.0** (`.specify/memory/constitution.md`), in particular: API-First Design (UI consumes documented contracts), Operational Excellence (observability is a product surface, Azure Monitor + Application Insights via OpenTelemetry), and the Frontend Technology Standards (Next.js 16.x, shadcn/ui, Tailwind CSS, accessibility, dark-mode support, keyboard-friendly workflows).
- **W3C Trace Context** specification — the foundation propagates `traceparent`/`tracestate` headers on UI-originated requests so frontend and backend traces correlate.
- **Backend OpenTelemetry instrumentation** — the foundation assumes backend services accept and propagate W3C Trace Context headers; the backend side of the handshake is owned by backend specs.
- **Source artifact** `speckit-artifacts/001-brand-system-and-design-foundation.md` for prescriptive design and stack guidance.

## Explicit Non-Goals

This spec does **not** define or implement:

- Business logic, backend APIs, or authentication business logic
- Service Bus discovery, ingestion, indexing, or topology resolution
- Queue, topic, subscription, governance, or environment management screens
- Production topology visualization (a future spec with a dedicated visualization decision)
- Data persistence architecture
- Monetization or SaaS-tier strategy
- A finalized tagline or final marketing copy

Future feature specs **must** consume the artifacts produced here rather than redefining UI foundations.
