"use client";

import { Slot } from "@radix-ui/react-slot";
import * as React from "react";

import { cn } from "@/lib/design-system/cn";
import { variants, type VariantPropsOf } from "@/lib/design-system/variants";

const buttonVariants = variants(
  cn(
    "inline-flex items-center justify-center gap-2 whitespace-nowrap font-medium",
    "rounded-md select-none",
    "transition-colors",
    "outline-none focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--focus-ring-color)",
    "disabled:pointer-events-none disabled:bg-disabled-surface disabled:text-disabled-foreground",
    "[&_svg]:pointer-events-none [&_svg]:shrink-0 [&_svg]:size-4",
  ),
  {
    variants: {
      intent: {
        primary: cn(
          "bg-accent-primary text-accent-primary-foreground",
          "hover:bg-accent-hover active:bg-accent-active",
        ),
        secondary: cn(
          "bg-surface-elevated text-foreground-default",
          "border border-border-default",
          "hover:bg-interactive-hover",
        ),
        outline: cn(
          "border border-border-default bg-transparent text-foreground-default",
          "hover:bg-interactive-hover",
        ),
        ghost: cn(
          "bg-transparent text-foreground-default",
          "hover:bg-interactive-hover",
        ),
        destructive: cn(
          "bg-error-surface text-error-foreground",
          "hover:opacity-90 active:opacity-80",
        ),
        link: cn(
          "bg-transparent text-accent-primary underline-offset-4",
          "hover:underline",
        ),
      },
      size: {
        sm: "h-8 px-3 text-xs",
        md: "h-9 px-4 text-sm",
        lg: "h-10 px-6 text-sm",
        icon: "h-9 w-9 p-0",
      },
    },
    defaultVariants: {
      intent: "primary",
      size: "md",
    },
  },
);

export type ButtonVariants = VariantPropsOf<typeof buttonVariants>;

export interface ButtonProps
  extends React.ButtonHTMLAttributes<HTMLButtonElement>,
    ButtonVariants {
  readonly asChild?: boolean;
}

export const Button = React.forwardRef<HTMLButtonElement, ButtonProps>(
  function Button({ className, intent, size, asChild = false, type = "button", ...rest }, ref) {
    const Component = asChild ? Slot : "button";
    return (
      <Component
        ref={ref}
        type={asChild ? undefined : type}
        className={cn(buttonVariants({ intent, size }), className)}
        {...rest}
      />
    );
  },
);

export { buttonVariants };
