import * as React from "react";
import { AlertCircle, AlertTriangle, CheckCircle2, Info } from "lucide-react";

import { cn } from "@/lib/design-system/cn";
import { variants, type VariantPropsOf } from "@/lib/design-system/variants";

const alertVariants = variants(
  cn(
    "relative w-full rounded-md border p-4",
    "[&>svg]:absolute [&>svg]:start-4 [&>svg]:top-4 [&>svg]:size-4",
    "[&>svg~*]:ps-7",
  ),
  {
    variants: {
      intent: {
        info: "border-info-foreground/30 bg-info-surface text-info-foreground",
        success: "border-success-foreground/30 bg-success-surface text-success-foreground",
        warning: "border-warning-foreground/30 bg-warning-surface text-warning-foreground",
        error: "border-error-foreground/30 bg-error-surface text-error-foreground",
      },
    },
    defaultVariants: {
      intent: "info",
    },
  },
);

export type AlertVariants = VariantPropsOf<typeof alertVariants>;

const ICONS = {
  info: Info,
  success: CheckCircle2,
  warning: AlertTriangle,
  error: AlertCircle,
} as const;

export interface AlertProps
  extends React.HTMLAttributes<HTMLDivElement>,
    AlertVariants {}

export const Alert = React.forwardRef<HTMLDivElement, AlertProps>(
  function Alert({ className, intent = "info", children, ...rest }, ref) {
    const IconComponent = ICONS[intent ?? "info"];
    return (
      <div ref={ref} role="alert" className={cn(alertVariants({ intent }), className)} {...rest}>
        <IconComponent aria-hidden="true" />
        {children}
      </div>
    );
  },
);

export const AlertTitle = React.forwardRef<HTMLHeadingElement, React.HTMLAttributes<HTMLHeadingElement>>(
  function AlertTitle({ className, ...rest }, ref) {
    return <h5 ref={ref} className={cn("mb-1 text-sm font-semibold leading-none", className)} {...rest} />;
  },
);

export const AlertDescription = React.forwardRef<HTMLDivElement, React.HTMLAttributes<HTMLDivElement>>(
  function AlertDescription({ className, ...rest }, ref) {
    return <div ref={ref} className={cn("text-sm leading-snug", className)} {...rest} />;
  },
);
