import * as React from "react";

import { cn } from "@/lib/design-system/cn";
import { t } from "@/lib/i18n/strings";
import { getDomainIcon } from "@/lib/iconography/domain-icons";

export interface TopologyMiniMapPlaceholderProps
  extends Omit<React.HTMLAttributes<HTMLDivElement>, "children" | "title"> {
  /**
   * Reserved height for the placeholder. The default keeps layout stable when
   * the topology visualization ships in a future spec (FR-028).
   */
  readonly height?: number | string;
}

/**
 * Inert placeholder reserving the layout slot the topology mini-map will
 * occupy in a future spec. Renders no interactive affordances and announces
 * itself to assistive tech as informational only — exposing future-work copy
 * inline is intentional so reviewers spot the deferred surface.
 */
export const TopologyMiniMapPlaceholder = React.forwardRef<
  HTMLDivElement,
  TopologyMiniMapPlaceholderProps
>(function TopologyMiniMapPlaceholder({ height = 220, className, style, ...rest }, ref) {
  const { icon: TopologyIcon, strokeWidth } = getDomainIcon("topology");
  return (
    <div
      ref={ref}
      role="note"
      aria-label={t("domain.topology.placeholderTitle")}
      className={cn(
        "relative flex items-center justify-center overflow-hidden rounded-md border border-dashed border-border-default bg-surface-muted text-center",
        className,
      )}
      style={{ minHeight: height, ...style }}
      {...rest}
    >
      <div className="flex flex-col items-center gap-2 p-4">
        <TopologyIcon
          aria-hidden="true"
          strokeWidth={strokeWidth}
          className="size-8 text-foreground-subtle"
        />
        <p className="text-sm font-medium text-foreground-default">
          {t("domain.topology.placeholderTitle")}
        </p>
        <p className="max-w-md text-xs text-foreground-muted">
          {t("domain.topology.placeholder")}
        </p>
      </div>
    </div>
  );
});
