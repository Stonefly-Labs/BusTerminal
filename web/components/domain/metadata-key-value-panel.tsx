import * as React from "react";

import { cn } from "@/lib/design-system/cn";
import { t } from "@/lib/i18n/strings";

export interface MetadataEntry {
  readonly key: string;
  readonly value: React.ReactNode;
  /** Render the value in the monospace family. Defaults to `true` because
   *  metadata values are typically technical identifiers (FR-009). */
  readonly mono?: boolean;
}

export interface MetadataKeyValuePanelProps
  extends Omit<React.HTMLAttributes<HTMLDListElement>, "children"> {
  readonly entries: ReadonlyArray<MetadataEntry>;
}

/**
 * Accessible definition list pairing metadata keys with values. Empty entry
 * sets render the documented `domain.metadata.empty` copy so callers don't
 * need to short-circuit. Values default to monospace because they are
 * typically technical identifiers.
 */
export const MetadataKeyValuePanel = React.forwardRef<HTMLDListElement, MetadataKeyValuePanelProps>(
  function MetadataKeyValuePanel({ entries, className, ...rest }, ref) {
    if (entries.length === 0) {
      return (
        <p
          className={cn(
            "rounded-md border border-dashed border-border-default bg-surface-muted px-3 py-2 text-sm text-foreground-muted",
            className,
          )}
        >
          {t("domain.metadata.empty")}
        </p>
      );
    }
    return (
      <dl
        ref={ref}
        className={cn(
          "grid grid-cols-[max-content_1fr] gap-x-4 gap-y-2 rounded-md border border-border-default bg-surface-elevated p-3 text-sm",
          className,
        )}
        {...rest}
      >
        {entries.map((entry) => (
          <React.Fragment key={entry.key}>
            <dt className="text-xs uppercase tracking-wide text-foreground-muted">{entry.key}</dt>
            <dd
              className={cn(
                "min-w-0 break-words text-foreground-default",
                entry.mono === false ? undefined : "font-mono",
              )}
            >
              {entry.value}
            </dd>
          </React.Fragment>
        ))}
      </dl>
    );
  },
);
