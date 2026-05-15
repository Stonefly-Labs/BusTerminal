import * as React from "react";

import { cn } from "@/lib/design-system/cn";
import { variants, type VariantPropsOf } from "@/lib/design-system/variants";

const badgeVariants = variants(
  cn(
    "inline-flex items-center gap-1 rounded-full border px-2.5 py-0.5 text-xs font-medium",
    "transition-colors",
    "focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--focus-ring-color)",
  ),
  {
    variants: {
      intent: {
        neutral: "border-border-default bg-surface-muted text-foreground-default",
        accent: "border-transparent bg-accent-primary text-accent-primary-foreground",
        success: "border-transparent bg-success-surface text-success-foreground",
        warning: "border-transparent bg-warning-surface text-warning-foreground",
        error: "border-transparent bg-error-surface text-error-foreground",
        info: "border-transparent bg-info-surface text-info-foreground",
        outline: "border-border-default bg-transparent text-foreground-default",
      },
    },
    defaultVariants: {
      intent: "neutral",
    },
  },
);

export type BadgeVariants = VariantPropsOf<typeof badgeVariants>;

export interface BadgeProps
  extends React.HTMLAttributes<HTMLSpanElement>,
    BadgeVariants {}

export function Badge({ className, intent, ...rest }: BadgeProps) {
  return <span className={cn(badgeVariants({ intent }), className)} {...rest} />;
}

export { badgeVariants };
