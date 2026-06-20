/**
 * Spec 009 / T076 / US2.
 *
 * Server component — renders the discovery-side audit trail for a single
 * published entity: lifecycle status badge, first-discovered timestamp,
 * last-seen timestamp, and the last discovery run id (link target lives in
 * Phase 5 — for now the id is presented as-is so operators can copy/paste
 * it into the discovery-runs list).
 */

import { Clock, Search } from "lucide-react";

import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import type { LifecycleStatus, PublishedEntity } from "@/lib/discovery/schemas";

interface EntityDiscoveryInfoProps {
  readonly entity: Pick<
    PublishedEntity,
    "lifecycleStatus" | "firstDiscoveredUtc" | "lastSeenUtc" | "lastDiscoveryRunId"
  >;
}

const LIFECYCLE_INTENT: Record<LifecycleStatus, "success" | "warning" | "info"> = {
  Active: "success",
  Missing: "warning",
  Archived: "info",
};

export function EntityDiscoveryInfo({ entity }: EntityDiscoveryInfoProps) {
  const first = entity.firstDiscoveredUtc ? new Date(entity.firstDiscoveredUtc) : null;
  const last = entity.lastSeenUtc ? new Date(entity.lastSeenUtc) : null;

  return (
    <Card data-testid="entity-discovery-info">
      <CardHeader>
        <CardTitle>Discovery info</CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-2">
        <div className="flex items-center gap-2">
          <Badge
            intent={LIFECYCLE_INTENT[entity.lifecycleStatus]}
            aria-label={`Lifecycle: ${entity.lifecycleStatus}`}
            data-testid="entity-lifecycle-badge"
          >
            {entity.lifecycleStatus}
          </Badge>
        </div>
        <Row icon={<Search aria-hidden="true" />} label="First discovered">
          <time data-testid="entity-first-discovered" dateTime={entity.firstDiscoveredUtc}>
            {first ? first.toLocaleString() : "—"}
          </time>
        </Row>
        <Row icon={<Clock aria-hidden="true" />} label="Last seen">
          <time data-testid="entity-last-seen" dateTime={entity.lastSeenUtc ?? undefined}>
            {last ? last.toLocaleString() : "—"}
          </time>
        </Row>
        {entity.lastDiscoveryRunId ? (
          <Row icon={<Search aria-hidden="true" />} label="Last run">
            <span data-testid="entity-last-run-id" className="font-mono text-xs">
              {entity.lastDiscoveryRunId}
            </span>
          </Row>
        ) : null}
      </CardContent>
    </Card>
  );
}

function Row({
  icon,
  label,
  children,
}: {
  icon: React.ReactNode;
  label: string;
  children: React.ReactNode;
}) {
  return (
    <div className="flex items-center gap-2 text-sm">
      <span className="text-foreground-muted">{icon}</span>
      <span className="text-foreground-muted">{label}:</span>
      <span className="font-medium text-foreground-default">{children}</span>
    </div>
  );
}
