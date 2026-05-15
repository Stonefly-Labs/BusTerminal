import * as React from "react";
import { AlertCircle, AlertTriangle, CheckCircle2 } from "lucide-react";

import { cn } from "@/lib/design-system/cn";
import { formatNumber } from "@/lib/i18n/format";
import { t } from "@/lib/i18n/strings";

export type HealthState = "healthy" | "degraded" | "unhealthy";

export interface HealthSummaryCounts {
  readonly healthy: number;
  readonly degraded: number;
  readonly unhealthy: number;
}

export interface HealthSummaryIndicatorProps
  extends Omit<React.HTMLAttributes<HTMLDivElement>, "children"> {
  readonly counts: HealthSummaryCounts;
}

const STATE_LABEL_KEY = {
  healthy: "domain.health.healthy",
  degraded: "domain.health.degraded",
  unhealthy: "domain.health.unhealthy",
} as const satisfies Record<HealthState, "domain.health.healthy" | "domain.health.degraded" | "domain.health.unhealthy">;

const STATE_ICON = {
  healthy: CheckCircle2,
  degraded: AlertTriangle,
  unhealthy: AlertCircle,
} as const;

function rollupState(counts: HealthSummaryCounts): HealthState {
  if (counts.unhealthy > 0) return "unhealthy";
  if (counts.degraded > 0) return "degraded";
  return "healthy";
}

/**
 * Three-pill summary of an entity set's health, paired with an aggregate
 * roll-up that drives a top-level icon + color. The composite is purely a
 * status surface — interactivity should live in the consuming page.
 */
export const HealthSummaryIndicator = React.forwardRef<HTMLDivElement, HealthSummaryIndicatorProps>(
  function HealthSummaryIndicator({ counts, className, ...rest }, ref) {
    const rollup = rollupState(counts);
    const RollupIcon = STATE_ICON[rollup];
    return (
      <div
        ref={ref}
        role="group"
        aria-label={t(STATE_LABEL_KEY[rollup])}
        className={cn(
          "inline-flex items-center gap-3 rounded-md border border-border-default bg-surface-elevated px-3 py-1.5",
          className,
        )}
        {...rest}
      >
        <RollupIcon
          aria-hidden="true"
          className={cn(
            "size-4 shrink-0",
            rollup === "healthy" && "text-success-foreground",
            rollup === "degraded" && "text-warning-foreground",
            rollup === "unhealthy" && "text-error-foreground",
          )}
        />
        <span className="text-sm font-medium text-foreground-default">
          {t(STATE_LABEL_KEY[rollup])}
        </span>
        <span className="hidden h-4 border-s border-border-default sm:inline-block" aria-hidden="true" />
        <ul className="hidden items-center gap-2 text-xs sm:inline-flex" role="list">
          {(["healthy", "degraded", "unhealthy"] as const).map((state) => {
            const StateIcon = STATE_ICON[state];
            const value = counts[state];
            return (
              <li key={state} className="inline-flex items-center gap-1">
                <StateIcon
                  aria-hidden="true"
                  className={cn(
                    "size-3",
                    state === "healthy" && "text-success-foreground",
                    state === "degraded" && "text-warning-foreground",
                    state === "unhealthy" && "text-error-foreground",
                  )}
                />
                <span className="font-mono tabular-nums text-foreground-default">
                  {formatNumber(value)}
                </span>
                <span className="text-foreground-muted">{t(STATE_LABEL_KEY[state])}</span>
              </li>
            );
          })}
        </ul>
      </div>
    );
  },
);
