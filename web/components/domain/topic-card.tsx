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
  TOPIC_STATUS_INTENT,
  TOPIC_STATUS_KEY,
  type TopicSummary,
} from "./topic-types";

export interface TopicCardProps
  extends Omit<React.HTMLAttributes<HTMLDivElement>, "children" | "title"> {
  readonly topic: TopicSummary;
}

export const TopicCard = React.forwardRef<HTMLDivElement, TopicCardProps>(
  function TopicCard({ topic, className, ...rest }, ref) {
    const { icon: TopicIcon, strokeWidth } = getDomainIcon("topic");
    return (
      <Card
        ref={ref}
        className={cn("w-full max-w-md", className)}
        aria-label={t("domain.topic.label")}
        {...rest}
      >
        <CardHeader className="gap-2">
          <div className="flex items-start gap-3">
            <TopicIcon
              aria-hidden="true"
              strokeWidth={strokeWidth}
              className="mt-0.5 size-5 shrink-0 text-foreground-muted"
            />
            <div className="min-w-0 flex-1">
              <TruncatedName
                name={topic.name}
                headingLevel="h3"
                mono
                className="text-base font-semibold leading-tight text-foreground-default"
              />
              <p className="mt-1 text-xs text-foreground-muted">
                {t("domain.topic.label")}
              </p>
            </div>
            <Badge intent={TOPIC_STATUS_INTENT[topic.status]}>
              {t(TOPIC_STATUS_KEY[topic.status])}
            </Badge>
          </div>
        </CardHeader>
        <CardContent className="grid grid-cols-2 gap-3 pt-2 text-sm">
          <div>
            <p className="text-xs uppercase tracking-wide text-foreground-muted">
              {t("domain.subscription.label")}
            </p>
            <p className="mt-1 font-mono text-base text-foreground-default">
              {formatNumber(topic.subscriptionCount)}
            </p>
          </div>
          <div>
            <p className="text-xs uppercase tracking-wide text-foreground-muted">
              {t("domain.messageCount.heading")}
            </p>
            <p className="mt-1 font-mono text-base text-foreground-default">
              {formatNumber(topic.messageCount)}
            </p>
          </div>
        </CardContent>
      </Card>
    );
  },
);
