"use client";

/**
 * Spec 008 / T108 / US2. TanStack-Table-backed inventory table.
 *
 * Rendering shape: rows are pre-paginated server-side via the inventory
 * endpoint's continuation-token paging — the client table just renders the
 * current page + provides sortable column headers that update the URL `sort`
 * param so the server can resort.
 *
 * Row click navigates to /namespaces/{id}.
 */

import { useMemo } from "react";
import { useRouter, usePathname, useSearchParams } from "next/navigation";
import type { Route } from "next";
import type { ColumnDef } from "@tanstack/react-table";
import { ArrowDown, ArrowUp, ArrowUpDown } from "lucide-react";

import { Card, CardContent } from "@/components/ui/card";
import { cn } from "@/lib/design-system/cn";
import type { OnboardedNamespace } from "@/lib/namespaces/types";

import { LifecycleStatusBadge } from "./lifecycle-status-badge";
import { ValidationStatusBadge } from "./validation-status-badge";

interface NamespaceInventoryTableProps {
  readonly items: ReadonlyArray<OnboardedNamespace>;
  readonly continuationToken?: string | null | undefined;
  readonly className?: string | undefined;
}

const SORT_KEYS = {
  displayName: { asc: "displayName_asc", desc: "displayName_desc" },
  environment: { asc: "environment_asc", desc: "environment_desc" },
  lastValidatedAt: { asc: "lastValidatedAt_asc", desc: "lastValidatedAt_desc" },
} as const;

type SortColumnKey = keyof typeof SORT_KEYS;

function parseSortParam(sort: string | null | undefined): {
  column: SortColumnKey | null;
  direction: "asc" | "desc";
} {
  if (!sort) return { column: "lastValidatedAt", direction: "desc" };
  for (const [col, dirs] of Object.entries(SORT_KEYS) as [SortColumnKey, { asc: string; desc: string }][]) {
    if (dirs.asc === sort) return { column: col, direction: "asc" };
    if (dirs.desc === sort) return { column: col, direction: "desc" };
  }
  return { column: "lastValidatedAt", direction: "desc" };
}

export function NamespaceInventoryTable({
  items,
  continuationToken,
  className,
}: NamespaceInventoryTableProps) {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const sortParam = searchParams.get("sort");
  const { column: sortColumn, direction: sortDirection } = parseSortParam(sortParam);

  const onSort = (column: SortColumnKey) => {
    const next = new URLSearchParams(searchParams.toString());
    const isSame = sortColumn === column;
    const nextDir: "asc" | "desc" = isSame ? (sortDirection === "asc" ? "desc" : "asc") : "asc";
    next.set("sort", SORT_KEYS[column][nextDir]);
    next.delete("continuationToken");
    router.replace(`${pathname}?${next.toString()}` as never);
  };

  const onNextPage = () => {
    if (!continuationToken) return;
    const next = new URLSearchParams(searchParams.toString());
    next.set("continuationToken", continuationToken);
    router.replace(`${pathname}?${next.toString()}` as never);
  };

  const onResetPage = () => {
    const next = new URLSearchParams(searchParams.toString());
    next.delete("continuationToken");
    router.replace(`${pathname}?${next.toString()}` as never);
  };

  const columns = useMemo<ColumnDef<OnboardedNamespace>[]>(
    () => [
      {
        accessorKey: "displayName",
        header: "Name",
        cell: ({ row }) => (
          <div className="flex flex-col">
            <span className="font-medium text-foreground-default">
              {row.original.displayName ?? row.original.name}
            </span>
            <span className="font-mono text-[11px] text-foreground-muted">
              {row.original.name}
            </span>
          </div>
        ),
      },
      {
        accessorKey: "environment",
        header: "Environment",
        cell: ({ row }) => (
          <span className="text-sm">{row.original.environment}</span>
        ),
      },
      {
        accessorKey: "businessUnit",
        header: "Business unit",
        cell: ({ row }) =>
          row.original.businessUnit ? (
            <span className="text-sm">{row.original.businessUnit}</span>
          ) : (
            <span className="text-foreground-muted">—</span>
          ),
      },
      {
        accessorKey: "primaryOwner",
        header: "Primary owner",
        cell: ({ row }) =>
          row.original.ownership?.primaryOwner?.displayNameSnapshot ? (
            <span className="text-sm">{row.original.ownership.primaryOwner.displayNameSnapshot}</span>
          ) : (
            <span className="text-foreground-muted">—</span>
          ),
      },
      {
        accessorKey: "lifecycleStatus",
        header: "Lifecycle",
        cell: ({ row }) => <LifecycleStatusBadge status={row.original.lifecycleStatus ?? null} />,
      },
      {
        accessorKey: "validationStatus",
        header: "Validation",
        cell: ({ row }) => <ValidationStatusBadge status={row.original.validationStatus ?? null} />,
      },
      {
        accessorKey: "lastValidatedAt",
        header: "Last validated",
        cell: ({ row }) =>
          row.original.lastValidatedAtUtc ? (
            <span className="text-sm">{formatRelative(row.original.lastValidatedAtUtc)}</span>
          ) : (
            <span className="text-foreground-muted">—</span>
          ),
      },
    ],
    [],
  );

  if (items.length === 0) {
    return (
      <Card data-testid="namespace-inventory-empty" className={cn("p-8 text-center", className)}>
        <CardContent>
          <h3 className="text-base font-semibold text-foreground-default">No onboarded namespaces</h3>
          <p className="mt-2 text-sm text-foreground-muted">
            No namespaces match the current filters. Try clearing filters, or onboard a new namespace
            from the inventory header.
          </p>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card data-testid="namespace-inventory-table" className={cn(className)}>
      <CardContent className="p-0">
        <table className="w-full text-sm">
          <thead className="bg-surface-canvas text-xs uppercase tracking-wide text-foreground-subtle">
            <tr>
              {columns.map((col) => {
                const id = (col as { accessorKey?: string }).accessorKey ?? "";
                const sortableColumn = (id === "displayName" || id === "environment" || id === "lastValidatedAt")
                  ? (id === "lastValidatedAt" ? "lastValidatedAt" : id as SortColumnKey)
                  : null;
                const isSorted = sortableColumn !== null && sortColumn === sortableColumn;
                const ariaSort: "ascending" | "descending" | "none" = !sortableColumn
                  ? "none"
                  : isSorted
                    ? sortDirection === "asc"
                      ? "ascending"
                      : "descending"
                    : "none";
                return (
                  <th
                    key={id}
                    scope="col"
                    aria-sort={ariaSort}
                    className="border-b border-border-default px-3 py-2 text-start font-medium"
                  >
                    {sortableColumn ? (
                      <button
                        type="button"
                        onClick={() => onSort(sortableColumn)}
                        className="inline-flex items-center gap-1 hover:text-foreground-default"
                      >
                        {typeof col.header === "string" ? col.header : id}
                        {isSorted && sortDirection === "asc" ? (
                          <ArrowUp className="size-3" aria-hidden="true" />
                        ) : isSorted ? (
                          <ArrowDown className="size-3" aria-hidden="true" />
                        ) : (
                          <ArrowUpDown className="size-3 opacity-60" aria-hidden="true" />
                        )}
                      </button>
                    ) : (
                      <span>{typeof col.header === "string" ? col.header : id}</span>
                    )}
                  </th>
                );
              })}
            </tr>
          </thead>
          <tbody>
            {items.map((ns) => (
              <tr
                key={ns.id}
                role="button"
                tabIndex={0}
                onClick={() => router.push(`/namespaces/${ns.id}` as Route)}
                onKeyDown={(e) => {
                  if (e.key === "Enter" || e.key === " ") {
                    e.preventDefault();
                    router.push(`/namespaces/${ns.id}` as Route);
                  }
                }}
                data-row-id={ns.id}
                className="cursor-pointer border-b border-border-default last:border-b-0 hover:bg-interactive-hover focus:bg-interactive-hover focus:outline-2 focus:outline-(--focus-ring-color)"
              >
                {columns.map((col) => {
                  const id = (col as { accessorKey?: string }).accessorKey ?? "";
                  const cellRenderer = col.cell;
                  return (
                    <td key={id} className="px-3 py-2 align-top">
                      {typeof cellRenderer === "function"
                        ? // eslint-disable-next-line @typescript-eslint/no-explicit-any
                          (cellRenderer as any)({ row: { original: ns, getValue: () => undefined } })
                        : "—"}
                    </td>
                  );
                })}
              </tr>
            ))}
          </tbody>
        </table>
        <div className="flex items-center justify-between border-t border-border-default px-3 py-2 text-xs text-foreground-muted">
          <span>
            Showing {items.length} {items.length === 1 ? "namespace" : "namespaces"}
            {continuationToken ? " (more available)" : ""}
          </span>
          <div className="flex items-center gap-2">
            <button
              type="button"
              onClick={onResetPage}
              disabled={!searchParams.get("continuationToken")}
              className="rounded border border-border-default px-2 py-1 disabled:opacity-50"
            >
              First page
            </button>
            <button
              type="button"
              onClick={onNextPage}
              disabled={!continuationToken}
              className="rounded border border-border-default px-2 py-1 disabled:opacity-50"
            >
              Next page
            </button>
          </div>
        </div>
      </CardContent>
    </Card>
  );
}

function formatRelative(iso: string): string {
  const then = Date.parse(iso);
  if (Number.isNaN(then)) return iso;
  const ageMs = Date.now() - then;
  const min = Math.floor(ageMs / 60_000);
  if (min < 1) return "just now";
  if (min < 60) return `${min} min ago`;
  const hrs = Math.floor(min / 60);
  if (hrs < 24) return `${hrs} hr ago`;
  const days = Math.floor(hrs / 24);
  return `${days} day${days === 1 ? "" : "s"} ago`;
}
