/**
 * Storybook UI theme (T043).
 *
 * Brand-aligned manager UI theme. Values mirror the dark-mode design
 * tokens declared in `web/styles/tokens.css` so the Storybook chrome reads
 * as the same product as the foundation app. Values are duplicated here
 * because Storybook's `create()` consumes raw CSS strings — it does not
 * resolve CSS custom properties.
 */

import { create } from "storybook/theming";

// Storybook's manager bundle is built with esbuild directly and does not
// honor the project's `@/*` TypeScript path alias. Constants are inlined
// here (mirroring `lib/design-system/raster-colors.ts`) so the Storybook
// UI stays brand-aligned. Update both files if you change the dark-surface
// palette in `web/styles/tokens.css`.
//
// IMPORTANT: All values must be hex/rgb/hsl. Storybook's `create()` runs
// every color through the `polished` library, which throws on `oklch(...)`
// and on CSS `var(--…)` references.
const RASTER_SURFACE_CANVAS_DARK = "#0f1115";
const RASTER_FOREGROUND_DEFAULT_DARK = "#e6edf5";
const RASTER_FOREGROUND_MUTED_DARK = "#9faec5";
const ACCENT = "#4ea3ff";
const BORDER = "#3a3f48";
const SURFACE_ELEVATED = "#1a1d23";

export default create({
  base: "dark",
  brandTitle: "BusTerminal Foundation",
  brandUrl: "/",
  brandTarget: "_self",

  colorPrimary: ACCENT,
  colorSecondary: ACCENT,

  appBg: RASTER_SURFACE_CANVAS_DARK,
  appContentBg: RASTER_SURFACE_CANVAS_DARK,
  appPreviewBg: SURFACE_ELEVATED,
  appBorderColor: BORDER,
  appBorderRadius: 8,

  fontBase:
    "ui-sans-serif, system-ui, -apple-system, 'Segoe UI', Roboto, sans-serif",
  fontCode:
    "ui-monospace, SFMono-Regular, Menlo, Consolas, 'Liberation Mono', monospace",

  textColor: RASTER_FOREGROUND_DEFAULT_DARK,
  textInverseColor: RASTER_SURFACE_CANVAS_DARK,
  textMutedColor: RASTER_FOREGROUND_MUTED_DARK,

  barBg: SURFACE_ELEVATED,
  barTextColor: RASTER_FOREGROUND_DEFAULT_DARK,
  barSelectedColor: ACCENT,

  inputBg: SURFACE_ELEVATED,
  inputBorder: BORDER,
  inputTextColor: RASTER_FOREGROUND_DEFAULT_DARK,
  inputBorderRadius: 6,
});
