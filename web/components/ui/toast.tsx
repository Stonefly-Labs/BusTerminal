"use client";

import { Toaster as SonnerToaster, toast as sonnerToast, type ToasterProps } from "sonner";
import { useTheme } from "next-themes";

import { cn } from "@/lib/design-system/cn";

export type ToasterMountProps = Omit<ToasterProps, "theme">;

/**
 * Mount the Sonner toaster once near the app root. Theme is sourced from
 * `next-themes` so toasts honor the active color scheme.
 */
export function Toaster({ className, position = "bottom-right", ...rest }: ToasterMountProps) {
  const { resolvedTheme } = useTheme();
  return (
    <SonnerToaster
      theme={(resolvedTheme as "light" | "dark" | undefined) ?? "system"}
      position={position}
      className={cn("font-sans", className)}
      toastOptions={{
        classNames: {
          toast: cn(
            "border border-border-default bg-surface-overlay text-foreground-default shadow-elevation-2",
            "rounded-md p-3 text-sm",
          ),
          title: "font-medium text-foreground-default",
          description: "text-foreground-muted",
          actionButton:
            "bg-accent-primary text-accent-primary-foreground rounded-md px-2 py-1 text-xs",
          cancelButton:
            "bg-transparent text-foreground-muted rounded-md px-2 py-1 text-xs",
        },
      }}
      {...rest}
    />
  );
}

export const toast = sonnerToast;
