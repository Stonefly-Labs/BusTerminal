import { AlertCircle } from "lucide-react";

import { Button } from "@/components/ui/button";
import { cn } from "@/lib/design-system/cn";
import { t } from "@/lib/i18n";

export interface DataTableErrorStateProps {
  readonly message?: string;
  readonly retry?: () => void;
  readonly className?: string;
}

export function DataTableErrorState({ message, retry, className }: DataTableErrorStateProps) {
  return (
    <div
      role="alert"
      className={cn(
        "flex flex-col items-center justify-center gap-2 rounded-md border border-error-foreground/30 bg-error-surface p-8 text-center text-error-foreground",
        className,
      )}
    >
      <AlertCircle className="h-6 w-6" aria-hidden="true" />
      <p className="text-sm font-medium">{t("table.error.title")}</p>
      <p className="text-xs">{message ?? t("table.error.description")}</p>
      {retry ? (
        <Button intent="secondary" size="sm" onClick={retry} className="mt-2">
          {t("table.error.retry")}
        </Button>
      ) : null}
    </div>
  );
}
