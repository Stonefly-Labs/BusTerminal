"use client";

import * as React from "react";

import { cn } from "@/lib/design-system/cn";

export type TextareaProps = React.TextareaHTMLAttributes<HTMLTextAreaElement>;

export const Textarea = React.forwardRef<HTMLTextAreaElement, TextareaProps>(
  function Textarea({ className, rows = 4, ...rest }, ref) {
    return (
      <textarea
        ref={ref}
        rows={rows}
        className={cn(
          "flex w-full min-w-0 rounded-md border border-border-default bg-surface-elevated px-3 py-2 text-sm",
          "text-foreground-default placeholder:text-foreground-subtle",
          "transition-colors resize-y",
          "focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--focus-ring-color)",
          "disabled:cursor-not-allowed disabled:bg-disabled-surface disabled:text-disabled-foreground",
          "aria-invalid:border-error-foreground aria-invalid:focus-visible:outline-error-foreground",
          className,
        )}
        {...rest}
      />
    );
  },
);
