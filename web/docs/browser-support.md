# Browser support

> **Spec**: `specs/001-brand-system-and-design-foundation/spec.md` —
> FR-035a, FR-035b, FR-035c.
> **Storybook**: `Foundation/09 — Browser support`.

BusTerminal targets the browsers operators actually run while working
with cloud infrastructure. The matrix is intentionally narrow so the
foundation can use modern CSS and modern web APIs without polyfill
overhead. This document is the contributor-facing companion to the
Storybook page; the two stay in sync.

---

## Supported matrix (FR-035a)

| Browser | Versions tested |
|---|---|
| Google Chrome (desktop) | last two major versions |
| Microsoft Edge (desktop, Chromium) | last two major versions |
| Mozilla Firefox (desktop) | last two major versions |
| Apple Safari (desktop, macOS) | last two major versions |
| Apple Safari (iPadOS) | last two major versions |
| Google Chrome (Android) | last two major versions |

"Last two major versions" tracks the official release cadence of each
vendor. Browsers older than the last two majors render but are not
tested; defects exclusive to out-of-matrix browsers are closed as
`wont-fix`.

The Playwright cross-browser smoke (`tests/e2e/cross-browser-smoke.spec.ts`,
T145) runs **Chromium, Firefox, and WebKit** with four viewport
projects per browser: mobile (390×844, iPhone 13), 13" laptop
(1366×768), desktop (1920×1080), and 4K (3840×2160).

---

## Out of scope

- Internet Explorer (any version).
- Legacy non-Chromium Edge (EdgeHTML).
- Embedded webviews older than the supported mobile-OS release
  windows.
- Proxy / data-saver browsers (Opera Mini, UC Browser) that strip
  modern JS / CSS.

Defects exclusive to these targets are out of scope.

---

## Modern CSS permitted without polyfills (FR-035b)

| Feature | Where it shows up |
|---|---|
| CSS nesting | Authoring sugar inside `:focus-visible`, `:hover`, media queries |
| `:has()` | Container-aware selectors (composites that style themselves based on contents) |
| `color-mix()` | Token-level color derivation |
| `@property` | Typed custom properties for animated CSS variables |
| Container queries | Composites that adapt to their container, not the viewport |
| OKLCH color space | All token color values; perceptually-uniform contrast tuning |
| CSS logical properties | `margin-inline-*`, `padding-inline-*`, `inset-inline-*`, `text-start`, `text-end`. RTL-safe by construction. |

Authors do **not** add polyfills for these features. If a CSS feature
ships outside this list, an ADR is required before depending on it.

---

## Modern JS permitted without polyfills

| API | Where it shows up |
|---|---|
| `Intl.DateTimeFormat`, `Intl.RelativeTimeFormat`, `Intl.NumberFormat` | `web/lib/i18n/format.ts` |
| `crypto.randomUUID()`, `crypto.getRandomValues()` | `web/lib/http/trace-context.ts` |
| `structuredClone()` | Audit scripts, test fixtures |
| `AbortController` / `AbortSignal` | `web/lib/http/client.ts` |
| `useSyncExternalStore` | `web/hooks/use-reduced-motion.ts` |
| ES2022 syntax | Tooling, tests, application code |

The Next.js compiler targets ES2022 output. The build does not emit a
legacy bundle.

---

## Mobile / tablet read paths (FR-012)

At the mobile viewport (390×844, iPhone 13), the cross-browser smoke
asserts the following render without information loss:

- Entity list renders without horizontal scroll.
- Entity detail drawer opens and renders without truncation.
- Page header remains visible.
- Primary navigation remains reachable via the collapsed sidebar / drawer
  composition pattern.

Write paths (forms, destructive confirmations) are best-effort at the
mobile viewport in v1.

---

## Reference viewports

| Viewport | Width × height | Use |
|---|---|---|
| Mobile (iPhone 13) | 390 × 844 | Mobile read-path check (FR-012) |
| Tablet | 768 × 1024 | Sanity check |
| 13" laptop | 1366 × 768 | Reference viewport — no horizontal scroll (SC-010) |
| Desktop | 1920 × 1080 | Default authoring viewport |
| 4K workstation | 3840 × 2160 | Density-adapts check (Spec Edge Cases) |

These five viewports are declared inline in `.storybook/preview.tsx`
so every primitive story can be inspected at any of them.

---

## Authoring rules

- **Reach for modern selectors and APIs first.** If `:has()` solves a
  layout cleanly, use it.
- **Don't add Babel plugins or transforms** for ES features inside
  the matrix. The Next.js compiler is sufficient.
- **Don't add polyfills** for anything in the supported lists.
- **Test in WebKit.** Most modern-CSS regressions surface in WebKit
  first because it ships the smallest set of newly-stabilized
  features.

---

## Related documents

- `web/stories/09-browser-support.mdx` — Storybook companion.
- `web/docs/performance-budget.md` — bundle target + Core Web Vitals
  thresholds on the reference viewport.
- `specs/001-brand-system-and-design-foundation/spec.md` — the
  underlying functional requirements.
