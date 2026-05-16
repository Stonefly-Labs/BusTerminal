import * as React from "react";
import { Inbox, type LucideIcon } from "lucide-react";

import { cn } from "@/lib/design-system/cn";
import { t } from "@/lib/i18n";
import type { StringKey } from "@/lib/i18n";

export interface EmptyStateProps {
  readonly titleKey?: StringKey;
  readonly descriptionKey?: StringKey;
  readonly icon?: LucideIcon;
  readonly action?: React.ReactNode;
  readonly className?: string;
}

/**
 * Generic empty-state surface (T082 / FR-020). For data-table-specific empty
 * states use `web/components/data-table/empty-state.tsx` instead.
 */
export function EmptyState({
  titleKey = "feedback.empty.defaultTitle",
  descriptionKey = "feedback.empty.defaultDescription",
  icon: Icon = Inbox,
  action,
  className,
}: EmptyStateProps) {
  return (
    <div
      role="status"
      className={cn(
        "flex flex-col items-center justify-center gap-2 rounded-md border border-dashed border-border-muted bg-surface-muted p-8 text-center",
        className,
      )}
    >
      <Icon className="h-6 w-6 text-foreground-subtle" aria-hidden="true" />
      <p className="text-sm font-medium text-foreground-default">{t(titleKey)}</p>
      <p className="text-xs text-foreground-muted">{t(descriptionKey)}</p>
      {action ? <div className="mt-2">{action}</div> : null}
    </div>
  );
}
