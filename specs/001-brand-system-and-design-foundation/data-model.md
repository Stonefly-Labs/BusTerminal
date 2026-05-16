# Phase 1 — Data Model: Brand System and Design Foundation

**Spec**: [`spec.md`](./spec.md)
**Plan**: [`plan.md`](./plan.md)

This document models the **conceptual entities** that the foundation manipulates. The foundation is frontend-only and does not introduce a database schema; instead, the entities below are realized as TypeScript types, design-token modules, MDX documentation pages, and committed SVG / metadata files. The corresponding contract surfaces are in [`contracts/`](./contracts/).

Each entity declares its purpose, observable fields, relationships, validation rules, and (where relevant) state transitions.

---

## 1. Design Token

**Definition**: A named, themeable value that primitives and composites consume by reference. Tokens are the only sanctioned source of color, typography, spacing, radius, elevation, motion-duration, border, layout-sizing, breakpoint, z-index, focus-ring, and data-visualization values inside the foundation.

**Fields**:

| Field | Type | Notes |
|---|---|---|
| `name` | string | Stable identifier (e.g., `color.surface.canvas`, `spacing.4`, `radius.md`, `motion.duration.fast`). Naming follows the `<category>.<role>.<modifier?>` convention. |
| `category` | `'color' \| 'spacing' \| 'radius' \| 'typography' \| 'elevation' \| 'motion' \| 'border' \| 'layout' \| 'breakpoint' \| 'z-index' \| 'focus-ring' \| 'chart-color'` | Enforced enumeration. |
| `semanticRole` | `'surface' \| 'foreground' \| 'border' \| 'accent' \| 'success' \| 'warning' \| 'error' \| 'info' \| 'disabled' \| 'interactive' \| 'focus' \| 'neutral' \| 'data-1' … 'data-12' \| 'n/a'` | Required for color tokens; optional for others. |
| `lightValue` | string \| number | The token's resolved value in the light theme (e.g., `oklch(98% 0 0)`, `8`, `200ms`). Required. |
| `darkValue` | string \| number | The token's resolved value in the dark theme. Required for color/elevation tokens; equal to `lightValue` for theme-agnostic tokens (spacing, motion, radius, typography size). |
| `wcagAA` | `'pass' \| 'pass-large' \| 'n/a'` | For color foreground/background pairings — confirmed by the audit script. |
| `intendedUsage` | string | One-sentence description (e.g., "Default surface for page canvases and cards"). |
| `usageRules` | string[] | "Do this / don't do this" notes (e.g., "Do not use as a text color"). |

**Relationships**:

- A `Theme` (entity 2) is the binding from every token to a concrete `lightValue` / `darkValue`.
- A `UI Primitive` (entity 3) and a `Domain Composite` (entity 4) consume tokens by name only — never by literal value.
- A `Documentation Entry` (entity 6) is published for every token.

**Validation rules**:

- Tokens MUST be defined in `web/styles/tokens.css` (CSS variables) and exposed in TypeScript via the design-token contract (see [`contracts/design-tokens.ts`](./contracts/design-tokens.ts)).
- Tokens MUST NOT be redefined inside component source.
- For color tokens with `semanticRole ∈ {success, warning, error, info, accent, interactive}`, the foreground-on-surface pairing MUST meet WCAG AA contrast in both themes (FR-007 / FR-027).
- The audit gate is `pnpm audit:tokens`, which fails the build if any primitive or composite source contains a hardcoded color/spacing/radius/elevation/motion literal (SC-003).

**State**: Tokens are static once defined; changes require a foundation PR. There are no runtime transitions.

---

## 2. Theme

**Definition**: A complete, named binding from the design-token system to concrete values for a viewing mode. Themes are first-class peers — neither is a "skin layered over" the other.

**Fields**:

| Field | Type | Notes |
|---|---|---|
| `id` | `'light' \| 'dark' \| 'system'` | `system` is the initial resolver state, not a target binding; the resolver yields `light` or `dark`. |
| `displayName` | string | Locale-formatted, sourced from the i18n string surface. |
| `tokenBindings` | record\<`Design Token.name`, resolved-value\> | Conceptually a map; physically expressed as CSS custom properties under `:root` (light) and `:root.dark` (dark). |
| `preferredBy` | `'user-explicit' \| 'system' \| 'default'` | Drives the persistence rules in FR-006. |

**Relationships**:

- Each `Theme` resolves every `Design Token` (entity 1) to a value.
- The active theme is observed by every `UI Primitive` (entity 3), `Domain Composite` (entity 4), `Layout Region` (entity 5), and every Storybook story.
- The theme contract is in [`contracts/theme-provider.ts`](./contracts/theme-provider.ts).

**Validation rules**:

- Both `light` and `dark` MUST be **complete** — every token has a binding (FR-005).
- On first load, the theme resolver MUST honor `prefers-color-scheme` and the persisted preference, and MUST NOT cause a flash of incorrect theme (FR-006 / SC-004).
- The user's explicit override MUST persist across reloads in `localStorage` (key: `bt:theme`, value: `'light' | 'dark' | 'system'`).

**State transitions**:

```text
default (system) ──user toggles light──► user-explicit:light ──user toggles dark──► user-explicit:dark
        │                                                                                   │
        └──user toggles dark──► user-explicit:dark ──user toggles light──► user-explicit:light
        │
        └──user toggles back to "system"──► default (system), persisted preference cleared
```

---

## 3. UI Primitive

**Definition**: A foundation-owned, reusable, accessible component published from `web/components/ui/` (shadcn/ui-derived) or its sibling foundation folders (`app-shell/`, `data-display/`, `data-table/`, `forms/`, `feedback/`, `navigation/`, `charts/`).

**Fields**:

| Field | Type | Notes |
|---|---|---|
| `name` | string | Pascal-cased component name (e.g., `Button`, `DataTable`, `Sheet`, `CommandPalette`). |
| `category` | `'inputs' \| 'overlays' \| 'layout' \| 'data-display' \| 'data-table' \| 'forms' \| 'feedback' \| 'navigation' \| 'charts' \| 'utility'` | Used for documentation grouping. |
| `props` | typed prop interface | Exposed; reviewed for backwards-compatible evolution. |
| `variants` | record\<variantName, variantValueSet\> | Defined with `class-variance-authority` (FR-014). |
| `states` | enumerated set of visual states | (e.g., `default`, `hover`, `focus`, `active`, `disabled`, `pending`, `error`). |
| `a11yProfile` | object describing semantic role, ARIA needs, keyboard contract | (e.g., for `Dialog`: role=`dialog`, focus-trap, `Esc` to close, returns focus on close). |
| `themesSupported` | `['light', 'dark']` | Both required (FR-005). |
| `rtlSafe` | boolean | Must be `true` (FR-022d / SC-011). |
| `reducedMotionAware` | boolean | Must be `true` for any primitive that emits motion (FR-025). |
| `stringDependencies` | string[] | Keys consumed from the i18n string surface (entity 7); MUST NOT contain hardcoded user-facing copy (FR-022a / SC-012). |
| `tokenDependencies` | string[] | Token names consumed; MUST NOT contain hardcoded values (SC-003). |

**Relationships**:

- A `UI Primitive` MAY be composed by zero or more `Domain Composites` (entity 4).
- Every primitive has at least one `Documentation Entry` (entity 6) covering principal states, variants, both themes, and accessibility validation (FR-030 / SC-005).
- Primitives consume `Design Tokens` (entity 1) and `i18n Strings` (entity 7).

**Validation rules**:

- All 27 primitives listed in [FR-013](./spec.md) MUST exist.
- Each primitive MUST pass automated WCAG 2.2 AA checks (`vitest-axe` + Storybook a11y addon) in every published state (FR-027 / SC-002).
- Each primitive MUST be keyboard-operable with visible focus and no traps (FR-023 / SC-007).
- Semantic state MUST be conveyed by icon or text in addition to color (FR-026).
- Each primitive MUST be composable; wrapper-heavy abstractions are prohibited (FR-014).

**State**: Per-component states are listed under `states`. There are no global lifecycle transitions for the entity itself.

---

## 4. Domain Composite

**Definition**: A higher-order, BusTerminal-aware component composed from primitives and dedicated to a Service Bus domain concept. Domain composites are where consistency for the product's vocabulary is enforced (Constitution Principle III — Strong Domain Modeling).

**Fields**:

| Field | Type | Notes |
|---|---|---|
| `name` | string | `NamespaceCard`, `QueueRow`, `TopicRow`, `SubscriptionRow`, `DeadLetterIndicator`, `MessageCountIndicator`, `HealthSummaryIndicator`, `DiscoveryJobStatus`, `EntityRelationshipBadge`, `EnvironmentBadge`, `AzureResourceLink`, `MetadataKeyValuePanel`, `TopologyMiniMapPlaceholder`. |
| `domainConcept` | `'namespace' \| 'queue' \| 'topic' \| 'subscription' \| 'dead-letter' \| 'message-count' \| 'health' \| 'discovery-job' \| 'entity-relationship' \| 'environment' \| 'azure-resource' \| 'metadata' \| 'topology'` | Canonical vocabulary per Constitution III. |
| `propsShape` | typed prop interface (uses canonical entity props) | (e.g., `QueueRow` takes a `Queue` shape with `name`, `messageCount`, `deadLetterCount`, `status`, `environment`, …). |
| `composedFrom` | `UI Primitive.name[]` | Composition record so the audit can prove no bespoke styling. |
| `states` | enumerated state set | (e.g., `QueueRow`: `active`, `idle`, `error`, `dead-lettered`). |

**Relationships**:

- Composed from `UI Primitives` (entity 3) only.
- Consume `Design Tokens` (entity 1), `i18n Strings` (entity 7), and `Iconography` (entity 9).
- Each composite has at least one `Documentation Entry` (entity 6) with stories for every state, both themes, and an a11y pass (FR-030 / SC-005).

**Validation rules**:

- All 13 composites listed in [FR-028](./spec.md) MUST exist.
- Composites MUST use the canonical domain vocabulary in prop names, ARIA labels, and tooltip text.
- Composites MUST NOT introduce new color, spacing, typography, or chrome values (SC-001 / SC-003).
- Composites MUST be RTL-safe (SC-011) and string-externalized (SC-012).

**State**: As declared per-composite under `states`. The `TopologyMiniMapPlaceholder` is intentionally inert — it renders a placeholder slot only and exposes no interactive state.

---

## 5. Layout Region

**Definition**: A standardized chrome surface that defines where content lives across all foundation-built screens.

**Fields**:

| Field | Type | Notes |
|---|---|---|
| `name` | string | `AppShell`, `Sidebar`, `TopBar`, `PageContainer`, `SectionContainer`, `Drawer`, `Sheet`, `SplitPanel`, `ResizablePanel`, `Footer`, `BreadcrumbBar`. |
| `slotContract` | TypeScript slot interface | (e.g., `AppShell` exposes `sidebar`, `topBar`, `main`, `footer` slots). |
| `breakpointBehavior` | record\<breakpoint, behavior\> | (e.g., `Sidebar`: collapsed → 56px at `<lg`; expanded → 280px at `≥lg`). |
| `keyboardContract` | string | The keyboard navigation expectations for the region (e.g., `Cmd/Ctrl+B` toggles sidebar, focus order preserved across collapse). |

**Relationships**:

- Compose `UI Primitives` (entity 3).
- Are themed via `Design Tokens` (entity 1).
- Are consumed by route layouts in `app/(app)/layout.tsx` and other route groups.

**Validation rules**:

- The application shell MUST optimize for wide-desktop, support tablet, and remain usable on mobile for read-only triage (FR-012 / SC-010).
- All regions MUST be keyboard operable with visible focus and predictable focus return when overlays close (FR-023 / SC-007).
- All regions MUST be RTL-safe (FR-022d / SC-011).

**State transitions** (region-specific examples):

```text
Sidebar:            collapsed ⇄ expanded   (user toggle; persisted)
Drawer/Sheet:       closed → opening → open → closing → closed   (focus-trap active in "open")
ResizablePanel:     idle ⇄ dragging       (Esc cancels drag; size persisted)
CommandPalette:     closed → open         (Cmd/Ctrl+K; closes on Esc; focus restored)
```

---

## 6. Documentation Entry

**Definition**: A canonical reference for a token, primitive, composite, layout region, or convention — including anatomy, states, accessibility behavior, usage rules, and live examples — accessible through the component documentation system.

**Fields**:

| Field | Type | Notes |
|---|---|---|
| `subject` | `Design Token \| UI Primitive \| Domain Composite \| Layout Region \| Convention` | The thing being documented. |
| `purpose` | string | One-paragraph description of intent. |
| `anatomy` | structured description / annotated diagram | Required for primitives and composites. |
| `propsTable` | structured prop reference | Required for primitives and composites. |
| `usageRules` | string[] | "Do this / don't do this". |
| `accessibilityNotes` | structured notes | Required; covers keyboard contract, ARIA, screen-reader announcement, reduced-motion behavior, color-vs-icon affordances. |
| `themingNotes` | string | Required for primitives — which tokens are consumed; how variants react to theme. |
| `liveExamples` | Storybook stories | One or more, covering principal states, all variants, both themes, and a representative RTL view. |
| `a11yValidationResult` | `'passed' \| 'failed-with-justification'` | CI-produced (axe-via-Storybook-test-runner). |

**Relationships**:

- Every published entity (1–5) has at least one Documentation Entry (FR-030 / FR-031 / SC-005 / SC-006).
- Documentation Entries are stored as `*.stories.tsx` (live examples) and `*.mdx` (narrative pages) inside `web/stories/` and alongside components.
- The documentation system also publishes top-level entries for: brand voice (`01-brand.mdx`), tokens (`02-design-tokens.mdx`), typography (`03-typography.mdx`), iconography (`04-iconography.mdx`), accessibility (`05-accessibility.mdx`), theming (`06-theming.mdx`), frontend contribution rules (`07-contribution.mdx`), agentic-coding guidance (`08-agentic-coding.mdx`), and browser support (`09-browser-support.mdx`).

**Validation rules**:

- Every primitive and composite has live-example stories covering principal states, all variants, both themes (FR-030 / SC-005).
- Every Storybook story passes axe via the Storybook test runner (FR-027).
- Documentation MUST distinguish development-time MCP tooling from runtime dependencies (FR-032).

---

## 7. i18n String Entry

**Definition**: A user-facing string sourced from the centralized string surface. The surface exists so a future translation spec can swap implementations without rewriting components.

**Fields**:

| Field | Type | Notes |
|---|---|---|
| `key` | dotted, hierarchical string | (e.g., `table.toolbar.search.placeholder`, `dialog.destructive.confirmLabel`). |
| `englishValue` | string | The v1 English copy. |
| `description` | string | Translator note describing context. |
| `interpolations` | `Record<string, 'string' \| 'number' \| 'date'>` | Declared interpolation slots (empty when none). |
| `consumers` | (primitive \| composite).name[] | Read-only audit field. |

**Relationships**:

- Read by `UI Primitives` (entity 3) and `Domain Composites` (entity 4) through a `t(key)` accessor.
- Locale-aware **values** (dates, numbers, durations, byte counts) flow through `Locale Formatter` helpers (entity 8), not the string surface.

**Validation rules**:

- No hardcoded user-facing strings may live inside primitive or composite source — audit gate is `pnpm audit:strings` (SC-012).
- `englishValue` MUST be present for every key in v1; other locales are deferred.

**State**: Static per build; changes require a foundation PR.

---

## 8. Locale Formatter Helper

**Definition**: A pure function that formats a typed input (date, number, byte count, duration) using the active locale.

**Fields**:

| Field | Type | Notes |
|---|---|---|
| `name` | string | `formatDate`, `formatTime`, `formatRelativeTime`, `formatDuration`, `formatNumber`, `formatBytes`. |
| `inputType` | `Date \| number \| { value: number, unit: TimeUnit }` | Strictly typed input. |
| `options` | `Intl.*Options` | Locale-aware options exposed to callers. |
| `localeSource` | `'browser' \| 'override'` | Default = browser; override hook reserved for future translation spec. |

**Relationships**:

- Wrap native `Intl` APIs (`Intl.DateTimeFormat`, `Intl.RelativeTimeFormat`, `Intl.NumberFormat`, `Intl.ListFormat`).
- Used by primitives and composites wherever locale-sensitive data is rendered (FR-022c).

**Validation rules**:

- Browser support matrix guarantees full `Intl` coverage — no polyfill required (FR-035b).
- Helpers MUST be pure and side-effect-free.

---

## 9. Iconography Entry

**Definition**: A named, themable icon — either a general-purpose icon (lucide-react) or a domain-specific concept icon (curated mapping today; potentially custom in a future spec).

**Fields**:

| Field | Type | Notes |
|---|---|---|
| `name` | string | (e.g., `queue`, `topic`, `subscription`, `dead-letter`, `namespace`, `message-flow`, `topology`, `discovery`, `relay`, `environment`). |
| `lucideIconName` | string | The lucide-react component name currently mapped (e.g., `MailMinus` → `dead-letter`). |
| `strokeWidth` | number | Consistent across the family (default `1.5`). |
| `accessibleLabel` | i18n string key | When the icon is meaningful (not purely decorative). |

**Relationships**:

- Lives in `web/lib/iconography/domain-icons.ts` (FR-021 / FR-022 / Research R9).
- Consumed by domain composites and primitives.

**Validation rules**:

- Single icon family across the product (FR-021).
- Consistent stroke widths and small-size readability (FR-021).
- When semantic, icons MUST be paired with a label or text (FR-026).

**State**: Static per build.

---

## 10. Observability Event

**Definition**: A typed event surfaced to the active `ObservabilityAdapter`. Events are the only conduit from foundation code to telemetry — feature code does not bypass the adapter.

**Fields**:

| Field | Type | Notes |
|---|---|---|
| `kind` | `'error' \| 'web-vital' \| 'route-change' \| 'custom'` | Tagged union. |
| `traceId` | string (32-hex) | W3C Trace Context trace ID. |
| `spanId` | string (16-hex) | W3C Trace Context span ID. |
| `traceFlags` | string (2-hex) | W3C Trace Context flags. |
| `attributes` | sanctioned attribute map (see below) | Strictly typed by `kind`; NO PII fields by construction (FR-041). |

**Attribute shapes per `kind`**:

- `error`: `{ message: string; category: string; componentStack?: string; route?: string }`
- `web-vital`: `{ name: 'LCP' \| 'INP' \| 'CLS' \| 'TTFB' \| 'FCP'; value: number; id: string; navigationType: string }`
- `route-change`: `{ fromRoute: string; toRoute: string; durationMs: number }`
- `custom`: `{ name: string; correlationIds?: string[] }`

**Relationships**:

- Produced by the error boundary, the Web Vitals collector, the route-change emitter, and (optionally) feature code via the adapter.
- Consumed by the active `ObservabilityAdapter` (no-op or Application Insights).
- Trace IDs are also the IDs propagated via `traceparent` on UI-originated HTTP requests (FR-039).

**Validation rules**:

- No PII fields appear in any attribute shape (FR-041). The TypeScript types enforce this — feature code that tries to pass disallowed fields fails type-checking.
- `traceparent` MUST be emitted on every UI-originated HTTP request regardless of adapter configuration (FR-039 / SC-014).
- Web Vitals are captured on **every** page load (FR-037 / SC-016).

---

## 11. Brand Asset

**Definition**: An identity artifact delivered in production-ready formats with dark/light variants and the recorded review note that satisfies FR-002a / FR-002b.

**Fields**:

| Field | Type | Notes |
|---|---|---|
| `name` | string | `wordmark`, `glyph`, `favicon`, `social-preview`, `repo-banner`. |
| `sourceFormat` | `'plain-svg'` | Required. No opaque rasters, no base64-embedded binaries. |
| `darkVariantPath` | repo-relative path | Required for any asset that renders on a dark surface. |
| `lightVariantPath` | repo-relative path | Required for any asset that renders on a light surface. |
| `exportedFormats` | `('svg' \| 'png' \| 'ico' \| 'apple-touch-icon')[]` | Variant exports for browser / OS surfaces. |
| `productionSizes` | number[] | Pixel sizes the asset must render correctly at (e.g., `[16, 24, 32, 48, 64, 128, 256, 512]`). |
| `aiAssisted` | boolean | If `true`, a `REVIEW.md` is required adjacent to the source. |
| `reviewRecordPath` | repo-relative path or `null` | Path to `REVIEW.md` when `aiAssisted === true`. |
| `license` | string | Project open-source license (must permit redistribution). |

**Relationships**:

- Referenced by `app/icon.tsx`, `app/apple-icon.tsx`, `app/opengraph-image.tsx`, the Storybook brand page (`01-brand.mdx`), and the README repo branding.
- Replaces the in-tree placeholder mark used during Phases A – E without changing import paths (Research R3).

**Validation rules**:

- `sourceFormat` MUST be `'plain-svg'`. Audit gate: a script that opens each committed SVG and rejects `<image>` tags, base64 data URIs, and embedded raster references (FR-002a / SC-017).
- For every asset where `aiAssisted === true`, the `reviewRecordPath` MUST resolve to a `REVIEW.md` that contains all five required checks signed by a human contributor (FR-002b — see [`contracts/brand-asset-review.md`](./contracts/brand-asset-review.md)).
- Every asset MUST render cleanly across `productionSizes` 16 → 512 px (FR-002 / SC-017).
- Every asset MUST ship dark and light variants where it renders on themed surfaces (FR-002 / FR-005).

**State transitions** (asset lifecycle):

```text
concept   ──(Stage 1: AI-assisted exploration)──►  draft-raster
draft-raster   ──(Stage 2: vector authoring + hand cleanup)──►  source-svg
source-svg   ──(Stage 3: variant export)──►  multi-format-bundle
multi-format-bundle   ──(Stage 4: human originality + licensing review)──►  reviewed (REVIEW.md committed)
reviewed   ──(Stage 5: commit + integrate)──►  in-foundation (placeholder hot-swapped)
```

---

## Cross-Entity Validation Summary

The audit gates that the foundation publishes are the cross-entity enforcement of the rules above:

| Audit | Enforces | Spec link |
|---|---|---|
| `pnpm audit:tokens` | No hardcoded color / spacing / radius / elevation / motion literals inside primitive or composite source. | SC-003 |
| `pnpm audit:strings` | No hardcoded user-facing strings inside primitive or composite source. | SC-012 |
| `pnpm audit:directions` | No physical `left` / `right` / `text-align: left|right` outside token files. | SC-012 |
| `pnpm audit:svg-hygiene` | All committed brand SVGs are plain (no `<image>`, no base64 data URIs). | FR-002a / SC-017 |
| `pnpm audit:review-records` | Every AI-assisted brand asset has a `REVIEW.md` with all required checks signed off. | FR-002b |
| `pnpm test` + `pnpm test:storybook` | Every primitive and composite is axe-clean in every published state, both themes. | FR-027 / SC-002 / SC-005 |
| `pnpm test:e2e` | Cross-browser smoke (Chromium / Firefox / WebKit); keyboard-only walkthrough; Core Web Vitals probe. | FR-035a / SC-007 / SC-018 / SC-019 |
| `pnpm analyze` + `scripts/bundle-diff.mjs` | Initial-JS bundle stays inside the soft / hard envelope; regressions alert. | FR-035e / SC-020 |
