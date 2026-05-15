import * as React from "react";
import { Check, Clock, Loader2, X } from "lucide-react";

import { Badge } from "@/components/ui/badge";
import { cn } from "@/lib/design-system/cn";
import { formatRelativeTime } from "@/lib/i18n/format";
import { t } from "@/lib/i18n/strings";

export type DiscoveryJobState = "queued" | "running" | "succeeded" | "failed";

export interface DiscoveryJobStatusProps
  extends Omit<React.HTMLAttributes<HTMLDivElement>, "children"> {
  readonly state: DiscoveryJobState;
  /**
   * Absolute timestamp at which the job started. Renders as locale-aware
   * relative time ("Started 5 minutes ago"). Omit when the job has not
   * started yet (state === "queued").
   */
  readonly startedAt?: Date;
  /**
   * Optional override for the reference "now" — exposed primarily for tests
   * and stories so the relative time is deterministic.
   */
  readonly now?: Date;
}

const STATE_INTENT = {
  queued: "neutral",
  running: "info",
  succeeded: "success",
  failed: "error",
} as const satisfies Record<DiscoveryJobState, "neutral" | "info" | "success" | "error">;

const STATE_LABEL_KEY = {
  queued: "domain.discoveryJob.queued",
  running: "domain.discoveryJob.running",
  succeeded: "domain.discoveryJob.succeeded",
  failed: "domain.discoveryJob.failed",
} as const satisfies Record<
  DiscoveryJobState,
  | "domain.discoveryJob.queued"
  | "domain.discoveryJob.running"
  | "domain.discoveryJob.succeeded"
  | "domain.discoveryJob.failed"
>;

const STATE_ICON = {
  queued: Clock,
  running: Loader2,
  succeeded: Check,
  failed: X,
} as const;

function relativeFromMs(ms: number): { value: number; unit: Intl.RelativeTimeFormatUnit } {
  const absSec = Math.abs(ms) / 1000;
  if (absSec < 60) return { value: Math.round(ms / 1000), unit: "second" };
  if (absSec < 3600) return { value: Math.round(ms / 60_000), unit: "minute" };
  if (absSec < 86_400) return { value: Math.round(ms / 3_600_000), unit: "hour" };
  return { value: Math.round(ms / 86_400_000), unit: "day" };
}

export const DiscoveryJobStatus = React.forwardRef<HTMLDivElement, DiscoveryJobStatusProps>(
  function DiscoveryJobStatus({ state, startedAt, now, className, ...rest }, ref) {
    const StateIcon = STATE_ICON[state];
    const ago = React.useMemo(() => {
      if (!startedAt) return null;
      const reference = now ?? new Date();
      const deltaMs = startedAt.getTime() - reference.getTime();
      const { value, unit } = relativeFromMs(deltaMs);
      return formatRelativeTime(value, unit);
    }, [startedAt, now]);

    return (
      <div
        ref={ref}
        role="group"
        aria-label={t(STATE_LABEL_KEY[state])}
        className={cn("inline-flex items-center gap-2", className)}
        {...rest}
      >
        <Badge intent={STATE_INTENT[state]} icon={false}>
          <StateIcon
            aria-hidden="true"
            className={cn("size-3", state === "running" && "motion-safe:animate-spin")}
          />
          <span className="ms-1">{t(STATE_LABEL_KEY[state])}</span>
        </Badge>
        {ago ? (
          <span className="text-xs text-foreground-muted">
            {t("domain.discoveryJob.startedAgo", { ago })}
          </span>
        ) : null}
      </div>
    );
  },
);
