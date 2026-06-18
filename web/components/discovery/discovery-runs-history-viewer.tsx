"use client";

/**
 * Spec 009 / T090 / US3.
 *
 * Client wrapper that loads the first page of discovery runs for a
 * namespace and renders the `<DiscoveryRunsTable>` with that page as
 * `initialItems`. Loading / error states surface inline so the route
 * shell stays a thin RSC.
 */

import { useQuery } from "@tanstack/react-query";

import { Alert, AlertTitle } from "@/components/ui/alert";
import { Skeleton } from "@/components/ui/skeleton";
import { useAcquireToken } from "@/hooks/use-acquire-token";
import * as DiscoveryApi from "@/lib/discovery/api";

import { DiscoveryRunsTable } from "./discovery-runs-table";

interface DiscoveryRunsHistoryViewerProps {
  readonly namespaceId: string;
  readonly pageSize?: number;
}

export function DiscoveryRunsHistoryViewer({
  namespaceId,
  pageSize = 25,
}: DiscoveryRunsHistoryViewerProps) {
  const getToken = useAcquireToken();
  const query = useQuery({
    queryKey: ["discovery-runs", namespaceId, "first-page", pageSize] as const,
    queryFn: async () => {
      const token = await getToken();
      return DiscoveryApi.listDiscoveryRuns(
        namespaceId,
        { pageSize },
        token ? { accessToken: token } : {},
      );
    },
  });

  if (query.isLoading) {
    return (
      <div className="flex flex-col gap-2" data-testid="discovery-runs-loading">
        <Skeleton className="h-12 w-full" />
        <Skeleton className="h-12 w-full" />
        <Skeleton className="h-12 w-full" />
      </div>
    );
  }

  if (query.isError || !query.data) {
    return (
      <Alert intent="error" data-testid="discovery-runs-error">
        <AlertTitle>Could not load discovery history</AlertTitle>
      </Alert>
    );
  }

  return (
    <DiscoveryRunsTable
      namespaceId={namespaceId}
      initialItems={query.data.items}
      initialContinuationToken={query.data.continuationToken ?? null}
      pageSize={pageSize}
    />
  );
}
