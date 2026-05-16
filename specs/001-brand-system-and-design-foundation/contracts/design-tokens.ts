/**
 * Design Token Contract
 *
 * Spec references:
 *   - FR-004 (all design values are tokens; no hardcoded values in feature code)
 *   - FR-005 (dark + light bindings)
 *   - FR-007 (semantic tokens meet WCAG AA in both themes)
 *   - FR-008 (typography scale)
 *   - SC-003 (audit finds zero hardcoded literals)
 *
 * Tokens are defined as CSS variables in `web/styles/tokens.css` and exposed
 * to TypeScript via the helpers in this contract. Feature code MUST NOT
 * introduce ad-hoc literals — the `pnpm audit:tokens` gate fails the build
 * when it finds any.
 */

// -----------------------------------------------------------------------------
// Token namespaces
// -----------------------------------------------------------------------------

export type ColorTokenName =
  // Surfaces
  | 'color.surface.canvas'
  | 'color.surface.elevated'
  | 'color.surface.overlay'
  | 'color.surface.muted'
  // Foreground
  | 'color.foreground.default'
  | 'color.foreground.muted'
  | 'color.foreground.subtle'
  | 'color.foreground.inverse'
  // Borders
  | 'color.border.default'
  | 'color.border.muted'
  | 'color.border.focus'
  // Brand accent
  | 'color.accent.primary'
  | 'color.accent.primary-foreground'
  | 'color.accent.hover'
  | 'color.accent.active'
  // Semantic
  | 'color.success.surface'
  | 'color.success.foreground'
  | 'color.warning.surface'
  | 'color.warning.foreground'
  | 'color.error.surface'
  | 'color.error.foreground'
  | 'color.info.surface'
  | 'color.info.foreground'
  // Disabled / interactive
  | 'color.disabled.surface'
  | 'color.disabled.foreground'
  | 'color.interactive.hover'
  | 'color.interactive.focus-ring'
  // Data viz
  | `color.chart.data-${1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12}`;

export type SpacingTokenName =
  | 'spacing.0'
  | 'spacing.0.5'
  | 'spacing.1'
  | 'spacing.1.5'
  | 'spacing.2'
  | 'spacing.3'
  | 'spacing.4'
  | 'spacing.5'
  | 'spacing.6'
  | 'spacing.8'
  | 'spacing.10'
  | 'spacing.12'
  | 'spacing.16';

export type RadiusTokenName =
  | 'radius.none'
  | 'radius.sm'
  | 'radius.md'
  | 'radius.lg'
  | 'radius.xl'
  | 'radius.full';

export type ElevationTokenName =
  | 'elevation.0'
  | 'elevation.1'
  | 'elevation.2'
  | 'elevation.3'
  | 'elevation.overlay';

export type MotionDurationTokenName =
  | 'motion.duration.instant'
  | 'motion.duration.fast'
  | 'motion.duration.normal'
  | 'motion.duration.slow';

export type MotionEasingTokenName =
  | 'motion.easing.standard'
  | 'motion.easing.enter'
  | 'motion.easing.exit';

export type TypographyScaleTokenName =
  | 'typography.display'
  | 'typography.h1'
  | 'typography.h2'
  | 'typography.h3'
  | 'typography.h4'
  | 'typography.h5'
  | 'typography.h6'
  | 'typography.body'
  | 'typography.body-sm'
  | 'typography.caption'
  | 'typography.label'
  | 'typography.table'
  | 'typography.mono'
  | 'typography.mono-sm';

export type BreakpointTokenName =
  | 'breakpoint.sm'
  | 'breakpoint.md'
  | 'breakpoint.lg'
  | 'breakpoint.xl'
  | 'breakpoint.2xl';

export type ZIndexTokenName =
  | 'z.base'
  | 'z.sticky'
  | 'z.dropdown'
  | 'z.overlay'
  | 'z.modal'
  | 'z.toast'
  | 'z.tooltip';

export type FocusRingTokenName =
  | 'focus-ring.color'
  | 'focus-ring.width'
  | 'focus-ring.offset';

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
 * Returns the CSS custom-property reference for a token, suitable for use
 * in inline styles. Prefer Tailwind utilities consuming the same variables
 * over inline styles; this helper exists for edge cases (e.g., chart color
 * arrays passed to Recharts as plain strings).
 *
 * Example: `tokenVar('color.accent.primary')` -> `'var(--color-accent-primary)'`
 */
export declare function tokenVar(name: DesignTokenName): string;

/**
 * Lookup table for the chart color sequence; consumed by the Recharts wrapper
 * so palette order is centrally controlled.
 */
export declare const CHART_DATA_TOKENS: readonly ColorTokenName[];

// -----------------------------------------------------------------------------
// Audit support
// -----------------------------------------------------------------------------

/**
 * The list of token names is enumerated to power `pnpm audit:tokens`,
 * which scans primitive and composite source for hardcoded color / spacing /
 * radius / elevation / motion literals and fails when it finds them.
 */
export declare const ALL_TOKEN_NAMES: readonly DesignTokenName[];
