import * as React from "react";

import { cn } from "@/lib/design-system/cn";

/**
 * Layout-stable shimmer placeholder (FR-019 / SC-019 CLS).
 *
 * Skeleton must be sized by its consumer so it occupies the same space the
 * eventual content will, eliminating layout shift on data load. Animation
 * honors `prefers-reduced-motion` via the global CSS rule.
 */
export function Skeleton({ className, ...rest }: React.HTMLAttributes<HTMLDivElement>) {
  return (
    <div
      role="presentation"
      aria-hidden="true"
      className={cn("animate-pulse rounded-md bg-surface-muted", className)}
      {...rest}
    />
  );
}
