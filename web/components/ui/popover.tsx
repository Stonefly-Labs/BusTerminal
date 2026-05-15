"use client";

import * as React from "react";
import * as PopoverPrimitive from "@radix-ui/react-popover";

import { cn } from "@/lib/design-system/cn";

export const Popover = PopoverPrimitive.Root;
export const PopoverTrigger = PopoverPrimitive.Trigger;
export const PopoverAnchor = PopoverPrimitive.Anchor;

export const PopoverContent = React.forwardRef<
  React.ElementRef<typeof PopoverPrimitive.Content>,
  React.ComponentPropsWithoutRef<typeof PopoverPrimitive.Content>
>(function PopoverContent({ className, align = "center", sideOffset = 4, ...rest }, ref) {
  return (
    <PopoverPrimitive.Portal>
      <PopoverPrimitive.Content
        ref={ref}
        align={align}
        sideOffset={sideOffset}
        className={cn(
          "z-50 w-72 rounded-md border border-border-default bg-surface-overlay p-4 text-foreground-default shadow-elevation-2",
          "outline-none focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--focus-ring-color)",
          "data-[state=open]:animate-in data-[state=closed]:animate-out",
          className,
        )}
        {...rest}
      />
    </PopoverPrimitive.Portal>
  );
});
