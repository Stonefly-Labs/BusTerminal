import * as React from "react";

import { cn } from "@/lib/design-system/cn";

export interface PageHeaderProps extends React.HTMLAttributes<HTMLDivElement> {
  readonly title: string;
  readonly description?: string;
  readonly breadcrumb?: React.ReactNode;
  readonly actions?: React.ReactNode;
}

export function PageHeader({
  title,
  description,
  breadcrumb,
  actions,
  className,
  ...rest
}: PageHeaderProps) {
  return (
    <div
      className={cn("flex flex-col gap-3 border-b border-border-muted pb-4", className)}
      {...rest}
    >
      {breadcrumb}
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="flex flex-col gap-1">
          <h1 className="text-2xl font-semibold leading-tight text-foreground-default">{title}</h1>
          {description ? (
            <p className="text-sm text-foreground-muted">{description}</p>
          ) : null}
        </div>
        {actions ? <div className="flex items-center gap-2">{actions}</div> : null}
      </div>
    </div>
  );
}
