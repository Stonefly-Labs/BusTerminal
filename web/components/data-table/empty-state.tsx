import * as React from "react";
import { Inbox } from "lucide-react";

import { cn } from "@/lib/design-system/cn";
import { t } from "@/lib/i18n";

export interface DataTableEmptyStateProps {
  readonly title?: string;
  readonly description?: string;
  readonly action?: React.ReactNode;
  readonly className?: string;
}

export function DataTableEmptyState({
  title,
  description,
  action,
  className,
}: DataTableEmptyStateProps) {
  return (
    <div
      className={cn(
        "flex flex-col items-center justify-center gap-2 rounded-md border border-dashed border-border-muted bg-surface-muted p-8 text-center",
        className,
      )}
      role="status"
    >
      <Inbox className="h-6 w-6 text-foreground-subtle" aria-hidden="true" />
      <p className="text-sm font-medium text-foreground-default">
        {title ?? t("table.empty.title")}
      </p>
      <p className="text-xs text-foreground-muted">
        {description ?? t("table.empty.description")}
      </p>
      {action ? <div className="mt-2">{action}</div> : null}
    </div>
  );
}
