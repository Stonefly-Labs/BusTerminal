"use client";

import * as React from "react";

import { Badge } from "@/components/ui/badge";
import { cn } from "@/lib/design-system/cn";
import { formatNumber } from "@/lib/i18n/format";
import { t } from "@/lib/i18n/strings";
import { getDomainIcon } from "@/lib/iconography/domain-icons";

import { TruncatedName } from "./_internals/truncated-name";
import {
  QUEUE_STATUS_INTENT,
  QUEUE_STATUS_KEY,
  type QueueSummary,
} from "./queue-types";

export interface QueueRowProps
  extends Omit<React.HTMLAttributes<HTMLDivElement>, "children"> {
  readonly queue: QueueSummary;
}

/**
 * Single-line representation of a Service Bus queue, suitable for use inside
 * a list or as a custom row in a `DataTable`. Pairs with `QueueCard` for the
 * higher-density tile representation.
 */
export const QueueRow = React.forwardRef<HTMLDivElement, QueueRowProps>(
  function QueueRow({ queue, className, ...rest }, ref) {
    const { icon: QueueIcon, strokeWidth } = getDomainIcon("queue");
    return (
      <div
        ref={ref}
        role="group"
        aria-label={t("domain.queue.label")}
        className={cn(
          "flex items-center gap-3 rounded-md border border-border-default bg-surface-elevated px-3 py-2",
          className,
        )}
        {...rest}
      >
        <QueueIcon
          aria-hidden="true"
          strokeWidth={strokeWidth}
          className="size-4 shrink-0 text-foreground-muted"
        />
        <div className="min-w-0 flex-1">
          <TruncatedName name={queue.name} mono />
        </div>
        <span className="hidden font-mono text-xs text-foreground-muted sm:inline">
          {t("domain.messageCount.label", { count: formatNumber(queue.activeCount) })}
        </span>
        {queue.deadLetterCount > 0 ? (
          <span className="hidden font-mono text-xs text-error-foreground sm:inline">
            {t("domain.deadLetter.count", { count: formatNumber(queue.deadLetterCount) })}
          </span>
        ) : null}
        <Badge intent={QUEUE_STATUS_INTENT[queue.status]}>
          {t(QUEUE_STATUS_KEY[queue.status])}
        </Badge>
      </div>
    );
  },
);
