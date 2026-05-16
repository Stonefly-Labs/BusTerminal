"use client";

import { ChevronLeft, ChevronRight } from "lucide-react";
import type { Table } from "@tanstack/react-table";

import { Button } from "@/components/ui/button";
import { t } from "@/lib/i18n";
import { cn } from "@/lib/design-system/cn";

export interface DataTablePaginationProps<TData> {
  readonly table: Table<TData>;
  readonly className?: string;
}

export function DataTablePagination<TData>({ table, className }: DataTablePaginationProps<TData>) {
  const pageIndex = table.getState().pagination.pageIndex;
  const pageCount = Math.max(table.getPageCount(), 1);
  return (
    <div
      className={cn(
        "flex items-center justify-between gap-3 border-t border-border-muted px-3 py-2 text-xs text-foreground-muted",
        className,
      )}
    >
      <div>{t("navigation.pagination.pageStatus", { page: pageIndex + 1, total: pageCount })}</div>
      <div className="flex gap-2">
        <Button
          intent="ghost"
          size="sm"
          onClick={() => table.previousPage()}
          disabled={!table.getCanPreviousPage()}
          aria-label={t("navigation.pagination.previous")}
        >
          <ChevronLeft className="rtl:rotate-180" />
          {t("navigation.pagination.previous")}
        </Button>
        <Button
          intent="ghost"
          size="sm"
          onClick={() => table.nextPage()}
          disabled={!table.getCanNextPage()}
          aria-label={t("navigation.pagination.next")}
        >
          {t("navigation.pagination.next")}
          <ChevronRight className="rtl:rotate-180" />
        </Button>
      </div>
    </div>
  );
}
