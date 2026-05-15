"use client";

import * as React from "react";
import { Columns3, Search } from "lucide-react";
import type { Table } from "@tanstack/react-table";

import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuCheckboxItem,
  DropdownMenuContent,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Input } from "@/components/ui/input";
import { cn } from "@/lib/design-system/cn";
import { t } from "@/lib/i18n";

export interface DataTableToolbarProps<TData> {
  readonly table: Table<TData>;
  readonly searchColumnId?: string;
  readonly bulkActions?: React.ReactNode;
  readonly extra?: React.ReactNode;
  readonly className?: string;
}

export function DataTableToolbar<TData>({
  table,
  searchColumnId,
  bulkActions,
  extra,
  className,
}: DataTableToolbarProps<TData>) {
  const selectedCount = table.getSelectedRowModel().rows.length;
  const searchColumn = searchColumnId ? table.getColumn(searchColumnId) : undefined;
  const value = (searchColumn?.getFilterValue() as string | undefined) ?? "";
  return (
    <div className={cn("flex flex-wrap items-center justify-between gap-2 p-3", className)}>
      <div className="flex flex-1 items-center gap-2">
        {searchColumn ? (
          <div className="relative w-full max-w-sm">
            <Search
              className="pointer-events-none absolute start-2.5 top-1/2 size-4 -translate-y-1/2 text-foreground-muted"
              aria-hidden="true"
            />
            <Input
              value={value}
              onChange={(event) => searchColumn.setFilterValue(event.target.value)}
              placeholder={t("table.toolbar.search.placeholder")}
              aria-label={t("table.toolbar.search.placeholder")}
              className="ps-8"
            />
          </div>
        ) : null}
        {selectedCount > 0 ? (
          <span className="text-xs text-foreground-muted">
            {t("table.toolbar.bulkActions.selected", { count: selectedCount })}
          </span>
        ) : null}
        {bulkActions}
      </div>
      <div className="flex items-center gap-2">
        {extra}
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button intent="secondary" size="sm" aria-label={t("table.toolbar.columnVisibility.label")}>
              <Columns3 />
              {t("table.toolbar.columnVisibility.label")}
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            <DropdownMenuLabel>{t("table.toolbar.columnVisibility.label")}</DropdownMenuLabel>
            <DropdownMenuSeparator />
            {table
              .getAllColumns()
              .filter((column) => column.getCanHide())
              .map((column) => (
                <DropdownMenuCheckboxItem
                  key={column.id}
                  checked={column.getIsVisible()}
                  onCheckedChange={(checked) => column.toggleVisibility(Boolean(checked))}
                >
                  {column.id}
                </DropdownMenuCheckboxItem>
              ))}
          </DropdownMenuContent>
        </DropdownMenu>
      </div>
    </div>
  );
}
