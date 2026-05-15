"use client";

import * as React from "react";
import { Toaster as SonnerToaster, toast as sonnerToast, type ToasterProps } from "sonner";
import { useTheme } from "next-themes";

import { cn } from "@/lib/design-system/cn";

export type ToasterMountProps = Omit<ToasterProps, "theme">;

/**
 * Mount the Sonner toaster once near the app root. Theme is sourced from
 * `next-themes` so toasts honor the active color scheme. Theme reading is
 * gated on mount because `resolvedTheme` is undefined during SSR — without
 * the gate, the server emits `theme="system"` while the client immediately
 * resolves to `dark`/`light`, triggering a hydration warning.
 */
export function Toaster({ className, position = "bottom-right", ...rest }: ToasterMountProps) {
  const { resolvedTheme } = useTheme();
  // `useSyncExternalStore` returns the server snapshot (`false`) during SSR
  // and the client snapshot (`true`) on hydration — without calling setState
  // inside an effect (avoids the `react-hooks/set-state-in-effect` rule).
  const mounted = React.useSyncExternalStore(
    () => () => {},
    () => true,
    () => false,
  );
  const theme: "light" | "dark" | "system" = mounted
    ? ((resolvedTheme as "light" | "dark" | undefined) ?? "system")
    : "system";
  return (
    <SonnerToaster
      theme={theme}
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
