"use client";

/**
 * Spec 009 / T057 / US1 / FR-025.
 *
 * Compact panel rendered next to the Discover button on the namespace
 * overview. Shows:
 *   - status badge for the most recent discovery run
 *   - last-run timestamp
 *   - entity counts (queues, topics, subscriptions, rules)
 *
 * The panel reads via TanStack Query so it picks up live updates when
 * `<DiscoverButton>` invalidates the runs query on terminal status.
 */

import { useQuery } from "@tanstack/react-query";
import { Clock, Hash, Layers } from "lucide-react";

import { Badge } from "@/components/ui/badge";
import { Card, CardContent } from "@/components/ui/card";
import { useAcquireToken } from "@/hooks/use-acquire-token";
import * as DiscoveryApi from "@/lib/discovery/api";
import type { DiscoveryRunStatus } from "@/lib/discovery/schemas";

interface DiscoveryStatusPanelProps {
  readonly namespaceId: string;
}

const BADGE_INTENT: Record<DiscoveryRunStatus, "success" | "warning" | "error" | "info"> = {
  Queued: "info",
  InProgress: "info",
  Succeeded: "success",
  Failed: "error",
};

export function DiscoveryStatusPanel({ namespaceId }: DiscoveryStatusPanelProps) {
  const getToken = useAcquireToken();
  const query = useQuery({
    queryKey: ["discovery-runs", namespaceId, "latest"] as const,
    queryFn: async () => {
      const token = await getToken();
      const page = await DiscoveryApi.listDiscoveryRuns(
        namespaceId,
        { pageSize: 1 },
        token ? { accessToken: token } : {},
      );
      return page.items[0] ?? null;
    },
  });

  if (query.isLoading) {
    return (
      <Card>
        <CardContent className="p-4">
          <p className="text-sm text-foreground-muted">Loading discovery status…</p>
        </CardContent>
      </Card>
    );
  }

  const run = query.data ?? null;
  if (!run) {
    return (
      <Card>
        <CardContent className="flex flex-col gap-2 p-4" data-testid="discovery-status-empty">
          <p className="text-sm text-foreground-muted">
            No discovery runs yet. Trigger one to populate the entity catalog.
          </p>
        </CardContent>
      </Card>
    );
  }

  const started = run.startedUtc ? new Date(run.startedUtc).toLocaleString() : "—";

  return (
    <Card data-testid="discovery-status-panel">
      <CardContent className="flex flex-col gap-3 p-4">
        <div className="flex items-center justify-between gap-3">
          <div className="flex items-center gap-2">
            <Badge intent={BADGE_INTENT[run.status]} aria-label={`Discovery status: ${run.status}`}>
              {run.status}
            </Badge>
            <span className="text-xs text-foreground-muted inline-flex items-center gap-1">
              <Clock className="size-3.5" aria-hidden="true" />
              {started}
            </span>
          </div>
        </div>
        <div className="grid grid-cols-2 gap-x-4 gap-y-1 text-sm sm:grid-cols-4">
          <Stat icon={<Layers className="size-4" aria-hidden="true" />} label="Queues" value={run.queueCount ?? 0} />
          <Stat icon={<Layers className="size-4" aria-hidden="true" />} label="Topics" value={run.topicCount ?? 0} />
          <Stat icon={<Hash className="size-4" aria-hidden="true" />} label="Subs" value={run.subscriptionCount ?? 0} />
          <Stat icon={<Hash className="size-4" aria-hidden="true" />} label="Rules" value={run.ruleCount ?? 0} />
        </div>
      </CardContent>
    </Card>
  );
}

function Stat({ icon, label, value }: { icon: React.ReactNode; label: string; value: number }) {
  return (
    <div className="flex items-center gap-2">
      <span className="text-foreground-muted">{icon}</span>
      <span className="text-foreground-muted">{label}:</span>
      <span className="font-medium text-foreground-default">{value}</span>
    </div>
  );
}
