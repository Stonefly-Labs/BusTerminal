"use client";

import * as React from "react";
import { Moon, Sun } from "lucide-react";
import { useTheme } from "next-themes";

import { Button } from "@/components/ui/button";
import { cn } from "@/lib/design-system/cn";

export interface NavigationHeaderProps {
  readonly userMenu?: React.ReactNode;
  readonly className?: string;
}

export function NavigationHeader({ userMenu, className }: NavigationHeaderProps) {
  const { resolvedTheme, setTheme } = useTheme();
  const mounted = React.useSyncExternalStore(
    () => () => {},
    () => true,
    () => false,
  );
  const isDark = mounted ? resolvedTheme === "dark" : false;

  return (
    <header
      className={cn(
        "sticky top-0 z-30 flex h-12 items-center gap-3 border-b border-border-default bg-surface-canvas/95 px-4 backdrop-blur",
        className,
      )}
    >
      <span className="flex items-center gap-2 font-semibold tracking-tight text-foreground-default">
        <span aria-hidden className="inline-block size-2 rounded-sm bg-accent-primary" />
        BusTerminal
      </span>
      <span className="ms-auto" />
      <Button
        intent="ghost"
        size="icon"
        onClick={() => setTheme(isDark ? "light" : "dark")}
        aria-label={mounted ? (isDark ? "Switch to light theme" : "Switch to dark theme") : "Toggle theme"}
        suppressHydrationWarning
      >
        <span suppressHydrationWarning>{isDark ? <Sun /> : <Moon />}</span>
      </Button>
      {userMenu}
    </header>
  );
}
