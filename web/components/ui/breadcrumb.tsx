import * as React from "react";
import { Slot } from "@radix-ui/react-slot";
import { ChevronRight, MoreHorizontal } from "lucide-react";

import { cn } from "@/lib/design-system/cn";

export const Breadcrumb = React.forwardRef<HTMLElement, React.HTMLAttributes<HTMLElement>>(
  function Breadcrumb({ className, ...rest }, ref) {
    return (
      <nav
        ref={ref}
        aria-label="Breadcrumb"
        className={cn("text-sm text-foreground-muted", className)}
        {...rest}
      />
    );
  },
);

export const BreadcrumbList = React.forwardRef<HTMLOListElement, React.OlHTMLAttributes<HTMLOListElement>>(
  function BreadcrumbList({ className, ...rest }, ref) {
    return (
      <ol
        ref={ref}
        className={cn("flex flex-wrap items-center gap-1.5 break-words", className)}
        {...rest}
      />
    );
  },
);

export const BreadcrumbItem = React.forwardRef<HTMLLIElement, React.LiHTMLAttributes<HTMLLIElement>>(
  function BreadcrumbItem({ className, ...rest }, ref) {
    return (
      <li ref={ref} className={cn("inline-flex items-center gap-1.5", className)} {...rest} />
    );
  },
);

export interface BreadcrumbLinkProps extends React.AnchorHTMLAttributes<HTMLAnchorElement> {
  readonly asChild?: boolean;
}

export const BreadcrumbLink = React.forwardRef<HTMLAnchorElement, BreadcrumbLinkProps>(
  function BreadcrumbLink({ className, asChild = false, ...rest }, ref) {
    const Component = asChild ? Slot : "a";
    return (
      <Component
        ref={ref}
        className={cn(
          "transition-colors hover:text-foreground-default",
          "focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--focus-ring-color)",
          className,
        )}
        {...rest}
      />
    );
  },
);

export const BreadcrumbPage = React.forwardRef<HTMLSpanElement, React.HTMLAttributes<HTMLSpanElement>>(
  function BreadcrumbPage({ className, ...rest }, ref) {
    return (
      <span
        ref={ref}
        role="link"
        aria-current="page"
        aria-disabled="true"
        className={cn("font-medium text-foreground-default", className)}
        {...rest}
      />
    );
  },
);

export function BreadcrumbSeparator({ className, ...rest }: React.LiHTMLAttributes<HTMLLIElement>) {
  return (
    <li role="presentation" aria-hidden="true" className={cn("[&>svg]:size-3.5", className)} {...rest}>
      <ChevronRight className="rtl:rotate-180" />
    </li>
  );
}

export function BreadcrumbEllipsis({ className, ...rest }: React.HTMLAttributes<HTMLSpanElement>) {
  return (
    <span
      role="presentation"
      aria-hidden="true"
      className={cn("flex h-9 w-9 items-center justify-center", className)}
      {...rest}
    >
      <MoreHorizontal className="size-4" />
    </span>
  );
}
