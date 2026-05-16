import * as React from "react";
import {
  AlertCircle,
  AlertTriangle,
  CheckCircle2,
  Info,
  type LucideIcon,
} from "lucide-react";

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

/**
 * Maps the four semantic intents to their default Lucide icon. Aligns with the
 * Alert primitive (T060) so success/warning/error/info read identically wherever
 * they appear — satisfies FR-026 (semantic states convey meaning via color +
 * icon + text, not color alone).
 */
const SEMANTIC_ICONS: Record<"success" | "warning" | "error" | "info", LucideIcon> = {
  success: CheckCircle2,
  warning: AlertTriangle,
  error: AlertCircle,
  info: Info,
};

export interface BadgeProps
  extends React.HTMLAttributes<HTMLSpanElement>,
    BadgeVariants {
  /**
   * Render an icon affordance alongside the badge label. Defaults to the
   * canonical semantic icon for `success` / `warning` / `error` / `info`; pass
   * a custom Lucide component to override, or `false` to suppress entirely.
   * Neutral / accent / outline intents do not auto-render an icon.
   */
  readonly icon?: LucideIcon | false;
}

export function Badge({ className, intent, icon, children, ...rest }: BadgeProps) {
  const semanticIcon =
    intent && intent !== "neutral" && intent !== "accent" && intent !== "outline"
      ? SEMANTIC_ICONS[intent]
      : undefined;
  const Icon = icon === false ? undefined : icon ?? semanticIcon;
  return (
    <span className={cn(badgeVariants({ intent }), className)} {...rest}>
      {Icon ? <Icon className="size-3" aria-hidden="true" /> : null}
      {children}
    </span>
  );
}

export { badgeVariants };
