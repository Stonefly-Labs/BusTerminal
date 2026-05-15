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
 * Reduced-motion is enforced globally via `app/globals.css`; consumers can
 * still pass `isAnimationActive={false}` to individual series for explicit
 * opt-out.
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
