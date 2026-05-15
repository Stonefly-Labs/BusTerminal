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
  SUBSCRIPTION_STATUS_INTENT,
  SUBSCRIPTION_STATUS_KEY,
  type SubscriptionSummary,
} from "./subscription-types";

export interface SubscriptionCardProps
  extends Omit<React.HTMLAttributes<HTMLDivElement>, "children" | "title"> {
  readonly subscription: SubscriptionSummary;
}

export const SubscriptionCard = React.forwardRef<HTMLDivElement, SubscriptionCardProps>(
  function SubscriptionCard({ subscription, className, ...rest }, ref) {
    const { icon: SubscriptionIcon, strokeWidth } = getDomainIcon("subscription");
    return (
      <Card
        ref={ref}
        className={cn("w-full max-w-md", className)}
        aria-label={t("domain.subscription.label")}
        {...rest}
      >
        <CardHeader className="gap-2">
          <div className="flex items-start gap-3">
            <SubscriptionIcon
              aria-hidden="true"
              strokeWidth={strokeWidth}
              className="mt-0.5 size-5 shrink-0 text-foreground-muted"
            />
            <div className="min-w-0 flex-1">
              <TruncatedName
                name={subscription.name}
                headingLevel="h3"
                mono
                className="text-base font-semibold leading-tight text-foreground-default"
              />
              <p className="mt-1 text-xs text-foreground-muted">
                <span>{t("domain.subscription.parentTopic")}: </span>
                <span className="font-mono">{subscription.parentTopic}</span>
              </p>
            </div>
            <Badge intent={SUBSCRIPTION_STATUS_INTENT[subscription.status]}>
              {t(SUBSCRIPTION_STATUS_KEY[subscription.status])}
            </Badge>
          </div>
        </CardHeader>
        <CardContent className="grid grid-cols-2 gap-3 pt-2 text-sm">
          <div>
            <p className="text-xs uppercase tracking-wide text-foreground-muted">
              {t("domain.messageCount.heading")}
            </p>
            <p className="mt-1 font-mono text-base text-foreground-default">
              {formatNumber(subscription.messageCount)}
            </p>
          </div>
          <div>
            <p className="text-xs uppercase tracking-wide text-foreground-muted">
              {t("domain.deadLetter.label")}
            </p>
            <p
              className={cn(
                "mt-1 font-mono text-base",
                subscription.deadLetterCount > 0
                  ? "text-error-foreground"
                  : "text-foreground-default",
              )}
            >
              {formatNumber(subscription.deadLetterCount)}
            </p>
          </div>
        </CardContent>
      </Card>
    );
  },
);
