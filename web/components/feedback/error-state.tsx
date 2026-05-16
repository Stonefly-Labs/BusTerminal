import * as React from "react";
import { AlertCircle } from "lucide-react";

import { cn } from "@/lib/design-system/cn";
import { t } from "@/lib/i18n";
import type { StringKey } from "@/lib/i18n";

export interface ErrorStateProps {
  readonly titleKey?: StringKey;
  readonly descriptionKey?: StringKey;
  readonly action?: React.ReactNode;
  readonly className?: string;
}

/**
 * Generic error-state surface (T083 / FR-020). Pair with
 * `RetryAffordance` when retry is supported.
 */
export function ErrorState({
  titleKey = "feedback.error.defaultTitle",
  descriptionKey = "feedback.error.defaultDescription",
  action,
  className,
}: ErrorStateProps) {
  return (
    <div
      role="alert"
      className={cn(
        "flex flex-col items-center justify-center gap-2 rounded-md border border-error-foreground/30 bg-error-surface p-8 text-center text-error-foreground",
        className,
      )}
    >
      <AlertCircle className="h-6 w-6" aria-hidden="true" />
      <p className="text-sm font-medium">{t(titleKey)}</p>
      <p className="text-xs">{t(descriptionKey)}</p>
      {action ? <div className="mt-2">{action}</div> : null}
    </div>
  );
}
