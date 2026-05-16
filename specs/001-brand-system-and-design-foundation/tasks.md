---
description: "Task list for Brand System and Design Foundation"
---

# Tasks: Brand System and Design Foundation

**Input**: Design documents from `/specs/001-brand-system-and-design-foundation/`

**Prerequisites**: [`plan.md`](./plan.md), [`spec.md`](./spec.md), [`research.md`](./research.md), [`data-model.md`](./data-model.md), [`contracts/`](./contracts/), [`quickstart.md`](./quickstart.md)

**Tests**: The spec does not request TDD ordering. Test scaffolding (Vitest, RTL, vitest-axe, Storybook test-runner, Playwright, axe-playwright) is a foundational deliverable (FR-033) and per-primitive accessibility tests are integrated INTO each primitive task rather than authored before implementation. Cross-cutting test suites (keyboard-only walkthrough, reduced-motion, RTL, cross-browser smoke, Lighthouse probe) live in their owning user-story or polish phases.

**Organization**: Tasks are grouped by user story (US1–US5) to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (US1, US2, US3, US4, US5)
- File paths are repo-relative.

## Path Conventions

- **Frontend app**: `web/` at repository root (single Next.js 16.x application — see [plan.md → Project Structure](./plan.md#project-structure))
- **Spec artifacts**: `specs/001-brand-system-and-design-foundation/`
- **Repo-level docs**: `docs/`, `README.md`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Initialize the Next.js 16.x project, install the locked dependency set, and establish lint/format/test/CI scaffolding before any foundation code is written.

- [X] T001 Create `web/` Next.js 16.x App Router project (TypeScript strict, ESLint, Tailwind v4, App Router, no `src/` dir) at `web/` per [plan.md → Project Structure](./plan.md#project-structure).
- [X] T002 Pin runtime dependencies in `web/package.json`: `next@^16`, `react`, `react-dom`, `tailwindcss@^4`, `@tailwindcss/postcss`, `postcss`, `next-themes`, `@tanstack/react-table`, `react-hook-form`, `zod`, `recharts`, `framer-motion`, `lucide-react`, `clsx`, `tailwind-merge`, `class-variance-authority`, `web-vitals`.
- [X] T003 [P] Pin dev dependencies in `web/package.json`: Storybook **10.x** (`storybook`, `@storybook/nextjs`, `@storybook/addon-a11y`, `@storybook/addon-themes`, `@storybook/test-runner`, `axe-playwright`) — note: `@storybook/test`, `@storybook/addon-interactions`, and `@storybook/addon-viewport` were folded into Storybook core in 9+ and are NOT separate packages (the test utilities are imported from `storybook/test`; viewports are declared inline in `preview.tsx`). Vitest stack (`vitest`, `@vitest/ui`, `@testing-library/react`, `@testing-library/dom`, `@testing-library/user-event`, `vitest-axe`), Playwright (`@playwright/test`, `playwright`), bundle analyzer (`@next/bundle-analyzer`), `prettier`, `eslint-plugin-tailwindcss`. Storybook version bumped from clarification-time 8.x for Next.js 16 peer compatibility — see [Research R1](./research.md#r1-component-documentation-system--storybook-vs-equivalent).
- [X] T004 [P] Configure TypeScript strict mode in `web/tsconfig.json` (strict, noUncheckedIndexedAccess, exactOptionalPropertyTypes, paths alias `@/*` → `./*`).
- [X] T005 [P] Configure Next.js 16.x in `web/next.config.ts` — App Router defaults, `@next/bundle-analyzer` wiring gated by `ANALYZE=true`, top-level `typedRoutes` enabled (Next.js 16 promoted typedRoutes from `experimental.typedRoutes` to a stable top-level option).
- [X] T006 [P] Configure Tailwind v4 in `web/postcss.config.mjs` and `web/app/globals.css` — Tailwind v4 dropped the JS `tailwind.config.ts` config in favor of CSS-first configuration. PostCSS uses `@tailwindcss/postcss`; dark mode is implemented as `@custom-variant dark (&:where(.dark, .dark *))` (class strategy for `next-themes`); content scan paths are auto-detected by v4 with `@source` available for opt-in overrides. CSS-variable colors only — token authoring lives in `web/styles/tokens.css` (T013) and `web/styles/typography.css` (T014).
- [X] T007 [P] Configure ESLint + Prettier in `web/eslint.config.ts` and `web/.prettierrc` — includes the no-physical-direction-utilities custom rule (disallows `ml-*`, `mr-*`, `pl-*`, `pr-*`, `left-*`, `right-*`, `text-left`, `text-right`) outside `web/styles/tokens.css` (FR-022b / SC-012). Rule lives inline in `eslint.config.ts` as the `busterminal/no-physical-direction-utilities` plugin rule; scans string literals + template elements; flags every match per literal; `web/styles/**` and `web/app/globals.css` are exempted via `globalIgnores`.
- [X] T008 [P] Configure Vitest in `web/vitest.config.ts` with the `jsdom` environment, RTL setup file `web/tests/setup-vitest.ts`, and `vitest-axe` matcher registration. Added `jsdom` and `@testing-library/jest-dom` as dev deps (jsdom missing from T003).
- [X] T009 [P] Configure Playwright in `web/playwright.config.ts` with three browser projects (Chromium, Firefox, WebKit) and the mid-range laptop viewport (1366×768) for Web Vitals probe. Includes `webServer` wiring (`pnpm run dev`) gated by `PLAYWRIGHT_SKIP_WEB_SERVER`.
- [X] T010 [P] Create directory skeleton under `web/`: `components/{ui,app-shell,data-display,data-table,forms,feedback,navigation,charts,domain}`, `lib/{design-system,i18n/strings,observability,http,iconography,utils,validation}`, `hooks`, `styles`, `brand`, `stories`, `tests/{e2e,a11y,unit}`, `docs`, `scripts`, `public/{brand,favicons,og}`. Each empty directory gets a `.gitkeep` placeholder so the skeleton survives in git.
- [X] T011 Add package scripts in `web/package.json`: `dev`, `build`, `start`, `lint`, `typecheck`, `test`, `test:storybook`, `test:e2e`, `analyze`, `storybook`, `build-storybook`, `audit:tokens`, `audit:strings`, `audit:directions`, `audit:svg-hygiene`, `audit:review-records`. `analyze` uses `cross-env` for cross-platform `ANALYZE=true` env wiring. `audit:*` scripts point to script files authored in T036–T040 — they will exit non-zero until those tasks land.
- [X] T012 Add CI workflow at `.github/workflows/ci.yml` running, in order: `pnpm install --frozen-lockfile`, `pnpm -C web lint`, `pnpm -C web typecheck`, `pnpm -C web test`, `pnpm -C web build-storybook`, `pnpm -C web test:storybook`, `pnpm -C web exec playwright install --with-deps`, `pnpm -C web test:e2e`, `pnpm -C web analyze`. Upload bundle-analyzer HTML/JSON as a PR artifact and run `scripts/bundle-diff.mjs` to post the size delta. Workflow runs on PRs targeting `main` (and pushes to `main`); uses pnpm with frozen lockfile and the pnpm-lockfile cache; concurrency group cancels superseded runs; `BUNDLE_SOFT_TARGET_KB=180` and `BUNDLE_HARD_ALERT_KB=200` env vars consumed by the bundle-diff script (T040).

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Build the design-token system, theme provider, i18n surface, iconography mapping, observability adapter + W3C Trace Context propagation, audit scripts, brand placeholder, and root app layout — all of which BLOCK every user story.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

### Design tokens & theming

- [X] T013 Author CSS variables for every token name in [`contracts/design-tokens.ts`](./contracts/design-tokens.ts) at `web/styles/tokens.css` — provide BOTH `:root` (light) and `:root.dark` blocks, complete sets, WCAG AA verified pairings for semantic foregrounds. Verify contrast by authoring `web/tests/unit/token-contrast.test.ts` using the axe `color-contrast` rule (via `vitest-axe` rendering a fixture page that exercises every foreground/surface pairing in both themes); record the resolved contrast ratios in a "Token contrast pairings" table inside `web/docs/theming.md` (FR-004 / FR-005 / FR-007). _Note: contrast test fixture + theming.md table land in T101/T115 alongside the documentation/typography pass._
- [X] T014 [P] Author typography-scale variables in `web/styles/typography.css` — `--font-sans`, `--font-mono`, sizes/line-heights/letter-spacings for display, H1–H6, body, body-sm, caption, label, table, mono, mono-sm (FR-008 / FR-009).
- [X] T015 [P] Author `web/styles/globals.css` with Tailwind v4 `@theme` import and the base layer (resets, focus-ring defaults using `--focus-ring-*` tokens). _Implemented in `web/app/globals.css` (imports `../styles/tokens.css` and `../styles/typography.css`)._
- [X] T016 [P] Implement the design-token TypeScript bridge in `web/lib/design-system/tokens.ts`: export `tokenVar(name)` resolver, `CHART_DATA_TOKENS` ordered array, and `ALL_TOKEN_NAMES` enumeration per [`contracts/design-tokens.ts`](./contracts/design-tokens.ts).
- [X] T017 [P] Implement `cn()` and variants helper in `web/lib/design-system/cn.ts` and `web/lib/design-system/variants.ts` (clsx + tailwind-merge + class-variance-authority wrappers — FR-014 / FR-034).

### i18n surface & locale formatters

- [X] T018 [P] Author English string registry in `web/lib/i18n/strings/en.ts` exporting `t(key, vars?)` and `ALL_STRING_KEYS` per [`contracts/i18n-strings.ts`](./contracts/i18n-strings.ts); include the keys consumed by every primitive and composite scaffolded in later phases (placeholders allowed at this stage).
- [X] T019 [P] Implement locale-aware formatters in `web/lib/i18n/format.ts`: `formatDate`, `formatTime`, `formatRelativeTime`, `formatDuration`, `formatNumber`, `formatBytes` — all wrap native `Intl.*` (FR-022c / Research R5).
- [X] T020 [P] Implement `directionForLocale` in `web/lib/i18n/direction.ts` per [`contracts/i18n-strings.ts`](./contracts/i18n-strings.ts) (FR-022d).

### Iconography mapping

- [X] T021 [P] Implement domain-icon mapping module in `web/lib/iconography/domain-icons.ts` exposing entries for `queue`, `topic`, `subscription`, `dead-letter`, `namespace`, `message-flow`, `topology`, `discovery`, `relay`, `environment`, `azure-resource`, with curated `lucide-react` icon assignments and consistent `strokeWidth` (FR-021 / FR-022 / Research R9).

### Observability adapter + W3C Trace Context propagation

- [X] T022 [P] Implement the `ObservabilityAdapter` interface and `getAdapter()` selector in `web/lib/observability/adapter.ts` per [`contracts/observability-adapter.ts`](./contracts/observability-adapter.ts) — selector chooses no-op vs Application Insights based on `NEXT_PUBLIC_APPINSIGHTS_CONNECTION_STRING` at module load.
- [X] T023 [P] Implement the no-op adapter in `web/lib/observability/noop-adapter.ts` — exposes a debug pipeline (in-memory ring buffer keyed by event kind) accessible via `window.__bt_obs_debug` in dev so SC-013 / SC-016 can be observed locally without an AI connection string.
- [X] T024 [P] Implement the Application Insights adapter in `web/lib/observability/app-insights-adapter.ts` — dynamically imports `@microsoft/applicationinsights-web` only when the connection string is present so the no-op path doesn't pay the JS cost (Research R4 / FR-040).
- [X] T025 [P] Implement W3C Trace Context primitives in `web/lib/http/trace-context.ts`: `newTraceContext`, `parseTraceparent`, `serializeTraceparent` (32-hex traceId, 16-hex spanId, 2-hex flags) per [`contracts/observability-adapter.ts`](./contracts/observability-adapter.ts) (FR-039).
- [X] T026 [US1 / cross-cutting] Implement the typed `fetch` wrapper in `web/lib/http/client.ts` — always attaches `traceparent` (and forwards `tracestate` when present), regardless of adapter selection; emits a `route-change` or `custom` event through the active adapter (FR-039 / SC-014).
- [X] T027 [P] Implement Web Vitals capture in `web/lib/observability/web-vitals.ts` — registers LCP/INP/CLS/TTFB/FCP via the `web-vitals` package and forwards each through the active adapter (FR-037 / SC-016).
- [X] T028 [P] Implement App Router route-change span emitter in `web/lib/observability/route-change.ts` — listens to `usePathname`/`useSearchParams` transitions and emits `route-change` events with `fromRoute`, `toRoute`, `durationMs` (FR-038).
- [X] T029 [P] Implement the top-level error boundary in `web/lib/observability/error-boundary.tsx` — captures unhandled rendering errors with the React component stack, forwards through the adapter, and renders an on-brand accessible error surface (FR-036 / SC-015).

### Root layout, providers, and brand placeholder wiring

- [X] T030 Implement `web/app/providers.tsx` (Client Component) composing `ThemeProvider` (next-themes, `class` strategy, `bt:theme` storage key per [`contracts/theme-provider.ts`](./contracts/theme-provider.ts)), the observability adapter init, the Web Vitals beacon, and the route-change listener. (FR-006 / FR-036–FR-038 / FR-040.)
- [X] T031 Implement `web/app/layout.tsx` — sets `<html lang>` and `dir` via `directionForLocale`, injects the next-themes anti-FOUC inline script in `<head>`, mounts `<Providers>` + `<ErrorBoundary>`, configures viewport / metadata, and wires global `<link rel="icon">` references to the brand placeholder (FR-006 / SC-004 / FR-002).
- [X] T032 [P] Author the brand **placeholder** wordmark SVGs in `web/brand/wordmark/wordmark-dark.svg` + `wordmark-light.svg` (plain hand-authored SVG, no AI tools, no embedded rasters), add empty `web/brand/wordmark/placeholder.flag`, and write `web/brand/wordmark/README.md` describing it as a placeholder per [Research R3](./research.md#r3-ai-assisted-brand-asset-production-and-required-human-review-fr-002a--fr-002b).
- [X] T033 [P] Author the brand **placeholder** glyph SVGs in `web/brand/glyph/glyph-dark.svg` + `glyph-light.svg`, add `web/brand/glyph/placeholder.flag`, write `web/brand/glyph/README.md`. (Mirror of T032 for the glyph mark.)
- [X] T034 [P] Export placeholder favicon set into `web/public/favicons/` (sizes 16/32/48/64/128/180/192/256/512) and wire `web/app/icon.tsx` + `web/app/apple-icon.tsx` to consume them. _Implementation note: placeholder phase uses Next.js dynamic `ImageResponse` generators in `app/icon.tsx` + `app/apple-icon.tsx`. Static PNG export to `web/public/favicons/` lands with the final-asset commit in T142._
- [X] T035 [P] Produce a placeholder social preview at `web/public/og/og-image.png` (1200×630) and wire `web/app/opengraph-image.tsx`. _Implementation note: placeholder uses Next.js `ImageResponse` in `app/opengraph-image.tsx`. Static PNG export lands in T143._

### Audit scripts & bundle-analyzer wiring

- [X] T036 [P] Implement `web/scripts/audit-tokens.mjs` — scans `web/components/**/*` and `web/lib/**/*` for hardcoded color/spacing/radius/elevation/motion literals; fails the build when found (SC-003).
- [X] T037 [P] Implement `web/scripts/audit-strings.mjs` — scans primitive and composite source for raw user-facing strings; fails the build when any are not sourced through `t(key)` (SC-012).
- [X] T038 [P] Implement `web/scripts/audit-directions.mjs` — scans for physical-direction Tailwind utilities (`ml-*`, `mr-*`, `pl-*`, `pr-*`, `left-*`, `right-*`, `text-left`, `text-right`) outside `web/styles/tokens.css`; fails when found (SC-012).
- [X] T039 [P] Implement `web/scripts/audit-svg-hygiene.mjs` — scans `web/brand/**/*.svg` and `web/public/brand/**/*.svg` for `<image>` tags and base64 `data:` URIs; fails on any hit (FR-002a / SC-017).
- [X] T040 [P] Implement `web/scripts/audit-review-records.mjs` and `web/scripts/bundle-diff.mjs`: the review-records audit enforces the [`contracts/brand-asset-review.md`](./contracts/brand-asset-review.md) gate behavior (presence, all checks ticked, decision = approved, signature line populated); the bundle-diff script compares the latest analyzer JSON against `web/docs/performance-budget.json` and posts a PR comment with red/green status on the 180 KB soft target and 200 KB hard alert (FR-002b / FR-035e / SC-020).

### Storybook initialization

- [X] T041 Initialize Storybook in `web/.storybook/main.ts` with the `@storybook/nextjs` framework and the addons pinned in T003. Configure typed framework options for App Router.
- [X] T042 Author `web/.storybook/preview.tsx` with global decorators: `<ThemeProvider>` mirroring the app, `addon-themes` toggle for light/dark, **inline viewport declarations** under the `parameters.viewport.viewports` key (the standalone `@storybook/addon-viewport` package was folded into Storybook core in 9+ — see [Research R1](./research.md#r1-component-documentation-system--storybook-vs-equivalent)) configured with the 13" laptop reference plus standard breakpoints (mobile 390×844, tablet 768×1024, laptop 1366×768, desktop 1920×1080, 4K 3840×2160 — the 4K entry supports the SC-010 / T145 wide-viewport check), and an RTL direction toggle that sets `dir` on the story root (Research R1 / SC-010 / SC-011).
- [X] T043 [P] Author `web/.storybook/theme.ts` — brand-aligned Storybook UI theming consuming the design tokens.

### Accessibility utilities (FR-034)

- [X] T153 [P] Implement shared accessibility utilities in `web/lib/utils/a11y.ts` — `useFocusTrap(ref)` (Tab/Shift+Tab cycling within a region), `useRestoreFocus(triggerRef)` (return focus to the opener when an overlay closes), `usePressEscape(handler)` (closes the active overlay layer), and `getAccessibleName(element)` helper. These utilities are consumed by Dialog (T052), Sheet (T053), Command palette (T056), Drawer composition patterns, and the destructive confirmation composite (T078) — fulfills the FR-034 mandate that "any required accessibility utilities MUST be published as shared foundation utilities."

**Checkpoint**: Foundation ready — token system, theme provider, observability adapter, W3C Trace Context propagation, audit scripts, brand placeholder, Storybook, and accessibility utilities are all in place. Phases 3–7 may now proceed in parallel.

---

## Phase 3: User Story 1 — Feature Developers Build From a Ready Foundation (Priority: P1) 🎯 MVP

**Goal**: Every primitive (FR-013), the application shell (FR-010 – FR-012), the TanStack Table foundation (FR-015 – FR-016), the React Hook Form + Zod form foundation (FR-017 – FR-018), the feedback primitives (FR-019 – FR-020), navigation primitives, and the Recharts chart wrapper layer (FR-029) are published and composed in a representative demo screen — enabling a developer or agent to scaffold an operational page from foundation primitives only with no new colors/spacings/typography introduced (SC-001).

**Independent Test**: A reviewer (or coding agent) scaffolds a representative operational page (sidebar + header + sortable/filterable data table + entity detail drawer + validated form + toast) using only foundation primitives. `pnpm audit:tokens`, `pnpm audit:strings`, `pnpm audit:directions` all report zero violations. The page renders correctly in dark and light themes.

### shadcn/ui primitives (FR-013)

> Each task below: run `pnpm dlx shadcn@latest add <name>`, then in the generated file replace hardcoded literals with token references, replace user-facing strings with `t(key)`, author `<name>.stories.tsx` covering default/all-variants/all-states/both-themes/RTL, and author `<name>.test.tsx` with a `vitest-axe` assertion. (FR-014 / FR-022a / FR-023 – FR-027 / SC-002 / SC-005 / SC-012.)

- [X] T044 [P] [US1] Add and theme the `Button` primitive in `web/components/ui/button.{tsx,stories.tsx,test.tsx}`.
- [X] T045 [P] [US1] Add and theme the `Input` primitive in `web/components/ui/input.{tsx,stories.tsx,test.tsx}`.
- [X] T046 [P] [US1] Add and theme the `Textarea` primitive in `web/components/ui/textarea.{tsx,stories.tsx,test.tsx}`.
- [X] T047 [P] [US1] Add and theme the `Select` primitive in `web/components/ui/select.{tsx,stories.tsx,test.tsx}`.
- [X] T048 [P] [US1] Add and theme the `Checkbox` primitive in `web/components/ui/checkbox.{tsx,stories.tsx,test.tsx}`.
- [X] T049 [P] [US1] Add and theme the `RadioGroup` primitive in `web/components/ui/radio-group.{tsx,stories.tsx,test.tsx}`.
- [X] T050 [P] [US1] Add and theme the `Switch` primitive in `web/components/ui/switch.{tsx,stories.tsx,test.tsx}`.
- [X] T051 [P] [US1] Add and theme the `Label` primitive in `web/components/ui/label.{tsx,stories.tsx,test.tsx}`.
- [X] T052 [P] [US1] Add and theme the `Dialog` primitive in `web/components/ui/dialog.{tsx,stories.tsx,test.tsx}` — focus-trap, Esc to close, focus return contract.
- [X] T053 [P] [US1] Add and theme the `Sheet` primitive (the canonical name for the side-overlay primitive; "Drawer" is reserved for the app-shell composition pattern that uses `Sheet`) in `web/components/ui/sheet.{tsx,stories.tsx,test.tsx}`.
- [X] T054 [P] [US1] Add and theme the `DropdownMenu` primitive in `web/components/ui/dropdown-menu.{tsx,stories.tsx,test.tsx}`.
- [X] T055 [P] [US1] Add and theme the `ContextMenu` primitive in `web/components/ui/context-menu.{tsx,stories.tsx,test.tsx}`.
- [X] T056 [P] [US1] Add and theme the `Command` (palette) primitive in `web/components/ui/command.{tsx,stories.tsx,test.tsx}`.
- [X] T057 [P] [US1] Add and theme the `Tabs` primitive in `web/components/ui/tabs.{tsx,stories.tsx,test.tsx}`.
- [X] T058 [P] [US1] Add and theme the `Card` primitive in `web/components/ui/card.{tsx,stories.tsx,test.tsx}`.
- [X] T059 [P] [US1] Add and theme the `Badge` primitive in `web/components/ui/badge.{tsx,stories.tsx,test.tsx}`.
- [X] T060 [P] [US1] Add and theme the `Alert` primitive in `web/components/ui/alert.{tsx,stories.tsx,test.tsx}` — color + icon + text per FR-026.
- [X] T061 [P] [US1] Add and theme the `Toast` surface (Sonner) in `web/components/ui/toast.{tsx,stories.tsx,test.tsx}` plus the `useToast` hook in `web/hooks/use-toast.ts`.
- [X] T062 [P] [US1] Add and theme the `Tooltip` primitive in `web/components/ui/tooltip.{tsx,stories.tsx,test.tsx}`.
- [X] T063 [P] [US1] Add and theme the `Popover` primitive in `web/components/ui/popover.{tsx,stories.tsx,test.tsx}`.
- [X] T064 [P] [US1] Add and theme the `Separator` primitive in `web/components/ui/separator.{tsx,stories.tsx,test.tsx}`.
- [X] T065 [P] [US1] Add and theme the `Skeleton` primitive in `web/components/ui/skeleton.{tsx,stories.tsx,test.tsx}` — preserves layout stability (FR-019 / SC-019 CLS).
- [X] T066 [P] [US1] Add and theme the basic `Table` primitive (markup-only) in `web/components/ui/table.{tsx,stories.tsx,test.tsx}` — consumed by the DataTable foundation (T071 below).
- [X] T067 [P] [US1] Add and theme the `Breadcrumb` primitive in `web/components/ui/breadcrumb.{tsx,stories.tsx,test.tsx}`.
- [X] T068 [P] [US1] Add and theme the `ScrollArea` primitive in `web/components/ui/scroll-area.{tsx,stories.tsx,test.tsx}`.
- [X] T069 [P] [US1] Add and theme the `Resizable` panels primitive in `web/components/ui/resizable.{tsx,stories.tsx,test.tsx}` — Esc cancels drag, sizes persisted via the table-foundation persistence key prefix.
- [X] T070 [P] [US1] Add and theme the shadcn `Form` wrapper in `web/components/ui/form.{tsx,stories.tsx,test.tsx}` — the low-level RHF + Zod glue consumed by the higher-level form composites in T076 – T079.

### Data table foundation (FR-015 / FR-016)

- [X] T071 [US1] Implement `DataTable<TData, TValue>` in `web/components/data-table/data-table.tsx` per [`contracts/table-foundation.ts`](./contracts/table-foundation.ts) — type-safe columns, sorting, filtering, column visibility, sticky header, keyboard navigation, row selection (multi-select gated by `enableMultiSelect`), pagination OR virtualization based on `paginationMode`, empty/loading/error states, responsive overflow.
- [X] T072 [P] [US1] Implement the table toolbar in `web/components/data-table/toolbar.tsx` — search input, column visibility menu, bulk-action menu, custom toolbar slot.
- [X] T073 [P] [US1] Implement table pagination in `web/components/data-table/pagination.tsx`.
- [X] T074 [P] [US1] Implement the table empty/error/loading states in `web/components/data-table/empty-state.tsx`, `error-state.tsx`, `loading-state.tsx` (skeleton-based — FR-019).
- [X] T075 [P] [US1] Author DataTable stories in `web/components/data-table/data-table.stories.tsx` and a `vitest-axe` test in `web/components/data-table/data-table.test.tsx` covering keyboard navigation (`ArrowUp`/`ArrowDown` row navigation, `Space` toggles selection), all states, both themes, RTL.

### Form foundation (FR-017 / FR-018)

- [X] T076 [US1] Implement the high-level `<Form>` composite in `web/components/forms/form.tsx` per [`contracts/form-foundation.ts`](./contracts/form-foundation.ts) — Zod schema-driven, `preventDoubleSubmit` default true, accessible name sourced from i18n.
- [X] T077 [P] [US1] Implement `<Field>` composite in `web/components/forms/field.tsx` — wires label, description, error-message, required-indication, `aria-describedby` linkage.
- [X] T078 [P] [US1] Implement `useDestructiveConfirm` in `web/components/forms/destructive-confirm.tsx` — opens the Dialog primitive with destructive variant + required confirmation copy from i18n.
- [X] T079 [P] [US1] Implement `useLongRunningSubmit` in `web/components/forms/long-running-submit.ts` — wraps `SubmitHandler` with progress toast surface integration (FR-018).
- [X] T080 [P] [US1] Implement reusable Zod helpers in `web/lib/validation/zod-helpers.ts` (required-string, optional-bounded-string, enum-from-tuple, etc.).
- [X] T081 [P] [US1] Author Form composite stories in `web/components/forms/form.stories.tsx` and the corresponding `vitest-axe` test — covers required indication, inline error display, submit-pending state, destructive confirmation, both themes, RTL.

### Feedback primitives (FR-019 / FR-020)

- [X] T082 [P] [US1] Implement `<EmptyState>` in `web/components/feedback/empty-state.tsx` — icon + title + description + optional action; copy sourced from i18n.
- [X] T083 [P] [US1] Implement `<ErrorState>` and `<RetryAffordance>` in `web/components/feedback/error-state.tsx` and `web/components/feedback/retry-affordance.tsx`.
- [X] T084 [P] [US1] Implement `<InlineValidation>` in `web/components/feedback/inline-validation.tsx` for non-form contexts.

### Navigation primitives

- [X] T085 [P] [US1] Implement the global Command Palette in `web/components/navigation/command-palette.tsx` — composes the `Command` primitive, keyboard shortcut `Cmd/Ctrl+K`, registers a default action set.
- [X] T086 [P] [US1] Implement the BusTerminal breadcrumb composite in `web/components/navigation/breadcrumb.tsx` — wraps the Breadcrumb primitive with route-aware items.
- [X] T087 [P] [US1] Implement pagination composite in `web/components/navigation/pagination.tsx`.

### Application shell & layout regions (FR-010 / FR-011 / FR-012)

- [X] T088 [US1] Implement `<AppShell>` in `web/components/app-shell/app-shell.tsx` with `sidebar`, `topBar`, `main`, `footer` slots per [data-model.md → Layout Region](./data-model.md#5-layout-region).
- [X] T089 [P] [US1] Implement `<Sidebar>` in `web/components/app-shell/sidebar.tsx` — collapsed/expanded states, `Cmd/Ctrl+B` toggle, focus preserved across collapse; collapsed = 56px, expanded = 280px at `≥lg`.
- [X] T090 [P] [US1] Implement `<TopBar>` in `web/components/app-shell/top-bar.tsx` — global search trigger, theme toggle, user menu placeholder.
- [X] T091 [P] [US1] Implement `<PageContainer>` and `<SectionContainer>` in `web/components/app-shell/page-container.tsx` and `section-container.tsx`.
- [X] T092 [P] [US1] Implement `<PageHeader>` in `web/components/app-shell/page-header.tsx` — title, breadcrumb slot, actions slot.
- [X] T093 [P] [US1] Implement `<Footer>` in `web/components/app-shell/footer.tsx`.
- [X] T094 [P] [US1] Implement `<SplitPanel>` and `<ResizablePanel>` in `web/components/app-shell/split-panel.tsx` and `resizable-panel.tsx` wrapping the `Resizable` primitive (T069).
- [X] T095 [US1] Compose `web/app/(app)/layout.tsx` from AppShell + Sidebar + TopBar + PageContainer + Footer; add `web/app/(app)/error.tsx` (consumes the top-level error boundary, on-brand surface — SC-015) and `web/app/(app)/loading.tsx` (skeleton-based — FR-019).

### Chart wrapper layer (FR-029)

- [X] T096 [P] [US1] Implement `<ChartContainer>` in `web/components/charts/chart-container.tsx` — injects token-derived chart colors from `CHART_DATA_TOKENS`, disables enter/update animations when `prefers-reduced-motion` is set (FR-025).
- [X] T097 [P] [US1] Implement `<ChartLine>`, `<ChartBar>`, `<ChartArea>` wrappers in `web/components/charts/chart-line.tsx`, `chart-bar.tsx`, `chart-area.tsx` — each accepts strongly-typed data and exposes accessible labels per FR-026.
- [X] T098 [P] [US1] Author chart wrapper stories in `web/components/charts/charts.stories.tsx` plus a `vitest-axe` smoke test.

### Representative composed demo screen (SC-001)

- [X] T099 [US1] Implement the representative composed demo screen at `web/app/(app)/page.tsx` exercising sidebar + top bar + page header + sortable/filterable DataTable + entity detail Drawer + Form (RHF + Zod) + Toast on save — using foundation primitives ONLY. Reset audit scripts (`audit:tokens`, `audit:strings`, `audit:directions`) MUST report zero violations.
- [X] T100 [P] [US1] Provide sample data + Zod schemas + column definitions in `web/app/(app)/_showcase/showcase-data.ts` and `web/app/(app)/_showcase/showcase-schemas.ts` to drive the demo.

**Checkpoint**: At this point, User Story 1 should be fully functional and testable independently. A new operational screen can be assembled from the foundation using only its primitives and design tokens — `pnpm audit:tokens`, `pnpm audit:strings`, and `pnpm audit:directions` all report zero violations (SC-001).

---

## Phase 4: User Story 2 — Operators Experience a Polished, Operationally Trustworthy UI (Priority: P1, co-equal)

**Goal**: Dark and light themes are complete first-class peers (FR-005), the typography scale + monospace family is applied to technical identifiers everywhere (FR-008 / FR-009), every primitive is polished and on-brand in both themes (SC-005), semantic state colors carry icon + text affordances (FR-026), and theme switching / first-load resolution is flash-free (FR-006 / SC-004).

**Independent Test**: A reviewer opens Storybook and confirms the foundation reads as infrastructure tooling (not a generic admin template) — both themes are polished and complete, monospace renders technical identifiers, semantic states (success/warning/error/info) are immediately recognizable via color + icon + text, and no placeholder/playful elements remain. A Playwright spec verifies no flash of incorrect theme on first paint.

- [X] T101 [US2] Implement the typography scale CSS in `web/styles/typography.css` against the actual sans + monospace families chosen during foundation work (Inter / Geist / IBM Plex Sans for body, JetBrains Mono / Geist Mono / IBM Plex Mono for code); load via `next/font` in `web/app/layout.tsx` with `display: 'swap'` and preconnect hints (FR-008 / FR-009 / FR-035f). _Geist Sans + Geist Mono wired via `next/font/google` with `display: 'swap'`; preconnect/preload is handled automatically by `next/font` self-hosting._
- [X] T102 [P] [US2] Apply monospace typography across all foundation primitives that render technical identifiers (queue/topic/subscription/namespace names, correlation IDs, connection strings, JSON payloads, message metadata) — update story states to demonstrate (FR-009). _Added `mono` boolean prop to Input, Textarea, and TableCell; updated Input/Textarea/Table stories with monospace demo states._
- [X] T103 [P] [US2] Polish semantic state visuals across every primitive that emits success/warning/error/info — each renders icon + text + color (FR-026); update the affected primitive stories. _Badge now auto-renders the canonical semantic icon for success/warning/error/info (with `icon` override for custom or `false` to suppress); aligns with Alert / InlineValidation / Toast (Sonner default icons). New badge stories cover icon overrides and semantic parity._
- [X] T104 [P] [US2] Author the brand showcase MDX at `web/stories/01-brand.mdx` — voice, traits, anti-patterns, dark/light parity side-by-side, placeholder mark hot-swap note.
- [X] T105 [P] [US2] Implement Playwright spec at `web/tests/e2e/theme-flash.spec.ts` asserting that, with the system color scheme set to dark, the first paint shows the dark theme (no white flash) and with persistence cleared the resolver honors `prefers-color-scheme` (FR-006 / SC-004).
- [X] T106 [P] [US2] Implement Playwright spec at `web/tests/e2e/theme-switch.spec.ts` exercising the edge case "dialog + drawer + toast + chart open simultaneously, toggle theme" — assert no leaked dark/light values, no broken focus rings, no stale chart colors (Spec Edge Cases / FR-005).

**Checkpoint**: At this point, User Story 1 AND User Story 2 are both fully functional. The foundation is composable AND visually polished.

---

## Phase 5: User Story 3 — Accessibility Is Enforced From the Foundation (Priority: P2)

**Goal**: Every primitive is automated-axe-clean (FR-027 / SC-002), keyboard-only walkthrough of the composed screen completes every interaction without a pointing device (SC-007), reduced-motion preferences eliminate non-essential animation (SC-008), `dir="rtl"` renders every primitive and composite without breakage (SC-011), and the i18n string surface + logical-properties enforcement leave zero hardcoded strings or physical-direction utilities (SC-012).

**Independent Test**: `pnpm test:storybook` (Storybook test-runner + axe-playwright) reports zero WCAG 2.2 AA violations across every published story. The keyboard-only Playwright walkthrough passes. The reduced-motion + RTL Playwright specs pass. `pnpm audit:strings` and `pnpm audit:directions` report zero violations.

- [X] T107 [US3] Wire `@storybook/test-runner` + `axe-playwright` so every published story is scanned for WCAG 2.2 AA violations in CI; configure failure thresholds at zero violations and ensure the runner can be invoked via `pnpm test:storybook` (FR-027 / SC-005).
- [X] T108 [P] [US3] Implement `useReducedMotion` hook in `web/hooks/use-reduced-motion.ts` and adopt it across every primitive that emits motion (Toast slide-in, Dialog open/close, Drawer open/close, Tabs underline, Chart enter/update); update story state to demonstrate reduced-motion paths (FR-025 / SC-008). _Hook landed in `hooks/use-reduced-motion.ts` with a unit test. Adopted in `<ChartLine>`, `<ChartBar>`, `<ChartArea>` for JS-scheduled enter/update tweens (Recharts `isAnimationActive`). CSS-driven motion in Toast/Dialog/Sheet/Tabs continues to be governed by the global `@media (prefers-reduced-motion: reduce)` block in `app/globals.css` — the hook exists for cases CSS can't cover._
- [X] T109 [P] [US3] Implement Playwright keyboard-only walkthrough at `web/tests/e2e/keyboard-only.spec.ts` against the demo screen at `/` — asserts every interaction (navigate, sort, filter, open detail drawer, fill form, save, dismiss toast) completes without a pointing device, focus is always visible, no traps (SC-007).
- [X] T110 [P] [US3] Implement Playwright reduced-motion spec at `web/tests/e2e/reduced-motion.spec.ts` — emulates `prefers-reduced-motion: reduce` and asserts non-essential motion is removed (SC-008).
- [X] T111 [P] [US3] Implement Playwright RTL spec at `web/tests/e2e/rtl-smoke.spec.ts` — sets `dir="rtl"` on the demo screen and walks every primitive in both themes asserting no clipping/overlap/mis-anchoring of menus/popovers (SC-011 / FR-022d).
- [X] T112 [P] [US3] Author the accessibility documentation MDX at `web/stories/05-accessibility.mdx` and the prose doc at `web/docs/accessibility.md` (keyboard contracts, ARIA approach, color-vs-icon affordances, reduced-motion strategy, RTL strategy).
- [X] T113 [US3] Run all three string/direction/SVG audits on a clean checkout: `pnpm audit:strings`, `pnpm audit:directions`, `pnpm audit:tokens`, `pnpm audit:svg-hygiene` — all must report zero violations (SC-003 / SC-012).
- [X] T154 [P] [US3] Implement Vitest unit tests for the locale-aware formatters in `web/tests/unit/format.test.ts` — exercise `formatDate`, `formatTime`, `formatRelativeTime`, `formatDuration`, `formatNumber`, `formatBytes` against the locale overrides `de-DE`, `ja-JP`, and `ar-EG` (RTL); assert each returns locale-correct output (e.g., `de-DE` → `"14.05.2026"`, `ja-JP` → `"2026/5/14"`, `ar-EG` → Arabic-Indic digits). Covers the spec's "Non-English locale formatting on first load" edge case and FR-022c. _Implementation note: Arabic-Indic digit assertion uses `formatRelativeTime(5, 'day', ...)` because Arabic grammar uses dual forms (`يومين`) for ±2 with no digit substitution — 5 forces the plural numeric form `٥`._

**Checkpoint**: At this point, US1 + US2 + US3 are all independently functional. The foundation is composable, polished, AND accessibility-enforced.

---

## Phase 6: User Story 4 — Brand, Tokens, and Components Are Discoverable and Documented (Priority: P2)

**Goal**: Every primitive and composite is discoverable in Storybook with anatomy/props/usage rules/accessibility notes/live examples in both themes (FR-030 / SC-005 / SC-006); every token has a documentation entry; brand voice, typography, iconography, theming, contribution rules, agentic-coding guidance, and browser support are documented (FR-031); MCP usage is framed as development-time only (FR-032).

**Independent Test**: A contributor unfamiliar with the project opens Storybook + `web/docs/` and can answer the SC-006 ten-task identification challenge in under fifteen minutes by consulting only the published documentation.

- [X] T114 [P] [US4] Author `web/stories/00-introduction.mdx` — landing page describing the foundation, links to all other pages.
- [X] T115 [P] [US4] Author `web/stories/02-design-tokens.mdx` — every token listed with name, value, theme variants, intended usage, "do this / don't do this" notes (FR-031 / SC-005).
- [X] T116 [P] [US4] Author `web/stories/03-typography.mdx` — full scale + monospace usage rules (FR-008 / FR-009).
- [X] T117 [P] [US4] Author `web/stories/04-iconography.mdx` — single family rationale, domain-icon mapping table.
- [X] T118 [P] [US4] Author `web/stories/06-theming.mdx` — dark/light parity rules, flash-free first paint, theme provider contract.
- [X] T119 [P] [US4] Author `web/stories/07-contribution.mdx` — frontend contribution rules; how to add a primitive, how to add a token, how to add a string key.
- [X] T120 [P] [US4] Author `web/stories/08-agentic-coding.mdx` — MCP servers as development-time tooling only, "coding agents must consult..." phrasing, anti-patterns to avoid (FR-032).
- [X] T121 [P] [US4] Author `web/stories/09-browser-support.mdx` and `web/docs/browser-support.md` — last two majors of evergreen browsers + iPadOS Safari + Android Chrome; modern CSS features permitted (FR-035a / FR-035b / FR-035c).
- [X] T122 [P] [US4] Author `web/docs/theming.md` — narrative theming doc paired with the MDX page.
- [X] T123 [P] [US4] Author `web/docs/agentic-coding.md` — narrative agentic-coding guidance paired with the MDX page.
- [X] T124 [P] [US4] Author `web/docs/contributing-frontend.md` — narrative contribution doc paired with the MDX page.
- [X] T125 [US4] Validate SC-006: have a fresh contributor or coding agent identify the correct primitive for the ten representative tasks listed in SC-006 by consulting only the docs; record the results inline in `web/docs/contributing-frontend.md` under a "Documentation Validation" section. _Recorded in `web/docs/contributing-frontend.md` → "Documentation validation (SC-006)" — all ten tasks identified in ~13 minutes (PASS, target ≤ 15 min); no doc gaps surfaced._
- [X] T126 [P] [US4] Author the root `README.md` (constitution sync-impact-report TODO closure) — links to the constitution, tech-stack reference, and the current spec; brief project pitch; quickstart pointer. _Root `README.md` authored; constitution Sync Impact Report TODO(README) closed in `.specify/memory/constitution.md`._

**Checkpoint**: At this point, US1 + US2 + US3 + US4 are all independently functional. The foundation is composable, polished, accessibility-enforced, AND fully documented.

---

## Phase 7: User Story 5 — Domain-Aware Components Exist for Service Bus Concepts (Priority: P3)

**Goal**: Every BusTerminal-specific composite from [FR-028](./spec.md) is published with documented props, all required states, accessibility behavior, and Storybook coverage — so the first feature spec can render a queue, namespace, dead-letter indicator, etc. without re-inventing presentation.

**Independent Test**: Every domain composite renders in Storybook with representative data covering all states; the iconography is consistent; environment context propagates via `<EnvironmentBadge>` on any screen showing entities across environments.

> Each composite below: implement the component in `web/components/domain/<name>.tsx`, author `<name>.stories.tsx` with stories for every state in both themes + RTL, author `<name>.test.tsx` with a `vitest-axe` assertion, consume i18n strings only, consume design tokens only, use the iconography mapping module from T021.

- [X] T127 [P] [US5] Implement `<NamespaceCard>` in `web/components/domain/namespace-card.{tsx,stories.tsx,test.tsx}`. Include a story state with an oversized namespace name (e.g., 80+ chars) and assert in the test file that the rendered name truncates predictably (CSS-only, `text-overflow: ellipsis` via logical properties) and surfaces the full value through a tooltip/popover on hover and on keyboard focus — covers the spec's "Long entity names / wide content" edge case.
- [X] T128 [P] [US5] Implement `<QueueRow>` and `<QueueCard>` in `web/components/domain/queue-row.{tsx,stories.tsx,test.tsx}` and `web/components/domain/queue-card.{tsx,stories.tsx,test.tsx}` (active/idle/error/dead-lettered states). Include a story state and test for an oversized queue name (80+ chars) asserting predictable truncation plus tooltip-disclosure on hover and keyboard focus — covers the spec's "Long entity names / wide content" edge case.
- [X] T129 [P] [US5] Implement `<TopicRow>` and `<TopicCard>` in `web/components/domain/topic-row.{tsx,stories.tsx,test.tsx}` and `web/components/domain/topic-card.{tsx,stories.tsx,test.tsx}`. Include a story state and test for an oversized topic name (80+ chars) asserting predictable truncation plus tooltip-disclosure on hover and keyboard focus — covers the spec's "Long entity names / wide content" edge case.
- [X] T130 [P] [US5] Implement `<SubscriptionRow>` and `<SubscriptionCard>` in `web/components/domain/subscription-row.{tsx,stories.tsx,test.tsx}` and `web/components/domain/subscription-card.{tsx,stories.tsx,test.tsx}`. Include a story state and test for an oversized subscription name (80+ chars) asserting predictable truncation plus tooltip-disclosure on hover and keyboard focus — covers the spec's "Long entity names / wide content" edge case.
- [X] T131 [P] [US5] Implement `<DeadLetterIndicator>` in `web/components/domain/dead-letter-indicator.{tsx,stories.tsx,test.tsx}` — color + icon + numeric badge.
- [X] T132 [P] [US5] Implement `<MessageCountIndicator>` in `web/components/domain/message-count-indicator.{tsx,stories.tsx,test.tsx}` — `formatNumber` for the count, sparkline-ready slot.
- [X] T133 [P] [US5] Implement `<HealthSummaryIndicator>` in `web/components/domain/health-summary-indicator.{tsx,stories.tsx,test.tsx}` — composite of three semantic state pills with a roll-up status.
- [X] T134 [P] [US5] Implement `<DiscoveryJobStatus>` in `web/components/domain/discovery-job-status.{tsx,stories.tsx,test.tsx}` — running/succeeded/failed/queued states with `formatRelativeTime` for "started X ago".
- [X] T135 [P] [US5] Implement `<EntityRelationshipBadge>` in `web/components/domain/entity-relationship-badge.{tsx,stories.tsx,test.tsx}` — used by topology-aware screens.
- [X] T136 [P] [US5] Implement `<EnvironmentBadge>` in `web/components/domain/environment-badge.{tsx,stories.tsx,test.tsx}` — color + label for `dev/test/staging/prod`; accessible name announces environment explicitly (FR-026).
- [X] T137 [P] [US5] Implement `<AzureResourceLink>` in `web/components/domain/azure-resource-link.{tsx,stories.tsx,test.tsx}` — copy-able + external-link affordance to the Azure portal route.
- [X] T138 [P] [US5] Implement `<MetadataKeyValuePanel>` in `web/components/domain/metadata-key-value-panel.{tsx,stories.tsx,test.tsx}` — accessible definition-list semantics; monospace values for technical IDs (FR-009).
- [X] T139 [P] [US5] Implement `<TopologyMiniMapPlaceholder>` in `web/components/domain/topology-mini-map-placeholder.{tsx,stories.tsx,test.tsx}` — inert placeholder with documented "future spec will replace" note (FR-028).
- [X] T140 [US5] Extend the representative demo screen at `web/app/(app)/page.tsx` (or a sibling showcase route under `web/app/(app)/_showcase/`) so every domain composite renders with realistic sample data — proves SC-001 still holds with domain composites included and lets reviewers verify visual consistency.

**Checkpoint**: All user stories now independently functional. The foundation is composable, polished, accessibility-enforced, documented, AND domain-aware.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Final brand asset commit, performance baselines, cross-browser smoke, regression alert wiring, and the foundation handoff.

### Brand asset finalization (Stages 4–5; SC-017)

> Per [Research R3](./research.md#r3-ai-assisted-brand-asset-production-and-required-human-review-fr-002a--fr-002b), Stages 1–3 (concept → vector → variant export) run as a **parallel track** that may start as early as Phase 2 and finishes any time before T141. Stages 4–5 are mandatory blockers on the handoff.

- [ ] T141 Author the signed `web/brand/wordmark/REVIEW.md` per the [`contracts/brand-asset-review.md`](./contracts/brand-asset-review.md) template — all "Checks performed" and "Render verification" boxes ticked, "Approved for commit" selected, signature line populated. Then commit the final wordmark SVG sources (replacing placeholder), update `web/public/brand/` exports, delete `web/brand/wordmark/placeholder.flag` (FR-002a / FR-002b / SC-017).
- [ ] T142 Author the signed `web/brand/glyph/REVIEW.md`, commit the final glyph SVG sources (replacing placeholder), update `web/public/favicons/` exports + the OG image at `web/public/og/og-image.png`, delete `web/brand/glyph/placeholder.flag`. Re-run `pnpm audit:review-records` and `pnpm audit:svg-hygiene` — both must pass.
- [ ] T143 Update `web/app/icon.tsx`, `web/app/apple-icon.tsx`, `web/app/opengraph-image.tsx`, and `web/stories/01-brand.mdx` to reference the final assets; verify favicons render at 16/32/48/64/128/180/192/256/512 px.

### Performance baselines & cross-browser smoke

- [ ] T144 [P] Capture the bundle baseline: run `pnpm -C web analyze`, record the gzipped First Load JS for the root authenticated route in `web/docs/performance-budget.md` and `web/docs/performance-budget.json`. Confirm the baseline ≤ **180 KB** soft target; if > 180 KB but < 200 KB, document the deferral plan in `performance-budget.md` per [Research R2](./research.md#r2-soft-initial-js-bundle-target-for-the-application-shell-fr-035e--sc-020). If ≥ 200 KB, treat as a release blocker (FR-035e / SC-020).
- [ ] T145 [P] Implement Playwright cross-browser + cross-viewport smoke at `web/tests/e2e/cross-browser-smoke.spec.ts` — exercises the representative composed screen across **three browser projects** (Chromium, Firefox, WebKit) **and four viewport projects** per browser: mobile (390×844 — iPhone 13), 13" laptop (1366×768), desktop default (1920×1080), and 4K workstation (3840×2160). Asserts: (a) visual structure + no console errors + same key interactions succeed in all three browsers (FR-035a / SC-018); (b) **no horizontal scroll on the body or any region at the 1366×768 viewport** (SC-010); (c) **at the mobile viewport, all primary read paths — entity list, entity detail drawer open, page header — render without information loss and primary navigation remains reachable** (FR-012, "Mobile/tablet read access" edge case); (d) **at the 4K viewport, the composed screen does not collapse into mostly-whitespace columns and information density adapts** ("Very wide and very narrow viewports" edge case).
- [ ] T146 [P] Implement Playwright Web Vitals probe at `web/tests/e2e/web-vitals.spec.ts` — measures LCP, INP, CLS on the demo screen against the FR-035d thresholds (LCP ≤ 2.5 s, INP ≤ 200 ms, CLS ≤ 0.1); fails the build on regression (FR-035d / FR-035f / SC-019).
- [ ] T147 [P] Implement Playwright observability E2E at `web/tests/e2e/observability.spec.ts` — with the AI connection string set, asserts a UI-originated fetch carries a `traceparent` header whose trace ID also appears in the AI debug pipeline (SC-014); with no connection string, asserts the no-op adapter is active and `traceparent` is still on the wire (SC-013).
- [ ] T148 [P] Implement Playwright error-boundary E2E at `web/tests/e2e/error-boundary.spec.ts` — triggers a render error, asserts the error surface is on-brand and accessible AND the no-op debug pipeline has captured the error event with the component stack (SC-015).

### CI regression alert wiring

- [ ] T149 Wire the bundle-size diff into CI in `.github/workflows/ci.yml` — `scripts/bundle-diff.mjs` posts a PR comment with red/green status against the 180 KB soft target / 200 KB hard alert; PR check fails when the size grows > +10% or crosses 200 KB.

### Constitution & repo-level closure

- [ ] T150 [P] Create the ADR placeholder at `docs/adr/README.md` describing the ADR location, format, and the deferred TODOs noted in the constitution Sync Impact Report.

### PII-hygiene verification

- [ ] T155 [P] Implement a TypeScript type-level test in `web/tests/unit/observability-pii.test.ts` that imports `ObservabilityEvent` from `web/lib/observability/adapter.ts` and uses `@ts-expect-error` assertions to prove the compiler rejects PII-shaped fields (e.g., `userEmail: string`, `userName: string`, `requestBody: string`) on any sanctioned event attribute shape. Also include a runtime sanity test that confirms the no-op adapter's debug pipeline NEVER stores fields outside the sanctioned attribute names. Verifies FR-041 structurally and at runtime.

### Final audit & handoff

- [ ] T151 Run the full handoff gate on a clean checkout: `pnpm install --frozen-lockfile && pnpm -C web lint && pnpm -C web typecheck && pnpm -C web audit:tokens && pnpm -C web audit:strings && pnpm -C web audit:directions && pnpm -C web audit:svg-hygiene && pnpm -C web audit:review-records && pnpm -C web test && pnpm -C web build-storybook && pnpm -C web test:storybook && pnpm -C web exec playwright install && pnpm -C web test:e2e && pnpm -C web analyze` — every command MUST succeed (SC-009).
- [ ] T152 Validate the [`quickstart.md`](./quickstart.md) end-to-end by following it on a fresh clone: confirm scaffolding, token wiring, theme provider, Storybook, first primitive walkthrough, observability + trace-context propagation, and brand-asset workflow all work as written. File issues against any drift; update `quickstart.md` accordingly.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — starts immediately.
- **Foundational (Phase 2)**: Depends on Setup completion — BLOCKS all user stories.
- **User Stories (Phases 3–7)**: All depend on Foundational completion.
  - US1 (P1) MVP-eligible. Begin immediately after Foundational.
  - US2 (P1, co-equal) depends on US1's primitive set existing (theme parity is a polish pass over the primitives). Start US2 once US1 primitives T044–T070 are landed; T101–T106 can finish slightly after US1.
  - US3 (P2) can begin in parallel with US1 once the foundational test scaffolding (T007/T008/T009/T041/T042) is in place — keyboard/RTL/reduced-motion E2E specs target the demo screen, which is T099.
  - US4 (P2) can begin in parallel with US1 — MDX docs for tokens/typography/iconography don't depend on every primitive existing.
  - US5 (P3) depends on the primitives + chart wrappers + iconography mapping. Start US5 after T044 – T097.
- **Polish (Phase 8)**: Brand asset Stages 1–3 may run in parallel with Phases 2–7 (the placeholder is in place from T032/T033). Stages 4–5 (T141–T143) MUST complete before handoff. Performance baselines (T144–T148) require US1 + US5 to be complete. The final handoff gate (T151) requires every prior task complete.

### User Story Dependencies

- **US1 (P1)**: Foundational only. No dependency on other stories.
- **US2 (P1)**: Depends on US1 primitives existing for the polish pass. Can start mid-US1.
- **US3 (P2)**: Foundational only. The E2E walkthroughs target the demo screen from US1, so the **specs themselves** can be authored in parallel with US1 — they begin failing meaningfully when T099 lands.
- **US4 (P2)**: Foundational only. Documentation MDX can be authored in parallel; SC-006 validation (T125) depends on the docs being complete.
- **US5 (P3)**: Depends on the iconography mapping (T021) and the primitives (T044 onwards). Cannot start before primitives exist.

### Within Each User Story

- shadcn primitive tasks within US1 (T044 – T070) are heavily parallelizable — every primitive is a different file.
- Domain composites within US5 (T127 – T139) are heavily parallelizable — every composite is a different file.
- Documentation MDX within US4 (T114 – T126) are heavily parallelizable — different files.

### Parallel Opportunities

- All `[P]` tasks in any phase can run in parallel within their phase.
- Brand-asset Stages 1–3 run as a parallel track from Phase 2 onward without blocking primitive work.
- Once Foundational completes, US1 / US3 / US4 may begin in parallel by separate contributors.

---

## Parallel Example: US1 primitives sweep

```bash
# Once T013 – T043 are complete, launch all shadcn primitive tasks in parallel:
Task T044: "Add and theme the Button primitive in web/components/ui/button.{tsx,stories.tsx,test.tsx}"
Task T045: "Add and theme the Input primitive in web/components/ui/input.{tsx,stories.tsx,test.tsx}"
Task T046: "Add and theme the Textarea primitive in web/components/ui/textarea.{tsx,stories.tsx,test.tsx}"
# ...etc. — every primitive lives in its own file set.
```

## Parallel Example: US5 domain composites

```bash
# Once T021 + primitives + iconography mapping are complete:
Task T127: "Implement <NamespaceCard> in web/components/domain/namespace-card.{tsx,stories.tsx,test.tsx}"
Task T128: "Implement <QueueRow>/<QueueCard> in web/components/domain/queue-{row,card}.{tsx,stories.tsx,test.tsx}"
# ...etc.
```

## Parallel Example: US4 documentation

```bash
# Once Foundational is complete:
Task T114: "Author web/stories/00-introduction.mdx"
Task T115: "Author web/stories/02-design-tokens.mdx"
Task T116: "Author web/stories/03-typography.mdx"
# ...etc.
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete **Phase 1** (Setup) — T001 – T012.
2. Complete **Phase 2** (Foundational) — T013 – T043. This is the critical bottleneck.
3. Complete **Phase 3** (US1) — T044 – T100. Primitive sweep is heavily parallel.
4. **STOP and VALIDATE**: A developer or coding agent can scaffold the representative operational screen from foundation primitives only — `pnpm audit:tokens && pnpm audit:strings && pnpm audit:directions` all clean. Demo / hand off as MVP.

### Incremental Delivery

1. MVP (US1) lands → foundation usable for feature scaffolding.
2. Add US2 (polish) → foundation now visually trustworthy.
3. Add US3 (accessibility verification) → foundation now WCAG 2.2 AA-gated.
4. Add US4 (docs) → foundation discoverable for new contributors and agents.
5. Add US5 (domain composites) → foundation now BusTerminal-aware.
6. Polish phase → brand finalized, performance baselined, cross-browser proven, handoff.

### Parallel Team Strategy

With multiple contributors after Foundational:

- **Contributor A**: US1 primitives sweep (T044 – T070) — heaviest workload, can split among multiple developers.
- **Contributor B**: US3 E2E specs + accessibility docs (T107 – T113) authored against the planned demo screen.
- **Contributor C**: US4 documentation MDX (T114 – T126) authored independently of components.
- **Contributor D (designer)**: Brand asset pipeline Stages 1–3 — concept exploration, vector authoring, variant export.

Once US1 primitives are complete, US2 polish (Contributor A), US5 domain composites (Contributor B/C), and brand asset Stages 4–5 (Contributor D) finish in parallel before the Polish handoff phase.

---

## Notes

- `[P]` tasks operate on different files with no incomplete dependencies.
- Every primitive task includes its story file and its `vitest-axe` test in the same task — these are not separate TDD tasks; they are author-together deliverables.
- `pnpm audit:tokens`, `pnpm audit:strings`, `pnpm audit:directions`, `pnpm audit:svg-hygiene`, `pnpm audit:review-records` must all pass before T151 (the final handoff gate).
- The brand asset placeholder + `placeholder.flag` pattern lets primitive work proceed without churning when the final assets land — the swap is the last set of foundation commits.
- Stop at any phase checkpoint to validate the foundation independently. The Phase 3 checkpoint is the MVP boundary.
- Avoid: cross-story dependencies that break independence; same-file conflicts when running [P] tasks in parallel; bypassing the i18n / token / direction audits.
