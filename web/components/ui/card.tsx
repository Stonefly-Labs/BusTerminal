import * as React from "react";

import { cn } from "@/lib/design-system/cn";

export const Card = React.forwardRef<HTMLDivElement, React.HTMLAttributes<HTMLDivElement>>(
  function Card({ className, ...rest }, ref) {
    return (
      <div
        ref={ref}
        className={cn(
          "rounded-lg border border-border-default bg-surface-elevated text-foreground-default shadow-elevation-1",
          className,
        )}
        {...rest}
      />
    );
  },
);

export const CardHeader = React.forwardRef<HTMLDivElement, React.HTMLAttributes<HTMLDivElement>>(
  function CardHeader({ className, ...rest }, ref) {
    return <div ref={ref} className={cn("flex flex-col gap-1.5 p-4", className)} {...rest} />;
  },
);

export const CardTitle = React.forwardRef<HTMLHeadingElement, React.HTMLAttributes<HTMLHeadingElement>>(
  function CardTitle({ className, ...rest }, ref) {
    return (
      <h3
        ref={ref}
        className={cn("text-base font-semibold leading-tight text-foreground-default", className)}
        {...rest}
      />
    );
  },
);

export const CardDescription = React.forwardRef<HTMLParagraphElement, React.HTMLAttributes<HTMLParagraphElement>>(
  function CardDescription({ className, ...rest }, ref) {
    return <p ref={ref} className={cn("text-sm text-foreground-muted", className)} {...rest} />;
  },
);

export const CardContent = React.forwardRef<HTMLDivElement, React.HTMLAttributes<HTMLDivElement>>(
  function CardContent({ className, ...rest }, ref) {
    return <div ref={ref} className={cn("p-4 pt-0", className)} {...rest} />;
  },
);

export const CardFooter = React.forwardRef<HTMLDivElement, React.HTMLAttributes<HTMLDivElement>>(
  function CardFooter({ className, ...rest }, ref) {
    return <div ref={ref} className={cn("flex items-center gap-2 p-4 pt-0", className)} {...rest} />;
  },
);
