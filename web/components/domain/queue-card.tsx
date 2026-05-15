"use client";

import * as React from "react";

import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader } from "@/components/ui/card";
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

export interface QueueCardProps
  extends Omit<React.HTMLAttributes<HTMLDivElement>, "children" | "title"> {
  readonly queue: QueueSummary;
}

export const QueueCard = React.forwardRef<HTMLDivElement, QueueCardProps>(
  function QueueCard({ queue, className, ...rest }, ref) {
    const { icon: QueueIcon, strokeWidth } = getDomainIcon("queue");
    return (
      <Card
        ref={ref}
        className={cn("w-full max-w-md", className)}
        aria-label={t("domain.queue.label")}
        {...rest}
      >
        <CardHeader className="gap-2">
          <div className="flex items-start gap-3">
            <QueueIcon
              aria-hidden="true"
              strokeWidth={strokeWidth}
              className="mt-0.5 size-5 shrink-0 text-foreground-muted"
            />
            <div className="min-w-0 flex-1">
              <TruncatedName
                name={queue.name}
                headingLevel="h3"
                mono
                className="text-base font-semibold leading-tight text-foreground-default"
              />
              <p className="mt-1 text-xs text-foreground-muted">
                {t("domain.queue.label")}
              </p>
            </div>
            <Badge intent={QUEUE_STATUS_INTENT[queue.status]}>
              {t(QUEUE_STATUS_KEY[queue.status])}
            </Badge>
          </div>
        </CardHeader>
        <CardContent className="grid grid-cols-2 gap-3 pt-2 text-sm">
          <div>
            <p className="text-xs uppercase tracking-wide text-foreground-muted">
              {t("domain.entity.status.active")}
            </p>
            <p className="mt-1 font-mono text-base text-foreground-default">
              {formatNumber(queue.activeCount)}
            </p>
          </div>
          <div>
            <p className="text-xs uppercase tracking-wide text-foreground-muted">
              {t("domain.deadLetter.label")}
            </p>
            <p
              className={cn(
                "mt-1 font-mono text-base",
                queue.deadLetterCount > 0 ? "text-error-foreground" : "text-foreground-default",
              )}
            >
              {formatNumber(queue.deadLetterCount)}
            </p>
          </div>
        </CardContent>
      </Card>
    );
  },
);
