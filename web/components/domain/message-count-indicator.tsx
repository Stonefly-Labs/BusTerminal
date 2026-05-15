import * as React from "react";

import { cn } from "@/lib/design-system/cn";
import { formatNumber } from "@/lib/i18n/format";
import { t } from "@/lib/i18n/strings";

/**
 * Numeric indicator for the active-message count on an entity. The visible
 * numeric uses `formatNumber` for locale-aware thousands grouping; the
 * accessible name is the full localized phrase from
 * `domain.messageCount.label`.
 *
 * A `sparkline` slot is provided so timeline composites can attach a small
 * trend visualization without re-styling the surrounding chip.
 */
export interface MessageCountIndicatorProps
  extends Omit<React.HTMLAttributes<HTMLSpanElement>, "children"> {
  readonly count: number;
  readonly sparkline?: React.ReactNode;
}

export const MessageCountIndicator = React.forwardRef<HTMLSpanElement, MessageCountIndicatorProps>(
  function MessageCountIndicator({ count, sparkline, className, ...rest }, ref) {
    const accessibleLabel = t("domain.messageCount.label", { count: formatNumber(count) });
    return (
      <span
        ref={ref}
        role="status"
        aria-label={accessibleLabel}
        className={cn(
          "inline-flex items-center gap-2 rounded-md border border-border-default bg-surface-muted px-2 py-0.5 text-sm text-foreground-default",
          className,
        )}
        {...rest}
      >
        <span className="font-mono tabular-nums">{formatNumber(count)}</span>
        <span className="text-xs text-foreground-muted">{t("domain.messageCount.heading")}</span>
        {sparkline ? <span aria-hidden="true" className="ms-1 inline-flex items-center">{sparkline}</span> : null}
      </span>
    );
  },
);
