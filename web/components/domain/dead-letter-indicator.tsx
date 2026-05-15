import * as React from "react";

import { cn } from "@/lib/design-system/cn";
import { formatNumber } from "@/lib/i18n/format";
import { t } from "@/lib/i18n/strings";
import { getDomainIcon } from "@/lib/iconography/domain-icons";

/**
 * Color + icon + text indicator for the dead-letter sub-queue.
 *
 * Intent palette tracks the count threshold so a zero count is neutral and
 * a positive count escalates to the error-surface tone. The label, icon, and
 * numeric badge together satisfy FR-026 (no color-only affordance).
 */
export interface DeadLetterIndicatorProps
  extends Omit<React.HTMLAttributes<HTMLSpanElement>, "children"> {
  readonly count: number;
  readonly size?: "sm" | "md";
}

export const DeadLetterIndicator = React.forwardRef<HTMLSpanElement, DeadLetterIndicatorProps>(
  function DeadLetterIndicator({ count, size = "md", className, ...rest }, ref) {
    const { icon: DeadLetterIcon, strokeWidth } = getDomainIcon("dead-letter");
    const hasDeadLetters = count > 0;
    return (
      <span
        ref={ref}
        role="status"
        aria-label={t("domain.deadLetter.count", { count: formatNumber(count) })}
        className={cn(
          "inline-flex items-center gap-1.5 rounded-full border px-2 py-0.5 font-medium",
          size === "sm" ? "text-xs" : "text-sm",
          hasDeadLetters
            ? "border-error-foreground/30 bg-error-surface text-error-foreground"
            : "border-border-default bg-surface-muted text-foreground-muted",
          className,
        )}
        {...rest}
      >
        <DeadLetterIcon
          aria-hidden="true"
          strokeWidth={strokeWidth}
          className={size === "sm" ? "size-3" : "size-3.5"}
        />
        <span className="font-mono tabular-nums">{formatNumber(count)}</span>
        <span>{t("domain.deadLetter.label")}</span>
      </span>
    );
  },
);
