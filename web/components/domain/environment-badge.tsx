import * as React from "react";

import { cn } from "@/lib/design-system/cn";
import { t } from "@/lib/i18n/strings";
import { getDomainIcon } from "@/lib/iconography/domain-icons";

export type Environment = "dev" | "test" | "staging" | "prod";

export interface EnvironmentBadgeProps
  extends Omit<React.HTMLAttributes<HTMLSpanElement>, "children"> {
  readonly environment: Environment;
  readonly size?: "sm" | "md";
}

const ENV_LABEL_KEY = {
  dev: "domain.environment.dev",
  test: "domain.environment.test",
  staging: "domain.environment.staging",
  prod: "domain.environment.prod",
} as const satisfies Record<
  Environment,
  | "domain.environment.dev"
  | "domain.environment.test"
  | "domain.environment.staging"
  | "domain.environment.prod"
>;

const ENV_SURFACE = {
  dev: "border-info-foreground/30 bg-info-surface text-info-foreground",
  test: "border-border-default bg-surface-muted text-foreground-default",
  staging: "border-warning-foreground/30 bg-warning-surface text-warning-foreground",
  prod: "border-error-foreground/30 bg-error-surface text-error-foreground",
} as const satisfies Record<Environment, string>;

export const EnvironmentBadge = React.forwardRef<HTMLSpanElement, EnvironmentBadgeProps>(
  function EnvironmentBadge({ environment, size = "md", className, ...rest }, ref) {
    const { icon: EnvironmentIcon, strokeWidth } = getDomainIcon("environment");
    const envLabel = t(ENV_LABEL_KEY[environment]);
    return (
      <span
        ref={ref}
        role="status"
        aria-label={t("domain.environment.announce", { environment: envLabel })}
        className={cn(
          "inline-flex items-center gap-1.5 rounded-md border px-2 py-0.5 font-medium uppercase tracking-wide",
          size === "sm" ? "text-xs" : "text-sm",
          ENV_SURFACE[environment],
          className,
        )}
        {...rest}
      >
        <EnvironmentIcon
          aria-hidden="true"
          strokeWidth={strokeWidth}
          className={size === "sm" ? "size-3" : "size-3.5"}
        />
        <span>{envLabel}</span>
      </span>
    );
  },
);
