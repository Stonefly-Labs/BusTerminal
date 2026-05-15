import * as React from "react";

import { cn } from "@/lib/design-system/cn";

export const Table = React.forwardRef<HTMLTableElement, React.HTMLAttributes<HTMLTableElement>>(
  function Table({ className, ...rest }, ref) {
    return (
      <div className="w-full overflow-auto">
        <table ref={ref} className={cn("w-full caption-bottom text-sm", className)} {...rest} />
      </div>
    );
  },
);

export const TableHeader = React.forwardRef<HTMLTableSectionElement, React.HTMLAttributes<HTMLTableSectionElement>>(
  function TableHeader({ className, ...rest }, ref) {
    return (
      <thead
        ref={ref}
        className={cn("sticky top-0 z-10 bg-surface-elevated [&_tr]:border-b [&_tr]:border-border-default", className)}
        {...rest}
      />
    );
  },
);

export const TableBody = React.forwardRef<HTMLTableSectionElement, React.HTMLAttributes<HTMLTableSectionElement>>(
  function TableBody({ className, ...rest }, ref) {
    return <tbody ref={ref} className={cn("[&_tr:last-child]:border-0", className)} {...rest} />;
  },
);

export const TableFooter = React.forwardRef<HTMLTableSectionElement, React.HTMLAttributes<HTMLTableSectionElement>>(
  function TableFooter({ className, ...rest }, ref) {
    return (
      <tfoot
        ref={ref}
        className={cn("border-t border-border-default bg-surface-muted/50 font-medium", className)}
        {...rest}
      />
    );
  },
);

export const TableRow = React.forwardRef<HTMLTableRowElement, React.HTMLAttributes<HTMLTableRowElement>>(
  function TableRow({ className, ...rest }, ref) {
    return (
      <tr
        ref={ref}
        className={cn(
          "border-b border-border-default transition-colors",
          "hover:bg-interactive-hover data-[state=selected]:bg-accent-primary/10",
          className,
        )}
        {...rest}
      />
    );
  },
);

export const TableHead = React.forwardRef<HTMLTableCellElement, React.ThHTMLAttributes<HTMLTableCellElement>>(
  function TableHead({ className, ...rest }, ref) {
    return (
      <th
        ref={ref}
        className={cn(
          "h-10 px-3 text-start align-middle text-xs font-semibold uppercase tracking-wide text-foreground-muted",
          "[&:has([role=checkbox])]:w-12",
          className,
        )}
        {...rest}
      />
    );
  },
);

export interface TableCellProps
  extends React.TdHTMLAttributes<HTMLTableCellElement> {
  /**
   * Render cell content with the monospace family (var(--font-mono)).
   * Use for technical-identifier columns — queue/topic/subscription/
   * namespace names, correlation IDs (FR-009).
   */
  readonly mono?: boolean;
}

export const TableCell = React.forwardRef<HTMLTableCellElement, TableCellProps>(
  function TableCell({ className, mono = false, ...rest }, ref) {
    return (
      <td
        ref={ref}
        className={cn(
          "p-3 align-middle text-sm text-foreground-default [&:has([role=checkbox])]:w-12",
          mono && "font-mono",
          className,
        )}
        {...rest}
      />
    );
  },
);

export const TableCaption = React.forwardRef<HTMLTableCaptionElement, React.HTMLAttributes<HTMLTableCaptionElement>>(
  function TableCaption({ className, ...rest }, ref) {
    return <caption ref={ref} className={cn("mt-3 text-xs text-foreground-muted", className)} {...rest} />;
  },
);
