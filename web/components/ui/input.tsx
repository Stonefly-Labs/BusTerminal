"use client";

import * as React from "react";

import { cn } from "@/lib/design-system/cn";

export type InputProps = React.InputHTMLAttributes<HTMLInputElement>;

export const Input = React.forwardRef<HTMLInputElement, InputProps>(
  function Input({ className, type = "text", ...rest }, ref) {
    return (
      <input
        ref={ref}
        type={type}
        className={cn(
          "flex h-9 w-full min-w-0 rounded-md border border-border-default bg-surface-elevated px-3 py-2 text-sm",
          "text-foreground-default placeholder:text-foreground-subtle",
          "transition-colors",
          "focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--focus-ring-color)",
          "disabled:cursor-not-allowed disabled:bg-disabled-surface disabled:text-disabled-foreground",
          "aria-invalid:border-error-foreground aria-invalid:focus-visible:outline-error-foreground",
          "file:me-2 file:border-0 file:bg-transparent file:text-sm file:font-medium file:text-foreground-default",
          className,
        )}
        {...rest}
      />
    );
  },
);
