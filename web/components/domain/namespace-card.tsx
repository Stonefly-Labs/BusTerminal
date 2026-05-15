"use client";

import * as React from "react";

import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader } from "@/components/ui/card";
import { cn } from "@/lib/design-system/cn";
import { formatNumber } from "@/lib/i18n/format";
import { t } from "@/lib/i18n/strings";
import { getDomainIcon } from "@/lib/iconography/domain-icons";

import { TruncatedName } from "./_internals/truncated-name";

export type NamespaceTier = "basic" | "standard" | "premium";
export type NamespaceStatus = "healthy" | "degraded" | "unhealthy";

export interface NamespaceSummary {
  readonly id: string;
  readonly name: string;
  readonly tier: NamespaceTier;
  readonly region: string;
  readonly status: NamespaceStatus;
  readonly queueCount: number;
  readonly topicCount: number;
}

const TIER_KEY = {
  basic: "domain.namespace.tier.basic",
  standard: "domain.namespace.tier.standard",
  premium: "domain.namespace.tier.premium",
} as const satisfies Record<NamespaceTier, "domain.namespace.tier.basic" | "domain.namespace.tier.standard" | "domain.namespace.tier.premium">;

const STATUS_INTENT: Record<NamespaceStatus, "success" | "warning" | "error"> = {
  healthy: "success",
  degraded: "warning",
  unhealthy: "error",
};

const STATUS_KEY = {
  healthy: "domain.health.healthy",
  degraded: "domain.health.degraded",
  unhealthy: "domain.health.unhealthy",
} as const satisfies Record<NamespaceStatus, "domain.health.healthy" | "domain.health.degraded" | "domain.health.unhealthy">;

export interface NamespaceCardProps
  extends Omit<React.HTMLAttributes<HTMLDivElement>, "children" | "title"> {
  readonly namespace: NamespaceSummary;
}

export const NamespaceCard = React.forwardRef<HTMLDivElement, NamespaceCardProps>(
  function NamespaceCard({ namespace, className, ...rest }, ref) {
    const { icon: NamespaceIcon, strokeWidth } = getDomainIcon("namespace");
    const statusLabel = t(STATUS_KEY[namespace.status]);
    return (
      <Card
        ref={ref}
        className={cn("w-full max-w-md", className)}
        aria-label={t("domain.namespace.label")}
        {...rest}
      >
        <CardHeader className="gap-2">
          <div className="flex items-start gap-3">
            <NamespaceIcon
              aria-hidden="true"
              strokeWidth={strokeWidth}
              className="mt-0.5 size-5 shrink-0 text-foreground-muted"
            />
            <div className="min-w-0 flex-1">
              <TruncatedName
                name={namespace.name}
                headingLevel="h3"
                mono
                className="text-base font-semibold leading-tight text-foreground-default"
              />
              <p className="mt-1 text-xs text-foreground-muted">
                <span className="font-mono">{namespace.region}</span>
                <span aria-hidden="true"> · </span>
                <span>{t(TIER_KEY[namespace.tier])}</span>
              </p>
            </div>
            <Badge intent={STATUS_INTENT[namespace.status]}>{statusLabel}</Badge>
          </div>
        </CardHeader>
        <CardContent className="grid grid-cols-2 gap-3 pt-2 text-sm">
          <div>
            <p className="text-xs uppercase tracking-wide text-foreground-muted">
              {t("domain.namespace.stats.queues")}
            </p>
            <p className="mt-1 font-mono text-base text-foreground-default">
              {formatNumber(namespace.queueCount)}
            </p>
          </div>
          <div>
            <p className="text-xs uppercase tracking-wide text-foreground-muted">
              {t("domain.namespace.stats.topics")}
            </p>
            <p className="mt-1 font-mono text-base text-foreground-default">
              {formatNumber(namespace.topicCount)}
            </p>
          </div>
        </CardContent>
      </Card>
    );
  },
);
