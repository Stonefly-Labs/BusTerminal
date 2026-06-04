"use client";

/**
 * Spec 006 / T113. Renders the search results in a TanStack-Table-backed
 * data table. Row click navigates to the matching detail page.
 *
 * Empty/loading/error/no-results states all read from the surrounding
 * `query` lifecycle; the empty-state distinguishes "no results" from
 * "feature unavailable" (FR-031) by inspecting the search status.
 */

import { useMemo } from "react";
import { useRouter } from "next/navigation";
import type { Route } from "next";
import type { ColumnDef } from "@tanstack/react-table";

import { Badge } from "@/components/ui/badge";
import { Card, CardContent } from "@/components/ui/card";
import { cn } from "@/lib/design-system/cn";

import { RegistryEmptyState } from "./registry-empty-state";
import { RegistryStatusBadge } from "./registry-status-badge";

export interface RegistrySearchResultRow {
  readonly id: string;
  readonly entityType: "Namespace" | "Queue" | "Topic" | "Subscription" | "Rule";
  readonly name: string;
  readonly fullyQualifiedName?: string | null | undefined;
  readonly environment?: string | null | undefined;
  readonly status?: string | null | undefined;
  readonly owner?: string | null | undefined;
  readonly namespaceName?: string | null | undefined;
  readonly score?: number | null | undefined;
}

interface RegistrySearchResultsTableProps {
  readonly results: readonly RegistrySearchResultRow[];
  readonly state: "idle" | "loading" | "loaded" | "error" | "unavailable";
  readonly errorMessage?: string | undefined;
  readonly totalCount?: number | null | undefined;
  readonly page: number;
  readonly pageSize: number;
  readonly onPageChange?: (next: number) => void;
  readonly className?: string | undefined;
}

export function RegistrySearchResultsTable({
  results,
  state,
  errorMessage,
  totalCount,
  page,
  pageSize,
  onPageChange,
  className,
}: RegistrySearchResultsTableProps) {
  const router = useRouter();

  const columns = useMemo<ColumnDef<RegistrySearchResultRow>[]>(
    () => [
      {
        accessorKey: "name",
        header: "Name",
        cell: ({ row }) => (
          <div className="flex flex-col">
            <span className="font-medium text-foreground-default">{row.original.name}</span>
            {row.original.fullyQualifiedName ? (
              <span className="font-mono text-[11px] text-foreground-muted">
                {row.original.fullyQualifiedName}
              </span>
            ) : null}
          </div>
        ),
      },
      {
        accessorKey: "entityType",
        header: "Type",
        cell: ({ row }) => <Badge intent="outline">{row.original.entityType}</Badge>,
      },
      {
        accessorKey: "environment",
        header: "Environment",
        cell: ({ row }) =>
          row.original.environment ? (
            <span className="text-sm">{row.original.environment}</span>
          ) : (
            <span className="text-foreground-muted">—</span>
          ),
      },
      {
        accessorKey: "namespaceName",
        header: "Parent namespace",
        cell: ({ row }) =>
          row.original.namespaceName ? (
            <span className="font-mono text-xs">{row.original.namespaceName}</span>
          ) : (
            <span className="text-foreground-muted">—</span>
          ),
      },
      {
        accessorKey: "owner",
        header: "Owner",
        cell: ({ row }) =>
          row.original.owner ? (
            <span className="text-sm">{row.original.owner}</span>
          ) : (
            <span className="text-foreground-muted">—</span>
          ),
      },
      {
        accessorKey: "status",
        header: "Status",
        cell: ({ row }) =>
          row.original.status === "Active" || row.original.status === "Deprecated" ? (
            <RegistryStatusBadge status={row.original.status} />
          ) : (
            <span className="text-foreground-muted">—</span>
          ),
      },
      {
        accessorKey: "score",
        header: "Score",
        cell: ({ row }) =>
          row.original.score === undefined || row.original.score === null ? (
            <span className="text-foreground-muted">—</span>
          ) : (
            <span className="font-mono text-xs">{row.original.score.toFixed(2)}</span>
          ),
      },
    ],
    [],
  );

  if (state === "unavailable") {
    return (
      <RegistryEmptyState
        variant="unavailable"
        title="Search unavailable"
        description="Azure AI Search is temporarily unreachable. Browse and detail experiences remain available — try again in a moment."
      />
    );
  }
  if (state === "error") {
    return (
      <RegistryEmptyState
        variant="unavailable"
        title="Search failed"
        description={errorMessage ?? "An unexpected error occurred while running the search."}
      />
    );
  }
  if (state === "loading") {
    return (
      <div data-testid="registry-search-results-loading" className="text-sm text-foreground-muted">
        Searching…
      </div>
    );
  }
  if (state === "idle") {
    return (
      <RegistryEmptyState
        variant="no-data"
        title="Type to search the registry"
        description="Results appear as you type. Filters narrow the result set without leaving the page."
      />
    );
  }
  if (results.length === 0) {
    return (
      <RegistryEmptyState
        variant="no-results"
        title="No results"
        description="No registry entities match your query and filters. Try a different keyword or clear a filter."
      />
    );
  }

  return (
    <Card data-testid="registry-search-results-table" className={cn(className)}>
      <CardContent className="p-0">
        <table className="w-full text-sm">
          <thead className="bg-surface-canvas text-xs uppercase tracking-wide text-foreground-subtle">
            <tr>
              {columns.map((col) => {
                const id = (col as { accessorKey?: string }).accessorKey ?? "";
                return (
                  <th
                    key={id}
                    scope="col"
                    className="border-b border-border-default px-3 py-2 text-start font-medium"
                  >
                    {typeof col.header === "string" ? col.header : id}
                  </th>
                );
              })}
            </tr>
          </thead>
          <tbody>
            {results.map((row) => (
              <tr
                key={row.id}
                role="button"
                tabIndex={0}
                onClick={() => router.push(`/registry/${row.entityType}/${row.id}` as Route)}
                onKeyDown={(e) => {
                  if (e.key === "Enter" || e.key === " ") {
                    e.preventDefault();
                    router.push(`/registry/${row.entityType}/${row.id}` as Route);
                  }
                }}
                data-row-id={row.id}
                className="cursor-pointer border-b border-border-default last:border-b-0 hover:bg-interactive-hover focus:bg-interactive-hover focus:outline-2 focus:outline-(--focus-ring-color)"
              >
                {columns.map((col) => {
                  const id = (col as { accessorKey?: string }).accessorKey ?? "";
                  const cellRenderer = col.cell;
                  const value = (row as unknown as Record<string, unknown>)[id];
                  // Render the cell function manually — passing a minimal row
                  // shim that exposes `original` is enough for the column
                  // renderers above.
                  return (
                    <td key={id} className="px-3 py-2 align-top">
                      {typeof cellRenderer === "function"
                        ? // eslint-disable-next-line @typescript-eslint/no-explicit-any
                          (cellRenderer as any)({ row: { original: row, getValue: () => value } })
                        : value === null || value === undefined
                          ? "—"
                          : String(value)}
                    </td>
                  );
                })}
              </tr>
            ))}
          </tbody>
        </table>
        {typeof totalCount === "number" && onPageChange ? (
          <div className="flex items-center justify-between border-t border-border-default px-3 py-2 text-xs text-foreground-muted">
            <span>
              Page {page} • {results.length} of {totalCount}
            </span>
            <div className="flex items-center gap-2">
              <button
                type="button"
                onClick={() => onPageChange(Math.max(1, page - 1))}
                disabled={page <= 1}
                className="rounded border border-border-default px-2 py-1 disabled:opacity-50"
              >
                Previous
              </button>
              <button
                type="button"
                onClick={() => onPageChange(page + 1)}
                disabled={page * pageSize >= totalCount}
                className="rounded border border-border-default px-2 py-1 disabled:opacity-50"
              >
                Next
              </button>
            </div>
          </div>
        ) : null}
      </CardContent>
    </Card>
  );
}
