"use client";

/**
 * Spec 009 / T088 / US3.
 *
 * Discovery history table for a single namespace. Source data is the page
 * of `DiscoveryRun` records returned by `listDiscoveryRuns` (already
 * reverse-chronological per the backend's composite index — the table's
 * `initialSorting` reflects that). Row click navigates to the run detail
 * route.
 *
 * Server-loaded first page is passed in via `initialItems`; pagination
 * happens client-side via TanStack Query so cursor walking doesn't force
 * a full page navigation.
 */

import { useMemo, useState } from "react";
import { useRouter } from "next/navigation";
import type { Route } from "next";
import { useQuery } from "@tanstack/react-query";
import type { ColumnDef } from "@tanstack/react-table";
import { ChevronRight } from "lucide-react";

import { DataTable } from "@/components/data-table/data-table";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { useAcquireToken } from "@/hooks/use-acquire-token";
import * as DiscoveryApi from "@/lib/discovery/api";
import type {
  DiscoveryRun,
  DiscoveryRunStatus,
} from "@/lib/discovery/schemas";

interface DiscoveryRunsTableProps {
  readonly namespaceId: string;
  readonly initialItems: readonly DiscoveryRun[];
  readonly initialContinuationToken?: string | null;
  readonly pageSize?: number;
}

const STATUS_INTENT: Record<DiscoveryRunStatus, "info" | "success" | "warning" | "error"> = {
  Queued: "info",
  InProgress: "info",
  Succeeded: "success",
  Failed: "error",
};

export function DiscoveryRunsTable({
  namespaceId,
  initialItems,
  initialContinuationToken = null,
  pageSize = 25,
}: DiscoveryRunsTableProps) {
  const router = useRouter();
  const getToken = useAcquireToken();
  const [continuationToken, setContinuationToken] = useState<string | null>(
    initialContinuationToken ?? null,
  );
  const [items, setItems] = useState<readonly DiscoveryRun[]>(initialItems);

  // Server-side paginated query: only fires when the user clicks "Load more".
  // Enabled is gated on a non-null token so the initial render uses
  // `initialItems` straight from the server component.
  const nextPageQuery = useQuery({
    queryKey: ["discovery-runs", namespaceId, continuationToken] as const,
    queryFn: async () => {
      if (!continuationToken) return null;
      const token = await getToken();
      const page = await DiscoveryApi.listDiscoveryRuns(
        namespaceId,
        { pageSize, continuationToken },
        token ? { accessToken: token } : {},
      );
      setItems((current) => [...current, ...page.items]);
      setContinuationToken(page.continuationToken ?? null);
      return page;
    },
    enabled: false, // imperative: kicked off by handleLoadMore
  });

  const columns = useMemo<ColumnDef<DiscoveryRun>[]>(() => buildColumns(namespaceId, router), [namespaceId, router]);

  return (
    <div className="flex flex-col gap-3">
      <DataTable<DiscoveryRun>
        caption="Discovery runs for this namespace, most recent first."
        columns={columns}
        data={items}
        getRowId={(row) => row.id}
        enableColumnFilters={false}
        enableColumnVisibility={false}
        enableMultiSelect={false}
        paginationMode="paginated"
        emptyState={{
          title: "No discovery runs yet",
          description: "Trigger a discovery run from the namespace overview to populate this list.",
        }}
        data-testid="discovery-runs-table"
      />
      {continuationToken ? (
        <div className="flex justify-center">
          <Button
            intent="secondary"
            onClick={() => nextPageQuery.refetch()}
            disabled={nextPageQuery.isFetching}
            data-testid="discovery-runs-load-more"
          >
            {nextPageQuery.isFetching ? "Loading…" : "Load more"}
          </Button>
        </div>
      ) : null}
    </div>
  );
}

function buildColumns(
  namespaceId: string,
  router: ReturnType<typeof useRouter>,
): ColumnDef<DiscoveryRun>[] {
  return [
    {
      id: "status",
      accessorKey: "status",
      header: "Status",
      cell: ({ row }) => {
        const status = row.original.status;
        return (
          <Badge
            intent={STATUS_INTENT[status]}
            aria-label={`Status: ${status}`}
            data-testid={`run-status-${row.original.id}`}
          >
            {status}
          </Badge>
        );
      },
    },
    {
      id: "startedUtc",
      accessorKey: "startedUtc",
      header: "Started",
      cell: ({ row }) => (
        <time dateTime={row.original.startedUtc} className="text-sm">
          {new Date(row.original.startedUtc).toLocaleString()}
        </time>
      ),
    },
    {
      id: "completedUtc",
      accessorKey: "completedUtc",
      header: "Completed",
      cell: ({ row }) => {
        const completed = row.original.completedUtc;
        if (!completed) return <span className="text-foreground-muted">—</span>;
        return (
          <time dateTime={completed} className="text-sm">
            {new Date(completed).toLocaleString()}
          </time>
        );
      },
    },
    {
      id: "durationMs",
      accessorKey: "durationMs",
      header: "Duration",
      cell: ({ row }) => (
        <span className="font-mono text-xs" data-testid={`run-duration-${row.original.id}`}>
          {formatDuration(row.original.durationMs)}
        </span>
      ),
    },
    {
      id: "counts",
      header: "Counts",
      cell: ({ row }) => (
        <span
          className="font-mono text-xs text-foreground-muted"
          data-testid={`run-counts-${row.original.id}`}
        >
          {formatCounts(row.original)}
        </span>
      ),
    },
    {
      id: "requestedBy",
      accessorKey: "requestedBy",
      header: "Requested by",
      cell: ({ row }) => (
        <span className="font-mono text-xs">{shortenObjectId(row.original.requestedBy)}</span>
      ),
    },
    {
      id: "id",
      accessorKey: "id",
      header: "Run id",
      cell: ({ row }) => {
        const href = `/namespaces/${namespaceId}/discovery-runs/${row.original.id}` as Route;
        return (
          <button
            type="button"
            onClick={() => router.push(href)}
            className="inline-flex items-center gap-1 font-mono text-xs text-accent-primary underline-offset-2 hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--focus-ring-color)"
            data-testid={`run-link-${row.original.id}`}
            aria-label={`Open discovery run ${row.original.id}`}
          >
            {row.original.id}
            <ChevronRight className="size-3" aria-hidden="true" />
          </button>
        );
      },
    },
  ];
}

function formatDuration(durationMs: number | null | undefined): string {
  if (durationMs == null) return "—";
  if (durationMs < 1000) return `${durationMs} ms`;
  if (durationMs < 60_000) return `${(durationMs / 1000).toFixed(1)} s`;
  const minutes = Math.floor(durationMs / 60_000);
  const seconds = Math.floor((durationMs % 60_000) / 1000);
  return `${minutes}m ${seconds}s`;
}

function formatCounts(run: DiscoveryRun): string {
  const segments = [
    `${run.newCount ?? 0} new`,
    `${run.updatedCount ?? 0} upd`,
    `${run.unchangedCount ?? 0} same`,
    `${run.missingCount ?? 0} miss`,
  ];
  return segments.join(" · ");
}

function shortenObjectId(value: string): string {
  if (value.length <= 12) return value;
  return `${value.slice(0, 8)}…`;
}
