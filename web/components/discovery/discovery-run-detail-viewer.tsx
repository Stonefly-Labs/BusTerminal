"use client";

/**
 * Spec 009 / T091 / US3.
 *
 * Client wrapper around `<DiscoveryRunDetail>` that loads the run via
 * `getDiscoveryRun` (PK + id read on the Cosmos `discovery-runs` container).
 * Mounted from the per-run route shell. Surfaces loading / not-found /
 * error states inline so the route shell stays a thin RSC.
 */

import { useQuery } from "@tanstack/react-query";

import { Alert, AlertTitle } from "@/components/ui/alert";
import { Skeleton } from "@/components/ui/skeleton";
import { useAcquireToken } from "@/hooks/use-acquire-token";
import * as DiscoveryApi from "@/lib/discovery/api";
import { DiscoveryApiError } from "@/lib/discovery/api";

import { DiscoveryRunDetail } from "./discovery-run-detail";

interface DiscoveryRunDetailViewerProps {
  readonly namespaceId: string;
  readonly runId: string;
}

export function DiscoveryRunDetailViewer({ namespaceId, runId }: DiscoveryRunDetailViewerProps) {
  const getToken = useAcquireToken();
  const query = useQuery({
    queryKey: ["discovery-run", namespaceId, runId] as const,
    queryFn: async () => {
      const token = await getToken();
      return DiscoveryApi.getDiscoveryRun(runId, namespaceId, token ? { accessToken: token } : {});
    },
    retry: (failureCount, error) => {
      if (error instanceof DiscoveryApiError && error.status === 404) return false;
      return failureCount < 1;
    },
  });

  if (query.isLoading) {
    return (
      <div className="flex flex-col gap-3" data-testid="discovery-run-loading">
        <Skeleton className="h-32 w-full" />
        <Skeleton className="h-32 w-full" />
      </div>
    );
  }

  if (query.error instanceof DiscoveryApiError && query.error.status === 404) {
    return (
      <Alert intent="warning" data-testid="discovery-run-not-found">
        <AlertTitle>Discovery run not found</AlertTitle>
      </Alert>
    );
  }

  if (query.isError || !query.data) {
    return (
      <Alert intent="error" data-testid="discovery-run-error">
        <AlertTitle>Could not load the discovery run</AlertTitle>
      </Alert>
    );
  }

  return <DiscoveryRunDetail run={query.data} />;
}
