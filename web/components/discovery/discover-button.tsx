"use client";

/**
 * Spec 009 / T056 / US1.
 *
 * Trigger a discovery run for a namespace. Role-gated on
 * `BusTerminal.NamespaceAdministrator` per FR-027. On click, posts to
 * `POST /api/namespaces/{id}/discover`; on success starts polling
 * `getDiscoveryRun` every 3 s (R-14) until the run reaches a terminal
 * status, then surfaces a success / failure toast (FR-025).
 *
 * The poll lives inside this component's TanStack Query so the
 * `<DiscoveryStatusPanel>` server-rendered alongside it gets re-fetched
 * naturally via React Query invalidation when the run completes.
 */

import { useCallback, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { PlayCircle, Loader2 } from "lucide-react";

import { Button } from "@/components/ui/button";
import { useAcquireToken } from "@/hooks/use-acquire-token";
import { useHasRole } from "@/hooks/use-has-role";
import { useToast } from "@/hooks/use-toast";
import * as DiscoveryApi from "@/lib/discovery/api";
import type { DiscoveryRunStatus } from "@/lib/discovery/schemas";

interface DiscoverButtonProps {
  readonly namespaceId: string;
  /** Optional label override — defaults to "Discover". */
  readonly label?: string;
}

const TERMINAL_STATUSES: ReadonlySet<DiscoveryRunStatus> = new Set([
  "Succeeded",
  "Failed",
]);

export function DiscoverButton({ namespaceId, label = "Discover" }: DiscoverButtonProps) {
  const canTrigger = useHasRole("BusTerminal.NamespaceAdministrator");
  const queryClient = useQueryClient();
  const getToken = useAcquireToken();
  const { toast } = useToast();
  const [activeRunId, setActiveRunId] = useState<string | null>(null);

  const triggerMutation = useMutation({
    mutationFn: async () => {
      const token = await getToken();
      return DiscoveryApi.startDiscovery(namespaceId, token ? { accessToken: token } : {});
    },
    onSuccess: (response) => {
      setActiveRunId(response.discoveryRunId);
      if (response.coalescedFromExisting) {
        toast.info("A discovery is already in flight — joined the existing run.");
      } else {
        toast.info("Discovery requested.");
      }
    },
    onError: (error) => {
      const message = error instanceof Error ? error.message : "Failed to start discovery.";
      toast.error(message);
    },
  });

  const pollQuery = useQuery({
    queryKey: ["discovery-run", activeRunId, namespaceId] as const,
    queryFn: async () => {
      if (!activeRunId) return null;
      const token = await getToken();
      return DiscoveryApi.getDiscoveryRun(activeRunId, namespaceId, token ? { accessToken: token } : {});
    },
    enabled: activeRunId !== null,
    refetchInterval: (query) => {
      const data = query.state.data;
      if (data && TERMINAL_STATUSES.has(data.status)) return false;
      return 3000; // R-14: 3 s poll cadence.
    },
    refetchIntervalInBackground: false,
  });

  const handleTerminal = useCallback(
    (status: DiscoveryRunStatus) => {
      if (status === "Succeeded") {
        toast.success("Discovery completed.");
      } else {
        toast.error("Discovery failed. See the discovery history for details.");
      }
      queryClient.invalidateQueries({ queryKey: ["discovery-runs", namespaceId] });
      setActiveRunId(null);
    },
    [namespaceId, queryClient, toast],
  );

  // Side-effect: when the polled run reaches a terminal state, fire one toast
  // and clear the active run id so a subsequent click starts fresh.
  if (pollQuery.data && TERMINAL_STATUSES.has(pollQuery.data.status) && activeRunId === pollQuery.data.id) {
    handleTerminal(pollQuery.data.status);
  }

  if (!canTrigger) {
    return null;
  }

  const isInFlight = triggerMutation.isPending || activeRunId !== null;

  return (
    <Button
      intent="primary"
      onClick={() => triggerMutation.mutate()}
      disabled={isInFlight}
      aria-live="polite"
      aria-busy={isInFlight}
      data-testid="discover-button"
    >
      {isInFlight ? (
        <>
          <Loader2 className="size-4 animate-spin" aria-hidden="true" />
          Discovering…
        </>
      ) : (
        <>
          <PlayCircle aria-hidden="true" />
          {label}
        </>
      )}
    </Button>
  );
}
