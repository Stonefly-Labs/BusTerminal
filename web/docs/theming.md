# Theming

> **Spec**: `specs/001-brand-system-and-design-foundation/spec.md` —
> FR-005, FR-006, FR-007, SC-004.
> **Storybook**: `Foundation/06 — Theming`.
> **Contract**: `specs/001-brand-system-and-design-foundation/contracts/theme-provider.ts`.

BusTerminal ships **dark mode as the primary operational experience**
and **light mode as a fully-supported peer** — not a tinted derivative.
Both themes are complete bindings of the same token surface. Every
semantic foreground / surface pair is contrast-verified at WCAG 2.2
AA in both themes (FR-007).

This document is the contributor-facing companion to the Storybook
page; the two stay in sync.

---

## Why dark is primary

Operators run BusTerminal in long sessions alongside terminals,
observability dashboards, and editors that are also dark. Defaulting
to light forces them to alt-tab between a bright app and a dark stack
all day. Dark is the primary experience because that's where most
reading happens; light is independently maintained, independently
verified, and independently tested.

---

## Theme provider

Theme management is `next-themes` with the **class strategy** on the
`<html>` element. The contract is
`specs/001-brand-system-and-design-foundation/contracts/theme-provider.ts`.

| Hook point | Responsibility |
|---|---|
| `web/app/layout.tsx` | Sets `<html lang>` and `<html dir>`; injects the inline anti-FOUC script in `<head>` (`strategy="beforeInteractive"`); mounts `<Providers>` |
| `web/app/providers.tsx` | `<ThemeProvider attribute="class" defaultTheme="system" enableSystem storageKey="bt:theme" disableTransitionOnChange>` |
| `web/lib/theme-provider-constants.ts` | Single source of truth for the `bt:theme` storage key (shared between RSC layout and client provider) |

`next-themes` resolves the theme at first paint in this order:

1. **Persisted preference.** `localStorage["bt:theme"]` = `"light"` or
   `"dark"`.
2. **System preference.** Otherwise the value of
   `prefers-color-scheme`.
3. **Default fallback.** Otherwise `"light"`.

The resolved theme is written to `document.documentElement.classList`
(`.dark` for dark) and `document.documentElement.style.colorScheme`
**before paint** — that's what makes first paint flash-free (SC-004).
`tests/e2e/theme-flash.spec.ts` (T105) asserts this end-to-end.

---

## Anti-FOUC script

```ts
// app/layout.tsx — synchronous, runs in <head> before any rendered HTML
(function () {
  try {
    var storageKey = "bt:theme";
    var stored = window.localStorage.getItem(storageKey);
    var systemDark =
      window.matchMedia("(prefers-color-scheme: dark)").matches;
    var theme =
      stored === "light" || stored === "dark"
        ? stored
        : systemDark
        ? "dark"
        : "light";
    if (theme === "dark") {
      document.documentElement.classList.add("dark");
    }
    document.documentElement.style.colorScheme = theme;
  } catch (err) {
    /* no-op; ThemeProvider resolves on hydration */
  }
})();
```

Three properties matter:

1. **`strategy="beforeInteractive"`** on the `<Script>` tag. Next.js
   guarantees execution before any rendered HTML paints.
2. **Synchronous body** — no `await`, no top-level Promise.
3. **Try / catch wrap** — private-mode browsers that throw on
   `localStorage` bail silently; `ThemeProvider` resolves on
   hydration (one brief flash; operational majority sees none).

---

## Storage key

`bt:theme`, declared once in
`web/lib/theme-provider-constants.ts`. The `bt:` prefix is
foundation-reserved. `bt:foundation:*` is reserved for table-foundation
column-visibility preferences. Feature specs pick a feature-scoped
prefix; do not reuse `bt:`.

---

## Toggling theme at runtime

```tsx
import { useTheme } from "next-themes";

function ThemeToggle() {
  const { resolvedTheme, setTheme } = useTheme();
  const next = resolvedTheme === "dark" ? "light" : "dark";
  return (
    <Button onClick={() => setTheme(next)}>
      {next === "dark"
        ? t("appshell.topbar.themeToggle.toDark")
        : t("appshell.topbar.themeToggle.toLight")}
    </Button>
  );
}
```

`disableTransitionOnChange={true}` on the provider so toggling does
not blur tokens through a stale transition — the new theme paints in
one frame.

The edge case worth testing: a Dialog, a Sheet, a Toast, and a Chart
all open simultaneously, theme toggle invoked. The Playwright spec
`tests/e2e/theme-switch.spec.ts` (T106) asserts no leaked dark / light
values, no broken focus rings, and no stale chart colors.

---

## Verified contrast pairings

Every semantic foreground / surface pair is verified at WCAG 2.2 AA
or AAA in both themes. The runtime check is
`web/tests/unit/token-contrast.test.ts`, which renders every pair
through `vitest-axe` with the `color-contrast` rule.

| Foreground | Surface | Light ratio | Dark ratio | Verdict |
|---|---|---:|---:|---|
| `foreground.default` | `surface.canvas` | 14.6 : 1 | 14.3 : 1 | AAA |
| `foreground.default` | `surface.elevated` | 15.4 : 1 | 12.0 : 1 | AAA |
| `foreground.muted` | `surface.canvas` | 6.4 : 1 | 6.7 : 1 | AAA |
| `foreground.muted` | `surface.elevated` | 6.7 : 1 | 5.6 : 1 | AA |
| `foreground.subtle` | `surface.canvas` | 4.6 : 1 | 3.6 : 1 | AA / fail-large |
| `accent.primary-foreground` | `accent.primary` | 6.0 : 1 | 9.1 : 1 | AAA |
| `success.foreground` | `success.surface` | 6.8 : 1 | 7.4 : 1 | AAA |
| `warning.foreground` | `warning.surface` | 6.2 : 1 | 7.1 : 1 | AAA |
| `error.foreground` | `error.surface` | 5.4 : 1 | 6.6 : 1 | AA |
| `info.foreground` | `info.surface` | 6.5 : 1 | 7.0 : 1 | AAA |

`foreground.subtle` is **not approved for body text** — it is
reserved for tertiary chrome where the surrounding context already
carries the meaning. Body text defaults to `foreground.default`;
secondary text steps down to `foreground.muted`.

---

## Authoring rules

- **Use only token references** in primitive and composite source.
  Both themes route the same token through to different OKLCH values
  — reaching for a hex literal breaks parity instantly.
- **Don't author against the `:root.dark` selector** in primitive
  source. The token system already handles the binding. The only
  files that should target `:root.dark` are `web/styles/tokens.css`
  (the token table) and `web/app/globals.css` (the base layer).
- **Test in both themes.** Every primitive story ships a
  `theme: light` and `theme: dark` parameter via `addon-themes` so
  the Storybook test-runner scans both.
- **Don't theme through `prefers-color-scheme` media queries.** Use
  the `.dark` class strategy. Media queries can desync from the
  persisted preference.
- **No tinting.** Light is not "darken everything 20%"; dark is not
  "lighten everything 20%." Both are independently designed.

---

## Related documents

- `web/stories/06-theming.mdx` — Storybook companion.
- `web/docs/accessibility.md` — focus-ring contract,
  color-vs-icon affordances.
- `specs/001-brand-system-and-design-foundation/contracts/theme-provider.ts`
  — the contract.
- `web/styles/tokens.css`, `web/styles/typography.css` — the token
  bindings.
