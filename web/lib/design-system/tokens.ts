/**
 * Design Token TypeScript Bridge
 *
 * Implements the contract in
 * `specs/001-brand-system-and-design-foundation/contracts/design-tokens.ts`.
 *
 * Tokens are defined as CSS custom properties in `web/styles/tokens.css`
 * (and `web/styles/typography.css`). This module exposes:
 *
 *   - `tokenVar(name)`        — resolves a token name to a `var(--…)` reference
 *                               for use in inline styles (e.g., Recharts color
 *                               arrays that demand plain strings).
 *   - `CHART_DATA_TOKENS`     — ordered chart palette consumed by the Recharts
 *                               wrapper layer so palette order is centrally
 *                               controlled.
 *   - `ALL_TOKEN_NAMES`       — enumeration of every published token name;
 *                               powers `pnpm audit:tokens` (SC-003).
 *
 * Prefer Tailwind utilities (`bg-surface-canvas`, `text-foreground-default`,
 * `p-4`, `rounded-md`, etc.) over `tokenVar()` whenever a class can express
 * the intent — utilities consume the same CSS variables and keep the token
 * audit's job simple.
 */

// -----------------------------------------------------------------------------
// Token name unions (mirrors contracts/design-tokens.ts)
// -----------------------------------------------------------------------------

export type ColorTokenName =
  | "color.surface.canvas"
  | "color.surface.elevated"
  | "color.surface.overlay"
  | "color.surface.muted"
  | "color.foreground.default"
  | "color.foreground.muted"
  | "color.foreground.subtle"
  | "color.foreground.inverse"
  | "color.border.default"
  | "color.border.muted"
  | "color.border.focus"
  | "color.accent.primary"
  | "color.accent.primary-foreground"
  | "color.accent.hover"
  | "color.accent.active"
  | "color.success.surface"
  | "color.success.foreground"
  | "color.warning.surface"
  | "color.warning.foreground"
  | "color.error.surface"
  | "color.error.foreground"
  | "color.info.surface"
  | "color.info.foreground"
  | "color.disabled.surface"
  | "color.disabled.foreground"
  | "color.interactive.hover"
  | "color.interactive.focus-ring"
  | `color.chart.data-${1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12}`;

export type SpacingTokenName =
  | "spacing.0"
  | "spacing.0.5"
  | "spacing.1"
  | "spacing.1.5"
  | "spacing.2"
  | "spacing.3"
  | "spacing.4"
  | "spacing.5"
  | "spacing.6"
  | "spacing.8"
  | "spacing.10"
  | "spacing.12"
  | "spacing.16";

export type RadiusTokenName =
  | "radius.none"
  | "radius.sm"
  | "radius.md"
  | "radius.lg"
  | "radius.xl"
  | "radius.full";

export type ElevationTokenName =
  | "elevation.0"
  | "elevation.1"
  | "elevation.2"
  | "elevation.3"
  | "elevation.overlay";

export type MotionDurationTokenName =
  | "motion.duration.instant"
  | "motion.duration.fast"
  | "motion.duration.normal"
  | "motion.duration.slow";

export type MotionEasingTokenName =
  | "motion.easing.standard"
  | "motion.easing.enter"
  | "motion.easing.exit";

export type TypographyScaleTokenName =
  | "typography.display"
  | "typography.h1"
  | "typography.h2"
  | "typography.h3"
  | "typography.h4"
  | "typography.h5"
  | "typography.h6"
  | "typography.body"
  | "typography.body-sm"
  | "typography.caption"
  | "typography.label"
  | "typography.table"
  | "typography.mono"
  | "typography.mono-sm";

export type BreakpointTokenName =
  | "breakpoint.sm"
  | "breakpoint.md"
  | "breakpoint.lg"
  | "breakpoint.xl"
  | "breakpoint.2xl";

export type ZIndexTokenName =
  | "z.base"
  | "z.sticky"
  | "z.dropdown"
  | "z.overlay"
  | "z.modal"
  | "z.toast"
  | "z.tooltip";

export type FocusRingTokenName =
  | "focus-ring.color"
  | "focus-ring.width"
  | "focus-ring.offset";

export type DesignTokenName =
  | ColorTokenName
  | SpacingTokenName
  | RadiusTokenName
  | ElevationTokenName
  | MotionDurationTokenName
  | MotionEasingTokenName
  | TypographyScaleTokenName
  | BreakpointTokenName
  | ZIndexTokenName
  | FocusRingTokenName;

// -----------------------------------------------------------------------------
// CSS variable bridge
// -----------------------------------------------------------------------------

/**
 * `color.surface.canvas` → `var(--color-surface-canvas)`.
 *
 * Dots in the token name become dashes in the CSS custom property; otherwise
 * the mapping is one-to-one with the CSS variable declarations in
 * `web/styles/tokens.css` and `web/styles/typography.css`.
 *
 * Typography step tokens (`typography.h2`, `typography.body`, …) resolve to
 * the size variable (`--typography-h2-size`) — primitives consume the
 * matching `-line-height` / `-letter-spacing` / `-weight` variables directly
 * when needed.
 */
export function tokenVar(name: DesignTokenName): string {
  const cssName = name.replaceAll(".", "-");
  if (name.startsWith("typography.")) {
    return `var(--${cssName}-size)`;
  }
  return `var(--${cssName})`;
}

/**
 * Ordered chart palette token references.
 *
 * Recharts series consume entries in declaration order, so palette order is
 * stable across the foundation and a single point of edit.
 */
export const CHART_DATA_TOKENS: readonly ColorTokenName[] = [
  "color.chart.data-1",
  "color.chart.data-2",
  "color.chart.data-3",
  "color.chart.data-4",
  "color.chart.data-5",
  "color.chart.data-6",
  "color.chart.data-7",
  "color.chart.data-8",
  "color.chart.data-9",
  "color.chart.data-10",
  "color.chart.data-11",
  "color.chart.data-12",
] as const;

// -----------------------------------------------------------------------------
// Audit support
// -----------------------------------------------------------------------------

const COLOR_TOKEN_NAMES: readonly ColorTokenName[] = [
  "color.surface.canvas",
  "color.surface.elevated",
  "color.surface.overlay",
  "color.surface.muted",
  "color.foreground.default",
  "color.foreground.muted",
  "color.foreground.subtle",
  "color.foreground.inverse",
  "color.border.default",
  "color.border.muted",
  "color.border.focus",
  "color.accent.primary",
  "color.accent.primary-foreground",
  "color.accent.hover",
  "color.accent.active",
  "color.success.surface",
  "color.success.foreground",
  "color.warning.surface",
  "color.warning.foreground",
  "color.error.surface",
  "color.error.foreground",
  "color.info.surface",
  "color.info.foreground",
  "color.disabled.surface",
  "color.disabled.foreground",
  "color.interactive.hover",
  "color.interactive.focus-ring",
  ...CHART_DATA_TOKENS,
] as const;

const SPACING_TOKEN_NAMES: readonly SpacingTokenName[] = [
  "spacing.0",
  "spacing.0.5",
  "spacing.1",
  "spacing.1.5",
  "spacing.2",
  "spacing.3",
  "spacing.4",
  "spacing.5",
  "spacing.6",
  "spacing.8",
  "spacing.10",
  "spacing.12",
  "spacing.16",
] as const;

const RADIUS_TOKEN_NAMES: readonly RadiusTokenName[] = [
  "radius.none",
  "radius.sm",
  "radius.md",
  "radius.lg",
  "radius.xl",
  "radius.full",
] as const;

const ELEVATION_TOKEN_NAMES: readonly ElevationTokenName[] = [
  "elevation.0",
  "elevation.1",
  "elevation.2",
  "elevation.3",
  "elevation.overlay",
] as const;

const MOTION_DURATION_TOKEN_NAMES: readonly MotionDurationTokenName[] = [
  "motion.duration.instant",
  "motion.duration.fast",
  "motion.duration.normal",
  "motion.duration.slow",
] as const;

const MOTION_EASING_TOKEN_NAMES: readonly MotionEasingTokenName[] = [
  "motion.easing.standard",
  "motion.easing.enter",
  "motion.easing.exit",
] as const;

const TYPOGRAPHY_TOKEN_NAMES: readonly TypographyScaleTokenName[] = [
  "typography.display",
  "typography.h1",
  "typography.h2",
  "typography.h3",
  "typography.h4",
  "typography.h5",
  "typography.h6",
  "typography.body",
  "typography.body-sm",
  "typography.caption",
  "typography.label",
  "typography.table",
  "typography.mono",
  "typography.mono-sm",
] as const;

const BREAKPOINT_TOKEN_NAMES: readonly BreakpointTokenName[] = [
  "breakpoint.sm",
  "breakpoint.md",
  "breakpoint.lg",
  "breakpoint.xl",
  "breakpoint.2xl",
] as const;

const Z_INDEX_TOKEN_NAMES: readonly ZIndexTokenName[] = [
  "z.base",
  "z.sticky",
  "z.dropdown",
  "z.overlay",
  "z.modal",
  "z.toast",
  "z.tooltip",
] as const;

const FOCUS_RING_TOKEN_NAMES: readonly FocusRingTokenName[] = [
  "focus-ring.color",
  "focus-ring.width",
  "focus-ring.offset",
] as const;

/**
 * Enumerated list of every published token name. Consumed by
 * `scripts/audit-tokens.mjs` to verify that primitive and composite source
 * never embeds raw color / spacing / radius / elevation / motion literals.
 */
export const ALL_TOKEN_NAMES: readonly DesignTokenName[] = [
  ...COLOR_TOKEN_NAMES,
  ...SPACING_TOKEN_NAMES,
  ...RADIUS_TOKEN_NAMES,
  ...ELEVATION_TOKEN_NAMES,
  ...MOTION_DURATION_TOKEN_NAMES,
  ...MOTION_EASING_TOKEN_NAMES,
  ...TYPOGRAPHY_TOKEN_NAMES,
  ...BREAKPOINT_TOKEN_NAMES,
  ...Z_INDEX_TOKEN_NAMES,
  ...FOCUS_RING_TOKEN_NAMES,
] as const;
