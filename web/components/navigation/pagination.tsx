"use client";

import { ChevronLeft, ChevronRight } from "lucide-react";

import { Button } from "@/components/ui/button";
import { cn } from "@/lib/design-system/cn";
import { t } from "@/lib/i18n";

export interface PaginationProps {
  readonly page: number;
  readonly totalPages: number;
  readonly onPageChange: (next: number) => void;
  readonly className?: string;
}

/**
 * Standalone Pagination composite (T087). Distinct from
 * `data-table/pagination.tsx`, which is wired to a TanStack table.
 */
export function Pagination({ page, totalPages, onPageChange, className }: PaginationProps) {
  const safeTotal = Math.max(totalPages, 1);
  const safePage = Math.min(Math.max(page, 1), safeTotal);
  return (
    <nav
      aria-label="Pagination"
      className={cn("flex items-center justify-between gap-3 text-xs text-foreground-muted", className)}
    >
      <span>{t("navigation.pagination.pageStatus", { page: safePage, total: safeTotal })}</span>
      <div className="flex gap-2">
        <Button
          intent="ghost"
          size="sm"
          onClick={() => onPageChange(safePage - 1)}
          disabled={safePage <= 1}
        >
          <ChevronLeft className="rtl:rotate-180" />
          {t("navigation.pagination.previous")}
        </Button>
        <Button
          intent="ghost"
          size="sm"
          onClick={() => onPageChange(safePage + 1)}
          disabled={safePage >= safeTotal}
        >
          {t("navigation.pagination.next")}
          <ChevronRight className="rtl:rotate-180" />
        </Button>
      </div>
    </nav>
  );
}
