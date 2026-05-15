"use client";

import * as React from "react";
import { ResponsiveContainer } from "recharts";

import { CHART_DATA_TOKENS, tokenVar } from "@/lib/design-system/tokens";
import { cn } from "@/lib/design-system/cn";

export interface ChartContainerProps {
  readonly children: React.ReactElement;
  readonly height?: number;
  readonly accessibleLabel: string;
  readonly className?: string;
}

/**
 * Wraps a Recharts root element with a responsive container, applies the
 * token-derived chart palette via CSS variables, and exposes an accessible
 * name for screen readers (T096 / FR-029).
 *
 * Reduced-motion enforcement is layered:
 *   1. `app/globals.css` collapses CSS-driven animation under
 *      `prefers-reduced-motion: reduce`.
 *   2. Recharts series enter/update tweens are JS-scheduled and are NOT
 *      governed by the CSS rule. The `ChartLine`, `ChartBar`, and
 *      `ChartArea` wrappers consume `useReducedMotion()` (T108) and pass
 *      `isAnimationActive={false}` to each series when the user has
 *      requested reduced motion (FR-025 / SC-008).
 */
export function ChartContainer({
  children,
  height = 240,
  accessibleLabel,
  className,
}: ChartContainerProps) {
  const paletteStyle = React.useMemo(() => {
    const style: Record<string, string> = {};
    CHART_DATA_TOKENS.forEach((token, index) => {
      style[`--bt-chart-${index + 1}`] = tokenVar(token);
    });
    return style as React.CSSProperties;
  }, []);

  return (
    <div
      role="img"
      aria-label={accessibleLabel}
      className={cn("w-full", className)}
      style={paletteStyle}
    >
      <ResponsiveContainer width="100%" height={height ?? 240}>
        {children}
      </ResponsiveContainer>
    </div>
  );
}

/** Helper for series components: returns the CSS-variable reference. */
export function chartSeriesColor(index: number): string {
  const safe = ((index % CHART_DATA_TOKENS.length) + CHART_DATA_TOKENS.length) % CHART_DATA_TOKENS.length;
  return `var(--bt-chart-${safe + 1})`;
}
