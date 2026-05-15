# Accessibility

> **Spec**: `specs/001-brand-system-and-design-foundation/spec.md` —
> FR-022a–d, FR-025, FR-026, FR-027, FR-034.
> **Storybook**: `Foundation/05 — Accessibility`.
> **Test suite**: `tests/e2e/keyboard-only.spec.ts`,
> `tests/e2e/reduced-motion.spec.ts`, `tests/e2e/rtl-smoke.spec.ts`,
> primitive `*.test.tsx` files with `vitest-axe` assertions.

BusTerminal's frontend foundation is engineered so that accessibility is
a property of the primitives, not an audit run before release. Every
primitive ships keyboard-operable, screen-reader-labeled, and
WCAG 2.2 AA clean. This document is the contributor-facing companion to
the Storybook page; the two stay in sync.

---

## Conformance target

| Layer | Target | Enforcement |
|---|---|---|
| Component | WCAG 2.2 AA | `vitest-axe` assertion in every `*.test.tsx` |
| Story | WCAG 2.2 AA | `@storybook/test-runner` + `axe-playwright` (`pnpm test:storybook`) |
| Composed screen | WCAG 2.2 AA | Playwright keyboard / reduced-motion / RTL specs |
| Color tokens | WCAG 2.2 AA (AAA where practical) | `tests/unit/token-contrast.test.ts` |

`pnpm test:storybook` runs with `wcag2a + wcag2aa + wcag21a + wcag21aa + wcag22aa`
configured in `.storybook/test-runner.ts`. The failure threshold is zero
violations — no severity filter, no exemptions, no skips except for
purpose-built failure-case stories that explicitly opt out via
`parameters.a11y.disable = true`.

---

## Keyboard contract

### Focus indication

A visible focus ring is required on every focusable element. The
global `:focus-visible` rule in `app/globals.css` paints an outline using
the design tokens — primitives inherit it automatically. Removing the
focus ring without a documented equally-visible replacement is a defect.

### Tab order

Tab order follows document order. `tabIndex={0}` is permitted to promote
a non-interactive container that needs keyboard reach (e.g., a scrollable
region). `tabIndex >= 1` is forbidden; manually-ordered sequences are
fragile and conflict with assistive technology re-flow.

### Activation map

| Element                         | Activation                                 |
| ------------------------------- | ------------------------------------------ |
| Button                          | `Enter` / `Space`                          |
| Toggle / Switch                 | `Space`                                    |
| Tab trigger                     | `Arrow` keys to move, `Enter` to select    |
| Menu item                       | `Arrow` keys to move, `Enter` to invoke    |
| Combobox / Command palette      | `Cmd/Ctrl+K` to open                       |
| Dialog / Sheet close            | `Escape`                                   |
| Row selection (DataTable)       | `Space`                                    |
| Row navigation (DataTable)      | `Arrow Up` / `Arrow Down`                  |

### Overlay layers

`Dialog`, `Sheet`, `DropdownMenu`, `Popover`, `ContextMenu`, and
`Command` palette ship Radix-provided focus traps. Tab cycles within the
open layer; Escape closes the topmost layer and returns focus to the
opener (`useRestoreFocus` in `lib/utils/a11y.ts`). Nested layers stack —
a Dialog opened from inside a Sheet returns focus to the Sheet first.

---

## ARIA approach

**Semantic HTML first. ARIA only fills semantic gaps.**

- Native elements (`<button>`, `<a href>`, `<input>`, `<select>`,
  `<dialog>`, `<details>`, `<table>`) carry their own semantics. Do not
  redundantly add `role="button"` to a `<button>`.
- Use `aria-label` / `aria-labelledby` only when the visible text is
  absent or insufficient (icon-only buttons; decorative wrappers around
  interactive children).
- Use `aria-describedby` for supporting copy. The `Field` composite
  wires this automatically for help text and validation messages.
- Use `role="status"` (polite) for non-blocking updates;
  `role="alert"` (assertive) only for genuine interruptive errors.
- `aria-required="true"` pairs with a visible required indicator —
  never rely on color alone to indicate required state.

---

## Color-vs-icon affordances

Every semantic state — success, warning, error, info — ships with a
semantic color, a canonical icon, and named text. The `<Alert>`,
`<Badge>`, `<InlineValidation>`, and Sonner toast surfaces render this
combination automatically. The `Badge` primitive accepts a custom icon
override and only allows `icon={false}` when the surrounding context
already provides a non-color affordance.

---

## Reduced-motion strategy

The strategy is layered:

1. **Global CSS rule.** `app/globals.css` has a
   `@media (prefers-reduced-motion: reduce)` block that collapses every
   animation and transition to ~0.01ms. This handles every CSS-driven
   transition across the primitive set (Dialog/Sheet open animations,
   Tabs underline, hover transitions, Toast slide-in).
2. **JavaScript-scheduled motion** is gated by the `useReducedMotion()`
   hook in `web/hooks/use-reduced-motion.ts` (T108). The hook subscribes
   to `prefers-reduced-motion` via `useSyncExternalStore` so consumers
   re-render when the OS preference flips.
3. **Recharts series enter/update tweens** are JS-scheduled. The chart
   wrappers (`<ChartLine>`, `<ChartBar>`, `<ChartArea>`) read
   `useReducedMotion()` and forward `isAnimationActive={false}` to each
   series when the user has requested reduced motion. The Playwright
   spec `tests/e2e/reduced-motion.spec.ts` (T110) verifies this
   end-to-end on the demo screen.

### Authoring rule

Default to no motion. Add motion when it expresses **state** (an
overlay opening; a row revealing). Never as decoration. If motion is
non-essential, gate it via `useReducedMotion()` or via a CSS
`transition-*` utility (the global rule then suppresses it
automatically).

---

## RTL strategy

RTL-safe by construction even though v1 content is English-only.

- **Static guard**: the ESLint rule
  `busterminal/no-physical-direction-utilities` bans `ml-*`, `mr-*`,
  `pl-*`, `pr-*`, `left-*`, `right-*`, `text-left`, `text-right` outside
  `web/styles/tokens.css`. CI also runs `pnpm audit:directions` against
  primitive + composite source.
- **Runtime layout**: every primitive uses CSS logical properties
  (`margin-inline-start`, `padding-inline-end`, `inset-inline-start`,
  `text-start`, etc.). When `dir="rtl"` is set on the document root,
  layout flips at the engine level — no manual mirror code is needed.
- **End-to-end verification**: the Playwright spec
  `tests/e2e/rtl-smoke.spec.ts` (T111) walks the demo screen in
  `dir="rtl"` for both dark and light themes and asserts no clipping,
  no viewport overflow, and correct anchor positioning for menus,
  popovers, and the Sheet.

### Locale formatters

`web/lib/i18n/format.ts` provides `formatDate`, `formatTime`,
`formatRelativeTime`, `formatDuration`, `formatNumber`, and
`formatBytes` — every one wraps native `Intl.*`. The active locale is
resolved at call time (`navigator.language` by default; overridable per
call). Locale-correct output is verified across `de-DE`, `ja-JP`, and
`ar-EG` in `tests/unit/format.test.ts` (T154).

---

## Failure modes

Each of the following is a defect, not a polish task:

- Any `vitest-axe` violation in a primitive's unit test.
- Any `@storybook/test-runner` violation under `pnpm test:storybook`.
- A primitive that requires a pointing device for any interaction.
- A primitive that signals state with color alone.
- A focus ring removed without a documented equally-visible replacement.
- Motion that does not respect `prefers-reduced-motion: reduce`.
- Any hardcoded user-facing string inside a primitive or composite.
- Any physical-direction utility outside `styles/tokens.css`.

---

## Related documents

- `specs/001-brand-system-and-design-foundation/spec.md` — the
  foundation specification (FR-022 / FR-025 / FR-026 / FR-027 / FR-034 /
  SC-002 / SC-005 / SC-007 / SC-008 / SC-011 / SC-012).
- `.specify/memory/constitution.md` — Accessibility & UX principles.
- `web/docs/theming.md` — theme provider + flash-free first paint.
- `web/docs/contributing-frontend.md` — adding a primitive without
  breaking the audits.
