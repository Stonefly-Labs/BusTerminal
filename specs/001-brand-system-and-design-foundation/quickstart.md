# Quickstart — Brand System and Design Foundation

**Spec**: [`spec.md`](./spec.md)
**Plan**: [`plan.md`](./plan.md)
**Research**: [`research.md`](./research.md)

This quickstart is the entry point for the **first contributor or coding agent** to land on the foundation. It walks through scaffolding the application, running Storybook and accessibility checks, configuring observability, and producing a brand asset with the required `REVIEW.md`.

It assumes you have:

- Node.js **20.x LTS** installed
- pnpm **9.x** installed (`corepack enable && corepack prepare pnpm@latest --activate`)
- Git
- A modern browser from the [browser support matrix](../../speckit-artifacts/tech-stack.md#2-frontend) — Chrome / Edge / Firefox / Safari, last two majors

---

## 1. Scaffold the foundation

> Run from the repository root (`C:\Projects\BusTerminal` on Windows; `~/projects/BusTerminal` on macOS / Linux).

```powershell
# Create the Next.js 16.x App Router application
pnpm create next-app@latest web --typescript --eslint --tailwind --app --src-dir false --import-alias "@/*"

# Move into the app
Set-Location web

# Pin Next.js 16.x and React strict
pnpm add next@^16 react react-dom

# Tailwind v4
pnpm add -D tailwindcss@^4 @tailwindcss/postcss postcss

# shadcn/ui foundation
pnpm dlx shadcn@latest init

# Theme management
pnpm add next-themes

# Tables, forms, validation, charts, animation, icons
pnpm add @tanstack/react-table react-hook-form zod recharts framer-motion lucide-react

# Class utilities
pnpm add clsx tailwind-merge class-variance-authority

# Storybook 8.x + required addons
pnpm dlx storybook@latest init --type=nextjs
pnpm add -D @storybook/addon-a11y @storybook/addon-themes @storybook/addon-interactions @storybook/addon-viewport @storybook/test @storybook/test-runner axe-playwright

# Testing
pnpm add -D vitest @vitest/ui @testing-library/react @testing-library/dom @testing-library/user-event vitest-axe
pnpm add -D playwright @playwright/test

# Bundle analysis
pnpm add -D @next/bundle-analyzer

# Web Vitals
pnpm add web-vitals
```

> Bash users: the same commands work; replace `Set-Location` with `cd`.

Once installed, lay down the directory skeleton documented in [`plan.md` → Project Structure](./plan.md#project-structure). The directories `web/components/{ui,app-shell,data-display,data-table,forms,feedback,navigation,charts,domain}`, `web/lib/{design-system,i18n,observability,http,iconography,utils,validation}`, `web/hooks`, `web/styles`, `web/brand`, `web/stories`, `web/tests`, `web/docs`, and `web/scripts` are required.

---

## 2. Wire the design tokens

1. Open `web/styles/tokens.css` and define the CSS variables for every token name listed in [`contracts/design-tokens.ts`](./contracts/design-tokens.ts). Provide both `:root` (light) and `:root.dark` blocks; both must be **complete** (FR-005).
2. Update `web/tailwind.config.ts` so Tailwind references the CSS variables (e.g., `colors: { surface: { canvas: 'var(--color-surface-canvas)', ... } }`).
3. Add the no-physical-direction ESLint rule (`web/eslint.config.ts`) to enforce SC-012. The rule disallows `ml-*`, `mr-*`, `pl-*`, `pr-*`, `left-*`, `right-*`, `text-left`, `text-right` outside the token files.
4. Run `pnpm lint`. The lint MUST pass.

---

## 3. Wire the theme provider with flash-free first paint

In `web/app/layout.tsx`:

- Set `<html lang="en" suppressHydrationWarning>` and dynamically set the `dir` attribute via [`directionForLocale`](./contracts/i18n-strings.ts).
- Inline the `next-themes` anti-FOUC script in `<head>`.
- Wrap the body in `<ThemeProvider>` (from `web/app/providers.tsx`) using the `class` strategy and the `bt:theme` storage key from [`contracts/theme-provider.ts`](./contracts/theme-provider.ts).

Reload the application — there should be no light flash on a dark-mode system, and the theme should persist across reloads (FR-006 / SC-004).

---

## 4. Initialize Storybook

```powershell
pnpm storybook
```

Storybook should open at `http://localhost:6006`. Configure `.storybook/preview.tsx` with:

- The `addon-themes` toggle wired to the same `class` strategy as the application (so dark/light parity is previewable).
- The `addon-viewport` defaults including a `13" laptop` reference (SC-010).
- The RTL direction toggle (Research R1 / SC-011).
- A global decorator that sets the `dir` attribute on the story root from the toggle.

Add the introduction pages (`stories/00-introduction.mdx`, `01-brand.mdx`, `02-design-tokens.mdx`) and a `Button.stories.tsx` to verify everything renders.

Run `pnpm test:storybook` (which executes `@storybook/test-runner` + `axe-playwright`). Every story MUST pass axe.

---

## 5. Add the first primitive (Button) end-to-end

This walkthrough validates that the full primitive workflow is in place. Do it once before adding the remaining primitives in Phase B.

1. `pnpm dlx shadcn@latest add button`. The generated component lands in `components/ui/button.tsx`.
2. Replace any hardcoded color/spacing literals with token references via Tailwind classes that resolve to CSS variables (FR-004 / SC-003).
3. Replace any user-facing string with a key from the i18n surface (FR-022a / SC-012).
4. Add `components/ui/button.stories.tsx` covering: default, all variants, all states, both themes, and an a11y check via `addon-a11y`.
5. Add `components/ui/button.test.tsx` with a `vitest-axe` assertion on the default render.
6. Run `pnpm lint && pnpm typecheck && pnpm test && pnpm test:storybook`. All MUST pass.

---

## 6. Wire observability and W3C Trace Context propagation

> Trace context propagation is required regardless of whether you configure Application Insights locally (FR-039 / SC-014).

1. Implement [`lib/observability/adapter.ts`](./contracts/observability-adapter.ts), `lib/observability/noop-adapter.ts`, and `lib/observability/app-insights-adapter.ts`. The AI adapter dynamically imports `@microsoft/applicationinsights-web` so the no-op path doesn't pay the JS cost.
2. Implement `lib/http/trace-context.ts` (`newTraceContext`, `parseTraceparent`, `serializeTraceparent`) per the W3C spec.
3. Implement `lib/http/client.ts` — a typed `fetch` wrapper that always attaches a `traceparent` header (and forwards `tracestate` if present). Feature code consumes only this wrapper.
4. Wire `web-vitals` to forward LCP / INP / CLS / TTFB / FCP through the adapter (FR-037 / SC-016).
5. Wrap `app/(app)/layout.tsx` (and any nested route layouts that own a content surface) with `lib/observability/error-boundary.tsx`. On error: log to the adapter with the React component stack, render an on-brand accessible error surface (FR-036 / SC-015).
6. Add `lib/observability/route-change.ts` to emit a span on App Router navigations (FR-038).

### Local verification

- **Default (no-op adapter)** — Run `pnpm dev` with no env vars set. Open DevTools → Network. You should see `traceparent` headers on every request the UI initiates to a backend route (SC-013 / SC-014 part 1).
- **AI adapter** — Set `NEXT_PUBLIC_APPINSIGHTS_CONNECTION_STRING` in `web/.env.local` and restart. The AI SDK loads dynamically. The browser request now shares its `traceparent` value with the AI dependency telemetry (SC-014 part 2).

---

## 7. Capture the bundle baseline

After Phase A – E are wired (token system, theme provider, shadcn/ui primitives initialized, observability adapter scaffolded, W3C Trace Context propagation in place):

```powershell
pnpm analyze
```

This runs `next build` with `@next/bundle-analyzer` enabled. The HTML and JSON reports land in `web/.next/analyze/`.

1. Open `client.html` and find the First Load JS for the root route `/`.
2. Record the gzipped value in `web/docs/performance-budget.md` as the **measured baseline at foundation handoff**.
3. Update `web/docs/performance-budget.json` with the numeric baseline; this is the input to `scripts/bundle-diff.mjs`.
4. Confirm: baseline ≤ **180 KB gzipped** (soft target). If the baseline exceeds 180 KB but is < 200 KB, document the deferral and budget-tightening plan in `performance-budget.md`. If it crosses **200 KB**, treat as a blocker and identify what can be deferred or dynamically imported.

The full rationale and thresholds are in [Research R2](./research.md#r2-soft-initial-js-bundle-target-for-the-application-shell-fr-035e--sc-020).

---

## 8. Run the cross-browser smoke

```powershell
pnpm exec playwright install
pnpm test:e2e
```

The Playwright config targets Chromium, Firefox, and WebKit. The smoke test exercises the representative composed screen (sidebar + top bar + page header + data table + drawer + form + toast) and asserts:

- No console errors (SC-013 baseline)
- LCP ≤ 2.5 s, INP ≤ 200 ms (via Lighthouse worker), CLS ≤ 0.1 (FR-035d / SC-019)
- Keyboard-only walkthrough completes every interaction without using a pointing device (SC-007)
- Cross-browser parity — all three browsers render without visual regressions (FR-035a / SC-018)

---

## 9. Produce a brand asset (with the required review)

> This walkthrough satisfies FR-002a, FR-002b, and SC-017 for **one** asset. Repeat for each AI-assisted asset.

### Stage 1 — concept exploration

Use any AI tool of choice (Recraft, Figma AI, DALL-E, Midjourney). Keep the candidate rasters and exploratory vectors **out of the repo**; work in a scratch folder until you have a chosen direction.

Document the prompt(s) verbatim — you will paste them into the `REVIEW.md`.

### Stage 2 — vector authoring & hand cleanup

In Figma / Affinity Designer / Inkscape, redraw the chosen concept as plain SVG. Strip embedded rasters. Strip base64 `data:` URIs. Confirm the file opens as text and is readable.

Commit the source SVG to `web/brand/<asset>/`.

### Stage 3 — variant export

Use `web/scripts/brand-export.mjs` (or your tool of choice) to produce the production formats: dark + light SVG, PNG exports across `[16, 24, 32, 48, 64, 128, 256, 512]` px, favicon ICO bundle, OG image (`1200×630`), repo banner. Land them under `web/public/brand/` and `web/public/favicons/`.

### Stage 4 — human originality & licensing review

Open [`contracts/brand-asset-review.md`](./contracts/brand-asset-review.md), copy the template into `web/brand/<asset>/REVIEW.md`, and perform every check. **The review MUST be performed by a human contributor** — agentic completion does not satisfy FR-002b.

Sign the review at the bottom with your GitHub handle and the date.

### Stage 5 — commit

```powershell
pnpm audit:review-records
pnpm audit:svg-hygiene
```

Both audits MUST pass before you commit. Then commit the SVG sources, the exported variants, the `REVIEW.md`, and any wiring changes in `app/icon.tsx` / `app/apple-icon.tsx` / `app/opengraph-image.tsx` / `stories/01-brand.mdx`.

When you replace the placeholder mark, delete the `placeholder.flag` file in the same directory.

---

## 10. Daily contributor commands

| Command | Purpose | Spec link |
|---|---|---|
| `pnpm dev` | Run the foundation locally with HMR | n/a |
| `pnpm storybook` | Run Storybook locally | FR-030 |
| `pnpm lint` | ESLint (incl. no-physical-direction rule) | FR-033 / SC-012 |
| `pnpm typecheck` | Strict TypeScript check | FR-033 |
| `pnpm test` | Vitest + RTL + vitest-axe | FR-027 / FR-033 |
| `pnpm test:storybook` | Story-level axe across every published story | FR-027 / SC-005 |
| `pnpm test:e2e` | Playwright cross-browser smoke + LCP/INP/CLS probe | FR-035a / FR-035d / SC-007 / SC-018 / SC-019 |
| `pnpm analyze` | Bundle analyzer + size diff vs. baseline | FR-035e / SC-020 |
| `pnpm audit:tokens` | No hardcoded color / spacing / radius / elevation / motion literals | SC-003 |
| `pnpm audit:strings` | No hardcoded user-facing strings in primitives/composites | SC-012 |
| `pnpm audit:directions` | No physical left/right outside token files | SC-012 |
| `pnpm audit:svg-hygiene` | All committed SVGs are plain (no `<image>`, no base64) | FR-002a / SC-017 |
| `pnpm audit:review-records` | Every AI-assisted asset has a complete signed `REVIEW.md` | FR-002b |

All commands MUST pass on a clean checkout (SC-009). The CI pipeline runs all of them on every PR.

---

## 11. MCP servers (development-time only)

The MCP servers below are workflow aids for **humans and coding agents while building**. They are **not** part of the BusTerminal runtime, and the product does not depend on them being reachable in production (FR-032).

- **Next.js MCP** — framework conventions, routing, rendering, caching, app architecture
- **shadcn/ui MCP** — component installation, registry usage, component patterns
- **Microsoft Learn MCP** — Azure and Microsoft platform guidance
- **context7 MCP** — current library documentation and examples

When writing or reviewing foundation code, agents and contributors **must** consult these MCP servers for current API guidance rather than relying on training data alone. When the product itself describes its dependencies (in user-facing docs, README, marketing material), MCP servers are never listed.

---

## 12. Where to go next

- For the full requirements list and acceptance criteria: [`spec.md`](./spec.md)
- For why each technology was chosen: [`research.md`](./research.md)
- For the project structure and phase breakdown: [`plan.md`](./plan.md)
- For the entity model (tokens, themes, primitives, composites, brand assets): [`data-model.md`](./data-model.md)
- For the contracts you must implement: [`contracts/`](./contracts/)
- For broader project context (constitution, tech stack reference, easily-forgotten rules): [`/CLAUDE.md`](../../CLAUDE.md)
