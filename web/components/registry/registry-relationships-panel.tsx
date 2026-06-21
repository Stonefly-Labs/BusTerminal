"use client";

/**
 * Spec 006 / T123 [US3]. Children list shown on the entity detail page.
 *
 * Queries the registry list endpoint scoped to the parent's environment with
 * `parentId={entity.id}`. Renders the children in a TanStack-Table-backed
 * data table; row click navigates to the child's detail page so the operator
 * can drill topic → subscription → rule (quickstart §7 walkthrough).
 *
 * Leaf entity types (Queue, Rule) have no children; the panel renders a
 * muted "no children" state instead of issuing a query.
 */

import { useMemo } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import type { Route } from "next";
import { useQuery } from "@tanstack/react-query";
import {
  flexRender,
  getCoreRowModel,
  useReactTable,
  type ColumnDef,
} from "@tanstack/react-table";

import { cn } from "@/lib/design-system/cn";
import { listEntities, RegistryApiError } from "@/lib/registry/api";
import { useAcquireToken } from "@/hooks/use-acquire-token";
import { registryQueryKeys } from "@/lib/registry/query-keys";
import type { RegistryEntity, RegistryEntityType } from "@/lib/registry/types";

import { RegistryStatusBadge } from "./registry-status-badge";

interface RegistryRelationshipsPanelProps {
  readonly entity: Pick<RegistryEntity, "id" | "entityType" | "environment">;
  readonly className?: string;
}

const LEAF_TYPES: ReadonlySet<RegistryEntityType> = new Set(["Queue", "Rule"]);

export function RegistryRelationshipsPanel({
  entity,
  className,
}: RegistryRelationshipsPanelProps) {
  const router = useRouter();
  const getToken = useAcquireToken();
  const isLeaf = LEAF_TYPES.has(entity.entityType);

  const childrenQuery = useQuery({
    queryKey: registryQueryKeys.entities.list({
      environment: entity.environment,
      parentId: entity.id,
    }),
    // The registry API client never acquires its own token — resolve one here
    // so the call is authenticated under real Entra auth.
    queryFn: async () => {
      const token = await getToken();
      return listEntities(
        {
          environment: entity.environment,
          parentId: entity.id,
          pageSize: 200,
        },
        token ? { accessToken: token } : {},
      );
    },
    enabled: !isLeaf,
  });

  const rows = useMemo(() => childrenQuery.data?.items ?? [], [childrenQuery.data]);

  const columns = useMemo<ColumnDef<RegistryEntity>[]>(
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
        cell: ({ row }) => (
          <span className="text-xs uppercase tracking-wide text-foreground-muted">
            {row.original.entityType}
          </span>
        ),
      },
      {
        accessorKey: "status",
        header: "Status",
        cell: ({ row }) => <RegistryStatusBadge status={row.original.status} />,
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
    ],
    [],
  );

  const table = useReactTable({
    data: rows,
    columns,
    getRowId: (r) => r.id,
    getCoreRowModel: getCoreRowModel(),
  });

  if (isLeaf) {
    return (
      <p
        data-testid="registry-relationships-panel"
        data-variant="leaf"
        className={cn("text-sm text-foreground-muted", className)}
      >
        {entity.entityType} entities have no children.
      </p>
    );
  }

  if (childrenQuery.isPending) {
    return (
      <p
        data-testid="registry-relationships-panel"
        data-variant="loading"
        className={cn("text-sm text-foreground-muted", className)}
      >
        Loading children…
      </p>
    );
  }

  if (childrenQuery.isError) {
    const message =
      childrenQuery.error instanceof RegistryApiError || childrenQuery.error instanceof Error
        ? childrenQuery.error.message
        : "Could not load child entities.";
    return (
      <p
        data-testid="registry-relationships-panel"
        data-variant="error"
        role="alert"
        className={cn("text-sm text-foreground-default", className)}
      >
        {message}
      </p>
    );
  }

  if (rows.length === 0) {
    return (
      <p
        data-testid="registry-relationships-panel"
        data-variant="empty"
        className={cn("text-sm text-foreground-muted", className)}
      >
        No children yet. Use the explorer&apos;s “New child” affordance to add one.
      </p>
    );
  }

  return (
    <div
      data-testid="registry-relationships-panel"
      data-variant="loaded"
      className={cn("overflow-hidden rounded-md border border-border-default", className)}
    >
      <table className="w-full text-sm">
        <thead className="bg-surface-canvas text-xs uppercase tracking-wide text-foreground-subtle">
          {table.getHeaderGroups().map((headerGroup) => (
            <tr key={headerGroup.id}>
              {headerGroup.headers.map((header) => (
                <th
                  key={header.id}
                  scope="col"
                  className="border-b border-border-default px-3 py-2 text-start font-medium"
                >
                  {flexRender(header.column.columnDef.header, header.getContext())}
                </th>
              ))}
            </tr>
          ))}
        </thead>
        <tbody>
          {table.getRowModel().rows.map((row) => {
            const href =
              `/registry/${row.original.entityType}/${row.original.id}` as Route;
            return (
              <tr
                key={row.id}
                data-row-id={row.original.id}
                tabIndex={0}
                onClick={() => router.push(href)}
                onKeyDown={(e) => {
                  if (e.key === "Enter" || e.key === " ") {
                    e.preventDefault();
                    router.push(href);
                  }
                }}
                className="cursor-pointer border-b border-border-default last:border-b-0 hover:bg-interactive-hover focus:bg-interactive-hover focus:outline-2 focus:outline-(--focus-ring-color)"
              >
                {row.getVisibleCells().map((cell, idx) => (
                  <td key={cell.id} className="px-3 py-2 align-top">
                    {idx === 0 ? (
                      <Link
                        href={href}
                        onClick={(e) => e.stopPropagation()}
                        className="block focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--focus-ring-color)"
                      >
                        {flexRender(cell.column.columnDef.cell, cell.getContext())}
                      </Link>
                    ) : (
                      flexRender(cell.column.columnDef.cell, cell.getContext())
                    )}
                  </td>
                ))}
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}
