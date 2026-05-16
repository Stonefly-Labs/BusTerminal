/**
 * Raster-rendering-only color constants.
 *
 * `ImageResponse` (used by `app/icon.tsx`, `app/apple-icon.tsx`,
 * `app/opengraph-image.tsx`) and the W3C `themeColor` viewport metadata
 * field cannot consume CSS custom properties — they render to a PNG / a
 * static HTML attribute respectively. These constants are the ONLY
 * sanctioned hardcoded color values in the foundation; they mirror the
 * dark/light surface tokens in `web/styles/tokens.css`.
 *
 * `pnpm audit:tokens` exempts:
 *   - `app/icon.tsx`
 *   - `app/apple-icon.tsx`
 *   - `app/opengraph-image.tsx`
 *   - `lib/design-system/raster-colors.ts` (this file)
 *
 * Component source MUST NOT import these constants — feature code consumes
 * tokens through Tailwind utilities or `tokenVar()`.
 */

export const RASTER_SURFACE_CANVAS_DARK = "#0f1115";
export const RASTER_FOREGROUND_DEFAULT_DARK = "#e6edf5";
export const RASTER_FOREGROUND_MUTED_DARK = "#9faec5";
export const RASTER_FOREGROUND_SUBTLE_DARK = "#5d6b85";
export const RASTER_SURFACE_CANVAS_LIGHT = "#fafafa";
export const RASTER_OG_GRADIENT_DARK_A = "#0b0e13";
export const RASTER_OG_GRADIENT_DARK_B = "#131923";
export const RASTER_OG_GRADIENT_DARK_C = "#1d2b3f";

export const RASTER_FOREGROUND_INVERSE_LIGHT = "#1f2937";
export const RASTER_FOREGROUND_MUTED_LIGHT = "#6b7280";

/**
 * Storybook manager-UI palette. Storybook's `create()` pipes color values
 * through the `polished` library to derive hover/active/border shades, and
 * polished only parses hex/rgb/hsl — it throws on `oklch(...)` and on CSS
 * `var(--…)` references. These hex values approximate the dark-theme tokens
 * (`color.accent.primary`, `color.border.default`, `color.surface.elevated`)
 * so the Storybook chrome stays visually aligned with the app.
 */
export const RASTER_ACCENT_PRIMARY_DARK = "#4ea3ff";
export const RASTER_BORDER_DEFAULT_DARK = "#3a3f48";
export const RASTER_SURFACE_ELEVATED_DARK = "#1a1d23";
