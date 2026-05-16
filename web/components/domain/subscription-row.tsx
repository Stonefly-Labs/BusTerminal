"use client";

import * as React from "react";

import { Badge } from "@/components/ui/badge";
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

export interface SubscriptionRowProps
  extends Omit<React.HTMLAttributes<HTMLDivElement>, "children"> {
  readonly subscription: SubscriptionSummary;
}

export const SubscriptionRow = React.forwardRef<HTMLDivElement, SubscriptionRowProps>(
  function SubscriptionRow({ subscription, className, ...rest }, ref) {
    const { icon: SubscriptionIcon, strokeWidth } = getDomainIcon("subscription");
    return (
      <div
        ref={ref}
        role="group"
        aria-label={t("domain.subscription.label")}
        className={cn(
          "flex items-center gap-3 rounded-md border border-border-default bg-surface-elevated px-3 py-2",
          className,
        )}
        {...rest}
      >
        <SubscriptionIcon
          aria-hidden="true"
          strokeWidth={strokeWidth}
          className="size-4 shrink-0 text-foreground-muted"
        />
        <div className="min-w-0 flex-1">
          <TruncatedName name={subscription.name} mono />
          <span className="block truncate font-mono text-xs text-foreground-muted">
            {subscription.parentTopic}
          </span>
        </div>
        <span className="hidden font-mono text-xs text-foreground-muted sm:inline">
          {t("domain.messageCount.label", { count: formatNumber(subscription.messageCount) })}
        </span>
        {subscription.deadLetterCount > 0 ? (
          <span className="hidden font-mono text-xs text-error-foreground sm:inline">
            {t("domain.deadLetter.count", { count: formatNumber(subscription.deadLetterCount) })}
          </span>
        ) : null}
        <Badge intent={SUBSCRIPTION_STATUS_INTENT[subscription.status]}>
          {t(SUBSCRIPTION_STATUS_KEY[subscription.status])}
        </Badge>
      </div>
    );
  },
);
