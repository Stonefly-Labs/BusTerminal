"use client";

import * as React from "react";
import {
  flexRender,
  getCoreRowModel,
  getFilteredRowModel,
  getPaginationRowModel,
  getSortedRowModel,
  useReactTable,
  type ColumnDef,
  type ColumnFiltersState,
  type RowSelectionState,
  type SortingState,
  type VisibilityState,
} from "@tanstack/react-table";
import { ArrowDown, ArrowUp, ArrowUpDown } from "lucide-react";

import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { cn } from "@/lib/design-system/cn";

import { DataTableEmptyState } from "./empty-state";
import { DataTableErrorState } from "./error-state";
import { DataTableLoadingState } from "./loading-state";
import { DataTablePagination } from "./pagination";
import { DataTableToolbar } from "./toolbar";

export interface DataTableEmptyStateProps {
  readonly title: string;
  readonly description?: string;
  readonly action?: React.ReactNode;
}

export interface DataTableProps<TData, TValue = unknown> {
  readonly columns: ReadonlyArray<ColumnDef<TData, TValue>>;
  readonly data: ReadonlyArray<TData>;
  readonly getRowId: (row: TData, index: number) => string;
  readonly caption: string;
  readonly enableSorting?: boolean;
  readonly enableColumnFilters?: boolean;
  readonly enableColumnVisibility?: boolean;
  readonly enableMultiSelect?: boolean;
  readonly enableStickyHeader?: boolean;
  readonly enableKeyboardNavigation?: boolean;
  readonly paginationMode?: "paginated" | "virtualized";
  readonly initialSorting?: SortingState;
  readonly initialRowSelection?: RowSelectionState;
  readonly persistenceKey?: string;
  readonly isLoading?: boolean;
  readonly error?: { readonly message: string; readonly retry?: () => void };
  readonly emptyState?: DataTableEmptyStateProps;
  readonly searchColumnId?: string;
  readonly toolbar?: React.ReactNode;
  readonly className?: string;
}

const PERSIST_NS = "bt:foundation:";

function loadPersisted<T>(key: string | undefined, fallback: T): T {
  if (!key || typeof window === "undefined") return fallback;
  try {
    const raw = window.localStorage.getItem(`${PERSIST_NS}${key}`);
    return raw ? (JSON.parse(raw) as T) : fallback;
  } catch {
    return fallback;
  }
}

function persist<T>(key: string | undefined, value: T): void {
  if (!key || typeof window === "undefined") return;
  try {
    window.localStorage.setItem(`${PERSIST_NS}${key}`, JSON.stringify(value));
  } catch {
    /* no-op */
  }
}

export function DataTable<TData, TValue = unknown>(props: DataTableProps<TData, TValue>) {
  const {
    columns,
    data,
    getRowId,
    caption,
    enableSorting = true,
    enableColumnFilters = true,
    enableColumnVisibility = true,
    enableMultiSelect = false,
    enableStickyHeader = true,
    enableKeyboardNavigation = true,
    paginationMode = "paginated",
    initialSorting,
    initialRowSelection,
    persistenceKey,
    isLoading,
    error,
    emptyState,
    searchColumnId,
    toolbar,
    className,
  } = props;

  const [sorting, setSorting] = React.useState<SortingState>(
    () => loadPersisted<SortingState>(persistenceKey ? `${persistenceKey}:sort` : undefined, initialSorting ?? []),
  );
  const [rowSelection, setRowSelection] = React.useState<RowSelectionState>(initialRowSelection ?? {});
  const [columnVisibility, setColumnVisibility] = React.useState<VisibilityState>(
    () => loadPersisted<VisibilityState>(persistenceKey ? `${persistenceKey}:cols` : undefined, {}),
  );
  const [columnFilters, setColumnFilters] = React.useState<ColumnFiltersState>([]);

  React.useEffect(() => {
    persist(persistenceKey ? `${persistenceKey}:sort` : undefined, sorting);
  }, [persistenceKey, sorting]);

  React.useEffect(() => {
    persist(persistenceKey ? `${persistenceKey}:cols` : undefined, columnVisibility);
  }, [persistenceKey, columnVisibility]);

  const table = useReactTable({
    data: data as TData[],
    columns: columns as ColumnDef<TData, TValue>[],
    getRowId,
    enableRowSelection: enableMultiSelect,
    enableMultiRowSelection: enableMultiSelect,
    state: { sorting, rowSelection, columnVisibility, columnFilters },
    onSortingChange: setSorting,
    onRowSelectionChange: setRowSelection,
    onColumnVisibilityChange: setColumnVisibility,
    onColumnFiltersChange: setColumnFilters,
    getCoreRowModel: getCoreRowModel(),
    ...(enableSorting ? { getSortedRowModel: getSortedRowModel() } : {}),
    ...(enableColumnFilters ? { getFilteredRowModel: getFilteredRowModel() } : {}),
    ...(paginationMode === "paginated" ? { getPaginationRowModel: getPaginationRowModel() } : {}),
  });

  const tBodyKeyDown = React.useCallback(
    (event: React.KeyboardEvent<HTMLTableSectionElement>) => {
      if (!enableKeyboardNavigation) return;
      const target = event.target as HTMLElement;
      const row = target.closest("tr");
      if (!row || !row.parentElement) return;
      if (event.key === "ArrowDown") {
        const next = row.nextElementSibling as HTMLTableRowElement | null;
        if (next) {
          event.preventDefault();
          (next.querySelector<HTMLElement>("[tabindex='0']") ?? next).focus();
        }
      } else if (event.key === "ArrowUp") {
        const prev = row.previousElementSibling as HTMLTableRowElement | null;
        if (prev) {
          event.preventDefault();
          (prev.querySelector<HTMLElement>("[tabindex='0']") ?? prev).focus();
        }
      }
    },
    [enableKeyboardNavigation],
  );

  const showToolbar = enableColumnVisibility || enableColumnFilters || toolbar;

  if (error) {
    return (
      <DataTableErrorState
        message={error.message}
        {...(error.retry ? { retry: error.retry } : {})}
      />
    );
  }

  return (
    <div className={cn("flex flex-col rounded-md border border-border-default bg-surface-elevated", className)}>
      {showToolbar ? (
        <>
          {toolbar ?? (
            <DataTableToolbar
              table={table}
              {...(searchColumnId ? { searchColumnId } : {})}
            />
          )}
        </>
      ) : null}
      <div className={cn("relative overflow-auto", enableStickyHeader && "max-h-[60vh]")}>
        {isLoading ? (
          <DataTableLoadingState rows={5} columns={columns.length} />
        ) : (
          <Table>
            <caption className="sr-only">{caption}</caption>
            <TableHeader>
              {table.getHeaderGroups().map((headerGroup) => (
                <TableRow key={headerGroup.id}>
                  {headerGroup.headers.map((header) => {
                    const sortable = header.column.getCanSort();
                    const sorted = header.column.getIsSorted();
                    const ariaSort = sorted === "asc" ? "ascending" : sorted === "desc" ? "descending" : "none";
                    return (
                      <TableHead key={header.id} aria-sort={sortable ? ariaSort : undefined}>
                        {sortable ? (
                          <button
                            type="button"
                            className="inline-flex items-center gap-1 rounded-sm text-xs font-semibold uppercase tracking-wide text-foreground-muted hover:text-foreground-default focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--focus-ring-color)"
                            onClick={() => header.column.toggleSorting(sorted === "asc")}
                          >
                            {flexRender(header.column.columnDef.header, header.getContext())}
                            {sorted === "asc" ? (
                              <ArrowUp className="size-3" aria-hidden="true" />
                            ) : sorted === "desc" ? (
                              <ArrowDown className="size-3" aria-hidden="true" />
                            ) : (
                              <ArrowUpDown className="size-3 opacity-60" aria-hidden="true" />
                            )}
                          </button>
                        ) : (
                          flexRender(header.column.columnDef.header, header.getContext())
                        )}
                      </TableHead>
                    );
                  })}
                </TableRow>
              ))}
            </TableHeader>
            <TableBody onKeyDown={tBodyKeyDown}>
              {table.getRowModel().rows.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={columns.length}>
                    <DataTableEmptyState
                      {...(emptyState?.title ? { title: emptyState.title } : {})}
                      {...(emptyState?.description ? { description: emptyState.description } : {})}
                      {...(emptyState?.action ? { action: emptyState.action } : {})}
                    />
                  </TableCell>
                </TableRow>
              ) : (
                table.getRowModel().rows.map((row) => (
                  <TableRow
                    key={row.id}
                    tabIndex={enableKeyboardNavigation ? 0 : -1}
                    {...(row.getIsSelected() ? { "data-state": "selected" } : {})}
                  >
                    {row.getVisibleCells().map((cell) => (
                      <TableCell key={cell.id}>
                        {flexRender(cell.column.columnDef.cell, cell.getContext())}
                      </TableCell>
                    ))}
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        )}
      </div>
      {paginationMode === "paginated" ? <DataTablePagination table={table} /> : null}
    </div>
  );
}
