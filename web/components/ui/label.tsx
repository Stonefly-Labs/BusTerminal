"use client";

import * as React from "react";
import * as LabelPrimitive from "@radix-ui/react-label";

import { cn } from "@/lib/design-system/cn";

export const Label = React.forwardRef<
  React.ElementRef<typeof LabelPrimitive.Root>,
  React.ComponentPropsWithoutRef<typeof LabelPrimitive.Root>
>(function Label({ className, ...rest }, ref) {
  return (
    <LabelPrimitive.Root
      ref={ref}
      className={cn(
        "inline-flex items-center gap-1 text-sm font-medium text-foreground-default",
        "peer-disabled:cursor-not-allowed peer-disabled:opacity-70",
        className,
      )}
      {...rest}
    />
  );
});
