"use client";

import * as React from "react";

import { Badge } from "@/components/ui/badge";
import { cn } from "@/lib/design-system/cn";
import { formatNumber } from "@/lib/i18n/format";
import { t } from "@/lib/i18n/strings";
import { getDomainIcon } from "@/lib/iconography/domain-icons";

import { TruncatedName } from "./_internals/truncated-name";
import {
  TOPIC_STATUS_INTENT,
  TOPIC_STATUS_KEY,
  type TopicSummary,
} from "./topic-types";

export interface TopicRowProps
  extends Omit<React.HTMLAttributes<HTMLDivElement>, "children"> {
  readonly topic: TopicSummary;
}

export const TopicRow = React.forwardRef<HTMLDivElement, TopicRowProps>(
  function TopicRow({ topic, className, ...rest }, ref) {
    const { icon: TopicIcon, strokeWidth } = getDomainIcon("topic");
    return (
      <div
        ref={ref}
        role="group"
        aria-label={t("domain.topic.label")}
        className={cn(
          "flex items-center gap-3 rounded-md border border-border-default bg-surface-elevated px-3 py-2",
          className,
        )}
        {...rest}
      >
        <TopicIcon
          aria-hidden="true"
          strokeWidth={strokeWidth}
          className="size-4 shrink-0 text-foreground-muted"
        />
        <div className="min-w-0 flex-1">
          <TruncatedName name={topic.name} mono />
        </div>
        <span className="hidden font-mono text-xs text-foreground-muted sm:inline">
          {t("domain.topic.subscriptionCount", {
            count: formatNumber(topic.subscriptionCount),
          })}
        </span>
        <Badge intent={TOPIC_STATUS_INTENT[topic.status]}>
          {t(TOPIC_STATUS_KEY[topic.status])}
        </Badge>
      </div>
    );
  },
);
