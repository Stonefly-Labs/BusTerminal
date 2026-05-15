import { Skeleton } from "@/components/ui/skeleton";
import { cn } from "@/lib/design-system/cn";
import { t } from "@/lib/i18n";

export interface DataTableLoadingStateProps {
  readonly rows?: number;
  readonly columns?: number;
  readonly className?: string;
}

export function DataTableLoadingState({ rows = 5, columns = 4, className }: DataTableLoadingStateProps) {
  return (
    <div className={cn("flex flex-col gap-2 p-4", className)} aria-busy="true" aria-live="polite">
      <span className="sr-only">{t("table.loading.label")}</span>
      {Array.from({ length: rows }).map((_, rowIndex) => (
        <div key={rowIndex} className="flex gap-3">
          {Array.from({ length: columns }).map((_, columnIndex) => (
            <Skeleton key={columnIndex} className="h-6 flex-1" />
          ))}
        </div>
      ))}
    </div>
  );
}
